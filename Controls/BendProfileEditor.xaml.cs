using Metal_Code.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code.Controls
{
    public partial class BendProfileEditor : UserControl
    {
        public BendProfileDrawing Profile { get; } = new();

        /// <summary>
        /// Заголовок профиля, отображаемый в панели инструментов.
        /// Задаётся извне: "По ширине" или "По высоте".
        /// </summary>
        public string ProfileTitle
        {
            get => (string)GetValue(ProfileTitleProperty);
            set => SetValue(ProfileTitleProperty, value);
        }

        public static readonly DependencyProperty ProfileTitleProperty =
            DependencyProperty.Register(
                nameof(ProfileTitle),
                typeof(string),
                typeof(BendProfileEditor),
                new PropertyMetadata("Профиль"));

        private bool _isEditMode = false;
        private int _selectedSegmentIndex = -1;
        private Point? _previewPoint;

        /// <summary>
        /// Текущий масштаб отображения (сколько пикселей = 1 мм профиля).
        /// Пересчитывается автоматически, чтобы профиль влезал в канвас.
        /// </summary>
        private double _displayScale = 1.0;
        private bool _drawingScaleInitialized = false;

        /// <summary>Смещение начала координат профиля на канвасе (в пикселях)</summary>
        private Point _originOffset = new(20, 20);

        private const double SegmentClickTolerancePx = 15;
        private const double MinSegmentLengthMm = 5.0; // минимальная длина сегмента

        public event Action? ProfileChanged;

        public BendProfileEditor()
        {
            InitializeComponent();
            Profile.Points.CollectionChanged += (_, _) =>
            {
                RecalculateDisplayScale();
                Redraw();
            };
            ProfileCanvas.SizeChanged += (_, _) => RecalculateDisplayScale();
        }

        public bool HasProfile => Profile.Points.Count >= 2;

        // ─────────────────────────────────────────────────────
        //  МАСШТАБИРОВАНИЕ
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Пересчитывает масштаб так, чтобы весь профиль влезал в канвас.
        /// </summary>
        private void RecalculateDisplayScale()
        {
            if (ProfileCanvas == null) return;
            if (ProfileCanvas.ActualWidth <= 0 || ProfileCanvas.ActualHeight <= 0) return;

            // Завершённый профиль всегда вписываем в окно
            if (Profile.IsFinished)
            {
                FitToView();
                return;
            }

            // Во время рисования масштаб фиксируется ОДИН раз и больше не меняется,
            // чтобы профиль не «растягивался» при добавлении каждой новой точки.
            if (!_drawingScaleInitialized)
            {
                EstablishDrawingScale();
                _drawingScaleInitialized = true;
            }
        }

        /// <summary>
        /// Задаёт стабильный масштаб для процесса рисования.
        /// Рабочая область: ~450 мм в ширину и ~180 мм в высоту.
        /// </summary>
        private void EstablishDrawingScale()
        {
            const double padding = 20;
            double availW = Math.Max(ProfileCanvas.ActualWidth - padding * 2, 10);
            double availH = Math.Max(ProfileCanvas.ActualHeight - padding * 2, 10);

            _displayScale = Math.Min(availW / 450.0, availH / 180.0);
            _displayScale = Math.Clamp(_displayScale, 0.05, 3.0);
            _originOffset = new Point(padding, padding);
        }

        /// <summary>
        /// Вписывает готовый профиль в канвас (используется после завершения и по кнопке 🔍).
        /// </summary>
        private void FitToView()
        {
            if (Profile.Points.Count < 2) return;
            if (ProfileCanvas.ActualWidth <= 0 || ProfileCanvas.ActualHeight <= 0) return;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in Profile.Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            double profileW = maxX - minX;
            double profileH = maxY - minY;
            if (profileW < 0.5 && profileH < 0.5) return;

            const double padding = 15;
            double availW = Math.Max(ProfileCanvas.ActualWidth - padding * 2, 10);
            double availH = Math.Max(ProfileCanvas.ActualHeight - padding * 2, 10);

            double scaleX = profileW > 0.5 ? availW / profileW : double.MaxValue;
            double scaleY = profileH > 0.5 ? availH / profileH : double.MaxValue;
            _displayScale = Math.Min(scaleX, scaleY);
            _displayScale = Math.Clamp(_displayScale, 0.05, 5.0);

            _originOffset = new Point(
                padding + (availW - profileW * _displayScale) / 2 - minX * _displayScale,
                padding + (availH - profileH * _displayScale) / 2 - minY * _displayScale);
        }

        /// <summary>Преобразует координаты профиля (мм) в пиксели канваса</summary>
        private Point ProfileToCanvas(Point p)
            => new(_originOffset.X + p.X * _displayScale,
                   _originOffset.Y + p.Y * _displayScale);

        /// <summary>Преобразует координаты пикселей канваса в мм профиля</summary>
        private Point CanvasToProfile(Point p)
            => new((p.X - _originOffset.X) / _displayScale,
                   (p.Y - _originOffset.Y) / _displayScale);

        // ─────────────────────────────────────────────────────
        //  РИСОВАНИЕ
        // ─────────────────────────────────────────────────────

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isEditMode && Profile.IsFinished)
            {
                var clickPoint = e.GetPosition(ProfileCanvas);
                int clicked = GetClickedSegment(clickPoint);
                if (clicked >= 0)
                {
                    _selectedSegmentIndex = clicked;
                    SegmentSizeInput.Text = $"{Profile.GetSegmentLength(clicked):0}";
                    UpdateSizeInputVisibility();
                    SegmentSizeInput.Focus();
                    SegmentSizeInput.SelectAll();
                }
                else
                {
                    _selectedSegmentIndex = -1;
                    SegmentSizeInput.Text = "";
                    UpdateSizeInputVisibility();
                }
                Redraw();
                return;
            }

            var clickPx = e.GetPosition(ProfileCanvas);
            var clickMm = CanvasToProfile(clickPx);

            // Привязка к 90°
            if (Profile.Points.Count > 0)
                clickMm = SnapTo90(Profile.Points[^1], clickMm);

            // Проверка минимальной длины сегмента
            if (Profile.Points.Count > 0)
            {
                var prev = Profile.Points[^1];
                double dist = Math.Sqrt(Math.Pow(clickMm.X - prev.X, 2) + Math.Pow(clickMm.Y - prev.Y, 2));
                if (dist < MinSegmentLengthMm) return; // слишком короткий сегмент
            }

            Profile.Points.Add(clickMm);
            UpdateInfo();
            ProfileChanged?.Invoke();
            e.Handled = true;
        }

        /// <summary>ПКМ = завершить профиль</summary>
        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!Profile.IsFinished && Profile.Points.Count >= 2)
            {
                FinishProfile();
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (Profile.IsFinished || Profile.Points.Count == 0)
            {
                _previewPoint = null;
                return;
            }

            var mousePx = e.GetPosition(ProfileCanvas);
            var mouseMm = CanvasToProfile(mousePx);
            _previewPoint = SnapTo90(Profile.Points[^1], mouseMm);
            Redraw();
        }

        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _previewPoint = null;
            Redraw();
        }

        private Point SnapTo90(Point prev, Point mouse)
        {
            double dx = Math.Abs(mouse.X - prev.X);
            double dy = Math.Abs(mouse.Y - prev.Y);
            if (dx < 3 && dy < 3) return prev;
            return dx > dy ? new Point(mouse.X, prev.Y) : new Point(prev.X, mouse.Y);
        }

        // ─────────────────────────────────────────────────────
        //  ЗАВЕРШЕНИЕ ПРОФИЛЯ
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Поле ввода размера видно только в режиме размеров и только когда выбран сегмент.
        /// </summary>
        private void UpdateSizeInputVisibility()
        {
            bool show = _isEditMode && _selectedSegmentIndex >= 0;
            if (SegmentSizeInput != null)
                SegmentSizeInput.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        public void FinishProfile()
        {
            if (Profile.Points.Count < 2) return;
            Profile.IsFinished = true;
            _previewPoint = null;

            // Режим размеров включается автоматически
            _isEditMode = true;
            _selectedSegmentIndex = -1;
            UpdateSizeInputVisibility();

            RecalculateDisplayScale();
            Redraw();
            UpdateInfo();
            ProfileChanged?.Invoke();
        }

        // ─────────────────────────────────────────────────────
        //  ОТРИСОВКА
        // ─────────────────────────────────────────────────────

        private void Redraw()
        {
            ProfileCanvas.Children.Clear();
            if (Profile.Points.Count == 0) return;

            double strokeThickness = 2;
            double markerSize = 6;

            // Сетка-подсказка (лёгкая, для ориентира)
            DrawGrid();

            // Превью-линия (в координатах канваса)
            if (_previewPoint.HasValue && !Profile.IsFinished && Profile.Points.Count > 0)
            {
                var lastPx = ProfileToCanvas(Profile.Points[^1]);
                var previewPx = ProfileToCanvas(_previewPoint.Value);
                ProfileCanvas.Children.Add(new Line
                {
                    X1 = lastPx.X,
                    Y1 = lastPx.Y,
                    X2 = previewPx.X,
                    Y2 = previewPx.Y,
                    Stroke = Brushes.DodgerBlue,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                });
            }

            // Линии сегментов
            for (int i = 0; i < Profile.Points.Count - 1; i++)
            {
                var p1Px = ProfileToCanvas(Profile.Points[i]);
                var p2Px = ProfileToCanvas(Profile.Points[i + 1]);
                bool isSelected = (_isEditMode && i == _selectedSegmentIndex);

                ProfileCanvas.Children.Add(new Line
                {
                    X1 = p1Px.X,
                    Y1 = p1Px.Y,
                    X2 = p2Px.X,
                    Y2 = p2Px.Y,
                    Stroke = isSelected ? Brushes.Red : Brushes.Black,
                    StrokeThickness = isSelected ? strokeThickness * 1.5 : strokeThickness
                });
            }

            // Маркеры вершин и гибов
            for (int i = 0; i < Profile.Points.Count; i++)
            {
                var pPx = ProfileToCanvas(Profile.Points[i]);
                bool isBendVertex = (i > 0 && i < Profile.Points.Count - 1);

                if (isBendVertex)
                {
                    // Маркер гиба (оранжевый круг)
                    var bendMarker = new Ellipse
                    {
                        Width = 14,
                        Height = 14,
                        Stroke = Brushes.DarkOrange,
                        StrokeThickness = 2,
                        Fill = new SolidColorBrush(Color.FromArgb(60, 255, 165, 0))
                    };
                    Canvas.SetLeft(bendMarker, pPx.X - 7);
                    Canvas.SetTop(bendMarker, pPx.Y - 7);
                    ProfileCanvas.Children.Add(bendMarker);
                }
                else
                {
                    // Концевая точка
                    var dot = new Ellipse
                    {
                        Width = markerSize,
                        Height = markerSize,
                        Fill = Brushes.DodgerBlue,
                        Stroke = Brushes.DodgerBlue,
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(dot, pPx.X - markerSize / 2);
                    Canvas.SetTop(dot, pPx.Y - markerSize / 2);
                    ProfileCanvas.Children.Add(dot);
                }
            }

            // Размеры сегментов (в режиме редактирования или после завершения)
            if (_isEditMode || Profile.IsFinished)
            {
                for (int i = 0; i < Profile.Points.Count - 1; i++)
                {
                    var p1Px = ProfileToCanvas(Profile.Points[i]);
                    var p2Px = ProfileToCanvas(Profile.Points[i + 1]);
                    double len = Profile.GetSegmentLength(i);
                    bool isHoriz = Profile.IsSegmentHorizontal(i);

                    double midX = (p1Px.X + p2Px.X) / 2;
                    double midY = (p1Px.Y + p2Px.Y) / 2;

                    double offsetX = isHoriz ? 0 : 18;
                    double offsetY = isHoriz ? -18 : 0;

                    bool isSelected = (_isEditMode && i == _selectedSegmentIndex);

                    var label = new TextBlock
                    {
                        Text = $"{len:0}",
                        FontSize = 11,
                        Foreground = isSelected ? Brushes.Red : Brushes.DarkBlue,
                        FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                        Background = Brushes.White,
                        Padding = new Thickness(2, 0, 2, 0)
                    };
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(label, midX + offsetX - label.DesiredSize.Width / 2);
                    Canvas.SetTop(label, midY + offsetY - label.DesiredSize.Height / 2);
                    ProfileCanvas.Children.Add(label);
                }
            }
        }

        /// <summary>Рисует лёгкую сетку для ориентира</summary>
        private void DrawGrid()
        {
            if (ProfileCanvas.ActualWidth <= 0 || ProfileCanvas.ActualHeight <= 0) return;

            double gridStepMm = 10; // шаг сетки в мм
            double gridStepPx = gridStepMm * _displayScale;

            // Не рисуем слишком мелкую сетку
            if (gridStepPx < 8) gridStepMm = 50;
            if (gridStepPx < 4) return;

            var gridBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));

            // Вертикальные линии
            double startX = _originOffset.X % (gridStepMm * _displayScale);
            if (startX < 0) startX += gridStepMm * _displayScale;
            for (double x = startX; x < ProfileCanvas.ActualWidth; x += gridStepMm * _displayScale)
            {
                ProfileCanvas.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = ProfileCanvas.ActualHeight,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                });
            }

            // Горизонтальные линии
            double startY = _originOffset.Y % (gridStepMm * _displayScale);
            if (startY < 0) startY += gridStepMm * _displayScale;
            for (double y = startY; y < ProfileCanvas.ActualHeight; y += gridStepMm * _displayScale)
            {
                ProfileCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = ProfileCanvas.ActualWidth,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                });
            }
        }

        // ─────────────────────────────────────────────────────
        //  РЕДАКТИРОВАНИЕ РАЗМЕРОВ
        // ─────────────────────────────────────────────────────

        private void SegmentSizeInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedSegmentIndex < 0) return;
            if (!double.TryParse(SegmentSizeInput.Text.Replace(',', '.'), out double newSize)) return;
            if (newSize <= 0) return;

            ApplyNewSizeToSegment(_selectedSegmentIndex, newSize);
            RecalculateDisplayScale();
            Redraw();
            ProfileChanged?.Invoke();
        }

        private void ApplyNewSizeToSegment(int segmentIndex, double newSize)
        {
            if (segmentIndex < 0 || segmentIndex >= Profile.Points.Count - 1) return;

            Point p1 = Profile.Points[segmentIndex];
            Point p2 = Profile.Points[segmentIndex + 1];
            double currentLength = Profile.GetSegmentLength(segmentIndex);
            if (currentLength < 0.01) return;

            double scale = newSize / currentLength;
            bool isHorizontal = Profile.IsSegmentHorizontal(segmentIndex);

            if (isHorizontal)
            {
                double deltaX = (p2.X - p1.X) * (scale - 1);
                for (int i = segmentIndex + 1; i < Profile.Points.Count; i++)
                    Profile.Points[i] = new Point(Profile.Points[i].X + deltaX, Profile.Points[i].Y);
            }
            else
            {
                double deltaY = (p2.Y - p1.Y) * (scale - 1);
                for (int i = segmentIndex + 1; i < Profile.Points.Count; i++)
                    Profile.Points[i] = new Point(Profile.Points[i].X, Profile.Points[i].Y + deltaY);
            }
        }

        private int GetClickedSegment(Point clickPointPx)
        {
            double minDistance = double.MaxValue;
            int closest = -1;

            for (int i = 0; i < Profile.Points.Count - 1; i++)
            {
                var p1Px = ProfileToCanvas(Profile.Points[i]);
                var p2Px = ProfileToCanvas(Profile.Points[i + 1]);
                double distance = DistanceFromPointToSegment(clickPointPx, p1Px, p2Px);
                if (distance < minDistance && distance <= SegmentClickTolerancePx)
                {
                    minDistance = distance;
                    closest = i;
                }
            }
            return closest;
        }

        private double DistanceFromPointToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            if (dx == 0 && dy == 0) return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));
            double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy)));
            double closestX = a.X + t * dx;
            double closestY = a.Y + t * dy;
            return Math.Sqrt(Math.Pow(p.X - closestX, 2) + Math.Pow(p.Y - closestY, 2));
        }

        // ─────────────────────────────────────────────────────
        //  КНОПКИ
        // ─────────────────────────────────────────────────────

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _drawingScaleInitialized = false;
            _isEditMode = false;
            _selectedSegmentIndex = -1;

            Profile.Clear();
            SegmentSizeInput.Text = "";
            UpdateSizeInputVisibility();

            RecalculateDisplayScale();
            Redraw();
            UpdateInfo();
            ProfileChanged?.Invoke();
        }

        private void UpdateInfo()
        {
            if (Profile.Points.Count < 2)
            {
                InfoText.Text = "ЛКМ — точка | ПКМ — завершить";
                return;
            }

            if (Profile.IsFinished)
            {
                InfoText.Text = $"Сегментов: {Profile.SegmentsCount} | Гибов: {Profile.BendsCount} | Клик на сегмент — изменить размер";
            }
            else
            {
                InfoText.Text = $"Сегментов: {Profile.SegmentsCount} | ЛКМ — точка | ПКМ — завершить";
            }
        }
    }
}