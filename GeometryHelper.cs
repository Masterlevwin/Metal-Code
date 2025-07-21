using System.Windows.Media;

namespace Metal_Code
{
    public static class GeometryHelper
    {
        public static string GeometryToString(PathGeometry geometry)
        {
            var path = "";

            foreach (var figure in geometry.Figures)
            {
                path += $"M {figure.StartPoint.X:F2},{figure.StartPoint.Y:F2} ";

                foreach (var segment in figure.Segments)
                {
                    if (segment is LineSegment line)
                    {
                        path += $"L {line.Point.X:F2},{line.Point.Y:F2} ";
                    }
                    else if (segment is ArcSegment arc)
                    {
                        path += $"A {arc.Size.Width:F2},{arc.Size.Height:F2} ";
                        path += $"{arc.RotationAngle:F0} ";
                        path += $"{(arc.IsLargeArc ? 1 : 0)} ";
                        path += $"{(arc.SweepDirection == SweepDirection.Clockwise ? 1 : 0)} ";
                        path += $"{arc.Point.X:F2},{arc.Point.Y:F2} ";
                    }
                    else if (segment is BezierSegment bezier)
                    {
                        path += $"C {bezier.Point1.X:F2},{bezier.Point1.Y:F2}, ";
                        path += $"{bezier.Point2.X:F2},{bezier.Point2.Y:F2}, ";
                        path += $"{bezier.Point3.X:F2},{bezier.Point3.Y:F2} ";
                    }
                    // Другие сегменты можно добавить по аналогии
                }
            }

            return path.Trim();
        }
    }
}