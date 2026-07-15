using Metal_Code.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private Canvas _uiLayer = null!;

        private double _currentSheetWidth;
        private double _currentSheetHeight;
        private NestingSheet _currentSheet = null!;

        // === Состояние интерактивности ===
        private readonly List<PartPlacement> _activePlacements = new();
        private readonly Dictionary<PartPlacement, Path> _activePaths = new();
        private readonly Dictionary<PartPlacement, (double X, double Y)> _originalPositions = new();
        private readonly Dictionary<Path, (Brush Stroke, Brush Fill)> _originalVisuals = new();
        private Point _dragStartPoint;
        private bool _isDragging;

        // === Состояние выделения ===
        private readonly List<PartPlacement> _selectedPlacements = new();
        private Rectangle? _selectionRect;
        private Point _selectionStartPoint;
        private bool _isSelecting;

        // === Состояние непрерывного копирования ===
        private bool _isCopyOperation;
        private bool _isContinuousCopying;
        private CopyAxis _lockedAxis;
        private PartPlacement? _baseCopyPart;
        private double _baseCopyX;
        private double _baseCopyY;
        private Point _mouseDownPos;
        private readonly List<PartPlacement> _pendingContinuousCopies = new();
        private readonly List<Path> _pendingContinuousPaths = new();
        public bool IsContinuousCopyMode { get; private set; } = false;
        private enum CopyAxis { None, X, Y }

        // === Визуализация зон валидности ===
        private readonly List<Shape> _validationVisuals = new();

        // === Предпросмотр заполнения ===
        private readonly List<Path> _previewPaths = new();

        public NestingPreviewControl() => InitializeUI();

        //-------------Базовая отрисовка листа с деталями----------//
        #region
        private void InitializeUI()
        {
            // === СЛОЙ 1: Масштабируемый контент (лист, детали, сетка) ===
            _rootViewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                ClipToBounds = false,
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _rootCanvas = new Canvas { Background = Brushes.White };
            _invertedLayer = new Canvas();
            _partsCanvas = new Canvas();
            _labelsCanvas = new Canvas();

            _invertedLayer.Children.Add(_partsCanvas);
            _rootCanvas.Children.Add(_invertedLayer);
            _rootCanvas.Children.Add(_labelsCanvas);

            _rootViewbox.Child = _rootCanvas;

            // === СЛОЙ 2: НЕ масштабируемый UI (кнопки, переключатели) ===
            _uiLayer = new Canvas
            {
                IsHitTestVisible = true,
                ClipToBounds = false
            };

            // Основной контейнер: Grid с двумя слоями
            var mainGrid = new Grid();
            mainGrid.Children.Add(_rootViewbox); // Слой 1 (снизу)
            mainGrid.Children.Add(_uiLayer);     // Слой 2 (сверху)

            Content = mainGrid;

            // === События ===
            _invertedLayer.PreviewMouseLeftButtonDown += InvertedLayer_MouseLeftButtonDown;
            _invertedLayer.PreviewMouseMove += InvertedLayer_PreviewMouseMove;
            _invertedLayer.PreviewMouseLeftButtonUp += InvertedLayer_PreviewMouseLeftButtonUp;

            PreviewKeyDown += NestingPreviewControl_PreviewKeyDown;
        }

        public void ShowSheet(NestingSheet sheet)
        {
            ResetInteractionState();

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
                AddPartToCanvas(placement);
            }
        }

        /// <summary>
        /// Полный сброс состояния интерактивности.
        /// </summary>
        private void ResetInteractionState()
        {
            _selectedPlacements.Clear();
            _activePlacements.Clear();
            _activePaths.Clear();
            _originalPositions.Clear();
            _originalVisuals.Clear();
            _isDragging = false;
            _isSelecting = false;
            ClearValidationVisuals();
            ClearPreview();
        }

        private void RebuildLayout(NestingSheet sheet)
        {
            _currentSheet = sheet;
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
            DrawCutLine(_invertedLayer, sheet.OptimizedWidth, sheet.OptimizedHeight, width, height);

            Canvas.SetLeft(_invertedLayer, LabelMarginLeft);
            Canvas.SetTop(_invertedLayer, 0);

            DrawLabels(width, height);
        }

        private void DrawCutLine(Canvas canvas, double optWidth, double optHeight, double fullWidth, double fullHeight)
        {
            if (Math.Abs(optWidth - fullWidth) < 1 && Math.Abs(optHeight - fullHeight) < 1)
                return;

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

            double displayWidth = Math.Ceiling(optWidth / 100) * 100;
            double displayHeight = Math.Ceiling(optHeight / 100) * 100;
            var normalizeTransform = new ScaleTransform { ScaleX = 1, ScaleY = -1 };

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
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                Canvas.SetLeft(text, optWidth + 10);
                Canvas.SetTop(text, (fullHeight / 2) - 20);
                canvas.Children.Add(text);
            }
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
                Canvas.SetTop(text, optHeight + 10);
                Canvas.SetLeft(text, (fullWidth / 2) - 20);
                canvas.Children.Add(text);
            }
        }

        private void DrawGrid(Canvas canvas, double width, double height)
        {
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
            canvas.Children.Add(new Line
            {
                X1 = width,
                Y1 = 0,
                X2 = width,
                Y2 = height,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            });

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

            for (double x = GridStep; x < width; x += GridStep) AddLabelX(x, height);
            if (width > 0) AddLabelX(width, height);

            for (double y = GridStep; y < height; y += GridStep) AddLabelY(y, height);
            if (height > 0) AddLabelY(height, height);

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
            var text = new TextBlock
            {
                Text = ((int)x).ToString(),
                FontSize = 40,
                Foreground = Brushes.Gray,
                IsHitTestVisible = false
            };
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
            Canvas.SetLeft(text, 0);
            Canvas.SetTop(text, height - y - 10);
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


        //-------------Интерактивность раскладки-------------//
        #region
        private void ApplyPlacementToPath(Path path, PartPlacement placement)
        {
            var geometry = path.Data as Geometry;
            if (geometry == null) return;

            var bounds = geometry.Bounds;
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new TranslateTransform(-bounds.Left, -bounds.Top));

            if (Math.Abs(placement.Rotation) > 0.1)
                transformGroup.Children.Add(new RotateTransform(-placement.Rotation, bounds.Width / 2, bounds.Height / 2));

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

            // 🔥 ПРИОРИТЕТ: Режим непрерывного копирования (активируется на любой детали)
            if (IsContinuousCopyMode)
            {
                e.Handled = true;

                // Сбрасываем старое выделение и выбираем только эту деталь как базу для копирования
                ClearSelection();
                _selectedPlacements.Add(placement);
                // Если у вас есть метод визуального выделения (например, HighlightPlacement), вызовите его здесь

                _isContinuousCopying = true;
                _lockedAxis = CopyAxis.None;
                _baseCopyPart = placement;
                _baseCopyX = placement.X;
                _baseCopyY = placement.Y;
                _mouseDownPos = e.GetPosition(_invertedLayer);

                _pendingContinuousCopies.Clear();
                _pendingContinuousPaths.Clear();

                _invertedLayer.CaptureMouse();
                return; // Прерываем выполнение, чтобы не сработала стандартная логика
            }

            // --- СТАНДАРТНАЯ ЛОГИКА (Перемещение или однократное копирование через SHIFT) ---
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            List<PartPlacement> toDrag;
            if (_selectedPlacements.Contains(placement))
            {
                toDrag = _selectedPlacements.ToList();
            }
            else
            {
                ClearSelection();
                toDrag = new List<PartPlacement> { placement };
            }

            if (isShiftPressed)
            {
                toDrag = CopyPlacements(toDrag);
                if (!toDrag.Any()) return;
                StartDrag(toDrag, isCopying: true);
            }
            else
            {
                StartDrag(toDrag, isCopying: false);
            }

            e.Handled = true;
        }

        /// <summary>
        /// Создаёт копии указанных деталей и добавляет их в текущий лист.
        /// Возвращает список новых PartPlacement.
        /// </summary>
        private List<PartPlacement> CopyPlacements(List<PartPlacement> source)
        {
            var copies = new List<PartPlacement>();

            foreach (var src in source)
            {
                var copy = new PartPlacement
                {
                    Part = src.Part,
                    X = src.X,
                    Y = src.Y,
                    Rotation = src.Rotation
                };
                copies.Add(copy);
                _currentSheet.Parts.Add(copy);
            }

            // Увеличиваем количество деталей
            var grouped = copies.GroupBy(c => c.Part);
            foreach (var group in grouped)
            {
                group.Key.Count += group.Count();
                // 🔥 Явно уведомляем об изменении Count
                group.Key.NotifyTotalChanged();
            }

            // Перерисовываем лист БЕЗ сброса состояния интерактивности
            _partsCanvas.Children.Clear();
            foreach (var placement in _currentSheet.Parts)
            {
                if (placement.Part.DisplayGeometry == null)
                    PartPreviewGenerator.EnsureDisplayGeometry(placement.Part);
                AddPartToCanvas(placement);
            }

            return copies;
        }

        /// <summary>
        /// Начинает перетаскивание указанных деталей.
        /// </summary>
        private void StartDrag(List<PartPlacement> placements, bool isCopying = false)
        {
            _isCopyOperation = isCopying;

            _activePlacements.Clear();
            _activePaths.Clear();
            _originalPositions.Clear();
            _originalVisuals.Clear();

            foreach (var plc in placements)
            {
                _activePlacements.Add(plc);
                _originalPositions[plc] = (plc.X, plc.Y);

                var path = _partsCanvas.Children.OfType<Path>().FirstOrDefault(p => p.Tag == plc);
                if (path != null)
                {
                    _activePaths[plc] = path;
                    _originalVisuals[path] = (path.Stroke, path.Fill);

                    // Поднимаем наверх (Z-order)
                    _partsCanvas.Children.Remove(path);
                    _partsCanvas.Children.Add(path);

                    path.Stroke = Brushes.Orange;
                    path.StrokeThickness = 3;
                }
            }

            _dragStartPoint = Mouse.GetPosition(_invertedLayer);
            _isDragging = true;

            _invertedLayer.Focusable = true;
            _invertedLayer.Focus();
            _invertedLayer.CaptureMouse();

            if (_activePlacements.Count == 1)
            {
                var first = _activePlacements[0];
                bool isValid = NestingHelper.IsValidPlacement(_currentSheet, first, first.X, first.Y, first.Rotation);
                DrawValidationZones(first.X, first.Y, first.Rotation, isValid);
            }
        }

        private void InvertedLayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
            _invertedLayer.Children.Add(_selectionRect);

            _invertedLayer.CaptureMouse();
            e.Handled = true;
        }

        private void InvertedLayer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point currentPos = e.GetPosition(_invertedLayer);

            // 🔥 ЛОГИКА НЕПРЕРЫВНОГО КОПИРОВАНИЯ (МАССИВА)
            if (_isContinuousCopying && _baseCopyPart != null)
            {
                double _deltaX = currentPos.X - _mouseDownPos.X;
                double _deltaY = currentPos.Y - _mouseDownPos.Y;

                // 1. Определяем ось при сдвиге > 10 пикселей
                if (_lockedAxis == CopyAxis.None)
                {
                    if (Math.Abs(_deltaX) > 10 || Math.Abs(_deltaY) > 10)
                    {
                        _lockedAxis = Math.Abs(_deltaX) > Math.Abs(_deltaY) ? CopyAxis.X : CopyAxis.Y;
                    }
                }

                // 2. Генерируем шлейф
                if (_lockedAxis != CopyAxis.None)
                {
                    var (w, h) = NestingHelper.GetPartDimensions(_baseCopyPart.Part, _baseCopyPart.Rotation);

                    // ВАЖНО: Убедитесь, что NestingHelper.Spacing существует. Если нет, замените на константу, например, 5.0
                    double step = (_lockedAxis == CopyAxis.X ? w : h) + NestingHelper.Spacing;

                    double delta = _lockedAxis == CopyAxis.X ? _deltaX : _deltaY;
                    int direction = Math.Sign(delta);
                    int steps = (int)(Math.Abs(delta) / step);

                    ClearContinuousPreview();

                    for (int i = 1; i <= steps; i++)
                    {
                        double candidateX = _baseCopyX + (_lockedAxis == CopyAxis.X ? (i * step * direction) : 0);
                        double candidateY = _baseCopyY + (_lockedAxis == CopyAxis.Y ? (i * step * direction) : 0);

                        // 🔥 Создаем временный объект для честной проверки коллизий
                        var tempPlc = new PartPlacement
                        {
                            Part = _baseCopyPart.Part,
                            X = candidateX,
                            Y = candidateY,
                            Rotation = _baseCopyPart.Rotation
                        };

                        // Если ваш метод IsValidPlacement имеет другую сигнатуру, адаптируйте эту строку:
                        if (NestingHelper.IsValidPlacement(_currentSheet, tempPlc, tempPlc.X, tempPlc.Y, tempPlc.Rotation))
                        {
                            _pendingContinuousCopies.Add(tempPlc);

                            var path = CreatePreviewPath(_baseCopyPart.Part, candidateX, candidateY, _baseCopyPart.Rotation);
                            if (path != null)
                            {
                                _partsCanvas.Children.Add(path); // Убедитесь, что имя Canvas верное (_partsCanvas или _rootCanvas)
                                _pendingContinuousPaths.Add(path);
                            }
                        }
                        else
                        {
                            // Столкновение или выход за границы: прерываем шлейф
                            break;
                        }
                    }
                }
            }

            // Rubber band
            if (_isSelecting && _selectionRect != null)
            {
                double x = Math.Min(_selectionStartPoint.X, currentPos.X);
                double y = Math.Min(_selectionStartPoint.Y, currentPos.Y);
                double w = Math.Abs(currentPos.X - _selectionStartPoint.X);
                double h = Math.Abs(currentPos.Y - _selectionStartPoint.Y);

                Canvas.SetLeft(_selectionRect, x);
                Canvas.SetTop(_selectionRect, y);
                _selectionRect.Width = w;
                _selectionRect.Height = h;
                return;
            }

            if (!_isDragging || !_activePlacements.Any()) return;

            double deltaX = currentPos.X - _dragStartPoint.X;
            double deltaY = currentPos.Y - _dragStartPoint.Y;

            var newPositions = new Dictionary<PartPlacement, (double X, double Y)>();
            foreach (var plc in _activePlacements)
            {
                var (origX, origY) = _originalPositions[plc];
                newPositions[plc] = (Math.Round(origX + deltaX), Math.Round(origY + deltaY));
            }

            bool isValid = IsGroupPlacementValid(newPositions, _activePlacements);

            foreach (var plc in _activePlacements)
            {
                if (!_activePaths.TryGetValue(plc, out var path)) continue;
                var (newX, newY) = newPositions[plc];

                plc.X = newX;
                plc.Y = newY;

                path.Stroke = isValid ? Brushes.LimeGreen : Brushes.Red;
                ApplyPlacementToPath(path, new PartPlacement
                {
                    Part = plc.Part,
                    X = newX,
                    Y = newY,
                    Rotation = plc.Rotation
                });
            }

            if (_activePlacements.Count == 1)
            {
                var only = _activePlacements[0];
                DrawValidationZones(only.X, only.Y, only.Rotation, isValid);
            }
        }

        private void InvertedLayer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 🔥 ЗАВЕРШЕНИЕ НЕПРЕРЫВНОГО КОПИРОВАНИЯ
            if (_isContinuousCopying)
            {
                int addedCount = 0;
                foreach (var copy in _pendingContinuousCopies)
                {
                    _currentSheet.Parts.Add(copy);
                    copy.Part.Count++;
                    copy.Part.NotifyTotalChanged();
                    addedCount++;
                }

                ClearContinuousPreview();
                _invertedLayer.ReleaseMouseCapture();

                // Сброс флагов
                _isContinuousCopying = false;
                _lockedAxis = CopyAxis.None;
                _baseCopyPart = null;

                if (addedCount > 0)
                {
                    NestingHelper.OptimizeSheetSize(_currentSheet);
                    RebuildLayout(_currentSheet);
                    ShowSheet(_currentSheet);
                    RecalculateSheetParameters();
                    MainWindow.M.StatusBegin($"Добавлено копий: {addedCount}", MainWindow.StatusMessageType.Success);
                }
                else
                {
                    MainWindow.M.StatusBegin("Копирование отменено: нет свободного места", MainWindow.StatusMessageType.Warning);
                }

                return; // Прерываем выполнение, чтобы не сработала стандартная логика MouseUp
            }

            Point endPos = e.GetPosition(_invertedLayer);

            if (_isSelecting)
            {
                FinishSelection(endPos);
                return;
            }

            if (!_isDragging || !_activePlacements.Any()) return;

            double deltaX = endPos.X - _dragStartPoint.X;
            double deltaY = endPos.Y - _dragStartPoint.Y;

            var finalPositions = new Dictionary<PartPlacement, (double X, double Y)>();
            foreach (var plc in _activePlacements)
            {
                var (origX, origY) = _originalPositions[plc];
                finalPositions[plc] = (Math.Round(origX + deltaX), Math.Round(origY + deltaY));
            }

            bool wasSingle = _activePlacements.Count == 1;

            // 🔥 Определяем, были ли это копии (они уже в _currentSheet.Parts)
            bool wasCopying = _activePlacements.All(p => _currentSheet.Parts.Contains(p) &&
                                                         !_originalPositions.ContainsKey(p) == false);
            // Упрощённо: если детали были скопированы, они уже в _currentSheet.Parts с самого начала

            if (IsGroupPlacementValid(finalPositions, _activePlacements))
            {
                foreach (var plc in _activePlacements)
                {
                    plc.X = finalPositions[plc].X;
                    plc.Y = finalPositions[plc].Y;
                }

                CommitChanges($"Перемещено деталей: {_activePlacements.Count}");
            }
            else if (wasSingle)
            {
                var only = _activePlacements[0];
                var (targetX, targetY) = finalPositions[only];
                var result = FindNearestValidPosition(only, targetX, targetY, searchRadius: 150);

                if (result.Found)
                {
                    only.X = result.X;
                    only.Y = result.Y;

                    if (_activePaths.TryGetValue(only, out var path))
                    {
                        path.Stroke = Brushes.Yellow;
                        System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (_activePaths.TryGetValue(only, out var p))
                                    RestoreVisual(p);
                            });
                        });
                    }

                    CommitChanges($"Деталь автоматически размещена в позиции ({result.X:0}, {result.Y:0})");
                }
                else
                {
                    RollbackDrag();
                    MainWindow.M.StatusBegin("Не удалось найти подходящее место для детали",
                        MainWindow.StatusMessageType.Warning);
                }
            }
            else
            {
                RollbackDrag();
                MainWindow.M.StatusBegin("Не удалось переместить группу — нет свободного места",
                    MainWindow.StatusMessageType.Warning);
            }

            FinishDrag();
        }

        private void ClearContinuousPreview()
        {
            foreach (var path in _pendingContinuousPaths)
            {
                if (_partsCanvas.Children.Contains(path)) // Или _rootCanvas, в зависимости от того, куда вы добавляете path
                {
                    _partsCanvas.Children.Remove(path);
                }
            }
            _pendingContinuousPaths.Clear();
            _pendingContinuousCopies.Clear();
        }

        private void FinishSelection(Point endPoint)
        {
            _isSelecting = false;
            _invertedLayer.ReleaseMouseCapture();

            if (_selectionRect != null)
            {
                _invertedLayer.Children.Remove(_selectionRect);
                _selectionRect = null;
            }

            double x = Math.Min(_selectionStartPoint.X, endPoint.X);
            double y = Math.Min(_selectionStartPoint.Y, endPoint.Y);
            double w = Math.Abs(endPoint.X - _selectionStartPoint.X);
            double h = Math.Abs(endPoint.Y - _selectionStartPoint.Y);

            if (w < 5 || h < 5) return;

            var selectionBounds = new Rect(x, y, w, h);

            foreach (var placement in _currentSheet.Parts)
            {
                var (pw, ph) = NestingHelper.GetPartDimensions(placement.Part, placement.Rotation);
                var partBounds = new Rect(placement.X, placement.Y, pw, ph);

                if (selectionBounds.IntersectsWith(partBounds) || selectionBounds.Contains(partBounds))
                    _selectedPlacements.Add(placement);
            }

            UpdateSelectionVisuals();

            if (_selectedPlacements.Count > 0)
                MainWindow.M.StatusBegin($"Выделено деталей: {_selectedPlacements.Count}",
                    MainWindow.StatusMessageType.Info);
        }

        private void CommitChanges(string statusMessage)
        {
            NestingHelper.OptimizeSheetSize(_currentSheet);
            RebuildLayout(_currentSheet);
            ShowSheet(_currentSheet);
            RecalculateSheetParameters();
            MainWindow.M.StatusBegin(statusMessage, MainWindow.StatusMessageType.Success);
        }

        private void RollbackDrag()
        {
            bool wasCopying = _isCopyOperation;

            foreach (var plc in _activePlacements)
            {
                if (wasCopying)
                {
                    _currentSheet.Parts.Remove(plc);
                    plc.Part.Count--;
                    plc.Part.NotifyTotalChanged();
                }
                else if (_originalPositions.TryGetValue(plc, out var orig))
                {
                    plc.X = orig.X;
                    plc.Y = orig.Y;
                }
            }

            RebuildLayout(_currentSheet);
            ShowSheet(_currentSheet);

            // 🔥 Пересчитываем параметры после любой отмены
            RecalculateSheetParameters();
        }

        private void FinishDrag()
        {
            _isDragging = false;
            _invertedLayer.ReleaseMouseCapture();

            foreach (var path in _activePaths.Values)
                RestoreVisual(path);

            _activePlacements.Clear();
            _activePaths.Clear();
            _originalPositions.Clear();
            _originalVisuals.Clear();
            ClearValidationVisuals();
        }

        private void RestoreVisual(Path path)
        {
            if (_originalVisuals.TryGetValue(path, out var vis))
            {
                path.Stroke = vis.Stroke;
                path.Fill = vis.Fill;
                path.StrokeThickness = 1.5;
            }
            else
            {
                var placement = path.Tag as PartPlacement;
                path.Stroke = placement?.Part.PartType == PartType.Round ? Brushes.DarkRed : Brushes.DarkBlue;
                path.StrokeThickness = 1.5;
            }
        }

        private void NestingPreviewControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_selectedPlacements.Any())
                {
                    ClearSelection();
                    MainWindow.M.StatusBegin("Выделение снято", MainWindow.StatusMessageType.Info);
                    e.Handled = true;
                }
                return;
            }

            // 🔥 Удаление деталей
            if (e.Key == Key.Delete)
            {
                DeleteSelectedParts();
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Home && e.Key != Key.End) return;

            var targets = _isDragging && _activePlacements.Any()
                ? _activePlacements.ToList()
                : _selectedPlacements.ToList();

            if (!targets.Any()) return;

            RotateTargets(targets, e.Key == Key.Home ? 90 : -90, skipValidation: _isDragging);
            e.Handled = true;
        }

        /// <summary>
        /// Удаляет выделенные детали с листа.
        /// </summary>
        private void DeleteSelectedParts()
        {
            var candidates = _selectedPlacements.Any()
                ? _selectedPlacements.ToList()
                : _activePlacements.ToList();

            if (!candidates.Any())
            {
                MainWindow.M.StatusBegin("Нет деталей для удаления", MainWindow.StatusMessageType.Warning);
                return;
            }

            var toDelete = candidates.Where(c => _currentSheet.Parts.Contains(c)).ToList();

            if (!toDelete.Any())
            {
                MainWindow.M.StatusBegin("Выделенные детали отсутствуют на листе", MainWindow.StatusMessageType.Warning);
                return;
            }

            foreach (var placement in toDelete)
            {
                _currentSheet.Parts.Remove(placement);
                placement.Part.Count--;
                placement.Part.NotifyTotalChanged();
            }

            MainWindow.M.StatusBegin($"Удалено деталей: {toDelete.Count}", MainWindow.StatusMessageType.Success);

            ClearSelection();
            _activePlacements.Clear();

            NestingHelper.OptimizeSheetSize(_currentSheet);
            RebuildLayout(_currentSheet);
            ShowSheet(_currentSheet);
            RecalculateSheetParameters();
        }

        /// <summary>
        /// Поворачивает указанные детали вокруг их общего центра.
        /// </summary>
        /// <param name="skipValidation">Если true, не проверяет коллизии и границы (во время перетаскивания).</param>
        private void RotateTargets(List<PartPlacement> targets, double rotationDelta, bool skipValidation = false)
        {
            double rotationRad = rotationDelta * Math.PI / 180.0;

            // 1. Находим центр группы
            double groupCenterX = 0, groupCenterY = 0;
            foreach (var plc in targets)
            {
                var (w, h) = NestingHelper.GetPartDimensions(plc.Part, plc.Rotation);
                groupCenterX += plc.X + w / 2;
                groupCenterY += plc.Y + h / 2;
            }
            groupCenterX /= targets.Count;
            groupCenterY /= targets.Count;

            // 2. Рассчитываем новые состояния
            var newStates = new Dictionary<PartPlacement, (double X, double Y, double Rotation)>();
            foreach (var plc in targets)
            {
                if (plc.Part.PartType == PartType.Round) continue;

                var (curW, curH) = NestingHelper.GetPartDimensions(plc.Part, plc.Rotation);
                double partCenterX = plc.X + curW / 2;
                double partCenterY = plc.Y + curH / 2;

                double dx = partCenterX - groupCenterX;
                double dy = partCenterY - groupCenterY;
                double rotCenterX = groupCenterX + dx * Math.Cos(rotationRad) - dy * Math.Sin(rotationRad);
                double rotCenterY = groupCenterY + dx * Math.Sin(rotationRad) + dy * Math.Cos(rotationRad);

                double targetRot = (plc.Rotation + rotationDelta + 360) % 360;
                var (rotW, rotH) = NestingHelper.GetPartDimensions(plc.Part, targetRot);

                double targetX = rotCenterX - rotW / 2;
                double targetY = rotCenterY - rotH / 2;

                newStates[plc] = (Math.Round(targetX), Math.Round(targetY), targetRot);
            }

            if (!newStates.Any()) return;

            // 3. Проверка валидности
            if (!skipValidation)
            {
                string failReason = "";
                PartPlacement? collisionPartner = null;

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
                        break;
                    }

                    foreach (var existing in _currentSheet.Parts)
                    {
                        if (targets.Contains(existing)) continue;

                        var (exW, exH) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);

                        if (chkX + chkW + NestingHelper.Spacing <= existing.X) continue;
                        if (existing.X + exW + NestingHelper.Spacing <= chkX) continue;
                        if (chkY + chkH + NestingHelper.Spacing <= existing.Y) continue;
                        if (existing.Y + exH + NestingHelper.Spacing <= chkY) continue;

                        failReason = $"Коллизия с '{existing.Part.Title}'";
                        collisionPartner = existing;
                        break;
                    }
                    if (!string.IsNullOrEmpty(failReason)) break;
                }

                if (!string.IsNullOrEmpty(failReason))
                {
                    System.Media.SystemSounds.Beep.Play();
                    MainWindow.M.StatusBegin($"Не удалось повернуть: {failReason}",
                        MainWindow.StatusMessageType.Warning);

                    if (collisionPartner != null)
                    {
                        var obstaclePath = _partsCanvas.Children.OfType<Path>()
                            .FirstOrDefault(p => p.Tag == collisionPartner);
                        if (obstaclePath != null)
                        {
                            var originalStroke = obstaclePath.Stroke;
                            var originalThickness = obstaclePath.StrokeThickness;
                            obstaclePath.Stroke = Brushes.Red;
                            obstaclePath.StrokeThickness = 4;

                            System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    obstaclePath.Stroke = originalStroke;
                                    obstaclePath.StrokeThickness = originalThickness;
                                });
                            });
                        }
                    }
                    return;
                }
            }

            // 4. Применяем повороты
            foreach (var state in newStates)
            {
                var plc = state.Key;
                var (appX, appY, appRot) = state.Value;

                plc.X = appX;
                plc.Y = appY;
                plc.Rotation = appRot;

                var path = _partsCanvas.Children.OfType<Path>().FirstOrDefault(p => p.Tag == plc);
                if (path != null)
                {
                    ApplyPlacementToPath(path, plc);
                    var (finW, finH) = NestingHelper.GetPartDimensions(plc.Part, appRot);
                    path.ToolTip = $"{plc.Part.Title}\n{finW:0}×{finH:0}мм (↻)";
                }

                if (_isDragging && _originalPositions.ContainsKey(plc))
                    _originalPositions[plc] = (appX, appY);
            }

            UpdateSelectionVisuals();

            if (_isDragging)
            {
                _dragStartPoint = Mouse.GetPosition(_invertedLayer);
                ClearValidationVisuals();
                NestingHelper.OptimizeSheetSize(_currentSheet);
            }
            else
            {
                CommitChanges($"Повернуто деталей: {newStates.Count} на {rotationDelta}°");
            }
        }

        private void UpdateSelectionVisuals()
        {
            foreach (var child in _partsCanvas.Children.OfType<Path>())
            {
                if (child.Tag is PartPlacement && _originalVisuals.TryGetValue(child, out var vis))
                {
                    child.Stroke = vis.Stroke;
                    child.Fill = vis.Fill;
                    child.StrokeThickness = 1.5;
                }
            }
            _originalVisuals.Clear();

            foreach (var child in _partsCanvas.Children.OfType<Path>())
            {
                if (child.Tag is PartPlacement placement && _selectedPlacements.Contains(placement))
                {
                    _originalVisuals[child] = (child.Stroke, child.Fill);
                    child.Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    child.StrokeThickness = 2.5;
                    child.Fill = new SolidColorBrush(Color.FromArgb(150, 0, 255, 0));
                }
            }
        }

        private void ClearSelection()
        {
            _selectedPlacements.Clear();
            UpdateSelectionVisuals();
        }

        private bool IsGroupPlacementValid(Dictionary<PartPlacement, (double X, double Y)> newPositions, List<PartPlacement> ignorePlacements)
        {
            foreach (var kvp in newPositions)
            {
                var plc = kvp.Key;
                var (newX, newY) = kvp.Value;
                var (w, h) = NestingHelper.GetPartDimensions(plc.Part, plc.Rotation);

                if (newX < NestingHelper.Spacing || newY < NestingHelper.Spacing ||
                    newX + w > _currentSheet.Width - NestingHelper.Spacing ||
                    newY + h > _currentSheet.Height - NestingHelper.Spacing)
                    return false;

                foreach (var existing in _currentSheet.Parts)
                {
                    if (ignorePlacements.Contains(existing)) continue;

                    var (exW, exH) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);

                    if (newX + w + NestingHelper.Spacing <= existing.X) continue;
                    if (existing.X + exW + NestingHelper.Spacing <= newX) continue;
                    if (newY + h + NestingHelper.Spacing <= existing.Y) continue;
                    if (existing.Y + exH + NestingHelper.Spacing <= newY) continue;

                    return false;
                }
            }
            return true;
        }

        private void DrawValidationZones(double movingX, double movingY, double movingRotation, bool isValid)
        {
            ClearValidationVisuals();
            _partsCanvas.UpdateLayout();

            // 1. Зоны запрета вокруг всех деталей (кроме активных)
            foreach (var placement in _currentSheet.Parts)
            {
                if (_activePlacements.Contains(placement)) continue;

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

            // 3. Footprint для каждой активной детали
            foreach (var plc in _activePlacements)
            {
                var (mw, mh) = NestingHelper.GetPartDimensions(plc.Part, plc.Rotation);

                var footprintRect = new Rectangle
                {
                    Width = mw,
                    Height = mh,
                    Fill = isValid
                        ? new SolidColorBrush(Color.FromArgb(60, 0, 255, 0))
                        : new SolidColorBrush(Color.FromArgb(60, 255, 0, 0)),
                    Stroke = isValid ? Brushes.LimeGreen : Brushes.Red,
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(footprintRect, plc.X);
                Canvas.SetTop(footprintRect, plc.Y);

                _partsCanvas.Children.Add(footprintRect);
                _validationVisuals.Add(footprintRect);
            }
        }

        private void ClearValidationVisuals()
        {
            var visualsToRemove = _validationVisuals.ToList();
            foreach (var visual in visualsToRemove)
            {
                if (_partsCanvas.Children.Contains(visual))
                    _partsCanvas.Children.Remove(visual);
            }
            _validationVisuals.Clear();
        }

        private (bool Found, double X, double Y) FindNearestValidPosition(
            PartPlacement placement, double currentX, double currentY, double searchRadius = 100)
        {
            var candidates = new List<(double X, double Y, double Distance)>();
            var (w, h) = NestingHelper.GetPartDimensions(placement.Part, placement.Rotation);

            foreach (var existing in _currentSheet.Parts)
            {
                if (ReferenceEquals(existing, placement)) continue;
                var (ew, eh) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);

                var positions = new[]
                {
                    (X: existing.X + ew + NestingHelper.Spacing, Y: existing.Y),
                    (X: existing.X - w - NestingHelper.Spacing, Y: existing.Y),
                    (X: existing.X, Y: existing.Y + eh + NestingHelper.Spacing),
                    (X: existing.X, Y: existing.Y - h - NestingHelper.Spacing),
                    (X: existing.X + ew + NestingHelper.Spacing, Y: existing.Y + eh + NestingHelper.Spacing),
                    (X: existing.X - w - NestingHelper.Spacing, Y: existing.Y + eh + NestingHelper.Spacing),
                    (X: existing.X + ew + NestingHelper.Spacing, Y: existing.Y - h - NestingHelper.Spacing),
                    (X: existing.X - w - NestingHelper.Spacing, Y: existing.Y - h - NestingHelper.Spacing),
                };

                foreach (var pos in positions)
                {
                    double distance = Math.Sqrt(Math.Pow(pos.X - currentX, 2) + Math.Pow(pos.Y - currentY, 2));
                    if (distance > searchRadius) continue;

                    double roundedX = Math.Round(pos.X);
                    double roundedY = Math.Round(pos.Y);

                    if (NestingHelper.IsValidPlacement(_currentSheet, placement, roundedX, roundedY, placement.Rotation))
                        candidates.Add((roundedX, roundedY, distance));
                }
            }

            if (!candidates.Any())
            {
                int step = 10;
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

            if (candidates.Any())
            {
                var best = candidates.OrderBy(c => c.Distance).First();
                return (true, best.X, best.Y);
            }

            return (false, 0, 0);
        }

        /// <summary>
        /// Пересчитывает все параметры листа (way, pinholes, mass, sheetSize) 
        /// и обновляет итоги в CutControl.
        /// Вызывается после любой операции с листом: перемещения, копирования, поворота.
        /// </summary>
        private void RecalculateSheetParameters()
        {
            foreach (var detail in MainWindow.M.DetailControls)
                foreach (var typeDetail in detail.TypeDetailControls)
                    foreach (var work in typeDetail.WorkControls)
                        if (work.workType is CutControl cut && cut.Items != null && cut.PartDetails != null)
                        {
                            var item = cut.Items.FirstOrDefault(i => i.NestingSheet?.Id == _currentSheet.Id);
                            if (item == null) continue;

                            // Длина реза и проколы (сумма по всем деталям на листе, включая копии)
                            double sheetWay = _currentSheet.Parts.Sum(p => p.Part.Way);
                            int sheetPinholes = _currentSheet.Parts.Sum(p =>
                                int.TryParse(p.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var val) ? val : 0);

                            // Масса и размер листа
                            double sheetArea = _currentSheet.OptimizedWidth * _currentSheet.OptimizedHeight;
                            double destiny = double.TryParse(item.destiny, out var d) ? d : 0;
                            var metal = MainWindow.M.Metals?.FirstOrDefault(m => m.Name == item.metal);
                            double density = metal?.Density ?? 0;

                            item.way = (float)sheetWay;
                            item.pinholes = sheetPinholes;
                            item.mass = (float)(sheetArea * destiny * density / 1_000_000);
                            item.sheetSize = $"{_currentSheet.OptimizedWidth:0}x{_currentSheet.OptimizedHeight:0}";
                            item.NestingSheet = _currentSheet;

                            // Обновляем итоги
                            cut.WayTotal = cut.PartDetails.Sum(p => p.Way * p.Count);
                            cut.MassTotal = cut.PartDetails.Sum(p => p.Mass * p.Count);
                            cut.SumProperties(cut.Items);
                            cut.work.type.CreateSort();
                            cut.work.type.MassCalculate();

                            break;
                        }
        }
        #endregion


        //-------------Заполнение листа----------//
        #region
        public void AddContinuousCopyToggle()
        {
            var toggle = new ToggleButton
            {
                Width = 44,
                Height = 44,
                ToolTip = "Режим непрерывного копирования (массив)\nПеретаскивание создаст шлейф копий",
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                // Прикрепляем к правому верхнему углу
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 30, 10, 0)
            };

            toggle.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 315,
                ShadowDepth = 2,
                Opacity = 0.25,
                BlurRadius = 4
            };

            var icon = new Path
            {
                Data = Geometry.Parse("M16,1H4C2.9,1 2,1.9 2,3V17H4V3H16V1M19,5H8C6.9,5 6,5.9 6,7V21C6,22.1 6.9,23 8,23H19C20.1,23 21,22.1 21,21V7C21,5.9 20.1,5 19,5M19,21H8V7H19V21Z"),
                Fill = Brushes.Gray,
                Width = 24,
                Height = 24,
                Stretch = Stretch.Uniform
            };
            toggle.Content = icon;

            toggle.MouseEnter += (s, e) =>
            {
                if (!toggle.IsChecked.GetValueOrDefault())
                {
                    toggle.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                    toggle.BorderBrush = new SolidColorBrush(Color.FromRgb(74, 144, 226));
                }
            };
            toggle.MouseLeave += (s, e) =>
            {
                if (!toggle.IsChecked.GetValueOrDefault())
                {
                    toggle.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                    toggle.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                }
            };

            toggle.Checked += (s, e) =>
            {
                IsContinuousCopyMode = true;
                toggle.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                toggle.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 124, 0));
                icon.Fill = Brushes.White;
                MainWindow.M.StatusBegin("Режим массива включен", MainWindow.StatusMessageType.Info);
            };

            toggle.Unchecked += (s, e) =>
            {
                IsContinuousCopyMode = false;
                toggle.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                toggle.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                icon.Fill = Brushes.Gray;
                MainWindow.M.StatusBegin("Режим массива выключен", MainWindow.StatusMessageType.Info);
            };

            // 🔥 Добавляем в НЕ масштабируемый слой
            _uiLayer.Children.Add(toggle);
        }

        private void OnPartRightClick(Part part, MouseButtonEventArgs e)
        {
            e.Handled = true;

            var menu = new ContextMenu();

            // Вариант 1: Заполнение до физических границ листа
            var fillFullItem = new MenuItem { Header = "Дополнить до размера листа" };
            fillFullItem.Click += (s, args) => FillSheetWithPart(part, fillToCutLine: false);
            menu.Items.Add(fillFullItem);

            // Вариант 2: Заполнение только до текущей линии обрезки
            var fillOptimizedItem = new MenuItem { Header = "Дополнить до линии обрезки" };
            fillOptimizedItem.Click += (s, args) => FillSheetWithPart(part, fillToCutLine: true);
            menu.Items.Add(fillOptimizedItem);

            menu.IsOpen = true;
            e.Handled = true;
        }

        private void FillSheetWithPart(Part part, bool fillToCutLine)
        {
            // Определяем целевые границы для алгоритма заполнения
            double targetWidth = fillToCutLine ? _currentSheet.OptimizedWidth : _currentSheet.StockWidth;
            double targetHeight = fillToCutLine ? _currentSheet.OptimizedHeight : _currentSheet.StockHeight;

            var testSheet = new NestingSheet
            {
                Id = _currentSheet.Id,
                // 🔥 Ключевой момент: для теста временно сужаем физические границы листа до линии обрезки,
                // чтобы NestingHelper не размещал детали за её пределами.
                StockWidth = targetWidth,
                StockHeight = targetHeight,

                // Оригинальные оптимизированные размеры сохраняем для корректного отображения
                OptimizedWidth = _currentSheet.OptimizedWidth,
                OptimizedHeight = _currentSheet.OptimizedHeight,

                Parts = _currentSheet.Parts.Select(p => new PartPlacement
                {
                    Part = p.Part,
                    X = p.X,
                    Y = p.Y,
                    Rotation = p.Rotation
                }).ToList()
            };

            int placedCount = NestingHelper.TryFillSheetWithPart(testSheet, part);

            if (placedCount == 0)
            {
                string reason = fillToCutLine ? "до линии обрезки" : "до размера листа";
                MessageBox.Show($"Нет свободного места для размещения детали {reason}.", "Предпросмотр",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newPlacements = testSheet.Parts.Skip(_currentSheet.Parts.Count).ToList();
            DrawPreviewParts(newPlacements);

            string actionText = fillToCutLine ? "до линии обрезки" : "до размера листа";
            var result = MessageBox.Show(
                $"На листе будет размещено ещё {placedCount} шт. \"{part.Title}\" ({actionText}).\nПрименить изменения?",
                "Подтверждение заполнения", MessageBoxButton.YesNo, MessageBoxImage.Question);

            ClearPreview();

            if (result == MessageBoxResult.Yes)
            {
                // Применяем изменения к реальному листу
                _currentSheet = testSheet;

                // Восстанавливаем оригинальные физические границы листа в реальном объекте
                _currentSheet.StockWidth = this._currentSheet.StockWidth; // (если нужно, или берем из исходного)
                _currentSheet.StockHeight = this._currentSheet.StockHeight;

                ShowSheet(_currentSheet);
                RecalculateFromSheet(_currentSheet, part, placedCount);

                string successMsg = fillToCutLine
                    ? $"Успешно добавлено {placedCount} шт. (в пределах линии обрезки)."
                    : $"Успешно добавлено {placedCount} шт.";

                MainWindow.M.StatusBegin(successMsg, MainWindow.StatusMessageType.Success);
            }
        }

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
                                if (_part is not null)
                                {
                                    _part.Count += placed;
                                    _part.NotifyTotalChanged();
                                }

                                var metal = MainWindow.M.Metals?.FirstOrDefault(m => m.Name == item.metal);
                                if (metal != null)
                                {
                                    // Площадь считается по оптимизированным размерам (фактически занятым)
                                    double sheetArea = sheet.OptimizedWidth * sheet.OptimizedHeight;
                                    double sheetMass = sheetArea * sheet.Parts[0].Part.Destiny * metal.Density / 1_000_000;
                                    double sheetWay = sheet.Parts.Sum(p => p.Part.Way);
                                    int sheetPinholes = sheet.Parts.Sum(p =>
                                        int.TryParse(p.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var val) ? val : 0);

                                    item.way = (float)sheetWay;
                                    item.pinholes = sheetPinholes;
                                    item.mass = (float)sheetMass;
                                    item.sheetSize = $"{sheet.OptimizedWidth:0}x{sheet.OptimizedHeight:0}";
                                    item.NestingSheet = sheet;
                                }

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

        private void DrawPreviewParts(List<PartPlacement> placements)
        {
            ClearPreview();
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

        private Path? CreatePreviewPath(Part part, double x, double y, double rotation = 0)
        {
            if (part.DisplayGeometry is null) return null;

            var geometry = PartPreviewGenerator.CloneGeometry(part.DisplayGeometry);
            if (geometry == null) return null;

            var bounds = geometry.Bounds;
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new TranslateTransform(-bounds.Left, -bounds.Top));

            if (Math.Abs(rotation) > 0.1)
                transformGroup.Children.Add(new RotateTransform(-rotation, bounds.Width / 2, bounds.Height / 2));

            double adjustedX = x;
            double adjustedY = y;

            if (Math.Abs(rotation - 90) < 0.1 || Math.Abs(rotation - 270) < 0.1)
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

        private void ClearPreview()
        {
            foreach (var path in _previewPaths)
                _partsCanvas.Children.Remove(path);
            _previewPaths.Clear();
        }
        #endregion
    }
}