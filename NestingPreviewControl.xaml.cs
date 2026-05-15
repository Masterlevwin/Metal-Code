using Metal_Code.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private NestingSheet _currentSheet = null!;
        public NestingPreviewControl() => InitializeUI();


        //-------------Базовая отрисовка листа с деталями----------//
        #region
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

                AddPartToCanvas(placement.Part, placement.X, placement.Y, placement.Rotation);
            }
        }

        /// <summary>
        /// Пересоздает структуру canvas, сетку и подписи под новые размеры листа
        /// </summary>
        private void RebuildLayout(NestingSheet sheet)
        {
            _currentSheet = sheet;

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

        private void AddPartToCanvas(Part part, double x, double y, double rotation = 0)
        {
            if (part.DisplayGeometry is null) return;

            var geometry = PartPreviewGenerator.CloneGeometry(part.DisplayGeometry);
            if (geometry == null) return;

            var bounds = geometry.Bounds;

            // Получаем размеры с учётом поворота
            var (partWidth, partHeight) = NestingHelper.GetPartDimensions(part, rotation);

            // Сдвигаем геометрию к (0,0)
            double offsetX = -bounds.Left;
            double offsetY = -bounds.Top;

            // Центр геометрии для поворота
            double centerX = bounds.Width / 2;
            double centerY = bounds.Height / 2;

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
                ToolTip = $"{part.Title}\n{partWidth:0}×{partHeight:0}мм{(rotation != 0 ? " (↻)" : "")}",
                IsHitTestVisible = true
            };

            var transformGroup = new TransformGroup();

            // 1️. Сдвиг геометрии к началу координат
            transformGroup.Children.Add(new TranslateTransform(offsetX, offsetY));

            // 2️. Поворот вокруг ЦЕНТРА детали
            // Знак "-" компенсирует инверсию Y у родительского _invertedLayer
            if (Math.Abs(rotation) > 0.1)
            {
                transformGroup.Children.Add(new RotateTransform(-rotation, centerX, centerY));
            }

            path.RenderTransform = transformGroup;

            // 3️. Корректировка позиции для компенсации изменения габаритов
            // При повороте на 90° ширина и высота меняются местами
            // Нужно сместить так, чтобы нижний левый угол детали остался в (x, y)
            double adjustedX = x;
            double adjustedY = y;

            if (Math.Abs(rotation - 90) < 0.1)
            {
                // Компенсация смещения при повороте на 90°
                // Деталь 1400×50 → 50×1400, центр смещается
                adjustedX = x + (bounds.Height - bounds.Width) / 2;
                adjustedY = y + (bounds.Width - bounds.Height) / 2;
            }

            Canvas.SetLeft(path, adjustedX);
            Canvas.SetTop(path, adjustedY);

            path.PreviewMouseRightButtonUp += (s, e) => OnPartRightClick(part, e);
            _partsCanvas.Children.Add(path);
        }
        #endregion

        //-------------Заполнение листа----------//
        #region
        private readonly List<Path> _previewPaths = new();  // Поле для хранения путей предпросмотра

        /// <summary>
        /// Обработчик правого клика по детали — показывает контекстное меню
        /// </summary>
        private void OnPartRightClick(Part part, MouseButtonEventArgs e)
        {
            e.Handled = true;

            var menu = new ContextMenu();

            var fillItem = new MenuItem { Header = "Дополнить лист" };
            fillItem.Click += (s, args) => FillSheetWithPart(part);

            menu.Items.Add(fillItem);
            menu.IsOpen = true;
        }

        /// <summary>
        /// Пытается заполнить текущий лист копиями указанной детали
        /// </summary>
        private void FillSheetWithPart(Part part)
        {
            // 1. Создаём полный клон листа для тестов
            var testSheet = new NestingSheet
            {
                Id = _currentSheet.Id,
                StockWidth = _currentSheet.StockWidth,
                StockHeight = _currentSheet.StockHeight,
                OptimizedWidth = _currentSheet.OptimizedWidth,
                OptimizedHeight = _currentSheet.OptimizedHeight,
                Parts = _currentSheet.Parts.Select(p => new PartPlacement
                {
                    Part = p.Part,
                    X = p.X,
                    Y = p.Y,
                    Rotation = p.Rotation  // ← КРИТИЧНО ВАЖНО!
                }).ToList()
            };

            // 2. Запускаем нестинг на клоне
            int placedCount = NestingHelper.TryFillSheetWithPart(testSheet, part);
            if (placedCount == 0)
            {
                MessageBox.Show("Нет свободного места для размещения детали.", "Предпросмотр", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Выделяем только НОВЫЕ размещения
            var newPlacements = testSheet.Parts.Skip(_currentSheet.Parts.Count).ToList();

            // 4. Рисуем предпросмотр (жирные зелёные детали)
            DrawPreviewParts(newPlacements);

            // 5. Запрашиваем подтверждение
            var result = MessageBox.Show(
                $"На листе будет размещено ещё {placedCount} шт. \"{part.Title}\".\nПрименить изменения?",
                "Подтверждение заполнения",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            // 6. Убираем предпросмотр
            ClearPreview();

            if (result == MessageBoxResult.Yes)
            {
                // ✅ ЗАМЕНЯЕМ текущий лист на тестовый (со всеми новыми деталями)
                _currentSheet = testSheet;

                // Полная перерисовка (ShowSheet сам очистит _partsCanvas и нарисует всё заново)
                ShowSheet(_currentSheet);

                // Пересчитываем стоимость и количество
                RecalculateFromSheet(_currentSheet, part, placedCount);

                MainWindow.M.StatusBegin($"Успешно добавлено {placedCount} шт.", MainWindow.StatusMessageType.Success);
            }
        }

        /// <summary>
        /// Пересчитывает параметры LaserItem на основе обновлённого NestingSheet
        /// </summary>
        public static void RecalculateFromSheet(NestingSheet sheet, Part part, int placed)
        {
            bool updated = false;

            foreach (var detail in MainWindow.M.DetailControls)
            {
                if (updated) break;
                foreach (var typeDetail in detail.TypeDetailControls)
                {
                    if (updated) break;
                    foreach (var work in typeDetail.WorkControls)
                    {
                        if (updated) break;
                        if (work.workType is CutControl cut && cut.Items?.Count > 0 && cut.PartDetails is not null)
                        {
                            var item = cut.Items.FirstOrDefault(i => i.NestingSheet?.Id == sheet.Id);
                            if (item is not null)
                            {
                                Part? _part = cut.PartDetails.FirstOrDefault(p => p.Title == part.Title);
                                if (_part is not null) _part.Count += placed;

                                // Пересчитываем параметры
                                var metal = MainWindow.M.Metals?.FirstOrDefault(m => m.Name == item.metal);
                                if (metal != null)
                                {
                                    // Площадь и масса
                                    double sheetArea = sheet.OptimizedWidth * sheet.OptimizedHeight;
                                    double sheetMass = sheetArea * sheet.Parts[0].Part.Destiny * metal.Density / 1_000_000;

                                    // Длина реза и проколы
                                    double sheetWay = sheet.Parts.Sum(p => p.Part.Way);
                                    int sheetPinholes = sheet.Parts.Sum(p =>
                                        int.TryParse(p.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var val) ? val : 0);

                                    // Обновляем свойства LaserItem
                                    item.way = (float)sheetWay;
                                    item.pinholes = sheetPinholes;
                                    item.mass = (float)sheetMass;

                                    // Обновляем размер, если оптимизация изменилась
                                    item.sheetSize = $"{sheet.OptimizedWidth:0}x{sheet.OptimizedHeight:0}";

                                    // Обновляем ссылку на визуализацию листа
                                    item.NestingSheet = sheet;
                                }

                                // Обновляем итоговые значения
                                cut.WayTotal = cut.PartDetails.Sum(p => p.Way * p.Count);
                                cut.MassTotal = cut.PartDetails.Sum(p => p.Mass * p.Count);
                                cut.SumProperties(cut.Items);
                                cut.work.type.MassCalculate();

                                updated = true;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Рисует новые детали жирным зелёным контуром поверх существующих
        /// </summary>
        private void DrawPreviewParts(List<PartPlacement> placements)
        {
            ClearPreview(); // На всякий случай очищаем старое

            foreach (var placement in placements)
            {
                var path = CreatePreviewPath(placement.Part, placement.X, placement.Y, placement.Rotation);
                if (path != null)
                {
                    _partsCanvas.Children.Add(path);
                    _previewPaths.Add(path);
                }
            }
        }

        /// <summary>
        /// Создаёт Path для предпросмотра: жирный, без пунктира, полупрозрачный
        /// </summary>
        private Path? CreatePreviewPath(Part part, double x, double y, double rotation = 0)
        {
            if (part.DisplayGeometry is null) return null;

            var geometry = PartPreviewGenerator.CloneGeometry(part.DisplayGeometry);
            if (geometry == null) return null;

            var bounds = geometry.Bounds;
            var (partWidth, partHeight) = NestingHelper.GetPartDimensions(part, rotation);

            double offsetX = -bounds.Left;
            double offsetY = -bounds.Top;
            double centerX = bounds.Width / 2;
            double centerY = bounds.Height / 2;

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new TranslateTransform(offsetX, offsetY));

            if (Math.Abs(rotation) > 0.1)
            {
                transformGroup.Children.Add(new RotateTransform(-rotation, centerX, centerY));
            }

            double adjustedX = x;
            double adjustedY = y;

            if (Math.Abs(rotation - 90) < 0.1)
            {
                adjustedX = x + (bounds.Height - bounds.Width) / 2;
                adjustedY = y + (bounds.Width - bounds.Height) / 2;
            }

            var path = new Path
            {
                Data = geometry,
                Fill = new SolidColorBrush(Color.FromArgb(70, 0, 180, 60)),
                Stroke = Brushes.LimeGreen,
                StrokeThickness = 2.5,
                StrokeLineJoin = PenLineJoin.Round,
                RenderTransform = transformGroup,
                IsHitTestVisible = false,
                Opacity = 0.85
            };

            Canvas.SetLeft(path, adjustedX);
            Canvas.SetTop(path, adjustedY);

            return path;
        }

        /// <summary>
        /// Удаляет детали предпросмотра с холста
        /// </summary>
        private void ClearPreview()
        {
            foreach (var path in _previewPaths)
                _partsCanvas.Children.Remove(path);
            _previewPaths.Clear();
        }
        #endregion
    }
}