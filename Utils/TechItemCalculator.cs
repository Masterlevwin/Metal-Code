using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Metal_Code.Utils
{
    public static class TechItemCalculator
    {
        /// <summary>
        /// Заполняет геометрические параметры: Width, Height, Way, Pinhole
        /// </summary>
        public static void UpdateFromGeometry(TechItem techItem)
        {
            if (techItem.CalculationGeometry == null || techItem.CalculationGeometry.IsEmpty())
                return;

            // 1. Габариты
            var bounds = techItem.CalculationGeometry.Bounds;
            techItem.Width = Math.Ceiling(bounds.Width);
            techItem.Height = Math.Ceiling(bounds.Height);
            techItem.Sizes = $"{techItem.Width}x{techItem.Height}";

            // 2. Длина резки
            techItem.Way = (float)CalculateCuttingLength(techItem.CalculationGeometry);

            // 3. Количество проколов = число замкнутых контуров
            techItem.Pinhole = CalculatePiercingCount(techItem.CalculationGeometry);
        }

        private static double CalculateCuttingLength(PathGeometry geometry)
        {
            double totalLength = 0.0;
            foreach (PathFigure figure in geometry.Figures)
            {
                totalLength += GetFigureLength(figure);
            }
            return totalLength;
        }

        private static double GetFigureLength(PathFigure figure)
        {
            double length = 0.0;
            Point current = figure.StartPoint;

            foreach (PathSegment seg in figure.Segments)
            {
                (Point end, double segLength) = seg switch
                {
                    LineSegment line => (line.Point, DxfToWpfConverter.Distance(current, line.Point)),
                    ArcSegment arc => ApproximateArcLength(current, arc),
                    PolyLineSegment poly => ApproximatePolyLineLength(current, poly),
                    _ => (current, 0.0)
                };
                length += segLength;
                current = end;
            }

            // Если фигура замкнута — добавляем сегмент к началу
            if (figure.IsClosed && figure.Segments.Count > 0)
            {
                length += DxfToWpfConverter.Distance(current, figure.StartPoint);
            }

            return length;
        }

        private static (Point, double) ApproximateArcLength(Point start, ArcSegment arc)
        {
            // Упрощённый расчёт длины дуги
            double radius = (arc.Size.Width + arc.Size.Height) / 2.0;
            // Угол дуги: к сожалению, из ArcSegment его не достать точно
            // Поэтому используем приближение по хорде
            double chord = DxfToWpfConverter.Distance(start, arc.Point);
            if (chord == 0) return (arc.Point, 0.0);

            // Максимальная длина дуги — π * radius (полукруг)
            // Минимальная — chord
            double angle = 2 * Math.Asin(chord / (2 * radius));
            if (double.IsNaN(angle) || angle <= 0) angle = Math.PI;

            if (arc.IsLargeArc) angle = 2 * Math.PI - angle;

            return (arc.Point, radius * angle);
        }

        private static (Point, double) ApproximatePolyLineLength(Point start, PolyLineSegment poly)
        {
            double len = 0.0;
            Point current = start;
            Point last = current;
            foreach (Point pt in poly.Points)
            {
                len += DxfToWpfConverter.Distance(current, pt);
                current = pt;
                last = pt;
            }
            return (last, len);
        }

        private static int CalculatePiercingCount(PathGeometry geometry)
        {
            // Каждый замкнутый контур = 1 прокол
            return geometry.Figures.Count(fig => fig.IsClosed);
        }


        /// <summary>
        /// Пересчитывает массу на основе металла
        /// </summary>
        public static double CalculateMass(TechItem techItem, double density)
        {
            var destiny = MainWindow.Parser(techItem.Destiny);

            if (techItem.CalculationGeometry != null && destiny > 0)
            {
                double areaMm2 = CalculateArea(techItem.CalculationGeometry);
                if (areaMm2 <= 0) return 0.0;

                double areaM2 = areaMm2 / 1_000_000.0;
                double volume = areaM2 * destiny;

                return volume * density; // кг
            }

            return 0.0;
        }

        private static double CalculateArea(PathGeometry geometry)
        {
            var absAreas = new List<double>();
            var rawAreas = new List<double>();

            foreach (var fig in geometry.Figures)
            {
                if (!fig.IsClosed) continue;
                var points = GetFigurePoints(fig);
                if (points.Count < 3) continue;
                double area = PolygonArea(points);
                rawAreas.Add(area);
                absAreas.Add(Math.Abs(area));
            }

            if (absAreas.Count == 0) return 0.0;

            int outerIndex = 0;
            for (int i = 1; i < absAreas.Count; i++)
                if (absAreas[i] > absAreas[outerIndex])
                    outerIndex = i;

            double total = absAreas[outerIndex];
            for (int i = 0; i < absAreas.Count; i++)
            {
                if (i != outerIndex)
                    total -= absAreas[i];
            }

            return Math.Max(0, total);
        }

        private static List<Point> GetFigurePoints(PathFigure figure)
        {
            var points = new List<Point> { figure.StartPoint };
            Point current = figure.StartPoint;

            foreach (var seg in figure.Segments)
            {
                switch (seg)
                {
                    case LineSegment line:
                        points.Add(line.Point);
                        current = line.Point;
                        break;
                    case ArcSegment arc:
                        // Аппроксимируем дугу линиями
                        var arcPoints = ApproximateArc(current, arc);
                        points.AddRange(arcPoints.Skip(1)); // первая точка = current
                        current = arc.Point;
                        break;
                    case PolyLineSegment poly:
                        points.AddRange(poly.Points);
                        if (poly.Points.Count > 0)
                            current = poly.Points[^1];
                        break;
                }
            }

            if (figure.IsClosed && points.Count > 1 && points[0] != points[^1])
            {
                points.Add(points[0]);
            }

            return points;
        }

        private static double PolygonArea(List<Point> points)
        {
            double area = 0.0;
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }
            return area / 2.0;
        }

        private static List<Point> ApproximateArc(Point start, ArcSegment arc)
        {
            // Простая аппроксимация дуги 10 отрезками
            var points = new List<Point>();
            double radiusX = arc.Size.Width;
            double radiusY = arc.Size.Height;

            // Найдём центр (упрощённо, предполагая окружность)
            double dx = arc.Point.X - start.X;
            double dy = arc.Point.Y - start.Y;
            double centerX = (start.X + arc.Point.X) / 2.0;
            double centerY = (start.Y + arc.Point.Y) / 2.0;

            // Углы
            double startAngle = Math.Atan2(start.Y - centerY, start.X - centerX);
            double endAngle = Math.Atan2(arc.Point.Y - centerY, arc.Point.X - centerX);

            if (endAngle <= startAngle) endAngle += 2 * Math.PI;
            if (arc.IsLargeArc && (endAngle - startAngle) < Math.PI)
                endAngle += 2 * Math.PI;
            if (!arc.IsLargeArc && (endAngle - startAngle) > Math.PI)
                endAngle -= 2 * Math.PI;

            int segments = 10;
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                double angle = startAngle + t * (endAngle - startAngle);
                double x = centerX + Math.Cos(angle) * radiusX;
                double y = centerY + Math.Sin(angle) * radiusY;
                points.Add(new Point(x, y));
            }

            return points;
        }
    }
}
