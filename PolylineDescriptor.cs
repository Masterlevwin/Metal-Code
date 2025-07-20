using ACadSharp.Entities;
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

        public Brush Stroke { get; set; } = Brushes.Black;
        public double StrokeThickness { get; set; } = 0.5;

        public void Draw(Canvas canvas)
        {
            if (Points.Count == 0 && ArcSegments.Count == 0)
                return;

            var figure = new PathFigure();
            var geometry = new PathGeometry();

            figure.StartPoint = new Point(Points[0].X, Points[0].Y);

            for (int i = 1; i < Points.Count; i++)
            {
                if (i <= ArcSegments.Count)
                {
                    var arc = ArcSegments[i - 1];

                    // Добавляем дуговой сегмент
                    var arcSegment = new ArcSegment(
                        arc.EndPoint,
                        new Size(arc.Radius, arc.Radius),
                        rotationAngle: 0,
                        isLargeArc: arc.IsLargeArc,
                        sweepDirection: arc.SweepDirection,
                        isStroked: true
                    );

                    figure.Segments.Add(arcSegment);
                }
                else
                {
                    // Обычная линия
                    figure.Segments.Add(new LineSegment(Points[i], true));
                }
            }

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

    // Вспомогательная структура для дуг в полилинии
    public class ArcSegmentInfo
    {
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
        public Point Center { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double SweepAngle { get; set; }
        public bool IsLargeArc { get; set; }
        public SweepDirection SweepDirection { get; set; }

        public void Draw(Canvas canvas)
        {
            var figure = new PathFigure
            {
                StartPoint = StartPoint
            };

            figure.Segments.Add(new ArcSegment(
                EndPoint,
                new Size(Radius, Radius),
                rotationAngle: 0,
                isLargeArc: IsLargeArc,
                sweepDirection: SweepDirection,
                isStroked: true
            ));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var path = new Path
            {
                Data = geometry,
                Stroke = Brushes.Red,
                StrokeThickness = 0.5
            };

            canvas.Children.Add(path);
        }
    }

    public static class GeometryConverter
    {
        public static PolylineDescriptor Convert(LwPolyline polyline, double scale, double offsetX, double offsetY)
        {
            var descriptor = new PolylineDescriptor();

            foreach (var vertex in polyline.Vertices)
            {
                // Применяем масштаб и инверсию Y
                double x = vertex.Location.X * scale + offsetX;
                double y = -vertex.Location.Y * scale + offsetY;

                descriptor.Points.Add(new Point(x, y));
            }

            for (int i = 0; i < polyline.Vertices.Count - 1; i++)
            {
                var start = polyline.Vertices[i];
                var end = polyline.Vertices[i + 1];
                double bulge = end.Bulge;

                if (Math.Abs(bulge) > 0.001)
                {
                    var arcInfo = CalculateArcFromBulge(
                        new Point(start.Location.X, start.Location.Y),
                        new Point(end.Location.X, end.Location.Y),
                        bulge,
                        scale,
                        offsetX,
                        offsetY
                    );

                    if (arcInfo != null)
                    {
                        descriptor.ArcSegments.Add(arcInfo);
                    }
                }
            }

            return descriptor;
        }

        private static ArcSegmentInfo CalculateArcFromBulge(Point startPoint, Point endPoint, double bulge, double scale, double offsetX, double offsetY)
        {
            //if (bulge == 0)
            //    return null;

            // Расстояние между точками
            double chordLength = Math.Sqrt(Math.Pow(endPoint.X - startPoint.X, 2) + Math.Pow(endPoint.Y - startPoint.Y, 2));
            //if (chordLength < 0.001)
            //    return null;

            // Угол дуги
            double angle = 4 * Math.Atan(bulge);
            bool isLargeArc = Math.Abs(angle) > Math.PI;

            // Радиус
            double radius = chordLength / (2 * Math.Sin(Math.Abs(angle) / 2));
            double sagitta = radius * (1 - Math.Cos(Math.Abs(angle) / 2));
            double direction = Math.Sign(bulge);

            // Средняя точка хорды
            Point chordMidpoint = new Point((startPoint.X + endPoint.X) / 2, (startPoint.Y + endPoint.Y) / 2);

            // Перпендикулярный вектор
            Point chordVector = new Point(endPoint.X - startPoint.X, endPoint.Y - startPoint.Y);
            Point perpendicular = new Point(-chordVector.Y, chordVector.X);
            double length = Math.Sqrt(perpendicular.X * perpendicular.X + perpendicular.Y * perpendicular.Y);
            if (length > 0)
            {
                perpendicular.X /= length;
                perpendicular.Y /= length;
            }

            // Центр дуги
            Point center = new Point(
                chordMidpoint.X + perpendicular.X * sagitta * direction,
                chordMidpoint.Y + perpendicular.Y * sagitta * direction
            );

            // Применяем масштаб и инверсию Y
            double scaledStartX = startPoint.X * scale + offsetX;
            double scaledStartY = -startPoint.Y * scale + offsetY;

            double scaledEndX = endPoint.X * scale + offsetX;
            double scaledEndY = -endPoint.Y * scale + offsetY;

            double scaledCenterX = center.X * scale + offsetX;
            double scaledCenterY = -center.Y * scale + offsetY;

            double scaledRadius = radius * scale;

            // Начальный и конечный угол
            double startAngle = Math.Atan2(scaledStartY - scaledCenterY, scaledStartX - scaledCenterX);
            double endAngle = Math.Atan2(scaledEndY - scaledCenterY, scaledEndX - scaledCenterX);

            // Определяем направление дуги
            SweepDirection sweepDirection = (bulge > 0) ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

            return new ArcSegmentInfo
            {
                StartPoint = new Point(scaledStartX, scaledStartY),
                EndPoint = new Point(scaledEndX, scaledEndY),
                Center = new Point(scaledCenterX, scaledCenterY),
                Radius = scaledRadius,
                StartAngle = startAngle,
                SweepAngle = endAngle - startAngle,
                IsLargeArc = isLargeArc,
                SweepDirection = sweepDirection
            };
        }
    }
}