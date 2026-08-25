using System;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code.Utils
{
    public static class NestingHelper
    {
        // Отступы между деталями и от краёв вынесены в константу
        public const double Spacing = 10;

        /// <summary>
        /// Пакетный нестинг: размещает ВСЕ детали на минимальном количестве листов.
        /// ВАЖНО: Список parts должен быть предварительно отсортирован по убыванию площади/размера
        /// для достижения наилучших результатов.
        /// </summary>
        public static List<NestingSheet> CreateNestingForBatch(List<Part> parts, double sheetWidth = 3000, double sheetHeight = 1500)
        {
            var sheets = new List<NestingSheet>();

            // Размножаем детали согласно количеству
            var allParts = parts
                .SelectMany(p => Enumerable.Repeat(p, p.Count))
                .OrderByDescending(p => Math.Max(p.Width, p.Height)) // Сначала самые "высокие"
                .ThenByDescending(p => p.Width * p.Height)           // Затем по площади
                .ThenBy(p => Math.Abs(p.Width - p.Height))           // Затем более квадратные
                .ToList();

            foreach (var part in allParts)
            {
                bool placed = false;

                // Пробуем разместить на существующих листах
                foreach (var sheet in sheets)
                {
                    if (TryPlacePartByColumn(sheet, part))
                    {
                        placed = true;
                        break;
                    }
                }

                // Если не влезло ни на один лист, создаем новый
                if (!placed)
                {
                    var newSheet = new NestingSheet
                    {
                        StockWidth = sheetWidth,
                        StockHeight = sheetHeight,
                        Spacing = Spacing
                    };

                    // На новом листе просто кладем деталь (ориентация уже выбрана или дефолтная)
                    // Для первой детали на листе TryPlacePartByColumn сработает как размещение в новый столбец
                    TryPlacePartByColumn(newSheet, part);
                    sheets.Add(newSheet);
                }
            }

            // Финальная оптимизация габаритов всех листов
            foreach (var sheet in sheets)
            {
                OptimizeSheetSize(sheet);
            }

            return sheets;
        }

        /// <summary>
        /// Рассчитывает оптимальные размеры обрезки листа.
        /// Учитывает минимальный допустимый остаток (MinRemnant = 300 мм).
        /// Если обрезка оставляет полосу уже 300 мм, лист не обрезается по этой стороне.
        /// </summary>
        public static void OptimizeSheetSize(NestingSheet sheet)
        {
            if (!sheet.Parts.Any())
            {
                sheet.OptimizedWidth = 0;
                sheet.OptimizedHeight = 0;
                return;
            }

            // Минимальная ширина полезной полосы отрезанного остатка (мм)
            const double MinRemnant = 300;

            double maxX = sheet.Parts.Max(p =>
            {
                var dims = GetPartDimensions(p.Part, p.Rotation);
                return p.X + dims.Width;
            });

            double maxY = sheet.Parts.Max(p =>
            {
                var dims = GetPartDimensions(p.Part, p.Rotation);
                return p.Y + dims.Height;
            });

            // Рассчитываем потенциальные остатки при обрезке
            double potentialWasteWidth = sheet.StockWidth - maxX;
            double potentialWasteHeight = sheet.StockHeight - maxY;

            // Определяем, по какой оси выгоднее обрезать (где больше мусора)
            bool cutByWidth = potentialWasteWidth > potentialWasteHeight;

            if (cutByWidth)
            {
                // --- Попытка обрезки по ШИРИНЕ ---

                // Округляем требуемую ширину вверх до кратных 100 мм
                double optimizedW = Math.Ceiling(maxX / 100) * 100;
                optimizedW = Math.Min(optimizedW, sheet.StockWidth);

                // Проверяем размер остатка
                double remnant = sheet.StockWidth - optimizedW;

                // Если остаток меньше минимального (но больше 0), то обрезка бессмысленна — оставляем полный лист
                if (remnant > 0 && remnant < MinRemnant)
                {
                    sheet.OptimizedWidth = sheet.StockWidth; // Не обрезаем
                }
                else
                {
                    sheet.OptimizedWidth = optimizedW;
                }

                sheet.OptimizedHeight = sheet.StockHeight;
            }
            else
            {
                // --- Попытка обрезки по ВЫСОТЕ ---

                double optimizedH = Math.Ceiling(maxY / 100) * 100;
                optimizedH = Math.Min(optimizedH, sheet.StockHeight);

                // Проверяем размер остатка
                double remnant = sheet.StockHeight - optimizedH;

                // Если остаток меньше минимального, оставляем полную высоту
                if (remnant > 0 && remnant < MinRemnant)
                {
                    sheet.OptimizedHeight = sheet.StockHeight; // Не обрезаем
                }
                else
                {
                    sheet.OptimizedHeight = optimizedH;
                }

                sheet.OptimizedWidth = sheet.StockWidth;
            }
        }

        /// <summary>
        /// Главная логика размещения одной детали.
        /// Выбирает лучшую ориентацию на основе оценки площади листа после размещения.
        /// </summary>
        private static bool TryPlacePartByColumn(NestingSheet sheet, Part part)
        {
            // Для кругов поворот не имеет смысла
            if (part.PartType == PartType.Round)
                return TryPlaceWithRotation(sheet, part, 0);

            // Оцениваем "стоимость" (площадь оптимизированного листа) для обеих ориентаций
            double cost0 = EstimateAreaCost(sheet, part, 0);
            double cost90 = EstimateAreaCost(sheet, part, 90);

            // Если ни одна ориентация не позволяет разместить деталь
            if (cost0 == double.MaxValue && cost90 == double.MaxValue)
                return false;

            // Выбираем ориентацию с МЕНЬШЕЙ стоимостью (меньшей площадью листа)
            // При равенстве предпочитаем исходную ориентацию (0 градусов)
            return cost0 <= cost90
                ? TryPlaceWithRotation(sheet, part, 0)
                : TryPlaceWithRotation(sheet, part, 90);
        }

        /// <summary>
        /// Оценивает эффективность ориентации по площади оптимизированного листа.
        /// Возвращает площадь (Width * Height). Чем меньше, тем лучше.
        /// Если разместить нельзя, возвращает double.MaxValue.
        /// </summary>
        private static double EstimateAreaCost(NestingSheet sheet, Part part, double rotation)
        {
            // Клонируем текущее состояние листа для тестирования
            var testSheet = new NestingSheet
            {
                StockWidth = sheet.StockWidth,
                StockHeight = sheet.StockHeight,
                Spacing = sheet.Spacing,
                Parts = sheet.Parts.Select(p => new PartPlacement
                {
                    Part = p.Part,
                    X = p.X,
                    Y = p.Y,
                    Rotation = p.Rotation
                }).ToList()
            };

            // Пробуем разместить ОДНУ деталь в тестируемой ориентации
            if (!TryPlaceWithRotation(testSheet, part, rotation))
                return double.MaxValue; // Не помещается

            // Считаем, какими стали бы оптимизированные размеры листа
            var dims = GetOptimizedDimensions(testSheet);

            // Возвращаем площадь как метрику "стоимости"
            return dims.Width * dims.Height;
        }

        /// <summary>
        /// Возвращает оптимизированные размеры листа (Width, Height) без изменения самого объекта.
        /// Используется для оценки стоимости размещения.
        /// </summary>
        private static (double Width, double Height) GetOptimizedDimensions(NestingSheet sheet)
        {
            if (sheet.Parts.Count == 0)
                return (0, 0);

            double maxX = sheet.Parts.Max(p =>
            {
                var dims = GetPartDimensions(p.Part, p.Rotation);
                return p.X + dims.Width;
            });

            double maxY = sheet.Parts.Max(p =>
            {
                var dims = GetPartDimensions(p.Part, p.Rotation);
                return p.Y + dims.Height;
            });

            double requiredWidth = maxX + sheet.Spacing * 2;
            double requiredHeight = maxY + sheet.Spacing * 2;

            double wasteWidth = sheet.StockWidth - requiredWidth;
            double wasteHeight = sheet.StockHeight - requiredHeight;

            if (wasteWidth > wasteHeight)
            {
                double optimizedW = Math.Ceiling(requiredWidth / 100) * 100;
                return (Math.Min(optimizedW, sheet.StockWidth), sheet.StockHeight);
            }
            else
            {
                double optimizedH = Math.Ceiling(requiredHeight / 100) * 100;
                return (sheet.StockWidth, Math.Min(optimizedH, sheet.StockHeight));
            }
        }

        /// <summary>
        /// Физически размещает деталь на листе в заданной ориентации.
        /// Стратегия: Строгие вертикальные столбцы слева направо.
        /// </summary>
        private static bool TryPlaceWithRotation(NestingSheet sheet, Part part, double rotation)
        {
            var (partWidth, partHeight) = GetPartDimensions(part, rotation);
            double sp = sheet.Spacing; // 🔥 Локальная переменная для краткости

            var columns = sheet.Parts.GroupBy(p => Math.Round(p.X, 0))
                .Select(g => new {
                    X = g.Key,
                    MaxY = g.Max(p => { var dims = GetPartDimensions(p.Part, p.Rotation); return p.Y + dims.Height; })
                }).OrderBy(c => c.X).ToList();

            foreach (var column in columns)
            {
                double candidateX = column.X;
                double candidateY = column.MaxY + sp;

                if (candidateY + partHeight > sheet.Height - sp) continue;
                if (candidateX + partWidth > sheet.Width - sp) continue;
                if (IsOverlapping(sheet, candidateX, candidateY, partWidth, partHeight)) continue;

                sheet.Parts.Add(new PartPlacement { Part = part, X = candidateX, Y = candidateY, Rotation = rotation });
                return true;
            }

            foreach (var existing in sheet.Parts.OrderByDescending(p => p.Y))
            {
                var (ew, eh) = GetPartDimensions(existing.Part, existing.Rotation);
                double candidateX = existing.X + ew + sp;
                double candidateY = existing.Y;

                if (CanPlaceAt(sheet, candidateX, candidateY, partWidth, partHeight))
                {
                    if (IsOverlapping(sheet, candidateX, candidateY, partWidth, partHeight)) continue;
                    sheet.Parts.Add(new PartPlacement { Part = part, X = candidateX, Y = candidateY, Rotation = rotation });
                    return true;
                }

                candidateX = Math.Max(sp, existing.X - partWidth - sp);
                candidateY = existing.Y + eh + sp;

                if (CanPlaceAt(sheet, candidateX, candidateY, partWidth, partHeight))
                {
                    if (IsOverlapping(sheet, candidateX, candidateY, partWidth, partHeight)) continue;
                    sheet.Parts.Add(new PartPlacement { Part = part, X = candidateX, Y = candidateY, Rotation = rotation });
                    return true;
                }
            }

            double newX = sp;
            if (sheet.Parts.Count > 0)
            {
                double rightmostRight = sheet.Parts.Max(p => { var dims = GetPartDimensions(p.Part, p.Rotation); return p.X + dims.Width; });
                newX = rightmostRight + sp;
            }

            if (newX + partWidth > sheet.Width - sp) return false;
            double newY = sp;

            if (IsOverlapping(sheet, newX, newY, partWidth, partHeight)) return false;

            sheet.Parts.Add(new PartPlacement { Part = part, X = newX, Y = newY, Rotation = rotation });
            return true;
        }

        /// <summary>
        /// Проверяет пересечение прямоугольных границ деталей с учетом отступов.
        /// </summary>
        private static bool IsOverlapping(NestingSheet sheet, double x, double y, double width, double height)
        {
            foreach (var existing in sheet.Parts)
            {
                var (ew, eh) = GetPartDimensions(existing.Part, existing.Rotation);
                double ex = existing.X;
                double ey = existing.Y;

                // Алгоритм проверки непересечения AABB (Axis-Aligned Bounding Box)
                if (x + width + sheet.Spacing <= ex) continue;  // Новая деталь левее существующей
                if (ex + ew + sheet.Spacing <= x) continue;     // Существующая левее новой
                if (y + height + sheet.Spacing <= ey) continue; // Новая деталь ниже существующей
                if (ey + eh + sheet.Spacing <= y) continue;     // Существующая ниже новой

                return true; // Пересечение есть
            }
            return false;
        }

        /// <summary>
        /// Проверяет, можно ли разместить деталь в указанных координатах 
        /// без выхода за границы и пересечений с другими деталями.
        /// </summary>
        public static bool IsValidPlacement(NestingSheet sheet, PartPlacement movingPart, double x, double y, double rotation)
        {
            var (w, h) = GetPartDimensions(movingPart.Part, rotation);

            // 1. Проверка выхода за границы листа
            if (x < sheet.Spacing || y < sheet.Spacing) return false;
            if (x + w > sheet.Width - sheet.Spacing) return false;
            if (y + h > sheet.Height - sheet.Spacing) return false;

            // 2. Проверка пересечений с ДРУГИМИ деталями
            foreach (var existing in sheet.Parts)
            {
                // Исключаем саму перемещаемую деталь
                if (ReferenceEquals(existing, movingPart)) continue;

                var (ew, eh) = GetPartDimensions(existing.Part, existing.Rotation);
                double ex = existing.X;
                double ey = existing.Y;

                // AABB — логика из IsOverlapping
                if (x + w + sheet.Spacing <= ex) continue;
                if (ex + ew + sheet.Spacing <= x) continue;
                if (y + h + sheet.Spacing <= ey) continue;
                if (ey + eh + sheet.Spacing <= y) continue;

                return false; // Пересечение найдено
            }

            return true;
        }

        /// <summary>
        /// Возвращает ширину и высоту детали с учётом поворота.
        /// </summary>
        public static (double Width, double Height) GetPartDimensions(Part part, double rotation)
        {
            if (part.PartType == PartType.Round)
                return (part.Width, part.Width);

            // 🔥 Проверяем и 90°, и 270°
            if (Math.Abs(rotation - 90) < 0.1 || Math.Abs(rotation - 270) < 0.1)
                return (part.Height, part.Width);
            else
                return (part.Width, part.Height);
        }

        // Вспомогательный метод для чистой проверки (без добавления)
        private static bool CanPlaceAt(NestingSheet sheet, double x, double y, double width, double height)
        {
            if (x < sheet.Spacing || y < sheet.Spacing) return false;
            if (x + width > sheet.Width - sheet.Spacing) return false;
            if (y + height > sheet.Height - sheet.Spacing) return false;
            return !IsOverlapping(sheet, x, y, width, height);
        }

        public static bool AreSheetsEqual(NestingSheet? sheet1, NestingSheet? sheet2)
        {
            if (sheet1 == null || sheet2 == null) return false;
            if (Math.Abs(sheet1.Width - sheet2.Width) > 0.1) return false;
            if (Math.Abs(sheet1.Height - sheet2.Height) > 0.1) return false;
            if (sheet1.Parts.Count != sheet2.Parts.Count) return false;

            var counts1 = sheet1.Parts.GroupBy(p => p.Part.Title ?? string.Empty).ToDictionary(g => g.Key, g => g.Count());
            var counts2 = sheet2.Parts.GroupBy(p => p.Part.Title ?? string.Empty).ToDictionary(g => g.Key, g => g.Count());

            if (counts1.Count != counts2.Count) return false;
            foreach (var kvp in counts1)
                if (!counts2.TryGetValue(kvp.Key, out int count2) || kvp.Value != count2) return false;

            return true;
        }

        public static int TryFillSheetWithPart(NestingSheet sheet, Part part)
        {
            int placedCount = 0;
            while (true)
            {
                // Используем основную логику размещения с выбором ориентации
                if (TryPlacePartByColumn(sheet, part))
                {
                    placedCount++;
                }
                else
                {
                    break;
                }
            }
            if (placedCount > 0) OptimizeSheetSize(sheet);
            return placedCount;
        }

        public static List<PipeStock> CreateNestingForPipeBatch(List<Part> parts, double stockLength = 6000, double clampZone = 340, double cutLoss = 10)
        {
            var stocks = new List<PipeStock>();
            var allParts = parts.SelectMany(p => Enumerable.Repeat(p, p.Count)).OrderByDescending(p => p.Length).ToList();

            foreach (var part in allParts)
            {
                bool placed = false;

                foreach (var stock in stocks.OrderByDescending(s => s.TotalRequiredLength))
                {
                    if (stock.StockLength >= stock.TotalRequiredLength + part.Length + stock.CutLoss)
                    {
                        double startPosition = stock.ClampZone + stock.UsedLengthWithCutLoss;
                        stock.Placements.Add(new PipePlacement { Part = part, StartPosition = startPosition });
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    var newStock = new PipeStock
                    {
                        StockLength = stockLength,
                        ClampZone = clampZone,
                        CutLoss = cutLoss
                    };

                    if (part.Length <= stockLength - clampZone)
                    {
                        newStock.Placements.Add(new PipePlacement { Part = part, StartPosition = clampZone });
                        stocks.Add(newStock);
                    }
                }
            }

            foreach (var stock in stocks)
            {
                OptimizeStockLength(stock);
            }

            return stocks;
        }

        private static void OptimizeStockLength(PipeStock stock)
        {
            if (stock.Placements.Count == 0) { stock.OptimizedLength = 0; return; }
            double requiredLength = stock.TotalRequiredLength;
            double optimizedLen = Math.Ceiling(requiredLength / 500) * 500;
            stock.OptimizedLength = Math.Min(optimizedLen, stock.StockLength);
        }

        public static bool AreStocksEqual(PipeStock? stock1, PipeStock? stock2)
        {
            if (stock1 == null || stock2 == null) return false;
            if (Math.Abs(stock1.StockLength - stock2.StockLength) > 0.1) return false;
            if (Math.Abs(stock1.ClampZone - stock2.ClampZone) > 0.1) return false;
            if (stock1.Placements.Count != stock2.Placements.Count) return false;

            var parts1 = stock1.Placements.GroupBy(p => new { p.Part.Width, p.Part.Height, p.Part.Length, p.Part.PartType })
                .Select(g => new { g.Key, Count = g.Count() }).OrderBy(x => x.Key.Width).ThenBy(x => x.Key.Height).ThenBy(x => x.Key.Length).ToList();
            var parts2 = stock2.Placements.GroupBy(p => new { p.Part.Width, p.Part.Height, p.Part.Length, p.Part.PartType })
                .Select(g => new { g.Key, Count = g.Count() }).OrderBy(x => x.Key.Width).ThenBy(x => x.Key.Height).ThenBy(x => x.Key.Length).ToList();

            if (parts1.Count != parts2.Count) return false;
            for (int i = 0; i < parts1.Count; i++)
            {
                if (Math.Abs(parts1[i].Key.Width - parts2[i].Key.Width) > 0.1 || Math.Abs(parts1[i].Key.Height - parts2[i].Key.Height) > 0.1 ||
                    Math.Abs(parts1[i].Key.Length - parts2[i].Key.Length) > 0.1 || parts1[i].Key.PartType != parts2[i].Key.PartType || parts1[i].Count != parts2[i].Count)
                    return false;
            }
            return true;
        }
    }

    public class NestingSheet
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Исходный размер заготовки (всегда полный, например 3000х1500)
        public double StockWidth { get; set; }
        public double StockHeight { get; set; }

        // Оптимизированный размер (фактически занятая область с отступами, округленная)
        public double OptimizedWidth { get; set; }
        public double OptimizedHeight { get; set; }

        // Для обратной совместимости и общей логики Width/Height могут равняться Stock, 
        // либо Optimized, в зависимости от того, что нужно для других расчетов.
        // Пусть Width/Height остаются полными (Stock), а для обрезки используем Optimized.
        public double Width => StockWidth;
        public double Height => StockHeight;

        // Отступ для этого конкретного листа
        public double Spacing { get; set; } = 10;

        /// <summary>
        /// Свободная ширина листа (разница между полной и оптимизированной)
        /// </summary>
        public double FreeWidth => Math.Max(0, StockWidth - OptimizedWidth);

        /// <summary>
        /// Свободная высота листа (разница между полной и оптимизированной)
        /// </summary>
        public double FreeHeight => Math.Max(0, StockHeight - OptimizedHeight);

        public List<PartPlacement> Parts { get; set; } = new();
    }

    public class PartPlacement
    {
        public Part Part { get; set; } = null!;
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotation { get; set; } = 0;
    }

    public class NestingSheetComparer : IEqualityComparer<NestingSheet>
    {
        public bool Equals(NestingSheet? x, NestingSheet? y)
        {
            return NestingHelper.AreSheetsEqual(x, y);
        }

        public int GetHashCode(NestingSheet obj)
        {
            if (obj == null) return 0;
            return obj.Width.GetHashCode() ^ obj.Height.GetHashCode() ^ obj.Parts.Count.GetHashCode();
        }
    }

    public class PipeStock
    {
        // Исходная длина стандартного хлыста (заготовки), выбранная пользователем
        public double StockLength { get; set; }

        // Зона зажима (технологический отступ)
        public double ClampZone { get; set; }

        // Список размещенных деталей
        public List<PipePlacement> Placements { get; set; } = new();

        // --- Новые свойства для оптимизации ---

        // Рассчитанная минимально необходимая длина хлыста с учетом всех деталей, отступов и зоны зажима.
        // Округлена вверх до удобного значения (например, до 100 мм или до целого метра).
        public double OptimizedLength { get; set; }

        public double CutLoss { get; set; } = 10; // Отступ между деталями (пропил)

        /// <summary>
        /// Занятая длина с учётом отступов между деталями (сумма длин + пропилы)
        /// </summary>
        public double UsedLengthWithCutLoss =>
            Placements.Count == 0
                ? 0
                : UsedLength + (Placements.Count - 1) * CutLoss;

        /// <summary>
        /// Сумма длин всех деталей
        /// </summary>
        public double UsedLength => Placements.Sum(p => p.Part.Length);

        /// <summary>
        /// Остаток хлыста
        /// </summary>
        public double AvailableLength => StockLength - ClampZone - UsedLengthWithCutLoss;

        /// <summary>
        /// Полная требуемая длина хлыста (Зона зажима + Детали + Пропилы)
        /// Используется для расчета оптимизированной длины.
        /// </summary>
        public double TotalRequiredLength => ClampZone + UsedLengthWithCutLoss;
    }

    public class PipePlacement
    {
        public Part Part { get; set; } = null!;
        public double StartPosition { get; set; }
    }

    public class PipeStockComparer : IEqualityComparer<PipeStock>
    {
        public bool Equals(PipeStock? x, PipeStock? y)
        {
            return NestingHelper.AreStocksEqual(x, y);
        }

        public int GetHashCode(PipeStock obj)
        {
            if (obj == null) return 0;
            int hash = obj.StockLength.GetHashCode() ^ obj.Placements.Count.GetHashCode();
            foreach (var placement in obj.Placements.Take(3))
            {
                hash ^= placement.Part.PartType.GetHashCode();
                hash ^= placement.Part.Length.GetHashCode();
            }
            return hash;
        }
    }
}