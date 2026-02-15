using System.Windows;
using System.Windows.Media;

namespace Metal_Code.Utils
{
    public static class PartPreviewGenerator
    {
        public static void EnsureDisplayGeometry(Part part)
        {
            if (part.DisplayGeometry != null)
                return;

            part.DisplayGeometry = part.PartType switch
            {
                PartType.Rectangle => CreateRectangleSection(part.Width, part.Height),
                PartType.Round => CreateRoundSection(part.Width),
                PartType.RectangularTube => CreateRectangularTubeSection(part.Width, part.Height, part.Destiny),
                PartType.RoundTube => CreateRoundTubeSection(part.Width, part.Destiny),
                _ => null
            };
        }

        public static PathGeometry CreateRectangleSection(double width, double height)
        {
            var geometry = new PathGeometry();

            var figure = new PathFigure
            {
                StartPoint = new Point(-width / 2, -height / 2),
                IsClosed = true,
                IsFilled = true
            };
            figure.Segments.Add(new LineSegment(new Point(width / 2, -height / 2), true));
            figure.Segments.Add(new LineSegment(new Point(width / 2, height / 2), true));
            figure.Segments.Add(new LineSegment(new Point(-width / 2, height / 2), true));
            geometry.Figures.Add(figure);

            return geometry;
        }

        public static PathGeometry CreateRoundSection(double diameter)
        {
            var geometry = new PathGeometry();

            double radius = diameter / 2;
            var figure = new PathFigure
            {
                StartPoint = new Point(radius, 0),
                IsClosed = true,
                IsFilled = true
            };
            figure.Segments.Add(new ArcSegment(
                new Point(-radius, 0),
                new Size(radius, radius),
                0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new ArcSegment(
                new Point(radius, 0),
                new Size(radius, radius),
                0, false, SweepDirection.Clockwise, true));
            geometry.Figures.Add(figure);

            return geometry;
        }

        public static PathGeometry CreateRectangularTubeSection(double width, double height, double thickness)
        {
            var geometry = new PathGeometry();

            // Внешний прямоугольник
            var outer = new PathFigure
            {
                StartPoint = new Point(-width / 2, -height / 2),
                IsClosed = true,
                IsFilled = true
            };
            outer.Segments.Add(new LineSegment(new Point(width / 2, -height / 2), true));
            outer.Segments.Add(new LineSegment(new Point(width / 2, height / 2), true));
            outer.Segments.Add(new LineSegment(new Point(-width / 2, height / 2), true));
            geometry.Figures.Add(outer);

            // Внутренний прямоугольник (полость)
            double innerW = width - 2 * thickness;
            double innerH = height - 2 * thickness;
            if (innerW > 0 && innerH > 0)
            {
                var inner = new PathFigure
                {
                    StartPoint = new Point(-innerW / 2, -innerH / 2),
                    IsClosed = true,
                    IsFilled = false
                };
                inner.Segments.Add(new LineSegment(new Point(innerW / 2, -innerH / 2), true));
                inner.Segments.Add(new LineSegment(new Point(innerW / 2, innerH / 2), true));
                inner.Segments.Add(new LineSegment(new Point(-innerW / 2, innerH / 2), true));
                geometry.Figures.Add(inner);
            }

            return geometry;
        }

        public static PathGeometry CreateRoundTubeSection(double diameter, double thickness)
        {
            var geometry = new PathGeometry();

            double outerR = diameter / 2;
            var outer = new PathFigure
            {
                StartPoint = new Point(outerR, 0),
                IsClosed = true,
                IsFilled = true
            };
            outer.Segments.Add(new ArcSegment(
                new Point(-outerR, 0),
                new Size(outerR, outerR),
                0, false, SweepDirection.Clockwise, true));
            outer.Segments.Add(new ArcSegment(
                new Point(outerR, 0),
                new Size(outerR, outerR),
                0, false, SweepDirection.Clockwise, true));
            geometry.Figures.Add(outer);

            double innerR = outerR - thickness;
            if (innerR > 0)
            {
                var inner = new PathFigure
                {
                    StartPoint = new Point(innerR, 0),
                    IsClosed = true,
                    IsFilled = false
                };
                inner.Segments.Add(new ArcSegment(
                    new Point(-innerR, 0),
                    new Size(innerR, innerR),
                    0, false, SweepDirection.Clockwise, true));
                inner.Segments.Add(new ArcSegment(
                    new Point(innerR, 0),
                    new Size(innerR, innerR),
                    0, false, SweepDirection.Clockwise, true));
                geometry.Figures.Add(inner);
            }

            return geometry;
        }
    }
}
