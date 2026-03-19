using Metal_Code.Utils;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    /// <summary>
    /// Контрол для визуализации раскладки деталей на листе металла.
    /// Размеры листа адаптируются под переданный объект NestingSheet.
    /// </summary>
    public partial class NestingPreviewControl : UserControl
    {
        private const double LabelMarginBottom = 40;
        private const double LabelMarginLeft = 50;
        private const double GridStep = 500;

        private Viewbox _rootViewbox = null!;
        private Canvas _rootCanvas = null!;
        private Canvas _invertedLayer = null!;
        private Canvas _labelsCanvas = null!;
        private Canvas _partsCanvas = null!;

        // Текущие отображаемые размеры
        private double _currentSheetWidth;
        private double _currentSheetHeight;

        public NestingPreviewControl()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            _rootViewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                ClipToBounds = false,
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Инициализируем канвас временными размерами, они будут пересчитаны в ShowSheet
            _rootCanvas = new Canvas
            {
                Background = Brushes.White
            };

            _invertedLayer = new Canvas();
            _partsCanvas = new Canvas();
            _labelsCanvas = new Canvas();

            _invertedLayer.Children.Add(_partsCanvas);
            _rootCanvas.Children.Add(_invertedLayer);
            _rootCanvas.Children.Add(_labelsCanvas);

            _rootViewbox.Child = _rootCanvas;
            Content = _rootViewbox;
        }

        /// <summary>
        /// Перестраивает интерфейс под размеры конкретного листа и отображает детали.
        /// </summary>
        public void ShowSheet(NestingSheet sheet)
        {
            if (sheet == null || sheet.Parts == null)
            {
                _partsCanvas?.Children.Clear();
                return;
            }

            // Если размеры изменились, перестраиваем холст и сетку
            if (Math.Abs(_currentSheetWidth - sheet.Width) > 0.1 ||
                Math.Abs(_currentSheetHeight - sheet.Height) > 0.1)
            {
                RebuildLayout(sheet);
            }

            _partsCanvas.Children.Clear();

            foreach (var placement in sheet.Parts)
            {
                if (placement.Part.DisplayGeometry == null)
                    PartPreviewGenerator.EnsureDisplayGeometry(placement.Part);

                AddPartToCanvas(placement.Part, placement.X, placement.Y);
            }
        }

        /// <summary>
        /// Пересоздает структуру canvas, сетку и подписи под новые размеры листа
        /// </summary>
        private void RebuildLayout(NestingSheet sheet)
        {
            // Используем ИСХОДНЫЕ размеры заготовки для построения холста
            double width = sheet.StockWidth;
            double height = sheet.StockHeight;

            _currentSheetWidth = width;
            _currentSheetHeight = height;

            _rootCanvas.Width = width + LabelMarginLeft;
            _rootCanvas.Height = height + LabelMarginBottom;

            _invertedLayer.Width = width;
            _invertedLayer.Height = height;

            _invertedLayer.RenderTransform = new TransformGroup
            {
                Children = new TransformCollection
                {
                    new ScaleTransform { ScaleX = 1, ScaleY = -1 },
                    new TranslateTransform { Y = height }
                }
            };

            _invertedLayer.Children.Clear();
            _invertedLayer.Children.Add(_partsCanvas);

            // Фон полного листа
            var sheetRect = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = new SolidColorBrush(Color.FromArgb(30, 240, 240, 240)),
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            _invertedLayer.Children.Insert(0, sheetRect);

            DrawGrid(_invertedLayer, width, height);

            // Рисуем линию обрезки (если оптимизация меньше полного размера)
            DrawCutLine(_invertedLayer, sheet.OptimizedWidth, sheet.OptimizedHeight, width, height);

            Canvas.SetLeft(_invertedLayer, LabelMarginLeft);
            Canvas.SetTop(_invertedLayer, 0);

            DrawLabels(width, height);
        }

        /// <summary>
        /// Рисует красную пунктирную линию обрезки и её размерную подпись.
        /// </summary>
        private void DrawCutLine(Canvas canvas, double optWidth, double optHeight, double fullWidth, double fullHeight)
        {
            // Если оптимизированный размер почти равен полному, ничего не рисуем
            if (Math.Abs(optWidth - fullWidth) < 1 && Math.Abs(optHeight - fullHeight) < 1)
                return;

            // 1. Рисуем саму линию обрезки
            var cutRect = new Rectangle
            {
                Width = optWidth,
                Height = optHeight,
                Fill = Brushes.Transparent,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 5 }
            };

            int index = canvas.Children.IndexOf(_partsCanvas);
            if (index >= 0)
                canvas.Children.Insert(index, cutRect);
            else
                canvas.Children.Add(cutRect);

            // 2. Рисуем подпись размера обрезки
            double displayWidth = Math.Ceiling(optWidth / 100) * 100;
            double displayHeight = Math.Ceiling(optHeight / 100) * 100;

            // Общая группа трансформации для компенсации инверсии родителя
            // Родитель инвертирован (Y * -1), поэтому мы делаем TextBlock тоже инвертированным (Y * -1),
            // чтобы в сумме получилось нормальное отображение (-1 * -1 = 1).
            var normalizeTransform = new ScaleTransform { ScaleX = 1, ScaleY = -1 };

            // --- Случай 1: Обрезана ширина (отход справа) ---
            if (Math.Abs(optWidth - fullWidth) > 1)
            {
                var text = new TextBlock
                {
                    Text = ((int)displayWidth).ToString(),
                    FontSize = 36,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Red,
                    Background = Brushes.White,
                    IsHitTestVisible = false,
                    RenderTransform = normalizeTransform,
                    RenderTransformOrigin = new Point(0.5, 0.5) // Центрируем трансформацию
                };

                // Координаты в инвертированной системе:
                // X остается как есть.
                Canvas.SetLeft(text, optWidth + 10);

                // Y: В инвертированной системе 0 внизу, Height вверху.
                // Центр листа визуально находится на fullHeight / 2.
                Canvas.SetTop(text, (fullHeight / 2) - 20);

                canvas.Children.Add(text);
            }

            // --- Случай 2: Обрезана высота (отход сверху) ---
            else if (Math.Abs(optHeight - fullHeight) > 1)
            {
                var text = new TextBlock
                {
                    Text = ((int)displayHeight).ToString(),
                    FontSize = 36,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Red,
                    Background = Brushes.White,
                    IsHitTestVisible = false,
                    RenderTransform = normalizeTransform,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };

                // Y: Сразу за линией обрезки
                Canvas.SetTop(text, optHeight + 10);

                // X: По центру ширины
                Canvas.SetLeft(text, (fullWidth / 2) - 20);

                canvas.Children.Add(text);
            }
        }

        private void DrawGrid(Canvas canvas, double width, double height)
        {
            // Вертикальные линии сетки
            for (double x = GridStep; x < width; x += GridStep)
            {
                canvas.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 5, 5 }
                });
            }
            // Крайняя линия полного листа (сплошная)
            canvas.Children.Add(new Line
            {
                X1 = width,
                Y1 = 0,
                X2 = width,
                Y2 = height,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            });

            // Горизонтальные линии сетки
            for (double y = GridStep; y < height; y += GridStep)
            {
                canvas.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 5, 5 }
                });
            }
            // Крайняя линия полного листа (сплошная)
            canvas.Children.Add(new Line
            {
                X1 = 0,
                Y1 = height,
                X2 = width,
                Y2 = height,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            });
        }

        private void DrawLabels(double width, double height)
        {
            _labelsCanvas.Children.Clear();

            // --- Ось X (Ширина) ---

            // 1. Рисуем стандартные шаги (500, 1000, ... до тех пор, пока x < width)
            for (double x = GridStep; x < width; x += GridStep)
            {
                AddLabelX(x, height);
            }

            // 2. Принудительно рисуем подпись крайнего значения (width)
            // Проверяем, не нарисовали ли мы её уже в цикле (если width кратно 500, последний шаг цикла мог быть равен width, 
            // но условие цикла x < width предотвращает это. 
            // Пример: width=3000. Цикл: 500, 1000, 1500, 2000, 2500. Стоп. 3000 не нарисована.
            // Значит, нужно рисовать всегда.
            if (width > 0)
            {
                AddLabelX(width, height);
            }

            // --- Ось Y (Высота) ---

            // 1. Рисуем стандартные шаги
            for (double y = GridStep; y < height; y += GridStep)
            {
                AddLabelY(y, height);
            }

            // 2. Принудительно рисуем подпись крайнего значения (height)
            if (height > 0)
            {
                AddLabelY(height, height);
            }

            // --- Ноль (0,0) ---
            var originLabel = new TextBlock
            {
                Text = "0",
                FontSize = 40,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Red,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(originLabel, -LabelMarginLeft * 0.8);
            Canvas.SetTop(originLabel, height + 10);
            _labelsCanvas.Children.Add(originLabel);
        }

        private void AddLabelX(double x, double height)
        {
            // Проверка на дублирование координат не нужна, так как цикл идет строго меньше width,
            // а этот вызов идет ровно для width.
            var text = new TextBlock
            {
                Text = ((int)x).ToString(),
                FontSize = 40,
                Foreground = Brushes.Gray,
                IsHitTestVisible = false
            };
            // Центрируем текст относительно линии
            Canvas.SetLeft(text, LabelMarginLeft + x - 20);
            Canvas.SetTop(text, height + 10);
            _labelsCanvas.Children.Add(text);
        }

        private void AddLabelY(double y, double height)
        {
            var text = new TextBlock
            {
                Text = ((int)y).ToString(),
                FontSize = 40,
                Foreground = Brushes.Gray,
                IsHitTestVisible = false
            };

            double screenY = height - y;
            Canvas.SetLeft(text, 0);
            Canvas.SetTop(text, screenY - 10);
            _labelsCanvas.Children.Add(text);
        }

        private void AddPartToCanvas(Part part, double x, double y)
        {
            if (part.DisplayGeometry is null) return;

            var geometry = PartPreviewGenerator.CloneGeometry(part.DisplayGeometry);
            if (geometry == null) return;

            // Центрирование геометрии внутри её bounding box не требуется, если она уже нормализована,
            // но смещение нужно, чтобы координаты X,Y соответствовали левому нижнему углу детали.
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