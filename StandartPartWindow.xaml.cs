using Metal_Code.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    public partial class StandartPartWindow : Window
    {
        private readonly Part _currentPart;
        private readonly ObservableCollection<Part> _batchBuffer = new();
        private readonly Dictionary<Part, Dictionary<double, (int Count, double BendLength)>> _batchBendsInfo = new();
        
        // Состояние рисования
        private readonly PointCollection _customPoints = new();
        private bool _isDrawingActive = false;
        private bool _isContourFinished = false;

        // Состояние панорамирования
        private bool _isPanning = false;
        private Point _panStartScreen;
        private Point _panStartOffset;

        // Константы листа и сетки (в мм)
        private const double SheetWidthMm = 3000;
        private const double SheetHeightMm = 1500;
        private const double Epsilon = 0.5;
        private const double ClosingSnapToleranceMm = 10.0;

        private static readonly double[] GridSteps = { 10, 20, 50, 100, 200, 250, 500, 1000 };

        // Режим редактирования размеров
        private bool _isEditMode = false;
        private int _selectedSegmentIndex = -1; // Индекс выбранного сегмента (-1 = не выбран)
        private const double SegmentClickTolerancePx = 15; // Допуск клика по линии (в пикселях экрана)

        public bool UseAutoNesting { get; private set; } = true;
        public double CustomSpacing { get; private set; } = 0;
        public double CustomClampZone { get; private set; } = 340;
        public double CustomCutLoss { get; private set; } = 10;

        public StandartPartWindow(Part templatePart)
        {
            InitializeComponent();
            _currentPart = templatePart;
            DataContext = _currentPart;
            BatchItemsControl.ItemsSource = _batchBuffer;
            UpdateWindowTitle();

            // Подписываемся на изменение материала/толщины (если они могут меняться)
            _currentPart.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Part.Metal) || e.PropertyName == nameof(Part.Destiny))
                    UpdateWindowTitle();

                if (_currentPart.PartType != PartType.Custom &&
                    (e.PropertyName == nameof(Part.Width) ||
                    e.PropertyName == nameof(Part.Height) ||
                    e.PropertyName == nameof(Part.Length)))
                {
                    if (_currentPart.Width > 0 && _currentPart.Height > 0)
                        UpdatePreview();
                }
            };

            if (_currentPart.PartType == PartType.Rectangle) RectangleRadio.IsChecked = true;

            if (_currentPart.HoleGroups is INotifyCollectionChanged incc)
                incc.CollectionChanged += (s, e) => UpdatePreview();

            UpdatePreview();
        }

        /// <summary>
        /// Обновляет заголовок окна и информационную панель с данными о материале
        /// </summary>
        private void UpdateWindowTitle()
        {
            string metal = _currentPart.Metal ?? "Не выбран";
            string thickness = _currentPart.Destiny > 0 ? $"{_currentPart.Destiny:0.#} мм" : "—";

            Title = $"Стандартные детали | {metal} | {thickness}";

            if (MaterialInfoText != null)
                MaterialInfoText.Text = $"📋 Материал: {metal} | Толщина: {thickness}";
        }

        private void OnShapeTypeChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string shapeType)
            {
                _isDrawingActive = false;
                _isContourFinished = false;
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

        // ==========================================
        // ЛОГИКА ХОЛСТА И СЕТКИ
        // ==========================================

        private void DrawingArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                InitializeView();
            }
        }

        private void InitializeView()
        {
            double fitScale = Math.Min(DrawingAreaBorder.ActualWidth / SheetWidthMm, DrawingAreaBorder.ActualHeight / SheetHeightMm) * 0.80;
            CanvasScale.ScaleX = fitScale;
            CanvasScale.ScaleY = fitScale;

            double sheetWidthPx = SheetWidthMm * fitScale;
            double sheetHeightPx = SheetHeightMm * fitScale;
            CanvasTranslate.X = (DrawingAreaBorder.ActualWidth - sheetWidthPx) / 2;
            CanvasTranslate.Y = (DrawingAreaBorder.ActualHeight - sheetHeightPx) / 2;

            RedrawBackground();
            RedrawCanvas();
        }

        private double GetAdaptiveGridStep()
        {
            double currentScale = DrawingAreaGrid.ActualWidth > 0 ? DrawingAreaGrid.ActualWidth / SheetWidthMm : 1.0;
            double desiredStepMm = 80 / currentScale; // Целевое расстояние ~80px

            return GridSteps.OrderBy(step => Math.Abs(desiredStepMm - step)).First();
        }

        private void RedrawBackground()
        {
            BackgroundCanvas.Children.Clear();
            double gridStep = GetAdaptiveGridStep();
            double offset = 15 / CanvasScale.ScaleX;
            double fontSize = 12 / CanvasScale.ScaleX;

            // 1. Граница листа
            BackgroundCanvas.Children.Add(new Rectangle
            {
                Width = SheetWidthMm,
                Height = SheetHeightMm,
                Stroke = Brushes.Black,
                StrokeThickness = 2 / CanvasScale.ScaleX,
                Fill = Brushes.White
            });

            // 2. Ось X (внизу)
            for (double x = gridStep; x < SheetWidthMm; x += gridStep)
            {
                BackgroundCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = SheetHeightMm, Stroke = Brushes.LightGray, StrokeThickness = 1 / CanvasScale.ScaleX });
                AddLabel($"{x:0}", x, SheetHeightMm + offset, fontSize, Brushes.Gray, false, true);
            }

            // 3. Ось Y (слева, инвертированная)
            for (double y = gridStep; y < SheetHeightMm; y += gridStep)
            {
                BackgroundCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = SheetWidthMm, Y2 = y, Stroke = Brushes.LightGray, StrokeThickness = 1 / CanvasScale.ScaleX });
                AddLabel($"{SheetHeightMm - y:0}", -offset, y, fontSize, Brushes.Gray, true, false);
            }

            // 4. Угловые метки
            AddLabel("0", -offset, SheetHeightMm + offset, fontSize * 1.2, Brushes.Red, true, false, true); // Нижний левый (КРАСНЫЙ)
            AddLabel("3000", SheetWidthMm, SheetHeightMm + offset, fontSize, Brushes.Gray, false, true, true); // Нижний правый
            AddLabel("1500", -offset, 0, fontSize, Brushes.Gray, true, false, true); // Верхний левый
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
                   type == PartType.Angle || type == PartType.Channel || type == PartType.IBeam;
        }

        // ==========================================
        // ОТРИСОВКА ДЕТАЛЕЙ
        // ==========================================

        private void RedrawCanvas(Point? previewPoint = null)
        {
            DrawingCanvas.Children.Clear();

            // Для трубных деталей — отдельный режим визуализации
            if (IsPipePart(_currentPart.PartType))
            {
                DrawPipeView();
                return;
            }

            // 🔥 ОБЪЯВЛЕНИЕ ПЕРЕМЕННЫХ (этого не хватало в фрагменте)
            double strokeThickness = 2 / CanvasScale.ScaleX;
            double markerSize = 6 / CanvasScale.ScaleX;
            double dashThickness = 1 / CanvasScale.ScaleX;

            // Если это шаблонная деталь — рисуем её и выходим
            if (_currentPart.PartType != PartType.Custom)
            {
                DrawTemplateGeometry(strokeThickness);
                DrawBendLines(strokeThickness);
                return;
            }

            // Отрисовка произвольной формы
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

            // "Резиновая нить" (превью следующего шага)
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

            // 🔥 УМНОЕ ЗАМЫКАНИЕ КОНТУРА
            if (_customPoints.Count >= 3)
            {
                var first = _customPoints[0];
                var last = _customPoints.Last();
                var effectiveLast = GetSnappedClosingPoint(first, last); // Применяем выравнивание
                bool canClose = CanCloseContour();

                // Основная замыкающая линия
                DrawingCanvas.Children.Add(new Line
                {
                    X1 = effectiveLast.X,
                    Y1 = effectiveLast.Y,
                    X2 = first.X,
                    Y2 = first.Y,
                    Stroke = _isContourFinished ? Brushes.Black : (canClose ? Brushes.Green : Brushes.Red),
                    // 🔥 Теперь компилятор найдет эти переменные, так как они объявлены выше
                    StrokeThickness = _isContourFinished ? strokeThickness : dashThickness,
                    StrokeDashArray = _isContourFinished ? null : new DoubleCollection { 4, 4 }
                });

                // Визуальная подсказка: тонкий пунктир от реального курсора к выровненной точке
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

            // 🔥 Отображение размеров линий (только в режиме редактирования после завершения контура)
            if (_isEditMode && _isContourFinished)
            {
                double labelFontSize = 14 / CanvasScale.ScaleX;

                for (int i = 0; i < _customPoints.Count; i++)
                {
                    Point p1 = _customPoints[i];
                    Point p2 = (i < _customPoints.Count - 1) ? _customPoints[i + 1] : _customPoints[0];

                    double length = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

                    // Средняя точка линии для размещения метки
                    double midX = (p1.X + p2.X) / 2;
                    double midY = (p1.Y + p2.Y) / 2;

                    // Смещение метки перпендикулярно линии
                    double offsetX = 0;
                    double offsetY = 0;
                    bool isHorizontal = Math.Abs(p1.Y - p2.Y) < Epsilon;

                    if (isHorizontal)
                    {
                        offsetY = -15 / CanvasScale.ScaleX; // Метка сверху от горизонтальной линии
                    }
                    else
                    {
                        offsetX = 15 / CanvasScale.ScaleX; // Метка справа от вертикальной линии
                    }

                    // Подсветка выбранной линии
                    bool isSelected = (i == _selectedSegmentIndex);
                    var lineColor = isSelected ? Brushes.Red : Brushes.Black;
                    var lineThickness = isSelected ? strokeThickness * 2 : strokeThickness;

                    // Перерисовываем линию с подсветкой (поверх существующей)
                    DrawingCanvas.Children.Add(new Line
                    {
                        X1 = p1.X,
                        Y1 = p1.Y,
                        X2 = p2.X,
                        Y2 = p2.Y,
                        Stroke = lineColor,
                        StrokeThickness = lineThickness
                    });

                    // Метка с размером
                    var label = new TextBlock
                    {
                        Text = $"{length:0} мм",
                        FontSize = labelFontSize,
                        Foreground = isSelected ? Brushes.Red : Brushes.DarkBlue,
                        FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                        Background = Brushes.White // Фон для читаемости
                    };

                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(label, midX + offsetX - label.DesiredSize.Width / 2);
                    Canvas.SetTop(label, midY + offsetY - label.DesiredSize.Height / 2);
                    DrawingCanvas.Children.Add(label);
                }
            }

            // 🔥 Отрисовка размещённых отверстий
            if (_currentPart.PartType == PartType.Custom && _currentPart.PlacedHoles.Count > 0)
            {
                foreach (var hole in _currentPart.PlacedHoles)
                {
                    double radius = hole.Diameter / 2.0;

                    // Круг отверстия
                    DrawingCanvas.Children.Add(new Ellipse
                    {
                        Width = radius * 2,
                        Height = radius * 2,
                        Stroke = Brushes.Red,
                        StrokeThickness = 2 / CanvasScale.ScaleX,
                        Fill = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0)), // Полупрозрачный красный
                        RenderTransform = new TranslateTransform(hole.X - radius, hole.Y - radius)
                    });

                    // Метка с диаметром
                    var label = new TextBlock
                    {
                        Text = $"⌀{hole.Diameter:0}",
                        FontSize = 12 / CanvasScale.ScaleX,
                        Foreground = Brushes.DarkRed,
                        FontWeight = FontWeights.Bold,
                        Background = Brushes.White
                    };
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(label, hole.X - label.DesiredSize.Width / 2);
                    Canvas.SetTop(label, hole.Y - label.DesiredSize.Height / 2);
                    DrawingCanvas.Children.Add(label);
                }
            }
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

        // ==========================================
        // ВИЗУАЛИЗАЦИЯ ТРУБНЫХ ДЕТАЛЕЙ
        // ==========================================
        private void DrawPipeView()
        {
            if (_currentPart.DisplayGeometry == null) return;

            var bounds = _currentPart.DisplayGeometry.Bounds;
            if (bounds.IsEmpty || bounds.Width < 0.1) return;

            double canvasW = 3000;
            double canvasH = 1500;

            // --- Масштаб сечения: целевой размер ~25% ширины Canvas ---
            double targetSectionPx = canvasW * 0.22;
            double maxDim = Math.Max(bounds.Width, bounds.Height);
            double sScale = targetSectionPx / maxDim;

            // Центр области сечения (левая часть)
            double secCX = canvasW * 0.2;
            double secCY = canvasH * 0.48;

            // Рисуем масштабированное сечение
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

            // --- Вид сбоку (справа) ---
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
                Height = Math.Max(sideH, 4), // Минимальная высота для видимости
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(25, 0, 120, 215))
            };
            Canvas.SetLeft(sideRect, sideX);
            Canvas.SetTop(sideRect, sideY);
            DrawingCanvas.Children.Add(sideRect);

            // --- Размерные линии ---
            double dimOffset = 30;
            double dimFontSize = 35;

            // Размер ширины сечения (снизу)
            double secLeft = secCX - bounds.Width * sScale / 2;
            double secRight = secCX + bounds.Width * sScale / 2;
            double secBottom = secCY + bounds.Height * sScale / 2;
            DrawDimLine(secLeft, secBottom + dimOffset, secRight, secBottom + dimOffset,
                        $"{bounds.Width:0}", dimFontSize, true);

            // Размер высоты сечения (справа от сечения)
            double secTop = secCY - bounds.Height * sScale / 2;
            DrawDimLine(secRight + dimOffset, secTop, secRight + dimOffset, secBottom,
                        $"{bounds.Height:0}", dimFontSize, false);

            // Размер длины (снизу от вида сбоку)
            DrawDimLine(sideX, sideY + sideH + dimOffset, sideX + sideW, sideY + sideH + dimOffset,
                        $"L = {_currentPart.Length:0}", dimFontSize, true);

            // Подпись типа сечения
            DrawCanvasLabel(GetPipeTypeName(_currentPart.PartType),
                            secCX, secTop - dimOffset * 2, dimFontSize, Brushes.DarkBlue);
        }

        // ==========================================
        // РАЗМЕРНЫЕ ЛИНИИ СО СТРЕЛКАМИ
        // ==========================================
        private void DrawDimLine(double x1, double y1, double x2, double y2,
                                 string text, double fontSize, bool horizontal)
        {
            double arrowSize = 12;
            var dimBrush = Brushes.DarkSlateGray;
            double stroke = 1.5;

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
                // Стрелки слева и справа
                DrawingCanvas.Children.Add(new Line { X1 = x1, Y1 = y1 - arrowSize / 2, X2 = x1, Y2 = y1 + arrowSize / 2, Stroke = dimBrush, StrokeThickness = stroke });
                DrawingCanvas.Children.Add(new Line { X1 = x2, Y1 = y2 - arrowSize / 2, X2 = x2, Y2 = y2 + arrowSize / 2, Stroke = dimBrush, StrokeThickness = stroke });

                var label = new TextBlock { Text = text, FontSize = fontSize, Foreground = dimBrush, FontWeight = FontWeights.Bold };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, (x1 + x2) / 2 - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, y1 + 5);
                DrawingCanvas.Children.Add(label);
            }
            else
            {
                // Стрелки сверху и снизу
                DrawingCanvas.Children.Add(new Line { X1 = x1 - arrowSize / 2, Y1 = y1, X2 = x1 + arrowSize / 2, Y2 = y1, Stroke = dimBrush, StrokeThickness = stroke });
                DrawingCanvas.Children.Add(new Line { X1 = x2 - arrowSize / 2, Y1 = y2, X2 = x2 + arrowSize / 2, Y2 = y2, Stroke = dimBrush, StrokeThickness = stroke });

                var label = new TextBlock { Text = text, FontSize = fontSize, Foreground = dimBrush, FontWeight = FontWeights.Bold };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, x1 + 5);
                Canvas.SetTop(label, (y1 + y2) / 2 - label.DesiredSize.Height / 2);
                DrawingCanvas.Children.Add(label);
            }
        }

        private void DrawCanvasLabel(string text, double x, double y, double fontSize, Brush color)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Foreground = color,
                FontWeight = FontWeights.Bold
            };
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
            _ => "Труба"
        };

        // ==========================================
        // ОБРАБОТЧИКИ МЫШИ И РИСОВАНИЯ
        // ==========================================

        private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 🔥 1. Режим размещения отверстий
            if (_isPlacingHoles && _isContourFinished)
            {
                var clickPoint = e.GetPosition(DrawingCanvas);
                clickPoint.X = Math.Clamp(clickPoint.X, 0, SheetWidthMm);
                clickPoint.Y = Math.Clamp(clickPoint.Y, 0, SheetHeightMm);

                PlaceHole(clickPoint);
                e.Handled = true;
                return;
            }

            // 🔥 Режим редактирования размеров
            if (_isEditMode && _isContourFinished)
            {
                var clickPoint = e.GetPosition(DrawingCanvas);
                int clickedSegment = GetClickedSegment(clickPoint);

                if (clickedSegment >= 0)
                {
                    _selectedSegmentIndex = clickedSegment;
                    double length = GetSegmentLength(clickedSegment);
                    SegmentSizeInput.Text = $"{length:0}";
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

            var mousePoint = e.GetPosition(DrawingCanvas);
            mousePoint.X = Math.Clamp(mousePoint.X, 0, SheetWidthMm);
            mousePoint.Y = Math.Clamp(mousePoint.Y, 0, SheetHeightMm);

            if (_customPoints.Count == 0)
            {
                _customPoints.Add(mousePoint);
            }
            else
            {
                var candidate = GetSnappedPoint(_customPoints.Last(), mousePoint);
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

        // ==========================================
        // ГЕОМЕТРИЯ И ВАЛИДАЦИЯ
        // ==========================================

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

            // Если погрешность по X мала, а по Y велика -> выравниваем по вертикали
            if (dx <= ClosingSnapToleranceMm && dy > ClosingSnapToleranceMm)
            {
                return new Point(first.X, last.Y);
            }
            // Если погрешность по Y мала, а по X велика -> выравниваем по горизонтали
            else if (dy <= ClosingSnapToleranceMm && dx > ClosingSnapToleranceMm)
            {
                return new Point(last.X, first.Y);
            }
            // Если обе погрешности малы -> точка практически совпадает с начальной
            else if (dx <= ClosingSnapToleranceMm && dy <= ClosingSnapToleranceMm)
            {
                return first;
            }

            // Если погрешность > 10 мм по обеим осям -> оставляем как есть (косая линия)
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
            var effectiveLast = GetSnappedClosingPoint(first, last); // 🔥 Проверяем выровненный вариант

            for (int i = 0; i < _customPoints.Count - 1; i++)
            {
                if (i == 0 || i == _customPoints.Count - 2) continue;

                if (DoSegmentsIntersect(effectiveLast, first, _customPoints[i], _customPoints[i + 1]))
                {
                    return false;
                }
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
            if (pointsForSegment.Count > 0)
            {
                pointsForSegment[pointsForSegment.Count - 1] = effectiveLast;
            }

            var figure = new PathFigure
            {
                StartPoint = first,
                IsClosed = true,
                IsFilled = true,
                Segments = { new PolyLineSegment(pointsForSegment, true) }
            };

            _currentPart.DisplayGeometry = new PathGeometry { Figures = { figure } };

            // 🔥 Округление до 2 знаков после запятой
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
            // Сброс режима редактирования
            _isEditMode = false;
            _selectedSegmentIndex = -1;
            EditModeToggle.IsChecked = false;
            EditModePanel.Visibility = Visibility.Collapsed;

            _customPoints.Clear();
            _isContourFinished = false;
            RedrawCanvas();
            UpdateCustomGeometry();
            UpdateFinishButtonState();
        }

        private void FinishDrawing_Click(object sender, RoutedEventArgs e)
        {
            if (!CanCloseContour()) { MessageBox.Show("Невозможно замкнуть контур без пересечений.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            _isContourFinished = true; RedrawCanvas(); UpdateFinishButtonState();

            // 🔥 Показываем панель редактирования размеров после завершения контура
            EditModePanel.Visibility = Visibility.Visible;
        }


        // ==========================================
        // РЕДАКТИРОВАНИЕ РАЗМЕРОВ ЛИНИЙ
        // ==========================================

        private void EditModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = EditModeToggle.IsChecked ?? false;
            _selectedSegmentIndex = -1; // Сбрасываем выбор при переключении режима

            if (_isEditMode)
            {
                SegmentSizeInput.IsEnabled = false; // Активируется только после выбора линии
                CurrentSizeText.Text = "Кликните по линии";
            }
            else
            {
                SegmentSizeInput.Text = "";
                CurrentSizeText.Text = "—";
            }

            RedrawCanvas(); // Перерисовываем, чтобы убрать/добавить метки размеров
        }

        private void SegmentSizeInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Просто обновляем UI, применение происходит по кнопке или Enter
        }

        private void SegmentSizeInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplySize_Click(sender, e);
            }
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

            // Обновляем геометрию и перерисовываем
            UpdateCustomGeometry();
            RedrawCanvas();

            // Сбрасываем выбор
            _selectedSegmentIndex = -1;
            SegmentSizeInput.Text = "";
            CurrentSizeText.Text = "Кликните по линии";
            SegmentSizeInput.IsEnabled = false;
        }

        // Определяет, по какой линии кликнул пользователь
        private int GetClickedSegment(Point clickPoint)
        {
            double minDistance = double.MaxValue;
            int closestSegment = -1;

            // Конвертируем допуск из экранных пикселей в миллиметры
            double toleranceMm = SegmentClickTolerancePx / CanvasScale.ScaleX;

            // Проверяем все сегменты (включая замыкающую линию)
            for (int i = 0; i < _customPoints.Count; i++)
            {
                Point p1 = _customPoints[i];
                Point p2 = (i < _customPoints.Count - 1) ? _customPoints[i + 1] : _customPoints[0]; // Замыкающая линия

                double distance = DistanceFromPointToSegment(clickPoint, p1, p2);

                if (distance < minDistance && distance <= toleranceMm)
                {
                    minDistance = distance;
                    closestSegment = i;
                }
            }

            return closestSegment;
        }

        // Вычисляет расстояние от точки до отрезка
        private double DistanceFromPointToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            if (dx == 0 && dy == 0) // Отрезок вырожден в точку
            {
                return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));
            }

            // Проекция точки на линию
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t)); // Ограничиваем пределами отрезка

            // Ближайшая точка на отрезке
            double closestX = a.X + t * dx;
            double closestY = a.Y + t * dy;

            return Math.Sqrt(Math.Pow(p.X - closestX, 2) + Math.Pow(p.Y - closestY, 2));
        }

        // Применяет новый размер к выбранному сегменту
        private void ApplyNewSizeToSegment(int segmentIndex, double newSize)
        {
            if (segmentIndex < 0 || segmentIndex >= _customPoints.Count) return;

            Point p1 = _customPoints[segmentIndex];
            Point p2 = (segmentIndex < _customPoints.Count - 1) ? _customPoints[segmentIndex + 1] : _customPoints[0];

            double currentLength = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            if (currentLength < 0.01) return; // Избегаем деления на ноль

            double scale = newSize / currentLength;

            // Определяем направление линии
            bool isHorizontal = Math.Abs(p1.Y - p2.Y) < Epsilon;
            bool isVertical = Math.Abs(p1.X - p2.X) < Epsilon;

            if (isHorizontal)
            {
                // Горизонтальная линия: меняем X конечной точки
                double deltaX = (p2.X - p1.X) * (scale - 1);

                // Сдвигаем конечную точку и все последующие
                for (int i = segmentIndex + 1; i < _customPoints.Count; i++)
                {
                    _customPoints[i] = new Point(_customPoints[i].X + deltaX, _customPoints[i].Y);
                }

                // Если это замыкающая линия (segmentIndex == _customPoints.Count - 1), сдвигаем первую точку
                if (segmentIndex == _customPoints.Count - 1)
                {
                    _customPoints[0] = new Point(_customPoints[0].X + deltaX, _customPoints[0].Y);
                }
            }
            else if (isVertical)
            {
                // Вертикальная линия: меняем Y конечной точки
                double deltaY = (p2.Y - p1.Y) * (scale - 1);

                // Сдвигаем конечную точку и все последующие
                for (int i = segmentIndex + 1; i < _customPoints.Count; i++)
                {
                    _customPoints[i] = new Point(_customPoints[i].X, _customPoints[i].Y + deltaY);
                }

                // Если это замыкающая линия, сдвигаем первую точку
                if (segmentIndex == _customPoints.Count - 1)
                {
                    _customPoints[0] = new Point(_customPoints[0].X, _customPoints[0].Y + deltaY);
                }
            }
            else
            {
                // Косая линия (не должна встречаться при ортогональном рисовании, но на всякий случай)
                MessageBox.Show("Редактирование косых линий не поддерживается.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Возвращает длину сегмента
        private double GetSegmentLength(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= _customPoints.Count) return 0;

            Point p1 = _customPoints[segmentIndex];
            Point p2 = (segmentIndex < _customPoints.Count - 1) ? _customPoints[segmentIndex + 1] : _customPoints[0];

            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        // ==========================================
        // СУЩЕСТВУЮЩАЯ БИЗНЕС-ЛОГИКА
        // ==========================================

        public List<Part> GetBatchedParts() => new(_batchBuffer);

        /// <summary>
        /// Возвращает информацию о гибах для всех деталей в буфере.
        /// Используется для автоматического заполнения карточек гибки.
        /// </summary>
        public Dictionary<Part, Dictionary<double, (int Count, double BendLength)>> GetBendsInfoForBatch()
        {
            return new Dictionary<Part, Dictionary<double, (int Count, double BendLength)>>(_batchBendsInfo);
        }

        private void AddHoleGroup_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPart.PartType == PartType.Custom) { MessageBox.Show("Отверстия для произвольных форм пока не поддерживаются.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information); return; }
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

        // Поля для режима отверстий
        private bool _isPlacingHoles = false;
        private const double MinEdgeDistanceMm = 3.0; // Минимальное расстояние от края отверстия до края детали (мм)

        private void HoleModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isPlacingHoles = HoleModeToggle.IsChecked ?? false;
            if (_isPlacingHoles)
            {
                // Отключаем режим редактирования размеров, если он был включен
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

        // Обработка клика для размещения отверстия
        private void PlaceHole(Point clickPoint)
        {
            if (!double.TryParse(HoleDiameterInput.Text, out double diameter) || diameter <= 0)
            {
                MessageBox.Show("Введите корректный диаметр отверстия.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 1. Проверка: точка внутри полигона?
            if (!IsPointInPolygon(clickPoint, _customPoints))
            {
                MessageBox.Show("Отверстие должно располагаться внутри контура детали.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Проверка: расстояние до краёв детали
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

            // Всё хорошо, добавляем отверстие
            _currentPart.PlacedHoles.Add(new PlacedHole { X = clickPoint.X, Y = clickPoint.Y, Diameter = diameter });
            RedrawCanvas();
        }

        // Алгоритм "Ray Casting" для проверки точки внутри полигона
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

        private void AddToBatch_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentPart.Title)) { MessageBox.Show("Укажите название", "Ошибка"); return; }
            if (_batchBuffer.Any(p => p.Title == _currentPart.Title)) { MessageBox.Show("Такое название уже есть", "Ошибка"); return; }
            if (_currentPart.Count <= 0) { MessageBox.Show("Укажите количество", "Ошибка"); return; }

            if (_currentPart.PartType != PartType.Custom)
            {
                var (isValid, error) = PartPreviewGenerator.ValidateHolesPlacement(_currentPart);
                if (!isValid && MessageBox.Show($"Проблемы с отверстиями:\n{error}\nПродолжить без них?", "Внимание", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                if (!isValid) _currentPart.HoleGroups.Clear();
            }

            // Синхронизация PlacedHoles -> HoleGroups для Custom
            if (_currentPart.PartType == PartType.Custom && _currentPart.PlacedHoles.Count > 0)
            {
                _currentPart.HoleGroups.Clear();
                var grouped = _currentPart.PlacedHoles.GroupBy(h => h.Diameter);
                foreach (var group in grouped)
                {
                    _currentPart.HoleGroups.Add(new HoleGroup(group.Key, group.Count()));
                }
                PartPreviewGenerator.EnsureDisplayGeometryWithHoles(_currentPart);
            }

            var clone = ClonePart(_currentPart);
            if (clone != null)
            {
                _batchBuffer.Add(clone);

                // 🔥 ДОБАВЛЕНО: Сохраняем информацию о гибах для этой детали
                var bendsInfo = CalculateBendsInfo();
                if (bendsInfo.Count > 0)
                {
                    _batchBendsInfo[clone] = bendsInfo;
                }

                _currentPart.HoleGroups.Clear();
                if (_currentPart.PartType == PartType.Custom) ClearDrawing_Click(sender, e);
                else UpdatePreview();
            }
        }

        private void FinishBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_batchBuffer.Count == 0) { MessageBox.Show("Список пуст", "Ошибка"); return; }

            UseAutoNesting = AutoNestingCheck.IsChecked ?? true;
            CustomSpacing = double.TryParse(SpacingInput.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double s) && s >= 0 ? s : 0;
            CustomClampZone = Clamp340Radio.IsChecked == true ? 340 :
                              Clamp160Radio.IsChecked == true ? 160 : 0;
            CustomCutLoss = double.TryParse(CutLossInput.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double cl) && cl >= 0 ? cl : 10;

            DialogResult = true;
            Close();
        }

        private void RemoveFromBatch_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is Part part)
            {
                _batchBuffer.Remove(part);

                // 🔥 ДОБАВЛЕНО: Удаляем информацию о гибах
                _batchBendsInfo.Remove(part);
            }
        }

        private static Part? ClonePart(Part s) => s.DisplayGeometry == null && s.PartType != PartType.Custom ? null : new Part
        {
            Title = s.Title,
            Count = s.Count,
            Metal = s.Metal,
            Destiny = s.Destiny,
            Width = s.Width,
            Height = s.Height,
            Length = s.Length,
            PartType = s.PartType,
            HoleGroups = new ObservableCollection<HoleGroup>(s.HoleGroups),
            DisplayGeometry = s.PartType == PartType.Custom ? PartPreviewGenerator.CloneGeometry(s.DisplayGeometry) : s.DisplayGeometry,
            PropsDict = new Dictionary<int, List<string>>(s.PropsDict)
        };

        // ==========================================
        // РАЗВЕРТКА ДЛЯ ГИБКИ
        // ==========================================

        // Параметры гибки
        private double _bendAngle = 90.0;
        private int _bendsCountX = 0;
        private int _bendsCountY = 0;
        private const double BendKFactor = 0.4;

        // Храним исходные (внешние) размеры, которые ввел пользователь
        private double _originalWidth = 0;
        private double _originalHeight = 0;
        private bool _isUpdatingFromBend = false;
        private bool _isResettingBends = false;

        /// <summary>
        /// Вычисляет компенсацию на один гиб для произвольного угла.
        /// BD = 2(R + T) × tan(α/2) − (π × α / 180) × (R + K × T)
        /// </summary>
        private double BendCompensation
        {
            get
            {
                double T = _currentPart.Destiny;
                double R = T; // Радиус равен толщине

                double angleRad = Math.PI * _bendAngle / 180.0;  // Угол в радианах
                double halfAngleTan = Math.Tan(angleRad / 2.0);   // tan(α/2)

                double bendAllowance = angleRad * (R + BendKFactor * T);  // BA
                double bendDeduction = 2 * (R + T) * halfAngleTan - bendAllowance;  // BD

                return bendDeduction;
            }
        }

        private void BendParameters_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isResettingBends) return;
            if (BendsCountXInput == null || BendsCountYInput == null ||
                BendInfoText == null || BendAngleInput == null) return;

            if (int.TryParse(BendsCountXInput.Text, out int bx) && bx >= 0)
                _bendsCountX = bx;
            if (int.TryParse(BendsCountYInput.Text, out int by) && by >= 0)
                _bendsCountY = by;

            // 🔥 Читаем угол гиба
            if (double.TryParse(BendAngleInput.Text, out double angle) && angle > 0 && angle < 180)
                _bendAngle = angle;

            UpdateBendInfo();
            RecalculatePartDimensions();
            RedrawCanvas();
        }

        /// <summary>
        /// Сбрасывает параметры гибки без запуска пересчёта
        /// </summary>
        private void ResetBendParameters()
        {
            _isResettingBends = true;
            try
            {
                _bendAngle = 90.0;
                _bendsCountX = 0;
                _bendsCountY = 0;
                _originalWidth = 0;
                _originalHeight = 0;

                if (BendAngleInput != null) BendAngleInput.Text = "90";
                if (BendsCountXInput != null) BendsCountXInput.Text = "0";
                if (BendsCountYInput != null) BendsCountYInput.Text = "0";
                if (BendInfoText != null) BendInfoText.Text = "Линии сгиба не заданы";
            }
            finally
            {
                _isResettingBends = false;
            }
        }

        /// <summary>
        /// Обновляет информационную строку о гибке
        /// </summary>
        private void UpdateBendInfo()
        {
            if (BendInfoText == null) return;

            if (_bendsCountX == 0 && _bendsCountY == 0)
            {
                BendInfoText.Text = "Линии гиба не заданы";
                return;
            }

            BendInfoText.Text = $"Вычет на 1 гиб: {BendCompensation:F2} мм";
        }

        /// <summary>
        /// Пересчитывает размеры детали (развертки) с учетом компенсации на гибку
        /// </summary>
        private void RecalculatePartDimensions()
        {
            if (_currentPart == null || _isUpdatingFromBend) return;

            _isUpdatingFromBend = true;

            try
            {
                // Сохраняем исходные размеры при первом расчете
                if (_originalWidth == 0 && _originalHeight == 0)
                {
                    _originalWidth = _currentPart.Width;
                    _originalHeight = _currentPart.Height;
                }

                double compensation = BendCompensation;

                // Рассчитываем новые размеры развертки
                double newWidth = _bendsCountX == 0
                    ? _originalWidth
                    : _originalWidth - (_bendsCountX * compensation);

                double newHeight = _bendsCountY == 0
                    ? _originalHeight
                    : _originalHeight - (_bendsCountY * compensation);

                _currentPart.Width = Math.Max(0.1, Math.Ceiling(newWidth));
                _currentPart.Height = Math.Max(0.1, Math.Ceiling(newHeight));

                UpdatePreview();
            }
            finally
            {
                _isUpdatingFromBend = false;
            }
        }

        /// <summary>
        /// Рисует линии гибов на Canvas
        /// </summary>
        private void DrawBendLines(double strokeThickness)
        {
            if (DrawingCanvas == null || _currentPart == null) return;
            if (_currentPart.PartType != PartType.Rectangle) return;
            if (_bendsCountX == 0 && _bendsCountY == 0) return;

            // Используем ИСХОДНЫЕ размеры для расчета позиций линий сгиба
            double width = _originalWidth > 0 ? _originalWidth : _currentPart.Width;
            double height = _originalHeight > 0 ? _originalHeight : _currentPart.Height;

            if (width <= 0 || height <= 0) return;

            double offsetX = (SheetWidthMm / 2) - (width / 2);
            double offsetY = (SheetHeightMm / 2) - (height / 2);

            var bendBrush = Brushes.DarkOrange;
            var dashArray = new DoubleCollection { 5, 3 };
            double lineThickness = strokeThickness * 0.8;

            // Вертикальные линии сгиба
            if (_bendsCountX > 0)
            {
                double segmentWidth = width / (_bendsCountX + 1);
                for (int i = 1; i <= _bendsCountX; i++)
                {
                    double x = offsetX + (segmentWidth * i);
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

            // Горизонтальные линии сгиба
            if (_bendsCountY > 0)
            {
                double segmentHeight = height / (_bendsCountY + 1);
                for (int i = 1; i <= _bendsCountY; i++)
                {
                    double y = offsetY + (segmentHeight * i);
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

        /// <summary>
        /// Вычисляет информацию о гибах для текущей детали.
        /// Ключ — размер полки гиба (мм), однозначно определяющий однотипные гибы.
        /// Значение — кортеж (количество однотипных гибов, длина гиба в мм).
        /// 
        /// ВАЖНО: Длина гиба берется из размеров РАЗВЕРТКИ, а не из исходных размеров,
        /// так как линии сгиба на развертке имеют увеличенную длину с учетом компенсации.
        /// </summary>
        private Dictionary<double, (int Count, double BendLength)> CalculateBendsInfo()
        {
            var result = new Dictionary<double, (int Count, double BendLength)>();

            if (_originalWidth <= 0 || _originalHeight <= 0)
                return result;

            // 🔥 ИСПРАВЛЕНО: Используем размеры РАЗВЕРТКИ, а не исходные размеры
            double unfoldedWidth = _currentPart.Width;
            double unfoldedHeight = _currentPart.Height;

            // Горизонтальные гибы (вертикальные линии сгиба, полка вдоль X)
            if (_bendsCountX > 0)
            {
                double shelfSize = Math.Round(_originalWidth / (_bendsCountX + 1), 1);
                double bendLength = Math.Round(unfoldedHeight, 1); // 🔥 Длина гиба = высота РАЗВЕРТКИ
                result[shelfSize] = (_bendsCountX, bendLength);
            }

            // Вертикальные гибы (горизонтальные линии сгиба, полка вдоль Y)
            if (_bendsCountY > 0)
            {
                double shelfSize = Math.Round(_originalHeight / (_bendsCountY + 1), 1);
                double bendLength = Math.Round(unfoldedWidth, 1); // 🔥 Длина гиба = ширина РАЗВЕРТКИ

                // Если полка совпадает с горизонтальной (редкий случай), объединяем
                if (result.ContainsKey(shelfSize))
                {
                    var existing = result[shelfSize];
                    result[shelfSize] = (existing.Count + _bendsCountY, bendLength);
                }
                else
                {
                    result[shelfSize] = (_bendsCountY, bendLength);
                }
            }

            return result;
        }
    }
}