using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Point = System.Windows.Point;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для DetailDataWindow.xaml
    /// </summary>
    public partial class DetailDataWindow : Window
    {
        public Detail Detail { get; set; }
        public TypeDetailControl Billet { get; set; }
        public ObservableCollection<DetailData> Details { get; set; } = new();

        public DetailDataWindow(Detail detail, TypeDetailControl type)
        {
            InitializeComponent();
            Detail = detail;
            Billet = type;
            DataContext = this;
        }

        private void DetailDataWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BilletType.Text = Billet.TypeDetailDrop.Text;

            if (Billet.TypeDetailDrop.Text == "Лист металла")
            {
                foreach (Work w in MainWindow.M.Works)
                    if (w.Name == "Лазерная резка")
                    {
                        Billet.WorkControls[0].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
            else
            {
                foreach (Work w in MainWindow.M.Works)
                    if (w.Name == "Труборез")
                    {
                        Billet.WorkControls[0].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
        }

        private void Add_DetailData(object sender, RoutedEventArgs e) { Add_DetailData(); }
        private void Add_DetailData()
        {
            if (Billet.TypeDetailDrop.Text == "Лист металла") Details.Add(new() { Number = Details.Count + 1, IsLaser = true, IsPipe = false });
            else Details.Add(new() { Number = Details.Count + 1, IsLaser = false, IsPipe = true });
        }

        private void Accept(object sender, RoutedEventArgs e)
        {
            //получаем количество заготовок
            if (Billet.TypeDetailDrop.Text == "Лист металла") Billet.Count = SheetsCalculate();
            else
            {
                Billet.Count = TubesCalculate();

                //в случае трубы заодно устанавливаем количество погонных метров
                if (Billet.WorkControls[0].workType is PipeControl pipe)
                    pipe.SetMold($"{Billet.L * Billet.Count * 0.95f / 1000}");
            }

            //получаем путь резки и количество проколов в базовом виде
            if (Billet.WorkControls.Count > 0 && Billet.WorkControls[0].workType is ICut cut)
            {
                cut.Way = WayCalculate();
                cut.Pinhole = Details.Sum(d => d.Count) * 2 * Detail.Count;
            }

            FocusMainWindow();
        }

        private int SheetsCalculate()
        {
            if (Details.Count == 0) return 0;

            return (int)Math.Ceiling(Details.Sum(d => (d.Width + 10) * (d.Height + 10) * d.Count)
                        * Detail.Count / (Billet.A * Billet.B));
        }

        private int TubesCalculate()
        {
            if (Details.Count == 0) return 0;

            return (int)Math.Ceiling(Details.Sum(d => (d.Length + 20) * d.Count)
                        * Detail.Count / Billet.L);
        }

        private float WayCalculate()
        {
            if (Billet.Count == 0) return 0;

            return (float)Math.Ceiling(Details.Sum(d => (d.Width + d.Height + 10) * d.Count)
                        * Detail.Count / 500);      
        }

        private void Cancel(object sender, RoutedEventArgs e) { FocusMainWindow(); }

        private void FocusMainWindow(object sender, EventArgs e) { FocusMainWindow(); }

        private void FocusMainWindow()
        {
            Hide();
            MainWindow.M.IsEnabled = true;
        }

        private void Load_Model(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "All files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames.Length > 0)
            {
                RenderDxf(openFileDialog.FileNames);
            }
            else MainWindow.M.StatusBegin($"Не выбрано ни одного файла");
        }

        private void RenderDxf(string[] paths)
        {
            foreach (string path in paths)
            {
                if (Path.GetExtension(path) != ".dxf") continue;

                var reader = new DxfReader(path);
                CadDocument dxf = reader.Read();

                // 1. Найдём границы чертежа
                Rect drawingBounds = MainWindow.GetDrawingBounds(dxf);

                if (drawingBounds.Width == 0 || drawingBounds.Height == 0) continue;

                // 2. Рассчитаем масштаб и смещение
                double targetWidth = 100;
                double targetHeight = 100;

                double scaleX = targetWidth / drawingBounds.Width;
                double scaleY = targetHeight / drawingBounds.Height;
                double scale = Math.Min(scaleX, scaleY);          //намеренно уменьшен масштаб

                double offsetX = (targetWidth - drawingBounds.Width * scale) / 2 - drawingBounds.X * scale;
                double offsetY = (targetHeight - drawingBounds.Height * scale) / 2 - drawingBounds.Y * scale;

                DetailData detailData = new() { Title = Path.GetFileNameWithoutExtension(path), Number = Details.Count + 1 };
                if (Billet.TypeDetailDrop.Text == "Лист металла")
                {
                    detailData.IsLaser = true;
                    detailData.IsPipe = false; 
                }
                else
                {
                    detailData.IsLaser = false;
                    detailData.IsPipe = true;
                }

                ObservableCollection<IGeometryDescriptor> geometries = new();

                // 3. Отрисуем все объекты с учётом масштаба и смещения
                foreach (var entity in dxf.Entities)
                {
                    if (entity is Line line)
                    {
                        MainWindow.DrawLine(line, scale, offsetX, offsetY, geometries);
                    }
                    //else if (entity is LwPolyline polyline)
                    //{
                    //    //DrawPolyline(polyline, scale, offsetX, offsetY, geometries);
                    //    var descriptor = GeometryConverter.Convert(polyline, scale, offsetX, offsetY);
                    //    detailData.Geometries.Add(descriptor);
                    //}
                    else if (entity is Arc arc)
                    {
                        MainWindow.DrawArc(arc, scale, offsetX, offsetY, geometries);
                    }
                    else if (entity is Circle circle)
                    {
                        MainWindow.DrawCircle(circle, scale, offsetX, offsetY, geometries);
                    }
                    else if (entity is Insert insert)
                    {
                        MainWindow.RenderBlock(insert.Block, scale, offsetX, offsetY, geometries);
                    }
                }

                detailData.Geometries = geometries;
                Details.Add(detailData);
            }
        }
    }

    public class DetailData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public string Title { get; set; } = string.Empty;
        public int Number { get; set; }
        public ObservableCollection<IGeometryDescriptor> Geometries { get; set; } = new();

        private bool isLaser;
        public bool IsLaser
        {
            get => isLaser;
            set => isLaser = value;
        }

        private bool isPipe;
        public bool IsPipe
        {
            get => isPipe;
            set => isPipe = value;
        }

        private float width;
        public float Width
        {
            get => width;
            set
            {
                if (value != width)
                {
                    width = value;
                    OnPropertyChanged(nameof(Width));
                }
            }
        }        
        
        private float height;
        public float Height
        {
            get => height;
            set
            {
                if (value != height)
                {
                    height = value;
                    OnPropertyChanged(nameof(Height));
                }
            }
        }        
        
        private float length;
        public float Length
        {
            get => length;
            set
            {
                if (value != length)
                {
                    length = value;
                    OnPropertyChanged(nameof(Length));
                }
            }
        }

        private int count = 1;
        public int Count
        {
            get => count;
            set
            {
                if (value != count)
                {
                    count = value;
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        public DetailData() { }
    }

    //public static class CanvasHelper
    //{
    //    public static readonly DependencyProperty ElementsProperty =
    //        DependencyProperty.RegisterAttached(
    //            "Elements",
    //            typeof(ObservableCollection<UIElement>),
    //            typeof(CanvasHelper),
    //            new PropertyMetadata(null, OnElementsChanged));

    //    public static ObservableCollection<UIElement> GetElements(DependencyObject obj)
    //    {
    //        return (ObservableCollection<UIElement>)obj.GetValue(ElementsProperty);
    //    }

    //    public static void SetElements(DependencyObject obj, ObservableCollection<UIElement> value)
    //    {
    //        obj.SetValue(ElementsProperty, value);
    //    }

    //    private static void OnElementsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    //    {
    //        if (d is Canvas canvas)
    //        {
    //            canvas.Children.Clear();

    //            if (e.NewValue is ObservableCollection<UIElement> elements)
    //            {
    //                foreach (var element in elements)
    //                {
    //                    canvas.Children.Add(element);
    //                }

    //                // Подписываемся на изменения коллекции
    //                elements.CollectionChanged += (sender, args) =>
    //                {
    //                    canvas.Children.Clear();
    //                    foreach (var element in elements)
    //                    {
    //                        canvas.Children.Add(element);
    //                    }
    //                };
    //            }
    //        }
    //    }
    //}
}
