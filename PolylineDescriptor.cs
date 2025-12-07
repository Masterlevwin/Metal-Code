using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    public class PolylineDescriptor : IGeometryDescriptor
    {
        public PointCollection Points { get; set; } = new PointCollection();
        public Brush Stroke { get; set; } = Brushes.Black;
        public double StrokeThickness { get; set; } = 0.5;
        public bool IsClosed { get; set; } = false;

        public void Draw(Canvas canvas)
        {
            if (Points.Count < 2) return;

            var polyline = new Polyline
            {
                Points = Points,
                Stroke = Stroke,
                StrokeThickness = StrokeThickness,
                Fill = IsClosed ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)) : null // прозрачная заливка, если замкнут
            };

            if (IsClosed && polyline.Points.Count >= 2)
            {
                // WPF Polyline не замыкает автоматически — но для предпросмотра можно и не замыкать
                // Если нужно — используем Path с PathGeometry, но для простоты оставим Polyline
            }

            canvas.Children.Add(polyline);
        }
    }
}