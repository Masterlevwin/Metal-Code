using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Metal_Code
{
    public class SplineDescriptor : IGeometryDescriptor
    {
        public List<Point> Points { get; set; } = new List<Point>();
        public Brush Stroke { get; set; } = Brushes.Black;
        public double StrokeThickness { get; set; } = 0.5;
        public bool IsClosed { get; set; } = false;

        public void Draw(Canvas canvas)
        {
            if (Points.Count < 2) return;

            // Если сплайн замкнут — добавим соединение последней точки с первой
            var pointsToDraw = new List<Point>(Points);
            if (IsClosed && Points.Count > 2 && !Points[0].Equals(Points[^1]))
            {
                pointsToDraw.Add(Points[0]);
            }

            // Рисуем ломаную (аппроксимация сплайна)
            for (int i = 1; i < pointsToDraw.Count; i++)
            {
                var line = new Line
                {
                    X1 = pointsToDraw[i - 1].X,
                    Y1 = pointsToDraw[i - 1].Y,
                    X2 = pointsToDraw[i].X,
                    Y2 = pointsToDraw[i].Y,
                    Stroke = Stroke,
                    StrokeThickness = StrokeThickness
                };
                canvas.Children.Add(line);
            }
        }
    }
}