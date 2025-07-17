using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    public class PolylineDescriptor : IGeometryDescriptor
    {
        public List<Point> Points { get; set; } = new();
        public bool IsClosed { get; set; }

        public Brush Stroke { get; set; } = Brushes.Black;
        public double StrokeThickness { get; set; } = 0.5;

        public void Draw(Canvas canvas)
        {
            if (Points.Count < 2) return;

            var pathFigure = new PathFigure
            {
                StartPoint = new Point(Points[0].X, Points[0].Y)
            };

            for (int i = 1; i < Points.Count; i++)
            {
                pathFigure.Segments.Add(new LineSegment(new Point(Points[i].X, Points[i].Y), true));
            }

            if (IsClosed)
            {
                pathFigure.Segments.Add(new LineSegment(pathFigure.StartPoint, true));
            }

            var geometry = new PathGeometry();
            geometry.Figures.Add(pathFigure);

            var path = new Path
            {
                Data = geometry,
                Stroke = Stroke,
                StrokeThickness = StrokeThickness
            };

            canvas.Children.Add(path);
        }
    }
}