using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace Metal_Code.Utils
{
    public static class PartPreviewGenerator
    {
        public static void EnsureDisplayGeometry(Part part)
        {
            if (part.DisplayGeometry != null)
                return;

            part.DisplayGeometry = part.PartType switch
            {
                PartType.Rectangle => CreateRectangleSection(part.Width, part.Height),
                PartType.Round => CreateRoundSection(part.Width),
                PartType.RectangularTube => CreateRectangularTubeSection(part.Width, part.Height, part.Destiny),
                PartType.RoundTube => CreateRoundTubeSection(part.Width, part.Destiny),
                _ => null
            };
        }

        public static PathGeometry CreateRectangleSection(double width, double height)
        {
            var geometry = new PathGeometry();

            var figure = new PathFigure
            {
                StartPoint = new Point(-width / 2, -height / 2),
                IsClosed = true,
                IsFilled = true
            };
            figure.Segments.Add(new LineSegment(new Point(width / 2, -height / 2), true));
            figure.Segments.Add(new LineSegment(new Point(width / 2, height / 2), true));
            figure.Segments.Add(new LineSegment(new Point(-width / 2, height / 2), true));
            geometry.Figures.Add(figure);

            return geometry;
        }

        public static PathGeometry CreateRoundSection(double diameter)
        {
            var geometry = new PathGeometry();
            double radius = diameter / 2;
            geometry.Figures.Add(CreateCircleFigure(0, 0, radius, true)); // Заполненный
            return geometry;
        }

        public static PathGeometry CreateRectangularTubeSection(double width, double height, double thickness)
        {
            var geometry = new PathGeometry();

            // Внешний прямоугольник
            var outer = new PathFigure
            {
                StartPoint = new Point(-width / 2, -height / 2),
                IsClosed = true,
                IsFilled = true
            };
            outer.Segments.Add(new LineSegment(new Point(width / 2, -height / 2), true));
            outer.Segments.Add(new LineSegment(new Point(width / 2, height / 2), true));
            outer.Segments.Add(new LineSegment(new Point(-width / 2, height / 2), true));
            geometry.Figures.Add(outer);

            // Внутренний прямоугольник (полость)
            double innerW = width - 2 * thickness;
            double innerH = height - 2 * thickness;
            if (innerW > 0 && innerH > 0)
            {
                var inner = new PathFigure
                {
                    StartPoint = new Point(-innerW / 2, -innerH / 2),
                    IsClosed = true,
                    IsFilled = false
                };
                inner.Segments.Add(new LineSegment(new Point(innerW / 2, -innerH / 2), true));
                inner.Segments.Add(new LineSegment(new Point(innerW / 2, innerH / 2), true));
                inner.Segments.Add(new LineSegment(new Point(-innerW / 2, innerH / 2), true));
                geometry.Figures.Add(inner);
            }

            return geometry;
        }

        public static PathGeometry CreateRoundTubeSection(double diameter, double thickness)
        {
            var geometry = new PathGeometry();
            double outerR = diameter / 2;
            double innerR = outerR - thickness;

            geometry.Figures.Add(CreateCircleFigure(0, 0, outerR, true)); // Внешний - заполненный

            if (innerR > 0)
                geometry.Figures.Add(CreateCircleFigure(0, 0, innerR, false)); // Внутренний - НЕзаполненный!

            return geometry;
        }

        /// <summary>
        /// Создаёт круглый контур (заполненный или незаполненный)
        /// </summary>
        private static PathFigure CreateCircleFigure(double centerX, double centerY, double radius, bool isFilled)
        {
            var figure = new PathFigure
            {
                StartPoint = new Point(centerX + radius, centerY),
                IsClosed = true,
                IsFilled = isFilled // ← КЛЮЧЕВОЙ ПАРАМЕТР!
            };
            figure.Segments.Add(new ArcSegment(
                new Point(centerX - radius, centerY),
                new Size(radius, radius),
                0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new ArcSegment(
                new Point(centerX + radius, centerY),
                new Size(radius, radius),
                0, false, SweepDirection.Clockwise, true));
            return figure;
        }


        /// <summary>
        /// Генерирует геометрию с отверстиями
        /// </summary>
        public static void EnsureDisplayGeometryWithHoles(Part part)
        {
            if (part == null) return;

            // Сохраняем оригинальную геометрию для восстановления в случае ошибки
            var originalGeometry = part.DisplayGeometry;

            // Генерируем базовую геометрию (без отверстий)
            part.DisplayGeometry = null;
            EnsureDisplayGeometry(part);

            if (part.DisplayGeometry == null || part.HoleGroups == null || part.HoleGroups.Count == 0)
            {
                part.DisplayGeometry = originalGeometry ?? part.DisplayGeometry;
                return;
            }

            // Валидация размещения отверстий
            var (isValid, error) = ValidateHolesPlacement(part);
            if (error != null) MainWindow.M.StatusBegin(error, MainWindow.StatusMessageType.Error);
            if (!isValid)
            {
                part.DisplayGeometry = originalGeometry ?? part.DisplayGeometry;
                return;
            }

            // Клонируем геометрию через сериализацию
            var geometry = CloneGeometry(part.DisplayGeometry);
            if (geometry == null)
            {
                part.DisplayGeometry = originalGeometry ?? part.DisplayGeometry;
                return;
            }

            // Добавляем отверстия как НЕзаполненные контуры (как полости в трубах)
            var holePositions = CalculateHolePositions(part);
            foreach (var (position, hole) in holePositions)
            {
                geometry.Figures.Add(CreateHoleFigure(position.X, position.Y, hole.Diameter / 2));
            }

            part.DisplayGeometry = geometry;
        }

        private static PathGeometry? CloneGeometry(PathGeometry source)
        {
            if (source == null) return null;

            try
            {
                // Сериализуем в XAML и парсим обратно — получаем полную копию со всеми внутренними состояниями
                string xaml = XamlWriter.Save(source);
                return (PathGeometry)XamlReader.Parse(xaml);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Проверяет, возможно ли разместить отверстия в детали
        /// </summary>
        public static (bool IsValid, string? ErrorMessage) ValidateHolesPlacement(Part part)
        {
            if (part.HoleGroups == null || part.HoleGroups.Count == 0)
                return (true, null);

            bool isSheetPart = part.PartType == PartType.Round || part.PartType == PartType.Rectangle;
            bool isPipePart = part.PartType == PartType.RoundTube || part.PartType == PartType.RectangularTube;

            if (isSheetPart)
            {
                return ValidateSheetHoles(part);
            }
            else if (isPipePart)
            {
                return ValidatePipeHoles(part);
            }

            return (true, null);
        }

        private static (bool IsValid, string? ErrorMessage) ValidateSheetHoles(Part part)
        {
            // Проверка минимальных размеров детали
            if (part.Width <= 0 || part.Height <= 0)
                return (false, "Неверные габариты детали");

            // Суммарная площадь отверстий
            double totalHoleArea = part.HoleGroups.Sum(g => g.TotalArea);

            // Площадь детали
            double partArea = part.PartType == PartType.Round
                ? Math.PI * Math.Pow(part.Width / 2, 2)
                : part.Width * part.Height;

            // Проверка: отверстия не должны занимать более 70% площади
            if (totalHoleArea > partArea * 0.7)
                return (false, $"Слишком много отверстий: они занимают более 70% площади детали");

            // Проверка минимального отступа от края
            double maxDiameter = part.HoleGroups.Max(g => g.Diameter);
            double minMargin = maxDiameter / 2 + 5; // радиус + 5мм зазор

            if (part.PartType == PartType.Rectangle)
            {
                if (part.Width < minMargin * 2 || part.Height < minMargin * 2)
                    return (false, $"Деталь слишком мала для отверстий диаметром {maxDiameter}мм");
            }
            else // Round
            {
                if (part.Width < minMargin * 2)
                    return (false, $"Деталь слишком мала для отверстий диаметром {maxDiameter}мм");
            }

            // Проверка максимального количества отверстий
            int totalCount = part.HoleGroups.Sum(g => g.Count);
            if (totalCount > 50)
                return (false, "Слишком много отверстий (максимум 50)");

            // Проверка расстояния между отверстиями (для листов)
            var positions = CalculateHolePositions(part);
            if (!ValidateHoleSpacing(positions))
                return (false, "Отверстия слишком близко друг к другу или к краям детали");

            return (true, null);
        }

        private static (bool IsValid, string? ErrorMessage) ValidatePipeHoles(Part part)
        {
            // Проверка длины трубы
            if (part.Length <= 0)
                return (false, "Укажите длину трубы");

            // Минимальный отступ от торцов трубы (20мм для безопасного сверления)
            const double endMargin = 20;

            // Доступная длина для размещения отверстий
            double availableLength = part.Length - 2 * endMargin;
            if (availableLength <= 0)
                return (false, $"Длина трубы слишком мала для сверления отверстий (минимум {endMargin * 2}мм)");

            // Сортируем отверстия по диаметру для оптимального размещения
            var holes = part.HoleGroups
                .SelectMany(g => Enumerable.Repeat(g.Diameter, g.Count))
                .OrderByDescending(d => d) // Сначала большие отверстия
                .ToList();

            // Минимальное расстояние между центрами отверстий = больший диаметр + 10мм зазор
            double minSpacing = holes.Count > 0 ? holes.Max() + 10 : 0;

            // Проверка: достаточно ли места для всех отверстий
            double requiredLength = holes.Count * minSpacing;
            if (requiredLength > availableLength)
            {
                int maxPossible = (int)(availableLength / minSpacing);
                return (false,
                    $"Недостаточно места для {holes.Count} отверстий. " +
                    $"Максимум можно разместить {maxPossible} отверстия(й) диаметром {holes.Max()}мм при длине трубы {part.Length}мм");
            }

            // Проверка максимального количества отверстий (ограничение разумности)
            if (holes.Count > 100)
                return (false, "Слишком много отверстий (максимум 100 для трубы)");

            // Проверка минимального диаметра отверстия относительно толщины стенки
            double minDiameter = holes.Min();
            if (minDiameter < part.Destiny * 0.8)
            {
                return (false,
                    $"Диаметр отверстия {minDiameter}мм меньше толщины стенки трубы {part.Destiny}мм. " +
                    "Рекомендуется диаметр отверстия не менее 80% от толщины стенки");
            }

            return (true, null);
        }

        private static bool ValidateHoleSpacing(List<(Point Position, Hole Hole)> positions)
        {
            const double minDistanceBetweenHoles = 5; // Минимум 5мм между краями отверстий

            for (int i = 0; i < positions.Count; i++)
            {
                var (pos1, hole1) = positions[i];
                double radius1 = hole1.Diameter / 2;

                // Проверка расстояния до других отверстий
                for (int j = i + 1; j < positions.Count; j++)
                {
                    var (pos2, hole2) = positions[j];
                    double radius2 = hole2.Diameter / 2;

                    double distance = Math.Sqrt(Math.Pow(pos2.X - pos1.X, 2) + Math.Pow(pos2.Y - pos1.Y, 2));
                    double minRequired = radius1 + radius2 + minDistanceBetweenHoles;

                    if (distance < minRequired)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Рассчитывает позиции отверстий с учётом их диаметров
        /// </summary>
        /// <summary>
        /// Рассчитывает позиции отверстий ДЛЯ ВИЗУАЛИЗАЦИИ в сечении.
        /// Для труб возвращает пустой список (отверстия не отображаются в сечении).
        /// </summary>
        private static List<(Point Position, Hole Hole)> CalculateHolePositions(Part part)
        {
            var positions = new List<(Point Position, Hole Hole)>();

            // Для труб НЕ визуализируем отверстия в сечении
            if (part.PartType == PartType.RoundTube || part.PartType == PartType.RectangularTube)
                return positions;

            // Для листов — текущая логика распределения
            var allHoles = part.HoleGroups
                .SelectMany(g => Enumerable.Repeat(new Hole(g.Diameter), g.Count))
                .ToList();

            int count = allHoles.Count;
            if (count == 0) return positions;

            double minMargin = allHoles.Max(h => h.Diameter) / 2 + 5;

            if (part.PartType == PartType.Rectangle)
            {
                int cols = (int)Math.Ceiling(Math.Sqrt(count));
                int rows = (int)Math.Ceiling((double)count / cols);

                double marginX = Math.Max(minMargin, part.Width * 0.1);
                double marginY = Math.Max(minMargin, part.Height * 0.1);
                double spacingX = (part.Width - 2 * marginX) / Math.Max(1, cols - 1);
                double spacingY = (part.Height - 2 * marginY) / Math.Max(1, rows - 1);

                int index = 0;
                for (int row = 0; row < rows && index < count; row++)
                {
                    for (int col = 0; col < cols && index < count; col++)
                    {
                        double x = -part.Width / 2 + marginX + col * spacingX;
                        double y = -part.Height / 2 + marginY + row * spacingY;
                        positions.Add((new Point(x, y), allHoles[index++]));
                    }
                }
            }
            else // Round
            {
                double maxRadius = (part.Width / 2) - minMargin;

                if (count == 1)
                {
                    positions.Add((new Point(0, 0), allHoles[0]));
                }
                else if (count <= 6)
                {
                    double radius = maxRadius * 0.6;
                    for (int i = 0; i < count; i++)
                    {
                        double angle = 2 * Math.PI * i / count - Math.PI / 2;
                        double x = Math.Cos(angle) * radius;
                        double y = Math.Sin(angle) * radius;
                        positions.Add((new Point(x, y), allHoles[i]));
                    }
                }
                else
                {
                    int innerCount = Math.Min(6, count);
                    int outerCount = count - innerCount;

                    double innerRadius = maxRadius * 0.4;
                    for (int i = 0; i < innerCount; i++)
                    {
                        double angle = 2 * Math.PI * i / innerCount - Math.PI / 2;
                        double x = Math.Cos(angle) * innerRadius;
                        double y = Math.Sin(angle) * innerRadius;
                        positions.Add((new Point(x, y), allHoles[i]));
                    }

                    if (outerCount > 0)
                    {
                        double outerRadius = maxRadius * 0.8;
                        for (int i = 0; i < outerCount; i++)
                        {
                            double angle = 2 * Math.PI * i / outerCount - Math.PI / 2;
                            double x = Math.Cos(angle) * outerRadius;
                            double y = Math.Sin(angle) * outerRadius;
                            positions.Add((new Point(x, y), allHoles[innerCount + i]));
                        }
                    }
                }
            }

            return positions;
        }

        private static PathFigure CreateHoleFigure(double centerX, double centerY, double radius)
        {
            // Отверстия ДОЛЖНЫ быть с IsFilled = false, как внутренние контуры труб!
            return CreateCircleFigure(centerX, centerY, radius, false);
        }
    }
}
