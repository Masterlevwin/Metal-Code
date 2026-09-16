using Metal_Code.Utils;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Units;
using System;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Metal_Code
{
    public static class DxfExporter
    {
        /// <summary>
        /// Универсальный экспорт детали в DXF с сохранением истинных дуг и окружностей.
        /// Автоматически извлекает все контуры и отверстия из DisplayGeometry.
        /// </summary>
        public static void Export(Part part, string filePath)
        {
            // 1. Гарантируем наличие актуальной геометрии (включая отверстия, если они есть)
            if (part.DisplayGeometry == null)
            {
                if (part.PartType == PartType.Custom || part.HoleGroups?.Count > 0)
                    PartPreviewGenerator.EnsureDisplayGeometryWithHoles(part);
                else
                    PartPreviewGenerator.EnsureDisplayGeometry(part);
            }

            if (part.DisplayGeometry == null)
                throw new InvalidOperationException("Не удалось сгенерировать геометрию детали.");

            var dxf = new DxfDocument(DxfVersion.AutoCad2010);
            dxf.DrawingVariables.InsUnits = DrawingUnits.Millimeters;

            // 2. Вычисляем смещение, чтобы левый нижний угол ограничивающего прямоугольника был в (0, 0)
            var bounds = part.DisplayGeometry.Bounds;
            double offsetX = bounds.Left;
            double offsetY = bounds.Top;

            // 3. Проходим по всем фигурам (внешний контур + все отверстия)
            foreach (var figure in part.DisplayGeometry.Figures)
            {
                Point currentPoint = figure.StartPoint;

                foreach (var segment in figure.Segments)
                {
                    if (segment is LineSegment lineSeg)
                    {
                        AddDxfLine(dxf, currentPoint, lineSeg.Point, offsetX, offsetY);
                        currentPoint = lineSeg.Point;
                    }
                    else if (segment is ArcSegment arcSeg)
                    {
                        AddDxfArc(dxf, currentPoint, arcSeg, offsetX, offsetY);
                        currentPoint = arcSeg.Point;
                    }
                    else if (segment is PolyLineSegment polySeg)
                    {
                        foreach (var pt in polySeg.Points)
                        {
                            AddDxfLine(dxf, currentPoint, pt, offsetX, offsetY);
                            currentPoint = pt;
                        }
                    }
                }

                // 4. Если фигура замкнута, добавляем замыкающий отрезок к начальной точке
                if (figure.IsClosed)
                {
                    AddDxfLine(dxf, currentPoint, figure.StartPoint, offsetX, offsetY);
                }
            }

            dxf.Save(filePath);
        }

        private static void AddDxfLine(DxfDocument dxf, Point start, Point end, double offsetX, double offsetY)
        {
            // Избегаем создания вырожденных линий нулевой длины
            if (Math.Abs(start.X - end.X) < 0.001 && Math.Abs(start.Y - end.Y) < 0.001)
                return;

            var line = new Line(
                new Vector2(start.X - offsetX, start.Y - offsetY),
                new Vector2(end.X - offsetX, end.Y - offsetY)
            );
            dxf.Entities.Add(line);
        }

        /// <summary>
        /// Надежная конвертация WPF ArcSegment в истинную дугу netDxf.Entities.Arc
        /// Использует геометрический расчет центра, исключающий ошибки схлопывания радиусов.
        /// </summary>
        private static void AddDxfArc(DxfDocument dxf, Point start, ArcSegment arc, double offsetX, double offsetY)
        {
            Point end = arc.Point;
            double radius = arc.Size.Width; // Для лазерной резки Width == Height (окружность)

            double dx = start.X - end.X;
            double dy = start.Y - end.Y;
            double chordLength = Math.Sqrt(dx * dx + dy * dy);

            if (chordLength < 0.001) return; // Вырожденная дуга

            // WPF автоматически масштабирует радиус, если он слишком мал для соединения точек
            double minRadius = chordLength / 2.0;
            if (radius < minRadius)
            {
                radius = minRadius;
            }

            // Середина хорды
            double midX = (start.X + end.X) / 2.0;
            double midY = (start.Y + end.Y) / 2.0;

            // Расстояние от середины хорды до центра окружности
            double h = Math.Sqrt(Math.Max(0, radius * radius - (chordLength / 2.0) * (chordLength / 2.0)));

            // Угол хорды
            double chordAngle = Math.Atan2(end.Y - start.Y, end.X - start.X);

            // Направление к центру: перпендикулярно хорде.
            // Слева от вектора Start->End: +PI/2, Справа: -PI/2
            double centerAngle = chordAngle + (arc.SweepDirection == SweepDirection.Clockwise ? -Math.PI / 2.0 : Math.PI / 2.0);

            // Если это большая дуга (>180 град), центр находится по другую сторону хорды
            if (arc.IsLargeArc)
            {
                centerAngle += Math.PI;
            }

            // Координаты центра
            double cx = midX + h * Math.Cos(centerAngle);
            double cy = midY + h * Math.Sin(centerAngle);

            // Углы начальной и конечной точек относительно найденного центра
            double angleStartRad = Math.Atan2(start.Y - cy, start.X - cx);
            double angleEndRad = Math.Atan2(end.Y - cy, end.X - cx);

            double angleStartDeg = angleStartRad * 180.0 / Math.PI;
            double angleEndDeg = angleEndRad * 180.0 / Math.PI;

            double dxfStart, dxfEnd;

            // netDxf рисует дуги ТОЛЬКО против часовой стрелки (CCW).
            // Чтобы нарисовать дугу по часовой стрелке (CW) в системе CCW, 
            // мы меняем местами начальный и конечный углы.
            if (arc.SweepDirection == SweepDirection.Clockwise)
            {
                dxfStart = angleEndDeg;
                dxfEnd = angleStartDeg;
            }
            else
            {
                dxfStart = angleStartDeg;
                dxfEnd = angleEndDeg;
            }

            // Нормализация углов в диапазон [0, 360)
            dxfStart = (dxfStart % 360 + 360) % 360;
            dxfEnd = (dxfEnd % 360 + 360) % 360;

            // Для корректного рисования CCW в netDxf конечный угол должен быть больше начального
            if (dxfEnd <= dxfStart)
            {
                dxfEnd += 360;
            }

            var dxfArc = new Arc(
                new Vector2(cx - offsetX, cy - offsetY),
                radius,
                dxfStart,
                dxfEnd
            );

            dxf.Entities.Add(dxfArc);
        }
    }
}