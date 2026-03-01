using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Metal_Code.Utils;

namespace Metal_Code
{
    public partial class PipeStockVisualizationControl : UserControl
    {
        public static readonly DependencyProperty StockProperty =
            DependencyProperty.Register(
                nameof(Stock),
                typeof(PipeStock),
                typeof(PipeStockVisualizationControl),
                new PropertyMetadata(null, OnStockChanged));

        public PipeStock? Stock
        {
            get => (PipeStock?)GetValue(StockProperty);
            set => SetValue(StockProperty, value);
        }

        // Масштаб: 800 пикселей на 6000 мм (стандартный хлыст)
        public double ScaleFactor { get; set; } = 800.0 / 6000.0;

        private Canvas _layoutCanvas = null!;

        public PipeStockVisualizationControl()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            _layoutCanvas = new Canvas
            {
                Width = 800,  // Фиксированная ширина для 6000 мм хлыста
                Height = 40,  // Высота полосы с деталями
                Background = Brushes.White,
                Margin = new Thickness(5)
            };

            Content = _layoutCanvas;
        }

        private static void OnStockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PipeStockVisualizationControl view)
            {
                view.RenderStock();
            }
        }

        private void RenderStock()
        {
            _layoutCanvas.Children.Clear();
            if (Stock == null || Stock.Placements.Count == 0) return;

            var colorGen = new RandomColorGenerator();

            // === ШАГ 1: Отрисовка зоны зажима (первые 340 мм) ===
            double clampWidthPx = Stock.ClampZone * ScaleFactor;
            var clampRect = new Rectangle
            {
                Width = Math.Max(1, clampWidthPx),
                Height = 30,
                Fill = new SolidColorBrush(Color.FromArgb(150, 255, 223, 186)), // Светло-оранжевый (зона зажима)
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 1,
                ToolTip = $"Зона зажима: {Stock.ClampZone} мм (недоступна для резки)"
            };
            Canvas.SetLeft(clampRect, 0);
            Canvas.SetTop(clampRect, 5);
            _layoutCanvas.Children.Add(clampRect);

            // Подпись зоны зажима
            var clampText = new TextBlock
            {
                Text = $"Зажим {Stock.ClampZone}мм",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.OrangeRed,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(clampText, 2);
            Canvas.SetTop(clampText, 10);
            _layoutCanvas.Children.Add(clampText);

            // === ШАГ 2: Агрегация деталей для компактного отображения ===
            var aggregated = AggregatePlacements(Stock.Placements);
            double currentX = clampWidthPx; // Начинаем после зоны зажима

            // === ШАГ 3: Отрисовка деталей ===
            foreach (var agg in aggregated)
            {
                double totalLengthMm = agg.Part.Length * agg.Count; // Без учёта реза между деталями (для визуализации)
                double totalLengthPx = totalLengthMm * ScaleFactor;
                double height = 25;

                var (fillColor, textColor) = colorGen.GetColorsForPart(agg.Part);

                var rect = new Rectangle
                {
                    Width = Math.Max(1, totalLengthPx),
                    Height = height,
                    Fill = new SolidColorBrush(fillColor),
                    Stroke = Brushes.Black,
                    StrokeThickness = 0.5,
                    ToolTip = $"{agg.Part.Title}\nСечение: {GetSectionLabel(agg.Part)}\nДлина: {agg.Part.Length} мм\nКоличество: {agg.Count} шт"
                };

                Canvas.SetLeft(rect, currentX);
                Canvas.SetTop(rect, (40 - height) / 2 + 5);
                _layoutCanvas.Children.Add(rect);

                // Подпись детали
                string label = agg.Count > 1
                    ? $"{agg.Count}×{agg.Part.Length:F0}мм"
                    : $"{agg.Part.Length:F0}мм";

                if (totalLengthPx > 40)
                {
                    var text = new TextBlock
                    {
                        Text = label,
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(textColor),
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(text, currentX + 2);
                    Canvas.SetTop(text, (40 - height) / 2 + 7);
                    _layoutCanvas.Children.Add(text);
                }

                currentX += totalLengthPx;
            }

            // === ШАГ 4: Отрисовка отхода ===
            double totalUsedPx = currentX;
            double wastePx = 800 - totalUsedPx;
            double wasteMm = Math.Max(0, Stock.StockLength - (Stock.ClampZone + Stock.UsedLength));

            if (wastePx > 0.5)
            {
                var wasteRect = new Rectangle
                {
                    Width = wastePx,
                    Height = 30,
                    Fill = new SolidColorBrush(Color.FromArgb(120, 255, 182, 193)), // Светло-красный с прозрачностью
                    Stroke = Brushes.Red,
                    StrokeThickness = 1,
                    ToolTip = $"Деловой остаток: {wasteMm:F0} мм"
                };
                Canvas.SetLeft(wasteRect, totalUsedPx);
                Canvas.SetTop(wasteRect, 5);
                _layoutCanvas.Children.Add(wasteRect);

                // Подпись отхода
                var wasteText = new TextBlock
                {
                    Text = $"Остаток: {wasteMm:F0}мм",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.DarkRed,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(wasteText, totalUsedPx + 2);
                Canvas.SetTop(wasteText, 12);
                _layoutCanvas.Children.Add(wasteText);
            }

            // === ШАГ 5: Шкала длины под полосой ===
            DrawScale();
        }

        private List<AggregatedPlacement> AggregatePlacements(List<PipePlacement> placements)
        {
            var result = new List<AggregatedPlacement>();
            if (placements.Count == 0) return result;

            // Сортируем по позиции (на случай если порядок нарушен)
            var sorted = placements.OrderBy(p => p.StartPosition).ToList();

            var current = sorted[0];
            int count = 1;

            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];
                bool isSameType =
                    Math.Abs(current.Part.Width - next.Part.Width) < 0.1 &&
                    Math.Abs(current.Part.Height - next.Part.Height) < 0.1 &&
                    Math.Abs(current.Part.Length - next.Part.Length) < 0.1 &&
                    current.Part.PartType == next.Part.PartType &&
                    current.Part.Metal == next.Part.Metal;

                if (isSameType && Math.Abs(next.StartPosition - (current.StartPosition + current.Part.Length * count)) < 1.0)
                {
                    count++;
                }
                else
                {
                    result.Add(new AggregatedPlacement { Part = current.Part, Count = count });
                    current = next;
                    count = 1;
                }
            }

            result.Add(new AggregatedPlacement { Part = current.Part, Count = count });
            return result;
        }

        private void DrawScale()
        {
            // Отрисовка шкалы под полосой (каждые 1000 мм)
            for (int i = 0; i <= 6000; i += 1000)
            {
                double x = i * ScaleFactor;

                // Линия шкалы
                var line = new Line
                {
                    X1 = x,
                    Y1 = 35,
                    X2 = x,
                    Y2 = 42,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1
                };
                _layoutCanvas.Children.Add(line);

                // Подпись
                var text = new TextBlock
                {
                    Text = $"{i}",
                    FontSize = 8,
                    Foreground = Brushes.Gray,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(text, x - 10);
                Canvas.SetTop(text, 42);
                _layoutCanvas.Children.Add(text);
            }

            // Общая подпись "6000 мм"
            var totalText = new TextBlock
            {
                Text = $"Хлыст: {Stock?.StockLength ?? 6000} мм",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkBlue,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(totalText, 700);
            Canvas.SetTop(totalText, 42);
            _layoutCanvas.Children.Add(totalText);
        }

        private string GetSectionLabel(Part part)
        {
            return part.PartType switch
            {
                PartType.RoundTube => $"Ø{part.Width}×{part.Destiny}",
                PartType.RectangularTube => $"{part.Width}×{part.Height}×{part.Destiny}",
                _ => "неизвестно"
            };
        }
    }

    // Вспомогательные классы
    internal class AggregatedPlacement
    {
        public Part Part { get; set; } = null!;
        public int Count { get; set; }
    }

    internal class RandomColorGenerator
    {
        private readonly Dictionary<string, Color> _colorCache = new();

        public (Color Fill, Color Text) GetColorsForPart(Part part)
        {
            string key = $"{part.PartType}_{part.Width}_{part.Height}_{part.Destiny}_{part.Metal}";
            if (_colorCache.TryGetValue(key, out Color fill))
            {
                return (fill, GetContrastColor(fill));
            }

            // Генерируем "стабильный" цвет на основе хэша
            int hash = key.GetHashCode();
            byte r = (byte)((hash & 0xFF0000) >> 16);
            byte g = (byte)((hash & 0x00FF00) >> 8);
            byte b = (byte)(hash & 0x0000FF);

            // Увеличиваем яркость, чтобы не было слишком тёмных цветов
            r = (byte)Math.Max(80, r % 128 + 80);
            g = (byte)Math.Max(80, g % 128 + 80);
            b = (byte)Math.Max(80, b % 128 + 80);

            fill = Color.FromRgb(r, g, b);
            _colorCache[key] = fill;
            return (fill, GetContrastColor(fill));
        }

        private static Color GetContrastColor(Color color)
        {
            double brightness = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            return brightness < 0.5 ? Colors.White : Colors.Black;
        }
    }
}