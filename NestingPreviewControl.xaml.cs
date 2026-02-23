using Metal_Code.Utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    /// <summary>
    /// Контрол для визуализации раскладки деталей на листе 1500×3000 мм
    /// </summary>
    public partial class NestingPreviewControl : UserControl
    {
        private const double SheetWidth = 3000;
        private const double SheetHeight = 1500;
        private const double LabelMarginBottom = 40;
        private const double LabelMarginLeft = 50;
        private const double GridStep = 500;

        private Canvas _rootCanvas = null!;
        private Canvas _invertedLayer = null!;
        private Canvas _labelsCanvas = null!;
        private Canvas _partsCanvas = null!;

        public NestingPreviewControl()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            var viewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                ClipToBounds = false,
                Margin = new Thickness(5)
            };

            _rootCanvas = new Canvas
            {
                Width = SheetWidth + LabelMarginLeft,
                Height = SheetHeight + LabelMarginBottom,
                Background = Brushes.White
            };

            _invertedLayer = new Canvas
            {
                Width = SheetWidth,
                Height = SheetHeight,
                RenderTransform = new TransformGroup
                {
                    Children = new TransformCollection
                    {
                        new ScaleTransform { ScaleX = 1, ScaleY = -1 },
                        new TranslateTransform { Y = SheetHeight }
                    }
                }
            };

            var sheetRect = new Rectangle
            {
                Width = SheetWidth,
                Height = SheetHeight,
                Fill = new SolidColorBrush(Color.FromArgb(30, 240, 240, 240)),
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            _invertedLayer.Children.Add(sheetRect);

            DrawGrid(_invertedLayer);

            _partsCanvas = new Canvas();
            _invertedLayer.Children.Add(_partsCanvas);

            _labelsCanvas = new Canvas();
            DrawLabels();

            _rootCanvas.Children.Add(_invertedLayer);
            Canvas.SetLeft(_invertedLayer, LabelMarginLeft);
            Canvas.SetTop(_invertedLayer, 0);

            _rootCanvas.Children.Add(_labelsCanvas);

            viewbox.Child = _rootCanvas;
            Content = viewbox;
        }

        private void DrawGrid(Canvas canvas)
        {
            for (double x = GridStep; x <= SheetWidth; x += GridStep)
            {
                canvas.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = SheetHeight,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 5, 5 }
                });
            }

            for (double y = GridStep; y <= SheetHeight; y += GridStep)
            {
                canvas.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = SheetWidth,
                    Y2 = y,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 5, 5 }
                });
            }
        }

        private void DrawLabels()
        {
            _labelsCanvas.Children.Clear();

            for (double x = GridStep; x <= SheetWidth; x += GridStep)
            {
                var text = new TextBlock
                {
                    Text = ((int)x).ToString(),
                    FontSize = 40,
                    Foreground = Brushes.Gray,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(text, LabelMarginLeft + x - 20);
                Canvas.SetTop(text, SheetHeight + 10);
                _labelsCanvas.Children.Add(text);
            }

            for (double y = GridStep; y <= SheetHeight; y += GridStep)
            {
                var text = new TextBlock
                {
                    Text = ((int)y).ToString(),
                    FontSize = 40,
                    Foreground = Brushes.Gray,
                    IsHitTestVisible = false
                };
                double screenY = SheetHeight - y;
                Canvas.SetLeft(text, -LabelMarginLeft * 2);
                Canvas.SetTop(text, screenY - 10);
                _labelsCanvas.Children.Add(text);
            }

            var originLabel = new TextBlock
            {
                Text = "0",
                FontSize = 40,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Red,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(originLabel, -LabelMarginLeft);
            Canvas.SetTop(originLabel, SheetHeight + 10);
            _labelsCanvas.Children.Add(originLabel);
        }

        /// <summary>
        /// Отображает раскладку для одного листа
        /// </summary>
        public void ShowSheet(NestingSheet sheet)
        {
            if (sheet == null || sheet.Parts == null)
            {
                _partsCanvas.Children.Clear();
                return;
            }

            _partsCanvas.Children.Clear();

            foreach (var placement in sheet.Parts)
            {
                if (placement.Part.DisplayGeometry == null)
                    PartPreviewGenerator.EnsureDisplayGeometry(placement.Part);

                AddPartToCanvas(placement.Part, placement.X, placement.Y);
            }
        }

        private void AddPartToCanvas(Part part, double x, double y)
        {
            if (part.DisplayGeometry is null) return;

            var geometry = PartPreviewGenerator.CloneGeometry(part.DisplayGeometry);
            if (geometry == null) return;

            var bounds = geometry.Bounds;
            double offsetX = -bounds.Left;
            double offsetY = -bounds.Top;

            var path = new Path
            {
                Data = geometry,
                Fill = part.PartType == PartType.Round
                    ? new SolidColorBrush(Color.FromArgb(150, 255, 150, 150))
                    : new SolidColorBrush(Color.FromArgb(150, 150, 200, 255)),
                Stroke = part.PartType == PartType.Round
                    ? Brushes.DarkRed
                    : Brushes.DarkBlue,
                StrokeThickness = 1.5,
                ToolTip = $"{part.Title}\n{GetPartDimensions(part)}",
                RenderTransform = new TranslateTransform(offsetX, offsetY)
            };

            Canvas.SetLeft(path, x);
            Canvas.SetTop(path, y);
            _partsCanvas.Children.Add(path);
        }

        private string GetPartDimensions(Part part)
        {
            if (part.PartType == PartType.Round)
                return $"Ø{part.Width:0}мм";
            return $"{part.Width:0}×{part.Height:0}мм";
        }
    }
}