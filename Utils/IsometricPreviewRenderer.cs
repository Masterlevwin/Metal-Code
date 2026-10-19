using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Metal_Code.Models;

namespace Metal_Code.Utils
{
    /// <summary>
    /// Рендерер изометрического превью собранной детали.
    /// Модель: объединение двух ортогональных экструзий нарисованных профилей.
    /// </summary>
    public static class IsometricPreviewRenderer
    {
        #region 3D-математика

        private struct V3
        {
            public double X, Y, Z;
            public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
            public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static V3 operator *(V3 a, double k) => new V3(a.X * k, a.Y * k, a.Z * k);
            public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
            public V3 Normalized()
            {
                double len = Length;
                return len < 1e-9 ? new V3(0, 0, 0) : this * (1.0 / len);
            }
            public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            public static V3 Cross(V3 a, V3 b) => new V3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }

        private sealed class Face
        {
            public V3[] Points = null!;
            public Color Color;
            public double Depth;
        }

        private const double Cos30 = 0.8660254037844386;

        private static Point Project(V3 v)
            => new Point((v.X - v.Y) * Cos30, (v.X + v.Y) * 0.5 - v.Z);

        private static readonly V3 LightDirection = new V3(0.5, 0.3, 1.0).Normalized();
        private static readonly V3 CameraDirection = new V3(1, 1, 1).Normalized();
        private static readonly Color BaseColor = Color.FromRgb(150, 178, 205);
        private static readonly Brush EdgeBrush = CreateFrozenBrush(Color.FromRgb(58, 76, 94));

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        #endregion

        #region Построение геометрии

        private static List<V3>? NormalizeSection(BendProfileDrawing? profile)
        {
            if (profile == null || !profile.IsFinished || profile.Points == null || profile.Points.Count < 2)
                return null;

            var raw = new List<V3>();
            foreach (Point p in profile.Points)
            {
                var v = new V3(p.X, 0, -p.Y);
                if (raw.Count > 0)
                {
                    V3 last = raw[raw.Count - 1];
                    if (Math.Abs(last.X - v.X) < 0.01 && Math.Abs(last.Z - v.Z) < 0.01) continue;
                }
                raw.Add(v);
            }

            if (raw.Count < 2) return null;
            double minU = raw.Min(v => v.X);
            double minH = raw.Min(v => v.Z);
            return raw.Select(v => new V3(v.X - minU, 0, v.Z - minH)).ToList();
        }

        private static double SpanU(List<V3> s) => s.Max(v => v.X);
        private static double SpanH(List<V3> s) => s.Max(v => v.Z);

        private static void AddFace(List<Face> faces, V3 p1, V3 p2, V3 p3, V3 p4)
        {
            V3 n = V3.Cross(p2 - p1, p3 - p1);
            if (n.Length < 1e-6) return;
            n = n.Normalized();
            if (V3.Dot(n, CameraDirection) < 0) n = n * -1.0;

            double diffuse = Math.Max(0.0, V3.Dot(n, LightDirection));
            double intensity = 0.38 + 0.62 * diffuse;

            var color = Color.FromRgb(
                (byte)Math.Clamp(BaseColor.R * intensity, 0, 255),
                (byte)Math.Clamp(BaseColor.G * intensity, 0, 255),
                (byte)Math.Clamp(BaseColor.B * intensity, 0, 255));

            double depth = (p1.X + p1.Y + p1.Z + p2.X + p2.Y + p2.Z +
                            p3.X + p3.Y + p3.Z + p4.X + p4.Y + p4.Z) / 4.0;

            faces.Add(new Face { Points = new[] { p1, p2, p3, p4 }, Color = color, Depth = depth });
        }

        private static void AddExtrusionX(List<Face> faces, List<V3> section, double depthY)
        {
            for (int i = 0; i < section.Count - 1; i++)
            {
                V3 a = section[i], b = section[i + 1];
                AddFace(faces,
                    new V3(a.X, 0, a.Z), new V3(b.X, 0, b.Z),
                    new V3(b.X, depthY, b.Z), new V3(a.X, depthY, a.Z));
            }
        }

        private static void AddExtrusionY(List<Face> faces, List<V3> section, double widthX)
        {
            for (int i = 0; i < section.Count - 1; i++)
            {
                V3 a = section[i], b = section[i + 1];
                AddFace(faces,
                    new V3(0, a.X, a.Z), new V3(0, b.X, b.Z),
                    new V3(widthX, b.X, b.Z), new V3(widthX, a.X, a.Z));
            }
        }

        private static void AddBox(List<Face> faces, double w, double d, double h)
        {
            var p000 = new V3(0, 0, 0); var p100 = new V3(w, 0, 0);
            var p110 = new V3(w, d, 0); var p010 = new V3(0, d, 0);
            var p001 = new V3(0, 0, h); var p101 = new V3(w, 0, h);
            var p111 = new V3(w, d, h); var p011 = new V3(0, d, h);
            AddFace(faces, p001, p101, p111, p011);
            AddFace(faces, p000, p010, p110, p100);
            AddFace(faces, p100, p101, p111, p110);
            AddFace(faces, p000, p010, p011, p001);
            AddFace(faces, p010, p011, p111, p110);
            AddFace(faces, p000, p100, p101, p001);
        }

        private static List<V3> MakeEndEdgeX(List<V3> section, double y)
            => section.Select(v => new V3(v.X, y, v.Z)).ToList();
        private static List<V3> MakeEndEdgeY(List<V3> section, double x)
            => section.Select(v => new V3(x, v.X, v.Z)).ToList();

        private struct GeometryResult
        {
            public List<Face> Faces;
            public List<List<V3>> EndEdges;
            public double FormedW, FormedD, FormedH;
        }

        private static GeometryResult BuildGeometry(
            double flatWidth, double flatHeight, double thickness,
            BendProfileDrawing? profileX, BendProfileDrawing? profileY)
        {
            var result = new GeometryResult
            {
                Faces = new List<Face>(),
                EndEdges = new List<List<V3>>()
            };

            var secX = NormalizeSection(profileX);
            var secY = NormalizeSection(profileY);
            double t = Math.Max(thickness, 0.5);

            if (secX == null && secY == null)
            {
                AddBox(result.Faces, flatWidth, flatHeight, t);
                result.FormedW = flatWidth; result.FormedD = flatHeight; result.FormedH = t;
            }
            else if (secX != null && secY == null)
            {
                AddExtrusionX(result.Faces, secX, flatHeight);
                result.EndEdges.Add(MakeEndEdgeX(secX, flatHeight));
                result.FormedW = SpanU(secX); result.FormedD = flatHeight;
                result.FormedH = Math.Max(SpanH(secX), t);
            }
            else if (secX == null && secY != null)
            {
                AddExtrusionY(result.Faces, secY, flatWidth);
                result.EndEdges.Add(MakeEndEdgeY(secY, flatWidth));
                result.FormedW = flatWidth; result.FormedD = SpanU(secY);
                result.FormedH = Math.Max(SpanH(secY), t);
            }
            else
            {
                double spanX = SpanU(secX!);
                double spanY = SpanU(secY!);
                AddExtrusionX(result.Faces, secX!, spanY);
                AddExtrusionY(result.Faces, secY!, spanX);
                result.EndEdges.Add(MakeEndEdgeX(secX!, spanY));
                result.EndEdges.Add(MakeEndEdgeY(secY!, spanX));
                result.FormedW = spanX; result.FormedD = spanY;
                result.FormedH = Math.Max(Math.Max(SpanH(secX!), SpanH(secY!)), t);
            }
            return result;
        }

        #endregion

        #region Публичный API

        /// <summary>
        /// Рисует изометрию в заданный канвас, заполняя его целиком.
        /// </summary>
        public static void RenderInto(
            Canvas canvas,
            double flatWidth, double flatHeight, double thickness,
            BendProfileDrawing? profileX, BendProfileDrawing? profileY)
        {
            if (canvas == null) return;
            canvas.Children.Clear();

            double cw = canvas.ActualWidth;
            double ch = canvas.ActualHeight;
            if (cw <= 0 || ch <= 0 || flatWidth <= 0 || flatHeight <= 0) return;

            var geo = BuildGeometry(flatWidth, flatHeight, thickness, profileX, profileY);
            if (geo.Faces.Count == 0) return;

            const double pad = 6;
            const double labelH = 16;
            double availW = Math.Max(cw - pad * 2, 10);
            double availH = Math.Max(ch - pad - labelH, 10);

            var allProjected = new List<Point>();
            foreach (var f in geo.Faces)
                foreach (var v in f.Points) allProjected.Add(Project(v));
            foreach (var edge in geo.EndEdges)
                foreach (var v in edge) allProjected.Add(Project(v));

            double minX = allProjected.Min(p => p.X);
            double maxX = allProjected.Max(p => p.X);
            double minY = allProjected.Min(p => p.Y);
            double maxY = allProjected.Max(p => p.Y);
            double bw = Math.Max(maxX - minX, 0.001);
            double bh = Math.Max(maxY - minY, 0.001);

            double fit = Math.Min(availW / bw, availH / bh);
            double offX = pad + (availW - bw * fit) / 2 - minX * fit;
            double offY = pad + (availH - bh * fit) / 2 - minY * fit;

            Point Map(V3 v)
            {
                var p = Project(v);
                return new Point(p.X * fit + offX, p.Y * fit + offY);
            }

            foreach (var f in geo.Faces.OrderBy(f => f.Depth))
            {
                var polygon = new Polygon
                {
                    Fill = new SolidColorBrush(f.Color),
                    Stroke = EdgeBrush,
                    StrokeThickness = 0.8,
                    StrokeLineJoin = PenLineJoin.Round
                };
                foreach (var v in f.Points) polygon.Points.Add(Map(v));
                canvas.Children.Add(polygon);
            }

            double edgeThickness = Math.Clamp(thickness * fit, 1.0, 3.0);
            foreach (var edge in geo.EndEdges)
            {
                if (edge.Count < 2) continue;
                var polyline = new Polyline
                {
                    Stroke = EdgeBrush,
                    StrokeThickness = edgeThickness,
                    StrokeLineJoin = PenLineJoin.Bevel,
                    StrokeStartLineCap = PenLineCap.Flat,
                    StrokeEndLineCap = PenLineCap.Flat
                };
                foreach (var v in edge) polyline.Points.Add(Map(v));
                canvas.Children.Add(polyline);
            }

            var dims = new TextBlock
            {
                Text = $"≈ {geo.FormedW:0.#} × {geo.FormedD:0.#} × {geo.FormedH:0.#} мм",
                FontSize = 9,
                Foreground = Brushes.DimGray
            };
            dims.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(dims, (cw - dims.DesiredSize.Width) / 2);
            Canvas.SetTop(dims, ch - dims.DesiredSize.Height - 1);
            canvas.Children.Add(dims);
        }

        #endregion
    }
}