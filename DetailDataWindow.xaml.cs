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
using static System.Windows.Forms.AxHost;
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
                double targetWidth = 200;
                double targetHeight = 200;

                double scaleX = targetWidth / drawingBounds.Width;
                double scaleY = targetHeight / drawingBounds.Height;
                double scale = Math.Min(scaleX, scaleY);          //намеренно уменьшен масштаб

                double offsetX = (targetWidth - drawingBounds.Width * scale) / 2 - drawingBounds.X * scale;
                double offsetY = (targetHeight - drawingBounds.Height * scale) / 2 - drawingBounds.Y * scale;


                DetailData detailData = new() { Title =Path.GetFileNameWithoutExtension(path), Number = Details.Count + 1 };
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


                // 3. Отрисуем все объекты с учётом масштаба и смещения
                foreach (var entity in dxf.Entities)
                {
                    if (entity is Line line)
                    {
                        DrawLine(line, scale, offsetX, offsetY, detailData);
                    }
                    //else if (entity is LwPolyline polyline)
                    //{
                    //    //DrawPolyline(polyline, scale, offsetX, offsetY, detailData);
                    //    var descriptor = GeometryConverter.Convert(polyline, scale, offsetX, offsetY);
                    //    detailData.Geometries.Add(descriptor);
                    //}
                    else if (entity is Arc arc)
                    {
                        DrawArc(arc, scale, offsetX, offsetY, detailData);
                    }
                    else if (entity is Circle circle)
                    {
                        DrawCircle(circle, scale, offsetX, offsetY, detailData);
                    }
                    else if (entity is Insert insert)
                    {
                        RenderBlock(insert.Block, scale, offsetX, offsetY, detailData);
                    }
                }

                Details.Add(detailData);

                MessageBox.Show(MainWindow.M.Log);
            }
        }

        private void DrawLine(Line line, double scale, double offsetX, double offsetY, DetailData detailData)
        {
            Point start = Transform(line.StartPoint, scale, offsetX, offsetY);
            Point end = Transform(line.EndPoint, scale, offsetX, offsetY);

            detailData.Geometries.Add(new LineDescriptor
            {
                Start = start,
                End = end
            });
        }

        private void DrawPolyline(LwPolyline polyline, double scale, double offsetX, double offsetY, DetailData detailData)
        {
            //var pathFigure = new PathFigure();
            //bool first = true;

            List<Point> points = new();

            foreach (var vertex in polyline.Vertices)
            {
                Point point = Transform(vertex.Location, scale, offsetX, offsetY);
                points.Add(point);

                //if (first)
                //{
                //    pathFigure.StartPoint = point;
                //    first = false;
                //}
                //else
                //{
                //    pathFigure.Segments.Add(new LineSegment(point, true));
                //}
            }

            //if (polyline.IsClosed && polyline.Vertices.Count > 1)
            //{
            //    pathFigure.Segments.Add(new LineSegment(pathFigure.StartPoint, true));
            //}

            //var geometry = new PathGeometry();
            //geometry.Figures.Add(pathFigure);

            //var path = new System.Windows.Shapes.Path
            //{
            //    Data = geometry,
            //    Stroke = Brushes.Black,
            //    StrokeThickness = 0.5
            //};

            detailData.Geometries.Add(new PolylineDescriptor
            {
                Points = points,
                IsClosed = polyline.IsClosed
            });
        }

        private void DrawArc(Arc arc, double scale, double offsetX, double offsetY, DetailData detailData)
        {
            Point center = Transform(arc.Center, scale, offsetX, offsetY);
            double radius = arc.Radius * scale;

            double startAngle = arc.StartAngle;
            double endAngle = arc.EndAngle;
            double sweepAngle = endAngle - startAngle;

            detailData.Geometries.Add(new ArcDescriptor
            {
                Center = center,
                Radius = radius,
                StartAngle = startAngle,
                SweepAngle = sweepAngle
            });
        }

        private static void DrawCircle(Circle circle, double scale, double offsetX, double offsetY, DetailData detailData)
        {
            Point center = Transform(circle.Center, scale, offsetX, offsetY);
            double radius = circle.Radius * scale;

            detailData.Geometries.Add(new CircleDescriptor
            {
                Center = center,
                Radius = radius
            });
        }

        private void RenderBlock(BlockRecord block, double scale, double offsetX, double offsetY, DetailData detailData)
        {
            if (block == null || block.Entities == null) return;

            foreach (var reference in block.Entities)
            {
                if (reference is Line line)
                {
                    DrawLine(line, scale, offsetX, offsetY, detailData);
                }
                //else if (reference is LwPolyline polyline)
                //{
                //    //DrawPolyline(polyline, scale, offsetX, offsetY, detailData);
                //    var descriptor = GeometryConverter.Convert(polyline, scale, offsetX, offsetY);
                //    detailData.Geometries.Add(descriptor);
                //}
                else if (reference is Arc arc)
                {
                    DrawArc(arc, scale, offsetX, offsetY, detailData);
                }
                else if (reference is Circle circle)
                {
                    DrawCircle(circle, scale, offsetX, offsetY, detailData);
                }
            }
        }

        //вспомогательные методы
        public static Point Transform(XY point, double scale, double offsetX, double offsetY)
        {
            return new Point(point.X * scale + offsetX, point.Y * scale + offsetY);
        }
                
        public static Point Transform(XYZ point, double scale, double offsetX, double offsetY)
        {
            return new Point(point.X * scale + offsetX, point.Y * scale + offsetY);
        }

        private static double DegToRad(double deg) => deg * Math.PI / 180;
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
