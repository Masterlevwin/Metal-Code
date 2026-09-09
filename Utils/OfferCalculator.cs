using System;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code.Utils
{
    public static class OfferCalculator
    {
        /// <summary>
        /// Перераспределяет стоимость скрытых деталей на видимые.
        /// ВАЖНО: Метод ожидает, что цены в коллекции parts УЖЕ содержат все коэффициенты (Ratio, BonusRatio).
        /// </summary>
        public static List<Part> PrepareVisiblePartsForOffer(IEnumerable<Part> parts)
        {
            return PrepareVisibleItemsCore(
                parts,
                p => p.IsHiddenInOffer,
                p => p.Count,
                p => p.Price,
                p => p.FixedPrice,
                (p, newPrice) => new Part
                {
                    Metal = p.Metal,
                    Destiny = p.Destiny,
                    Accuracy = p.Accuracy,
                    Description = p.Description,
                    Title = p.Title,
                    Count = p.Count,
                    Price = newPrice, // Новая цена включает долю скрытых деталей
                    Mass = p.Mass,
                    Way = p.Way,
                    FixedPrice = p.FixedPrice,
                    IsFixed = p.IsFixed
                }
            );
        }

        private static List<TOut> PrepareVisibleItemsCore<TIn, TOut>(
            IEnumerable<TIn> items,
            Func<TIn, bool> getIsHidden,
            Func<TIn, int> getCount,
            Func<TIn, float> getPrice,
            Func<TIn, float> getFixedPrice,
            Func<TIn, float, TOut> createOutput)
        {
            var itemList = items.ToList();
            var hiddenItems = itemList.Where(getIsHidden).ToList();
            var visibleItems = itemList.Where(x => !getIsHidden(x)).ToList();

            if (visibleItems.Count == 0)
                throw new InvalidOperationException("Нет видимых позиций для формирования КП.");

            // getPrice(p) уже возвращает ФИНАЛЬНУЮ цену с наценкой.
            // Мы просто считаем общую стоимость скрытых позиций и делим её на количество видимых.
            decimal hiddenTotal = hiddenItems.Sum(p => (decimal)(getCount(p) * getPrice(p)));
            int totalVisibleCount = visibleItems.Sum(getCount);

            if (totalVisibleCount <= 0)
                throw new InvalidOperationException("Общее количество видимых единиц равно нулю.");

            decimal hiddenCostPerUnit = hiddenTotal / totalVisibleCount;

            var result = new List<TOut>(visibleItems.Count);

            foreach (var item in visibleItems)
            {
                float originalPrice = getPrice(item);
                float fixedPrice = getFixedPrice(item);

                decimal basePrice = (decimal)originalPrice;

                // Добавляем долю скрытых деталей к уже готовой цене
                decimal priceWithHidden = basePrice + hiddenCostPerUnit;

                // Округляем вверх и проверяем, не упала ли цена ниже фиксированной
                decimal finalPrice = Math.Ceiling(priceWithHidden);

                if (finalPrice < (decimal)fixedPrice)
                    finalPrice = (decimal)fixedPrice;

                var outputItem = createOutput(item, (float)finalPrice);
                result.Add(outputItem);
            }

            return result;
        }
    }
}