using System;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code.Utils
{
    public static class NestingHelper
    {
        // Отступы между деталями и от краёв вынесены в константу, так как они обычно фиксированы технологически
        private const double Spacing = 10;

        /// <summary>
        /// Пакетный нестинг: размещает ВСЕ детали на минимальном количестве листов
        /// Размещение: снизу вверх в столбце, затем слева направо по столбцам
        /// </summary>
        /// <param name="parts">Список деталей</param>
        /// <param name="sheetWidth">Ширина доступного листа металла (мм). По умолчанию 3000.</param>
        /// <param name="sheetHeight">Высота доступного листа металла (мм). По умолчанию 1500.</param>
        public static List<NestingSheet> CreateNestingForBatch(List<Part> parts, double sheetWidth = 3000, double sheetHeight = 1500)
        {
            var sheets = new List<NestingSheet>();
            var allParts = parts.SelectMany(p => Enumerable.Repeat(p, p.Count)).ToList();

            foreach (var part in allParts)
            {
                bool placed = false;
                foreach (var sheet in sheets)
                {
                    if (TryPlacePartByColumn(sheet, part))
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
                        StockHeight = sheetHeight
                    };
                    TryPlacePartByColumn(newSheet, part);
                    sheets.Add(newSheet);
                }
            }

            // Оптимизируем размеры листов
            foreach (var sheet in sheets)
            {
                OptimizeSheetSize(sheet);
            }

            return sheets;
        }

        /// <summary>
        /// Рассчитывает оптимальные размеры обрезки, но НЕ меняет габариты заготовки.
        /// Результат записывается в OptimizedWidth/OptimizedHeight.
        /// </summary>
        private static void OptimizeSheetSize(NestingSheet sheet)
        {
            if (sheet.Parts.Count == 0)
            {
                sheet.OptimizedWidth = 0;
                sheet.OptimizedHeight = 0;
                return;
            }

            double maxX = sheet.Parts.Max(p =>
            {
                double w = p.Part.PartType == PartType.Round ? p.Part.Width : p.Part.Width;
                return p.X + w;
            });

            double maxY = sheet.Parts.Max(p =>
            {
                double h = p.Part.PartType == PartType.Round ? p.Part.Width : p.Part.Height;
                return p.Y + h;
            });

            double requiredWidth = maxX + Spacing * 2;
            double requiredHeight = maxY + Spacing * 2;

            double wasteWidth = sheet.StockWidth - requiredWidth;
            double wasteHeight = sheet.StockHeight - requiredHeight;

            // Логика выбора стороны обрезки (где отход больше)
            if (wasteWidth > wasteHeight)
            {
                // Обрезаем ширину
                double optimizedW = Math.Ceiling(requiredWidth / 100) * 100;
                sheet.OptimizedWidth = Math.Min(optimizedW, sheet.StockWidth);
                sheet.OptimizedHeight = sheet.StockHeight; // Высота полная
            }
            else
            {
                // Обрезаем высоту
                double optimizedH = Math.Ceiling(requiredHeight / 100) * 100;
                sheet.OptimizedHeight = Math.Min(optimizedH, sheet.StockHeight);
                sheet.OptimizedWidth = sheet.StockWidth; // Ширина полная
            }
        }

        /// <summary>
        /// Размещает деталь по столбцам: снизу вверх, затем слева направо
        /// </summary>
        private static bool TryPlacePartByColumn(NestingSheet sheet, Part part)
        {
            double partWidth = part.PartType == PartType.Round ? part.Width : part.Width;
            double partHeight = part.PartType == PartType.Round ? part.Width : part.Height;

            // Находим все существующие столбцы (группируем по X с точностью до 1мм)
            var columns = sheet.Parts
                .GroupBy(p => Math.Round(p.X, 0))
                .Select(g => new
                {
                    X = g.Key,
                    MaxY = g.Max(p => p.Y + (p.Part.PartType == PartType.Round ? p.Part.Width : p.Part.Height))
                })
                .OrderBy(c => c.X)
                .ToList();

            // Пробуем разместить в существующих столбцах
            foreach (var column in columns)
            {
                double candidateX = column.X;
                double candidateY = column.MaxY + Spacing;

                // Проверяем границы листа (используем актуальные размеры sheet.Width/Height)
                if (candidateY + partHeight > sheet.Height - Spacing)
                    continue;

                if (candidateX + partWidth > sheet.Width - Spacing)
                    continue;

                if (IsOverlapping(sheet, candidateX, candidateY, partWidth, partHeight))
                    continue;

                sheet.Parts.Add(new PartPlacement { Part = part, X = candidateX, Y = candidateY });
                return true;
            }

            // Создаём новый столбец справа
            double newX = Spacing;

            if (sheet.Parts.Count > 0)
            {
                double rightmostRight = sheet.Parts.Max(p =>
                {
                    double w = p.Part.PartType == PartType.Round ? p.Part.Width : p.Part.Width;
                    return p.X + w;
                });
                newX = rightmostRight + Spacing;
            }

            // Проверяем, помещается ли новый столбец в ширину листа
            if (newX + partWidth > sheet.Width - Spacing)
                return false;

            // Размещаем в начале нового столбца
            double newY = Spacing;

            if (IsOverlapping(sheet, newX, newY, partWidth, partHeight))
                return false;

            sheet.Parts.Add(new PartPlacement { Part = part, X = newX, Y = newY });
            return true;
        }

        /// <summary>
        /// Проверяет пересечение с учётом отступов
        /// </summary>
        private static bool IsOverlapping(NestingSheet sheet, double x, double y, double width, double height)
        {
            foreach (var existing in sheet.Parts)
            {
                double ex = existing.X;
                double ey = existing.Y;
                double ew = existing.Part.PartType == PartType.Round
                    ? existing.Part.Width
                    : existing.Part.Width;
                double eh = existing.Part.PartType == PartType.Round
                    ? existing.Part.Width
                    : existing.Part.Height;

                if (x + width + Spacing <= ex) continue;
                if (ex + ew + Spacing <= x) continue;
                if (y + height + Spacing <= ey) continue;
                if (ey + eh + Spacing <= y) continue;

                return true;
            }
            return false;
        }

        /// <summary>
        /// Проверяет идентичность листов по размерам и набору наименований деталей (Title).
        /// Учитывает возможность null значения в Title.
        /// </summary>
        public static bool AreSheetsEqual(NestingSheet? sheet1, NestingSheet? sheet2)
        {
            if (sheet1 == null || sheet2 == null)
                return false;

            // 1. Сравниваем габариты листа
            if (Math.Abs(sheet1.Width - sheet2.Width) > 0.1)
                return false;
            if (Math.Abs(sheet1.Height - sheet2.Height) > 0.1)
                return false;

            // 2. Сравниваем количество деталей
            if (sheet1.Parts.Count != sheet2.Parts.Count)
                return false;

            // 3. Сравниваем состав по наименованиям (Title)
            // Используем оператор null-coalescing (??), чтобы заменить null на пустую строку или специальный маркер.
            // Это гарантирует, что группировка пройдет без ошибок.
            var counts1 = sheet1.Parts
                .GroupBy(p => p.Part.Title ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Count());

            var counts2 = sheet2.Parts
                .GroupBy(p => p.Part.Title ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Count());

            // Если количество уникальных типов деталей разное
            if (counts1.Count != counts2.Count)
                return false;

            // Проверяем совпадение количества для каждого наименования
            foreach (var kvp in counts1)
            {
                // Если ключ отсутствует во втором словаре или количество не совпадает
                if (!counts2.TryGetValue(kvp.Key, out int count2) || kvp.Value != count2)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Пытается разместить максимально возможное количество копий детали на существующем листе.
        /// Возвращает количество успешно размещённых деталей.
        /// </summary>
        public static int TryFillSheetWithPart(NestingSheet sheet, Part part)
        {
            int placedCount = 0;

            // Пытаемся размещать, пока есть место и деталь помещается
            while (true)
            {
                // Быстрая проверка: влезает ли деталь в свободную область
                double partWidth = part.PartType == PartType.Round ? part.Width : part.Width;
                double partHeight = part.PartType == PartType.Round ? part.Width : part.Height;

                if (partWidth > sheet.FreeWidth + 1 && partHeight > sheet.FreeHeight + 1)
                    break; // Деталь явно не поместится ни в одну из свободных зон

                // Пробуем разместить через существующий алгоритм
                if (TryPlacePartByColumn(sheet, part))
                {
                    placedCount++;
                }
                else
                {
                    break; // Алгоритм не смог разместить — выходим
                }
            }

            // Если разместили хотя бы одну — пересчитываем оптимизированные размеры
            if (placedCount > 0)
            {
                OptimizeSheetSize(sheet);
            }

            return placedCount;
        }

        /// <summary>
        /// Пакетный нестинг труб.
        /// </summary>
        /// <param name="parts">Список деталей</param>
        /// <param name="stockLength">Длина стандартного хлыста (заготовки) в мм. По умолчанию 6000.</param>
        /// <param name="clampZone">Зона зажима станка в мм. По умолчанию 340.</param>
        public static List<PipeStock> CreateNestingForPipeBatch(List<Part> parts, double stockLength = 6000, double clampZone = 340)
        {
            var stocks = new List<PipeStock>();

            // Размножаем и сортируем по убыванию длины (First Fit Decreasing)
            var allParts = parts
                .SelectMany(p => Enumerable.Repeat(p, p.Count))
                .OrderByDescending(p => p.Length)
                .ToList();

            foreach (var part in allParts)
            {
                bool placed = false;

                // Пробуем разместить на существующих хлыстах
                // Сортируем по использованной длине, чтобы заполнять более занятые хлысты (или менее, в зависимости от стратегии)
                // Здесь оставляем стратегию "заполнять первый подходящий", но сортировка помогает уплотнению
                foreach (var stock in stocks.OrderBy(s => s.TotalRequiredLength))
                {
                    // Проверяем, влезает ли деталь в остаток текущего хлыста (с учетом зоны зажима и пропилов)
                    if (stock.StockLength >= stock.TotalRequiredLength + part.Length + Spacing)
                    {
                        double startPosition = stock.ClampZone + stock.UsedLengthWithCutLoss;

                        stock.Placements.Add(new PipePlacement
                        {
                            Part = part,
                            StartPosition = startPosition
                        });

                        placed = true;
                        break;
                    }
                }

                // Создаём новый хлыст при необходимости
                if (!placed)
                {
                    var newStock = new PipeStock
                    {
                        StockLength = stockLength,
                        ClampZone = clampZone
                    };

                    // Проверка: не превышает ли одна деталь доступную длину хлыста (минус зажим)
                    if (part.Length > stockLength - clampZone)
                    {
                        // Тут можно выбросить исключение или обработать ошибку, что деталь слишком длинная
                        // Для сейчас просто добавляем, но это будет ошибка в логике раскроя
                    }

                    newStock.Placements.Add(new PipePlacement
                    {
                        Part = part,
                        StartPosition = clampZone
                    });

                    stocks.Add(newStock);
                }
            }

            // Оптимизируем длину каждого хлыста
            foreach (var stock in stocks)
            {
                OptimizeStockLength(stock);
            }

            return stocks;
        }

        /// <summary>
        /// Рассчитывает оптимальную длину отреза хлыста.
        /// Результат записывается в свойство OptimizedLength.
        /// </summary>
        private static void OptimizeStockLength(PipeStock stock)
        {
            if (stock.Placements.Count == 0)
            {
                stock.OptimizedLength = 0;
                return;
            }

            // Полная необходимая длина: Зона зажима + Детали + Пропилы
            double requiredLength = stock.TotalRequiredLength;

            // Округляем вверх до ближайших 500 мм для удобства резки/заказа
            double optimizedLen = Math.Ceiling(requiredLength / 500) * 500;

            // Не можем обрезать больше, чем есть в заготовке
            stock.OptimizedLength = Math.Min(optimizedLen, stock.StockLength);
        }

        public static bool AreStocksEqual(PipeStock? stock1, PipeStock? stock2)
        {
            if (stock1 == null || stock2 == null)
                return false;

            if (Math.Abs(stock1.StockLength - stock2.StockLength) > 0.1)
                return false;
            if (Math.Abs(stock1.ClampZone - stock2.ClampZone) > 0.1)
                return false;

            if (stock1.Placements.Count != stock2.Placements.Count)
                return false;

            var parts1 = stock1.Placements
                .GroupBy(p => new { p.Part.Width, p.Part.Height, p.Part.Length, p.Part.PartType })
                .Select(g => new { g.Key, Count = g.Count() })
                .OrderBy(x => x.Key.Width)
                .ThenBy(x => x.Key.Height)
                .ThenBy(x => x.Key.Length)
                .ToList();

            var parts2 = stock2.Placements
                .GroupBy(p => new { p.Part.Width, p.Part.Height, p.Part.Length, p.Part.PartType })
                .Select(g => new { g.Key, Count = g.Count() })
                .OrderBy(x => x.Key.Width)
                .ThenBy(x => x.Key.Height)
                .ThenBy(x => x.Key.Length)
                .ToList();

            if (parts1.Count != parts2.Count)
                return false;

            for (int i = 0; i < parts1.Count; i++)
            {
                if (Math.Abs(parts1[i].Key.Width - parts2[i].Key.Width) > 0.1 ||
                    Math.Abs(parts1[i].Key.Height - parts2[i].Key.Height) > 0.1 ||
                    Math.Abs(parts1[i].Key.Length - parts2[i].Key.Length) > 0.1 ||
                    parts1[i].Key.PartType != parts2[i].Key.PartType ||
                    parts1[i].Count != parts2[i].Count)
                {
                    return false;
                }
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

        private const double CutLoss = 10; // Отступ между деталями (пропил)

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