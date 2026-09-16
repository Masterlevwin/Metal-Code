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
        private static PathFigure CreateCircleFigure(double centerX, double centerY, double radius, bool isFilled, SweepDirection direction = SweepDirection.Clockwise)
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
                0, false, direction, true));
            figure.Segments.Add(new ArcSegment(
                new Point(centerX + radius, centerY),
                new Size(radius, radius),
                0, false, direction, true));
            return figure;
        }


        /// <summary>
        /// Генерирует геометрию с отверстиями (с оптимизацией производительности для больших количеств)
        /// </summary>
        public static void EnsureDisplayGeometryWithHoles(Part part)
        {
            if (part == null) return;

            // 🔥 СПЕЦИАЛЬНАЯ ОБРАБОТКА ДЛЯ ПРОИЗВОЛЬНЫХ ФОРМ (без изменений)
            if (part.PartType == PartType.Custom)
            {
                if (part.DisplayGeometry == null) return;
                bool hasHoleGroups = part.HoleGroups != null && part.HoleGroups.Count > 0;
                bool hasPlacedHoles = part.PlacedHoles != null && part.PlacedHoles.Count > 0;

                if (!hasHoleGroups && !hasPlacedHoles) return;

                var geometry = CloneGeometry(part.DisplayGeometry);
                if (geometry == null) return;

                var holePositions = CalculateHolePositions(part);
                foreach (var (position, hole) in holePositions)
                {
                    geometry.Figures.Add(CreateHoleFigure(position.X, position.Y, hole.Diameter / 2));
                }
                part.DisplayGeometry = geometry;
                return;
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

            // Валидация (работает быстро, так как это чистая математика, а не UI)
            var (isValid, error) = ValidateHolesPlacement(part);
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

            // 🔥 ПРОВЕРКА НА "РЕЖИМ СВОДКИ" (ОПТИМИЗАЦИЯ)
            // Если хотя бы одна группа содержит более 50 отверстий, не рисуем их по отдельности
            bool useSummaryMode = part.HoleGroups.Any(g => g.Count > 50);

            if (!useSummaryMode)
            {
                // Мало отверстий: рисуем каждое для точной визуализации
                var positionsStandard = CalculateHolePositions(part);
                foreach (var (position, hole) in positionsStandard)
                {
                    geometryStandard.Figures.Add(CreateHoleFigure(position.X, position.Y, hole.Diameter / 2));
                }
            }
            // Если useSummaryMode == true, мы просто НЕ добавляем фигуры отверстий в geometryStandard.
            // Это экономит огромное количество ресурсов WPF. Визуальный индикатор мы добавим в StandartPartWindow.

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

            // 🔥 Технологический отступ равен толщине материала (минимум 0.5 мм для безопасности расчетов)
            double clearance = Math.Max(0.5, part.Destiny);
            double maxDiameter = part.HoleGroups.Max(g => g.Diameter);

            // 1. Проверка: влезает ли максимальное отверстие в контур с учетом отступов
            if (part.PartType == PartType.Round)
            {
                if (maxDiameter > part.Width - 2 * clearance)
                    return (false, $"Диаметр отверстия {maxDiameter}мм слишком велик для круга ⌀{part.Width}мм с отступом {clearance}мм");
            }
            else // Rectangle, Triangle
            {
                double minDimension = Math.Min(part.Width, part.Height);
                if (maxDiameter > minDimension - 2 * clearance)
                    return (false, $"Диаметр отверстия {maxDiameter}мм слишком велик для данного габарита с отступом {clearance}мм");
            }

            // 2. Проверка совокупной "эффективной" площади (отверстие + зона отступа вокруг него)
            // Площадь круга с радиусом (R + clearance)
            double totalEffectiveArea = part.HoleGroups.Sum(g => Math.PI * Math.Pow((g.Diameter / 2.0) + clearance, 2) * g.Count);

            double partArea = part.PartType switch
            {
                PartType.Round => Math.PI * Math.Pow(part.Width / 2, 2),
                PartType.Triangle => part.Width * part.Height / 2.0,
                _ => part.Width * part.Height
            };

            // Теоретический максимум плотности упаковки кругов ~90.6%. Ставим лимит 85% для безопасности.
            // Это позволяет разместить 400 отверстий 5мм на полосе 100х2000, но остановит абсурдные значения.
            if (totalEffectiveArea > partArea * 0.85)
                return (false, "Совокупная площадь отверстий с учетом технологических отступов превышает физически возможный лимит (85% площади детали).");

            // 3. Финальная проверка геометрической расстановки
            var positions = CalculateHolePositions(part);
            if (!ValidateHoleSpacing(positions, clearance))
                return (false, $"Невозможно разместить отверстия с заданным отступом ({clearance}мм). Попробуйте уменьшить количество или диаметр.");

            return (true, null);
        }

        private static (bool IsValid, string? ErrorMessage) ValidatePipeHoles(Part part)
        {
            if (part.Length <= 0)
                return (false, "Укажите длину трубы");

            const double endMargin = 20; // Отступ от торца оставляем фиксированным для надежности патрона
            double availableLength = part.Length - 2 * endMargin;
            if (availableLength <= 0)
                return (false, $"Длина трубы слишком мала для сверления (минимум {endMargin * 2}мм)");

            var holes = part.HoleGroups
                .SelectMany(g => Enumerable.Repeat(g.Diameter, g.Count))
                .OrderByDescending(d => d)
                .ToList();

            // 🔥 Минимальное расстояние между центрами = больший диаметр + толщина стенки (вместо жестких 10мм)
            double clearance = Math.Max(1.0, part.Destiny);
            double minSpacing = holes.Count > 0 ? holes.Max() + clearance : 0;

            double requiredLength = holes.Count * minSpacing;
            if (requiredLength > availableLength)
            {
                int maxPossible = (int)(availableLength / minSpacing);
                return (false, $"Недостаточно места для {holes.Count} отверстий. Максимум: {maxPossible} шт. при длине {part.Length}мм");
            }

            // Проверка минимального диаметра относительно толщины стенки (оставляем как рекомендацию, но можно сделать строже)
            double minDiameter = holes.Min();
            if (minDiameter < part.Destiny * 0.8)
            {
                return (false, $"Диаметр отверстия {minDiameter}мм меньше толщины стенки {part.Destiny}мм. Рекомендуется не менее 80% от толщины.");
            }

            return (true, null);
        }

        private static bool ValidateHoleSpacing(List<(Point Position, Hole Hole)> positions, double clearance)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                var (pos1, hole1) = positions[i];
                double radius1 = hole1.Diameter / 2.0;

                for (int j = i + 1; j < positions.Count; j++)
                {
                    var (pos2, hole2) = positions[j];
                    double radius2 = hole2.Diameter / 2.0;

                    double distance = Math.Sqrt(Math.Pow(pos2.X - pos1.X, 2) + Math.Pow(pos2.Y - pos1.Y, 2));

                    // 🔥 ИСПРАВЛЕНО: Минимальное расстояние между центрами = радиус1 + радиус2 + зазор (clearance)
                    // Раньше было (radius1 + clearance) + (radius2 + clearance), что ошибочно удваивало требуемый зазор!
                    double minRequiredDistance = radius1 + radius2 + clearance;

                    if (distance < minRequiredDistance)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Рассчитывает позиции отверстий с учетом физического размера детали и технологических отступов.
        /// </summary>
        public static List<(Point Position, Hole Hole)> CalculateHolePositions(Part part)
        {
            var positions = new List<(Point Position, Hole Hole)>();

            // 1. Для произвольных форм используем точные координаты пользователя
            if (part.PartType == PartType.Custom && part.PlacedHoles != null && part.PlacedHoles.Count > 0)
            {
                foreach (var placed in part.PlacedHoles)
                    positions.Add((new Point(placed.X, placed.Y), new Hole(placed.Diameter)));
                return positions;
            }

            // 2. Для труб отверстия в сечении не визуализируем
            if (part.PartType == PartType.RoundTube || part.PartType == PartType.RectangularTube)
                return positions;

            var allHoles = part.HoleGroups
                .SelectMany(g => Enumerable.Repeat(new Hole(g.Diameter), g.Count))
                .ToList();

            int count = allHoles.Count;
            if (count == 0) return positions;

            double clearance = Math.Max(1.0, part.Destiny);
            double maxD = allHoles.Max(h => h.Diameter);

            // 🔥 КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: minMargin должен включать радиус отверстия + технологический отступ
            double minMargin = (maxD / 2.0) + clearance;
            double pitch = maxD + clearance; // Минимально допустимый шаг между центрами отверстий

            if (part.PartType == PartType.Rectangle)
            {
                double availableW = part.Width - 2 * minMargin;
                double availableH = part.Height - 2 * minMargin;

                int maxCols = availableW > 0 ? (int)Math.Floor(availableW / pitch) + 1 : 0;
                maxCols = Math.Max(1, maxCols);

                int rows = (int)Math.Ceiling((double)count / maxCols);
                double stepY = rows > 1 ? availableH / (rows - 1) : 0;

                int index = 0;
                for (int row = 0; row < rows && index < count; row++)
                {
                    int colsInThisRow = Math.Min(maxCols, count - index);
                    double stepX = colsInThisRow > 1 ? availableW / (colsInThisRow - 1) : 0;

                    double startX = colsInThisRow > 1 ? -part.Width / 2 + minMargin : 0;
                    double y = rows > 1 ? (-part.Height / 2 + minMargin + row * stepY) : 0;

                    for (int c = 0; c < colsInThisRow && index < count; c++)
                    {
                        double x = colsInThisRow > 1 ? startX + c * stepX : 0;
                        positions.Add((new Point(x, y), allHoles[index++]));
                    }
                }
            }
            else if (part.PartType == PartType.Triangle)
            {
                int rows = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count * 1.5)));
                double availableH = part.Height - 2 * minMargin;
                double spacingY = rows > 1 ? availableH / (rows - 1) : 0;

                int index = 0;
                for (int row = 0; row < rows && index < count; row++)
                {
                    // 🔥 ИСПРАВЛЕНО: y начинается с учетом minMargin от верхнего края
                    double y = (part.Height / 2) - minMargin - row * spacingY;
                    if (rows == 1) y = 0;

                    double xLeft = -part.Width / 2;
                    double xRight = -part.Width / 2 + (part.Width / part.Height) * (y + part.Height / 2);
                    double currentWidth = xRight - xLeft;

                    // 🔥 ИСПРАВЛЕНО: availableWidth учитывает minMargin с обеих сторон
                    double availableWidth = currentWidth - 2 * minMargin;

                    int holesInThisRow = 0;
                    double currentSpacingX = 0;

                    if (availableWidth >= 0)
                    {
                        holesInThisRow = (int)Math.Floor(availableWidth / pitch) + 1;
                        holesInThisRow = Math.Max(1, holesInThisRow);
                        if (holesInThisRow > 1)
                            currentSpacingX = availableWidth / (holesInThisRow - 1);
                    }
                    else if (currentWidth > maxD + 2 * clearance) // Если влезает хотя бы одно с отступами
                    {
                        holesInThisRow = 1;
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
                // 🔥 ИСПРАВЛЕНО: maxRadius учитывает minMargin, чтобы край отверстия не вылезал за контур
                double maxRadius = (part.Width / 2) - minMargin;

                if (count == 1)
                {
                    positions.Add((new Point(0, 0), allHoles[0]));
                }
                else if (count <= 6)
                {
                    // Размещаем на радиусе, который гарантирует отступ от края (немного уменьшаем для эстетики, но не более maxRadius)
                    double radius = Math.Max(0, Math.Min(maxRadius, maxRadius * 0.8));

                    for (int i = 0; i < count; i++)
                    {
                        double angle = 2 * Math.PI * i / count - Math.PI / 2;
                        positions.Add((new Point(Math.Cos(angle) * radius, Math.Sin(angle) * radius), allHoles[i]));
                    }
                }
                else
                {
                    int innerCount = Math.Min(6, count);
                    int outerCount = count - innerCount;

                    double innerRadius = Math.Max(0, maxRadius * 0.4);
                    for (int i = 0; i < innerCount; i++)
                    {
                        double angle = 2 * Math.PI * i / innerCount - Math.PI / 2;
                        positions.Add((new Point(Math.Cos(angle) * innerRadius, Math.Sin(angle) * innerRadius), allHoles[i]));
                    }

                    if (outerCount > 0)
                    {
                        double outerRadius = Math.Max(0, maxRadius);
                        for (int i = 0; i < outerCount; i++)
                        {
                            double angle = 2 * Math.PI * i / outerCount - Math.PI / 2;
                            positions.Add((new Point(Math.Cos(angle) * outerRadius, Math.Sin(angle) * outerRadius), allHoles[innerCount + i]));
                        }
                    }
                }
            }

            return positions;
        }

        private static PathFigure CreateHoleFigure(double centerX, double centerY, double radius)
        {
            return CreateCircleFigure(centerX, centerY, radius, true, SweepDirection.Counterclockwise);
        }
    }
}