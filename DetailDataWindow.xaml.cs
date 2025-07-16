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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        private void Add_DetailData(object sender, RoutedEventArgs e)
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

        CadDocument dxf = null!;

        private void RenderDxf(string[] paths)
        {
            var reader = new DxfReader(paths[0]);
            dxf = reader.Read();

            // 1. Найдём границы чертежа
            Rect drawingBounds = GetDrawingBounds();

            if (drawingBounds.Width == 0 || drawingBounds.Height == 0)
                return;

            // 2. Рассчитаем масштаб и смещение
            double targetWidth = 100;
            double targetHeight = 100;

            double scaleX = targetWidth / drawingBounds.Width;
            double scaleY = targetHeight / drawingBounds.Height;
            double scale = Math.Min(scaleX, scaleY);

            double offsetX = (targetWidth - drawingBounds.Width * scale) / 2 - drawingBounds.X * scale;
            double offsetY = (targetHeight - drawingBounds.Height * scale) / 2 - drawingBounds.Y * scale;

            // 3. Отрисуем все объекты с учётом масштаба и смещения
            foreach (var entity in dxf.Entities)
            {
                if (entity is Line line)
                {
                    DrawLine(line, scale, offsetX, offsetY);
                }
                else if (entity is LwPolyline polyline)
                {
                    DrawPolyline(polyline, scale, offsetX, offsetY);
                }
                else if (entity is Arc arc)
                {
                    DrawArc(arc, scale, offsetX, offsetY);
                }
                else if (entity is Circle circle)
                {
                    DrawCircle(circle, scale, offsetX, offsetY);
                }
                else if (entity is Insert insert)
                {
                    RenderBlock(insert.Block, scale, offsetX, offsetY);
                }
            }
        }

        private Rect GetDrawingBounds()
        {
            var bounds = new List<Point>();

            foreach (var entity in dxf.Entities)
            {
                if (entity is Line line)
                {
                    bounds.Add(new Point(line.StartPoint.X, line.StartPoint.Y));
                    bounds.Add(new Point(line.EndPoint.X, line.EndPoint.Y));
                }
                else if (entity is LwPolyline polyline)
                {
                    foreach (var vertex in polyline.Vertices)
                    {
                        bounds.Add(new Point(vertex.Location.X, vertex.Location.Y));
                    }
                }
                else if (entity is Arc arc)
                {
                    bounds.Add(new Point(arc.Center.X, arc.Center.Y));
                }
                else if (entity is Circle circle)
                {
                    bounds.Add(new Point(circle.Center.X, circle.Center.Y));
                }
                else if (entity is Insert insert)
                {
                    foreach (var reference in insert.Block.Entities)
                    {
                        if (reference is Line _line)
                        {
                            bounds.Add(new Point(_line.StartPoint.X, _line.StartPoint.Y));
                            bounds.Add(new Point(_line.EndPoint.X, _line.EndPoint.Y));
                        }
                        else if (reference is LwPolyline _polyline)
                        {
                            foreach (var vertex in _polyline.Vertices)
                            {
                                bounds.Add(new Point(vertex.Location.X, vertex.Location.Y));
                            }
                        }
                    }
                }
            }

            if (bounds.Count == 0)
                return Rect.Empty;

            double minX = bounds.Min(p => p.X);
            double maxX = bounds.Max(p => p.X);
            double minY = bounds.Min(p => p.Y);
            double maxY = bounds.Max(p => p.Y);

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private void DrawLine(Line line, double scale, double offsetX, double offsetY)
        {
            Point start = Transform(line.StartPoint, scale, offsetX, offsetY);
            Point end = Transform(line.EndPoint, scale, offsetX, offsetY);

            var lineShape = new System.Windows.Shapes.Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5
            };

            DetailCanvas.Children.Add(lineShape);
        }

        private void DrawPolyline(LwPolyline polyline, double scale, double offsetX, double offsetY)
        {
            var pathFigure = new PathFigure();
            bool first = true;

            foreach (var vertex in polyline.Vertices)
            {
                Point point = Transform(vertex.Location, scale, offsetX, offsetY);

                if (first)
                {
                    pathFigure.StartPoint = point;
                    first = false;
                }
                else
                {
                    pathFigure.Segments.Add(new LineSegment(point, true));
                }
            }

            if (polyline.IsClosed && polyline.Vertices.Count > 1)
            {
                pathFigure.Segments.Add(new LineSegment(pathFigure.StartPoint, true));
            }

            var geometry = new PathGeometry();
            geometry.Figures.Add(pathFigure);

            var path = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5
            };

            DetailCanvas.Children.Add(path);
        }

        private void DrawArc(Arc arc, double scale, double offsetX, double offsetY)
        {
            Point center = Transform(arc.Center, scale, offsetX, offsetY);
            double radius = arc.Radius * scale;

            double startAngle = arc.StartAngle;
            double endAngle = arc.EndAngle;
            double sweepAngle = endAngle - startAngle;

            bool isLargeArc = Math.Abs(sweepAngle) > 180.0;

            var startPoint = new System.Windows.Point(
                center.X + radius * Math.Cos(DegToRad(startAngle)),
                center.Y + radius * Math.Sin(DegToRad(startAngle)));

            var endPoint = new Point(
                center.X + radius * Math.Cos(DegToRad(endAngle)),
                center.Y + radius * Math.Sin(DegToRad(endAngle)));

            var arcSegment = new ArcSegment(
                endPoint,
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true);

            var figure = new PathFigure
            {
                StartPoint = startPoint
            };
            figure.Segments.Add(arcSegment);

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var path = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Stroke = Brushes.Green,
                StrokeThickness = 0.5
            };

            DetailCanvas.Children.Add(path);
        }

        private void DrawCircle(Circle circle, double scale, double offsetX, double offsetY)
        {
            Point center = Transform(circle.Center, scale, offsetX, offsetY);
            double radius = circle.Radius * scale;

            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = Brushes.Red,
                StrokeThickness = 0.5
            };

            Canvas.SetLeft(ellipse, center.X - radius);
            Canvas.SetTop(ellipse, center.Y - radius);

            DetailCanvas.Children.Add(ellipse);
        }

        private void RenderBlock(BlockRecord block, double scale, double offsetX, double offsetY)
        {
            if (block == null || block.Entities == null) return;

            foreach (var reference in block.Entities)
            {
                if (reference is Line line)
                {
                    DrawLine(line, scale, offsetX, offsetY);
                }
                else if (reference is LwPolyline polyline)
                {
                    DrawPolyline(polyline, scale, offsetX, offsetY);
                }
                else if (reference is Arc arc)
                {
                    DrawArc(arc, scale, offsetX, offsetY);
                }
                else if (reference is Circle circle)
                {
                    DrawCircle(circle, scale, offsetX, offsetY);
                }
            }
        }

        //вспомогательные методы
        private Point Transform(XY point, double scale, double offsetX, double offsetY)
        {
            return new Point(point.X * scale + offsetX, point.Y * scale + offsetY);
        }
                
        private Point Transform(XYZ point, double scale, double offsetX, double offsetY)
        {
            return new Point(point.X * scale + offsetX, point.Y * scale + offsetY); 
        }

        private double DegToRad(double deg) => deg * Math.PI / 180;
    }

    public class DetailData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public int Number { get; set; }

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
}
