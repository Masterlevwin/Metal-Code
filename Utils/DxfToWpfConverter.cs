using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;
using Trace = System.Diagnostics.Trace;

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

    public static class GeometryAnalyzer
    {
        public static PartType DetectPartType(PathGeometry geometry)
        {
            Trace.WriteLine("[GeoAnalyzer] === Начало анализа геометрии ===");

            if (geometry == null || geometry.Figures.Count == 0)
            {
                Trace.WriteLine("[GeoAnalyzer] Геометрия пуста. Возврат Rectangle.");
                return PartType.Rectangle;
            }

            Trace.WriteLine($"[GeoAnalyzer] Всего фигур (контуров): {geometry.Figures.Count}");

            // Находим самый большой контур (внешнюю границу детали)
            var mainFigure = geometry.Figures
                .Where(f => f.IsClosed)
                .OrderByDescending(f => GetBoundsArea(f))
                .FirstOrDefault();

            if (mainFigure == null)
            {
                Trace.WriteLine("[GeoAnalyzer] Не найдено замкнутых фигур. Возврат Rectangle.");
                return PartType.Rectangle;
            }

            Trace.WriteLine($"[GeoAnalyzer] Выбран главный контур. Сегментов в нем: {mainFigure.Segments.Count}");

            int lineCount = 0;
            int arcCount = 0;
            var rawVertices = new List<Point>();

            Point current = mainFigure.StartPoint;
            rawVertices.Add(current);

            foreach (var seg in mainFigure.Segments)
            {
                if (seg is LineSegment line)
                {
                    lineCount++;
                    if (Distance(current, line.Point) > 0.5) // Игнорируем микро-отрезки < 0.5 мм
                    {
                        rawVertices.Add(line.Point);
                    }
                    current = line.Point;
                }
                else if (seg is ArcSegment arc)
                {
                    arcCount++;
                    current = arc.Point;
                }
            }

            Trace.WriteLine($"[GeoAnalyzer] Сырые данные: Линий={lineCount}, Дуг={arcCount}, Сырых вершин={rawVertices.Count}");

            // Упрощаем полигон
            var uniqueVertices = SimplifyPolygon(rawVertices, sinTolerance: 0.05); // ~3 градуса допуска

            Trace.WriteLine($"[GeoAnalyzer] Вершин после упрощения: {uniqueVertices.Count}");
            for (int i = 0; i < uniqueVertices.Count; i++)
            {
                Trace.WriteLine($"  Вершина {i + 1}: X={uniqueVertices[i].X:F2}, Y={uniqueVertices[i].Y:F2}");
            }

            // 1. Если есть дуги и мало прямых линий - это круг
            if (arcCount > 0 && lineCount <= 2)
            {
                Trace.WriteLine("[GeoAnalyzer] Результат: Round (круг)");
                return PartType.Round;
            }

            // 2. Если ровно 3 значимые вершины - это треугольник
            if (uniqueVertices.Count == 3)
            {
                Trace.WriteLine("[GeoAnalyzer] Результат: Triangle (треугольник)");
                return PartType.Triangle;
            }

            // 3. Если 4 вершины, проверяем, является ли он прямоугольником
            if (uniqueVertices.Count == 4)
            {
                if (IsRectangle(uniqueVertices))
                {
                    Trace.WriteLine("[GeoAnalyzer] Результат: Rectangle (прямоугольник)");
                    return PartType.Rectangle;
                }
                else
                {
                    Trace.WriteLine("[GeoAnalyzer] Результат: Rectangle (fallback, 4 вершины, но не прямоугольник)");
                }
            }

            // Fallback
            Trace.WriteLine($"[GeoAnalyzer] Результат: Rectangle (fallback, вершин={uniqueVertices.Count})");
            return PartType.Rectangle;
        }

        private static double GetBoundsArea(PathFigure figure)
        {
            var bounds = new PathGeometry(new[] { figure }).Bounds;
            return bounds.Width * bounds.Height;
        }

        private static double Distance(Point a, Point b)
        {
            return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        }

        private static List<Point> SimplifyPolygon(List<Point> points, double sinTolerance)
        {
            if (points.Count <= 3) return new List<Point>(points);

            var result = new List<Point> { points[0] };

            for (int i = 1; i < points.Count - 1; i++)
            {
                Point pPrev = points[i - 1];
                Point pCurr = points[i];
                Point pNext = points[i + 1];

                // Векторы pPrev->pCurr и pCurr->pNext
                double dx1 = pCurr.X - pPrev.X;
                double dy1 = pCurr.Y - pPrev.Y;
                double dx2 = pNext.X - pCurr.X;
                double dy2 = pNext.Y - pCurr.Y;

                // Модуль векторного произведения
                double crossProduct = Math.Abs(dx1 * dy2 - dy1 * dx2);

                double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                double len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                double lengthProduct = len1 * len2;

                if (lengthProduct > 0)
                {
                    double sinAngle = crossProduct / lengthProduct;

                    // Если sin угла больше допуска, это реальная вершина (излом)
                    if (sinAngle >= sinTolerance)
                    {
                        result.Add(pCurr);
                    }
                }
            }

            // 🔥 КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Удаление дублирующейся замыкающей точки.
            // Если DXF замкнут, последняя точка часто совпадает с первой (A -> B -> C -> A).
            // Мы должны удалить последнюю точку, если она практически идентична первой, 
            // чтобы получить истинное количество вершин (3, а не 4).
            if (result.Count > 2 && Distance(result[0], result[result.Count - 1]) < 0.5)
            {
                result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        private static bool IsRectangle(List<Point> pts)
        {
            if (pts.Count != 4) return false;

            for (int i = 0; i < 4; i++)
            {
                Point p0 = pts[i];
                Point p1 = pts[(i + 1) % 4];
                Point p2 = pts[(i + 2) % 4];

                double dx1 = p0.X - p1.X;
                double dy1 = p0.Y - p1.Y;
                double dx2 = p2.X - p1.X;
                double dy2 = p2.Y - p1.Y;

                double dotProduct = dx1 * dx2 + dy1 * dy2;
                double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                double len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                double lengthProduct = len1 * len2;

                if (lengthProduct > 0)
                {
                    double cosAngle = Math.Abs(dotProduct / lengthProduct);
                    if (cosAngle > 0.1) // Допуск ~6 градусов
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}