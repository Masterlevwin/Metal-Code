using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Metal_Code.Utils
{
    public static class SkylineNestingHelper
    {
        private const double Epsilon = 0.01;

        public static List<NestingSheet> CreateNestingSkyline(
            List<Part> parts,
            double sheetWidth = 3000,
            double sheetHeight = 1500,
            double spacing = 10)
        {
            // 1. Размножаем детали в плоский список
            var allParts = parts.SelectMany(p => Enumerable.Repeat(p, p.Count)).ToList();

            // 2. Объединяем пары треугольников в прямоугольники для оптимальной упаковки
            var optimizedParts = PairTrianglesToRectangles(allParts);

            // 3. Сортировка для нестинга
            optimizedParts = optimizedParts
                .OrderByDescending(p => Math.Max(p.Width, p.Height))
                .ThenByDescending(p => p.Width * p.Height)
                .ThenBy(p => Math.Abs(p.Width - p.Height))
                .ToList();

            var sheets = new List<NestingSheet>();

            foreach (var part in optimizedParts)
            {
                bool placed = false;
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
                    var newSheet = new NestingSheet
                    {
                        StockWidth = sheetWidth,
                        StockHeight = sheetHeight,
                        Spacing = spacing
                    };

                    if (TryPlaceWithSkyline(newSheet, part))
                    {
                        sheets.Add(newSheet);
                    }
                }
            }

            // 4. Разворачиваем прямоугольники обратно в треугольники и оптимизируем размеры листов
            foreach (var sheet in sheets)
            {
                ExpandPairedTriangles(sheet);
                NestingHelper.OptimizeSheetSize(sheet);
            }

            return sheets;
        }

        public static List<NestingSheet> CreateNestingSkylineAutoSheet(
            List<Part> parts,
            string materialName,
            double thickness,
            double spacing = 0)
        {
            var allowedStocks = SheetStockConfig.GetAllowedStocks(materialName, thickness);

            if (!allowedStocks.Any())
            {
                throw new InvalidOperationException($"Нет доступных типоразмеров для материала '{materialName}'.");
            }

            double autoSpacing = Math.Max(10.0, thickness);
            double finalSpacing = spacing > 0 ? spacing : autoSpacing;

            // 1. Размножаем детали в плоский список
            var allParts = parts.SelectMany(p => Enumerable.Repeat(p, p.Count)).ToList();

            // 2. Объединяем пары треугольников в прямоугольники
            var optimizedParts = PairTrianglesToRectangles(allParts);

            // 3. Сортировка для нестинга
            optimizedParts = optimizedParts
                .OrderByDescending(p => Math.Max(p.Width, p.Height))
                .ThenByDescending(p => p.Width * p.Height)
                .ToList();

            var sheets = new List<NestingSheet>();
            var unplacedParts = new List<Part>();

            for (int i = 0; i < optimizedParts.Count; i++)
            {
                var part = optimizedParts[i];
                bool placed = false;

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
                    var remainingParts = optimizedParts.Skip(i).ToList();
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
                string problemParts = string.Join(", ", unplacedParts.Take(5).Select(p => p.Title));
                string moreText = unplacedParts.Count > 5 ? $"\n...и ещё {unplacedParts.Count - 5} деталей." : "";

                MessageBox.Show(
                    $"Не все детали удалось разместить на доступных листах.\n\n" +
                    $"Максимальный формат листа: {maxStock.Width}x{maxStock.Height} мм\n\n" +
                    $"Проблемные детали (слишком большие):\n{problemParts}{moreText}\n\n" +
                    $"Успешно размещено листов: {sheets.Count}\n" +
                    $"Не размещено деталей: {unplacedParts.Count}\n\n" +
                    $"Вы можете продолжить работу с размещенными листами или скорректировать заказ.",
                    "Предупреждение нестинга",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            // 4. Разворачиваем прямоугольники обратно в треугольники и оптимизируем размеры листов
            foreach (var sheet in sheets)
            {
                ExpandPairedTriangles(sheet);
                NestingHelper.OptimizeSheetSize(sheet);
            }

            return sheets;
        }

        private static List<Part> PairTrianglesToRectangles(List<Part> parts)
        {
            var result = new List<Part>();
            var triangles = parts.Where(p => p.PartType == PartType.Triangle).ToList();
            var otherParts = parts.Where(p => p.PartType != PartType.Triangle).ToList();

            if (triangles.Count == 0)
            {
                return parts;
            }

            var triangleGroups = triangles
                .GroupBy(p => $"{p.Width:F2}x{p.Height:F2}")
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var group in triangleGroups)
            {
                var triangleList = group.Value;
                int pairCount = triangleList.Count / 2;
                int remainder = triangleList.Count % 2;

                for (int i = 0; i < pairCount; i++)
                {
                    var template = triangleList[i * 2];
                    var rectanglePart = new Part
                    {
                        Title = $"{template.Title}_paired_{i}",
                        Count = 1,
                        Metal = template.Metal,
                        Destiny = template.Destiny,
                        Width = template.Width,
                        Height = template.Height,
                        PartType = PartType.Rectangle,

                        DisplayGeometry = template.DisplayGeometry?.Clone(),

                        PropsDict = new Dictionary<int, List<string>>(template.PropsDict ?? new Dictionary<int, List<string>>()),
                        HoleGroups = template.HoleGroups != null ?
                            new System.Collections.ObjectModel.ObservableCollection<HoleGroup>(template.HoleGroups) :
                            new System.Collections.ObjectModel.ObservableCollection<HoleGroup>()
                    };

                    rectanglePart.PropsDict[999] = new List<string> { "TRIANGLE_PAIR" };
                    result.Add(rectanglePart);
                }

                for (int i = 0; i < remainder; i++)
                {
                    result.Add(triangleList[triangleList.Count - 1 - i]);
                }
            }

            result.AddRange(otherParts);
            return result;
        }

        private static void ExpandPairedTriangles(NestingSheet sheet)
        {
            var newPlacements = new List<PartPlacement>();
            var processedIndices = new HashSet<int>();

            for (int i = 0; i < sheet.Parts.Count; i++)
            {
                if (processedIndices.Contains(i)) continue;

                var placement = sheet.Parts[i];

                bool isPairedRectangle = placement.Part.PartType == PartType.Rectangle &&
                    placement.Part.PropsDict.ContainsKey(999) &&
                    placement.Part.PropsDict[999].Contains("TRIANGLE_PAIR");

                if (isPairedRectangle && placement.Part.Title != null)
                {
                    string baseTitle = placement.Part.Title.Replace("_paired", "");

                    double baseRotation = placement.Rotation;
                    double rotation1 = baseRotation;
                    double rotation2 = (baseRotation + 180) % 360;

                    // Первый треугольник
                    var triangle1 = new Part
                    {
                        Title = baseTitle,
                        Count = 1,
                        Metal = placement.Part.Metal,
                        Destiny = placement.Part.Destiny,
                        Width = placement.Part.Width,
                        Height = placement.Part.Height,
                        PartType = PartType.Triangle,

                        DisplayGeometry = placement.Part.DisplayGeometry?.Clone(),

                        PropsDict = placement.Part.PropsDict != null ?
                            new Dictionary<int, List<string>>(placement.Part.PropsDict) :
                            new Dictionary<int, List<string>>(),
                        HoleGroups = placement.Part.HoleGroups != null ?
                            new System.Collections.ObjectModel.ObservableCollection<HoleGroup>(placement.Part.HoleGroups) :
                            new System.Collections.ObjectModel.ObservableCollection<HoleGroup>()
                    };

                    if (triangle1.PropsDict.ContainsKey(999))
                        triangle1.PropsDict.Remove(999);

                    newPlacements.Add(new PartPlacement { Part = triangle1, X = placement.X, Y = placement.Y, Rotation = rotation1 });

                    var triangle2 = new Part
                    {
                        Title = baseTitle,
                        Count = 1,
                        Metal = placement.Part.Metal,
                        Destiny = placement.Part.Destiny,
                        Width = placement.Part.Width,
                        Height = placement.Part.Height,
                        PartType = PartType.Triangle,

                        DisplayGeometry = placement.Part.DisplayGeometry?.Clone(),

                        PropsDict = triangle1.PropsDict != null ?
                            new Dictionary<int, List<string>>(triangle1.PropsDict) :
                            new Dictionary<int, List<string>>(),
                        HoleGroups = triangle1.HoleGroups != null ?
                            new System.Collections.ObjectModel.ObservableCollection<HoleGroup>(triangle1.HoleGroups) :
                            new System.Collections.ObjectModel.ObservableCollection<HoleGroup>()
                    };

                    newPlacements.Add(new PartPlacement { Part = triangle2, X = placement.X, Y = placement.Y, Rotation = rotation2 });
                    processedIndices.Add(i);
                }
                else
                {
                    newPlacements.Add(placement);
                }
            }

            sheet.Parts = newPlacements;
        }

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
                return TryPlaceWithSkyline(sheet, currentPart) ? sheet : null;
            }

            double totalRemainingArea = remainingParts.Sum(p => p.Width * p.Height);
            double requiredAreaWithWaste = totalRemainingArea * 1.15;
            var sortedStocks = allowedStocks.OrderBy(s => s.Area).ToList();

            SheetStock? candidateStock = sortedStocks.FirstOrDefault(s => s.Area >= requiredAreaWithWaste) ?? sortedStocks.Last();

            var testSheet = new NestingSheet { StockWidth = candidateStock.Width, StockHeight = candidateStock.Height, Spacing = spacing };
            if (TryPlaceWithSkyline(testSheet, currentPart))
                return testSheet;

            foreach (var stock in sortedStocks.Where(s => s.Area >= candidateStock.Area))
            {
                testSheet = new NestingSheet { StockWidth = stock.Width, StockHeight = stock.Height, Spacing = spacing };
                if (TryPlaceWithSkyline(testSheet, currentPart))
                    return testSheet;
            }

            return null;
        }

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

        private static bool TryPlaceWithSkylineRotation(NestingSheet sheet, Part part, double rotation)
        {
            var placementResult = EstimateSkylinePlacement(sheet, part, rotation);
            if (!placementResult.CanPlace) return false;

            sheet.Parts.Add(new PartPlacement
            {
                Part = part,
                X = placementResult.X,
                Y = placementResult.Y,
                Rotation = rotation
            });

            return true;
        }

        private static PlacementResult EstimateSkylinePlacement(NestingSheet sheet, Part part, double rotation)
        {
            var (partWidth, partHeight) = NestingHelper.GetPartDimensions(part, rotation);
            var skyline = BuildSkyline(sheet);
            var candidates = FindPlacementCandidates(skyline, partWidth, partHeight, sheet.Width, sheet.Height, sheet);

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

        private static List<SkylineSegment> BuildSkyline(NestingSheet sheet)
        {
            if (!sheet.Parts.Any())
            {
                return new List<SkylineSegment> { new SkylineSegment { XStart = 0, XEnd = sheet.Width, YHeight = 0 } };
            }

            var events = new List<(double X, double Y, bool IsEnd)>();
            foreach (var p in sheet.Parts)
            {
                var (w, h) = NestingHelper.GetPartDimensions(p.Part, p.Rotation);
                events.Add((p.X, p.Y + h, false));
                events.Add((p.X + w, p.Y + h, true));
            }

            events = events.OrderBy(e => e.X).ThenBy(e => e.IsEnd ? 1 : 0).ToList();

            var skyline = new List<SkylineSegment>();
            var activeHeights = new SortedList<double, int>();
            activeHeights[0] = 1;
            double lastX = 0;

            foreach (var evt in events)
            {
                if (evt.X > lastX + Epsilon)
                {
                    skyline.Add(new SkylineSegment { XStart = lastX, XEnd = evt.X, YHeight = activeHeights.Last().Key });
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
                skyline.Add(new SkylineSegment { XStart = lastX, XEnd = sheet.Width, YHeight = activeHeights.Any() ? activeHeights.Last().Key : 0 });
            }

            return skyline;
        }

        private static List<PlacementCandidate> FindPlacementCandidates(
            List<SkylineSegment> skyline, double partWidth, double partHeight, double sheetWidth, double sheetHeight, NestingSheet sheet)
        {
            var candidates = new List<PlacementCandidate>();
            double sp = sheet.Spacing;
            double effectiveWidth = sheetWidth - sp;
            double effectiveHeight = sheetHeight - sp;

            bool isWidePart = partWidth > (effectiveWidth) * 0.90;

            if (isWidePart && sheet.Parts.Any())
            {
                foreach (var existing in sheet.Parts)
                {
                    var (ew, eh) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);
                    if (Math.Abs(ew - partWidth) <= sp)
                    {
                        double stackX = existing.X;
                        double stackY = existing.Y + eh + sp;

                        if (stackY + partHeight > effectiveHeight) continue;
                        if (stackX + partWidth > effectiveWidth) continue;
                        if (IsOverlappingAny(sheet, stackX, stackY, partWidth, partHeight)) continue;

                        double finalX = FindLeftmostPosition(sheet, stackX, stackY, partWidth, partHeight, sheetWidth, sp);
                        candidates.Add(new PlacementCandidate { X = finalX, Y = stackY, FitScore = stackY * 10000 + finalX, SegmentIndex = -1 });
                    }
                }
            }

            for (int i = 0; i < skyline.Count; i++)
            {
                var segment = skyline[i];
                double baseX = segment.XStart + sp;
                double candidateY = segment.YHeight + sp;

                if (candidateY + partHeight > effectiveHeight) continue;

                double requiredRightEdge = segment.XStart + partWidth;
                bool canFitWidth = false;

                for (int j = i; j < skyline.Count; j++)
                {
                    var s = skyline[j];
                    if (s.YHeight > segment.YHeight + Epsilon) break;
                    if (s.XEnd >= requiredRightEdge - Epsilon)
                    {
                        canFitWidth = true;
                        break;
                    }
                }

                if (!canFitWidth || baseX + partWidth > effectiveWidth) continue;

                double finalX = FindLeftmostPosition(sheet, baseX, candidateY, partWidth, partHeight, sheetWidth, sp);
                if (!IsOverlappingAny(sheet, finalX, candidateY, partWidth, partHeight))
                {
                    candidates.Add(new PlacementCandidate { X = finalX, Y = candidateY, FitScore = finalX * 10000 + candidateY, SegmentIndex = i });
                }
            }

            return candidates.OrderBy(c => c.FitScore).ThenBy(c => c.SegmentIndex == -1 ? 0 : 1).ThenBy(c => c.X).ThenBy(c => c.Y).ToList();
        }

        private static double FindLeftmostPosition(NestingSheet sheet, double candidateX, double candidateY, double partWidth, double partHeight, double sheetWidth, double spacing)
        {
            double bestX = candidateX;
            for (double testX = spacing; testX <= candidateX + Epsilon; testX += 1)
            {
                if (testX + partWidth > sheetWidth - spacing) break;
                if (!IsOverlappingAny(sheet, testX, candidateY, partWidth, partHeight))
                {
                    bestX = testX;
                    break;
                }
            }
            return bestX;
        }

        private static (double OptWidth, double OptHeight, double EstimatedArea) EstimateSheetImpact(NestingSheet sheet, double partX, double partY, double partWidth, double partHeight)
        {
            double sp = sheet.Spacing;
            double currentMaxX = 0;
            double currentMaxY = 0;

            if (sheet.Parts.Any())
            {
                currentMaxX = sheet.Parts.Max(p => { var d = NestingHelper.GetPartDimensions(p.Part, p.Rotation); return p.X + d.Width; });
                currentMaxY = sheet.Parts.Max(p => { var d = NestingHelper.GetPartDimensions(p.Part, p.Rotation); return p.Y + d.Height; });
            }

            double reqW = Math.Max(currentMaxX, partX + partWidth) + sp * 2;
            double reqH = Math.Max(currentMaxY, partY + partHeight) + sp * 2;

            if (reqW > sheet.StockWidth || reqH > sheet.StockHeight)
                return (double.MaxValue, double.MaxValue, double.MaxValue);

            if (sheet.StockWidth - reqW > sheet.StockHeight - reqH)
            {
                double finalW = Math.Min(Math.Ceiling(reqW / 100) * 100, sheet.StockWidth);
                return (finalW, sheet.StockHeight, finalW * sheet.StockHeight);
            }
            else
            {
                double finalH = Math.Min(Math.Ceiling(reqH / 100) * 100, sheet.StockHeight);
                return (sheet.StockWidth, finalH, sheet.StockWidth * finalH);
            }
        }

        private static bool IsOverlappingAny(NestingSheet sheet, double x, double y, double width, double height)
        {
            double sp = sheet.Spacing;
            foreach (var existing in sheet.Parts)
            {
                var (ew, eh) = NestingHelper.GetPartDimensions(existing.Part, existing.Rotation);
                double ex = existing.X;
                double ey = existing.Y;

                if (x + width + sp <= ex) continue;
                if (ex + ew + sp <= x) continue;
                if (y + height + sp <= ey) continue;
                if (ey + eh + sp <= y) continue;

                return true;
            }
            return false;
        }

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

            public int CompareTo(PlacementCandidate other) => FitScore.CompareTo(other.FitScore);
        }
    }
}