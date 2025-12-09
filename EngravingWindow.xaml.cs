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

        private double? fontSizeOverride = null;
        public double? FontSizeOverride
        {
            get => fontSizeOverride;
            set
            {
                fontSizeOverride = value > 10 ? value : null;
                OnPropertyChanged(nameof(FontSizeOverride));
                UpdateEngravingPreview();
            }
        }

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

        private readonly EngravingMode _mode;

        public EngravingWindow(EngravingMode mode, TechItem? techItem = null)
        {
            InitializeComponent();
            DataContext = this;
            _mode = mode;
            TargetTechItem = techItem;

            if (_mode == EngravingMode.SingleItem && TargetTechItem != null)
            {
                TextMarking = TargetTechItem.TextMarking ?? "";
            }

            InitializeUI();

            // Запускаем предпросмотр после загрузки
            PreviewCanvas.Loaded += (s, e) => UpdateEngravingPreview();
        }

        private void InitializeUI()
        {
            switch (_mode)
            {
                case EngravingMode.SingleItem:
                    TextEngraving.IsEnabled = true;
                    TextEngraving.Visibility = Visibility.Visible;
                    break;

                case EngravingMode.BatchPreview:
                    TextEngraving.IsEnabled = false;
                    TextEngraving.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void UpdateEngravingPreview()
        {
            PreviewCanvas.Children.Clear();

            string previewText = _mode switch
            {
                EngravingMode.SingleItem => TextMarking,
                EngravingMode.BatchPreview => SelectedFont ?? "Preview",
                _ => ""
            };

            if (string.IsNullOrWhiteSpace(previewText))
            {
                PreviewCanvas.Width = 120;
                PreviewCanvas.Height = 60;
                return;
            }

            try
            {
                // Используем фиксированный размер шрифта или override
                double fontSize = 20;

                // Генерируем контуры
                var origin = new Point(0, 0);
                var contours = Engraving.TextToPathGeometries(previewText, SelectedFont, fontSize, origin);

                // Находим bounding box
                Rect textBounds = new();
                foreach (var contour in contours)
                {
                    foreach (var pt in contour)
                        textBounds.Union(new Rect(pt, new Size(1, 1)));
                }

                if (textBounds.IsEmpty) return;

                // Добавим отступы (например, 10 пикселей)
                double margin = 10;
                double canvasWidth = textBounds.Width + margin;
                double canvasHeight = textBounds.Height + margin;

                // Устанавливаем размер Canvas
                PreviewCanvas.Width = Math.Max(canvasWidth, 120);
                PreviewCanvas.Height = Math.Max(canvasHeight, 60);

                // Центрируем текст в Canvas
                double offsetX = (PreviewCanvas.Width - textBounds.Width) / 2.0 - textBounds.Left;
                double offsetY = (PreviewCanvas.Height - textBounds.Height) / 2.0 - textBounds.Top;

                foreach (var contour in contours)
                {
                    var points = new PointCollection();
                    foreach (var pt in contour)
                    {
                        // Смещаем, чтобы текст был по центру
                        double x = pt.X + offsetX;
                        double y = pt.Y + offsetY; // без инверсии!
                        if (!double.IsFinite(x) || !double.IsFinite(y)) continue;
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
            catch { }
        }

        // ===== ОБРАБОТЧИКИ КНОПОК =====
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void ApplyButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    }


    public enum EngravingMode
    {
        SingleItem,   // Редактирование текста для одной детали
        BatchPreview  // Только выбор шрифта/размера, текст = имя шрифта
    }
}