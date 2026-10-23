using Metal_Code.Models;
using Metal_Code.Services;
using Metal_Code.Utils;
using Microsoft.Win32;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Units;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Ellipse = System.Windows.Shapes.Ellipse;
using Line = System.Windows.Shapes.Line;
using Point = System.Windows.Point;
using TextAlignment = System.Windows.TextAlignment;

namespace Metal_Code
{
    public partial class StandartPartWindow : Window
    {
        #region
        private readonly Part _currentPart;
        private readonly ObservableCollection<Part> _batchBuffer = new();
        private readonly Dictionary<Part, (Dictionary<double, (int Count, double BendLength)> BendsX,
                                            Dictionary<double, (int Count, double BendLength)> BendsY)> _batchBendsInfo = new();
        private readonly PointCollection _customPoints = new();
        private bool _isDrawingActive = false;
        private bool _isContourFinished = false;
        private bool _isTitleManual = false;
        private bool _isPanning = false;
        private Point _panStartScreen;
        private Point _panStartOffset;

        private const double SheetWidthMm = 3000;
        private const double SheetHeightMm = 1500;
        private const double Epsilon = 0.5;
        private const double ClosingSnapToleranceMm = 10.0;
        private static readonly double[] GridSteps = { 10, 20, 50, 100, 200, 250, 500, 1000 };

        private bool _isEditMode = false;
        private int _selectedSegmentIndex = -1;
        private const double SegmentClickTolerancePx = 15;
        public bool UseAutoNesting { get; private set; } = true;
        public double CustomSpacing { get; private set; } = 0;
        public double CustomClampZone { get; private set; } = 340;
        public double CustomCutLoss { get; private set; } = 10;

        private bool _isPlacingHoles = false;
        private const double MinEdgeDistanceMm = 3.0;

        private int _selectedHoleIndex = -1;
        private int _nearestVerticalSegmentIndex = -1;
        private int _nearestHorizontalSegmentIndex = -1;
        private const double HoleClickTolerancePx = 15;

        private readonly ObservableCollection<LengthChipItem> _recentChips = new();

        public event Action<StandartPartWindow>? PartsAdded;

        #endregion

        #region
        public StandartPartWindow(Part templatePart)
        {
            InitializeComponent();
            _currentPart = templatePart;
            DataContext = _currentPart;
            BatchItemsControl.ItemsSource = _batchBuffer;
            UpdateWindowTitle();

            BendEditorX.ProfileChanged += RecalculateBends;
            BendEditorY.ProfileChanged += RecalculateBends;

            InitializePipeInput();

            _currentPart.PropertyChanged += (s, e) =>
            {
                // 1. Обновляем заголовок окна при смене материала или толщины
                if (e.PropertyName == nameof(Part.Metal) || e.PropertyName == nameof(Part.Destiny))
                    UpdateWindowTitle();

                // 2. Обновляем превью только при изменении габаритов (не Custom)
                if (_currentPart.PartType != PartType.Custom &&
                    (e.PropertyName == nameof(Part.Width) ||
                     e.PropertyName == nameof(Part.Height) ||
                     e.PropertyName == nameof(Part.Length)))
                {
                    if (_currentPart.Width > 0 && _currentPart.Height > 0)
                        UpdatePreview();
                }

                // 3. Перерасчёт матрицы при изменении длины гиба (ширина/высота)
                // Защита _isUpdatingFromBend предотвращает дублирующий вызов из RecalculateBends
                if (!_isUpdatingFromBend &&
                    (e.PropertyName == nameof(Part.Width) || e.PropertyName == nameof(Part.Height)))
                {
                    bool hasX = BendEditorX != null && BendEditorX.HasProfile && BendEditorX.Profile.IsFinished;
                    bool hasY = BendEditorY != null && BendEditorY.HasProfile && BendEditorY.Profile.IsFinished;
                    if (hasX || hasY)
                    {
                        UpdateMatrixRecommendation();
                    }
                }

                // Автогенерация имени для ЛИСТОВЫХ деталей
                if (!IsPipePart(_currentPart.PartType) &&
                    (e.PropertyName == nameof(Part.Width) ||
                     e.PropertyName == nameof(Part.Height) ||
                     e.PropertyName == nameof(Part.Destiny)))
                {
                    _currentPart.Title = GenerateSheetTitle();
                    _currentPart.OnPropertyChanged(nameof(Part.Title));
                }

                // Автогенерация имени для ТРУБНЫХ деталей
                if (IsPipePart(_currentPart.PartType) && e.PropertyName == nameof(Part.Length))
                {
                    _currentPart.Title = GenerateTitle(_currentPart.Length);
                    _currentPart.OnPropertyChanged(nameof(Part.Title));
                }
            };

            if (_currentPart.PartType == PartType.Rectangle) RectangleRadio.IsChecked = true;

            if (_currentPart.HoleGroups is INotifyCollectionChanged incc)
                incc.CollectionChanged += (s, e) => UpdatePreview();

            UpdatePreview();
        }

        private void InitializePipeInput()
        {
            if (RecentLengthsItemsControl != null)
                RecentLengthsItemsControl.ItemsSource = _recentChips;
        }

        private void PipeLengthInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddToBatch_Click(sender, e);
                e.Handled = true;
            }
        }

        private void UpdateWindowTitle()
        {
            string metal = _currentPart.Metal ?? "Не выбран";
            string thickness = _currentPart.Destiny > 0 ? $"{_currentPart.Destiny:0.#} мм" : "—";

            Title = $"Стандартные детали | {metal} | {thickness}";
        }

        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                _isTitleManual = true;
            }
        }

        private void DimensionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Если пользователь уже задал имя вручную, мы его НЕ трогаем
            if (_isTitleManual) return;

            // Иначе генерируем имя автоматически на основе текущих данных
            GenerateAutoTitle();
        }

        private void GenerateAutoTitle()
        {
            if (_currentPart == null) return;

            string? newTitle;

            if (IsPipePart(_currentPart.PartType))
            {
                newTitle = GenerateTitle(_currentPart.Length);
            }
            else
            {
                newTitle = GenerateSheetTitle();
            }

            if (TitleTextBox.Text != newTitle)
            {
                TitleTextBox.Text = newTitle;
            }
        }
        #endregion

        #region
        private void DrawingArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0 && e.NewSize.Height > 0) InitializeView();
        }

        private void InitializeView()
        {
            if (DrawingAreaGrid.ActualWidth <= 0 || DrawingAreaGrid.ActualHeight <= 0) return;

            double fitScale = Math.Min(
                DrawingAreaGrid.ActualWidth / SheetWidthMm,
                DrawingAreaGrid.ActualHeight / SheetHeightMm) * 0.80;

            CanvasScale.ScaleX = fitScale;
            CanvasScale.ScaleY = fitScale;

            double sheetWidthPx = SheetWidthMm * fitScale;
            double sheetHeightPx = SheetHeightMm * fitScale;

            CanvasTranslate.X = (DrawingAreaGrid.ActualWidth - sheetWidthPx) / 2;
            CanvasTranslate.Y = (DrawingAreaGrid.ActualHeight - sheetHeightPx) / 2;

            RedrawBackground();
            RedrawCanvas();
        }

        private double GetAdaptiveGridStep()
        {
            double currentScale = DrawingAreaGrid.ActualWidth > 0 ? DrawingAreaGrid.ActualWidth / SheetWidthMm : 1.0;
            double desiredStepMm = 80 / currentScale;
            return GridSteps.OrderBy(step => Math.Abs(desiredStepMm - step)).First();
        }

        private void RedrawBackground()
        {
            BackgroundCanvas.Children.Clear();
            double gridStep = GetAdaptiveGridStep();
            double offset = 15 / CanvasScale.ScaleX;
            double fontSize = 12 / CanvasScale.ScaleX;

            BackgroundCanvas.Children.Add(new Rectangle
            {
                Width = SheetWidthMm,
                Height = SheetHeightMm,
                Stroke = Brushes.Black,
                StrokeThickness = 2 / CanvasScale.ScaleX,
                Fill = Brushes.White
            });

            for (double x = gridStep; x < SheetWidthMm; x += gridStep)
            {
                BackgroundCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = SheetHeightMm, Stroke = Brushes.LightGray, StrokeThickness = 1 / CanvasScale.ScaleX });
                AddLabel($"{x:0}", x, SheetHeightMm + offset, fontSize, Brushes.Gray, false, true);
            }

            for (double y = gridStep; y < SheetHeightMm; y += gridStep)
            {
                BackgroundCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = SheetWidthMm, Y2 = y, Stroke = Brushes.LightGray, StrokeThickness = 1 / CanvasScale.ScaleX });
                AddLabel($"{SheetHeightMm - y:0}", -offset, y, fontSize, Brushes.Gray, true, false);
            }

            AddLabel("0", -offset, SheetHeightMm + offset, fontSize * 1.2, Brushes.Red, true, false, true);
            AddLabel("3000", SheetWidthMm, SheetHeightMm + offset, fontSize, Brushes.Gray, false, true, true);
            AddLabel("1500", -offset, 0, fontSize, Brushes.Gray, true, false, true);
        }

        private void AddLabel(string text, double x, double y, double fontSize, Brush color, bool alignRight, bool alignCenterX, bool isBold = false)
        {
            var lbl = new TextBlock { Text = text, FontSize = fontSize, Foreground = color, FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal };
            lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double left = alignRight ? x - lbl.DesiredSize.Width : (alignCenterX ? x - lbl.DesiredSize.Width / 2 : x);
            double top = y - lbl.DesiredSize.Height / 2;

            Canvas.SetLeft(lbl, left);
            Canvas.SetTop(lbl, top);
            BackgroundCanvas.Children.Add(lbl);
        }

        private bool IsPipePart(PartType type)
        {
            return type == PartType.RoundTube || type == PartType.RectangularTube ||
                   type == PartType.Angle || type == PartType.Channel || type == PartType.IBeam ||
                   type == PartType.Circle || type == PartType.SquareBar;
        }
        #endregion

        #region
        private void RedrawCanvas(Point? previewPoint = null)
        {
            DrawingCanvas.Children.Clear();

            if (IsPipePart(_currentPart.PartType))
            {
                DrawPipeView();
                return;
            }

            double strokeThickness = 2 / CanvasScale.ScaleX;
            double markerSize = 6 / CanvasScale.ScaleX;
            double dashThickness = 1 / CanvasScale.ScaleX;

            if (_currentPart.PartType != PartType.Custom)
            {
                DrawTemplateGeometry(strokeThickness);
                DrawSheetDimensions();
                DrawBendLines(strokeThickness);
                RedrawIsometricPreview();

                if (_currentPart.HoleGroups != null && _currentPart.HoleGroups.Any(g => g.Count > 50))
                {
                    Rect bounds = (Rect)(_currentPart.DisplayGeometry?.Bounds)!;
                    if (!bounds.IsEmpty)
                    {
                        double centerX = bounds.X + (bounds.Width / 2.0);
                        double centerY = bounds.Y + (bounds.Height / 2.0);
                        double offset = (SheetWidthMm / 2) - (bounds.X + bounds.Width / 2);
                        double offsetY = (SheetHeightMm / 2) - (bounds.Y + bounds.Height / 2);

                        double canvasCx = centerX + offset;
                        double canvasCy = centerY + offsetY;

                        double indicatorSize = 40 / CanvasScale.ScaleX;
                        DrawingCanvas.Children.Add(new Ellipse
                        {
                            Width = indicatorSize,
                            Height = indicatorSize,
                            Stroke = Brushes.Magenta,
                            StrokeThickness = 2 / CanvasScale.ScaleX,
                            StrokeDashArray = new DoubleCollection { 4, 4 },
                            Fill = new SolidColorBrush(Color.FromArgb(30, 255, 0, 255)),
                            RenderTransform = new TranslateTransform(canvasCx - indicatorSize / 2, canvasCy - indicatorSize / 2)
                        });

                        string summaryText = string.Join(", ", _currentPart.HoleGroups.Select(g => $"⌀{g.Diameter:0}x{g.Count}"));
                        var label = new TextBlock
                        {
                            Text = summaryText,
                            FontSize = 14 / CanvasScale.ScaleX,
                            Foreground = Brushes.Magenta,
                            FontWeight = FontWeights.Bold,
                            Background = Brushes.White,
                            TextAlignment = TextAlignment.Center
                        };
                        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                        Canvas.SetLeft(label, canvasCx - label.DesiredSize.Width / 2);
                        Canvas.SetTop(label, canvasCy - label.DesiredSize.Height / 2);
                        DrawingCanvas.Children.Add(label);
                    }
                }
                return;
            }

            for (int i = 0; i < _customPoints.Count; i++)
            {
                var p = _customPoints[i];
                DrawingCanvas.Children.Add(new Ellipse
                {
                    Width = markerSize,
                    Height = markerSize,
                    Fill = Brushes.DodgerBlue,
                    RenderTransform = new TranslateTransform(p.X - markerSize / 2, p.Y - markerSize / 2)
                });

                if (i < _customPoints.Count - 1)
                {
                    var pNext = _customPoints[i + 1];
                    DrawingCanvas.Children.Add(new Line
                    {
                        X1 = p.X,
                        Y1 = p.Y,
                        X2 = pNext.X,
                        Y2 = pNext.Y,
                        Stroke = Brushes.Black,
                        StrokeThickness = strokeThickness
                    });
                }
            }

            if (previewPoint.HasValue && !_isContourFinished && _customPoints.Count > 0)
            {
                var last = _customPoints.Last();
                DrawingCanvas.Children.Add(new Line
                {
                    X1 = last.X,
                    Y1 = last.Y,
                    X2 = previewPoint.Value.X,
                    Y2 = previewPoint.Value.Y,
                    Stroke = Brushes.DodgerBlue,
                    StrokeThickness = dashThickness,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                });
            }

            if (_customPoints.Count >= 3)
            {
                var first = _customPoints[0];
                var last = _customPoints.Last();
                var effectiveLast = GetSnappedClosingPoint(first, last);
                bool canClose = CanCloseContour();

                DrawingCanvas.Children.Add(new Line
                {
                    X1 = effectiveLast.X,
                    Y1 = effectiveLast.Y,
                    X2 = first.X,
                    Y2 = first.Y,
                    Stroke = _isContourFinished ? Brushes.Black : (canClose ? Brushes.Green : Brushes.Red),
                    StrokeThickness = _isContourFinished ? strokeThickness : dashThickness,
                    StrokeDashArray = _isContourFinished ? null : new DoubleCollection { 4, 4 }
                });

                if (!_isContourFinished && (effectiveLast.X != last.X || effectiveLast.Y != last.Y))
                {
                    DrawingCanvas.Children.Add(new Line
                    {
                        X1 = last.X,
                        Y1 = last.Y,
                        X2 = effectiveLast.X,
                        Y2 = effectiveLast.Y,
                        Stroke = Brushes.Gray,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 2, 2 }
                    });
                }
            }

            if (_isEditMode && _isContourFinished)
            {
                double labelFontSize = 14 / CanvasScale.ScaleX;

                for (int i = 0; i < _customPoints.Count; i++)
                {
                    Point p1 = _customPoints[i];
                    Point p2 = (i < _customPoints.Count - 1) ? _customPoints[i + 1] : _customPoints[0];

                    double length = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
                    double midX = (p1.X + p2.X) / 2;
                    double midY = (p1.Y + p2.Y) / 2;

                    double offsetX = 0, offsetY = 0;
                    bool isHorizontal = Math.Abs(p1.Y - p2.Y) < Epsilon;

                    if (isHorizontal) offsetY = -15 / CanvasScale.ScaleX;
                    else offsetX = 15 / CanvasScale.ScaleX;

                    bool isSelected = (i == _selectedSegmentIndex);
                    var lineColor = isSelected ? Brushes.Red : Brushes.Black;
                    var lineThickness = isSelected ? strokeThickness * 2 : strokeThickness;

                    DrawingCanvas.Children.Add(new Line
                    {
                        X1 = p1.X,
                        Y1 = p1.Y,
                        X2 = p2.X,
                        Y2 = p2.Y,
                        Stroke = lineColor,
                        StrokeThickness = lineThickness
                    });

                    var label = new TextBlock
                    {
                        Text = $"{length:0} мм",
                        FontSize = labelFontSize,
                        Foreground = isSelected ? Brushes.Red : Brushes.DarkBlue,
                        FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                        Background = Brushes.White
                    };

                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(label, midX + offsetX - label.DesiredSize.Width / 2);
                    Canvas.SetTop(label, midY + offsetY - label.DesiredSize.Height / 2);
                    DrawingCanvas.Children.Add(label);
                }
            }

            if (_currentPart.PartType == PartType.Custom && _currentPart.PlacedHoles.Count > 0)
            {
                for (int i = 0; i < _currentPart.PlacedHoles.Count; i++)
                {
                    var hole = _currentPart.PlacedHoles[i];
                    double radius = hole.Diameter / 2.0;
                    bool isSelected = (i == _selectedHoleIndex);

                    DrawingCanvas.Children.Add(new Ellipse
                    {
                        Width = radius * 2,
                        Height = radius * 2,
                        Stroke = isSelected ? Brushes.Yellow : Brushes.Red,
                        StrokeThickness = (isSelected ? 3 : 2) / CanvasScale.ScaleX,
                        Fill = new SolidColorBrush(Color.FromArgb((byte)(isSelected ? 100 : 60), 255, 0, 0)),
                        RenderTransform = new TranslateTransform(hole.X - radius, hole.Y - radius)
                    });

                    if (isSelected)
                    {
                        var dashArray = new DoubleCollection { 3, 3 };
                        var lineBrush = Brushes.DarkGreen;
                        double lineThickness = 1.5 / CanvasScale.ScaleX;

                        if (_nearestVerticalSegmentIndex >= 0 && _nearestVerticalSegmentIndex < _customPoints.Count)
                        {
                            Point p1 = _customPoints[_nearestVerticalSegmentIndex];
                            DrawingCanvas.Children.Add(new Line { X1 = hole.X, Y1 = hole.Y, X2 = p1.X, Y2 = hole.Y, Stroke = lineBrush, StrokeThickness = lineThickness, StrokeDashArray = dashArray });
                        }
                        if (_nearestHorizontalSegmentIndex >= 0 && _nearestHorizontalSegmentIndex < _customPoints.Count)
                        {
                            Point p1 = _customPoints[_nearestHorizontalSegmentIndex];
                            DrawingCanvas.Children.Add(new Line { X1 = hole.X, Y1 = hole.Y, X2 = hole.X, Y2 = p1.Y, Stroke = lineBrush, StrokeThickness = lineThickness, StrokeDashArray = dashArray });
                        }
                    }

                    var label = new TextBlock
                    {
                        Text = isSelected ? $"⌀{hole.Diameter:0}\nX:{hole.X:0} Y:{hole.Y:0}" : $"⌀{hole.Diameter:0}",
                        FontSize = 12 / CanvasScale.ScaleX,
                        Foreground = isSelected ? Brushes.DarkBlue : Brushes.DarkRed,
                        FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                        Background = Brushes.White,
                        TextAlignment = TextAlignment.Center
                    };
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(label, hole.X - label.DesiredSize.Width / 2);
                    Canvas.SetTop(label, hole.Y - label.DesiredSize.Height / 2);
                    DrawingCanvas.Children.Add(label);
                }
            }
        }

        private void IsoPreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawIsometricPreview();
        }

        private void RedrawIsometricPreview()
        {
            if (IsoPreviewCanvas == null || _currentPart == null) return;
            if (_currentPart.PartType != PartType.Rectangle) return;
            if (_currentPart.Width <= 0 || _currentPart.Height <= 0) return;

            IsometricPreviewRenderer.RenderInto(
                IsoPreviewCanvas,
                _currentPart.Width,
                _currentPart.Height,
                _currentPart.Destiny,
                BendEditorX?.Profile,
                BendEditorY?.Profile);
        }

        private void DrawTemplateGeometry(double strokeThickness)
        {
            if (_currentPart.DisplayGeometry == null) return;

            var bounds = _currentPart.DisplayGeometry.Bounds;
            double offsetX = (SheetWidthMm / 2) - (bounds.X + bounds.Width / 2);
            double offsetY = (SheetHeightMm / 2) - (bounds.Y + bounds.Height / 2);

            var path = new Path
            {
                Data = _currentPart.DisplayGeometry,
                Stroke = Brushes.Black,
                StrokeThickness = strokeThickness * 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215)),
                RenderTransform = new TranslateTransform(offsetX, offsetY)
            };
            DrawingCanvas.Children.Add(path);
        }

        private void OnShapeTypeChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string shapeType)
            {
                _isDrawingActive = false;
                _isContourFinished = false;
                _isTitleManual = false;
                _customPoints.Clear();
                DrawingCanvas.Children.Clear();

                ResetBendParameters();

                switch (shapeType)
                {
                    case "Round": _currentPart.PartType = PartType.Round; _currentPart.Title = "Круг"; break;
                    case "Triangle": _currentPart.PartType = PartType.Triangle; _currentPart.Title = "Треугольник"; break;
                    case "Custom":
                        _currentPart.PartType = PartType.Custom;
                        _currentPart.Title = "Произвольная форма";
                        _isDrawingActive = true;
                        _currentPart.Width = 0;
                        _currentPart.Height = 0;
                        break;
                    default: _currentPart.PartType = PartType.Rectangle; _currentPart.Title = "Прямоугольник"; break;
                }

                _currentPart.OnPropertyChanged(nameof(Part.PartType));
                _currentPart.OnPropertyChanged(nameof(Part.Title));
                _currentPart.HoleGroups.Clear();

                if (_currentPart.PartType == PartType.Custom)
                {
                    _currentPart.DisplayGeometry = null;
                    _currentPart.OnPropertyChanged(nameof(Part.DisplayGeometry));
                }
                else
                {
                    if (_currentPart.Width <= 0) _currentPart.Width = 100;
                    if (_currentPart.Height <= 0) _currentPart.Height = 100;
                    UpdatePreview();
                }
            }
        }

        private void UpdatePreview()
        {
            if (_currentPart.PartType == PartType.Custom) return;
            if (_currentPart.Width <= 0 || _currentPart.Height <= 0) return;

            _currentPart.DisplayGeometry = null;
            if (_currentPart.HoleGroups.Count > 0)
                PartPreviewGenerator.EnsureDisplayGeometryWithHoles(_currentPart);
            else
                PartPreviewGenerator.EnsureDisplayGeometry(_currentPart);

            RedrawCanvas();
        }
        #endregion

        #region
        private void DrawPipeView()
        {
            if (_currentPart.DisplayGeometry == null) return;

            var bounds = _currentPart.DisplayGeometry.Bounds;
            if (bounds.IsEmpty || bounds.Width < 0.1) return;

            double canvasW = 3000;
            double canvasH = 1500;

            double targetSectionPx = canvasW * 0.22;
            double maxDim = Math.Max(bounds.Width, bounds.Height);
            double sScale = targetSectionPx / maxDim;

            double secCX = canvasW * 0.2;
            double secCY = canvasH * 0.48;

            var sectionPath = new Path
            {
                Data = _currentPart.DisplayGeometry,
                Stroke = Brushes.Black,
                StrokeThickness = 2.0 / sScale,
                Fill = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215)),
                RenderTransform = new TransformGroup
                {
                    Children = new TransformCollection
                    {
                        new ScaleTransform(sScale, sScale),
                        new TranslateTransform(
                            secCX - (bounds.X + bounds.Width / 2) * sScale,
                            secCY - (bounds.Y + bounds.Height / 2) * sScale)
                    }
                }
            };
            DrawingCanvas.Children.Add(sectionPath);

            double targetSidePx = canvasW * 0.35;
            double sideLength = Math.Max(_currentPart.Length, 1);
            double sideScale = targetSidePx / sideLength;
            double sideW = sideLength * sideScale;
            double sideH = bounds.Height * sScale;
            double sideX = canvasW * 0.52;
            double sideY = secCY - sideH / 2;

            var sideRect = new Rectangle
            {
                Width = sideW,
                Height = Math.Max(sideH, 4),
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(25, 0, 120, 215))
            };
            Canvas.SetLeft(sideRect, sideX);
            Canvas.SetTop(sideRect, sideY);
            DrawingCanvas.Children.Add(sideRect);

            double dimOffset = 30;
            double dimFontSize = 35;

            double secLeft = secCX - bounds.Width * sScale / 2;
            double secRight = secCX + bounds.Width * sScale / 2;
            double secBottom = secCY + bounds.Height * sScale / 2;
            DrawDimLine(secLeft, secBottom + dimOffset, secRight, secBottom + dimOffset, $"Ш: {bounds.Width:0}", dimFontSize, true);

            double secTop = secCY - bounds.Height * sScale / 2;
            DrawDimLine(secRight + dimOffset, secTop, secRight + dimOffset, secBottom, $"В: {bounds.Height:0}", dimFontSize, false);

            DrawDimLine(sideX, sideY + sideH + dimOffset, sideX + sideW, sideY + sideH + dimOffset,
                        $"L = {_currentPart.Length:0}", dimFontSize, true);

            DrawCanvasLabel(GetPipeTypeName(_currentPart.PartType),
                            secCX, secTop - dimOffset * 2, dimFontSize, Brushes.DarkBlue);
        }
        #endregion

        #region
        private void DrawDimLine(double x1, double y1, double x2, double y2, string text, double fontSize, bool horizontal)
        {
            // ⭐ Масштабируем элементы размерной линии вместе с холстом
            double scale = 1.0 / CanvasScale.ScaleX;
            double arrowSize = 12 * scale;
            double stroke = 1.5 * scale;
            var dimBrush = Brushes.DarkSlateGray;

            // Основная линия
            DrawingCanvas.Children.Add(new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = dimBrush,
                StrokeThickness = stroke
            });

            if (horizontal)
            {
                // Засечки по краям
                DrawingCanvas.Children.Add(new Line { X1 = x1, Y1 = y1 - arrowSize, X2 = x1, Y2 = y1 + arrowSize, Stroke = dimBrush, StrokeThickness = stroke });
                DrawingCanvas.Children.Add(new Line { X1 = x2, Y1 = y2 - arrowSize, X2 = x2, Y2 = y2 + arrowSize, Stroke = dimBrush, StrokeThickness = stroke });

                // Текст по центру снизу
                var label = new TextBlock
                {
                    Text = text,
                    FontSize = fontSize,
                    Foreground = dimBrush,
                    FontWeight = FontWeights.Bold,
                    Background = Brushes.White // Чтобы линия не перечеркивала текст
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, (x1 + x2) / 2 - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, y1 + (5 * scale));
                DrawingCanvas.Children.Add(label);
            }
            else
            {
                // Засечки по краям
                DrawingCanvas.Children.Add(new Line { X1 = x1 - arrowSize, Y1 = y1, X2 = x1 + arrowSize, Y2 = y1, Stroke = dimBrush, StrokeThickness = stroke });
                DrawingCanvas.Children.Add(new Line { X1 = x2 - arrowSize, Y1 = y2, X2 = x2 + arrowSize, Y2 = y2, Stroke = dimBrush, StrokeThickness = stroke });

                // Текст по центру справа
                var label = new TextBlock
                {
                    Text = text,
                    FontSize = fontSize,
                    Foreground = dimBrush,
                    FontWeight = FontWeights.Bold,
                    Background = Brushes.White
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, x1 + (5 * scale));
                Canvas.SetTop(label, (y1 + y2) / 2 - label.DesiredSize.Height / 2);
                DrawingCanvas.Children.Add(label);
            }
        }

        private void DrawSheetDimensions()
        {
            if (_currentPart.DisplayGeometry == null || _currentPart.Width <= 0 || _currentPart.Height <= 0) return;

            var bounds = _currentPart.DisplayGeometry.Bounds;

            // Те же смещения, что и в DrawTemplateGeometry, чтобы размеры совпадали с фигурой
            double offsetX = (SheetWidthMm / 2) - (bounds.X + bounds.Width / 2);
            double offsetY = (SheetHeightMm / 2) - (bounds.Y + bounds.Height / 2);

            double scale = 1.0 / CanvasScale.ScaleX;
            double dimOffset = 40 * scale; // Отступ размерной линии от детали
            double fontSize = 22 * scale;  // Размер шрифта, масштабируемый вместе с холстом

            // 1. Размерная линия ШИРИНЫ (внизу)
            double bottomY = bounds.Y + bounds.Height + offsetY + dimOffset;
            DrawDimLine(
                bounds.X + offsetX, bottomY,
                bounds.X + bounds.Width + offsetX, bottomY,
                $"Ш: {_currentPart.Width:0}", fontSize, true
            );

            // 2. Размерная линия ВЫСОТЫ (справа)
            double rightX = bounds.X + bounds.Width + offsetX + dimOffset;
            DrawDimLine(
                rightX, bounds.Y + offsetY,
                rightX, bounds.Y + bounds.Height + offsetY,
                $"В: {_currentPart.Height:0}", fontSize, false
            );

            // 3. Спец. случай для Круга: добавим обозначение диаметра в центре
            if (_currentPart.PartType == PartType.Round)
            {
                DrawCanvasLabel(
                    $"⌀ {_currentPart.Width:0}",
                    bounds.X + bounds.Width / 2 + offsetX,
                    bounds.Y + bounds.Height / 2 + offsetY,
                    fontSize * 1.2,
                    Brushes.DarkBlue
                );
            }
        }

        private void DrawCanvasLabel(string text, double x, double y, double fontSize, Brush color)
        {
            var label = new TextBlock { Text = text, FontSize = fontSize, Foreground = color, FontWeight = FontWeights.Bold };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, y);
            DrawingCanvas.Children.Add(label);
        }

        private string GetPipeTypeName(PartType type) => type switch
        {
            PartType.RoundTube => "Круглая труба",
            PartType.RectangularTube => "Профильная труба",
            PartType.Angle => "Уголок",
            PartType.Channel => "Швеллер",
            PartType.IBeam => "Двутавр",
            PartType.Circle => "Круглый пруток",
            PartType.SquareBar => "Квадратный пруток",
            _ => "Труба"
        };
        #endregion

        #region
        private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var clickPoint = e.GetPosition(DrawingCanvas);
            clickPoint.X = Math.Clamp(clickPoint.X, 0, SheetWidthMm);
            clickPoint.Y = Math.Clamp(clickPoint.Y, 0, SheetHeightMm);

            int clickedHoleIndex = GetClickedHole(clickPoint);
            if (clickedHoleIndex >= 0)
            {
                SelectHoleForEditing(clickedHoleIndex);
                e.Handled = true;
                return;
            }

            if (_selectedHoleIndex >= 0)
            {
                _selectedHoleIndex = -1;
                HoleEditPanel.Visibility = Visibility.Collapsed;
                RedrawCanvas();
            }

            if (_isPlacingHoles && _isContourFinished)
            {
                PlaceHole(clickPoint);
                e.Handled = true;
                return;
            }

            if (_isEditMode && _isContourFinished)
            {
                int clickedSegment = GetClickedSegment(clickPoint);
                if (clickedSegment >= 0)
                {
                    _selectedSegmentIndex = clickedSegment;
                    SegmentSizeInput.Text = $"{GetSegmentLength(clickedSegment):0}";
                    CurrentSizeText.Text = $"Выбрана линия {clickedSegment + 1}";
                    SegmentSizeInput.IsEnabled = true;
                    SegmentSizeInput.Focus();
                    SegmentSizeInput.SelectAll();
                }
                else
                {
                    _selectedSegmentIndex = -1;
                    SegmentSizeInput.Text = "";
                    CurrentSizeText.Text = "Линия не найдена";
                    SegmentSizeInput.IsEnabled = false;
                }
                RedrawCanvas();
                e.Handled = true;
                return;
            }

            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            {
                _isPanning = true;
                _panStartScreen = e.GetPosition(DrawingAreaBorder);
                _panStartOffset = new Point(CanvasTranslate.X, CanvasTranslate.Y);
                DrawingAreaGrid.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (!_isDrawingActive || _isContourFinished) return;

            if (_customPoints.Count == 0)
            {
                _customPoints.Add(clickPoint);
            }
            else
            {
                var candidate = GetSnappedPoint(_customPoints.Last(), clickPoint);
                if (candidate == _customPoints.Last()) return;
                if (HasIntersections(_customPoints.Last(), candidate))
                {
                    MessageBox.Show("Линия пересекает существующий контур!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _customPoints.Add(candidate);
            }
            RedrawCanvas();
            UpdateCustomGeometry();
            UpdateFinishButtonState();
            e.Handled = true;
        }

        private void Grid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning) { _isPanning = false; DrawingAreaGrid.ReleaseMouseCapture(); }
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var current = e.GetPosition(DrawingAreaBorder);
                CanvasTranslate.X = _panStartOffset.X + (current.X - _panStartScreen.X);
                CanvasTranslate.Y = _panStartOffset.Y + (current.Y - _panStartScreen.Y);
                return;
            }

            if (_isDrawingActive && !_isContourFinished && _customPoints.Count > 0)
            {
                var mouse = e.GetPosition(DrawingCanvas);
                mouse.X = Math.Clamp(mouse.X, 0, SheetWidthMm);
                mouse.Y = Math.Clamp(mouse.Y, 0, SheetHeightMm);
                RedrawCanvas(GetSnappedPoint(_customPoints.Last(), mouse));
            }
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e) => RedrawCanvas();

        private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            double factor = e.Delta > 0 ? 1.2 : 1 / 1.2;
            var worldPoint = e.GetPosition(DrawingCanvas);

            double newScale = Math.Clamp(CanvasScale.ScaleX * factor, 0.05, 5.0);
            CanvasTranslate.X -= worldPoint.X * (newScale - CanvasScale.ScaleX);
            CanvasTranslate.Y -= worldPoint.Y * (newScale - CanvasScale.ScaleY);

            CanvasScale.ScaleX = newScale;
            CanvasScale.ScaleY = newScale;

            RedrawBackground();
            RedrawCanvas();
            e.Handled = true;
        }

        private void FitView_Click(object sender, RoutedEventArgs e) => InitializeView();
        private void ZoomIn_Click(object sender, RoutedEventArgs e) { CanvasScale.ScaleX *= 1.2; CanvasScale.ScaleY *= 1.2; RedrawBackground(); }
        private void ZoomOut_Click(object sender, RoutedEventArgs e) { CanvasScale.ScaleX /= 1.2; CanvasScale.ScaleY /= 1.2; RedrawBackground(); }
        #endregion

        #region
        private Point GetSnappedPoint(Point prev, Point mouse)
        {
            double dx = Math.Abs(mouse.X - prev.X);
            double dy = Math.Abs(mouse.Y - prev.Y);
            if (dx < 5 && dy < 5) return prev;
            return dx > dy ? new Point(mouse.X, prev.Y) : new Point(prev.X, mouse.Y);
        }

        private Point GetSnappedClosingPoint(Point first, Point last)
        {
            double dx = Math.Abs(last.X - first.X);
            double dy = Math.Abs(last.Y - first.Y);

            if (dx <= ClosingSnapToleranceMm && dy > ClosingSnapToleranceMm) return new Point(first.X, last.Y);
            else if (dy <= ClosingSnapToleranceMm && dx > ClosingSnapToleranceMm) return new Point(last.X, first.Y);
            else if (dx <= ClosingSnapToleranceMm && dy <= ClosingSnapToleranceMm) return first;
            return last;
        }

        private bool HasIntersections(Point p1, Point p2)
        {
            for (int i = 0; i < _customPoints.Count - 1; i++)
            {
                if (i == _customPoints.Count - 2) continue;
                if (DoSegmentsIntersect(p1, p2, _customPoints[i], _customPoints[i + 1])) return true;
            }
            return false;
        }

        private bool DoSegmentsIntersect(Point a1, Point a2, Point b1, Point b2)
        {
            bool aVert = Math.Abs(a1.X - a2.X) < Epsilon;
            bool bVert = Math.Abs(b1.X - b2.X) < Epsilon;

            if (aVert && bVert) return Math.Abs(a1.X - b1.X) < Epsilon && Math.Max(Math.Min(a1.Y, a2.Y), Math.Min(b1.Y, b2.Y)) <= Math.Min(Math.Max(a1.Y, a2.Y), Math.Max(b1.Y, b2.Y)) + Epsilon;
            if (!aVert && !bVert) return Math.Abs(a1.Y - b1.Y) < Epsilon && Math.Max(Math.Min(a1.X, a2.X), Math.Min(b1.X, b2.X)) <= Math.Min(Math.Max(a1.X, a2.X), Math.Max(b1.X, b2.X)) + Epsilon;

            Point v1 = aVert ? a1 : b1, v2 = aVert ? a2 : b2;
            Point h1 = aVert ? b1 : a1, h2 = aVert ? b2 : a2;

            return v1.X >= Math.Min(h1.X, h2.X) - Epsilon && v1.X <= Math.Max(h1.X, h2.X) + Epsilon &&
                   h1.Y >= Math.Min(v1.Y, v2.Y) - Epsilon && h1.Y <= Math.Max(v1.Y, v2.Y) + Epsilon;
        }

        private bool CanCloseContour()
        {
            if (_customPoints.Count < 3) return false;

            var first = _customPoints[0];
            var last = _customPoints.Last();
            var effectiveLast = GetSnappedClosingPoint(first, last);

            for (int i = 0; i < _customPoints.Count - 1; i++)
            {
                if (i == 0 || i == _customPoints.Count - 2) continue;
                if (DoSegmentsIntersect(effectiveLast, first, _customPoints[i], _customPoints[i + 1])) return false;
            }
            return true;
        }

        private void UpdateCustomGeometry()
        {
            if (_customPoints.Count < 3)
            {
                _currentPart.DisplayGeometry = null;
                _currentPart.Width = 0;
                _currentPart.Height = 0;
                _currentPart.OnPropertyChanged(nameof(Part.DisplayGeometry));
                _currentPart.OnPropertyChanged(nameof(Part.Width));
                _currentPart.OnPropertyChanged(nameof(Part.Height));
                return;
            }

            var first = _customPoints[0];
            var last = _customPoints.Last();
            var effectiveLast = GetSnappedClosingPoint(first, last);

            var pointsForSegment = _customPoints.Skip(1).ToList();
            if (pointsForSegment.Count > 0) pointsForSegment[pointsForSegment.Count - 1] = effectiveLast;

            var figure = new PathFigure
            {
                StartPoint = first,
                IsClosed = true,
                IsFilled = true,
                Segments = { new PolyLineSegment(pointsForSegment, true) }
            };

            _currentPart.DisplayGeometry = new PathGeometry { Figures = { figure } };

            var bounds = _currentPart.DisplayGeometry.Bounds;
            _currentPart.Width = Math.Ceiling(bounds.Width);
            _currentPart.Height = Math.Ceiling(bounds.Height);

            _currentPart.OnPropertyChanged(nameof(Part.DisplayGeometry));
            _currentPart.OnPropertyChanged(nameof(Part.Width));
            _currentPart.OnPropertyChanged(nameof(Part.Height));
        }

        private void UpdateFinishButtonState() => FinishDrawingButton.IsEnabled = CanCloseContour();

        private void UndoPoint_Click(object sender, RoutedEventArgs e)
        {
            if (_customPoints.Count > 0) { _customPoints.RemoveAt(_customPoints.Count - 1); RedrawCanvas(); UpdateCustomGeometry(); UpdateFinishButtonState(); }
        }

        private void ClearDrawing_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = false;
            _selectedSegmentIndex = -1;
            if (EditModeToggle != null) EditModeToggle.IsChecked = false;
            if (EditModePanel != null) EditModePanel.Visibility = Visibility.Collapsed;

            _selectedHoleIndex = -1;
            _nearestVerticalSegmentIndex = -1;
            _nearestHorizontalSegmentIndex = -1;
            if (HoleEditPanel != null) HoleEditPanel.Visibility = Visibility.Collapsed;

            _customPoints.Clear();
            _isContourFinished = false;
            if (_currentPart.PlacedHoles != null) _currentPart.PlacedHoles.Clear();

            RedrawCanvas();
            UpdateCustomGeometry();
            UpdateFinishButtonState();
        }

        private void FinishDrawing_Click(object sender, RoutedEventArgs e)
        {
            if (!CanCloseContour()) { MessageBox.Show("Невозможно замкнуть контур без пересечений.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            _isContourFinished = true; RedrawCanvas(); UpdateFinishButtonState();
            EditModePanel.Visibility = Visibility.Visible;
        }
        #endregion

        #region
        private void EditModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = EditModeToggle.IsChecked ?? false;
            _selectedSegmentIndex = -1;

            if (_isEditMode)
            {
                SegmentSizeInput.IsEnabled = false;
                CurrentSizeText.Text = "Кликните по линии";
            }
            else
            {
                SegmentSizeInput.Text = "";
                CurrentSizeText.Text = "—";
            }

            RedrawCanvas();
        }

        private void SegmentSizeInput_TextChanged(object sender, TextChangedEventArgs e) { }

        private void SegmentSizeInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplySize_Click(sender, e);
        }

        private void ApplySize_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSegmentIndex < 0 || _selectedSegmentIndex >= _customPoints.Count)
            {
                MessageBox.Show("Сначала выберите линию, кликнув по ней.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!double.TryParse(SegmentSizeInput.Text, out double newSize) || newSize <= 0)
            {
                MessageBox.Show("Введите корректный размер (> 0).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ApplyNewSizeToSegment(_selectedSegmentIndex, newSize);
            UpdateCustomGeometry();
            RedrawCanvas();

            _selectedSegmentIndex = -1;
            SegmentSizeInput.Text = "";
            CurrentSizeText.Text = "Кликните по линии";
            SegmentSizeInput.IsEnabled = false;
        }

        private int GetClickedSegment(Point clickPoint)
        {
            double minDistance = double.MaxValue;
            int closestSegment = -1;
            double toleranceMm = SegmentClickTolerancePx / CanvasScale.ScaleX;

            for (int i = 0; i < _customPoints.Count; i++)
            {
                Point p1 = _customPoints[i];
                Point p2 = (i < _customPoints.Count - 1) ? _customPoints[i + 1] : _customPoints[0];

                double distance = DistanceFromPointToSegment(clickPoint, p1, p2);
                if (distance < minDistance && distance <= toleranceMm)
                {
                    minDistance = distance;
                    closestSegment = i;
                }
            }
            return closestSegment;
        }

        private double DistanceFromPointToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            if (dx == 0 && dy == 0) return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));

            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));

            double closestX = a.X + t * dx;
            double closestY = a.Y + t * dy;

            return Math.Sqrt(Math.Pow(p.X - closestX, 2) + Math.Pow(p.Y - closestY, 2));
        }

        private void ApplyNewSizeToSegment(int segmentIndex, double newSize)
        {
            if (segmentIndex < 0 || segmentIndex >= _customPoints.Count) return;

            Point p1 = _customPoints[segmentIndex];
            Point p2 = (segmentIndex < _customPoints.Count - 1) ? _customPoints[segmentIndex + 1] : _customPoints[0];

            double currentLength = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            if (currentLength < 0.01) return;

            double scale = newSize / currentLength;
            bool isHorizontal = Math.Abs(p1.Y - p2.Y) < Epsilon;
            bool isVertical = Math.Abs(p1.X - p2.X) < Epsilon;

            if (isHorizontal)
            {
                double deltaX = (p2.X - p1.X) * (scale - 1);
                for (int i = segmentIndex + 1; i < _customPoints.Count; i++)
                    _customPoints[i] = new Point(_customPoints[i].X + deltaX, _customPoints[i].Y);
                if (segmentIndex == _customPoints.Count - 1)
                    _customPoints[0] = new Point(_customPoints[0].X + deltaX, _customPoints[0].Y);
            }
            else if (isVertical)
            {
                double deltaY = (p2.Y - p1.Y) * (scale - 1);
                for (int i = segmentIndex + 1; i < _customPoints.Count; i++)
                    _customPoints[i] = new Point(_customPoints[i].X, _customPoints[i].Y + deltaY);
                if (segmentIndex == _customPoints.Count - 1)
                    _customPoints[0] = new Point(_customPoints[0].X, _customPoints[0].Y + deltaY);
            }
            else
            {
                MessageBox.Show("Редактирование косых линий не поддерживается.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private double GetSegmentLength(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= _customPoints.Count) return 0;
            Point p1 = _customPoints[segmentIndex];
            Point p2 = (segmentIndex < _customPoints.Count - 1) ? _customPoints[segmentIndex + 1] : _customPoints[0];
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }
        #endregion

        #region
        private void AddHoleGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(DiameterInput.Text, out double d) || d <= 0) { MessageBox.Show("Некорректный диаметр", "Ошибка"); return; }
            if (!int.TryParse(CountInput.Text, out int c) || c <= 0) { MessageBox.Show("Некорректное количество", "Ошибка"); return; }

            _currentPart.HoleGroups.Add(new HoleGroup(d, c));
            DiameterInput.Text = "10"; CountInput.Text = "1";
            UpdatePreview();
        }

        private void RemoveHoleGroup_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is HoleGroup group) { _currentPart.HoleGroups.Remove(group); UpdatePreview(); }
        }

        private void HoleModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isPlacingHoles = HoleModeToggle.IsChecked ?? false;
            if (_isPlacingHoles)
            {
                _isEditMode = false;
                EditModeToggle.IsChecked = false;
            }
            RedrawCanvas();
        }

        private void RemovePlacedHole_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is PlacedHole hole)
            {
                _currentPart.PlacedHoles.Remove(hole);
                RedrawCanvas();
            }
        }

        private void PlaceHole(Point clickPoint)
        {
            if (!double.TryParse(HoleDiameterInput.Text, out double diameter) || diameter <= 0)
            {
                MessageBox.Show("Введите корректный диаметр отверстия.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!IsPointInPolygon(clickPoint, _customPoints))
            {
                MessageBox.Show("Отверстие должно располагаться внутри контура детали.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double minRequiredDistance = (diameter / 2.0) + MinEdgeDistanceMm;
            bool isTooClose = false;

            for (int i = 0; i < _customPoints.Count; i++)
            {
                Point p1 = _customPoints[i];
                Point p2 = (i < _customPoints.Count - 1) ? _customPoints[i + 1] : _customPoints[0];

                if (DistanceFromPointToSegment(clickPoint, p1, p2) < minRequiredDistance)
                {
                    isTooClose = true;
                    break;
                }
            }

            if (isTooClose)
            {
                MessageBox.Show($"Отверстие слишком близко к краю детали. Минимальный отступ: {MinEdgeDistanceMm} мм.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _currentPart.PlacedHoles.Add(new PlacedHole { X = clickPoint.X, Y = clickPoint.Y, Diameter = diameter });
            RedrawCanvas();
        }

        private bool IsPointInPolygon(Point p, PointCollection polygon)
        {
            bool isInside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y)) &&
                    (p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    isInside = !isInside;
                }
            }
            return isInside;
        }

        private int GetClickedHole(Point clickPoint)
        {
            if (_currentPart.PlacedHoles == null || _currentPart.PlacedHoles.Count == 0) return -1;
            double toleranceMm = HoleClickTolerancePx / CanvasScale.ScaleX;

            for (int i = 0; i < _currentPart.PlacedHoles.Count; i++)
            {
                var hole = _currentPart.PlacedHoles[i];
                double distance = Math.Sqrt(Math.Pow(clickPoint.X - hole.X, 2) + Math.Pow(clickPoint.Y - hole.Y, 2));
                double clickRadius = Math.Max(hole.Diameter / 2.0, toleranceMm);
                if (distance <= clickRadius) return i;
            }
            return -1;
        }

        private void FindNearestSegmentsToHole(Point holePoint)
        {
            _nearestVerticalSegmentIndex = -1;
            _nearestHorizontalSegmentIndex = -1;
            double minVertDist = double.MaxValue, minHorizDist = double.MaxValue;

            for (int i = 0; i < _customPoints.Count; i++)
            {
                Point p1 = _customPoints[i];
                Point p2 = (i < _customPoints.Count - 1) ? _customPoints[i + 1] : _customPoints[0];
                bool isHorizontal = Math.Abs(p1.Y - p2.Y) < Epsilon;
                bool isVertical = Math.Abs(p1.X - p2.X) < Epsilon;

                if (isVertical)
                {
                    double dist = Math.Abs(holePoint.X - p1.X);
                    if (dist < minVertDist) { minVertDist = dist; _nearestVerticalSegmentIndex = i; }
                }
                else if (isHorizontal)
                {
                    double dist = Math.Abs(holePoint.Y - p1.Y);
                    if (dist < minHorizDist) { minHorizDist = dist; _nearestHorizontalSegmentIndex = i; }
                }
            }
        }

        private double GetDistanceToSegment(Point point, int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= _customPoints.Count) return 0;
            Point p1 = _customPoints[segmentIndex];
            bool isVertical = Math.Abs(p1.X - (segmentIndex < _customPoints.Count - 1 ? _customPoints[segmentIndex + 1].X : _customPoints[0].X)) < Epsilon;
            return isVertical ? Math.Abs(point.X - p1.X) : Math.Abs(point.Y - p1.Y);
        }

        private void SelectHoleForEditing(int holeIndex)
        {
            _selectedHoleIndex = holeIndex;
            var hole = _currentPart.PlacedHoles[holeIndex];
            FindNearestSegmentsToHole(new Point(hole.X, hole.Y));

            HoleEditPanel.Visibility = Visibility.Visible;

            if (_nearestVerticalSegmentIndex >= 0)
            {
                HoleHorizDistInput.Text = $"{GetDistanceToSegment(new Point(hole.X, hole.Y), _nearestVerticalSegmentIndex):0.##}";
                HoleHorizLabel.Visibility = Visibility.Visible;
                HoleHorizDistInput.Visibility = Visibility.Visible;
            }
            else { HoleHorizLabel.Visibility = Visibility.Collapsed; HoleHorizDistInput.Visibility = Visibility.Collapsed; }

            if (_nearestHorizontalSegmentIndex >= 0)
            {
                HoleVertDistInput.Text = $"{GetDistanceToSegment(new Point(hole.X, hole.Y), _nearestHorizontalSegmentIndex):0.##}";
                HoleVertLabel.Visibility = Visibility.Visible;
                HoleVertDistInput.Visibility = Visibility.Visible;
            }
            else { HoleVertLabel.Visibility = Visibility.Collapsed; HoleVertDistInput.Visibility = Visibility.Collapsed; }

            _isPlacingHoles = false;
            if (HoleModeToggle != null) HoleModeToggle.IsChecked = false;
            RedrawCanvas();
        }

        private void ApplyHolePosition_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedHoleIndex < 0 || _currentPart.PlacedHoles == null) return;
            var hole = _currentPart.PlacedHoles[_selectedHoleIndex];
            double newX = hole.X, newY = hole.Y;

            if (_nearestVerticalSegmentIndex >= 0 && double.TryParse(HoleHorizDistInput.Text.Replace(',', '.'), out double horizDist))
            {
                Point p1 = _customPoints[_nearestVerticalSegmentIndex];
                bool isLeft = hole.X < p1.X;
                newX = isLeft ? p1.X - horizDist : p1.X + horizDist;
            }

            if (_nearestHorizontalSegmentIndex >= 0 && double.TryParse(HoleVertDistInput.Text.Replace(',', '.'), out double vertDist))
            {
                Point p1 = _customPoints[_nearestHorizontalSegmentIndex];
                bool isBelow = hole.Y > p1.Y;
                newY = isBelow ? p1.Y + vertDist : p1.Y - vertDist;
            }

            if (!IsPointInPolygon(new Point(newX, newY), _customPoints))
            {
                MessageBox.Show("Отверстие выходит за пределы контура детали.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            hole.X = newX; hole.Y = newY;
            _selectedHoleIndex = -1;
            HoleEditPanel.Visibility = Visibility.Collapsed;
            RedrawCanvas();
            SyncHolesToGroups();
        }

        private void CancelHoleEdit_Click(object sender, RoutedEventArgs e)
        {
            _selectedHoleIndex = -1;
            HoleEditPanel.Visibility = Visibility.Collapsed;
            RedrawCanvas();
        }

        private void HoleDistInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyHolePosition_Click(sender, e);
        }

        private void SyncHolesToGroups()
        {
            if (_currentPart.PartType == PartType.Custom && _currentPart.PlacedHoles.Count > 0)
            {
                _currentPart.HoleGroups.Clear();
                foreach (var group in _currentPart.PlacedHoles.GroupBy(h => h.Diameter))
                    _currentPart.HoleGroups.Add(new HoleGroup(group.Key, group.Count()));
            }
        }
        #endregion

        #region
        private double _bendAngle = 90.0;
        private const double BendKFactor = 0.4;
        private bool _isUpdatingFromBend = false;

        private readonly BendTableService _bendTableService = new();

        /// <summary>
        /// Определяет идентификатор материала для таблицы гибов по названию металла детали.
        /// </summary>
        private string DetermineMaterialId()
        {
            string metal = _currentPart.Metal?.ToLowerInvariant() ?? "";

            if (metal.Contains("aisi") || metal.Contains("нерж"))
                return "stainless";
            else if (metal.Contains("амг") || metal.Contains("д16") || metal.Contains("алюм"))
                return "aluminum";
            else if (metal.Contains("латунь") || metal.Contains("br"))
                return "brass";
            else if (metal.Contains("медь") || metal.Contains("cu") || metal.Contains("cu."))
                return "copper";
            else
                return "steel"; // ст3, хк, 09г2с и прочие
        }

        /// <summary>
        /// Подбирает оптимальную матрицу для текущих профилей гибки.
        /// Критерии: минимальная полка, допустимость по толщине, усилие ≤ 100 т.
        /// </summary>
        private void UpdateMatrixRecommendation()
        {
            if (MatrixRecommendationText == null || MatrixRecommendationBorder == null) return;

            bool hasX = BendEditorX != null && BendEditorX.HasProfile && BendEditorX.Profile.IsFinished;
            bool hasY = BendEditorY != null && BendEditorY.HasProfile && BendEditorY.Profile.IsFinished;

            if (!hasX && !hasY)
            {
                MatrixRecommendationBorder.Visibility = Visibility.Collapsed;
                return;
            }

            var (bendsX, bendsY) = CalculateBendsInfo();

            double minShelf = double.MaxValue;
            double maxBendLength = 0;
            int totalBends = 0;

            foreach (var kvp in bendsX)
            {
                minShelf = Math.Min(minShelf, kvp.Key);
                maxBendLength = Math.Max(maxBendLength, kvp.Value.BendLength);
                totalBends += kvp.Value.Count;
            }
            foreach (var kvp in bendsY)
            {
                minShelf = Math.Min(minShelf, kvp.Key);
                maxBendLength = Math.Max(maxBendLength, kvp.Value.BendLength);
                totalBends += kvp.Value.Count;
            }

            if (minShelf == double.MaxValue || maxBendLength <= 0 || totalBends == 0)
            {
                MatrixRecommendationBorder.Visibility = Visibility.Collapsed;
                return;
            }

            string materialId = DetermineMaterialId();
            double thickness = _currentPart.Destiny;
            string thicknessKey = thickness.ToString("0.0", CultureInfo.InvariantCulture);

            var candidateMatrices = _bendTableService.GetAllMatrices()
                .Where(m => m.Forces.ContainsKey(materialId)
                         && m.Forces[materialId].ContainsKey(thicknessKey))
                .Where(m => m.MinFlange <= minShelf)
                .OrderBy(m => m.MatrixWidth)
                .ToList();

            if (candidateMatrices.Count == 0)
            {
                ShowRecommendation(
                    $"⚠ Гибка недопустима: полка {minShelf:0.#} мм слишком мала для толщины {thickness:0.#} мм.",
                    Brushes.OrangeRed);
                return;
            }

            // Ищем матрицу, подходящую по усилию
            BendMatrixData? forceMatrix = null;
            double? bestForce = null;

            foreach (var matrix in candidateMatrices)
            {
                double? force = _bendTableService.GetBendingForce(
                    matrix.MatrixWidth, thickness, materialId, maxBendLength);

                if (force.HasValue && force.Value <= 100)
                {
                    forceMatrix = matrix;
                    bestForce = force;
                    break;
                }
            }

            if (forceMatrix == null || bestForce == null)
            {
                var bestMatrix = candidateMatrices.Last();
                double? lastForce = _bendTableService.GetBendingForce(
                    bestMatrix.MatrixWidth, thickness, materialId, maxBendLength);
                double? lastForcePerMeter = _bendTableService.GetBendingForce(
                    bestMatrix.MatrixWidth, thickness, materialId, 1000);

                ShowRecommendation(
                    $"✗ Усилие превышает 100 т даже на максимальной матрице: " +
                    $"V={bestMatrix.MatrixWidth:0} → {lastForcePerMeter:0.#} т/м × {maxBendLength:0} мм = {lastForce:0.#} т.",
                    Brushes.OrangeRed);
                return;
            }

            var profileProblems = new List<string>();
            bool hasComplexProfile = false;

            if (hasX && BendEditorX != null)
            {
                var segmentsX = GetSegmentLengths(BendEditorX.Profile);
                if (segmentsX.Count <= 3)
                {
                    var problemsX = _bendTableService.ValidateProfileForMatrix(
                        forceMatrix, thickness, segmentsX);
                    profileProblems.AddRange(problemsX.Select(p => $"X: {p}"));
                }
                else
                {
                    hasComplexProfile = true;
                }
            }

            if (hasY && BendEditorY != null)
            {
                var segmentsY = GetSegmentLengths(BendEditorY.Profile);
                if (segmentsY.Count <= 3)
                {
                    var problemsY = _bendTableService.ValidateProfileForMatrix(
                        forceMatrix, thickness, segmentsY);
                    profileProblems.AddRange(problemsY.Select(p => $"Y: {p}"));
                }
                else
                {
                    hasComplexProfile = true;
                }
            }

            double? forcePerMeter = _bendTableService.GetBendingForce(
                forceMatrix.MatrixWidth, thickness, materialId, 1000);

            if (profileProblems.Count > 0)
            {
                // Собираем предложения по исправлению
                var allSuggestions = new List<BendFixSuggestion>();

                if (hasX && BendEditorX != null)
                {
                    var segmentsX = GetSegmentLengths(BendEditorX.Profile);
                    var suggestionsX = _bendTableService.SuggestProfileFixes(
                        forceMatrix, thickness, segmentsX);
                    allSuggestions.AddRange(suggestionsX);
                }

                if (hasY && BendEditorY != null)
                {
                    var segmentsY = GetSegmentLengths(BendEditorY.Profile);
                    var suggestionsY = _bendTableService.SuggestProfileFixes(
                        forceMatrix, thickness, segmentsY);
                    allSuggestions.AddRange(suggestionsY);
                }

                string problemsText = string.Join("\n", profileProblems.Select(p => $"  • {p}"));

                string suggestionsText = allSuggestions.Count > 0
                    ? "\nВарианты исправления:\n" + string.Join("\n", allSuggestions.Select(s => $"  → {s.Description}"))
                    : "\nИзмените размеры профиля или подберите другую матрицу.";

                ShowRecommendation(
                    $"⚠ Матрица V={forceMatrix.MatrixWidth:0} подходит по усилию ({forcePerMeter:0.#} т/м), " +
                    $"но профиль согнуть невозможно:\n{problemsText}\n{suggestionsText}",
                    Brushes.OrangeRed);
            }
            else if (hasComplexProfile)
            {
                ShowRecommendation(
                    $"⚠ Матрица V={forceMatrix.MatrixWidth:0} мм подходит по усилию " +
                    $"({forcePerMeter:0.#} т/м × {maxBendLength:0} мм = {bestForce.Value:0.#} т), " +
                    $"но профиль содержит более 3 гибов.\n" +
                    $"Автоматическая проверка геометрии невозможна — " +
                    $"согласуйте возможность гибки и последовательность операций с гибщиком.\n" +
                    $"Полка от {forceMatrix.MinFlange:0.#} мм | Радиус R={forceMatrix.InnerRadius:0.#} мм | " +
                    $"Гибов: {totalBends}",
                    Brushes.DarkOrange);
            }
            else
            {
                ShowRecommendation(
                    $"✓ Матрица V={forceMatrix.MatrixWidth:0} мм | " +
                    $"Усилие: {forcePerMeter:0.#} т/м × {maxBendLength:0} мм = {bestForce.Value:0.#} т | " +
                    $"Полка от {forceMatrix.MinFlange:0.#} мм | Радиус R={forceMatrix.InnerRadius:0.#} мм | " +
                    $"Гибов: {totalBends}",
                    Brushes.Green);
            }
        }

        /// <summary>
        /// Возвращает список длин сегментов профиля.
        /// </summary>
        private static List<double> GetSegmentLengths(BendProfileDrawing profile)
        {
            var lengths = new List<double>();

            for (int i = 0; i < profile.SegmentsCount; i++)
            {
                lengths.Add(profile.GetSegmentLength(i));
            }

            return lengths;
        }

        /// <summary>Отображает текст рекомендации с заданным цветом.</summary>
        private void ShowRecommendation(string text, Brush foreground)
        {
            MatrixRecommendationText.Text = text;
            MatrixRecommendationText.Foreground = foreground;
            MatrixRecommendationBorder.Visibility = Visibility.Visible;
        }

        private double BendCompensation
        {
            get
            {
                double T = _currentPart.Destiny;
                double R = T;
                double angleRad = Math.PI * _bendAngle / 180.0;
                double halfAngleTan = Math.Tan(angleRad / 2.0);
                double bendAllowance = angleRad * (R + BendKFactor * T);
                return 2 * (R + T) * halfAngleTan - bendAllowance;
            }
        }

        private void BendExpander_Expanded(object sender, RoutedEventArgs e)
        {
            RedrawIsometricPreview();
        }

        private void BendParameters_Changed(object sender, TextChangedEventArgs e)
        {
            if (_currentPart == null) return;
            if (double.TryParse(BendAngleInput.Text, out double angle) && angle > 0 && angle < 180)
                _bendAngle = angle;
            RecalculateBends();
        }

        private void OnBendProfileChanged()
        {
            RecalculateBends();
            RedrawIsometricPreview();
        }

        private void RecalculateBends()
        {
            if (_isUpdatingFromBend || _currentPart == null) return;
            if (BendEditorX == null || BendEditorY == null) return;
            _isUpdatingFromBend = true;

            try
            {
                double bd = BendCompensation;
                bool hasX = BendEditorX.HasProfile && BendEditorX.Profile.IsFinished;
                bool hasY = BendEditorY.HasProfile && BendEditorY.Profile.IsFinished;

                if (hasX)
                {
                    double flatWidth = BendEditorX.Profile.CalculateFlatLength(bd);
                    _currentPart.Width = Math.Max(0.1, Math.Ceiling(flatWidth));
                }

                if (hasY)
                {
                    double flatHeight = BendEditorY.Profile.CalculateFlatLength(bd);
                    _currentPart.Height = Math.Max(0.1, Math.Ceiling(flatHeight));
                }

                if (hasX || hasY)
                {
                    UpdatePreview();
                    RedrawIsometricPreview();
                }

                UpdateMainDimensionsState(hasX, hasY);
                UpdateBendInfo(hasX, hasY);
                UpdateMatrixRecommendation();
            }
            finally
            {
                _isUpdatingFromBend = false;
            }
        }

        private void UpdateMainDimensionsState(bool hasX, bool hasY)
        {
            if (WidthInput != null) WidthInput.IsEnabled = !hasX;
            if (HeightInput != null) HeightInput.IsEnabled = !hasY;
        }

        private void UpdateBendInfo(bool hasX, bool hasY)
        {
            if (BendInfoText == null) return;
            if (!hasX && !hasY)
            {
                BendInfoText.Text = "Профили не заданы";
                return;
            }

            double bd = BendCompensation;
            var lines = new List<string> { $"Вычет на 1 гиб: {bd:F2} мм" };

            if (hasX)
            {
                var p = BendEditorX.Profile;
                lines.Add($"X: сегментов {p.SegmentsCount}, гибов {p.BendsCount}, " +
                          $"развертка = {_currentPart.Width:0} мм");
            }
            if (hasY)
            {
                var p = BendEditorY.Profile;
                lines.Add($"Y: сегментов {p.SegmentsCount}, гибов {p.BendsCount}, " +
                          $"развертка = {_currentPart.Height:0} мм");
            }

            BendInfoText.Text = string.Join("\n", lines);
        }

        private void DrawBendLines(double strokeThickness)
        {
            if (DrawingCanvas == null || _currentPart == null) return;
            if (_currentPart.PartType != PartType.Rectangle) return;

            double width = _currentPart.Width;
            double height = _currentPart.Height;
            if (width <= 0 || height <= 0) return;

            double offsetX = (SheetWidthMm / 2) - (width / 2);
            double offsetY = (SheetHeightMm / 2) - (height / 2);

            var bendBrush = Brushes.DarkOrange;
            var dashArray = new DoubleCollection { 5, 3 };
            double lineThickness = strokeThickness * 0.8;
            double bd = BendCompensation;

            // Линии по X (вертикальные)
            if (BendEditorX?.Profile is { IsFinished: true, Points.Count: >= 3 } profileX)
            {
                foreach (double pos in profileX.GetBendPositions(bd))
                {
                    if (pos <= 0 || pos >= width) continue;
                    double x = offsetX + pos;
                    DrawingCanvas.Children.Add(new Line
                    {
                        X1 = x,
                        Y1 = offsetY,
                        X2 = x,
                        Y2 = offsetY + height,
                        Stroke = bendBrush,
                        StrokeThickness = lineThickness,
                        StrokeDashArray = dashArray
                    });
                }
            }

            // Линии по Y (горизонтальные)
            if (BendEditorY?.Profile is { IsFinished: true, Points.Count: >= 3 } profileY)
            {
                foreach (double pos in profileY.GetBendPositions(bd))
                {
                    if (pos <= 0 || pos >= height) continue;
                    double y = offsetY + pos;
                    DrawingCanvas.Children.Add(new Line
                    {
                        X1 = offsetX,
                        Y1 = y,
                        X2 = offsetX + width,
                        Y2 = y,
                        Stroke = bendBrush,
                        StrokeThickness = lineThickness,
                        StrokeDashArray = dashArray
                    });
                }
            }
        }

        private (Dictionary<double, (int Count, double BendLength)> BendsX,
                 Dictionary<double, (int Count, double BendLength)> BendsY) CalculateBendsInfo()
        {
            var bendsX = new Dictionary<double, (int Count, double BendLength)>();
            var bendsY = new Dictionary<double, (int Count, double BendLength)>();
            if (_currentPart == null) return (bendsX, bendsY);

            void MergeBendInfo(BendProfileDrawing? profile, double crossLength,
                               Dictionary<double, (int Count, double BendLength)> target)
            {
                if (profile == null || !profile.IsFinished) return;
                foreach (var (shelfSize, count, bendLength) in profile.GetBendInfo(crossLength))
                {
                    if (target.ContainsKey(shelfSize))
                    {
                        var ex = target[shelfSize];
                        target[shelfSize] = (ex.Count + count, bendLength);
                    }
                    else target[shelfSize] = (count, bendLength);
                }
            }

            MergeBendInfo(BendEditorX?.Profile, _currentPart.Height, bendsX);
            MergeBendInfo(BendEditorY?.Profile, _currentPart.Width, bendsY);
            return (bendsX, bendsY);
        }

        private void ResetBendParameters()
        {
            _bendAngle = 90.0;
            if (BendAngleInput != null) BendAngleInput.Text = "90";
            BendEditorX?.Profile.Clear();
            BendEditorY?.Profile.Clear();
            if (BendInfoText != null) BendInfoText.Text = "Профили не заданы";
            UpdateMainDimensionsState(false, false);

            // Скрываем рекомендацию
            if (MatrixRecommendationBorder != null)
                MatrixRecommendationBorder.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region
        private void ExportToDxf_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPart.PartType == PartType.Custom && _customPoints.Count < 3)
            {
                MessageBox.Show("Для экспорта произвольной формы необходимо завершить построение контура (минимум 3 точки).",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Файлы DXF (*.dxf)|*.dxf",
                FileName = $"{_currentPart.Title ?? "Деталь"}_{_currentPart.Metal}_{_currentPart.Destiny}мм.dxf",
                DefaultExt = ".dxf",
                Title = "Сохранить DXF-файл детали"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    CreateDxfFile(saveFileDialog.FileName, _currentPart);
                    MessageBox.Show("Файл успешно сохранен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении DXF: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CreateDxfFile(string filePath, Part part)
        {
            var dxf = new DxfDocument(DxfVersion.AutoCad2010);
            dxf.DrawingVariables.InsUnits = DrawingUnits.Millimeters;

            if (part.PartType == PartType.Custom) ExportCustomPart(dxf, part);
            else if (part.PartType == PartType.Rectangle || part.PartType == PartType.Round || part.PartType == PartType.Triangle) ExportSheetPart(dxf, part);
            else if (IsPipePart(part.PartType)) ExportPipePart(dxf, part);

            dxf.Save(filePath);
        }

        private void ExportCustomPart(DxfDocument dxf, Part part)
        {
            double minX = _customPoints.Min(p => p.X);
            double minY = _customPoints.Min(p => p.Y);

            if (part.PlacedHoles.Count > 0)
            {
                minX = Math.Min(minX, part.PlacedHoles.Min(h => h.X - h.Diameter / 2.0));
                minY = Math.Min(minY, part.PlacedHoles.Min(h => h.Y - h.Diameter / 2.0));
            }

            var polyline = new Polyline2D { IsClosed = true };
            foreach (var pt in _customPoints)
                polyline.Vertexes.Add(new Polyline2DVertex(new Vector2(pt.X - minX, pt.Y - minY)));
            dxf.Entities.Add(polyline);

            foreach (var hole in part.PlacedHoles)
            {
                var circle = new Circle(new Vector2(hole.X - minX, hole.Y - minY), hole.Diameter / 2.0);
                dxf.Entities.Add(circle);
            }
        }

        private void ExportSheetPart(DxfDocument dxf, Part part)
        {
            double offsetX, offsetY;

            if (part.PartType == PartType.Rectangle)
            {
                var rect = new Polyline2D { IsClosed = true };
                rect.Vertexes.Add(new Polyline2DVertex(new Vector2(0, 0)));
                rect.Vertexes.Add(new Polyline2DVertex(new Vector2(part.Width, 0)));
                rect.Vertexes.Add(new Polyline2DVertex(new Vector2(part.Width, part.Height)));
                rect.Vertexes.Add(new Polyline2DVertex(new Vector2(0, part.Height)));
                dxf.Entities.Add(rect);
                offsetX = part.Width / 2.0; offsetY = part.Height / 2.0;
            }
            else if (part.PartType == PartType.Round)
            {
                double radius = part.Width / 2.0;
                var circle = new Circle(new Vector2(radius, radius), radius);
                dxf.Entities.Add(circle);
                offsetX = radius; offsetY = radius;
            }
            else if (part.PartType == PartType.Triangle)
            {
                var triangle = new Polyline2D { IsClosed = true };
                triangle.Vertexes.Add(new Polyline2DVertex(new Vector2(0, 0)));
                triangle.Vertexes.Add(new Polyline2DVertex(new Vector2(part.Width, 0)));
                triangle.Vertexes.Add(new Polyline2DVertex(new Vector2(0, part.Height)));
                dxf.Entities.Add(triangle);
                offsetX = part.Width / 2.0; offsetY = part.Height / 2.0;
            }
            else return;

            if (part.HoleGroups != null && part.HoleGroups.Count > 0)
            {
                var holePositions = PartPreviewGenerator.CalculateHolePositions(part);
                foreach (var (position, hole) in holePositions)
                {
                    var circle = new Circle(new Vector2(position.X + offsetX, position.Y + offsetY), hole.Diameter / 2.0);
                    dxf.Entities.Add(circle);
                }
            }
        }

        private void ExportPipePart(DxfDocument dxf, Part part)
        {
            double offsetX = part.Width / 2.0;
            double offsetY = part.Height / 2.0;

            if (part.DisplayGeometry != null)
            {
                foreach (PathFigure figure in part.DisplayGeometry.Figures)
                {
                    var points = new List<Vector2>();
                    points.Add(new Vector2(figure.StartPoint.X + offsetX, figure.StartPoint.Y + offsetY));

                    foreach (PathSegment segment in figure.Segments)
                    {
                        if (segment is LineSegment lineSeg) points.Add(new Vector2(lineSeg.Point.X + offsetX, lineSeg.Point.Y + offsetY));
                        else if (segment is PolyLineSegment polySeg)
                            foreach (var pt in polySeg.Points) points.Add(new Vector2(pt.X + offsetX, pt.Y + offsetY));
                        else if (segment is ArcSegment arcSeg) points.Add(new Vector2(arcSeg.Point.X + offsetX, arcSeg.Point.Y + offsetY));
                    }

                    if (points.Count >= 2)
                    {
                        var polyline = new Polyline2D { IsClosed = figure.IsClosed };
                        foreach (var pt in points) polyline.Vertexes.Add(new Polyline2DVertex(pt));
                        dxf.Entities.Add(polyline);
                    }
                }
            }
        }
        #endregion

        #region
        private string GetProfileDesignation()
        {
            var typeName = GetPipeTypeName(_currentPart.PartType);
            double thickness = _currentPart.Destiny;

            return _currentPart.PartType switch
            {
                PartType.RoundTube or PartType.Circle => thickness > 0
                    ? $"{typeName} ⌀{_currentPart.Width:0.#}x{thickness:0.#}"
                    : $"{typeName} ⌀{_currentPart.Width:0.#}",
                PartType.SquareBar => thickness > 0
                    ? $"{typeName} {_currentPart.Width:0.#}x{thickness:0.#}"
                    : $"{typeName} {_currentPart.Width:0.#}",
                _ => thickness > 0
                    ? $"{typeName} {_currentPart.Width:0.#}x{_currentPart.Height:0.#}x{thickness:0.#}"
                    : $"{typeName} {_currentPart.Width:0.#}x{_currentPart.Height:0.#}"
            };
        }

        private string GenerateTitle(double length)
        {
            var profile = GetProfileDesignation();
            return length > 0 ? $"{profile} L{length:0}" : profile;
        }

        /// <summary>
        /// Генерирует автоимя для листовых деталей (прямоугольник, круг, треугольник, произвольная)
        /// </summary>
        private string? GenerateSheetTitle()
        {
            double thickness = _currentPart.Destiny;
            string thicknessStr = thickness > 0 ? $"x{thickness:0.#}" : "";

            return _currentPart.PartType switch
            {
                PartType.Round => thickness > 0
                    ? $"Круг ⌀{_currentPart.Width:0}{thicknessStr}"
                    : $"Круг ⌀{_currentPart.Width:0}",
                PartType.Rectangle => $"Прямоугольник {_currentPart.Width:0}x{_currentPart.Height:0}{thicknessStr}",
                PartType.Triangle => $"Треугольник {_currentPart.Width:0}x{_currentPart.Height:0}{thicknessStr}",
                PartType.Custom => $"Произвольная {_currentPart.Width:0}x{_currentPart.Height:0}{thicknessStr}",
                _ => _currentPart.Title
            };
        }

        private void AddSinglePartToBatch(double length, int count, string? customTitle)
        {
            if (length <= 0 || count <= 0) return;

            var title = string.IsNullOrWhiteSpace(customTitle)
                ? GenerateTitle(length)
                : customTitle;

            // Автообъединение
            var existing = _batchBuffer.FirstOrDefault(p =>
                p.PartType == _currentPart.PartType &&
                Math.Abs(p.Length - length) < 0.1 &&
                p.Title == title);

            if (existing != null)
            {
                existing.Count += count;
                BatchItemsControl.Items.Refresh();
            }
            else
            {
                var clone = new Part
                {
                    Title = title,
                    Count = count,
                    Metal = _currentPart.Metal,
                    Destiny = _currentPart.Destiny,
                    Width = _currentPart.Width,
                    Height = _currentPart.Height,
                    Length = length,
                    PartType = _currentPart.PartType,
                    HoleGroups = _currentPart.HoleGroups != null
                        ? new ObservableCollection<HoleGroup>(_currentPart.HoleGroups)
                        : new ObservableCollection<HoleGroup>(),
                    DisplayGeometry = _currentPart.DisplayGeometry,
                    PropsDict = _currentPart.PropsDict != null
                        ? new Dictionary<int, List<string>>(_currentPart.PropsDict)
                        : new Dictionary<int, List<string>>()
                };
                _batchBuffer.Add(clone);
            }

            // Обновляем чипс
            AddOrUpdateChip(length, count);
        }

        /// <summary>
        /// Добавляет или обновляет чипс для указанной длины
        /// </summary>
        private void AddOrUpdateChip(double length, int delta)
        {
            var chip = _recentChips.FirstOrDefault(c => Math.Abs(c.Length - length) < 0.1);

            if (chip != null)
            {
                chip.Count += delta;
                if (chip.Count <= 0)
                    _recentChips.Remove(chip);
            }
            else if (delta > 0)
            {
                _recentChips.Insert(0, new LengthChipItem(length, delta));
                // Ограничиваем количество чипсов
                while (_recentChips.Count > 8)
                    _recentChips.RemoveAt(_recentChips.Count - 1);
            }
        }

        /// <summary>
        /// Пересчитывает все чипсы на основе текущего буфера деталей
        /// </summary>
        private void RefreshChipsFromBuffer()
        {
            _recentChips.Clear();

            var grouped = _batchBuffer
                .Where(p => IsPipePart(p.PartType))
                .GroupBy(p => p.Length)
                .OrderByDescending(g => g.First().Length)
                .Take(8);

            foreach (var group in grouped)
            {
                int totalCount = group.Sum(p => p.Count);
                if (totalCount > 0)
                    _recentChips.Add(new LengthChipItem(group.Key, totalCount));
            }
        }

        /// <summary>
        /// Кнопка "+" на чипсе — добавить 1 деталь этой длины
        /// </summary>
        private void ChipIncrement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LengthChipItem chip)
            {
                AddSinglePartToBatch(chip.Length, 1, null);
            }
        }

        /// <summary>
        /// Кнопка "−" на чипсе — убрать 1 деталь этой длины
        /// </summary>
        private void ChipDecrement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LengthChipItem chip)
            {
                RemoveOneFromBuffer(chip.Length);
                RefreshChipsFromBuffer();
            }
        }

        /// <summary>
        /// Удаляет одну деталь указанной длины из буфера
        /// </summary>
        private void RemoveOneFromBuffer(double length)
        {
            var part = _batchBuffer.FirstOrDefault(p =>
                IsPipePart(p.PartType) && Math.Abs(p.Length - length) < 0.1);

            if (part == null) return;

            if (part.Count > 1)
            {
                part.Count--;
                BatchItemsControl.Items.Refresh();
            }
            else
            {
                _batchBuffer.Remove(part);
                _batchBendsInfo.Remove(part);
            }
        }
        #endregion

        #region
        public List<Part> GetBatchedParts() => new(_batchBuffer);
        public ObservableCollection<PipeRemnant> PipeRemnants { get; set; } = new();

        private void OpenRemnants_Click(object sender, RoutedEventArgs e)
        {
            string profile = _currentPart.PartType switch
            {
                PartType.Rectangle => "Прямоугольник",
                PartType.Round => "Круг",
                PartType.Triangle => "Треугольник",
                PartType.Custom => "Произвольная форма",
                PartType.RectangularTube => "Профильная труба",
                PartType.RoundTube => "Круглая труба",
                PartType.Angle => "Уголок",
                PartType.Channel => "Швеллер",
                PartType.IBeam => "Двутавр",
                PartType.Circle => "Круглый прут",
                PartType.SquareBar => "Квадратный прут",
                _ => "Остаток"
            };

            string currentProfileName = $"{profile} {_currentPart.Width}x{_currentPart.Height}x{_currentPart.Destiny} {_currentPart.Metal}";

            var window = new PipeRemnantsWindow(currentProfileName, PipeRemnants) { Owner = GetWindow(this) };

            if (window.ShowDialog() == true)
            {
                PipeRemnants.Clear();
                foreach (var r in window.Remnants) PipeRemnants.Add(r);
                MainWindow.M.StatusBegin($"Сохранено остатков: {PipeRemnants.Sum(r => r.Count)} шт.", MainWindow.StatusMessageType.Success);
            }
        }

        public Dictionary<Part, (Dictionary<double, (int Count, double BendLength)> BendsX,
                                 Dictionary<double, (int Count, double BendLength)> BendsY)> GetBendsInfoForBatch()
        {
            return new Dictionary<Part, (Dictionary<double, (int Count, double BendLength)>,
                                          Dictionary<double, (int Count, double BendLength)>)>(_batchBendsInfo);
        }

        private void AddToBatch_Click(object sender, RoutedEventArgs e)
        {
            string? title;
            bool wasAutoTitle = _isTitleManual == false;

            // ===== ТРУБНЫЕ ДЕТАЛИ =====
            if (IsPipePart(_currentPart.PartType))
            {
                if (_currentPart.Length <= 0)
                {
                    MessageBox.Show("Укажите длину детали.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (_currentPart.Count <= 0)
                {
                    MessageBox.Show("Укажите количество.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (wasAutoTitle)
                {
                    title = GenerateTitle(_currentPart.Length);
                    _currentPart.Title = title;
                    _currentPart.OnPropertyChanged(nameof(Part.Title));
                }
                else
                {
                    title = _currentPart.Title;
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        MessageBox.Show("Укажите название или включите автогенерацию.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                AddSinglePartToBatch(_currentPart.Length, _currentPart.Count, title);

                // Устанавливаем дефолтную длину 1000 мм
                _currentPart.Length = 1000;
                _currentPart.OnPropertyChanged(nameof(Part.Length));

                // Возвращаем фокус в поле длины
                PipeLengthInput?.Focus();
                PipeLengthInput?.SelectAll();
                return;
            }

            // ===== ЛИСТОВЫЕ ДЕТАЛИ =====
            if (_currentPart.Count <= 0) { MessageBox.Show("Укажите количество", "Ошибка"); return; }

            // Определяем название
            if (wasAutoTitle && _currentPart.Width > 0 && _currentPart.Height > 0)
            {
                title = GenerateSheetTitle();
                _currentPart.Title = title;
                _currentPart.OnPropertyChanged(nameof(Part.Title));
            }
            else
            {
                title = _currentPart.Title;
                if (string.IsNullOrWhiteSpace(title))
                {
                    MessageBox.Show("Укажите название или включите автогенерацию.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Проверка на дубликаты
            if (_batchBuffer.Any(p => p.Title == title))
            {
                MessageBox.Show("Такое название уже есть в списке", "Ошибка");
                return;
            }

            // Валидация отверстий
            if (_currentPart.PartType != PartType.Custom)
            {
                var (isValid, error) = PartPreviewGenerator.ValidateHolesPlacement(_currentPart);
                if (!isValid && MessageBox.Show($"Проблемы с отверстиями:\n{error}\nПродолжить без них?", "Внимание", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                if (!isValid) _currentPart.HoleGroups.Clear();
            }

            if (_currentPart.PartType == PartType.Custom && _currentPart.PlacedHoles.Count > 0)
            {
                SyncHolesToGroups();
                PartPreviewGenerator.EnsureDisplayGeometryWithHoles(_currentPart);
            }

            var clone = ClonePart(_currentPart);
            if (clone != null)
            {
                _batchBuffer.Add(clone);

                // Собираем информацию о гибах для текущей детали (разделённую по X и Y)
                var (bendsX, bendsY) = CalculateBendsInfo();
                if (bendsX.Count > 0 || bendsY.Count > 0)
                {
                    _batchBendsInfo[clone] = (bendsX, bendsY);
                }

                _currentPart.HoleGroups.Clear();
                if (_currentPart.PartType == PartType.Custom) ClearDrawing_Click(sender, e);
                else UpdatePreview();
            }

            // Возвращаем фокус на поле ширины и выделяем весь текст
            WidthInput?.Focus();
            WidthInput?.SelectAll();
        }

        private void FinishBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_batchBuffer.Count == 0) { MessageBox.Show("Список пуст", "Ошибка"); return; }

            UseAutoNesting = AutoNestingCheck.IsChecked ?? true;
            CustomSpacing = double.TryParse(SpacingInput.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double s) && s >= 0 ? s : 0;
            CustomClampZone = Clamp340Radio.IsChecked == true ? 340 :
                              Clamp160Radio.IsChecked == true ? 160 : 0;
            CustomCutLoss = double.TryParse(CutLossInput.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double cl) && cl >= 0 ? cl : 10;

            PartsAdded?.Invoke(this);
            Close();
        }

        private void RemoveFromBatch_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is Part part)
            {
                _batchBuffer.Remove(part);
                _batchBendsInfo.Remove(part);

                // Обновляем чипсы
                if (IsPipePart(part.PartType))
                    RefreshChipsFromBuffer();
            }
        }

        private static Part ClonePart(Part s)
        {
            return new Part
            {
                Title = s.Title,
                Count = s.Count,
                Metal = s.Metal,
                Destiny = s.Destiny,
                Width = s.Width,
                Height = s.Height,
                Length = s.Length,
                PartType = s.PartType,
                HoleGroups = s.HoleGroups != null ? new ObservableCollection<HoleGroup>(s.HoleGroups) : new ObservableCollection<HoleGroup>(),
                DisplayGeometry = s.PartType == PartType.Custom ? PartPreviewGenerator.CloneGeometry(s.DisplayGeometry) : s.DisplayGeometry,
                PropsDict = s.PropsDict != null ? new Dictionary<int, List<string>>(s.PropsDict) : new Dictionary<int, List<string>>()
            };
        }
        #endregion
    }

    public class BendProfileController
    {
        private BendProfileBase _profile;

        public BendProfileBase Profile
        {
            get => _profile;
            set
            {
                if (_profile != null) _profile.ParametersChanged -= OnParametersChanged;
                _profile = value;
                if (_profile != null) _profile.ParametersChanged += OnParametersChanged;
                RedrawCallback?.Invoke();
            }
        }

        public Action? RedrawCallback { get; set; }

        public BendProfileController(BendProfileBase initialProfile)
        {
            _profile = initialProfile;
            _profile.ParametersChanged += OnParametersChanged;
        }

        private void OnParametersChanged()
        {
            RedrawCallback?.Invoke();
        }
    }

    public class LengthChipItem : INotifyPropertyChanged
    {
        private int _count;

        public double Length { get; }
        public int Count
        {
            get => _count;
            set { _count = value; OnPropertyChanged(); }
        }

        public LengthChipItem(double length, int count = 1)
        {
            Length = length;
            _count = count;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}