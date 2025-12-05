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
            Rect partBounds,                 // границы детали (в DXF-координатах)
            string fontFamily = "GOST type A",
            string layerName = "Engraving")
        {
            if (!doc.Layers.TryGetValue(layerName, out Layer layer))
            {
                layer = new Layer(layerName);
                doc.Layers.Add(layer);
            }

            // 1. Вычисляем оптимальный размер шрифта
            double fontSize = CalculateOptimalFontSize(text, fontFamily, partBounds);

            // 2. Вычисляем позицию для центрирования
            var positionWpf = GetCenteredTextPosition(text, fontFamily, fontSize, partBounds);

            // 3. Генерируем геометрию
            var contours = TextToPathGeometries(text, fontFamily, fontSize, positionWpf);

            foreach (var contour in contours)
            {
                if (contour.Count < 2) continue;

                var vertices = new List<LwPolyline.Vertex>();
                foreach (var pt in contour)
                {
                    float x = (float)pt.X;
                    float y = (float)-pt.Y; // инверсия Y: WPF → DXF

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
                    ConstantWidth = 0.0f
                };

                doc.Entities.Add(polyline);
            }
        }

        public static List<List<Point>> TextToPathGeometries(string text, string fontFamily, double fontSize, Point origin)
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return new List<List<Point>>();

            // 1. Измеряем все строки и находим максимальную ширину
            var formattedLines = new List<FormattedText>();
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
                ft.TextAlignment = TextAlignment.Center;
                formattedLines.Add(ft);
                maxWidth = Math.Max(maxWidth, ft.WidthIncludingTrailingWhitespace);
            }

            // 2. Генерируем геометрию
            var allContours = new List<List<Point>>();
            double currentY = origin.Y;

            for (int i = 0; i < formattedLines.Count; i++)
            {
                var ft = formattedLines[i];
                // Горизонтальное смещение для центрирования строки относительно maxWidth
                double offsetX = (maxWidth - ft.WidthIncludingTrailingWhitespace) / 2.0;
                Point lineOrigin = new Point(origin.X + offsetX, currentY);

                var geometry = ft.BuildGeometry(lineOrigin);
                var flattened = geometry.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute);

                foreach (PathFigure figure in flattened.Figures)
                {
                    var points = new List<Point> { figure.StartPoint };
                    foreach (PathSegment seg in figure.Segments)
                    {
                        if (seg is LineSegment l) points.Add(l.Point);
                        else if (seg is PolyLineSegment p) points.AddRange(p.Points);
                    }
                    if (figure.IsClosed && points.Count > 1)
                        points.Add(figure.StartPoint);
                    allContours.Add(points);
                }

                currentY += ft.Height;
            }

            return allContours;
        }

        // Метод для измерения размера текста
        public static Size MeasureText(string text, string fontFamily, double fontSize)
        {
            var formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily),
                fontSize,
                Brushes.Transparent,
                1.0
            );
            return new Size(formattedText.Width, formattedText.Height);
        }

        // Метод подбора оптимального размера шрифта
        public static double CalculateOptimalFontSize(string text, string fontFamily, Rect area, double maxFontSize = 20.0, double minFontSize = 10.0)
        {
            const double paddingRatio = 0.8; // 80% от размера детали
            double targetWidth = area.Width * paddingRatio;
            double targetHeight = area.Height * paddingRatio;

            // Простой итеративный подбор (можно заменить на бинарный поиск)
            for (double size = maxFontSize; size >= minFontSize; size -= 0.5)
            {
                var sizeText = MeasureText(text, fontFamily, size);
                if (sizeText.Width <= targetWidth && sizeText.Height <= targetHeight)
                    return size;
            }
            return minFontSize; // fallback
        }

        // Центрирование: вычисление позиции текста
        public static Point GetCenteredTextPosition(string text, string fontFamily, double fontSize, Rect partBounds)
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return new Point(partBounds.X, partBounds.Y);

            // Измеряем каждую строку
            double maxWidth = 0;
            double totalHeight = 0;
            var lineHeights = new List<double>();

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
                ft.TextAlignment = TextAlignment.Center; // важно для внутреннего центрирования глифов
                maxWidth = Math.Max(maxWidth, ft.WidthIncludingTrailingWhitespace);
                lineHeights.Add(ft.Height);
                totalHeight += ft.Height;
            }

            // Центр детали
            double centerX = partBounds.Left + partBounds.Width / 2.0;
            double centerY = partBounds.Top + partBounds.Height / 2.0;

            // Верхний левый угол текстового блока
            double blockLeft = centerX - maxWidth / 2.0;
            double blockTop = centerY - totalHeight / 2.0;

            return new Point(blockLeft, blockTop);
        }
    }
}