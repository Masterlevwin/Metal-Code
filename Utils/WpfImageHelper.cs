using Metal_Code;
using Metal_Code.Utils;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Path = System.Windows.Shapes.Path;

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

    /// <summary>
    /// Рендерит геометрию детали в изображение заданного размера.
    /// Линия контура масштабируется так, чтобы в итоговом изображении её толщина была ровно 1 пиксель.
    /// </summary>
    public static byte[]? RenderPartToPng(Part part, int widthPx = 36, int heightPx = 36)
    {
        if (part.DisplayGeometry == null)
            PartPreviewGenerator.EnsureDisplayGeometry(part);

        if (part.DisplayGeometry == null) return null;

        // 1. Клонируем геометрию
        var geometry = PartPreviewGenerator.CloneGeometry(part.DisplayGeometry);
        if (geometry == null) return null;

        // 2. НОРМАЛИЗАЦИЯ: Сдвигаем геометрию в (0,0), чтобы убрать глобальные координаты DXF
        var bounds = geometry.Bounds;
        var pathData = geometry.Clone();
        pathData.Transform = new TranslateTransform(-bounds.Left, -bounds.Top);

        // 3. Рассчитываем толщину линии для исходной геометрии
        double sourceSize = Math.Max(bounds.Width, bounds.Height);

        // Используем меньшую сторону целевого изображения для расчета масштаба (Uniform stretch)
        double targetSize = Math.Min(widthPx, heightPx);

        // Желаемая визуальная толщина линии в итоговом PNG (в пикселях)
        double desiredVisualStroke = 0.5;

        double strokeThickness = 0.5; // Значение по умолчанию

        if (sourceSize > 0)
        {
            // Масштаб сжатия/расширения
            double scale = targetSize / sourceSize;

            // Обратный расчет: какая толщина нужна исходному Path, 
            // чтобы после масштабирования Viewbox'ом она стала 0.5 px?
            strokeThickness = desiredVisualStroke / scale;

            // Ограничиваем разумными пределами, чтобы избежать артефактов
            strokeThickness = Math.Max(0.1, Math.Min(strokeThickness, 100.0));
        }

        // 4. Создаем Path
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

        // 5. Viewbox масштабирует деталь под размер widthPx x heightPx
        var viewbox = new Viewbox
        {
            Width = widthPx,
            Height = heightPx,
            Stretch = Stretch.Uniform,
            Child = path
        };

        return RenderVisualToPng(viewbox, widthPx, heightPx);
    }

    /// <summary>
    /// Возвращает байты изображения детали.
    /// Если ImageBytes пуст, но есть DisplayGeometry — генерирует, кэширует и возвращает.
    /// </summary>
    public static byte[] GetOrCreatePartImage(Part part, int width = 36, int height = 36)
    {
        // 1. Проверяем кэш
        if (part.ImageBytes != null && part.ImageBytes.Length > 0)
        {
            return part.ImageBytes;
        }

        // 2. Если геометрии нет, вернуть пустой массив или null
        if (part.DisplayGeometry == null)
        {
            // Можно попробовать сгенерировать геометрию, если она ленивая
            PartPreviewGenerator.EnsureDisplayGeometry(part);
            if (part.DisplayGeometry == null) return Array.Empty<byte>();
        }

        // 3. Генерируем изображение через наш настроенный рендерер
        byte[]? pngBytes = RenderPartToPng(part, widthPx: width, heightPx: height);

        // 4. Кэшируем результат в свойство детали
        if (pngBytes != null)
        {
            part.ImageBytes = pngBytes;
        }

        return pngBytes ?? Array.Empty<byte>();
    }
}