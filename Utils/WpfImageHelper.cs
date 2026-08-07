using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Path = System.Windows.Shapes.Path;

namespace Metal_Code.Utils
{
    public static class WpfImageHelper
    {
        public static byte[] RenderVisualToPng(UIElement visual, int width, int height, double dpi = 96)
        {
            visual.Measure(new Size(width, height));
            visual.Arrange(new Rect(0, 0, width, height));
            visual.UpdateLayout();

            var renderBitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
            renderBitmap.Render(visual);

            using var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            encoder.Save(stream);

            return stream.ToArray();
        }

        public static byte[]? RenderPartToPng(Part part, int widthPx = 36, int heightPx = 36)
        {
            if (part.DisplayGeometry == null)
                PartPreviewGenerator.EnsureDisplayGeometry(part);

            if (part.DisplayGeometry == null) return null;

            var geometry = PartPreviewGenerator.CloneGeometry(part.DisplayGeometry);
            if (geometry == null) return null;

            var bounds = geometry.Bounds;
            var pathData = geometry.Clone();
            pathData.Transform = new TranslateTransform(-bounds.Left, -bounds.Top);

            double sourceSize = Math.Max(bounds.Width, bounds.Height);
            double targetSize = Math.Min(widthPx, heightPx);
            double desiredVisualStroke = 0.5;
            double strokeThickness = 0.5;

            if (sourceSize > 0)
            {
                double scale = targetSize / sourceSize;
                strokeThickness = desiredVisualStroke / scale;
                strokeThickness = Math.Max(0.1, Math.Min(strokeThickness, 100.0));
            }

            var path = new Path
            {
                Data = pathData,
                Fill = new SolidColorBrush(Color.FromArgb(100, 70, 130, 180)),
                Stroke = Brushes.DarkBlue,
                StrokeThickness = strokeThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            var viewbox = new Viewbox
            {
                Width = widthPx,
                Height = heightPx,
                Stretch = Stretch.Uniform,
                Child = path
            };

            return RenderVisualToPng(viewbox, widthPx, heightPx);
        }

        public static byte[] GetOrCreatePartImage(Part part, int width = 36, int height = 36)
        {
            if (part.ImageBytes != null && part.ImageBytes.Length > 0)
                return part.ImageBytes;

            if (part.DisplayGeometry == null)
            {
                PartPreviewGenerator.EnsureDisplayGeometry(part);
                if (part.DisplayGeometry == null) return Array.Empty<byte>();
            }

            byte[]? pngBytes = RenderPartToPng(part, widthPx: width, heightPx: height);

            if (pngBytes != null)
                part.ImageBytes = pngBytes;

            return pngBytes ?? Array.Empty<byte>();
        }

        /// <summary>
        /// Получает поток изображения из Embedded Resources.
        /// Возвращает null, если ресурс не найден (без выбрасывания исключений).
        /// </summary>
        public static Stream? GetStream(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return null;

            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                // GetManifestResourceStream сам возвращает null, если ресурс не найден
                return assembly.GetManifestResourceStream(resourceName);
            }
            catch
            {
                return null; // Безопасно возвращаем null вместо исключения
            }
        }
    }
}