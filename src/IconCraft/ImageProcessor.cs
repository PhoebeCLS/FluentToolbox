using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using SkiaSharp;
using Svg.Skia;

namespace IconCraft
{
    public static class ImageProcessor
    {
        private static readonly HashSet<string> DoubleTlds = new(StringComparer.OrdinalIgnoreCase)
        {
            "com.cn", "net.cn", "org.cn", "gov.cn", "edu.cn",
            "co.uk", "org.uk", "me.uk",
            "co.jp", "com.hk", "com.tw", "com.au"
        };

        public static string CleanAppName(string stem)
        {
            var s = Regex.Replace(stem, @"^yafd[-_\.]\s*", "", RegexOptions.IgnoreCase);
            var parts = s.Split('.');
            if (parts.Length >= 2 && Regex.IsMatch(parts[^1], @"^[a-zA-Z]{2,}$"))
            {
                var lastTwo = $"{parts[^2]}.{parts[^1]}".ToLower();
                if (DoubleTlds.Contains(lastTwo) && parts.Length >= 3)
                {
                    return $"{parts[^3]}.{lastTwo}";
                }
                return $"{parts[^2]}.{parts[^1]}";
            }

            var cleaned = Regex.Replace(s, @"_[vV]?\d+[\d\.\-_a-zA-Z]*$", "");
            if (cleaned == s)
            {
                cleaned = Regex.Replace(s, @"[\s_\-]+[vV]?\d+(\.\d+)+.*$", "");
            }
            return cleaned.Trim();
        }

        public static SKBitmap LoadImage(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".svg")
            {
                var svg = new SKSvg();
                using var fs = File.OpenRead(path);
                svg.Load(fs);
                if (svg.Picture == null)
                    throw new InvalidOperationException("Failed to parse SVG");

                var bounds = svg.Picture.CullRect;
                float width = bounds.Width > 0 ? bounds.Width : 256;
                float height = bounds.Height > 0 ? bounds.Height : 256;
                
                int maxDim = 512;
                float scale = Math.Min(maxDim / width, maxDim / height);
                int targetW = Math.Max(1, (int)(width * scale));
                int targetH = Math.Max(1, (int)(height * scale));

                var bitmap = new SKBitmap(targetW, targetH, SKColorType.Rgba8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    canvas.Scale(scale, scale);
                    canvas.DrawPicture(svg.Picture);
                }
                return bitmap;
            }
            else
            {
                using var fs = File.OpenRead(path);
                using var codec = SKCodec.Create(fs);
                if (codec == null)
                {
                    var bmp = SKBitmap.Decode(path);
                    if (bmp == null) throw new InvalidOperationException("Failed to decode image");
                    return bmp;
                }
                var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                var bitmap = new SKBitmap(info);
                codec.GetPixels(info, bitmap.GetPixels());
                return bitmap;
            }
        }

        public static SKBitmap TrimOuterBorders(SKBitmap source, int darkThreshold = 25)
        {
            int width = source.Width;
            int height = source.Height;
            int minX = width, minY = height, maxX = -1, maxY = -1;

            unsafe
            {
                var ptr = (byte*)source.GetPixels().ToPointer();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = (y * width + x) * 4;
                        byte r = ptr[idx];
                        byte g = ptr[idx + 1];
                        byte b = ptr[idx + 2];
                        byte a = ptr[idx + 3];

                        if (a > 15 && (r > darkThreshold || g > darkThreshold || b > darkThreshold))
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }

            if (maxX >= minX && maxY >= minY)
            {
                int cropW = maxX - minX + 1;
                int cropH = maxY - minY + 1;
                var cropped = new SKBitmap(cropW, cropH, SKColorType.Rgba8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(cropped))
                {
                    canvas.Clear(SKColors.Transparent);
                    using var img = SKImage.FromBitmap(source);
                    canvas.DrawImage(img, SKRect.Create(minX, minY, cropW, cropH), SKRect.Create(0, 0, cropW, cropH), new SKSamplingOptions(SKFilterMode.Linear));
                }
                return cropped;
            }
            return source;
        }

        public static SKBitmap Process(SKBitmap input, string shapeMode, int targetSize)
        {
            using var trimmed = (shapeMode != "raw") ? TrimOuterBorders(input) : input;

            int scale = 4;
            int highRes = targetSize * scale;
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

            if (shapeMode == "squircle")
            {
                float radius = highRes * 0.22f;
                float aspect = (float)trimmed.Width / trimmed.Height;
                int newW, newH;
                if (aspect > 1)
                {
                    newW = highRes;
                    newH = Math.Max(1, (int)(highRes / aspect));
                }
                else
                {
                    newH = highRes;
                    newW = Math.Max(1, (int)(highRes * aspect));
                }

                using var canvasBmp = new SKBitmap(highRes, highRes, SKColorType.Rgba8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(canvasBmp))
                {
                    canvas.Clear(SKColors.Transparent);
                    int pasteX = (highRes - newW) / 2;
                    int pasteY = (highRes - newH) / 2;
                    using var trimmedImg = SKImage.FromBitmap(trimmed);
                    canvas.DrawImage(trimmedImg, SKRect.Create(pasteX, pasteY, newW, newH), sampling);

                    using var maskBmp = new SKBitmap(highRes, highRes, SKColorType.Rgba8888, SKAlphaType.Premul);
                    using var maskCanvas = new SKCanvas(maskBmp);
                    maskCanvas.Clear(SKColors.Transparent);
                    using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
                    maskCanvas.DrawRoundRect(new SKRoundRect(SKRect.Create(0, 0, highRes, highRes), radius, radius), paint);

                    using var paintBlend = new SKPaint { BlendMode = SKBlendMode.DstIn };
                    using var maskImg = SKImage.FromBitmap(maskBmp);
                    canvas.DrawImage(maskImg, 0, 0, sampling, paintBlend);
                }

                return canvasBmp.Resize(new SKImageInfo(targetSize, targetSize), sampling);
            }
            else if (shapeMode == "circle")
            {
                double diag = Math.Sqrt(trimmed.Width * trimmed.Width + trimmed.Height * trimmed.Height);
                double maxDiag = highRes * 0.92;
                double ratio = diag > 0 ? maxDiag / diag : 1.0;
                int newW = Math.Max(1, (int)(trimmed.Width * ratio));
                int newH = Math.Max(1, (int)(trimmed.Height * ratio));

                using var canvasBmp = new SKBitmap(highRes, highRes, SKColorType.Rgba8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(canvasBmp))
                {
                    canvas.Clear(SKColors.Transparent);
                    int pasteX = (highRes - newW) / 2;
                    int pasteY = (highRes - newH) / 2;
                    using var trimmedImg = SKImage.FromBitmap(trimmed);
                    canvas.DrawImage(trimmedImg, SKRect.Create(pasteX, pasteY, newW, newH), sampling);

                    using var maskBmp = new SKBitmap(highRes, highRes, SKColorType.Rgba8888, SKAlphaType.Premul);
                    using var maskCanvas = new SKCanvas(maskBmp);
                    maskCanvas.Clear(SKColors.Transparent);
                    using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
                    maskCanvas.DrawOval(SKRect.Create(0, 0, highRes, highRes), paint);

                    using var paintBlend = new SKPaint { BlendMode = SKBlendMode.DstIn };
                    using var maskImg = SKImage.FromBitmap(maskBmp);
                    canvas.DrawImage(maskImg, 0, 0, sampling, paintBlend);
                }

                return canvasBmp.Resize(new SKImageInfo(targetSize, targetSize), sampling);
            }
            else
            {
                return trimmed.Resize(new SKImageInfo(targetSize, targetSize), sampling);
            }
        }

        public static void SavePng(SKBitmap bitmap, string outPath)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var fs = File.OpenWrite(outPath);
            data.SaveTo(fs);
        }

        public static void SaveIco(SKBitmap rawImg, string shapeMode, string outPath)
        {
            int[] sizes = { 16, 32, 48, 64, 128, 256 };
            var pngStreams = new List<byte[]>();

            foreach (var sz in sizes)
            {
                using var proc = Process(rawImg, shapeMode, sz);
                using var img = SKImage.FromBitmap(proc);
                using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                pngStreams.Add(data.ToArray());
            }

            using var fs = File.Create(outPath);
            using var bw = new BinaryWriter(fs);

            // ICO Header
            bw.Write((short)0); // Reserved
            bw.Write((short)1); // Type 1 = ICO
            bw.Write((short)sizes.Length); // Count

            int offset = 6 + (16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                byte bSize = (byte)(sz >= 256 ? 0 : sz);
                bw.Write(bSize); // Width
                bw.Write(bSize); // Height
                bw.Write((byte)0); // Colors
                bw.Write((byte)0); // Reserved
                bw.Write((short)1); // Color planes
                bw.Write((short)32); // Bits per pixel
                bw.Write(pngStreams[i].Length); // Image size in bytes
                bw.Write(offset); // Image offset
                offset += pngStreams[i].Length;
            }

            foreach (var png in pngStreams)
            {
                bw.Write(png);
            }
        }
    }
}


