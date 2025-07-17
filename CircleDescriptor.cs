using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    public class CircleDescriptor : IGeometryDescriptor
    {
        public Point Center { get; set; }
        public double Radius { get; set; }

        public Brush Stroke { get; set; } = Brushes.Red;
        public double StrokeThickness { get; set; } = 0.5;

        public void Draw(Canvas canvas)
        {
            var ellipse = new Ellipse
            {
                Width = Radius * 2,
                Height = Radius * 2,
                Stroke = Stroke,
                StrokeThickness = StrokeThickness
            };

            Canvas.SetLeft(ellipse, Center.X - Radius);
            Canvas.SetTop(ellipse, Center.Y - Radius);

            canvas.Children.Add(ellipse);
        }
    }
}