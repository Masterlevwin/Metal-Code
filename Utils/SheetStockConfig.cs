using System.Collections.Generic;
using System.Linq;

namespace Metal_Code.Utils
{
    public class SheetStock
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public string Name => $"{Width:F0}x{Height:F0}";
        public double Area => Width * Height;
    }

    public static class SheetStockConfig
    {
        private static readonly List<SheetStock> AllStocks = new()
    {
        new SheetStock { Width = 3000, Height = 1500 }, // Стандарт
        new SheetStock { Width = 2500, Height = 1250 },
        new SheetStock { Width = 2000, Height = 1000 },
        new SheetStock { Width = 3000, Height = 1200 },
        new SheetStock { Width = 1500, Height = 600 }
    };

        /// <summary>
        /// Возвращает список допустимых листов. 
        /// Если материал не распознан или нет подходящих форматов, возвращает ПУСТОЙ список.
        /// </summary>
        public static List<SheetStock> GetAllowedStocks(string materialName, double thickness)
        {
            if (string.IsNullOrEmpty(materialName))
                return new List<SheetStock>(); // Важно: пустой список вместо fallback

            string matLower = materialName.ToLower().Trim();

            // Сталь ("ст3", "09г2с"...)
            if (matLower.Contains("ст") || matLower.Contains("хк")
                || matLower.Contains("09г2с") || matLower.Contains("цинк") || matLower.Contains("рифл"))
            {
                var stocks = new List<SheetStock>();

                // Если толщина < 3, добавляем 2500x1250, иначе - 3000х1500
                if (thickness < 3)
                    stocks.Add(AllStocks.First(s => s.Width == 2500 && s.Height == 1250));
                else stocks.Add(AllStocks.First(s => s.Width == 3000 && s.Height == 1500));

                return stocks;
            }

            // Нержавейка
            if (matLower.Contains("aisi"))
            {
                return new List<SheetStock>
            {
                AllStocks.First(s => s.Width == 3000 && s.Height == 1500),
                AllStocks.First(s => s.Width == 2500 && s.Height == 1250),
                AllStocks.First(s => s.Width == 2000 && s.Height == 1000)
            };
            }

            // Алюминий
            if (matLower.Contains("амг") || matLower.Contains("д16") || matLower.Contains("ад"))
            {
                return new List<SheetStock>
            {
                AllStocks.First(s => s.Width == 3000 && s.Height == 1500),
                AllStocks.First(s => s.Width == 3000 && s.Height == 1200)
            };
            }

            // Медь / Латунь
            if (matLower.Contains("медь") || matLower.Contains("латунь"))
            {
                return new List<SheetStock>
            {
                AllStocks.First(s => s.Width == 1500 && s.Height == 600)
            };
            }

            // Если материал не распознан — возвращаем пустой список.
            // Это заставит алгоритм выбросить ошибку, а не использовать "левый" лист.
            return new List<SheetStock>();
        }
    }
}