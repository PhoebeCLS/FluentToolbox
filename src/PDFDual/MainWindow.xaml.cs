using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using WinPdf = Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PDFDual
{
    public partial class MainWindow : Window
    {
        private string _layoutMode = "side";
        private string _lastOutputFile = "";
        private bool _isProcessing = false;
        private TaskbarHelper? _taskbar;
        private int _dragCounter = 0;

        private string? _pendingModalFile = null;
        private string _suggestedLang = "cn";
        private readonly Queue<string> _pendingAmbiguousFiles = new();

        // 3D Touch Preview & Paging
        private string? _activePeekPdf = null;
        private uint _activePeekPage = 0;
        private uint _activePeekTotalPages = 1;
        private bool _isRenderingPage = false;
        private readonly Dictionary<string, BitmapSource> _previewCache = new(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _taskbar = new TaskbarHelper(this);
            SmoothScrollHelper.Register(MainScrollViewer);
            ThemeManager.SetTheme(this, "system");

            var args = Environment.GetCommandLineArgs();
            int idx = Array.IndexOf(args, "--screenshot");
            if (idx >= 0)
            {
                string outPath = (idx + 1 < args.Length) ? args[idx + 1] : "pdfdual_preview.jpg";
                SaveWindowScreenshot(outPath);
                Application.Current.Shutdown();
                return;
            }
        }

                private void SaveWindowScreenshot(string path)
        {
            try
            {
                int singleW = 880;
                int singleH = 680;

                // 1. Render Dark Mode
                ThemeManager.SetTheme(this, "dark");
                this.Width = singleW;
                this.Measure(new Size(singleW, double.PositiveInfinity));
                singleH = (int)Math.Max(this.DesiredSize.Height, 660);
                this.Height = singleH;
                this.Arrange(new Rect(0, 0, singleW, singleH));
                this.UpdateLayout();

                var rtbDark = new RenderTargetBitmap(singleW, singleH, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtbDark.Render(this);

                // 2. Render Light Mode
                ThemeManager.SetTheme(this, "light");
                this.UpdateLayout();

                var rtbLight = new RenderTargetBitmap(singleW, singleH, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtbLight.Render(this);

                // 3. Composite Side-by-Side (Scheme A)
                int spacing = 24;
                int totalW = singleW * 2 + spacing;
                int totalH = singleH;

                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18)), null, new Rect(0, 0, totalW, totalH));
                    dc.DrawImage(rtbDark, new Rect(0, 0, singleW, singleH));
                    dc.DrawImage(rtbLight, new Rect(singleW + spacing, 0, singleW, singleH));
                }

                var rtbComposite = new RenderTargetBitmap(totalW, totalH, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtbComposite.Render(dv);

                var encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                encoder.Frames.Add(BitmapFrame.Create(rtbComposite));
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                using var fs = File.Create(path);
                encoder.Save(fs);
            }
            catch (Exception ex)
            {
                File.WriteAllText(path + ".err", ex.ToString());
            }
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                ThemeManager.SetTheme(this, tag);
            }
        }

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _layoutMode = tag;
                if (TxtModeDesc != null)
                {
                    TxtModeDesc.Text = tag == "side"
                        ? "💡 左右并排：横向合并双页宽度，左侧显示中文、右侧显示英文，适合宽屏与双语对照精读。"
                        : "💡 上下并排：纵向合并双页高度，上方显示中文、下方显示英文，适合竖屏与紧凑对照阅读。";
                }
            }
        }

        private void DropArea_Click(object sender, MouseButtonEventArgs e)
        {
            BtnBrowseCn_Click(sender, e);
        }

        // Fullscreen Drag & Drop Handling
        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                _dragCounter++;
                DragOverlay.Visibility = Visibility.Visible;
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            _dragCounter--;
            if (_dragCounter <= 0)
            {
                _dragCounter = 0;
                DragOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            _dragCounter = 0;
            DragOverlay.Visibility = Visibility.Collapsed;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var pdfs = new List<string>();

                foreach (var item in files)
                {
                    if (Directory.Exists(item))
                    {
                        pdfs.AddRange(Directory.EnumerateFiles(item, "*.pdf", SearchOption.AllDirectories));
                    }
                    else if (File.Exists(item) && Path.GetExtension(item).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        pdfs.Add(item);
                    }
                }

                ProcessDroppedFiles(pdfs);
            }
        }

        private void ProcessDroppedFiles(List<string> pdfs)
        {
            if (pdfs.Count == 0) return;

            foreach (var pdf in pdfs)
            {
                _pendingAmbiguousFiles.Enqueue(pdf);
            }

            CheckNextFileRecommendation();
        }

        private void CheckNextFileRecommendation()
        {
            if (_pendingAmbiguousFiles.Count > 0)
            {
                _pendingModalFile = _pendingAmbiguousFiles.Dequeue();
                var fi = new FileInfo(_pendingModalFile);
                TxtModalFileName.Text = $"📄 {fi.Name} ({fi.Length / (1024.0 * 1024.0):F2}MB)";

                var (lang, desc) = FastSniffLanguage(_pendingModalFile);
                _suggestedLang = lang;

                if (lang == "cn")
                {
                    BorderSuggestion.Visibility = Visibility.Visible;
                    BtnAcceptSuggestion.Visibility = Visibility.Visible;
                    TxtSmartSuggestion.Text = $"💡 智能分析建议：检测到文本为【中文】({desc})，建议填入【中文 PDF】，是否采纳？";
                    BtnAcceptSuggestion.Content = "✨ 采纳建议：设为中文 PDF (推荐)";
                }
                else if (lang == "en")
                {
                    BorderSuggestion.Visibility = Visibility.Visible;
                    BtnAcceptSuggestion.Visibility = Visibility.Visible;
                    TxtSmartSuggestion.Text = $"💡 智能分析建议：检测到文本为【英文】({desc})，建议填入【英文 PDF】，是否采纳？";
                    BtnAcceptSuggestion.Content = "✨ 采纳建议：设为英文 PDF (推荐)";
                }
                else
                {
                    BorderSuggestion.Visibility = Visibility.Visible;
                    BtnAcceptSuggestion.Visibility = Visibility.Collapsed;
                    TxtSmartSuggestion.Text = "💡 未能检测到明确文字流（可能为扫描件），请手动指定语言类型：";
                }

                LanguagePickerModal.Visibility = Visibility.Visible;
                AnimateModalShow();
            }
            else
            {
                _pendingModalFile = null;
                LanguagePickerModal.Visibility = Visibility.Collapsed;
            }
        }

        private void AnimateModalShow()
        {
            var animScale = new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var animOpacity = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180));

            ModalCard.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animScale);
            ModalCard.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animScale);
            LanguagePickerModal.BeginAnimation(OpacityProperty, animOpacity);
        }

        private void BtnAcceptSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_pendingModalFile))
            {
                if (_suggestedLang == "en")
                    TxtEnPath.Text = _pendingModalFile;
                else
                    TxtCnPath.Text = _pendingModalFile;
            }
            CheckNextFileRecommendation();
        }

        private void BtnModalSetCn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_pendingModalFile))
            {
                TxtCnPath.Text = _pendingModalFile;
            }
            CheckNextFileRecommendation();
        }

        private void BtnModalSetEn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_pendingModalFile))
            {
                TxtEnPath.Text = _pendingModalFile;
            }
            CheckNextFileRecommendation();
        }

        private void BtnModalCancel_Click(object sender, RoutedEventArgs e)
        {
            _pendingAmbiguousFiles.Clear();
            _pendingModalFile = null;
            LanguagePickerModal.Visibility = Visibility.Collapsed;
        }

        // ==========================================
        // Apple 3D Touch Peek & Pop + Mouse Wheel Paging
        // ==========================================
        private async Task TriggerPeekPreviewAsync(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            _activePeekPdf = filePath;
            _activePeekPage = 0;

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                var pdfDoc = await WinPdf.PdfDocument.LoadFromFileAsync(file);
                _activePeekTotalPages = pdfDoc.PageCount;
            }
            catch
            {
                _activePeekTotalPages = 1;
            }

            TxtPeekTitle.Text = $"📄 {Path.GetFileName(filePath)} (1/{_activePeekTotalPages} 页)";

            string cacheKey = $"{filePath}#0";
            if (_previewCache.TryGetValue(cacheKey, out var cached))
            {
                ImgPeekPreview.Source = cached;
            }
            else
            {
                try
                {
                    var bmp = await RenderPdfPageAsync(filePath, 0, 720);
                    _previewCache[cacheKey] = bmp;
                    ImgPeekPreview.Source = bmp;
                }
                catch
                {
                    return;
                }
            }

            PeekPreviewOverlay.Visibility = Visibility.Visible;

            var animScale = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var animOpacity = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160));

            PeekCard.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animScale);
            PeekCard.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animScale);
            PeekPreviewOverlay.BeginAnimation(OpacityProperty, animOpacity);
        }

        private async void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (PeekPreviewOverlay.Visibility == Visibility.Visible && !string.IsNullOrEmpty(_activePeekPdf))
            {
                e.Handled = true;
                if (_isRenderingPage || _activePeekTotalPages <= 1) return;

                if (e.Delta < 0)
                {
                    if (_activePeekPage < _activePeekTotalPages - 1)
                    {
                        _activePeekPage++;
                        await SwitchPeekPageAsync(_activePeekPage);
                    }
                }
                else if (e.Delta > 0)
                {
                    if (_activePeekPage > 0)
                    {
                        _activePeekPage--;
                        await SwitchPeekPageAsync(_activePeekPage);
                    }
                }
            }
        }

        private async Task SwitchPeekPageAsync(uint pageIndex)
        {
            if (string.IsNullOrEmpty(_activePeekPdf)) return;

            _isRenderingPage = true;
            string cacheKey = $"{_activePeekPdf}#{pageIndex}";
            TxtPeekTitle.Text = $"📄 {Path.GetFileName(_activePeekPdf)} ({pageIndex + 1}/{_activePeekTotalPages} 页)";

            if (_previewCache.TryGetValue(cacheKey, out var cached))
            {
                ImgPeekPreview.Source = cached;
            }
            else
            {
                try
                {
                    var bmp = await RenderPdfPageAsync(_activePeekPdf, pageIndex, 720);
                    _previewCache[cacheKey] = bmp;
                    ImgPeekPreview.Source = bmp;
                }
                catch
                {
                }
            }
            _isRenderingPage = false;
        }

        private void HidePeekPreview()
        {
            if (PeekPreviewOverlay.Visibility == Visibility.Visible)
            {
                var animScale = new DoubleAnimation(1.0, 0.85, TimeSpan.FromMilliseconds(140))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                var animOpacity = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(140));
                animOpacity.Completed += (s, e) =>
                {
                    PeekPreviewOverlay.Visibility = Visibility.Collapsed;
                    _activePeekPdf = null;
                };

                PeekCard.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animScale);
                PeekCard.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animScale);
                PeekPreviewOverlay.BeginAnimation(OpacityProperty, animOpacity);
            }
        }

        private async void BtnModalPeek_Down(object sender, MouseButtonEventArgs e)
        {
            await TriggerPeekPreviewAsync(_pendingModalFile);
        }

        private async void BtnPeekCn_Down(object sender, MouseButtonEventArgs e)
        {
            await TriggerPeekPreviewAsync(TxtCnPath.Text.Trim('\"', '\''));
        }

        private async void BtnPeekEn_Down(object sender, MouseButtonEventArgs e)
        {
            await TriggerPeekPreviewAsync(TxtEnPath.Text.Trim('\"', '\''));
        }

        private void BtnPeek_Up(object sender, RoutedEventArgs e)
        {
            HidePeekPreview();
        }

        public static async Task<BitmapSource> RenderPdfPageAsync(string pdfPath, uint pageIndex = 0, uint targetWidth = 720)
        {
            var file = await StorageFile.GetFileFromPathAsync(pdfPath);
            var pdfDoc = await WinPdf.PdfDocument.LoadFromFileAsync(file);
            using var page = pdfDoc.GetPage(pageIndex);
            using var stream = new InMemoryRandomAccessStream();
            var options = new WinPdf.PdfPageRenderOptions
            {
                DestinationWidth = targetWidth
            };
            await page.RenderToStreamAsync(stream, options);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = stream.AsStreamForRead();
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        // ==========================================
        // Fast Language Sniffer
        // ==========================================
        public static (string lang, string desc) FastSniffLanguage(string pdfPath)
        {
            string fileName = Path.GetFileName(pdfPath);

            // 1. Filename contains ANY Chinese characters (e.g. 测试文件.pdf)
            if (fileName.Any(c => c >= 0x4E00 && c <= 0x9FA5))
                return ("cn", "文件名包含汉字");

            if (Regex.IsMatch(fileName, @"(cn|zh|中文|汉|汉语|简中|繁中)", RegexOptions.IgnoreCase))
                return ("cn", "文件名含中文关键字");

            if (Regex.IsMatch(fileName, @"(en|eng|英文|英语|english)", RegexOptions.IgnoreCase))
                return ("en", "文件名含英文关键字");

            try
            {
                using var fs = File.OpenRead(pdfPath);
                byte[] buffer = new byte[Math.Min(fs.Length, 1024 * 1024)];
                int bytesRead = fs.Read(buffer, 0, buffer.Length);

                string utf8 = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                int cnUtf8 = utf8.Count(c => c >= 0x4E00 && c <= 0x9FA5);
                int enCount = utf8.Count(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

                bool hasChineseFont = Regex.IsMatch(utf8, @"(SimSun|SimHei|MicrosoftYaHei|STSong|AdobeSong|PingFang|KaiTi|FangSong|STHeiti|GBK|GB2312|Identity-H)", RegexOptions.IgnoreCase);

                if (hasChineseFont || cnUtf8 >= 6)
                {
                    return ("cn", hasChineseFont ? "检测到内置中文字体 (SimSun/YaHei)" : $"检测到 {cnUtf8} 个汉字编码");
                }

                if (enCount >= 30 && cnUtf8 == 0)
                {
                    return ("en", $"检测到 {enCount} 个英文文本字符");
                }
            }
            catch
            {
            }

            return ("unknown", "无文字层 (纯图片扫描件)");
        }

        private void BtnBrowseCn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择中文版 PDF 文件",
                Filter = "PDF Document (*.pdf)|*.pdf|所有文件|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtCnPath.Text = dlg.FileName;
            }
        }

        private void BtnBrowseEn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择英文版 PDF 文件",
                Filter = "PDF Document (*.pdf)|*.pdf|所有文件|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtEnPath.Text = dlg.FileName;
            }
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "选择自定义输出文件夹"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtOutputDir.Text = dlg.FolderName;
            }
        }

        private async void BtnMerge_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            string cnPath = TxtCnPath.Text.Trim().Trim('\"', '\'');
            string enPath = TxtEnPath.Text.Trim().Trim('\"', '\'');

            if (string.IsNullOrEmpty(cnPath) || !File.Exists(cnPath))
            {
                MessageBox.Show("请先选择有效的【中文 PDF】文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(enPath) || !File.Exists(enPath))
            {
                MessageBox.Show("请先选择有效的【英文 PDF】文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isProcessing = true;
            BtnMerge.IsEnabled = false;
            BtnMerge.Content = "⏳ 正在生成中英对照 PDF...";
            GridActionButtons.Visibility = Visibility.Collapsed;
            ProgressBar.Value = 0;
            TxtStatus.Text = "⏳ 正在初始化并加载 PDF 页面...";

            await Task.Delay(100);

            string customOut = TxtOutputDir.Text.Trim().Trim('\"', '\'');
            string mode = _layoutMode;
            string modeName = mode == "side" ? "左右并排" : "上下并排";
            bool isSuccess = false;
            string finalOutFile = "";
            string errorMsg = "";

            await Task.Run(() =>
            {
                try
                {
                    string outDir;
                    if (!string.IsNullOrEmpty(customOut) && Directory.Exists(customOut))
                        outDir = customOut;
                    else
                        outDir = Path.GetDirectoryName(cnPath) ?? "";

                    Directory.CreateDirectory(outDir);

                    string baseName = Path.GetFileNameWithoutExtension(cnPath);
                    finalOutFile = Path.Combine(outDir, $"{baseName}_中英.pdf");

                    using var formCn = XPdfForm.FromFile(cnPath);
                    using var formEn = XPdfForm.FromFile(enPath);
                    using var outputDoc = new PdfSharp.Pdf.PdfDocument();

                    int n = Math.Min(formCn.PageCount, formEn.PageCount);
                    if (n == 0) throw new InvalidOperationException("选中的 PDF 文件没有有效页面！");

                    long lastUpdate = 0;

                    for (int i = 0; i < n; i++)
                    {
                        formCn.PageIndex = i;
                        formEn.PageIndex = i;

                        double wCn = formCn.PointWidth;
                        double hCn = formCn.PointHeight;
                        double wEn = formEn.PointWidth;
                        double hEn = formEn.PointHeight;

                        if (mode == "side")
                        {
                            double totalW = wCn + wEn;
                            double maxH = Math.Max(hCn, hEn);

                            var newPage = outputDoc.AddPage();
                            newPage.Width = XUnit.FromPoint(totalW);
                            newPage.Height = XUnit.FromPoint(maxH);

                            using var gfx = XGraphics.FromPdfPage(newPage);
                            gfx.DrawImage(formCn, 0, 0, wCn, hCn);
                            gfx.DrawImage(formEn, wCn, 0, wEn, hEn);
                        }
                        else
                        {
                            double maxW = Math.Max(wCn, wEn);
                            double totalH = hCn + hEn;

                            var newPage = outputDoc.AddPage();
                            newPage.Width = XUnit.FromPoint(maxW);
                            newPage.Height = XUnit.FromPoint(totalH);

                            using var gfx = XGraphics.FromPdfPage(newPage);
                            gfx.DrawImage(formCn, 0, 0, wCn, hCn);
                            gfx.DrawImage(formEn, 0, hCn, wEn, hEn);
                        }

                        int cur = i + 1;
                        double prog = (double)cur / n * 100;
                        long now = Environment.TickCount64;
                        if (now - lastUpdate > 30 || cur == n)
                        {
                            lastUpdate = now;
                            Dispatcher.Invoke(() =>
                            {
                                TxtStatus.Text = $"正在合成双语页面 ({cur}/{n})...";
                                ProgressBar.Value = prog;
                                _taskbar?.SetProgress((ulong)cur, (ulong)n);
                            });
                        }
                    }

                    outputDoc.Save(finalOutFile);
                    isSuccess = true;
                }
                catch (Exception ex)
                {
                    errorMsg = ex.Message;
                }
            });

            _lastOutputFile = finalOutFile;
            _isProcessing = false;
            BtnMerge.IsEnabled = true;
            BtnMerge.Content = "⚡ 开始生成中英对照 PDF";
            ProgressBar.Value = 100;
            _taskbar?.Reset();

            if (isSuccess)
            {
                TxtStatus.Text = $"🎉 生成成功 [{DateTime.Now:HH:mm:ss}] ({modeName})！已保存为：{Path.GetFileName(finalOutFile)}";
                GridActionButtons.Visibility = Visibility.Visible;
            }
            else
            {
                TxtStatus.Text = $"❌ 生成失败: {errorMsg}";
            }
        }

        private void BtnOpenPdf_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastOutputFile) && File.Exists(_lastOutputFile))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _lastOutputFile,
                    UseShellExecute = true
                });
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastOutputFile) && File.Exists(_lastOutputFile))
            {
                Process.Start("explorer.exe", $"/select,\"{_lastOutputFile}\"");
            }
            else
            {
                string dir = Path.GetDirectoryName(_lastOutputFile) ?? "";
                if (Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", dir);
                }
            }
        }
    }
}



