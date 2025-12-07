using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    public class EllipseDescriptor : IGeometryDescriptor
    {
        public Point Center { get; set; }
        public double RadiusX { get; set; } // Большая/малая полуось по X
        public double RadiusY { get; set; } // Большая/малая полуось по Y

        public Brush Stroke { get; set; } = Brushes.Red;
        public double StrokeThickness { get; set; } = 0.5;

        public void Draw(Canvas canvas)
        {
            double width = RadiusX * 2;
            double height = RadiusY * 2;

            var ellipse = new Ellipse
            {
                Width = width,
                Height = height,
                Stroke = Stroke,
                StrokeThickness = StrokeThickness,
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(ellipse, Center.X - RadiusX);
            Canvas.SetTop(ellipse, Center.Y - RadiusY);

            canvas.Children.Add(ellipse);
        }
    }
}