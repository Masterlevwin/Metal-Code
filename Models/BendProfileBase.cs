using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code.Models
{
    // ═══════════════════════════════════════════════════════
    //  Параметр профиля (полка, основание, загиб)
    // ═══════════════════════════════════════════════════════
    public class ProfileParameter : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private double _value;
        public string Name { get; }
        public string Description { get; }

        public double Value
        {
            get => _value;
            set
            {
                if (Math.Abs(_value - value) > 0.01)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        public ProfileParameter(string name, string description, double defaultValue = 50)
        {
            Name = name;
            Description = description;
            _value = defaultValue;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Базовый класс гибочного профиля
    // ═══════════════════════════════════════════════════════
    public abstract class BendProfileBase
    {
        public event Action? ParametersChanged;

        public abstract string DisplayName { get; }
        public abstract int BendsCount { get; }

        protected readonly List<ProfileParameter> _parameters = new();

        protected void RegisterParameter(ProfileParameter p)
        {
            p.PropertyChanged += (_, _) => ParametersChanged?.Invoke();
            _parameters.Add(p);
        }

        public List<ProfileParameter> GetParameters() => _parameters;

        /// <summary>Длина развертки по данному профилю</summary>
        public abstract double CalculateFlatLength(double bendDeduction);

        /// <summary>Позиции линий гибки на развертке (для отрисовки)</summary>
        public abstract List<double> GetBendPositions();

        /// <summary>Информация о гибах: (размер полки, кол-во гибов, длина гибки)</summary>
        public abstract List<(double ShelfSize, int Count, double BendLength)> GetBendInfo(double crossLength);

        /// <summary>Отрисовка профиля на мини-канвасе</summary>
        public abstract void DrawOnCanvas(Canvas canvas, double W, double H);
    }

    // ═══════════════════════════════════════════════════════
    //  Нет гибки
    // ═══════════════════════════════════════════════════════
    public class NoneProfile : BendProfileBase
    {
        public override string DisplayName => "Нет";
        public override int BendsCount => 0;

        public override double CalculateFlatLength(double bd) => 0;
        public override List<double> GetBendPositions() => new();
        public override List<(double, int, double)> GetBendInfo(double crossLength) => new();

        public override void DrawOnCanvas(Canvas canvas, double W, double H) { }
    }

    // ═══════════════════════════════════════════════════════
    //  УГОЛОК (L) — 1 гиб, 2 полки
    // ═══════════════════════════════════════════════════════
    public class AngleProfile : BendProfileBase
    {
        public override string DisplayName => "Уголок (L)";
        public override int BendsCount => 1;

        public ProfileParameter Shelf1 { get; }
        public ProfileParameter Shelf2 { get; }

        public AngleProfile()
        {
            Shelf1 = new ProfileParameter("Полка 1", "Полка 1", 40);
            Shelf2 = new ProfileParameter("Полка 2", "Полка 2", 50);
            RegisterParameter(Shelf1);
            RegisterParameter(Shelf2);
        }

        public override double CalculateFlatLength(double bd)
            => Shelf1.Value + Shelf2.Value - bd;

        public override List<double> GetBendPositions()
            => new() { Shelf1.Value };

        public override List<(double, int, double)> GetBendInfo(double crossLength)
            => new() { (Shelf1.Value, 1, crossLength) };

        public override void DrawOnCanvas(Canvas canvas, double W, double H)
        {
            double s1 = Shelf1.Value, s2 = Shelf2.Value;
            if (s1 <= 0 && s2 <= 0) return;

            double maxLen = Math.Max(s1, s2);
            double scale = Math.Min(W * 0.6, H * 0.6) / Math.Max(maxLen, 1);

            var points = new PointCollection
            {
                new(W / 2 - s1 * scale / 2, H / 2 - s2 * scale),
                new(W / 2 - s1 * scale / 2, H / 2),
                new(W / 2 - s1 * scale / 2 + s1 * scale, H / 2)
            };

            ProfileDrawingHelper.DrawProfileLine(canvas, points);
            ProfileDrawingHelper.DrawDimension(canvas, points[0], points[1], $"{s1:0}", true);
            ProfileDrawingHelper.DrawDimension(canvas, points[1], points[2], $"{s2:0}", false);
            ProfileDrawingHelper.DrawBendMarker(canvas, points[1]);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  П-ОБРАЗНЫЙ / КОРОБКА (U) — 2 гиба
    // ═══════════════════════════════════════════════════════
    public class UShapeProfile : BendProfileBase
    {
        public override string DisplayName => "П-образный (коробка)";
        public override int BendsCount => 2;

        public ProfileParameter Base { get; }
        public ProfileParameter Shelf { get; }

        public UShapeProfile()
        {
            Base = new ProfileParameter("Основание", "Основание", 100);
            Shelf = new ProfileParameter("Полка", "Полка", 30);
            RegisterParameter(Base);
            RegisterParameter(Shelf);
        }

        public override double CalculateFlatLength(double bd)
            => Base.Value + 2 * Shelf.Value - 2 * bd;

        public override List<double> GetBendPositions()
        {
            double flat = CalculateFlatLength(0); // без вычета для позиции
            return new() { Shelf.Value, flat - Shelf.Value };
        }

        public override List<(double, int, double)> GetBendInfo(double crossLength)
            => new() { (Shelf.Value, 2, crossLength) };

        public override void DrawOnCanvas(Canvas canvas, double W, double H)
        {
            double b = Base.Value, s = Shelf.Value;
            if (b <= 0 && s <= 0) return;

            double maxLen = Math.Max(b, s);
            double scale = Math.Min(W * 0.6, H * 0.6) / Math.Max(maxLen, 1);

            var points = new PointCollection
            {
                new(W / 2 - b * scale / 2, H / 2 - s * scale),
                new(W / 2 - b * scale / 2, H / 2),
                new(W / 2 + b * scale / 2, H / 2),
                new(W / 2 + b * scale / 2, H / 2 - s * scale)
            };

            ProfileDrawingHelper.DrawProfileLine(canvas, points);
            ProfileDrawingHelper.DrawDimension(canvas, points[0], points[1], $"{s:0}", true);
            ProfileDrawingHelper.DrawDimension(canvas, points[1], points[2], $"{b:0}", false);
            ProfileDrawingHelper.DrawDimension(canvas, points[2], points[3], $"{s:0}", true);
            ProfileDrawingHelper.DrawBendMarker(canvas, points[1]);
            ProfileDrawingHelper.DrawBendMarker(canvas, points[2]);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  ШВЕЛЛЕР (C) — 4 гиба
    // ═══════════════════════════════════════════════════════
    public class ChannelProfile : BendProfileBase
    {
        public override string DisplayName => "Швеллер (C)";
        public override int BendsCount => 4;

        public ProfileParameter Web { get; }
        public ProfileParameter Shelf { get; }
        public ProfileParameter Edge { get; }

        public ChannelProfile()
        {
            Web = new ProfileParameter("Стенка", "Стенка", 80);
            Shelf = new ProfileParameter("Полка", "Полка", 40);
            Edge = new ProfileParameter("Загиб", "Загиб", 15);
            RegisterParameter(Web);
            RegisterParameter(Shelf);
            RegisterParameter(Edge);
        }

        public override double CalculateFlatLength(double bd)
            => Web.Value + 2 * Shelf.Value + 2 * Edge.Value - 4 * bd;

        public override List<double> GetBendPositions()
        {
            double e = Edge.Value, s = Shelf.Value, w = Web.Value;
            return new() { e, e + s, e + s + w, e + s + w + s };
        }

        public override List<(double, int, double)> GetBendInfo(double crossLength)
            => new()
            {
                (Shelf.Value, 2, crossLength),
                (Edge.Value, 2, crossLength)
            };

        public override void DrawOnCanvas(Canvas canvas, double W, double H)
        {
            double w = Web.Value, s = Shelf.Value, e = Edge.Value;
            if (w <= 0 && s <= 0) return;

            double maxLen = Math.Max(w, Math.Max(s, e));
            double scale = Math.Min(W * 0.6, H * 0.6) / Math.Max(maxLen, 1);

            var points = new PointCollection
            {
                new(W / 2 - w * scale / 2, H / 2 - s * scale + e * scale),
                new(W / 2 - w * scale / 2, H / 2 - s * scale),
                new(W / 2 - w * scale / 2 + s * scale, H / 2 - s * scale),
                new(W / 2 - w * scale / 2 + s * scale, H / 2 + s * scale),
                new(W / 2 - w * scale / 2, H / 2 + s * scale),
                new(W / 2 - w * scale / 2, H / 2 + s * scale - e * scale)
            };

            ProfileDrawingHelper.DrawProfileLine(canvas, points);
            ProfileDrawingHelper.DrawDimension(canvas, points[1], points[2], $"{s:0}", false);
            ProfileDrawingHelper.DrawDimension(canvas, points[2], points[3], $"{w:0}", true);
            ProfileDrawingHelper.DrawDimension(canvas, points[3], points[4], $"{s:0}", false);
            ProfileDrawingHelper.DrawBendMarker(canvas, points[1]);
            ProfileDrawingHelper.DrawBendMarker(canvas, points[2]);
            ProfileDrawingHelper.DrawBendMarker(canvas, points[3]);
            ProfileDrawingHelper.DrawBendMarker(canvas, points[4]);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Z-ОБРАЗНЫЙ — 2 гиба в разные стороны
    // ═══════════════════════════════════════════════════════
    public class ZShapeProfile : BendProfileBase
    {
        public override string DisplayName => "Z-образный";
        public override int BendsCount => 2;

        public ProfileParameter TopShelf { get; }
        public ProfileParameter Web { get; }
        public ProfileParameter BottomShelf { get; }

        public ZShapeProfile()
        {
            TopShelf = new ProfileParameter("Верхняя", "Верхняя", 40);
            Web = new ProfileParameter("Стенка", "Стенка", 60);
            BottomShelf = new ProfileParameter("Нижняя", "Нижняя", 40);
            RegisterParameter(TopShelf);
            RegisterParameter(Web);
            RegisterParameter(BottomShelf);
        }

        public override double CalculateFlatLength(double bd)
            => TopShelf.Value + Web.Value + BottomShelf.Value - 2 * bd;

        public override List<double> GetBendPositions()
            => new() { TopShelf.Value, TopShelf.Value + Web.Value };

        public override List<(double, int, double)> GetBendInfo(double crossLength)
            => new()
            {
                (TopShelf.Value, 1, crossLength),
                (BottomShelf.Value, 1, crossLength)
            };

        public override void DrawOnCanvas(Canvas canvas, double W, double H)
        {
            double t = TopShelf.Value, w = Web.Value, b = BottomShelf.Value;
            if (t <= 0 && w <= 0 && b <= 0) return;

            double maxLen = Math.Max(t, Math.Max(w, b));
            double scale = Math.Min(W * 0.6, H * 0.6) / Math.Max(maxLen, 1);

            var points = new PointCollection
            {
                new(W / 2 - w * scale / 2 - t * scale, H / 2 - w * scale / 2),
                new(W / 2 - w * scale / 2, H / 2 - w * scale / 2),
                new(W / 2 + w * scale / 2, H / 2 + w * scale / 2),
                new(W / 2 + w * scale / 2 + b * scale, H / 2 + w * scale / 2)
            };

            ProfileDrawingHelper.DrawProfileLine(canvas, points);
            ProfileDrawingHelper.DrawDimension(canvas, points[0], points[1], $"{t:0}", false);
            ProfileDrawingHelper.DrawDimension(canvas, points[1], points[2], $"{w:0}", true);
            ProfileDrawingHelper.DrawDimension(canvas, points[2], points[3], $"{b:0}", false);
            ProfileDrawingHelper.DrawBendMarker(canvas, points[1]);
            ProfileDrawingHelper.DrawBendMarker(canvas, points[2]);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Вспомогательные методы отрисовки
    // ═══════════════════════════════════════════════════════
    public static class ProfileDrawingHelper
    {
        public static void DrawProfileLine(Canvas canvas, PointCollection points)
        {
            var polyline = new Polyline
            {
                Points = points,
                Stroke = Brushes.DarkSlateGray,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };
            canvas.Children.Add(polyline);

            foreach (var p in points)
            {
                var dot = new Ellipse { Width = 5, Height = 5, Fill = Brushes.DarkSlateGray };
                Canvas.SetLeft(dot, p.X - 2.5);
                Canvas.SetTop(dot, p.Y - 2.5);
                canvas.Children.Add(dot);
            }
        }

        public static void DrawDimension(Canvas canvas, Point p1, Point p2, string text, bool vertical)
        {
            double offset = 14;
            var color = Brushes.SteelBlue;

            if (vertical)
            {
                double x = Math.Min(p1.X, p2.X) - offset;
                canvas.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = p1.Y,
                    X2 = x,
                    Y2 = p2.Y,
                    Stroke = color,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                });

                var tb = new TextBlock
                {
                    Text = text,
                    FontSize = 10,
                    Foreground = color,
                    FontWeight = FontWeights.SemiBold,
                    Background = Brushes.White
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, x - tb.DesiredSize.Width - 3);
                Canvas.SetTop(tb, (p1.Y + p2.Y) / 2 - tb.DesiredSize.Height / 2);
                canvas.Children.Add(tb);
            }
            else
            {
                double y = Math.Max(p1.Y, p2.Y) + offset;
                canvas.Children.Add(new Line
                {
                    X1 = p1.X,
                    Y1 = y,
                    X2 = p2.X,
                    Y2 = y,
                    Stroke = color,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                });

                var tb = new TextBlock
                {
                    Text = text,
                    FontSize = 10,
                    Foreground = color,
                    FontWeight = FontWeights.SemiBold,
                    Background = Brushes.White
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, (p1.X + p2.X) / 2 - tb.DesiredSize.Width / 2);
                Canvas.SetTop(tb, y + 2);
                canvas.Children.Add(tb);
            }
        }

        public static void DrawBendMarker(Canvas canvas, Point p)
        {
            var marker = new Ellipse
            {
                Width = 10,
                Height = 10,
                Stroke = Brushes.DarkOrange,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(60, 255, 165, 0))
            };
            Canvas.SetLeft(marker, p.X - 5);
            Canvas.SetTop(marker, p.Y - 5);
            canvas.Children.Add(marker);
        }
    }
}