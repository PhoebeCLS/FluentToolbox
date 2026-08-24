using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace IconCraft
{
    public partial class MainWindow : Window
    {
        private readonly List<string> _fileList = new();
        private string _exportFormat = "png";
        private string _shapeMode = "squircle";
        private string _lastOutputDir = "";
        private bool _isProcessing = false;
        private TaskbarHelper? _taskbar;

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tiff", ".tif", ".ico", ".gif", ".svg"
        };

        private static readonly Dictionary<string, string> ShapeDescriptions = new()
        {
            ["squircle"] = "💡 iOS/macOS 规范 22% 圆角矩形：完美兼顾画面不切角与所有图标视觉大小对齐一致 (默认强烈推荐！)",
            ["circle"] = "💡 无切角圆形 (0.92 对角线等比缩放)：容纳完整图形，适合圆形徽章，绝不裁切边角。",
            ["square"] = "💡 自动去黑边 + 方形原形状：自动裁剪四周深色与透明多余边框，最大化保留原始比例与图形细节。",
            ["raw"] = "💡 原样直转：不进行任何边缘裁剪与圆角遮罩处理，直接按原图缩放输出 32x32 尺寸。"
        };

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _taskbar = new TaskbarHelper(this);
            SmoothScrollHelper.Register(MainScrollViewer);
            ThemeManager.SetTheme(this, "system");
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                ThemeManager.SetTheme(this, tag);
            }
        }

        private void Format_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _exportFormat = tag;
            }
        }

        private void Shape_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _shapeMode = tag;
                if (ShapeDescriptions.TryGetValue(tag, out var desc))
                {
                    TxtShapeDesc.Text = desc;
                }
            }
        }

        private void DropCard_Click(object sender, MouseButtonEventArgs e)
        {
            BtnBrowseFiles_Click(sender, e);
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
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

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var found = new List<string>();
                foreach (var item in files)
                {
                    if (Directory.Exists(item))
                    {
                        foreach (var sub in Directory.EnumerateFiles(item, "*.*", SearchOption.AllDirectories))
                        {
                            if (ImageExtensions.Contains(Path.GetExtension(sub)))
                                found.Add(sub);
                        }
                    }
                    else if (File.Exists(item) && ImageExtensions.Contains(Path.GetExtension(item)))
                    {
                        found.Add(item);
                    }
                }
                AddFiles(found);
            }
        }

        private void BtnBrowseFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择图片或 SVG 文件",
                Filter = "所有支持格式|*.svg;*.png;*.jpg;*.jpeg;*.webp;*.ico;*.bmp;*.tiff;*.gif|SVG 矢量图|*.svg|PNG 图片|*.png|所有文件|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                AddFiles(dlg.FileNames);
            }
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "选择包含图片的文件夹",
                Multiselect = false
            };
            if (dlg.ShowDialog() == true)
            {
                var found = Directory.EnumerateFiles(dlg.FolderName, "*.*", SearchOption.AllDirectories)
                    .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
                    .ToList();
                AddFiles(found);
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

        private void AddFiles(IEnumerable<string> paths)
        {
            var set = new HashSet<string>(_fileList, StringComparer.OrdinalIgnoreCase);
            foreach (var p in paths)
            {
                if (set.Add(p))
                    _fileList.Add(p);
            }
            RefreshQueue();
        }

        private void BtnClearQueue_Click(object sender, RoutedEventArgs e)
        {
            _fileList.Clear();
            RefreshQueue();
        }

        private void RefreshQueue()
        {
            int n = _fileList.Count;
            TxtQueueTitle.Text = $"📋 待处理队列 ({n} 个文件)";
            BtnConvert.Content = $"🚀 开始批量转换 ({n} 个文件)";

            if (n == 0)
            {
                TxtQueueBox.Text = "等待添加文件...";
            }
            else
            {
                var lines = _fileList.Take(100).Select((p, idx) =>
                {
                    var name = Path.GetFileName(p);
                    var clean = ImageProcessor.CleanAppName(Path.GetFileNameWithoutExtension(p));
                    var sz = File.Exists(p) ? (new FileInfo(p).Length / 1024.0) : 0;
                    return $"{idx + 1:02d}. {name,-30} -> 拟命名: [{clean}] ({sz:F1}KB)";
                }).ToList();

                if (n > 100)
                    lines.Add($"... 还有 {n - 100} 个文件未在预览中展示");

                TxtQueueBox.Text = string.Join(Environment.NewLine, lines);
            }
        }

        private async void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            if (_fileList.Count == 0)
            {
                MessageBox.Show("请先添加待处理的图片或 SVG 文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isProcessing = true;
            BtnConvert.IsEnabled = false;
            BtnConvert.Content = "⏳ 正在批量转换中...";
            BtnOpenOutput.Visibility = Visibility.Collapsed;
            ProgressBar.Value = 0;
            TxtStatus.Text = "⏳ 正在初始化转换任务...";

            // Visual feedback delay so secondary generation clearly resets
            await Task.Delay(100);

            var files = _fileList.ToList();
            var format = _exportFormat;
            var shape = _shapeMode;
            var customOut = TxtOutputDir.Text.Trim();

            int success = 0;
            string lastDir = "";

            await Task.Run(() =>
            {
                int total = files.Count;
                long lastUpdate = 0;

                for (int i = 0; i < total; i++)
                {
                    var file = files[i];
                    try
                    {
                        if (!File.Exists(file)) continue;

                        string outDir;
                        if (!string.IsNullOrEmpty(customOut) && Directory.Exists(customOut))
                            outDir = customOut;
                        else
                            outDir = Path.Combine(Path.GetDirectoryName(file) ?? "", "32×32");

                        Directory.CreateDirectory(outDir);
                        lastDir = outDir;

                        string clean = ImageProcessor.CleanAppName(Path.GetFileNameWithoutExtension(file));
                        using var raw = ImageProcessor.LoadImage(file);

                        if (format is "png" or "both")
                        {
                            using var proc32 = ImageProcessor.Process(raw, shape, 32);
                            string outPng = Path.Combine(outDir, $"{clean}.png");
                            ImageProcessor.SavePng(proc32, outPng);
                        }

                        if (format is "ico" or "both")
                        {
                            string outIco = Path.Combine(outDir, $"{clean}.ico");
                            ImageProcessor.SaveIco(raw, shape, outIco);
                        }

                        success++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing {file}: {ex.Message}");
                    }

                    int cur = i + 1;
                    double prog = (double)cur / total * 100;
                    long now = Environment.TickCount64;
                    if (now - lastUpdate > 30 || cur == total)
                    {
                        lastUpdate = now;
                        Dispatcher.Invoke(() =>
                        {
                            TxtStatus.Text = $"正在处理 ({cur}/{total}): {Path.GetFileName(file)}";
                            ProgressBar.Value = prog;
                            _taskbar?.SetProgress((ulong)cur, (ulong)total);
                        });
                    }
                }
            });

            _lastOutputDir = lastDir;
            _isProcessing = false;
            BtnConvert.IsEnabled = true;
            BtnConvert.Content = $"🚀 开始批量转换 ({_fileList.Count} 个文件)";
            ProgressBar.Value = 100;
            TxtStatus.Text = $"🎉 转换完成 [{DateTime.Now:HH:mm:ss}]！成功输出 {success}/{files.Count} 个图标到目标目录";
            _taskbar?.Reset();
            BtnOpenOutput.Visibility = Visibility.Visible;
        }

        private void BtnOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastOutputDir) && Directory.Exists(_lastOutputDir))
            {
                Process.Start("explorer.exe", _lastOutputDir);
            }
        }
    }
}

