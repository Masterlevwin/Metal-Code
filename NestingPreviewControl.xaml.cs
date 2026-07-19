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
        private bool _isCopyOperation;

        // === Состояние выделения ===
        private readonly List<PartPlacement> _selectedPlacements = new();
        private Rectangle? _selectionRect;
        private Point _selectionStartPoint;
        private bool _isSelecting;

        // === Состояние непрерывного копирования (одиночная деталь) ===
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

        // === Визуализация зон валидности и предпросмотра ===
        private readonly List<Shape> _validationVisuals = new();
        private readonly List<Path> _previewPaths = new();

        private Path? _dragOverPreview = null!;

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

            _rootCanvas = new Canvas { Background = Brushes.White };
            _invertedLayer = new Canvas();
            _partsCanvas = new Canvas();
            _labelsCanvas = new Canvas();

            _invertedLayer.Children.Add(_partsCanvas);
            _rootCanvas.Children.Add(_invertedLayer);
            _rootCanvas.Children.Add(_labelsCanvas);
            _rootViewbox.Child = _rootCanvas;

            _uiLayer = new Canvas { IsHitTestVisible = true, ClipToBounds = false };

            var mainGrid = new Grid();
            mainGrid.Children.Add(_rootViewbox);
            mainGrid.Children.Add(_uiLayer);
            Content = mainGrid;

            _invertedLayer.AllowDrop = true;
            _invertedLayer.DragEnter += InvertedLayer_DragEnter;
            _invertedLayer.PreviewDragOver += InvertedLayer_PreviewDragOver;
            _invertedLayer.Drop += InvertedLayer_Drop;
            _invertedLayer.DragLeave += InvertedLayer_DragLeave;
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

            if (Math.Abs(_currentSheetWidth - sheet.Width) > 0.1 || Math.Abs(_currentSheetHeight - sheet.Height) > 0.1)
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
            _currentSheetWidth = sheet.StockWidth;
            _currentSheetHeight = sheet.StockHeight;

            _rootCanvas.Width = _currentSheetWidth + LabelMarginLeft;
            _rootCanvas.Height = _currentSheetHeight + LabelMarginBottom;

            _invertedLayer.Width = _currentSheetWidth;
            _invertedLayer.Height = _currentSheetHeight;
            _invertedLayer.RenderTransform = new TransformGroup
            {
                Children = new TransformCollection
                {
                    new ScaleTransform { ScaleX = 1, ScaleY = -1 },
                    new TranslateTransform { Y = _currentSheetHeight }
                }
            };

            _invertedLayer.Children.Clear();
            _invertedLayer.Children.Add(_partsCanvas);

            _invertedLayer.Children.Insert(0, new Rectangle
            {
                Width = _currentSheetWidth,
                Height = _currentSheetHeight,
                Fill = new SolidColorBrush(Color.FromArgb(30, 240, 240, 240)),
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            });

            DrawGrid(_invertedLayer, _currentSheetWidth, _currentSheetHeight);
            DrawCutLine(_invertedLayer, sheet.OptimizedWidth, sheet.OptimizedHeight, _currentSheetWidth, _currentSheetHeight);

            Canvas.SetLeft(_invertedLayer, LabelMarginLeft);
            Canvas.SetTop(_invertedLayer, 0);
            DrawLabels(_currentSheetWidth, _currentSheetHeight);
        }

        private void DrawCutLine(Canvas canvas, double optWidth, double optHeight, double fullWidth, double fullHeight)
        {
            if (Math.Abs(optWidth - fullWidth) < 1 && Math.Abs(optHeight - fullHeight) < 1) return;

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
            if (index >= 0) canvas.Children.Insert(index, cutRect);
            else canvas.Children.Add(cutRect);

            double displayWidth = Math.Ceiling(optWidth / 100) * 100;
            double displayHeight = Math.Ceiling(optHeight / 100) * 100;
            var normalizeTransform = new ScaleTransform { ScaleX = 1, ScaleY = -1 };

            if (Math.Abs(optWidth - fullWidth) > 1)
            {
                canvas.Children.Add(new TextBlock
                {
                    Text = ((int)displayWidth).ToString(),
                    FontSize = 36,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Red,
                    Background = Brushes.White,
                    IsHitTestVisible = false,
                    RenderTransform = normalizeTransform,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    Margin = new Thickness(optWidth + 10, (fullHeight / 2) - 20, 0, 0)
                });
            }
            else if (Math.Abs(optHeight - fullHeight) > 1)
            {
                canvas.Children.Add(new TextBlock
                {
                    Text = ((int)displayHeight).ToString(),
                    FontSize = 36,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Red,
                    Background = Brushes.White,
                    IsHitTestVisible = false,
                    RenderTransform = normalizeTransform,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    Margin = new Thickness((fullWidth / 2) - 20, optHeight + 10, 0, 0)
                });
            }
        }

        private void DrawGrid(Canvas canvas, double width, double height)
        {
            for (double x = GridStep; x < width; x += GridStep)
                canvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = height, Stroke = Brushes.LightGray, StrokeThickness = 0.5, StrokeDashArray = new DoubleCollection { 5, 5 } });

            canvas.Children.Add(new Line { X1 = width, Y1 = 0, X2 = width, Y2 = height, Stroke = Brushes.Gray, StrokeThickness = 1 });

            for (double y = GridStep; y < height; y += GridStep)
                canvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, Stroke = Brushes.LightGray, StrokeThickness = 0.5, StrokeDashArray = new DoubleCollection { 5, 5 } });

            canvas.Children.Add(new Line { X1 = 0, Y1 = height, X2 = width, Y2 = height, Stroke = Brushes.Gray, StrokeThickness = 1 });
        }

        private void DrawLabels(double width, double height)
        {
            _labelsCanvas.Children.Clear();
            for (double x = GridStep; x < width; x += GridStep) AddLabelX(x, height);
            if (width > 0) AddLabelX(width, height);
            for (double y = GridStep; y < height; y += GridStep) AddLabelY(y, height);
            if (height > 0) AddLabelY(height, height);

            _labelsCanvas.Children.Add(new TextBlock
            {
                Text = "0",
                FontSize = 40,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Red,
                IsHitTestVisible = false,
                Margin = new Thickness(-LabelMarginLeft * 0.8, height + 10, 0, 0)
            });
        }

        private void AddLabelX(double x, double height) => _labelsCanvas.Children.Add(new TextBlock { Text = ((int)x).ToString(), FontSize = 40, Foreground = Brushes.Gray, IsHitTestVisible = false, Margin = new Thickness(LabelMarginLeft + x - 20, height + 10, 0, 0) });
        private void AddLabelY(double y, double height) => _labelsCanvas.Children.Add(new TextBlock { Text = ((int)y).ToString(), FontSize = 40, Foreground = Brushes.Gray, IsHitTestVisible = false, Margin = new Thickness(0, height - y - 10, 0, 0) });

        private void AddPartToCanvas(PartPlacement placement)
        {
            var part = placement.Part;
            if (part.DisplayGeometry is null) return;

            var geometry = PartPreviewGenerator.CloneGeometry(part.DisplayGeometry);
            if (geometry == null) return;

            var path = new Path
            {
                Data = geometry,
                Fill = part.PartType == PartType.Round ? new SolidColorBrush(Color.FromArgb(150, 255, 150, 150)) : new SolidColorBrush(Color.FromArgb(150, 150, 200, 255)),
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

        public void AddContinuousCopyToggle()
        {
            var toggle = new ToggleButton
            {
                Width = 30,
                Height = 30,
                ToolTip = "Режим непрерывного копирования (массив)",
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, Direction = 315, ShadowDepth = 2, Opacity = 0.25, BlurRadius = 4 }
            };

            var icon = new Path { Data = Geometry.Parse("M3,11H11V3H3V11M3,21H11V13H3V21M13,3V11H21V3H13M13,21H21V13H13V21Z"), Fill = Brushes.Gray, Width = 16, Height = 16, Stretch = Stretch.Uniform };
            toggle.Content = icon;

            toggle.MouseEnter += (s, e) => { if (!toggle.IsChecked.GetValueOrDefault()) { toggle.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235)); toggle.BorderBrush = new SolidColorBrush(Color.FromRgb(74, 144, 226)); } };
            toggle.MouseLeave += (s, e) => { if (!toggle.IsChecked.GetValueOrDefault()) { toggle.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)); toggle.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)); } };
            toggle.Checked += (s, e) => { IsContinuousCopyMode = true; toggle.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)); toggle.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 142, 60)); icon.Fill = Brushes.White; toggle.ToolTip = "Режим массива включен"; };
            toggle.Unchecked += (s, e) => { IsContinuousCopyMode = false; toggle.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)); toggle.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)); icon.Fill = Brushes.Gray; toggle.ToolTip = "Режим массива выключен"; };

            _uiLayer.Children.Add(toggle);
            _rootViewbox.SizeChanged += (s, e) => { if (_rootViewbox.ActualWidth > 40) { Canvas.SetLeft(toggle, _rootViewbox.ActualWidth - 40); Canvas.SetTop(toggle, 10); } };
            Loaded += (s, e) => Dispatcher.InvokeAsync(() => { if (_rootViewbox.ActualWidth > 40) { Canvas.SetLeft(toggle, _rootViewbox.ActualWidth - 40); Canvas.SetTop(toggle, 10); } }, System.Windows.Threading.DispatcherPriority.Render);
        }
        #endregion

        //-------------Интерактивность раскладки-------------//
        #region
        /// <summary>
        /// Указывает системе, что мы готовы принять перетаскиваемый объект, если это деталь.
        /// </summary>
        private void InvertedLayer_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(Part)))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = DragDropEffects.Copy;
            e.Handled = true;

            var part = (Part)e.Data.GetData(typeof(Part));
            if (part == null) return;

            if (part.DisplayGeometry == null)
                PartPreviewGenerator.EnsureDisplayGeometry(part);

            if (_dragOverPreview == null)
            {
                _dragOverPreview = new Path
                {
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 255, 165, 0)),
                    IsHitTestVisible = false,
                    Opacity = 0.8
                };
                _partsCanvas.Children.Add(_dragOverPreview);
            }

            Point dropPoint = e.GetPosition(_invertedLayer);

            int bestRotation = 0;
            var (w, h) = NestingHelper.GetPartDimensions(part, 0);
            var testPlacement = new PartPlacement { Part = part, X = dropPoint.X - (w / 2), Y = dropPoint.Y - (h / 2), Rotation = 0 };

            bool isValid = NestingHelper.IsValidPlacement(_currentSheet, testPlacement, testPlacement.X, testPlacement.Y, 0);

            if (!isValid)
            {
                var (w90, h90) = NestingHelper.GetPartDimensions(part, 90);
                testPlacement = new PartPlacement { Part = part, X = dropPoint.X - (w90 / 2), Y = dropPoint.Y - (h90 / 2), Rotation = 90 };

                if (NestingHelper.IsValidPlacement(_currentSheet, testPlacement, testPlacement.X, testPlacement.Y, 90))
                {
                    bestRotation = 90;
                    w = w90;
                    h = h90;
                    isValid = true;
                }
            }

            _dragOverPreview.Data = part.DisplayGeometry?.Clone();
            _dragOverPreview.Stroke = isValid ? Brushes.LimeGreen : Brushes.Red;

            var previewPlacement = new PartPlacement
            {
                Part = part,
                X = dropPoint.X - (w / 2),
                Y = dropPoint.Y - (h / 2),
                Rotation = bestRotation
            };

            ApplyPlacementToPath(_dragOverPreview, previewPlacement);
            _dragOverPreview.Visibility = Visibility.Visible;
        }

        private void InvertedLayer_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(Part)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        /// <summary>
        /// Обрабатывает фактический сброс детали на лист.
        /// </summary>
        private void InvertedLayer_Drop(object sender, DragEventArgs e)
        {
            if (_dragOverPreview != null)
            {
                _partsCanvas.Children.Remove(_dragOverPreview);
                _dragOverPreview = null;
            }

            if (e.Data.GetDataPresent(typeof(Part)) && _currentSheet != null)
            {
                var part = (Part)e.Data.GetData(typeof(Part));
                if (part == null) return;

                Point dropPoint = e.GetPosition(_invertedLayer);

                if (part.DisplayGeometry == null)
                    PartPreviewGenerator.EnsureDisplayGeometry(part);

                int[] rotationsToTry = { 0, 90 };
                PartPlacement? successfulPlacement = null;
                int usedRotation = 0;

                foreach (var rotation in rotationsToTry)
                {
                    var (w, h) = NestingHelper.GetPartDimensions(part, rotation);

                    // 🔥 ЗАМЕНА: Используем _currentSheet.Spacing
                    double placementX = Math.Max(_currentSheet.Spacing, dropPoint.X - (w / 2));
                    double placementY = Math.Max(_currentSheet.Spacing, dropPoint.Y - (h / 2));

                    var testPlacement = new PartPlacement { Part = part, X = placementX, Y = placementY, Rotation = rotation };

                    if (NestingHelper.IsValidPlacement(_currentSheet, testPlacement, placementX, placementY, rotation))
                    {
                        successfulPlacement = testPlacement;
                        usedRotation = rotation;
                        break;
                    }
                }

                if (successfulPlacement != null)
                {
                    _currentSheet.Parts.Add(successfulPlacement);
                    part.Count++;
                    part.NotifyTotalChanged();

                    NestingHelper.OptimizeSheetSize(_currentSheet);
                    RebuildLayout(_currentSheet);
                    ShowSheet(_currentSheet);
                    RecalculateSheetMetrics();

                    string rotationMsg = usedRotation == 90 ? " (автоматически повернута на 90°)" : "";
                    MainWindow.M.StatusBegin($"Деталь '{part.Title}' добавлена на лист{rotationMsg}", MainWindow.StatusMessageType.Success);
                }
                else
                {
                    MainWindow.M.StatusBegin("Недостаточно места или коллизия с другой деталью в этой точке", MainWindow.StatusMessageType.Warning);
                }
            }
            e.Handled = true;
        }

        private void InvertedLayer_DragLeave(object sender, DragEventArgs e)
        {
            if (_dragOverPreview != null)
            {
                _partsCanvas.Children.Remove(_dragOverPreview);
                _dragOverPreview = null;
            }
        }

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

            double adjustedX = placement.X, adjustedY = placement.Y;
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

            if (IsContinuousCopyMode)
            {
                e.Handled = true;
                ClearSelection();
                _selectedPlacements.Add(placement);
                _isContinuousCopying = true;
                _lockedAxis = CopyAxis.None;
                _baseCopyPart = placement;
                _baseCopyX = placement.X;
                _baseCopyY = placement.Y;
                _mouseDownPos = e.GetPosition(_invertedLayer);
                _pendingContinuousCopies.Clear();
                _pendingContinuousPaths.Clear();
                _invertedLayer.CaptureMouse();
                return;
            }

            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            List<PartPlacement> toDrag = _selectedPlacements.Contains(placement) ? _selectedPlacements.ToList() : new List<PartPlacement> { placement };

            if (!_selectedPlacements.Contains(placement) && !isShiftPressed) ClearSelection();

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

        private List<PartPlacement> CopyPlacements(List<PartPlacement> source)
        {
            var copies = new List<PartPlacement>();
            foreach (var src in source)
            {
                var copy = new PartPlacement { Part = src.Part, X = src.X, Y = src.Y, Rotation = src.Rotation };
                copies.Add(copy);
                _currentSheet.Parts.Add(copy);
            }

            foreach (var group in copies.GroupBy(c => c.Part))
            {
                group.Key.Count += group.Count();
                group.Key.NotifyTotalChanged();
            }

            _partsCanvas.Children.Clear();
            foreach (var placement in _currentSheet.Parts)
            {
                if (placement.Part.DisplayGeometry == null) PartPreviewGenerator.EnsureDisplayGeometry(placement.Part);
                AddPartToCanvas(placement);
            }
            return copies;
        }

        private void StartDrag(List<PartPlacement> placements, bool isCopying = false)
        {
            _isCopyOperation = isCopying;
            _activePlacements.Clear(); _activePaths.Clear(); _originalPositions.Clear(); _originalVisuals.Clear();

            foreach (var plc in placements)
            {
                _activePlacements.Add(plc);
                _originalPositions[plc] = (plc.X, plc.Y);
                var path = _partsCanvas.Children.OfType<Path>().FirstOrDefault(p => p.Tag == plc);
                if (path != null)
                {
                    _activePaths[plc] = path;
                    _originalVisuals[path] = (path.Stroke, path.Fill);
                    _partsCanvas.Children.Remove(path);
                    _partsCanvas.Children.Add(path);
                    path.Stroke = Brushes.Orange;
                    path.StrokeThickness = 3;
                }
            }

            _dragStartPoint = Mouse.GetPosition(_invertedLayer);
            _isDragging = true;
            _invertedLayer.Focusable = true; _invertedLayer.Focus(); _invertedLayer.CaptureMouse();

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
            _selectionRect = new Rectangle { Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)), StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 6, 3 }, Fill = new SolidColorBrush(Color.FromArgb(70, 0, 120, 215)), IsHitTestVisible = false };
            Canvas.SetLeft(_selectionRect, _selectionStartPoint.X);
            Canvas.SetTop(_selectionRect, _selectionStartPoint.Y);
            _invertedLayer.Children.Add(_selectionRect);
            _invertedLayer.CaptureMouse();
            e.Handled = true;
        }

        private void InvertedLayer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point currentPos = e.GetPosition(_invertedLayer);

            if (_isContinuousCopying && _baseCopyPart != null)
            {
                double _deltaX = currentPos.X - _mouseDownPos.X;
                double _deltaY = currentPos.Y - _mouseDownPos.Y;

                if (_lockedAxis == CopyAxis.None && (Math.Abs(_deltaX) > 10 || Math.Abs(_deltaY) > 10))
                    _lockedAxis = Math.Abs(_deltaX) > Math.Abs(_deltaY) ? CopyAxis.X : CopyAxis.Y;

                if (_lockedAxis != CopyAxis.None)
                {
                    var (w, h) = NestingHelper.GetPartDimensions(_baseCopyPart.Part, _baseCopyPart.Rotation);

                    // 🔥 ЗАМЕНА: Используем _currentSheet.Spacing
                    double step = (_lockedAxis == CopyAxis.X ? w : h) + _currentSheet.Spacing;
                    double delta = _lockedAxis == CopyAxis.X ? _deltaX : _deltaY;
                    int direction = Math.Sign(delta);
                    int steps = (int)(Math.Abs(delta) / step);

                    ClearContinuousPreview();

                    for (int i = 1; i <= steps; i++)
                    {
                        double candidateX = _baseCopyX + (_lockedAxis == CopyAxis.X ? (i * step * direction) : 0);
                        double candidateY = _baseCopyY + (_lockedAxis == CopyAxis.Y ? (i * step * direction) : 0);

                        var tempPlc = new PartPlacement { Part = _baseCopyPart.Part, X = candidateX, Y = candidateY, Rotation = _baseCopyPart.Rotation };

                        if (NestingHelper.IsValidPlacement(_currentSheet, tempPlc, tempPlc.X, tempPlc.Y, tempPlc.Rotation))
                        {
                            _pendingContinuousCopies.Add(tempPlc);
                            var path = CreatePreviewPath(_baseCopyPart.Part, candidateX, candidateY, _baseCopyPart.Rotation);
                            if (path != null) { _partsCanvas.Children.Add(path); _pendingContinuousPaths.Add(path); }
                        }
                        else { break; }
                    }
                }
            }

            if (_isSelecting && _selectionRect != null)
            {
                double x = Math.Min(_selectionStartPoint.X, currentPos.X);
                double y = Math.Min(_selectionStartPoint.Y, currentPos.Y);
                _selectionRect.Width = Math.Abs(currentPos.X - _selectionStartPoint.X);
                _selectionRect.Height = Math.Abs(currentPos.Y - _selectionStartPoint.Y);
                Canvas.SetLeft(_selectionRect, x);
                Canvas.SetTop(_selectionRect, y);
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
                plc.X = newX; plc.Y = newY;
                path.Stroke = isValid ? Brushes.LimeGreen : Brushes.Red;
                ApplyPlacementToPath(path, new PartPlacement { Part = plc.Part, X = newX, Y = newY, Rotation = plc.Rotation });
            }

            if (_activePlacements.Count == 1)
            {
                var only = _activePlacements[0];
                DrawValidationZones(only.X, only.Y, only.Rotation, isValid);
            }
        }

        private void InvertedLayer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
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
                _isContinuousCopying = false; _lockedAxis = CopyAxis.None; _baseCopyPart = null;

                if (addedCount > 0)
                {
                    NestingHelper.OptimizeSheetSize(_currentSheet);
                    RebuildLayout(_currentSheet);
                    ShowSheet(_currentSheet);
                    RecalculateSheetMetrics();
                    MainWindow.M.StatusBegin($"Добавлено копий: {addedCount}", MainWindow.StatusMessageType.Success);
                }
                else
                {
                    MainWindow.M.StatusBegin("Копирование отменено: нет свободного места", MainWindow.StatusMessageType.Warning);
                }
                return;
            }

            Point endPos = e.GetPosition(_invertedLayer);
            if (_isSelecting) { FinishSelection(endPos); return; }
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

            if (IsGroupPlacementValid(finalPositions, _activePlacements))
            {
                foreach (var plc in _activePlacements) { plc.X = finalPositions[plc].X; plc.Y = finalPositions[plc].Y; }
                CommitChanges($"Перемещено деталей: {_activePlacements.Count}");
            }
            else if (wasSingle)
            {
                var only = _activePlacements[0];
                var (targetX, targetY) = finalPositions[only];
                var result = FindNearestValidPosition(only, targetX, targetY, searchRadius: 150);

                if (result.Found)
                {
                    only.X = result.X; only.Y = result.Y;
                    if (_activePaths.TryGetValue(only, out var path))
                    {
                        path.Stroke = Brushes.Yellow;
                        System.Threading.Tasks.Task.Delay(200).ContinueWith(_ => Application.Current.Dispatcher.Invoke(() => { if (_activePaths.TryGetValue(only, out var p)) RestoreVisual(p); }));
                    }
                    CommitChanges($"Деталь автоматически размещена в позиции ({result.X:0}, {result.Y:0})");
                }
                else { RollbackDrag(); MainWindow.M.StatusBegin("Не удалось найти подходящее место для детали", MainWindow.StatusMessageType.Warning); }
            }
            else { RollbackDrag(); MainWindow.M.StatusBegin("Не удалось переместить группу — нет свободного места", MainWindow.StatusMessageType.Warning); }

            FinishDrag();
        }

        private void ClearContinuousPreview()
        {
            foreach (var path in _pendingContinuousPaths) if (_partsCanvas.Children.Contains(path)) _partsCanvas.Children.Remove(path);
            _pendingContinuousPaths.Clear(); _pendingContinuousCopies.Clear();
        }

        private void FinishSelection(Point endPoint)
        {
            _isSelecting = false; _invertedLayer.ReleaseMouseCapture();
            if (_selectionRect != null) { _invertedLayer.Children.Remove(_selectionRect); _selectionRect = null; }

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
                if (selectionBounds.IntersectsWith(partBounds) || selectionBounds.Contains(partBounds)) _selectedPlacements.Add(placement);
            }
            UpdateSelectionVisuals();
            if (_selectedPlacements.Count > 0) MainWindow.M.StatusBegin($"Выделено деталей: {_selectedPlacements.Count}", MainWindow.StatusMessageType.Info);
        }

        private void CommitChanges(string statusMessage)
        {
            NestingHelper.OptimizeSheetSize(_currentSheet);
            RebuildLayout(_currentSheet);
            ShowSheet(_currentSheet);
            RecalculateSheetMetrics();
            MainWindow.M.StatusBegin(statusMessage, MainWindow.StatusMessageType.Success);
        }

        private void RollbackDrag()
        {
            bool wasCopying = _isCopyOperation;
            foreach (var plc in _activePlacements)
            {
                if (wasCopying) { _currentSheet.Parts.Remove(plc); plc.Part.Count--; plc.Part.NotifyTotalChanged(); }
                else if (_originalPositions.TryGetValue(plc, out var orig)) { plc.X = orig.X; plc.Y = orig.Y; }
            }
            RebuildLayout(_currentSheet); ShowSheet(_currentSheet);
            RecalculateSheetMetrics();
        }

        private void FinishDrag()
        {
            _isDragging = false; _invertedLayer.ReleaseMouseCapture();
            foreach (var path in _activePaths.Values) RestoreVisual(path);
            _activePlacements.Clear(); _activePaths.Clear(); _originalPositions.Clear(); _originalVisuals.Clear();
            ClearValidationVisuals();
        }

        private void RestoreVisual(Path path)
        {
            if (_originalVisuals.TryGetValue(path, out var vis)) { path.Stroke = vis.Stroke; path.Fill = vis.Fill; path.StrokeThickness = 1.5; }
            else
            {
                var placement = path.Tag as PartPlacement;
                path.Stroke = placement?.Part.PartType == PartType.Round ? Brushes.DarkRed : Brushes.DarkBlue;
                path.StrokeThickness = 1.5;
            }
        }

        private void NestingPreviewControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _selectedPlacements.Any()) { ClearSelection(); MainWindow.M.StatusBegin("Выделение снято", MainWindow.StatusMessageType.Info); e.Handled = true; return; }
            if (e.Key == Key.Delete) { DeleteSelectedParts(); e.Handled = true; return; }
            if (e.Key != Key.Home && e.Key != Key.End) return;

            var targets = (_isDragging && _activePlacements.Any()) ? _activePlacements.ToList() : _selectedPlacements.ToList();
            if (targets.Any()) { RotateTargets(targets, e.Key == Key.Home ? 90 : -90, skipValidation: _isDragging); e.Handled = true; }
        }

        private void DeleteSelectedParts()
        {
            var candidates = _selectedPlacements.Any() ? _selectedPlacements.ToList() : _activePlacements.ToList();
            if (!candidates.Any()) { MainWindow.M.StatusBegin("Нет деталей для удаления", MainWindow.StatusMessageType.Warning); return; }

            var toDelete = candidates.Where(c => _currentSheet.Parts.Contains(c)).ToList();
            if (!toDelete.Any()) { MainWindow.M.StatusBegin("Выделенные детали отсутствуют на листе", MainWindow.StatusMessageType.Warning); return; }

            foreach (var placement in toDelete) { _currentSheet.Parts.Remove(placement); placement.Part.Count--; placement.Part.NotifyTotalChanged(); }
            MainWindow.M.StatusBegin($"Удалено деталей: {toDelete.Count}", MainWindow.StatusMessageType.Success);

            ClearSelection(); _activePlacements.Clear();
            NestingHelper.OptimizeSheetSize(_currentSheet); RebuildLayout(_currentSheet); ShowSheet(_currentSheet);
            RecalculateSheetMetrics();
        }

        private void RotateTargets(List<PartPlacement> targets, double rotationDelta, bool skipValidation = false)
        {
            double rotationRad = rotationDelta * Math.PI / 180.0;
            double groupCenterX = 0, groupCenterY = 0;
            foreach (var plc in targets) { var (w, h) = NestingHelper.GetPartDimensions(plc.Part, plc.Rotation); groupCenterX += plc.X + w / 2; groupCenterY += plc.Y + h / 2; }
            groupCenterX /= targets.Count; groupCenterY /= targets.Count;

            var newStates = new Dictionary<PartPlacement, (double X, double Y, double Rotation)>();
            foreach (var plc in targets)
            {
                if (plc.Part.PartType == PartType.Round) continue;
                var (curW, curH) = NestingHelper.GetPartDimensions(plc.Part, plc.Rotation);
                double dx = (plc.X + curW / 2) - groupCenterX;
                double dy = (plc.Y + curH / 2) - groupCenterY;
                double rotCenterX = groupCenterX + dx * Math.Cos(rotationRad) - dy * Math.Sin(rotationRad);
                double rotCenterY = groupCenterY + dx * Math.Sin(rotationRad) + dy * Math.Cos(rotationRad);
                double targetRot = (plc.Rotation + rotationDelta + 360) % 360;
                var (rotW, rotH) = NestingHelper.GetPartDimensions(plc.Part, targetRot);
                newStates[plc] = (Math.Round(rotCenterX - rotW / 2), Math.Round(rotCenterY - rotH / 2), targetRot);
            }

            if (!newStates.Any()) return;

            if (!skipValidation)
            {
                string failReason = ""; PartPlacement? collisionPartner = null;

                // 🔥 ЗАМЕНА: Локальная переменная для удобства чтения
                double sp = _currentSheet.Spacing;

                foreach (var state in newStates)
                {
                    var plcToCheck = state.Key; var (chkX, chkY, chkRot) = state.Value;
                    var (chkW, chkH) = NestingHelper.GetPartDimensions(plcToCheck.Part, chkRot);

                    if (chkX < sp || chkY < sp || chkX + chkW > _currentSheet.Width - sp || chkY + chkH > _currentSheet.Height - sp)
                    { failReason = "Деталь выходит за границы листа после поворота"; break; }

                    foreach (var existing in _currentSheet.Parts)
                    {
                        if (targets.Contains(existing)) continue;
                        var (exW, exH) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);
                        if (chkX + chkW + sp <= existing.X || existing.X + exW + sp <= chkX || chkY + chkH + sp <= existing.Y || existing.Y + exH + sp <= chkY) continue;
                        failReason = $"Коллизия с '{existing.Part.Title}'"; collisionPartner = existing; break;
                    }
                    if (!string.IsNullOrEmpty(failReason)) break;
                }

                if (!string.IsNullOrEmpty(failReason))
                {
                    System.Media.SystemSounds.Beep.Play();
                    MainWindow.M.StatusBegin($"Не удалось повернуть: {failReason}", MainWindow.StatusMessageType.Warning);
                    if (collisionPartner != null)
                    {
                        var obstaclePath = _partsCanvas.Children.OfType<Path>().FirstOrDefault(p => p.Tag == collisionPartner);
                        if (obstaclePath != null)
                        {
                            var originalStroke = obstaclePath.Stroke; var originalThickness = obstaclePath.StrokeThickness;
                            obstaclePath.Stroke = Brushes.Red; obstaclePath.StrokeThickness = 4;
                            System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ => Application.Current.Dispatcher.Invoke(() => { obstaclePath.Stroke = originalStroke; obstaclePath.StrokeThickness = originalThickness; }));
                        }
                    }
                    return;
                }
            }

            foreach (var state in newStates)
            {
                var plc = state.Key; var (appX, appY, appRot) = state.Value;
                plc.X = appX; plc.Y = appY; plc.Rotation = appRot;
                var path = _partsCanvas.Children.OfType<Path>().FirstOrDefault(p => p.Tag == plc);
                if (path != null) { ApplyPlacementToPath(path, plc); var (finW, finH) = NestingHelper.GetPartDimensions(plc.Part, appRot); path.ToolTip = $"{plc.Part.Title}\n{finW:0}×{finH:0}мм (↻)"; }
                if (_isDragging && _originalPositions.ContainsKey(plc)) _originalPositions[plc] = (appX, appY);
            }

            UpdateSelectionVisuals();
            if (_isDragging) { _dragStartPoint = Mouse.GetPosition(_invertedLayer); ClearValidationVisuals(); NestingHelper.OptimizeSheetSize(_currentSheet); }
            else { CommitChanges($"Повернуто деталей: {newStates.Count} на {rotationDelta}°"); }
        }

        private void UpdateSelectionVisuals()
        {
            foreach (var child in _partsCanvas.Children.OfType<Path>())
            {
                if (child.Tag is PartPlacement && _originalVisuals.TryGetValue(child, out var vis)) { child.Stroke = vis.Stroke; child.Fill = vis.Fill; child.StrokeThickness = 1.5; }
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

        private void ClearSelection() { _selectedPlacements.Clear(); UpdateSelectionVisuals(); }

        private bool IsGroupPlacementValid(Dictionary<PartPlacement, (double X, double Y)> newPositions, List<PartPlacement> ignorePlacements)
        {
            // 🔥 ЗАМЕНА: Локальная переменная для удобства чтения
            double sp = _currentSheet.Spacing;

            foreach (var kvp in newPositions)
            {
                var plc = kvp.Key; var (newX, newY) = kvp.Value;
                var (w, h) = NestingHelper.GetPartDimensions(plc.Part, plc.Rotation);
                if (newX < sp || newY < sp || newX + w > _currentSheet.Width - sp || newY + h > _currentSheet.Height - sp) return false;

                foreach (var existing in _currentSheet.Parts)
                {
                    if (ignorePlacements.Contains(existing)) continue;
                    var (exW, exH) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);
                    if (newX + w + sp <= existing.X || existing.X + exW + sp <= newX || newY + h + sp <= existing.Y || existing.Y + exH + sp <= newY) continue;
                    return false;
                }
            }
            return true;
        }

        private void DrawValidationZones(double movingX, double movingY, double movingRotation, bool isValid)
        {
            ClearValidationVisuals(); _partsCanvas.UpdateLayout();

            // 🔥 ЗАМЕНА: Локальная переменная для удобства чтения
            double sp = _currentSheet.Spacing;

            foreach (var placement in _currentSheet.Parts)
            {
                if (_activePlacements.Contains(placement)) continue;
                var (w, h) = NestingHelper.GetPartDimensions(placement.Part, placement.Rotation);
                var zoneRect = new Rectangle { Width = w + sp * 2, Height = h + sp * 2, Fill = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0)), Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 }, IsHitTestVisible = false };
                Canvas.SetLeft(zoneRect, placement.X - sp); Canvas.SetTop(zoneRect, placement.Y - sp);
                _partsCanvas.Children.Add(zoneRect); _validationVisuals.Add(zoneRect);
            }

            var boundaryRect = new Rectangle { Width = _currentSheet.Width - sp * 2, Height = _currentSheet.Height - sp * 2, Fill = Brushes.Transparent, Stroke = new SolidColorBrush(Color.FromArgb(100, 0, 100, 255)), StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 5, 5 }, IsHitTestVisible = false };
            Canvas.SetLeft(boundaryRect, sp); Canvas.SetTop(boundaryRect, sp);
            _partsCanvas.Children.Add(boundaryRect); _validationVisuals.Add(boundaryRect);

            foreach (var plc in _activePlacements)
            {
                var (mw, mh) = NestingHelper.GetPartDimensions(plc.Part, plc.Rotation);
                var footprintRect = new Rectangle { Width = mw, Height = mh, Fill = isValid ? new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)) : new SolidColorBrush(Color.FromArgb(60, 255, 0, 0)), Stroke = isValid ? Brushes.LimeGreen : Brushes.Red, StrokeThickness = 2, IsHitTestVisible = false };
                Canvas.SetLeft(footprintRect, plc.X); Canvas.SetTop(footprintRect, plc.Y);
                _partsCanvas.Children.Add(footprintRect); _validationVisuals.Add(footprintRect);
            }
        }

        private void ClearValidationVisuals()
        {
            var visualsToRemove = _validationVisuals.ToList();
            foreach (var visual in visualsToRemove) if (_partsCanvas.Children.Contains(visual)) _partsCanvas.Children.Remove(visual);
            _validationVisuals.Clear();
        }

        private (bool Found, double X, double Y) FindNearestValidPosition(PartPlacement placement, double currentX, double currentY, double searchRadius = 100)
        {
            var candidates = new List<(double X, double Y, double Distance)>();
            var (w, h) = NestingHelper.GetPartDimensions(placement.Part, placement.Rotation);

            // 🔥 ЗАМЕНА: Локальная переменная для удобства чтения
            double sp = _currentSheet.Spacing;

            foreach (var existing in _currentSheet.Parts)
            {
                if (ReferenceEquals(existing, placement)) continue;
                var (ew, eh) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);
                var positions = new[] {
                    (X: existing.X + ew + sp, Y: existing.Y),
                    (X: existing.X - w - sp, Y: existing.Y),
                    (X: existing.X, Y: existing.Y + eh + sp),
                    (X: existing.X, Y: existing.Y - h - sp),
                    (X: existing.X + ew + sp, Y: existing.Y + eh + sp),
                    (X: existing.X - w - sp, Y: existing.Y + eh + sp),
                    (X: existing.X + ew + sp, Y: existing.Y - h - sp),
                    (X: existing.X - w - sp, Y: existing.Y - h - sp)
                };

                foreach (var pos in positions)
                {
                    double distance = Math.Sqrt(Math.Pow(pos.X - currentX, 2) + Math.Pow(pos.Y - currentY, 2));
                    if (distance > searchRadius) continue;
                    if (NestingHelper.IsValidPlacement(_currentSheet, placement, Math.Round(pos.X), Math.Round(pos.Y), placement.Rotation)) candidates.Add((Math.Round(pos.X), Math.Round(pos.Y), distance));
                }
            }

            if (!candidates.Any())
            {
                int step = 10, radiusSteps = (int)(searchRadius / step);
                for (int dx = -radiusSteps; dx <= radiusSteps; dx++)
                    for (int dy = -radiusSteps; dy <= radiusSteps; dy++)
                        if (NestingHelper.IsValidPlacement(_currentSheet, placement, Math.Round(currentX + dx * step), Math.Round(currentY + dy * step), placement.Rotation))
                            candidates.Add((Math.Round(currentX + dx * step), Math.Round(currentY + dy * step), Math.Sqrt(Math.Pow(dx * step, 2) + Math.Pow(dy * step, 2))));
            }

            return candidates.Any() ? (true, candidates.OrderBy(c => c.Distance).First().X, candidates.OrderBy(c => c.Distance).First().Y) : (false, 0, 0);
        }

        /// <summary>
        /// УНИФИЦИРОВАННЫЙ МЕТОД: Обновляет метрики текущего листа и синхронизирует их с CutControl.
        /// Опционально увеличивает счетчик конкретной детали (используется при заполнении листа).
        /// </summary>
        private void RecalculateSheetMetrics(Part? partToUpdate = null, int addedCount = 0)
        {
            if (_currentSheet == null) return;

            // 1. Если это операция заполнения, обновляем счетчик конкретной детали в глобальном списке
            if (partToUpdate != null && addedCount > 0)
            {
                foreach (var detail in MainWindow.M.DetailControls)
                {
                    foreach (var typeDetail in detail.TypeDetailControls)
                    {
                        foreach (var work in typeDetail.WorkControls)
                        {
                            if (work.workType is CutControl cut && cut.PartDetails != null)
                            {
                                var globalPart = cut.PartDetails.FirstOrDefault(p => p.Title == partToUpdate.Title);
                                if (globalPart != null) { globalPart.Count += addedCount; globalPart.NotifyTotalChanged(); goto Metrics; }
                            }
                        }
                    }
                }
            }

        Metrics:
            // 2. Считаем метрики текущего листа
            double sheetWay = _currentSheet.Parts.Sum(p => p.Part.Way);
            int sheetPinholes = _currentSheet.Parts.Sum(p => int.TryParse(p.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var val) ? val : 0);
            double sheetArea = _currentSheet.OptimizedWidth * _currentSheet.OptimizedHeight;

            // 3. Синхронизируем с CutControl
            foreach (var detail in MainWindow.M.DetailControls)
            {
                foreach (var typeDetail in detail.TypeDetailControls)
                {
                    foreach (var work in typeDetail.WorkControls)
                    {
                        if (work.workType is CutControl cut && cut.Items != null && cut.PartDetails != null)
                        {
                            var item = cut.Items.FirstOrDefault(i => i.NestingSheet?.Id == _currentSheet.Id);
                            if (item == null) continue;

                            var metal = MainWindow.M.Metals?.FirstOrDefault(m => m.Name == item.metal);
                            double density = metal?.Density ?? 0;
                            double destiny = double.TryParse(item.destiny, out var d) ? d : 0;

                            item.way = (float)sheetWay;
                            item.pinholes = sheetPinholes;
                            item.mass = (float)(sheetArea * destiny * density / 1_000_000);
                            item.sheetSize = $"{_currentSheet.OptimizedWidth:0}x{_currentSheet.OptimizedHeight:0}";
                            item.NestingSheet = _currentSheet;

                            cut.WayTotal = cut.PartDetails.Sum(p => p.Way * p.Count);
                            cut.MassTotal = cut.PartDetails.Sum(p => p.Mass * p.Count);
                            cut.SumProperties(cut.Items);
                            cut.work.type.CreateSort();
                            cut.work.type.MassCalculate();
                            return; // Лист найден и обновлен, выходим
                        }
                    }
                }
            }
        }
        #endregion

        //-------------Заполнение листа----------//
        #region
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
            double targetWidth = fillToCutLine ? _currentSheet.OptimizedWidth : _currentSheet.StockWidth;
            double targetHeight = fillToCutLine ? _currentSheet.OptimizedHeight : _currentSheet.StockHeight;

            var testSheet = new NestingSheet
            {
                Id = _currentSheet.Id,
                StockWidth = targetWidth,
                StockHeight = targetHeight,
                OptimizedWidth = _currentSheet.OptimizedWidth,
                OptimizedHeight = _currentSheet.OptimizedHeight,
                Spacing = _currentSheet.Spacing, // 🔥 ВАЖНО: Сохраняем отступ при клонировании листа
                Parts = _currentSheet.Parts.Select(p => new PartPlacement { Part = p.Part, X = p.X, Y = p.Y, Rotation = p.Rotation }).ToList()
            };

            int placedCount = NestingHelper.TryFillSheetWithPart(testSheet, part);
            if (placedCount == 0)
            {
                MessageBox.Show($"Нет свободного места для размещения детали {(fillToCutLine ? "до линии обрезки" : "до размера листа")}.", "Предпросмотр", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DrawPreviewParts(testSheet.Parts.Skip(_currentSheet.Parts.Count).ToList());
            var result = MessageBox.Show($"На листе будет размещено ещё {placedCount} шт. \"{part.Title}\" ({(fillToCutLine ? "до линии обрезки" : "до размера листа")}).\nПрименить изменения?", "Подтверждение заполнения", MessageBoxButton.YesNo, MessageBoxImage.Question);
            ClearPreview();

            if (result == MessageBoxResult.Yes)
            {
                double originalStockWidth = _currentSheet.StockWidth;
                double originalStockHeight = _currentSheet.StockHeight;
                double originalSpacing = _currentSheet.Spacing; // 🔥 Сохраняем отступ

                _currentSheet = testSheet;
                _currentSheet.StockWidth = originalStockWidth;
                _currentSheet.StockHeight = originalStockHeight;
                _currentSheet.Spacing = originalSpacing; // 🔥 Восстанавливаем отступ

                ShowSheet(_currentSheet);

                RecalculateSheetMetrics(part, placedCount);

                MainWindow.M.StatusBegin($"Успешно добавлено {placedCount} шт. {(fillToCutLine ? "(в пределах линии обрезки)" : "")}", MainWindow.StatusMessageType.Success);
            }
        }

        private void DrawPreviewParts(List<PartPlacement> placements)
        {
            ClearPreview();
            foreach (var placement in placements)
            {
                var path = CreatePreviewPath(placement.Part, placement.X, placement.Y, placement.Rotation);
                if (path != null) { _partsCanvas.Children.Add(path); _previewPaths.Add(path); }
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
            if (Math.Abs(rotation) > 0.1) transformGroup.Children.Add(new RotateTransform(-rotation, bounds.Width / 2, bounds.Height / 2));

            double adjustedX = x, adjustedY = y;
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

        private void ClearPreview() { foreach (var path in _previewPaths) _partsCanvas.Children.Remove(path); _previewPaths.Clear(); }
        #endregion
    }
}