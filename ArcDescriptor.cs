using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    public class ArcDescriptor : IGeometryDescriptor
    {
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
        public Size Size { get; set; }
        public bool IsLargeArc { get; set; }
        public SweepDirection SweepDirection { get; set; }

        public Brush Stroke { get; set; } = Brushes.Black;
        public double StrokeThickness { get; set; } = 0.5;

        public void Draw(Canvas canvas)
        {
            var arcSegment = new ArcSegment
            {
                Point = EndPoint,
                Size = Size,
                RotationAngle = 0,
                IsLargeArc = IsLargeArc,
                SweepDirection = SweepDirection,
                IsStroked = true
            };
            
            var figure = new PathFigure
            {
                StartPoint = StartPoint,
                Segments = { arcSegment },
                IsClosed = false
            };

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var path = new Path
            {
                Data = geometry,
                Stroke = Stroke,
                StrokeThickness = StrokeThickness
            };

            canvas.Children.Add(path);

            Trace.WriteLine($"Arc: from {StartPoint:F2} to {EndPoint:F2}, size={Size:F2}, large={IsLargeArc}, sweep={SweepDirection}");
        }
    }

    public static class DoubleExtensions
    {
        public static double ToRadians(this double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}