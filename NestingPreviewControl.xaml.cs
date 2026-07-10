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

        // Состояние перетаскивания
        private Path? _draggedPath;
        private PartPlacement? _draggedPlacement;
        private Point _startMouseLogicalPos;
        private double _startPartX, _startPartY;

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

            _invertedLayer.PreviewMouseMove += InvertedLayer_PreviewMouseMove;
            _invertedLayer.PreviewMouseLeftButtonUp += InvertedLayer_PreviewMouseLeftButtonUp;
            _invertedLayer.PreviewKeyDown += InvertedLayer_PreviewKeyDown;

            // Подписки для группового выделения
            _invertedLayer.PreviewMouseLeftButtonDown += InvertedLayer_GroupSelectMouseDown;
            _invertedLayer.PreviewMouseLeftButtonUp += InvertedLayer_GroupSelectMouseUp;
            _invertedLayer.PreviewKeyDown += InvertedLayer_EscapeHandler;
        }

        /// <summary>
        /// Перестраивает интерфейс под размеры конкретного листа и отображает детали.
        /// </summary>
        public void ShowSheet(NestingSheet sheet)
        {
            // При полной перерисовке снимаем выделение, чтобы не было "висящих" ссылок
            _selectedPlacements.Clear();
            _originalStrokes.Clear();

            if (sheet == null || sheet.Parts == null)
            {
                _partsCanvas?.Children.Clear();
                return;
            }

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

                // ИЗМЕНЕНО: Передаем весь placement
                AddPartToCanvas(placement);
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

        private void AddPartToCanvas(PartPlacement placement)
        {
            var part = placement.Part;
            if (part.DisplayGeometry is null) return;

            var geometry = PartPreviewGenerator.CloneGeometry(part.DisplayGeometry);
            if (geometry == null) return;

            var path = new Path
            {
                Data = geometry,
                Fill = part.PartType == PartType.Round
                    ? new SolidColorBrush(Color.FromArgb(150, 255, 150, 150))
                    : new SolidColorBrush(Color.FromArgb(150, 150, 200, 255)),
                Stroke = part.PartType == PartType.Round ? Brushes.DarkRed : Brushes.DarkBlue,
                StrokeThickness = 1.5,
                IsHitTestVisible = true,
                Tag = placement
            };

            var (partWidth, partHeight) = NestingHelper.GetPartDimensions(part, placement.Rotation);
            path.ToolTip = $"{part.Title}\n{partWidth:0}×{partHeight:0}мм{(placement.Rotation != 0 ? " (↻)" : "")}";

            ApplyPlacementToPath(path, placement);

            path.PreviewMouseLeftButtonDown += Path_PreviewMouseLeftButtonDown;
            path.PreviewMouseRightButtonUp += (s, e) => OnPartRightClick(part, e);

            _partsCanvas.Children.Add(path);
        }
        #endregion


        //-------------Интеркативность раскладки-------------//
        #region
        private void ApplyPlacementToPath(Path path, PartPlacement placement)
        {
            var geometry = path.Data as Geometry;
            if (geometry == null) return;

            var bounds = geometry.Bounds;
            double offsetX = -bounds.Left;
            double offsetY = -bounds.Top;
            double centerX = bounds.Width / 2;
            double centerY = bounds.Height / 2;

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new TranslateTransform(offsetX, offsetY));

            if (Math.Abs(placement.Rotation) > 0.1)
            {
                transformGroup.Children.Add(new RotateTransform(-placement.Rotation, centerX, centerY));
            }

            path.RenderTransform = transformGroup;

            double adjustedX = placement.X;
            double adjustedY = placement.Y;

            if (Math.Abs(placement.Rotation - 90) < 0.1 || Math.Abs(placement.Rotation - 270) < 0.1)
            {
                adjustedX = placement.X + (bounds.Height - bounds.Width) / 2;
                adjustedY = placement.Y + (bounds.Width - bounds.Height) / 2;
            }

            Canvas.SetLeft(path, adjustedX);
            Canvas.SetTop(path, adjustedY);
        }

        private void Path_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Path path || path.Tag is not PartPlacement placement) return;

            // 🔥 Если деталь выделена — начинаем групповое перемещение
            if (_selectedPlacements.Contains(placement))
            {
                _isDraggingGroup = true;
                _groupDragStartPoint = e.GetPosition(_invertedLayer);

                // Сохраняем исходные позиции всех выделенных деталей
                _groupOriginalPositions.Clear();
                foreach (var selected in _selectedPlacements)
                {
                    _groupOriginalPositions[selected] = (selected.X, selected.Y);
                }

                // Визуальное выделение
                path.Stroke = Brushes.Orange;
                path.StrokeThickness = 3;

                _invertedLayer.Focusable = true;
                _invertedLayer.Focus();
                _invertedLayer.CaptureMouse();

                e.Handled = true;
                return;
            }

            // Одиночное перемещение (существующая логика)
            _draggedPath = path;
            _draggedPlacement = placement;

            _startMouseLogicalPos = e.GetPosition(_invertedLayer);
            _startPartX = placement.X;
            _startPartY = placement.Y;

            path.Stroke = Brushes.Orange;
            path.StrokeThickness = 3;

            _partsCanvas.Children.Remove(path);
            _partsCanvas.Children.Add(path);

            bool isValid = NestingHelper.IsValidPlacement(_currentSheet, placement, placement.X, placement.Y, placement.Rotation);
            DrawValidationZones(placement.X, placement.Y, placement.Rotation, isValid);

            _invertedLayer.Focusable = true;
            _invertedLayer.Focus();
            _invertedLayer.CaptureMouse();

            e.Handled = true;
        }

        private void InvertedLayer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point currentPos = e.GetPosition(_invertedLayer);

            // 🔥 Групповое выделение (rubber band)
            if (_isSelecting && _selectionRect != null)
            {
                double x = Math.Min(_selectionStartPoint.X, currentPos.X);
                double y = Math.Min(_selectionStartPoint.Y, currentPos.Y);
                double width = Math.Abs(currentPos.X - _selectionStartPoint.X);
                double height = Math.Abs(currentPos.Y - _selectionStartPoint.Y);

                Canvas.SetLeft(_selectionRect, x);
                Canvas.SetTop(_selectionRect, y);
                _selectionRect.Width = width;
                _selectionRect.Height = height;
                return;
            }

            // 🔥 Групповое перемещение
            if (_isDraggingGroup && _groupOriginalPositions.Any())
            {
                double deltaX = currentPos.X - _groupDragStartPoint.X;
                double deltaY = currentPos.Y - _groupDragStartPoint.Y;

                var newPositions = new Dictionary<PartPlacement, (double X, double Y)>();
                foreach (var kvp in _groupOriginalPositions)
                {
                    var placement = kvp.Key;
                    var (origX, origY) = kvp.Value;
                    newPositions[placement] = (Math.Round(origX + deltaX), Math.Round(origY + deltaY));
                }

                bool isValid = IsGroupPlacementValid(newPositions);

                foreach (var kvp in newPositions)
                {
                    var placement = kvp.Key;
                    var (newX, newY) = kvp.Value;

                    var path = _partsCanvas.Children.OfType<Path>().FirstOrDefault(p => p.Tag == placement);
                    if (path != null)
                    {
                        path.Stroke = isValid ? Brushes.LimeGreen : Brushes.Red;
                        ApplyPlacementToPath(path, new PartPlacement
                        {
                            Part = placement.Part,
                            X = newX,
                            Y = newY,
                            Rotation = placement.Rotation
                        });
                    }
                }
                return;
            }

            // Одиночное перемещение
            if (_draggedPath == null || _draggedPlacement == null) return;

            double deltaXSingle = currentPos.X - _startMouseLogicalPos.X;
            double deltaYSingle = currentPos.Y - _startMouseLogicalPos.Y;
            double newXS = Math.Round(_startPartX + deltaXSingle);
            double newYS = Math.Round(_startPartY + deltaYSingle);

            bool isValidSingle = NestingHelper.IsValidPlacement(_currentSheet, _draggedPlacement, newXS, newYS, _draggedPlacement.Rotation);

            _draggedPath.Stroke = isValidSingle ? Brushes.LimeGreen : Brushes.Red;
            DrawValidationZones(newXS, newYS, _draggedPlacement.Rotation, isValidSingle);

            ApplyPlacementToPath(_draggedPath, new PartPlacement
            {
                Part = _draggedPlacement.Part,
                X = newXS,
                Y = newYS,
                Rotation = _draggedPlacement.Rotation
            });
        }

        private void InvertedLayer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point endPos = e.GetPosition(_invertedLayer);

            // 🔥 Завершение группового перемещения
            if (_isDraggingGroup)
            {
                _isDraggingGroup = false;
                _invertedLayer.ReleaseMouseCapture();

                double deltaX = endPos.X - _groupDragStartPoint.X;
                double deltaY = endPos.Y - _groupDragStartPoint.Y;

                var finalPositions = new Dictionary<PartPlacement, (double X, double Y)>();
                foreach (var kvp in _groupOriginalPositions)
                {
                    var placement = kvp.Key;
                    var (origX, origY) = kvp.Value;
                    finalPositions[placement] = (Math.Round(origX + deltaX), Math.Round(origY + deltaY));
                }

                if (IsGroupPlacementValid(finalPositions))
                {
                    foreach (var kvp in finalPositions)
                    {
                        kvp.Key.X = kvp.Value.X;
                        kvp.Key.Y = kvp.Value.Y;
                    }

                    NestingHelper.OptimizeSheetSize(_currentSheet);
                    RebuildLayout(_currentSheet);
                    ShowSheet(_currentSheet);
                    RecalculateAfterManualEdit();

                    MainWindow.M.StatusBegin($"Группа из {_selectedPlacements.Count} деталей перемещена",
                        MainWindow.StatusMessageType.Success);
                }
                else
                {
                    foreach (var kvp in _groupOriginalPositions)
                    {
                        kvp.Key.X = kvp.Value.X;
                        kvp.Key.Y = kvp.Value.Y;
                    }

                    RebuildLayout(_currentSheet);
                    ShowSheet(_currentSheet);

                    MainWindow.M.StatusBegin("Не удалось переместить группу — нет свободного места",
                        MainWindow.StatusMessageType.Warning);
                }

                _groupOriginalPositions.Clear();
                ClearSelection();
                return;
            }

            // Одиночное перемещение
            if (_draggedPath == null || _draggedPlacement == null) return;

            double newXS = Math.Round(_startPartX + (endPos.X - _startMouseLogicalPos.X));
            double newYS = Math.Round(_startPartY + (endPos.Y - _startMouseLogicalPos.Y));

            var currentPath = _draggedPath;
            var currentPlacement = _draggedPlacement;

            bool isValid = NestingHelper.IsValidPlacement(_currentSheet, _draggedPlacement, newXS, newYS, _draggedPlacement.Rotation);

            if (isValid)
            {
                _draggedPlacement.X = newXS;
                _draggedPlacement.Y = newYS;
            }
            else
            {
                var result = FindNearestValidPosition(_draggedPlacement, newXS, newYS, searchRadius: 150);

                if (result.Found)
                {
                    _draggedPlacement.X = result.X;
                    _draggedPlacement.Y = result.Y;

                    _draggedPath.Stroke = Brushes.Yellow;
                    System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _draggedPath.Stroke = currentPlacement.Part.PartType == PartType.Round
                                ? Brushes.DarkRed
                                : Brushes.DarkBlue;
                            _draggedPath.StrokeThickness = 1.5;
                        });
                    });

                    MainWindow.M.StatusBegin($"Деталь автоматически размещена в позиции ({result.X:0}, {result.Y:0})",
                        MainWindow.StatusMessageType.Info);
                }
                else
                {
                    ApplyPlacementToPath(_draggedPath, _draggedPlacement);
                    MainWindow.M.StatusBegin("Не удалось найти подходящее место для детали",
                        MainWindow.StatusMessageType.Warning);

                    ClearValidationVisuals();
                    currentPath.Stroke = currentPlacement.Part.PartType == PartType.Round ? Brushes.DarkRed : Brushes.DarkBlue;
                    currentPath.StrokeThickness = 1.5;

                    _draggedPath = null;
                    _draggedPlacement = null;
                    _invertedLayer.ReleaseMouseCapture();
                    return;
                }
            }

            NestingHelper.OptimizeSheetSize(_currentSheet);
            RebuildLayout(_currentSheet);
            ShowSheet(_currentSheet);
            RecalculateAfterManualEdit();

            if (isValid)
            {
                MainWindow.M.StatusBegin("Раскладка обновлена", MainWindow.StatusMessageType.Success);
            }

            ClearValidationVisuals();

            currentPath.Stroke = currentPlacement.Part.PartType == PartType.Round ? Brushes.DarkRed : Brushes.DarkBlue;
            currentPath.StrokeThickness = 1.5;

            _draggedPath = null;
            _draggedPlacement = null;
            _invertedLayer.ReleaseMouseCapture();
        }

        private void InvertedLayer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 🔥 Групповой поворот вокруг центра группы
            if (_selectedPlacements.Any() && (e.Key == Key.Home || e.Key == Key.End))
            {
                double rotationDelta = e.Key == Key.Home ? 90 : -90;
                double rotationRad = rotationDelta * Math.PI / 180.0;

                // 1. Находим центр группы
                double groupCenterX = 0, groupCenterY = 0;
                foreach (var selPlc in _selectedPlacements)
                {
                    var (w, h) = NestingHelper.GetPartDimensions(selPlc.Part, selPlc.Rotation);
                    groupCenterX += selPlc.X + w / 2;
                    groupCenterY += selPlc.Y + h / 2;
                }
                groupCenterX /= _selectedPlacements.Count;
                groupCenterY /= _selectedPlacements.Count;

                // 2. Рассчитываем новые позиции и повороты
                var newStates = new Dictionary<PartPlacement, (double X, double Y, double Rotation)>();
                foreach (var curPlc in _selectedPlacements)
                {
                    var (curW, curH) = NestingHelper.GetPartDimensions(curPlc.Part, curPlc.Rotation);
                    double partCenterX = curPlc.X + curW / 2;
                    double partCenterY = curPlc.Y + curH / 2;

                    double dx = partCenterX - groupCenterX;
                    double dy = partCenterY - groupCenterY;
                    double rotCenterX = groupCenterX + dx * Math.Cos(rotationRad) - dy * Math.Sin(rotationRad);
                    double rotCenterY = groupCenterY + dx * Math.Sin(rotationRad) + dy * Math.Cos(rotationRad);

                    double targetRot = (curPlc.Rotation + rotationDelta + 360) % 360;
                    var (rotW, rotH) = NestingHelper.GetPartDimensions(curPlc.Part, targetRot);

                    double targetX = rotCenterX - rotW / 2;
                    double targetY = rotCenterY - rotH / 2;

                    newStates[curPlc] = (Math.Round(targetX), Math.Round(targetY), targetRot);
                }

                // 3. Проверяем валидность (игнорируем выделенные детали при проверке коллизий)
                bool allValid = true;
                string failReason = "";
                PartPlacement collisionPartner = null;

                foreach (var state in newStates)
                {
                    var plcToCheck = state.Key;
                    var (chkX, chkY, chkRot) = state.Value;
                    var (chkW, chkH) = NestingHelper.GetPartDimensions(plcToCheck.Part, chkRot);

                    if (chkX < NestingHelper.Spacing || chkY < NestingHelper.Spacing ||
                        chkX + chkW > _currentSheet.Width - NestingHelper.Spacing ||
                        chkY + chkH > _currentSheet.Height - NestingHelper.Spacing)
                    {
                        failReason = "Деталь выходит за границы листа после поворота";
                        allValid = false;
                        break;
                    }

                    foreach (var existing in _currentSheet.Parts)
                    {
                        if (_selectedPlacements.Contains(existing)) continue;

                        var (exW, exH) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);

                        if (chkX + chkW + NestingHelper.Spacing <= existing.X) continue;
                        if (existing.X + exW + NestingHelper.Spacing <= chkX) continue;
                        if (chkY + chkH + NestingHelper.Spacing <= existing.Y) continue;
                        if (existing.Y + exH + NestingHelper.Spacing <= chkY) continue;

                        failReason = $"Коллизия с '{existing.Part.Title}'";
                        collisionPartner = existing;
                        allValid = false;
                        break;
                    }
                    if (!allValid) break;
                }

                if (allValid)
                {
                    foreach (var apply in newStates)
                    {
                        var plcApply = apply.Key;
                        var (appX, appY, appRot) = apply.Value;

                        plcApply.X = appX;
                        plcApply.Y = appY;
                        plcApply.Rotation = appRot;

                        var pathApply = _partsCanvas.Children.OfType<Path>().FirstOrDefault(p => p.Tag == plcApply);
                        if (pathApply != null)
                        {
                            ApplyPlacementToPath(pathApply, plcApply);
                            var (finW, finH) = NestingHelper.GetPartDimensions(plcApply.Part, appRot);
                            pathApply.ToolTip = $"{plcApply.Part.Title}\n{finW:0}×{finH:0}мм (↻)";
                        }
                    }

                    UpdateSelectionVisuals();
                    NestingHelper.OptimizeSheetSize(_currentSheet);
                    RebuildLayout(_currentSheet);
                    ShowSheet(_currentSheet);
                    RecalculateAfterManualEdit();

                    MainWindow.M.StatusBegin($"Группа из {_selectedPlacements.Count} деталей повёрнута на {rotationDelta}°",
                        MainWindow.StatusMessageType.Success);
                }
                else
                {
                    System.Media.SystemSounds.Beep.Play();
                    MainWindow.M.StatusBegin($"Не удалось повернуть группу: {failReason}",
                        MainWindow.StatusMessageType.Warning);

                    // 🔥 Визуальная подсказка: подсвечиваем деталь-препятствие красным
                    if (collisionPartner != null)
                    {
                        var obstaclePath = _partsCanvas.Children.OfType<Path>()
                            .FirstOrDefault(p => p.Tag == collisionPartner);
                        if (obstaclePath != null)
                        {
                            var originalStroke = obstaclePath.Stroke;
                            obstaclePath.Stroke = Brushes.Red;
                            obstaclePath.StrokeThickness = 4;

                            System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    obstaclePath.Stroke = originalStroke;
                                    obstaclePath.StrokeThickness = 1.5;
                                });
                            });
                        }
                    }
                }

                e.Handled = true;
                return;
            }

            // Одиночный поворот (существующая логика)
            if (_draggedPath?.Tag is not PartPlacement singlePlc) return;

            if (e.Key == Key.Home || e.Key == Key.End)
            {
                if (singlePlc.Part.PartType == PartType.Round) return;

                double singleRot = singlePlc.Rotation;
                if (e.Key == Key.Home)
                    singleRot = (singlePlc.Rotation + 90) % 360;
                else if (e.Key == Key.End)
                    singleRot = (singlePlc.Rotation - 90 + 360) % 360;

                bool singleOk = NestingHelper.IsValidPlacement(_currentSheet, singlePlc, singlePlc.X, singlePlc.Y, singleRot);

                if (singleOk)
                {
                    singlePlc.Rotation = singleRot;
                    ApplyPlacementToPath(_draggedPath, singlePlc);
                    DrawValidationZones(singlePlc.X, singlePlc.Y, singleRot, true);

                    var (sw, sh) = NestingHelper.GetPartDimensions(singlePlc.Part, singleRot);
                    _draggedPath.ToolTip = $"{singlePlc.Part.Title}\n{sw:0}×{sh:0}мм (↻)";
                }
                else
                {
                    _draggedPath.Stroke = Brushes.Red;
                    _draggedPath.StrokeThickness = 4;

                    System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _draggedPath.Stroke = Brushes.Orange;
                            _draggedPath.StrokeThickness = 3;
                        });
                    });

                    System.Media.SystemSounds.Beep.Play();
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// Пересчитывает массу и размер листа после ручного перемещения детали.
        /// </summary>
        private void RecalculateAfterManualEdit()
        {
            foreach (var detail in MainWindow.M.DetailControls)
                foreach (var typeDetail in detail.TypeDetailControls)
                    foreach (var work in typeDetail.WorkControls)
                        if (work.workType is CutControl cut && cut.Items != null)
                        {
                            var item = cut.Items.FirstOrDefault(i => i.NestingSheet?.Id == _currentSheet.Id);
                            if (item == null) continue;

                            double sheetArea = _currentSheet.OptimizedWidth * _currentSheet.OptimizedHeight;
                            double destiny = double.TryParse(item.destiny, out var d) ? d : 0;
                            var metal = MainWindow.M.Metals?.FirstOrDefault(m => m.Name == item.metal);
                            double density = metal?.Density ?? 0;

                            item.mass = (float)(sheetArea * destiny * density / 1_000_000);
                            item.sheetSize = $"{_currentSheet.OptimizedWidth:0}x{_currentSheet.OptimizedHeight:0}";

                            cut.SumProperties(cut.Items);
                            cut.work.type.CreateSort();
                            cut.work.type.MassCalculate();

                            break;
                        }
        }

        private readonly List<Shape> _validationVisuals = new();

        /// <summary>
        /// Рисует зоны запрета вокруг всех деталей и footprint перемещаемой детали.
        /// </summary>
        private void DrawValidationZones(double movingX, double movingY, double movingRotation, bool isValid)
        {
            ClearValidationVisuals();
            _partsCanvas.UpdateLayout();

            // 1. Зоны запрета вокруг всех деталей
            foreach (var placement in _currentSheet.Parts)
            {
                if (ReferenceEquals(placement, _draggedPlacement)) continue;

                if (placement.Part == _draggedPlacement?.Part &&
                    Math.Abs(placement.X - _draggedPlacement.X) < 0.1 &&
                    Math.Abs(placement.Y - _draggedPlacement.Y) < 0.1 &&
                    Math.Abs(placement.Rotation - _draggedPlacement.Rotation) < 0.1)
                    continue;

                var (w, h) = NestingHelper.GetPartDimensions(placement.Part, placement.Rotation);

                var zoneRect = new Rectangle
                {
                    Width = w + NestingHelper.Spacing * 2,
                    Height = h + NestingHelper.Spacing * 2,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0)),
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 3 },
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(zoneRect, placement.X - NestingHelper.Spacing);
                Canvas.SetTop(zoneRect, placement.Y - NestingHelper.Spacing);

                _partsCanvas.Children.Add(zoneRect);
                _validationVisuals.Add(zoneRect);
            }

            // 2. Границы листа
            var boundaryRect = new Rectangle
            {
                Width = _currentSheet.Width - NestingHelper.Spacing * 2,
                Height = _currentSheet.Height - NestingHelper.Spacing * 2,
                Fill = Brushes.Transparent,
                Stroke = new SolidColorBrush(Color.FromArgb(100, 0, 100, 255)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 5 },
                IsHitTestVisible = false
            };

            Canvas.SetLeft(boundaryRect, NestingHelper.Spacing);
            Canvas.SetTop(boundaryRect, NestingHelper.Spacing);

            _partsCanvas.Children.Add(boundaryRect);
            _validationVisuals.Add(boundaryRect);

            if (_draggedPlacement is null) return;

            // 3. Footprint перемещаемой детали
            var (movingW, movingH) = NestingHelper.GetPartDimensions(_draggedPlacement.Part, movingRotation);

            var footprintRect = new Rectangle
            {
                Width = movingW,
                Height = movingH,
                Fill = isValid
                    ? new SolidColorBrush(Color.FromArgb(60, 0, 255, 0))
                    : new SolidColorBrush(Color.FromArgb(60, 255, 0, 0)),
                Stroke = isValid ? Brushes.LimeGreen : Brushes.Red,
                StrokeThickness = 2,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(footprintRect, movingX);
            Canvas.SetTop(footprintRect, movingY);

            _partsCanvas.Children.Add(footprintRect);
            _validationVisuals.Add(footprintRect);
        }

        /// <summary>
        /// Удаляет все визуальные элементы зон валидности.
        /// </summary>
        private void ClearValidationVisuals()
        {
            var visualsToRemove = _validationVisuals.ToList();

            foreach (var visual in visualsToRemove)
            {
                if (_partsCanvas.Children.Contains(visual))
                {
                    _partsCanvas.Children.Remove(visual);
                }
            }

            _validationVisuals.Clear();
        }

        /// <summary>
        /// Ищет ближайшую валидную позицию для детали в радиусе searchRadius.
        /// Приоритет: позиции относительно существующих деталей, затем сетка.
        /// </summary>
        private (bool Found, double X, double Y) FindNearestValidPosition(
            PartPlacement placement,
            double currentX,
            double currentY,
            double searchRadius = 100)
        {
            var candidates = new List<(double X, double Y, double Distance)>();
            var (w, h) = NestingHelper.GetPartDimensions(placement.Part, placement.Rotation);

            // 1. Генерируем кандидаты вокруг существующих деталей
            foreach (var existing in _currentSheet.Parts)
            {
                if (ReferenceEquals(existing, placement)) continue;

                var (ew, eh) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);

                // Позиции вокруг существующей детали (слева, справа, снизу, сверху)
                var positions = new[]
                {
            (X: existing.X + ew + NestingHelper.Spacing, Y: existing.Y),                    // Справа
            (X: existing.X - w - NestingHelper.Spacing, Y: existing.Y),                     // Слева
            (X: existing.X, Y: existing.Y + eh + NestingHelper.Spacing),                    // Сверху
            (X: existing.X, Y: existing.Y - h - NestingHelper.Spacing),                     // Снизу
            // Угловые позиции
            (X: existing.X + ew + NestingHelper.Spacing, Y: existing.Y + eh + NestingHelper.Spacing),
            (X: existing.X - w - NestingHelper.Spacing, Y: existing.Y + eh + NestingHelper.Spacing),
            (X: existing.X + ew + NestingHelper.Spacing, Y: existing.Y - h - NestingHelper.Spacing),
            (X: existing.X - w - NestingHelper.Spacing, Y: existing.Y - h - NestingHelper.Spacing),
        };

                foreach (var pos in positions)
                {
                    // Проверяем, находится ли кандидат в радиусе поиска
                    double distance = Math.Sqrt(Math.Pow(pos.X - currentX, 2) + Math.Pow(pos.Y - currentY, 2));
                    if (distance <= searchRadius)
                    {
                        // Округляем до целых
                        double roundedX = Math.Round(pos.X);
                        double roundedY = Math.Round(pos.Y);

                        // Проверяем валидность
                        if (NestingHelper.IsValidPlacement(_currentSheet, placement, roundedX, roundedY, placement.Rotation))
                        {
                            candidates.Add((roundedX, roundedY, distance));
                        }
                    }
                }
            }

            // 2. Если не нашли кандидатов вокруг деталей, сканируем сетку вокруг точки отпускания
            if (!candidates.Any())
            {
                int step = 10; // Шаг сетки в мм
                int radiusSteps = (int)(searchRadius / step);

                for (int dx = -radiusSteps; dx <= radiusSteps; dx++)
                {
                    for (int dy = -radiusSteps; dy <= radiusSteps; dy++)
                    {
                        double testX = Math.Round(currentX + dx * step);
                        double testY = Math.Round(currentY + dy * step);

                        if (NestingHelper.IsValidPlacement(_currentSheet, placement, testX, testY, placement.Rotation))
                        {
                            double distance = Math.Sqrt(Math.Pow(testX - currentX, 2) + Math.Pow(testY - currentY, 2));
                            candidates.Add((testX, testY, distance));
                        }
                    }
                }
            }

            // 3. Возвращаем ближайшую валидную позицию
            if (candidates.Any())
            {
                var best = candidates.OrderBy(c => c.Distance).First();
                return (true, best.X, best.Y);
            }

            return (false, 0, 0);
        }


        // --- Групповое выделение ---
        private readonly List<PartPlacement> _selectedPlacements = new();
        private Rectangle _selectionRect;          // Rubber band
        private Point _selectionStartPoint;        // Точка начала выделения
        private bool _isSelecting;                 // Идёт ли процесс выделения
        private bool _isDraggingGroup;             // Идёт ли групповое перемещение
        private Dictionary<Path, Brush> _originalStrokes = new(); // Оригинальные цвета для восстановления
        private Dictionary<Path, Brush> _originalFills = new(); // Оригинальные заливки для восстановления
        private Point _groupDragStartPoint;
        private Dictionary<PartPlacement, (double X, double Y)> _groupOriginalPositions = new();

        /// <summary>
        /// Начало выделения: клик по пустому месту на листе.
        /// </summary>
        private void InvertedLayer_GroupSelectMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Path) return;

            ClearSelection();

            _selectionStartPoint = e.GetPosition(_invertedLayer);
            _isSelecting = true;

            _selectionRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 6, 3 },
                Fill = new SolidColorBrush(Color.FromArgb(70, 0, 120, 215)),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(_selectionRect, _selectionStartPoint.X);
            Canvas.SetTop(_selectionRect, _selectionStartPoint.Y);

            // 🔥 Добавляем напрямую в _invertedLayer, чтобы быть поверх всего
            _invertedLayer.Children.Add(_selectionRect);

            _invertedLayer.CaptureMouse();
            e.Handled = true;
        }

        /// <summary>
        /// Завершение выделения: отпускаем мышь и определяем попавшие детали.
        /// </summary>
        private void InvertedLayer_GroupSelectMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;

            _isSelecting = false;
            _invertedLayer.ReleaseMouseCapture();

            // Удаляем rubber band из того же контейнера
            if (_selectionRect != null)
            {
                _invertedLayer.Children.Remove(_selectionRect);  // ← Изменили с _partsCanvas на _invertedLayer
                _selectionRect = null;
            }

            // Определяем конечную точку
            Point endPoint = e.GetPosition(_invertedLayer);

            // Строим прямоугольник выделения (нормализуем, если тянули влево/вверх)
            double x = Math.Min(_selectionStartPoint.X, endPoint.X);
            double y = Math.Min(_selectionStartPoint.Y, endPoint.Y);
            double w = Math.Abs(endPoint.X - _selectionStartPoint.X);
            double h = Math.Abs(endPoint.Y - _selectionStartPoint.Y);

            // Если выделение слишком маленькое (просто клик) — не выделяем ничего
            if (w < 5 || h < 5) return;

            var selectionBounds = new Rect(x, y, w, h);

            // Находим все детали, чьи габариты попали в прямоугольник
            foreach (var placement in _currentSheet.Parts)
            {
                var (pw, ph) = NestingHelper.GetPartDimensions(placement.Part, placement.Rotation);
                var partBounds = new Rect(placement.X, placement.Y, pw, ph);

                if (selectionBounds.IntersectsWith(partBounds) || selectionBounds.Contains(partBounds))
                {
                    _selectedPlacements.Add(placement);
                }
            }

            UpdateSelectionVisuals();

            if (_selectedPlacements.Count > 0)
            {
                MainWindow.M.StatusBegin($"Выделено деталей: {_selectedPlacements.Count}",
                    MainWindow.StatusMessageType.Info);
            }
        }

        /// <summary>
        /// Обновляет визуальное отображение выделенных деталей (золотая обводка).
        /// </summary>
        private void UpdateSelectionVisuals()
        {
            // Восстанавливаем оригинальные цвета у всех деталей
            foreach (var child in _partsCanvas.Children.OfType<Path>())
            {
                if (child.Tag is PartPlacement placement)
                {
                    if (_originalStrokes.TryGetValue(child, out var originalBrush))
                    {
                        child.Stroke = originalBrush;
                        child.StrokeThickness = 1.5;
                    }

                    // Восстанавливаем оригинальную заливку
                    if (_originalFills.TryGetValue(child, out var originalFill))
                    {
                        child.Fill = originalFill;
                    }
                }
            }
            _originalStrokes.Clear();
            _originalFills.Clear();

            // 🔥 Подсвечиваем выделенные детали: зелёная заливка + синяя обводка
            foreach (var child in _partsCanvas.Children.OfType<Path>())
            {
                if (child.Tag is PartPlacement placement && _selectedPlacements.Contains(placement))
                {
                    _originalStrokes[child] = child.Stroke;
                    _originalFills[child] = child.Fill;

                    child.Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)); // Синяя обводка
                    child.StrokeThickness = 2.5;
                    child.Fill = new SolidColorBrush(Color.FromArgb(150, 0, 255, 0)); // Зелёная заливка
                }
            }
        }

        /// <summary>
        /// Снимает выделение со всех деталей.
        /// </summary>
        private void ClearSelection()
        {
            _selectedPlacements.Clear();
            UpdateSelectionVisuals();
        }

        /// <summary>
        /// Обработчик Escape для снятия выделения.
        /// </summary>
        private void InvertedLayer_EscapeHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _selectedPlacements.Any())
            {
                ClearSelection();
                MainWindow.M.StatusBegin("Выделение снято", MainWindow.StatusMessageType.Info);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Проверяет, можно ли разместить всю группу деталей в новых позициях.
        /// </summary>
        private bool IsGroupPlacementValid(Dictionary<PartPlacement, (double X, double Y)> newPositions)
        {
            foreach (var kvp in newPositions)
            {
                var placement = kvp.Key;
                var (newX, newY) = kvp.Value;

                if (!NestingHelper.IsValidPlacement(_currentSheet, placement, newX, newY, placement.Rotation))
                {
                    return false;
                }
            }

            return true;
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