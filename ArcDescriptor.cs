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

        public double Scale { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }

        public Brush Stroke { get; set; } = Brushes.Green;
        public double StrokeThickness { get; set; } = 0.5;

        public void Draw(Canvas canvas)
        {
            // Рассчитываем начальную и конечную точку дуги
            double startX = Center.X + Radius * Math.Cos(StartAngle.ToRadians());
            double startY = Center.Y + Radius * Math.Sin(StartAngle.ToRadians());

            double endX = Center.X + Radius * Math.Cos((StartAngle + SweepAngle).ToRadians());
            double endY = Center.Y + Radius * Math.Sin((StartAngle + SweepAngle).ToRadians());

            // Определяем направление дуги
            bool isLargeArc = Math.Abs(SweepAngle) >= Math.PI;
            SweepDirection sweepDirection = SweepAngle >= 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

            // Создаём дугу
            var arcSegment = new ArcSegment(
                new Point(endX, endY),
                new Size(Radius, Radius),
                rotationAngle: 0,
                isLargeArc: isLargeArc,
                sweepDirection: sweepDirection,
                isStroked: true
            );

            //Создаём PathFigure
            var figure = new PathFigure
            {
                StartPoint = new Point(startX, startY),
                IsClosed = false
            };
            figure.Segments.Add(arcSegment);

            // Создаём PathGeometry и Path
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var path = new Path
            {
                Data = geometry,
                Stroke = Stroke,
                StrokeThickness = StrokeThickness
            };

            // Добавляем на Canvas
            canvas.Children.Add(path);
        }
    }

    public static class DoubleExtensions
    {
        public static double ToRadians(this double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }
    }
}