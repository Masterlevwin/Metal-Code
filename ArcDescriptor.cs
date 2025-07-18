using System;
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

        public Brush Stroke { get; set; } = Brushes.Green;
        public double StrokeThickness { get; set; } = 0.5;

        public void Draw(Canvas canvas)
        {
            // 1. Проверка на корректные данные
            if (double.IsNaN(Center.X) || double.IsNaN(Center.Y) || Radius <= 0)
            {
                MainWindow.M.Log += "\n[Ошибка] Неверные данные дуги: NaN или радиус <= 0";
                return;
            }

            // 2. Рассчитываем начальную и конечную точку
            double startX = Center.X + Radius * Math.Cos(StartAngle.ToRadians());
            double startY = Center.Y + Radius * Math.Sin(StartAngle.ToRadians());

            double endX = Center.X + Radius * Math.Cos((StartAngle + SweepAngle).ToRadians());
            double endY = Center.Y + Radius * Math.Sin((StartAngle + SweepAngle).ToRadians());

            if (startX < 0 || startX > 200 || startY < 0 || startY > 200 ||
                endX < 0 || endX > 200 || endY < 0 || endY > 200)
            {
                MainWindow.M.Log += "\n[ARC] Дуга выходит за пределы Canvas";
            }

            // 3. Определяем направление дуги
            bool isLargeArc = Math.Abs(SweepAngle) > 180;
            SweepDirection direction = SweepAngle >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

            if (Math.Abs(SweepAngle) < 0.1)
            {
                MainWindow.M.Log += "\n[ARC] SweepAngle слишком маленький или 0 → дуга не отрисуется";
                return;
            }

            // 4. Создаём ArcSegment
            var arcSegment = new ArcSegment(
                new Point(endX, endY),
                new Size(Radius, Radius),
                rotationAngle: 0,
                isLargeArc: isLargeArc,
                sweepDirection: direction,
                isStroked: true
            );

            // 5. Создаём PathFigure
            var figure = new PathFigure
            {
                StartPoint = new Point(startX, startY),
                IsClosed = true
            };
            figure.Segments.Add(arcSegment);

            // 6. Создаём PathGeometry и Path
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            var path = new Path
            {
                Data = geometry,
                Stroke = Stroke,
                StrokeThickness = StrokeThickness
            };

            // 7. Добавляем на Canvas
            canvas.Children.Add(path);

            // 8. Логируем
            MainWindow.M.Log += $"\n[ARC] Start=({startX:F2}, {startY:F2}), End=({endX:F2}, {endY:F2}), Radius={Radius:F2}, SweepAngle={SweepAngle:F2}, IsLargeArc={isLargeArc}, Direction={direction}";
        }
    }
}