using ACadSharp.Entities;
using CSMath;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;

namespace Metal_Code
{
    public class PolylineDescriptor : IGeometryDescriptor
    {
        public List<Point> Points { get; set; } = new();
        public List<ArcSegmentInfo> ArcSegments { get; set; } = new();

        public bool IsClosed { get; set; }

        public Brush Stroke { get; set; } = Brushes.Black;
        public double StrokeThickness { get; set; } = 0.5;

        public void Draw(Canvas canvas)
        {
            if (Points.Count == 0 && ArcSegments.Count == 0)
                return;

            var pathFigure = new PathFigure();

            if (Points.Count > 0)
            {
                pathFigure.StartPoint = Points[0];

                int i = 1;
                foreach (var arc in ArcSegments)
                {
                    if (i < Points.Count)
                    {
                        // Линейный сегмент до дуги
                        pathFigure.Segments.Add(new LineSegment(Points[i++], true));
                    }

                    // Дуговой сегмент
                    pathFigure.Segments.Add(new ArcSegment(
                        arc.Point,
                        arc.Size,
                        arc.RotationAngle,
                        arc.IsLargeArc,
                        arc.SweepDirection,
                        true));
                }

                // Оставшиеся линейные сегменты
                for (; i < Points.Count; i++)
                {
                    pathFigure.Segments.Add(new LineSegment(Points[i], true));
                }
            }

            // Замыкание полилинии, если нужно
            if (IsClosed && Points.Count > 1)
            {
                pathFigure.Segments.Add(new LineSegment(Points[0], true));
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

    // Вспомогательная структура для дуг в полилинии
    public class ArcSegmentInfo
    {
        public Point Point { get; set; }
        public Size Size { get; set; }
        public double RotationAngle { get; set; }
        public bool IsLargeArc { get; set; }
        public SweepDirection SweepDirection { get; set; }
    }


    public static class GeometryConverter
    {
        public static PolylineDescriptor Convert(LwPolyline polyline, double scale, double offsetX, double offsetY)
        {
            var descriptor = new PolylineDescriptor
            {
                IsClosed = polyline.IsClosed,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5
            };

            foreach (var vertex in polyline.Vertices)
            {
                // Просто добавляем точки полилинии
                var point = new Point(
                    vertex.Location.X * scale + offsetX,
                    -vertex.Location.Y * scale + offsetY);

                descriptor.Points.Add(point);
            }

            // Если есть дуговые сегменты — добавляем их отдельно
            for (int i = 0; i < polyline.Vertices.Count - 1; i++)
            {
                var current = polyline.Vertices[i];
                var next = polyline.Vertices[i + 1];

                Point currentStart = Transform(current.Location, scale, offsetX, offsetY);
                Point nextStart = Transform(next.Location, scale, offsetX, offsetY);

                if (next.Bulge != 0) // Bulge != 0 → дуга
                {
                    // Рассчитываем дуговой сегмент из Bulge
                    var arc = CalculateArcFromBulge(currentStart, nextStart, next.Bulge);
                    var arcInfo = new ArcSegmentInfo
                    {
                        Point = new Point(
                            arc.EndPoint.X * scale + offsetX,
                            -arc.EndPoint.Y * scale + offsetY),
                        Size = new Size(arc.Radius * scale, arc.Radius * scale),
                        RotationAngle = arc.Angle,
                        IsLargeArc = arc.IsLargeArc,
                        SweepDirection = arc.SweepDirection
                    };

                    descriptor.ArcSegments.Add(arcInfo);
                }
            }

            return descriptor;
        }

        private static ArcInfo CalculateArcFromBulge(Point start, Point end, double bulge)
        {
            // Bulge = tan(θ/4), где θ — угол дуги
            double chordLength = Math.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y));
            double theta = 4 * Math.Atan(bulge);
            double radius = chordLength / (2 * Math.Sin(theta / 2));
            double sagitta = radius * (1 - Math.Cos(theta / 2));
            double direction = Math.Sign(bulge);

            // Центр дуги
            var chordMidpoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            var chordVector = new Point(end.X - start.X, end.Y - start.Y);
            var perpendicular = new Point(-chordVector.Y, chordVector.X);
            double length = Math.Sqrt(perpendicular.X * perpendicular.X + perpendicular.Y * perpendicular.Y);
            perpendicular.X /= length;
            perpendicular.Y /= length;

            var center = new Point(
                chordMidpoint.X + perpendicular.X * sagitta * direction,
                chordMidpoint.Y + perpendicular.Y * sagitta * direction
            );

            // Рассчитываем дугу
            var arc = new ArcInfo
            {
                StartPoint = start,
                EndPoint = end,
                Center = center,
                Radius = radius,
                Angle = 0,
                IsLargeArc = theta > Math.PI
            };

            // Определяем направление дуги
            arc.SweepDirection = (bulge > 0) ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

            return arc;
        }

        //вспомогательные методы
        private static Point Transform(XY point, double scale, double offsetX, double offsetY)
        {
            return new Point(point.X * scale + offsetX, point.Y * scale + offsetY);
        }
    }

    internal class ArcInfo
    {
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
        public Point Center { get; set; }
        public double Radius { get; set; }
        public double Angle { get; set; }
        public bool IsLargeArc { get; set; }
        public SweepDirection SweepDirection { get; set; }
    }
}