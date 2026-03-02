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
    /// <summary>
    /// Контрол визуализации раскладки трубного хлыста с отступами и улучшенной разметкой
    /// </summary>
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

        // Масштаб: 820 пикселей на длину хлыста
        private double ScaleFactor => 820.0 / (Stock?.StockLength ?? 6000.0);

        private Canvas _layoutCanvas = null!;
        private readonly RandomColorGenerator _colorGen = new();

        public PipeStockVisualizationControl()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            _layoutCanvas = new Canvas
            {
                Width = 820,
                Height = 70,
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

            // === ШАГ 1: Зона зажима (первые 340 мм) ===
            double clampWidthPx = Stock.ClampZone * ScaleFactor;
            var clampRect = new Rectangle
            {
                Width = Math.Max(1, clampWidthPx),
                Height = 30,
                Fill = new SolidColorBrush(Color.FromArgb(180, 255, 230, 200)), // Мягкий оранжевый
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 1,
                RadiusX = 3,
                RadiusY = 3,
                ToolTip = $"Зона зажима станка: {Stock.ClampZone} мм\n(недоступна для резки)"
            };
            Canvas.SetLeft(clampRect, 0);
            Canvas.SetTop(clampRect, 5);
            _layoutCanvas.Children.Add(clampRect);

            // Двухстрочная подпись зоны зажима
            var clampLabel1 = new TextBlock
            {
                Text = "ЗАЖИМ",
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.OrangeRed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = Math.Max(40, clampWidthPx - 4)
            };
            Canvas.SetLeft(clampLabel1, 2);
            Canvas.SetTop(clampLabel1, 8);
            _layoutCanvas.Children.Add(clampLabel1);

            var clampLabel2 = new TextBlock
            {
                Text = $"{Stock.ClampZone} мм",
                FontSize = 8,
                Foreground = Brushes.OrangeRed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = Math.Max(40, clampWidthPx - 4)
            };
            Canvas.SetLeft(clampLabel2, 5);
            Canvas.SetTop(clampLabel2, 20);
            _layoutCanvas.Children.Add(clampLabel2);

            // === ШАГ 2: Детали с отступами ===
            var aggregated = AggregatePlacements(Stock.Placements);
            double currentX = clampWidthPx;
            const double cutLoss = 10;
            double cutLossPx = cutLoss * ScaleFactor;

            foreach (var agg in aggregated)
            {
                double totalLengthMm = agg.Part.Length * agg.Count + (agg.Count - 1) * cutLoss;
                double totalLengthPx = totalLengthMm * ScaleFactor;
                double height = 30;

                var (fillColor, textColor) = _colorGen.GetColorsForPart(agg.Part);

                var rect = new Rectangle
                {
                    Width = Math.Max(1, totalLengthPx),
                    Height = height,
                    Fill = new SolidColorBrush(fillColor),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    RadiusX = 2,
                    RadiusY = 2,
                    ToolTip = $"{agg.Part.Title}\nСечение: {GetSectionLabel(agg.Part)}\n" +
                             $"Длина: {agg.Part.Length:F0} мм × {agg.Count} шт"
                };

                Canvas.SetLeft(rect, currentX);
                Canvas.SetTop(rect, 5);
                _layoutCanvas.Children.Add(rect);

                // Отступы между деталями (визуализация)
                if (agg.Count > 1 && cutLossPx > 1)
                {
                    for (int i = 1; i < agg.Count; i++)
                    {
                        double cutX = currentX + (agg.Part.Length * i + (i - 1) * cutLoss) * ScaleFactor;
                        var cutLine = new Line
                        {
                            X1 = cutX,
                            Y1 = 8,
                            X2 = cutX,
                            Y2 = 38,
                            Stroke = Brushes.White,
                            StrokeThickness = 2,
                            StrokeDashArray = new DoubleCollection { 2, 2 }
                        };
                        _layoutCanvas.Children.Add(cutLine);
                    }
                }

                // Подпись детали
                string label = agg.Count > 1
                    ? $"{agg.Count}×{agg.Part.Length:F0}мм"
                    : $"{agg.Part.Length:F0}мм";

                if (totalLengthPx > 50)
                {
                    var text = new TextBlock
                    {
                        Text = label,
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(textColor),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = Math.Max(50, totalLengthPx - 4)
                    };
                    Canvas.SetLeft(text, currentX + 2);
                    Canvas.SetTop(text, 15);
                    _layoutCanvas.Children.Add(text);
                }

                currentX += totalLengthPx;
            }

            // === ШАГ 3: Остаток (деловой) ===
            double wastePx = 820 - currentX;
            double wasteMm = Stock.AvailableLength;

            if (wastePx > 1.0)
            {
                var wasteRect = new Rectangle
                {
                    Width = wastePx,
                    Height = 30,
                    Fill = new SolidColorBrush(Color.FromArgb(150, 255, 200, 200)), // Мягкий красный
                    Stroke = Brushes.Red,
                    StrokeThickness = 1,
                    RadiusX = 3,
                    RadiusY = 3,
                    ToolTip = $"Деловой остаток хлыста: {wasteMm:F0} мм\n" +
                             $"(может быть использован для мелких деталей)"
                };
                Canvas.SetLeft(wasteRect, currentX);
                Canvas.SetTop(wasteRect, 5);
                _layoutCanvas.Children.Add(wasteRect);

                // Двухстрочная подпись остатка
                var wasteLabel1 = new TextBlock
                {
                    Text = "ОСТАТОК",
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.DarkRed,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = Math.Max(50, wastePx - 4)
                };
                Canvas.SetLeft(wasteLabel1, currentX + 2);
                Canvas.SetTop(wasteLabel1, 8);
                _layoutCanvas.Children.Add(wasteLabel1);

                var wasteLabel2 = new TextBlock
                {
                    Text = $"{wasteMm:F0} мм",
                    FontSize = 8,
                    Foreground = Brushes.DarkRed,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = Math.Max(50, wastePx - 4)
                };
                Canvas.SetLeft(wasteLabel2, currentX + 5);
                Canvas.SetTop(wasteLabel2, 20);
                _layoutCanvas.Children.Add(wasteLabel2);
            }

            // === ШАГ 4: Шкала длины ===
            DrawScale();
        }

        private List<AggregatedPlacement> AggregatePlacements(List<PipePlacement> placements)
        {
            var result = new List<AggregatedPlacement>();
            if (placements.Count == 0) return result;

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

                // Проверяем, что детали идут подряд с отступом 10 мм
                double expectedPosition = current.StartPosition + current.Part.Length * count + (count - 1) * 10;
                bool isConsecutive = Math.Abs(next.StartPosition - expectedPosition) < 1.0;

                if (isSameType && isConsecutive)
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
            // Горизонтальная линия шкалы
            var baseLine = new Line
            {
                X1 = 0,
                Y1 = 45,
                X2 = 820,
                Y2 = 45,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            _layoutCanvas.Children.Add(baseLine);

            // Отметки каждые 1000 мм
            for (int i = 0; i <= (Stock?.StockLength ?? 6000); i += 1000)
            {
                double x = i * ScaleFactor;

                // Вертикальная линия отметки
                var mark = new Line
                {
                    X1 = x,
                    Y1 = 45,
                    X2 = x,
                    Y2 = 50,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1
                };
                _layoutCanvas.Children.Add(mark);

                // Подпись отметки
                var text = new TextBlock
                {
                    Text = $"{i}",
                    FontSize = 9,
                    Foreground = Brushes.Gray,
                    FontWeight = FontWeights.Bold
                };
                Canvas.SetLeft(text, x - 12);
                Canvas.SetTop(text, 52);
                _layoutCanvas.Children.Add(text);
            }
        }

        private string GetSectionLabel(Part part)
        {
            return part.PartType switch
            {
                PartType.RoundTube => $"Ø{part.Width}×{part.Destiny}",
                PartType.RectangularTube => $"{part.Width}×{part.Height}×{part.Destiny}",
                _ => $"{part.Width}×{part.Height}"
            };
        }
    }

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

            int hash = key.GetHashCode();
            byte r = (byte)Math.Max(100, (hash & 0xFF) % 100 + 100);
            byte g = (byte)Math.Max(100, ((hash >> 8) & 0xFF) % 100 + 100);
            byte b = (byte)Math.Max(100, ((hash >> 16) & 0xFF) % 100 + 100);

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