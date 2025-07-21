using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    public class ArcDescriptor : IGeometryDescriptor
    {
        public Point Center { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double SweepAngle { get; set; }

        public double Scale { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }

        public Brush Stroke { get; set; } = Brushes.Green;
        public double StrokeThickness { get; set; } = 0.5;

        public void Draw(Canvas canvas)
        {
            // Рассчитываем начальную и конечную точку дуги
            double startX = Center.X + Radius * Math.Cos(StartAngle.ToRadians());
            double startY = Center.Y + Radius * Math.Sin(StartAngle.ToRadians());

            double endX = Center.X + Radius * Math.Cos((StartAngle + SweepAngle).ToRadians());
            double endY = Center.Y + Radius * Math.Sin((StartAngle + SweepAngle).ToRadians());

            // Определяем направление дуги
            bool isLargeArc = Math.Abs(SweepAngle) >= Math.PI;
            SweepDirection sweepDirection = SweepAngle >= 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

            // Создаём дугу
            var arcSegment = new ArcSegment(
                new Point(endX, endY),
                new Size(Radius, Radius),
                rotationAngle: 0,
                isLargeArc: isLargeArc,
                sweepDirection: sweepDirection,
                isStroked: true
            );

            //Создаём PathFigure
            var figure = new PathFigure
            {
                StartPoint = new Point(startX, startY),
                IsClosed = false
            };
            figure.Segments.Add(arcSegment);

            // Создаём PathGeometry и Path
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var path = new Path
            {
                Data = geometry,
                Stroke = Stroke,
                StrokeThickness = StrokeThickness
            };

            // Отладка
            Trace.WriteLine(GeometryHelper.GeometryToString(geometry));

            // Добавляем на Canvas
            canvas.Children.Add(path);

            DrawCoordinateSystem(canvas, length: 50, thickness: 0.5, offsetX: 60, offsetY: 60);
        }
        public static void DrawCoordinateSystem(Canvas canvas, double length = 100, double thickness = 0.5, double offsetX = 0, double offsetY = 0)
        {
            // Ось X (красная)
            var xAxis = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = length,
                Y2 = 0,
                Stroke = Brushes.Red,
                StrokeThickness = thickness
            };

            // Стрелка на оси X
            var xArrow = new Line
            {
                X1 = length,
                Y1 = 0,
                X2 = length - 5,
                Y2 = -2,
                Stroke = Brushes.Red,
                StrokeThickness = thickness
            };

            var xArrow2 = new Line
            {
                X1 = length,
                Y1 = 0,
                X2 = length - 5,
                Y2 = 2,
                Stroke = Brushes.Red,
                StrokeThickness = thickness
            };

            // Ось Y (синяя)
            var yAxis = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = 0,
                Y2 = length,
                Stroke = Brushes.Blue,
                StrokeThickness = thickness
            };

            // Стрелка на оси Y
            var yArrow = new Line
            {
                X1 = 0,
                Y1 = length,
                X2 = 2,
                Y2 = length - 5,
                Stroke = Brushes.Blue,
                StrokeThickness = thickness
            };

            var yArrow2 = new Line
            {
                X1 = 0,
                Y1 = length,
                X2 = -2,
                Y2 = length - 5,
                Stroke = Brushes.Blue,
                StrokeThickness = thickness
            };

            // Текстовые обозначения осей
            var xAxisText = new TextBlock
            {
                Text = "X",
                Foreground = Brushes.Red,
                FontSize = 6
            };

            var yAxisText = new TextBlock
            {
                Text = "Y",
                Foreground = Brushes.Blue,
                FontSize = 6
            };

            Canvas.SetLeft(xAxisText, length + 2);
            Canvas.SetTop(xAxisText, 2);

            Canvas.SetLeft(yAxisText, 2);
            Canvas.SetTop(yAxisText, length - 8);

            // Добавляем на Canvas
            canvas.Children.Add(xAxis);
            canvas.Children.Add(xArrow);
            canvas.Children.Add(xArrow2);
            canvas.Children.Add(yAxis);
            canvas.Children.Add(yArrow);
            canvas.Children.Add(yArrow2);
            canvas.Children.Add(xAxisText);
            canvas.Children.Add(yAxisText);
        }
    }

    public static class DoubleExtensions
    {
        public static double ToRadians(this double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }
    }
}