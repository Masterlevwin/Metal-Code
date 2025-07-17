using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    public class ArcDescriptor : IGeometryDescriptor
    {
        public Point Center { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double SweepAngle { get; set; }

        public Brush Stroke { get; set; } = Brushes.Green;
        public double StrokeThickness { get; set; } = 0.5;

        public void Draw(Canvas canvas)
        {
            double startX = Center.X + Radius * Math.Cos(StartAngle.ToRadians());
            double startY = Center.Y + Radius * Math.Sin(StartAngle.ToRadians());

            double endX = Center.X + Radius * Math.Cos((StartAngle + SweepAngle).ToRadians());
            double endY = Center.Y + Radius * Math.Sin((StartAngle + SweepAngle).ToRadians());

            var arcSegment = new ArcSegment(
                new Point(endX, endY),
                new Size(Radius, Radius),
                0,
                SweepAngle > 180,
                SweepDirection.Clockwise,
                true);

            var figure = new PathFigure
            {
                StartPoint = new Point(startX, startY)
            };
            figure.Segments.Add(arcSegment);

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var path = new Path
            {
                Data = geometry,
                Stroke = Stroke,
                StrokeThickness = StrokeThickness
            };

            canvas.Children.Add(path);
        }
    }

    // Расширение для угла
    public static class DoubleExtensions
    {
        public static double ToRadians(this double deg) => deg * Math.PI / 180.0;
    }
}