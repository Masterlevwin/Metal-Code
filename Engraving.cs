using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Metal_Code
{
    public static class Engraving
    {
        public static void AddEngravingAsPolylines(
            CadDocument doc,
            string text,
            Rect partBounds,
            string fontFamily = "Danger",
            double? fixedSize = null)
        {
            if (!doc.Layers.TryGetValue("MARK", out Layer layer))
            {
                layer = new Layer("MARK");
                doc.Layers.Add(layer);
            }

            // Если пользователь задал фиксированный размер — используем его, иначе — подбираем автоматически
            double fontSize = fixedSize ?? CalculateOptimalFontSize(text, fontFamily, partBounds, maxFontSize: 30, minFontSize: 10);

            Point origin = GetCenteredTextPosition(text, fontFamily, fontSize, partBounds);

            var contours = TextToPathGeometries(text, fontFamily, fontSize, origin);

            foreach (var contour in contours)
            {
                if (contour.Count < 2) continue;

                var vertices = new List<LwPolyline.Vertex>();
                foreach (var pt in contour)
                {
                    float x = (float)pt.X;
                    float y = (float)-pt.Y;

                    if (!float.IsFinite(x) || !float.IsFinite(y)) continue;
                    vertices.Add(new LwPolyline.Vertex(new XY(x, y)));
                }

                if (vertices.Count < 2) continue;

                bool isClosed = contour.Count > 2 &&
                                 Math.Abs(contour[0].X - contour[^1].X) < 1e-3 &&
                                 Math.Abs(contour[0].Y - contour[^1].Y) < 1e-3;

                var polyline = new LwPolyline(vertices)
                {
                    Layer = layer,
                    IsClosed = isClosed,
                    ConstantWidth = 0.0f,
                    Color = new ACadSharp.Color(100)
                };

                doc.Entities.Add(polyline);
            }
        }

        public static List<List<Point>> TextToPathGeometries(string text, string? fontFamily, double fontSize, Point origin)
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return new List<List<Point>>();

            // 1. Измеряем ВСЕ строки и находим максимальную ширину
            var lineGeometries = new List<FormattedText>();
            double maxWidth = 0;

            foreach (var line in lines)
            {
                var ft = new FormattedText(
                    line,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(fontFamily),
                    fontSize,
                    Brushes.Black,
                    1.0
                );

                lineGeometries.Add(ft);
                maxWidth = Math.Max(maxWidth, ft.Width);
            }

            // 2. Генерируем геометрию
            var allContours = new List<List<Point>>();
            double currentY = origin.Y;

            for (int i = 0; i < lineGeometries.Count; i++)
            {
                var ft = lineGeometries[i];

                // Центрируем строку: левый край блока + (максШирина - ширина строки) / 2
                double offsetX = (maxWidth - ft.Width) / 2.0;
                Point lineOrigin = new(origin.X + offsetX, currentY);

                var geometry = ft.BuildGeometry(lineOrigin);
                var flattened = geometry.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute);

                foreach (PathFigure figure in flattened.Figures)
                {
                    var points = new List<Point> { figure.StartPoint };

                    foreach (PathSegment seg in figure.Segments)
                    {
                        if (seg is LineSegment line)
                            points.Add(line.Point);
                        else if (seg is PolyLineSegment poly)
                            points.AddRange(poly.Points);
                    }

                    if (figure.IsClosed && points.Count > 1)
                        points.Add(figure.StartPoint);

                    allContours.Add(points);
                }

                currentY += ft.Height;
            }

            return allContours;
        }

        public static Size MeasureMultilineText(string text, string fontFamily, double fontSize)
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return new Size(0, 0);

            double maxWidth = 0;
            double totalHeight = 0;

            foreach (var line in lines)
            {
                var ft = new FormattedText(line, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(fontFamily), fontSize, Brushes.Transparent, 1.0);
                maxWidth = Math.Max(maxWidth, ft.Width);
                totalHeight += ft.Height;
            }

            return new Size(maxWidth, totalHeight);
        }

        public static double CalculateOptimalFontSize(
            string text,
            string fontFamily,
            Rect area,
            double maxFontSize = 30.0,
            double minFontSize = 10.0)
        {
            const double paddingRatio = 0.8;
            double targetWidth = area.Width * paddingRatio;
            double targetHeight = area.Height * paddingRatio;

            for (double size = maxFontSize; size >= minFontSize; size -= 0.5)
            {
                var sizeText = MeasureMultilineText(text, fontFamily, size);
                if (sizeText.Width <= targetWidth && sizeText.Height <= targetHeight)
                    return size;
            }

            return minFontSize;
        }

        public static Point GetCenteredTextPosition(string text, string fontFamily, double fontSize, Rect area)
        {
            var size = MeasureMultilineText(text, fontFamily, fontSize);
            double x = area.Left + (area.Width - size.Width) / 2.0;
            double y = area.Top + (area.Height - size.Height) / 2.0;

            return new Point(x, y);
        }
    }
}