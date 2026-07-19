using System;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code.Utils
{
    public static class SkylineNestingHelper
    {
        private const double Epsilon = 0.01;

        /// <summary>
        /// Основной метод нестинга с использованием Skyline-алгоритма на листе заданного размера.
        /// </summary>
        public static List<NestingSheet> CreateNestingSkyline(
            List<Part> parts,
            double sheetWidth = 3000,
            double sheetHeight = 1500,
            double spacing = 10) // 🔥 Добавлен параметр spacing по умолчанию
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
                        StockHeight = sheetHeight,
                        Spacing = spacing // 🔥 Передаем отступ при создании
                    };

                    if (TryPlaceWithSkyline(newSheet, part))
                    {
                        sheets.Add(newSheet);
                    }
                }
            }

            // Финальная оптимизация размеров всех листов
            foreach (var sheet in sheets)
                NestingHelper.OptimizeSheetSize(sheet);

            return sheets;
        }

        /// <summary>
        /// АВТОПОДБОР ЛИСТА с автоматическим расчетом отступа на основе толщины.
        /// Выбрасывает InvalidOperationException, если материал не распознан или детали не влезают ни в один формат.
        /// </summary>
        public static List<NestingSheet> CreateNestingSkylineAutoSheet(
            List<Part> parts,
            string materialName,
            double thickness,
            double spacing = 0) // 0 означает "вычислить автоматически"
        {
            var allowedStocks = SheetStockConfig.GetAllowedStocks(materialName, thickness);

            if (!allowedStocks.Any())
            {
                throw new InvalidOperationException($"Нет доступных типоразмеров для материала '{materialName}'.");
            }

            // 🔥 АВТОМАТИЧЕСКИЙ РАСЧЕТ ОТСТУПА:
            // Если толщина <= 10, отступ = 10. Если толщина > 10, отступ = толщине.
            double autoSpacing = Math.Max(10.0, thickness);

            // Используем переданный вручную отступ, если он > 0, иначе берем автоматический
            double finalSpacing = spacing > 0 ? spacing : autoSpacing;

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

                    // 🔥 Передаем вычисленный finalSpacing в метод подбора листа
                    var bestSheet = FindBestNewSheetForPart(part, allowedStocks, remainingParts, finalSpacing);

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
        /// </summary>
        private static NestingSheet? FindBestNewSheetForPart(
            Part currentPart,
            List<SheetStock> allowedStocks,
            List<Part> remainingParts,
            double spacing)
        {
            if (!allowedStocks.Any()) return null;

            if (allowedStocks.Count == 1)
            {
                var stock = allowedStocks.First();
                var sheet = new NestingSheet { StockWidth = stock.Width, StockHeight = stock.Height, Spacing = spacing };

                if (TryPlaceWithSkyline(sheet, currentPart))
                    return sheet;

                return null;
            }

            double totalRemainingArea = remainingParts.Sum(p => p.Width * p.Height);
            double requiredAreaWithWaste = totalRemainingArea * 1.15;

            var sortedStocks = allowedStocks.OrderBy(s => s.Area).ToList();

            SheetStock? candidateStock = null;
            foreach (var stock in sortedStocks)
            {
                if (stock.Area >= requiredAreaWithWaste)
                {
                    candidateStock = stock;
                    break;
                }
            }

            if (candidateStock == null)
            {
                candidateStock = sortedStocks.Last();
            }

            var testSheet = new NestingSheet
            {
                StockWidth = candidateStock.Width,
                StockHeight = candidateStock.Height,
                Spacing = spacing // 🔥 Передаем отступ
            };

            if (TryPlaceWithSkyline(testSheet, currentPart))
            {
                return testSheet;
            }

            foreach (var stock in sortedStocks)
            {
                if (stock.Area < candidateStock.Area) continue;

                testSheet = new NestingSheet { StockWidth = stock.Width, StockHeight = stock.Height, Spacing = spacing };
                if (TryPlaceWithSkyline(testSheet, currentPart))
                {
                    return testSheet;
                }
            }

            return null;
        }

        /// <summary>
        /// Попытка разместить деталь на листе с использованием Skyline
        /// </summary>
        private static bool TryPlaceWithSkyline(NestingSheet sheet, Part part)
        {
            if (part.PartType == PartType.Round)
                return TryPlaceWithSkylineRotation(sheet, part, 0);

            var result0 = EstimateSkylinePlacement(sheet, part, 0);
            var result90 = EstimateSkylinePlacement(sheet, part, 90);

            if (!result0.CanPlace && !result90.CanPlace)
                return false;

            PlacementResult best;
            if (!result0.CanPlace)
                best = result90;
            else if (!result90.CanPlace)
                best = result0;
            else
            {
                var impact0 = EstimateSheetImpact(sheet, result0.X, result0.Y, result0.Width, result0.Height);
                var impact90 = EstimateSheetImpact(sheet, result90.X, result90.Y, result90.Width, result90.Height);

                if (impact0.EstimatedArea == double.MaxValue) best = result90;
                else if (impact90.EstimatedArea == double.MaxValue) best = result0;
                else
                {
                    if (impact90.EstimatedArea < impact0.EstimatedArea - Epsilon)
                        best = result90;
                    else if (impact0.EstimatedArea < impact90.EstimatedArea - Epsilon)
                        best = result0;
                    else
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

            // 🔥 ЗАМЕНА: Используем sheet.Spacing
            double sp = sheet.Spacing;
            double effectiveWidth = sheetWidth - sp;
            double effectiveHeight = sheetHeight - sp;

            bool isWidePart = partWidth > (effectiveWidth) * 0.90;

            // ========================================================================
            // СПЕЦИАЛЬНАЯ ЛОГИКА ДЛЯ ШИРОКИХ ДЕТАЛЕЙ: вертикальное штабелирование
            // ========================================================================
            if (isWidePart && sheet.Parts.Any())
            {
                foreach (var existing in sheet.Parts)
                {
                    var (ew, eh) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);

                    if (Math.Abs(ew - partWidth) <= sp) // 🔥 ЗАМЕНА
                    {
                        double stackX = existing.X;
                        double stackY = existing.Y + eh + sp; // 🔥 ЗАМЕНА

                        if (stackY + partHeight > effectiveHeight)
                            continue;

                        if (stackX + partWidth > effectiveWidth)
                            continue;

                        if (IsOverlappingAny(sheet, stackX, stackY, partWidth, partHeight))
                            continue;

                        double finalX = FindLeftmostPosition(sheet, stackX, stackY, partWidth, partHeight, sheetWidth, sp); // 🔥 ЗАМЕНА: передача sp

                        double score = stackY * 10000 + finalX;

                        candidates.Add(new PlacementCandidate
                        {
                            X = finalX,
                            Y = stackY,
                            FitScore = score,
                            SegmentIndex = -1
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

                double baseX = segment.XStart + sp; // 🔥 ЗАМЕНА
                double candidateY = segment.YHeight + sp; // 🔥 ЗАМЕНА

                if (candidateY + partHeight > effectiveHeight)
                    continue;

                double requiredRightEdge = segment.XStart + partWidth;
                bool canFitWidth = false;

                for (int j = i; j < skyline.Count; j++)
                {
                    var s = skyline[j];

                    if (s.YHeight > segment.YHeight + Epsilon)
                        break;

                    if (s.XEnd >= requiredRightEdge - Epsilon)
                    {
                        canFitWidth = true;
                        break;
                    }
                }

                if (!canFitWidth)
                    continue;

                if (baseX + partWidth > effectiveWidth)
                    continue;

                double finalX = FindLeftmostPosition(sheet, baseX, candidateY, partWidth, partHeight, sheetWidth, sp); // 🔥 ЗАМЕНА: передача sp

                if (IsOverlappingAny(sheet, finalX, candidateY, partWidth, partHeight))
                    continue;

                double score = finalX * 10000 + candidateY;

                candidates.Add(new PlacementCandidate
                {
                    X = finalX,
                    Y = candidateY,
                    FitScore = score,
                    SegmentIndex = i
                });
            }

            return candidates
                .OrderBy(c => c.FitScore)
                .ThenBy(c => c.SegmentIndex == -1 ? 0 : 1)
                .ThenBy(c => c.X)
                .ThenBy(c => c.Y)
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
            double sheetWidth,
            double spacing) // 🔥 ЗАМЕНА: добавлен параметр spacing
        {
            double bestX = candidateX;

            for (double testX = spacing; testX <= candidateX + Epsilon; testX += 1) // 🔥 ЗАМЕНА
            {
                if (testX + partWidth > sheetWidth - spacing) // 🔥 ЗАМЕНА
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
            // 🔥 ЗАМЕНА: Используем sheet.Spacing
            double sp = sheet.Spacing;
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

            double reqW = newMaxX + sp * 2; // 🔥 ЗАМЕНА
            double reqH = newMaxY + sp * 2; // 🔥 ЗАМЕНА

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
            // 🔥 ЗАМЕНА: Используем sheet.Spacing
            double sp = sheet.Spacing;

            foreach (var existing in sheet.Parts)
            {
                var (ew, eh) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);
                double ex = existing.X;
                double ey = existing.Y;

                if (x + width + sp <= ex) continue; // 🔥 ЗАМЕНА
                if (ex + ew + sp <= x) continue;    // 🔥 ЗАМЕНА
                if (y + height + sp <= ey) continue; // 🔥 ЗАМЕНА
                if (ey + eh + sp <= y) continue;    // 🔥 ЗАМЕНА

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

        // --- Вспомогательные структуры ---

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