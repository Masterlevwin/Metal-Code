using System;
using System.Collections.Generic;
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
                PartType.Triangle => CreateTriangleSection(part.Width, part.Height),
                PartType.RectangularTube => CreateRectangularTubeSection(part.Width, part.Height, part.Destiny),
                PartType.RoundTube => CreateRoundTubeSection(part.Width, part.Destiny),
                PartType.Circle => CreateRoundTubeSection(part.Width, part.Destiny),
                PartType.SquareBar => CreateRectangularTubeSection(part.Width, part.Width, part.Destiny),
                PartType.Angle => CreateAngleSection(part.Width, part.Height, part.Destiny),
                PartType.Channel => CreateChannelSection(part.Width, part.Height, part.Destiny),
                PartType.IBeam => CreateIBeamSection(part.Width, part.Height, part.Destiny),

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

        public static PathGeometry CreateTriangleSection(double width, double height)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                // Начинаем с левого нижнего угла (прямой угол)
                StartPoint = new Point(-width / 2, height / 2),
                IsClosed = true,
                IsFilled = true
            };

            // Линия вдоль нижнего катета (вправо)
            figure.Segments.Add(new LineSegment(new Point(width / 2, height / 2), true));

            // Линия гипотенузы (вверх и влево к верхней вершине)
            figure.Segments.Add(new LineSegment(new Point(-width / 2, -height / 2), true));

            // Линия вдоль левого катета (вниз к начальной точке, замыкается автоматически, но добавим для ясности)
            figure.Segments.Add(new LineSegment(new Point(-width / 2, height / 2), true));

            geometry.Figures.Add(figure);

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
        /// Создаёт сечение уголка (равно- или неравнополочного)
        /// Параметры: leg1 — длина первой полки (Width), leg2 — длина второй полки (Height), thickness — толщина полок (Destiny)
        /// Если leg1 == leg2 — создаётся равнополочный уголок
        /// </summary>
        public static PathGeometry CreateAngleSection(double leg1, double leg2, double thickness)
        {
            var geometry = new PathGeometry();

            // Валидация: толщина не должна превышать половину меньшей полки
            double minLeg = Math.Min(leg1, leg2);
            if (thickness >= minLeg / 2 - 1)
                thickness = Math.Max(1, minLeg / 2 - 1);

            // Центрируем уголок: внутренний угол смещён для симметричного отображения
            // Координаты рассчитываются относительно центра масс для наглядности
            double centerX = (leg1 - thickness) / 2 - leg1 / 2;
            double centerY = (leg2 - thickness) / 2 - leg2 / 2;

            var figure = new PathFigure
            {
                StartPoint = new Point(-leg1 / 2, -leg2 / 2),
                IsClosed = true,
                IsFilled = true
            };

            // Обход внешнего периметра по часовой стрелке, начиная с левого нижнего угла
            // Горизонтальная полка (leg1)
            figure.Segments.Add(new LineSegment(new Point(leg1 / 2, -leg2 / 2), true));                    // → вправо
            figure.Segments.Add(new LineSegment(new Point(leg1 / 2, -leg2 / 2 + thickness), true));        // ↑ на толщину
            figure.Segments.Add(new LineSegment(new Point(-leg1 / 2 + thickness, -leg2 / 2 + thickness), true)); // ← к внутреннему углу
                                                                                                                 // Вертикальная полка (leg2)
            figure.Segments.Add(new LineSegment(new Point(-leg1 / 2 + thickness, leg2 / 2), true));        // ↑ вверх
            figure.Segments.Add(new LineSegment(new Point(-leg1 / 2, leg2 / 2), true));                    // ← влево
                                                                                                           // Замыкается автоматически к начальной точке

            geometry.Figures.Add(figure);
            return geometry;
        }

        /// <summary>
        /// Создаёт сечение швеллера (U-образный профиль, открытие вверх)
        /// Параметры: width — общая ширина, height — общая высота, thickness — единая толщина всех элементов
        /// </summary>
        public static PathGeometry CreateChannelSection(double width, double height, double thickness)
        {
            var geometry = new PathGeometry();
            double halfW = width / 2;
            double halfH = height / 2;

            // Валидация: толщина не должна превышать 1/3 от меньшего размера
            if (thickness >= Math.Min(width, height) / 3)
                thickness = Math.Max(1, Math.Min(width, height) / 3 - 1);

            var figure = new PathFigure
            {
                StartPoint = new Point(-halfW, -halfH),  // Левый верхний угол (внешний)
                IsClosed = true,
                IsFilled = true
            };

            // Обход контура по часовой стрелке (U-образный, открытие вверх)
            // === ЛЕВАЯ СТЕНКА (вверх-вниз) ===
            // 1. По верху левой стенки → вправо (внешняя грань)
            figure.Segments.Add(new LineSegment(new Point(-halfW + thickness, -halfH), true));
            // 2. Внутренняя грань левой стенки ↓ вниз
            figure.Segments.Add(new LineSegment(new Point(-halfW + thickness, halfH - thickness), true));

            // === ОСНОВАНИЕ (горизонтальное, внизу) ===
            // 3. По внутренней грани основания → вправо
            figure.Segments.Add(new LineSegment(new Point(halfW - thickness, halfH - thickness), true));

            // === ПРАВАЯ СТЕНКА (вниз-вверх) ===
            // 4. Внутренняя грань правой стенки ↑ вверх
            figure.Segments.Add(new LineSegment(new Point(halfW - thickness, -halfH), true));
            // 5. По верху правой стенки → вправо (внутренняя грань)
            figure.Segments.Add(new LineSegment(new Point(halfW, -halfH), true));
            // 6. Внешняя правая грань ↓ вниз до основания
            figure.Segments.Add(new LineSegment(new Point(halfW, halfH), true));

            // === НИЗ ОСНОВАНИЯ ===
            // 7. По низу основания ← влево
            figure.Segments.Add(new LineSegment(new Point(-halfW, halfH), true));
            // 8. Внешняя левая грань ↑ вверх к началу (замыкается автоматически)

            geometry.Figures.Add(figure);
            return geometry;
        }

        /// <summary>
        /// Создаёт сечение двутавра (I-образный профиль)
        /// Параметры: width — ширина полки, height — высота профиля, thickness — единая толщина всех элементов
        /// </summary>
        public static PathGeometry CreateIBeamSection(double width, double height, double thickness)
        {
            var geometry = new PathGeometry();
            double halfW = width / 2;
            double halfH = height / 2;
            double halfT = thickness / 2;

            // Валидация: толщина не должна превышать 1/3 от меньшего размера
            if (thickness >= Math.Min(width, height) / 3)
                thickness = Math.Max(1, Math.Min(width, height) / 3 - 1);
            halfT = thickness / 2;

            var figure = new PathFigure
            {
                StartPoint = new Point(-halfW, -halfH),  // Левый верхний угол (внешний)
                IsClosed = true,
                IsFilled = true
            };

            figure.Segments.Add(new LineSegment(new Point(halfW, -halfH), true));
            figure.Segments.Add(new LineSegment(new Point(halfW, -halfH + thickness), true));
            figure.Segments.Add(new LineSegment(new Point(halfT, -halfH + thickness), true));
            figure.Segments.Add(new LineSegment(new Point(halfT, halfH - thickness), true));
            figure.Segments.Add(new LineSegment(new Point(halfW, halfH - thickness), true));
            figure.Segments.Add(new LineSegment(new Point(halfW, halfH), true));
            figure.Segments.Add(new LineSegment(new Point(-halfW, halfH), true));
            figure.Segments.Add(new LineSegment(new Point(-halfW, halfH - thickness), true));
            figure.Segments.Add(new LineSegment(new Point(-halfT, halfH - thickness), true));
            figure.Segments.Add(new LineSegment(new Point(-halfT, -halfH + thickness), true));
            figure.Segments.Add(new LineSegment(new Point(-halfW, -halfH + thickness), true));

            geometry.Figures.Add(figure);
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
                IsFilled = isFilled
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

            // 🔥 СПЕЦИАЛЬНАЯ ОБРАБОТКА ДЛЯ ПРОИЗВОЛЬНЫХ ФОРМ
            if (part.PartType == PartType.Custom)
            {
                // Если геометрии нет, нам нечего улучшать
                if (part.DisplayGeometry == null) return;

                // Если отверстий нет, оставляем геометрию как есть (она уже правильная)
                bool hasHoleGroups = part.HoleGroups != null && part.HoleGroups.Count > 0;
                bool hasPlacedHoles = part.PlacedHoles != null && part.PlacedHoles.Count > 0;

                if (!hasHoleGroups && !hasPlacedHoles)
                {
                    return;
                }

                // Клонируем существующую геометрию, нарисованную пользователем
                var geometry = CloneGeometry(part.DisplayGeometry);
                if (geometry == null) return;

                // Добавляем отверстия как НЕзаполненные контуры (полости)
                var holePositions = CalculateHolePositions(part);
                foreach (var (position, hole) in holePositions)
                {
                    geometry.Figures.Add(CreateHoleFigure(position.X, position.Y, hole.Diameter / 2));
                }

                part.DisplayGeometry = geometry;
                return; // 🔥 ВАЖНО: выходим, не выполняя стандартную логику ниже
            }

            // === СТАНДАРТНАЯ ЛОГИКА ДЛЯ ШАБЛОННЫХ ДЕТАЛЕЙ ===
            var originalGeometry = part.DisplayGeometry;

            part.DisplayGeometry = null;
            EnsureDisplayGeometry(part);

            if (part.DisplayGeometry == null || part.HoleGroups == null || part.HoleGroups.Count == 0)
            {
                part.DisplayGeometry = originalGeometry ?? part.DisplayGeometry;
                return;
            }

            var (isValid, error) = ValidateHolesPlacement(part);
            if (error != null) MainWindow.M.StatusBegin(error, MainWindow.StatusMessageType.Error);
            if (!isValid)
            {
                part.DisplayGeometry = originalGeometry ?? part.DisplayGeometry;
                return;
            }

            var geometryStandard = CloneGeometry(part.DisplayGeometry);
            if (geometryStandard == null)
            {
                part.DisplayGeometry = originalGeometry ?? part.DisplayGeometry;
                return;
            }

            var positionsStandard = CalculateHolePositions(part);
            foreach (var (position, hole) in positionsStandard)
            {
                geometryStandard.Figures.Add(CreateHoleFigure(position.X, position.Y, hole.Diameter / 2));
            }

            part.DisplayGeometry = geometryStandard;
        }

        public static PathGeometry? CloneGeometry(PathGeometry? source)
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

            bool isSheetPart = part.PartType == PartType.Round || part.PartType == PartType.Rectangle
                            || part.PartType == PartType.Triangle;
            bool isPipePart = part.PartType == PartType.RoundTube || part.PartType == PartType.RectangularTube
                            || part.PartType == PartType.Angle || part.PartType == PartType.Channel
                            || part.PartType == PartType.IBeam;

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
            if (part.Width <= 0 || part.Height <= 0)
                return (false, "Неверные габариты детали");

            double totalHoleArea = part.HoleGroups.Sum(g => g.TotalArea);

            // Расчет площади с учетом треугольника
            double partArea = part.PartType switch
            {
                PartType.Round => Math.PI * Math.Pow(part.Width / 2, 2),
                PartType.Triangle => part.Width * part.Height / 2.0,
                _ => part.Width * part.Height
            };

            if (totalHoleArea > partArea * 0.7)
                return (false, $"Слишком много отверстий: они занимают более 70% площади детали");

            double maxDiameter = part.HoleGroups.Max(g => g.Diameter);
            double minMargin = maxDiameter / 2 + 5;

            // Для прямоугольника и треугольника проверяем оба габарита
            if (part.PartType == PartType.Rectangle || part.PartType == PartType.Triangle)
            {
                if (part.Width < minMargin * 2 || part.Height < minMargin * 2)
                    return (false, $"Деталь слишком мала для отверстий диаметром {maxDiameter}мм");
            }
            else // Round
            {
                if (part.Width < minMargin * 2)
                    return (false, $"Деталь слишком мала для отверстий диаметром {maxDiameter}мм");
            }

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

            // Проверка: достаточно ли места для всех отверстий по длине
            double requiredLength = holes.Count * minSpacing;
            if (requiredLength > availableLength)
            {
                int maxPossible = (int)(availableLength / minSpacing);
                return (false,
                    $"Недостаточно места для {holes.Count} отверстий. " +
                    $"Максимум можно разместить {maxPossible} отверстия(й) диаметром {holes.Max()}мм при длине трубы {part.Length}мм");
            }

            // Ограничение на максимальное количество отверстий (100 шт) снято

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
        /// Рассчитывает позиции отверстий.
        /// Для произвольных форм использует координаты, заданные пользователем вручную.
        /// Для труб возвращает пустой список (отверстия не отображаются в сечении).
        /// </summary>
        private static List<(Point Position, Hole Hole)> CalculateHolePositions(Part part)
        {
            var positions = new List<(Point Position, Hole Hole)>();

            // 🔥 ДЛЯ ПРОИЗВОЛЬНЫХ ФОРМ: используем точные координаты из PlacedHoles
            if (part.PartType == PartType.Custom && part.PlacedHoles != null && part.PlacedHoles.Count > 0)
            {
                foreach (var placed in part.PlacedHoles)
                {
                    // Создаем объект Hole только с диаметром, так как координаты уже есть в placed.X/Y
                    positions.Add((new Point(placed.X, placed.Y), new Hole(placed.Diameter)));
                }
                return positions;
            }

            // Для труб НЕ визуализируем отверстия в сечении
            if (part.PartType == PartType.RoundTube || part.PartType == PartType.RectangularTube)
                return positions;

            // === СТАНДАРТНАЯ ЛОГИКА ДЛЯ ЛИСТОВЫХ ДЕТАЛЕЙ ===
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
            else if (part.PartType == PartType.Triangle)
            {
                double maxDiameter = allHoles.Max(h => h.Diameter);
                double pitch = maxDiameter + 5;

                int rows = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count * 1.5)));
                double spacingY = rows > 1 ? (part.Height - 2 * minMargin) / (rows - 1) : 0;

                int index = 0;

                for (int row = 0; row < rows && index < count; row++)
                {
                    double y = (part.Height / 2) - minMargin - row * spacingY;
                    if (rows == 1) y = 0;

                    double xLeft = -part.Width / 2;
                    double xRight = -part.Width / 2 + (part.Width / part.Height) * (y + part.Height / 2);
                    double currentWidth = xRight - xLeft;
                    double availableWidth = currentWidth - 2 * minMargin;

                    int holesInThisRow = 0;
                    double currentSpacingX = 0;

                    if (availableWidth >= 0)
                    {
                        holesInThisRow = (int)Math.Floor(availableWidth / pitch) + 1;
                        holesInThisRow = Math.Max(1, holesInThisRow);

                        if (holesInThisRow > 1)
                        {
                            currentSpacingX = availableWidth / (holesInThisRow - 1);
                        }
                    }
                    else
                    {
                        if (currentWidth > maxDiameter + 10)
                        {
                            holesInThisRow = 1;
                        }
                    }

                    for (int col = 0; col < holesInThisRow && index < count; col++)
                    {
                        double x = (holesInThisRow == 1)
                            ? (xLeft + currentWidth / 2)
                            : (xLeft + minMargin + col * currentSpacingX);

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