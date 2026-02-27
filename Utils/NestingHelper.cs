using System;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code.Utils
{
    public static class NestingHelper
    {
        // СТАНДАРТНЫЙ ЛИСТ: 1500 мм (ширина) × 3000 мм (длина)
        private const double SheetWidth = 3000;   // Ширина листа (ось X)
        private const double SheetHeight = 1500;  // Длина листа (ось Y)
        private const double Spacing = 10;        // Отступы между деталями и от краёв

        /// <summary>
        /// Пакетный нестинг: размещает ВСЕ детали на минимальном количестве листов
        /// Размещение: снизу вверх в столбце, затем слева направо по столбцам
        /// </summary>
        public static List<NestingSheet> CreateNestingForBatch(List<Part> parts)
        {
            var sheets = new List<NestingSheet>();

            // Размножаем детали по количеству
            var allParts = parts
                .SelectMany(p => Enumerable.Repeat(p, p.Count))
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

                // Если не разместили - создаём новый лист
                if (!placed)
                {
                    var newSheet = new NestingSheet { Width = SheetWidth, Height = SheetHeight };
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
        /// Размещает деталь по столбцам: снизу вверх, затем слева направо
        /// </summary>
        private static bool TryPlacePartByColumn(NestingSheet sheet, Part part)
        {
            double partWidth = part.PartType == PartType.Round ? part.Width : part.Width;
            double partHeight = part.PartType == PartType.Round ? part.Width : part.Height;

            // Находим все существующие столбцы (группируем по X с точностью до 1мм)
            var columns = sheet.Parts
                .GroupBy(p => Math.Round(p.X, 0)) // Группируем по округлённому X
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

                // Проверяем границы листа
                if (candidateY + partHeight > sheet.Height - Spacing)
                    continue;

                if (candidateX + partWidth > sheet.Width - Spacing)
                    continue;

                // Проверяем пересечения
                if (IsOverlapping(sheet, candidateX, candidateY, partWidth, partHeight))
                    continue;

                // Размещаем деталь
                sheet.Parts.Add(new PartPlacement { Part = part, X = candidateX, Y = candidateY });
                return true;
            }

            // Создаём новый столбец справа
            double newX = Spacing;

            if (sheet.Parts.Count > 0)
            {
                // Находим правую границу самого правого столбца
                double rightmostRight = sheet.Parts.Max(p =>
                {
                    double w = p.Part.PartType == PartType.Round ? p.Part.Width : p.Part.Width;
                    return p.X + w;
                });
                newX = rightmostRight + Spacing;
            }

            // Проверяем, помещается ли новый столбец
            if (newX + partWidth > sheet.Width - Spacing)
                return false;

            // Размещаем в начале нового столбца
            double newY = Spacing;

            // Финальная проверка пересечений
            if (IsOverlapping(sheet, newX, newY, partWidth, partHeight))
                return false;

            sheet.Parts.Add(new PartPlacement { Part = part, X = newX, Y = newY });
            return true;
        }

        /// <summary>
        /// Проверяет пересечение с учётом отступов (правильная логика)
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

                // Два прямоугольника НЕ пересекаются, если между ними есть зазор >= Spacing:
                if (x + width + Spacing <= ex) continue; // Новая деталь слева от существующей
                if (ex + ew + Spacing <= x) continue;    // Существующая деталь слева от новой
                if (y + height + Spacing <= ey) continue; // Новая деталь ниже существующей
                if (ey + eh + Spacing <= y) continue;     // Существующая деталь ниже новой

                // Если ни одно условие не выполнилось — есть пересечение
                return true;
            }
            return false;
        }

        private static void OptimizeSheetSize(NestingSheet sheet)
        {
            if (sheet.Parts.Count == 0) return;

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

            double optimizedWidth = Math.Ceiling((maxX + Spacing * 2) / 100) * 100;
            double optimizedHeight = Math.Ceiling((maxY + Spacing * 2) / 100) * 100;

            sheet.Width = Math.Min(optimizedWidth, SheetWidth);
            sheet.Height = Math.Min(optimizedHeight, SheetHeight);
        }

        public static bool AreSheetsEqual(NestingSheet? sheet1, NestingSheet? sheet2)
        {
            if (sheet1 == null || sheet2 == null)
                return false;

            if (Math.Abs(sheet1.Width - sheet2.Width) > 0.1)
                return false;
            if (Math.Abs(sheet1.Height - sheet2.Height) > 0.1)
                return false;
            if (sheet1.Parts.Count != sheet2.Parts.Count)
                return false;

            return true;
        }
    }

    public class NestingSheet
    {
        public double Width { get; set; } = 3000;
        public double Height { get; set; } = 1500;
        public List<PartPlacement> Parts { get; set; } = new();
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

    public class PartPlacement
    {
        public Part Part { get; set; } = null!;
        public double X { get; set; }
        public double Y { get; set; }
    }
}