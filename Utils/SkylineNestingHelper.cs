using System;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code.Utils
{
    public static class SkylineNestingHelper
    {
        public const double Spacing = 10;
        private const double Epsilon = 0.01;

        /// <summary>
        /// Основной метод нестинга с использованием Skyline-алгоритма на листе заданного размера.
        /// </summary>
        public static List<NestingSheet> CreateNestingSkyline(
            List<Part> parts,
            double sheetWidth = 3000,
            double sheetHeight = 1500)
        {
            // Подготовка: размножение и сортировка деталей
            var allParts = parts
                .SelectMany(p => Enumerable.Repeat(p, p.Count))
                .OrderByDescending(p => Math.Max(p.Width, p.Height)) // Сначала высокие
                .ThenByDescending(p => p.Width * p.Height)           // Затем по площади
                .ThenBy(p => Math.Abs(p.Width - p.Height))           // Затем квадратные
                .ToList();

            var sheets = new List<NestingSheet>();

            foreach (var part in allParts)
            {
                bool placed = false;

                // Пробуем разместить на существующих листах
                foreach (var sheet in sheets)
                {
                    if (TryPlaceWithSkyline(sheet, part))
                    {
                        placed = true;
                        break;
                    }
                }

                // Создаём новый лист, если не разместили
                if (!placed)
                {
                    var newSheet = new NestingSheet
                    {
                        StockWidth = sheetWidth,
                        StockHeight = sheetHeight
                    };

                    if (TryPlaceWithSkyline(newSheet, part))
                    {
                        sheets.Add(newSheet);
                    }
                    // Если деталь не влезает даже на пустой лист нового размера, она пропускается
                    // (или можно выбросить исключение, в зависимости от требований)
                }
            }

            // Финальная оптимизация размеров всех листов
            foreach (var sheet in sheets)
                NestingHelper.OptimizeSheetSize(sheet);

            return sheets;
        }

        /// <summary>
        /// АВТОПОДБОР ЛИСТА.
        /// Выбрасывает InvalidOperationException, если материал не распознан или детали не влезают ни в один формат.
        /// </summary>
        public static List<NestingSheet> CreateNestingSkylineAutoSheet(
            List<Part> parts,
            string materialName,
            double thickness)
        {
            var allowedStocks = SheetStockConfig.GetAllowedStocks(materialName, thickness);

            if (!allowedStocks.Any())
            {
                throw new InvalidOperationException($"Нет доступных типоразмеров для материала '{materialName}'.");
            }

            var allParts = parts
                .SelectMany(p => Enumerable.Repeat(p, p.Count))
                .OrderByDescending(p => Math.Max(p.Width, p.Height))
                .ThenByDescending(p => p.Width * p.Height)
                .ToList();

            var sheets = new List<NestingSheet>();
            var unplacedParts = new List<Part>();

            for (int i = 0; i < allParts.Count; i++)
            {
                var part = allParts[i];
                bool placed = false;

                // Пробуем разместить на существующих листах
                foreach (var sheet in sheets)
                {
                    if (TryPlaceWithSkyline(sheet, part))
                    {
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    // Список оставшихся деталей (текущая + все следующие)
                    var remainingParts = allParts.Skip(i).ToList();

                    var bestSheet = FindBestNewSheetForPart(part, allowedStocks, remainingParts);

                    if (bestSheet != null)
                    {
                        sheets.Add(bestSheet);
                    }
                    else
                    {
                        unplacedParts.Add(part);
                    }
                }
            }

            if (unplacedParts.Any())
            {
                var maxStock = allowedStocks.OrderByDescending(s => s.Width).First();
                throw new InvalidOperationException(
                    $"Детали не помещаются в доступные листы. Максимальный формат: {maxStock.Width}x{maxStock.Height}. " +
                    $"Проблемные детали: {string.Join(", ", unplacedParts.Take(5).Select(p => p.Title))}");
            }

            foreach (var sheet in sheets)
                NestingHelper.OptimizeSheetSize(sheet);

            return sheets;
        }

        /// <summary>
        /// Выбирает размер нового листа на основе суммарной площади оставшихся деталей.
        /// Стратегия: Использовать наименьший возможный лист, который вместит все остатки.
        /// </summary>
        private static NestingSheet? FindBestNewSheetForPart(
            Part currentPart,
            List<SheetStock> allowedStocks,
            List<Part> remainingParts)
        {
            if (!allowedStocks.Any()) return null;

            // Оптимизация: Если доступен только один тип листа, сразу создаем его
            if (allowedStocks.Count == 1)
            {
                var stock = allowedStocks.First();
                var sheet = new NestingSheet { StockWidth = stock.Width, StockHeight = stock.Height };

                // Проверяем, влезает ли хоть текущая деталь
                if (TryPlaceWithSkyline(sheet, currentPart))
                    return sheet;

                return null; // Деталь не влезает даже в единственный доступный формат
            }

            // 1. Рассчитываем суммарную площадь всех оставшихся деталей
            double totalRemainingArea = remainingParts.Sum(p => p.Width * p.Height);

            // Добавляем коэффициент "запаса" на неизбежные отходы при нестинге (например, 15%)
            // Если площадь деталей 10 м2, нам нужно минимум ~11.5 м2 листа.
            double requiredAreaWithWaste = totalRemainingArea * 1.15;

            // 2. Сортируем листы от меньшего к большему
            var sortedStocks = allowedStocks.OrderBy(s => s.Area).ToList();

            // 3. Ищем наименьший лист, который теоретически вместит все остатки
            SheetStock? candidateStock = null;
            foreach (var stock in sortedStocks)
            {
                if (stock.Area >= requiredAreaWithWaste)
                {
                    candidateStock = stock;
                    break; // Берем первый подходящий (он будет наименьшим из подходящих)
                }
            }

            // 4. Если ни один лист не вмещает всю массу остатков, берем самый большой доступный
            if (candidateStock == null)
            {
                candidateStock = sortedStocks.Last();
            }

            // 5. КРИТИЧЕСКАЯ ПРОВЕРКА: Влезает ли ТЕКУЩАЯ деталь в выбранный кандидат?
            // Бывает, что общая площадь мелочевки большая, но одна деталь длинная/широкая и не влезает в малый лист.
            var testSheet = new NestingSheet
            {
                StockWidth = candidateStock.Width,
                StockHeight = candidateStock.Height
            };

            if (TryPlaceWithSkyline(testSheet, currentPart))
            {
                // Все ок, возвращаем этот лист
                return testSheet;
            }

            // 6. FALLBACK: Если текущая деталь не влезла в "экономичный" лист,
            // ищем следующий по размеру лист, куда она влезет.
            foreach (var stock in sortedStocks)
            {
                if (stock.Area < candidateStock.Area) continue; // Пропускаем те, что уже проверили и они меньше

                testSheet = new NestingSheet { StockWidth = stock.Width, StockHeight = stock.Height };
                if (TryPlaceWithSkyline(testSheet, currentPart))
                {
                    return testSheet;
                }
            }

            // Если ничего не подошло (деталь гигантская), возвращаем null (обработается как ошибка выше)
            return null;
        }

        /// <summary>
        /// Попытка разместить деталь на листе с использованием Skyline
        /// </summary>
        private static bool TryPlaceWithSkyline(NestingSheet sheet, Part part)
        {
            // Для кругов поворот не имеет смысла
            if (part.PartType == PartType.Round)
                return TryPlaceWithSkylineRotation(sheet, part, 0);

            // Оцениваем обе ориентации
            var result0 = EstimateSkylinePlacement(sheet, part, 0);
            var result90 = EstimateSkylinePlacement(sheet, part, 90);

            if (!result0.CanPlace && !result90.CanPlace)
                return false;

            // Выбираем лучшую ориентацию
            PlacementResult best;
            if (!result0.CanPlace)
                best = result90;
            else if (!result90.CanPlace)
                best = result0;
            else
            {
                // ГЛОБАЛЬНАЯ ОЦЕНКА: сравниваем влияние на отход листа
                var impact0 = EstimateSheetImpact(sheet, result0.X, result0.Y, result0.Width, result0.Height);
                var impact90 = EstimateSheetImpact(sheet, result90.X, result90.Y, result90.Width, result90.Height);

                if (impact0.EstimatedArea == double.MaxValue) best = result90;
                else if (impact90.EstimatedArea == double.MaxValue) best = result0;
                else
                {
                    // Выбираем ориентацию с МЕНЬШЕЙ итоговой площадью
                    if (impact90.EstimatedArea < impact0.EstimatedArea - Epsilon)
                        best = result90;
                    else if (impact0.EstimatedArea < impact90.EstimatedArea - Epsilon)
                        best = result0;
                    else
                        // При равной площади оставляем локально более выгодную позицию (ниже/левее)
                        best = result0.FitScore <= result90.FitScore ? result0 : result90;
                }
            }

            if (best.CanPlace)
            {
                sheet.Parts.Add(new PartPlacement
                {
                    Part = part,
                    X = best.X,
                    Y = best.Y,
                    Rotation = best.Rotation
                });
                return true;
            }

            return false;
        }

        /// <summary>
        /// Размещает деталь в заданной ориентации с использованием Skyline
        /// </summary>
        private static bool TryPlaceWithSkylineRotation(NestingSheet sheet, Part part, double rotation)
        {
            var placementResult = EstimateSkylinePlacement(sheet, part, rotation);

            if (!placementResult.CanPlace)
                return false;

            sheet.Parts.Add(new PartPlacement
            {
                Part = part,
                X = placementResult.X,
                Y = placementResult.Y,
                Rotation = rotation
            });

            return true;
        }

        /// <summary>
        /// Оценка размещения для конкретной ориентации
        /// </summary>
        private static PlacementResult EstimateSkylinePlacement(
            NestingSheet sheet,
            Part part,
            double rotation)
        {
            var (partWidth, partHeight) = NestingHelper.GetPartDimensions(part, rotation);
            var skyline = BuildSkyline(sheet);

            var candidates = FindPlacementCandidates(
                skyline,
                partWidth,
                partHeight,
                sheet.Width,
                sheet.Height,
                sheet);

            if (!candidates.Any())
                return new PlacementResult { CanPlace = false };

            var best = candidates.OrderBy(c => c).First();

            return new PlacementResult
            {
                CanPlace = true,
                X = best.X,
                Y = best.Y,
                Rotation = rotation,
                Width = partWidth,
                Height = partHeight,
                FitScore = best.FitScore
            };
        }

        /// <summary>
        /// Строит текущий Skyline из размещённых деталей
        /// </summary>
        private static List<SkylineSegment> BuildSkyline(NestingSheet sheet)
        {
            if (!sheet.Parts.Any())
            {
                return new List<SkylineSegment>
                {
                    new SkylineSegment { XStart = 0, XEnd = sheet.Width, YHeight = 0 }
                };
            }

            var events = new List<(double X, double Y, bool IsEnd)>();

            foreach (var p in sheet.Parts)
            {
                var (w, h) = NestingHelper.GetPartDimensions(p.Part, p.Rotation);
                events.Add((p.X, p.Y + h, false));  // Начало верхней границы
                events.Add((p.X + w, p.Y + h, true)); // Конец верхней границы
            }

            events = events.OrderBy(e => e.X).ThenBy(e => e.IsEnd ? 1 : 0).ToList();

            var skyline = new List<SkylineSegment>();
            var activeHeights = new SortedList<double, int>();
            activeHeights[0] = 1; // Базовый уровень

            double lastX = 0;

            foreach (var evt in events)
            {
                if (evt.X > lastX + Epsilon)
                {
                    var currentHeight = activeHeights.Last().Key;
                    skyline.Add(new SkylineSegment
                    {
                        XStart = lastX,
                        XEnd = evt.X,
                        YHeight = currentHeight
                    });
                }

                if (!evt.IsEnd)
                {
                    if (!activeHeights.ContainsKey(evt.Y)) activeHeights[evt.Y] = 0;
                    activeHeights[evt.Y]++;
                }
                else
                {
                    if (activeHeights.ContainsKey(evt.Y))
                    {
                        activeHeights[evt.Y]--;
                        if (activeHeights[evt.Y] == 0) activeHeights.Remove(evt.Y);
                    }
                }
                lastX = evt.X;
            }

            if (lastX < sheet.Width - Epsilon)
            {
                skyline.Add(new SkylineSegment
                {
                    XStart = lastX,
                    XEnd = sheet.Width,
                    YHeight = activeHeights.Any() ? activeHeights.Last().Key : 0
                });
            }

            return skyline;
        }

        /// <summary>
        /// Находит все возможные позиции размещения на основе Skyline.
        /// С приоритетом колонок (слева-направо, внутри колонки снизу-вверх).
        /// Для широких деталей (>90% ширины листа) добавляет позиции вертикального штабелирования.
        /// </summary>
        private static List<PlacementCandidate> FindPlacementCandidates(
            List<SkylineSegment> skyline,
            double partWidth,
            double partHeight,
            double sheetWidth,
            double sheetHeight,
            NestingSheet sheet)
        {
            var candidates = new List<PlacementCandidate>();
            double effectiveWidth = sheetWidth - Spacing;
            double effectiveHeight = sheetHeight - Spacing;

            // 🔧 ОПРЕДЕЛЯЕМ: является ли деталь "широкой" (занимает >90% полезной ширины листа)
            bool isWidePart = partWidth > (effectiveWidth) * 0.90;

            // ========================================================================
            // СПЕЦИАЛЬНАЯ ЛОГИКА ДЛЯ ШИРОКИХ ДЕТАЛЕЙ: вертикальное штабелирование
            // ========================================================================
            if (isWidePart && sheet.Parts.Any())
            {
                // Ищем на листе детали примерно той же ширины, чтобы поставить новую поверх них
                foreach (var existing in sheet.Parts)
                {
                    var (ew, eh) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);

                    // Если ширина существующей детали близка к ширине новой (допуск = Spacing)
                    if (Math.Abs(ew - partWidth) <= Spacing)
                    {
                        // Позиция: та же координата X, сразу над существующей деталью + отступ
                        double stackX = existing.X;
                        double stackY = existing.Y + eh + Spacing;

                        // Проверка: не вылезает ли за верх листа
                        if (stackY + partHeight > effectiveHeight)
                            continue;

                        // Проверка: не вылезает ли за правый край (на всякий случай)
                        if (stackX + partWidth > effectiveWidth)
                            continue;

                        // Проверка на пересечения с другими деталями (страховка)
                        if (IsOverlappingAny(sheet, stackX, stackY, partWidth, partHeight))
                            continue;

                        // Уплотнение влево (для широких деталей обычно не сдвинет, но на всякий случай)
                        double finalX = FindLeftmostPosition(sheet, stackX, stackY, partWidth, partHeight, sheetWidth);

                        // Скоринг: приоритет по минимальному Y (ниже = лучше), затем по минимальному X
                        double score = stackY * 10000 + finalX;

                        candidates.Add(new PlacementCandidate
                        {
                            X = finalX,
                            Y = stackY,
                            FitScore = score,
                            SegmentIndex = -1 // Маркер: позиция из "штабелирования"
                        });
                    }
                }
            }

            // ========================================================================
            // ОБЫЧНАЯ ЛОГИКА: поиск позиций на основе Skyline-сегментов
            // ========================================================================
            for (int i = 0; i < skyline.Count; i++)
            {
                var segment = skyline[i];

                // Базовая позиция: левый край сегмента + отступ
                double baseX = segment.XStart + Spacing;
                double candidateY = segment.YHeight + Spacing;

                // Проверка по высоте листа
                if (candidateY + partHeight > effectiveHeight)
                    continue;

                // Проверка: влезает ли деталь по ширине в непрерывную область
                double requiredRightEdge = segment.XStart + partWidth;
                bool canFitWidth = false;

                for (int j = i; j < skyline.Count; j++)
                {
                    var s = skyline[j];

                    // Если сегмент выше допустимого уровня — прерываем (не можем «перепрыгнуть»)
                    if (s.YHeight > segment.YHeight + Epsilon)
                        break;

                    // Если достигли нужной ширины — успех
                    if (s.XEnd >= requiredRightEdge - Epsilon)
                    {
                        canFitWidth = true;
                        break;
                    }
                }

                if (!canFitWidth)
                    continue;

                // Проверка по ширине листа
                if (baseX + partWidth > effectiveWidth)
                    continue;

                // 🔧 УПЛОТНЕНИЕ ВЛЕВО: находим максимально левую позицию на этом уровне Y
                double finalX = FindLeftmostPosition(sheet, baseX, candidateY, partWidth, partHeight, sheetWidth);

                // Финальная проверка пересечений (страховка)
                if (IsOverlappingAny(sheet, finalX, candidateY, partWidth, partHeight))
                    continue;

                // 🔧 СКОРИНГ: приоритет по X (колонки слева-направо), затем по Y (снизу-вверх)
                double score = finalX * 10000 + candidateY;

                candidates.Add(new PlacementCandidate
                {
                    X = finalX,
                    Y = candidateY,
                    FitScore = score,
                    SegmentIndex = i
                });
            }

            // ========================================================================
            // ФИНАЛЬНАЯ СОРТИРОВКА И ВОЗВРАТ
            // ========================================================================

            // Если есть кандидаты от штабелирования (SegmentIndex == -1), отдаём им приоритет
            // при равном скоринге, так как они гарантированно эффективны для широких деталей
            return candidates
                .OrderBy(c => c.FitScore)                    // Сначала по скорингу
                .ThenBy(c => c.SegmentIndex == -1 ? 0 : 1)   // Затем приоритет штабелированным позициям
                .ThenBy(c => c.X)                            // Затем по горизонтали
                .ThenBy(c => c.Y)                            // Затем по вертикали
                .ToList();
        }

        /// <summary>
        /// Находит максимально левую позицию для детали на заданной высоте Y
        /// </summary>
        private static double FindLeftmostPosition(
            NestingSheet sheet,
            double candidateX,
            double candidateY,
            double partWidth,
            double partHeight,
            double sheetWidth)
        {
            double bestX = candidateX;

            for (double testX = Spacing; testX <= candidateX + Epsilon; testX += 1)
            {
                if (testX + partWidth > sheetWidth - Spacing)
                    break;

                if (!IsOverlappingAny(sheet, testX, candidateY, partWidth, partHeight))
                {
                    bestX = testX;
                    break;
                }
            }

            return bestX;
        }

        /// <summary>
        /// Оценивает, какими станут оптимизированные размеры листа после размещения детали.
        /// </summary>
        private static (double OptWidth, double OptHeight, double EstimatedArea) EstimateSheetImpact(
            NestingSheet sheet, double partX, double partY, double partWidth, double partHeight)
        {
            double currentMaxX = 0;
            double currentMaxY = 0;

            if (sheet.Parts.Any())
            {
                currentMaxX = sheet.Parts.Max(p =>
                {
                    var d = NestingHelper.GetPartDimensions(p.Part, p.Rotation);
                    return p.X + d.Width;
                });
                currentMaxY = sheet.Parts.Max(p =>
                {
                    var d = NestingHelper.GetPartDimensions(p.Part, p.Rotation);
                    return p.Y + d.Height;
                });
            }

            double newMaxX = Math.Max(currentMaxX, partX + partWidth);
            double newMaxY = Math.Max(currentMaxY, partY + partHeight);

            double reqW = newMaxX + Spacing * 2;
            double reqH = newMaxY + Spacing * 2;

            if (reqW > sheet.StockWidth || reqH > sheet.StockHeight)
                return (double.MaxValue, double.MaxValue, double.MaxValue);

            double wasteW = sheet.StockWidth - reqW;
            double wasteH = sheet.StockHeight - reqH;

            if (wasteW > wasteH)
            {
                double optW = Math.Ceiling(reqW / 100) * 100;
                double finalW = Math.Min(optW, sheet.StockWidth);
                return (finalW, sheet.StockHeight, finalW * sheet.StockHeight);
            }
            else
            {
                double optH = Math.Ceiling(reqH / 100) * 100;
                double finalH = Math.Min(optH, sheet.StockHeight);
                return (sheet.StockWidth, finalH, sheet.StockWidth * finalH);
            }
        }

        /// <summary>
        /// Проверка пересечений с уже размещёнными деталями
        /// </summary>
        private static bool IsOverlappingAny(NestingSheet sheet, double x, double y, double width, double height)
        {
            foreach (var existing in sheet.Parts)
            {
                var (ew, eh) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);
                double ex = existing.X;
                double ey = existing.Y;

                if (x + width + Spacing <= ex) continue;
                if (ex + ew + Spacing <= x) continue;
                if (y + height + Spacing <= ey) continue;
                if (ey + eh + Spacing <= y) continue;

                return true;
            }
            return false;
        }

        /// <summary>
        /// Расчёт процента использования материала
        /// </summary>
        public static double CalculateUtilization(NestingSheet sheet)
        {
            if (sheet.OptimizedWidth * sheet.OptimizedHeight < Epsilon) return 0;

            double usedArea = sheet.Parts.Sum(p =>
            {
                var (w, h) = NestingHelper.GetPartDimensions(p.Part, p.Rotation);
                return w * h;
            });

            double totalArea = sheet.OptimizedWidth * sheet.OptimizedHeight;
            return usedArea / totalArea * 100;
        }

        // --- Вспомогательные структуры (упрощенные) ---

        private class PlacementResult
        {
            public bool CanPlace { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Rotation { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double FitScore { get; set; }
        }

        public class SkylineSegment
        {
            public double XStart { get; set; }
            public double XEnd { get; set; }
            public double YHeight { get; set; }
            public double Width => XEnd - XStart;
        }

        public struct PlacementCandidate : IComparable<PlacementCandidate>
        {
            public double X;
            public double Y;
            public double FitScore;
            public int SegmentIndex;

            public int CompareTo(PlacementCandidate other) =>
                FitScore.CompareTo(other.FitScore);
        }
    }
}