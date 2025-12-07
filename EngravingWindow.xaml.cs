using ACadSharp.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    public partial class EngravingWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string textMarking = string.Empty;
        public string TextMarking
        {
            get => textMarking;
            set
            {
                if (textMarking != value)
                {
                    textMarking = value;
                    OnPropertyChanged(nameof(TextMarking));
                    UpdateEngravingPreview();
                }
            }
        }

        private string selectedFont = "Danger";
        public string SelectedFont
        {
            get => selectedFont;
            set
            {
                if (selectedFont != value)
                {
                    selectedFont = value;
                    OnPropertyChanged(nameof(SelectedFont));
                    UpdateEngravingPreview();
                }
            }
        }

        public List<string> FontList { get; } = new()
        {
            "Danger",
            "GOST type A",
            "GOST type B"
        };

        private TechItem? targetTechItem;
        public TechItem? TargetTechItem
        {
            get => targetTechItem;
            set
            {
                targetTechItem = value;
                OnPropertyChanged(nameof(TargetTechItem));
            }
        }

        public EngravingWindow(TechItem techItem)
        {
            InitializeComponent();
            DataContext = this;

            TargetTechItem = techItem;

            // Запускаем предпросмотр после загрузки
            PreviewCanvas.Loaded += (s, e) => UpdateEngravingPreview();
        }

        private void UpdateEngravingPreview()
        {
            PreviewCanvas.Children.Clear();

            if (TargetTechItem == null || string.IsNullOrWhiteSpace(TextMarking))
                return;

            try
            {
                // 1. ПОЛУЧАЕМ DXF и габариты детали
                using var reader = new DxfReader(TargetTechItem.PathToModel);
                var dxf = reader.Read();
                var (partBoundsDxf, _, _) = MainWindow.GetDrawingBounds(dxf);

                // 2. РАЗМЕР CANVAS
                double canvasWidth = Math.Max(PreviewCanvas.ActualWidth, 1);
                double canvasHeight = Math.Max(PreviewCanvas.ActualHeight, 1);

                // 3. ВЫЧИСЛЯЕМ ОПТИМАЛЬНЫЙ РАЗМЕР ШРИФТА И ПОЗИЦИЮ
                double fontSize = Engraving.CalculateOptimalFontSize(TextMarking, SelectedFont, partBoundsDxf, 20, 5);
                Point originDxf = Engraving.GetCenteredTextPosition(TextMarking, SelectedFont, fontSize, partBoundsDxf);
                var contours = Engraving.TextToPathGeometries(TextMarking, SelectedFont, fontSize, originDxf);

                // 4. ВЫЧИСЛЯЕМ МАСШТАБ ДЛЯ ТЕКСТА (чтобы он заполнил холст)
                // Сначала найдём bounding box текста в DXF-координатах
                Rect textBounds = new();
                foreach (var contour in contours)
                {
                    foreach (var pt in contour)
                    {
                        textBounds.Union(new Rect(pt, new Size(1, 1)));
                    }
                }

                if (textBounds.IsEmpty)
                    return;

                // Масштабируем текст, чтобы он занял ~90% холста
                double scaleX = canvasWidth / textBounds.Width;
                double scaleY = canvasHeight / textBounds.Height;
                double scale = Math.Min(scaleX, scaleY) * 0.9;

                // Центрируем текст в Canvas
                double textCenterX = textBounds.Left + textBounds.Width / 2.0;
                double textCenterY = textBounds.Top + textBounds.Height / 2.0;
                double canvasCenterX = canvasWidth / 2.0;
                double canvasCenterY = canvasHeight / 2.0;

                // 5. РИСУЕМ КАЖДУЮ КОНТУРНУЮ ЛОМАНУЮ
                foreach (var contour in contours)
                {
                    var points = new PointCollection();
                    foreach (var pt in contour)
                    {
                        // Переносим точку из DXF → Canvas с центрированием и масштабированием
                        double x = canvasCenterX + (pt.X - textCenterX) * scale;
                        double y = canvasCenterY - (pt.Y - textCenterY) * scale; // инверсия Y
                        points.Add(new Point(x, y));
                    }

                    if (points.Count >= 2)
                    {
                        PreviewCanvas.Children.Add(new Polyline
                        {
                            Points = points,
                            Stroke = Brushes.Green,
                            StrokeThickness = 1
                        });
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки рендеринга
            }
        }

        // Вспомогательный метод: преобразование из DXF → Canvas
        private Point DxfToCanvas(double x, double y, double dxfCenterX, double dxfCenterY, double scale, double canvasCenterX, double canvasCenterY)
        {
            double canvasX = canvasCenterX + (x - dxfCenterX) * scale;
            double canvasY = canvasCenterY - (y - dxfCenterY) * scale; // инверсия Y: DXF (вверх) → WPF (вниз)
            return new Point(canvasX, canvasY);
        }

        // ===== ОБРАБОТЧИКИ КНОПОК =====
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void ApplyButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}