using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Metal_Code.Utils
{
    public static class DxfToWpfConverter
    {
        /// <summary>
        /// Конвертирует DXF-документ в PathGeometry без замыкания контуров.
        /// </summary>
        public static PathGeometry ConvertToPathGeometryAsIs(DxfDocument dxf, double scale = 1.0)
        {
            var figures = new List<PathFigure>();
            foreach (var entity in dxf.Entities.All)
            {
                AppendEntityToFigures(entity, figures, scale);
            }
            return new PathGeometry(figures, FillRule.EvenOdd, null);
        }

        /// <summary>
        /// Конвертирует DXF-документ в PathGeometry с автоматическим замыканием контуров.
        /// </summary>
        public static PathGeometry ConvertToPathGeometryWithClosedContours(DxfDocument dxf, double unitsToPixels = 1.0)
        {
            var rawFigures = new List<PathFigure>();

            // Этап 1: конвертация всех сущностей в PathFigure
            foreach (var entity in dxf.Entities.All)
            {
                AppendEntityToFigures(entity, rawFigures, unitsToPixels);
            }

            if (rawFigures.Count == 0)
                return new PathGeometry { FillRule = FillRule.EvenOdd };

            // Этап 2: извлекаем все сегменты с координатами начала и конца
            var segments = new List<SegmentWithEndpoints>();
            foreach (var fig in rawFigures)
            {
                Point current = fig.StartPoint;
                foreach (PathSegment seg in fig.Segments)
                {
                    Point end = GetSegmentEnd(current, seg);
                    segments.Add(new SegmentWithEndpoints(current, end, seg));
                    current = end;
                }

                // Явно добавляем замыкающий сегмент, если фигура должна быть замкнута
                if (fig.IsClosed && fig.Segments.Count > 0)
                {
                    segments.Add(new SegmentWithEndpoints(current, fig.StartPoint,
                        new LineSegment(fig.StartPoint, true)));
                }
            }

            if (segments.Count == 0)
                return new PathGeometry { FillRule = FillRule.EvenOdd };

            // Этап 3: сборка цепочек (склеивание линий)
            var used = new bool[segments.Count];
            var finalFigures = new List<PathFigure>();

            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i]) continue;

                var chain = BuildChain(segments, used, i);
                if (chain.Count == 0) continue;

                var start = chain[0].Start;
                var end = chain[^1].End;
                bool isClosed = Distance(start, end) <= 0.1; // допуск 0.1 мм

                var figure = new PathFigure { StartPoint = start, IsClosed = isClosed };
                foreach (var seg in chain)
                    figure.Segments.Add(seg.Segment);

                // Принудительное замыкание "почти замкнутых" контуров
                if (!isClosed && Distance(start, end) <= 0.1)
                {
                    figure.Segments.Add(new LineSegment(start, true));
                    figure.IsClosed = true;
                }

                // Добавляем только значимые замкнутые контуры
                if (figure.IsClosed && figure.Segments.Count >= 3)
                {
                    finalFigures.Add(figure);
                }
            }

            return new PathGeometry(finalFigures, FillRule.EvenOdd, null);
        }

        // ---- Вспомогательные методы конвертации DXF -> PathFigure ----

        private static void AppendEntityToFigures(EntityObject entity, List<PathFigure> figures, double scale)
        {
            if (entity is Insert insert)
            {
                foreach (var subEntity in insert.Block.Entities)
                {
                    AppendEntityToFigures(subEntity, figures, scale);
                }
            }
            else
            {
                var figure = EntityToPathFigure(entity, scale);
                if (figure != null)
                    figures.Add(figure);
            }
        }

        private static PathFigure? EntityToPathFigure(EntityObject entity, double scale)
        {
            return entity switch
            {
                Line line => LineToPathFigure(line, scale),
                Polyline2D polyline => PolylineToPathFigure(polyline, scale),
                Circle circle => CircleToPathFigure(circle, scale),
                Arc arc => ArcToPathFigure(arc, scale),
                Spline spline => SplineToPathFigure(spline, scale),
                _ => null
            };
        }

        private static PathFigure LineToPathFigure(Line line, double scale)
        {
            var start = new Point(line.StartPoint.X * scale, -line.StartPoint.Y * scale);
            var end = new Point(line.EndPoint.X * scale, -line.EndPoint.Y * scale);
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new LineSegment(end, true));
            return figure;
        }

        private static PathFigure? PolylineToPathFigure(Polyline2D pl, double scale)
        {
            if (pl.Vertexes.Count == 0) return null;
            var points = pl.Vertexes.Select(v => new Point(v.Position.X * scale, -v.Position.Y * scale)).ToList();
            return CreatePathFigureFromPoints(points, pl.IsClosed);
        }

        private static PathFigure CircleToPathFigure(Circle circle, double scale)
        {
            var center = new Point(circle.Center.X * scale, -circle.Center.Y * scale);
            double radius = circle.Radius * scale;
            var start = new Point(center.X + radius, center.Y);
            var figure = new PathFigure { StartPoint = start, IsClosed = true };
            figure.Segments.Add(new ArcSegment(
                new Point(center.X - radius, center.Y),
                new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new ArcSegment(
                new Point(center.X + radius, center.Y),
                new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            return figure;
        }

        private static PathFigure ArcToPathFigure(Arc arc, double scale)
        {
            var center = new Point(arc.Center.X * scale, -arc.Center.Y * scale);
            double radius = arc.Radius * scale;
            double startAngleRad = arc.StartAngle * Math.PI / 180.0;
            double endAngleRad = arc.EndAngle * Math.PI / 180.0;

            var startPoint = new Point(
                center.X + radius * Math.Cos(startAngleRad),
                center.Y - radius * Math.Sin(startAngleRad));
            var endPoint = new Point(
                center.X + radius * Math.Cos(endAngleRad),
                center.Y - radius * Math.Sin(endAngleRad));

            double angleDiff = (endAngleRad - startAngleRad + 2 * Math.PI) % (2 * Math.PI);
            bool isLargeArc = angleDiff > Math.PI;

            var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
            figure.Segments.Add(new ArcSegment(
                endPoint, new Size(radius, radius), 0, isLargeArc,
                SweepDirection.Counterclockwise, true));
            return figure;
        }

        private static PathFigure? SplineToPathFigure(Spline spline, double scale)
        {
            var points2D = spline.ToPolyline2D(30).Vertexes;
            if (points2D == null || points2D.Count == 0) return null;
            var points = points2D.Select(v => new Point(v.Position.X * scale, -v.Position.Y * scale)).ToList();
            return CreatePathFigureFromPoints(points, spline.IsClosed);
        }

        private static PathFigure? CreatePathFigureFromPoints(List<Point> points, bool isClosed)
        {
            if (points.Count == 0) return null;
            var figure = new PathFigure { StartPoint = points[0], IsClosed = isClosed };
            for (int i = 1; i < points.Count; i++)
            {
                figure.Segments.Add(new LineSegment(points[i], true));
            }
            if (isClosed && points.Count > 1 && points[0] != points[^1])
            {
                figure.Segments.Add(new LineSegment(points[0], true));
            }
            return figure;
        }

        // ---- Логика сборки контуров из сегментов ----

        private readonly record struct SegmentWithEndpoints(Point Start, Point End, PathSegment Segment);

        private static Point GetSegmentEnd(Point start, PathSegment seg) => seg switch
        {
            LineSegment line => line.Point,
            ArcSegment arc => arc.Point,
            PolyLineSegment poly when poly.Points.Count > 0 => poly.Points[^1],
            _ => start
        };

        private static List<SegmentWithEndpoints> BuildChain(
            List<SegmentWithEndpoints> segments, bool[] used, int startIndex)
        {
            var chain = new List<SegmentWithEndpoints>();
            int currentIdx = startIndex;
            Point currentPoint = segments[startIndex].End;

            while (true)
            {
                used[currentIdx] = true;
                chain.Add(segments[currentIdx]);

                int nextIdx = -1;
                Point nextStart = default;

                // Ищем следующий сегмент, начинающийся в currentPoint
                for (int i = 0; i < segments.Count; i++)
                {
                    if (used[i]) continue;
                    var seg = segments[i];

                    if (Distance(seg.Start, currentPoint) <= 0.1)
                    {
                        nextIdx = i;
                        nextStart = seg.Start;
                        currentPoint = seg.End;
                        break;
                    }

                    // Проверяем в обратном направлении (сегмент "наоборот")
                    if (Distance(seg.End, currentPoint) <= 0.1)
                    {
                        // Переворачиваем сегмент (только для Line/PolyLine)
                        PathSegment reversedSeg = seg.Segment switch
                        {
                            LineSegment ls => new LineSegment(seg.Start, true),
                            PolyLineSegment pls => ReversePolyLineSegment(pls),
                            _ => seg.Segment // дуги не переворачиваем
                        };
                        nextIdx = i;
                        nextStart = seg.End;
                        currentPoint = seg.Start;
                        // Обновляем сегмент в списке
                        segments[i] = new SegmentWithEndpoints(seg.End, seg.Start, reversedSeg);
                        break;
                    }
                }

                if (nextIdx == -1) break;
                currentIdx = nextIdx;
            }

            return chain;
        }

        private static PolyLineSegment ReversePolyLineSegment(PolyLineSegment seg)
        {
            var reversed = new List<Point>(seg.Points);
            reversed.Reverse();
            return new PolyLineSegment(reversed, true);
        }

        public static double Distance(Point a, Point b)
        {
            return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        }
    }
}