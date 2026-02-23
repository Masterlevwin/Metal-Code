using System;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code.Utils
{
    public static class NestingHelper
    {
        private const double SheetWidth = 3000;
        private const double SheetHeight = 1500;
        private const double Spacing = 10;

        /// <summary>
        /// Раскладывает детали по листам с учётом обрезки последнего листа
        /// </summary>
        public static List<NestingSheet> CreateNesting(Part part)
        {
            var sheets = new List<NestingSheet>();

            if (part == null || part.Count <= 0)
                return sheets;

            if (part.DisplayGeometry == null)
                PartPreviewGenerator.EnsureDisplayGeometry(part);

            double partWidth = part.PartType == PartType.Round ? part.Width : part.Width;
            double partHeight = part.PartType == PartType.Round ? part.Width : part.Height;
            int remaining = part.Count;

            while (remaining > 0)
            {
                var sheet = new NestingSheet
                {
                    Width = SheetWidth,
                    Height = SheetHeight
                };

                int placedOnSheet = PlacePartsOnSheet(sheet, part, partWidth, partHeight, ref remaining);

                if (placedOnSheet > 0)
                {
                    // Если это последний лист и деталей мало — уменьшаем размер листа
                    if (remaining == 0 && placedOnSheet < CalculatePartsPerFullSheet(partWidth, partHeight))
                    {
                        OptimizeLastSheet(sheet);
                    }
                    sheets.Add(sheet);
                }
                else
                {
                    break;
                }
            }

            return sheets;
        }

        private static int PlacePartsOnSheet(
            NestingSheet sheet,
            Part part,
            double partWidth,
            double partHeight,
            ref int remaining)
        {
            int placed = 0;
            double x = Spacing;
            double y = Spacing;

            while (remaining > 0)
            {
                // Проверяем, помещается ли деталь по высоте
                if (y + partHeight > SheetHeight - Spacing)
                    break;

                int partsInRow = CalculatePartsInRow(x, partWidth);
                if (partsInRow <= 0)
                {
                    y += partHeight + Spacing;
                    x = Spacing;
                    continue;
                }

                int toPlace = Math.Min(partsInRow, remaining);
                for (int i = 0; i < toPlace; i++)
                {
                    sheet.Parts.Add(new PartPlacement
                    {
                        Part = part,
                        X = x,
                        Y = y
                    });
                    x += partWidth + Spacing;
                    placed++;
                    remaining--;
                }

                y += partHeight + Spacing;
                x = Spacing;
            }

            return placed;
        }

        private static int CalculatePartsInRow(double startX, double partWidth)
        {
            double availableWidth = SheetWidth - Spacing - startX;
            if (availableWidth <= 0)
                return 0;

            return (int)(availableWidth / (partWidth + Spacing));
        }

        private static int CalculatePartsPerFullSheet(double partWidth, double partHeight)
        {
            int partsPerRow = (int)Math.Floor((SheetWidth - Spacing) / (partWidth + Spacing));
            int rows = (int)Math.Floor((SheetHeight - Spacing) / (partHeight + Spacing));
            return partsPerRow * rows;
        }

        /// <summary>
        /// Оптимизирует последний лист — уменьшает его размер до минимально необходимого
        /// </summary>
        private static void OptimizeLastSheet(NestingSheet sheet)
        {
            if (sheet.Parts.Count == 0) return;

            // Находим максимальные координаты размещённых деталей
            double maxX = sheet.Parts.Max(p =>
            {
                double width = p.Part.PartType == PartType.Round ? p.Part.Width : p.Part.Width;
                return p.X + width;
            });

            double maxY = sheet.Parts.Max(p =>
            {
                double height = p.Part.PartType == PartType.Round ? p.Part.Width : p.Part.Height;
                return p.Y + height;
            });

            // Добавляем отступы и округляем до кратного 100 мм
            double optimizedWidth = Math.Ceiling((maxX + Spacing * 2) / 100) * 100;
            double optimizedHeight = Math.Ceiling((maxY + Spacing * 2) / 100) * 100;

            // Ограничиваем стандартными размерами листа
            sheet.Width = Math.Min(optimizedWidth, SheetWidth);
            sheet.Height = Math.Min(optimizedHeight, SheetHeight);
        }

        /// <summary>
        /// Проверяет, являются ли два листа одинаковыми (по размерам и количеству деталей)
        /// </summary>
        public static bool AreSheetsEqual(NestingSheet? sheet1, NestingSheet? sheet2)
        {
            if (sheet1 == null || sheet2 == null)
                return false;

            // Сравниваем размеры листов
            if (Math.Abs(sheet1.Width - sheet2.Width) > 0.1)
                return false;
            if (Math.Abs(sheet1.Height - sheet2.Height) > 0.1)
                return false;

            // Сравниваем количество деталей
            if (sheet1.Parts.Count != sheet2.Parts.Count)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Один лист с размещёнными на нём деталями
    /// </summary>
    public class NestingSheet
    {
        public double Width { get; set; } = 3000;
        public double Height { get; set; } = 1500;
        public List<PartPlacement> Parts { get; set; } = new();
    }


    /// <summary>
    /// Компаратор для сравнения листов в словаре
    /// </summary>
    public class NestingSheetComparer : IEqualityComparer<NestingSheet>
    {
        public bool Equals(NestingSheet? x, NestingSheet? y)
        {
            return NestingHelper.AreSheetsEqual(x, y);
        }

        public int GetHashCode(NestingSheet obj)
        {
            if (obj == null) return 0;
            // Хэш по размерам и количеству деталей
            return obj.Width.GetHashCode() ^ obj.Height.GetHashCode() ^ obj.Parts.Count.GetHashCode();
        }
    }

    /// <summary>
    /// Размещение одной детали на листе
    /// </summary>
    public class PartPlacement
    {
        public Part Part { get; set; } = null!;
        public double X { get; set; }  // От левого края листа
        public double Y { get; set; }  // От нижнего края листа (в инвертированной системе)
    }
}