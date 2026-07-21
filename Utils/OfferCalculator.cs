using System;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code.Utils
{
    public static class OfferCalculator
    {
        // Публичный метод для Part
        public static List<Part> PrepareVisiblePartsForOffer(
            IEnumerable<Part> parts,
            float ratio,
            float bonusRatio,
            bool applyMarkup = true)
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
                    Price = newPrice,
                    Mass = p.Mass,
                    Way = p.Way,
                    FixedPrice = p.FixedPrice,
                    IsFixed = p.IsFixed
                },
                ratio,
                bonusRatio,
                applyMarkup
            );
        }

        // Приватное ядро расчёта — работает с любым типом через делегаты
        private static List<TOut> PrepareVisibleItemsCore<TIn, TOut>(
            IEnumerable<TIn> items,
            Func<TIn, bool> getIsHidden,
            Func<TIn, int> getCount,
            Func<TIn, float> getPrice,
            Func<TIn, float> getFixedPrice,
            Func<TIn, float, TOut> createOutput,
            float ratio,
            float bonusRatio,
            bool applyMarkup)
        {
            var itemList = items.ToList();
            var hiddenItems = itemList.Where(getIsHidden).ToList();
            var visibleItems = itemList.Where(x => !getIsHidden(x)).ToList();

            if (visibleItems.Count == 0)
                throw new InvalidOperationException("Нет видимых позиций для формирования КП.");

            decimal hiddenTotal = hiddenItems.Sum(p => (decimal)(getCount(p) * getPrice(p)));
            int totalVisibleCount = visibleItems.Sum(getCount);

            if (totalVisibleCount <= 0)
                throw new InvalidOperationException("Общее количество видимых единиц равно нулю.");

            decimal hiddenCostPerUnit = hiddenTotal / totalVisibleCount;

            var result = new List<TOut>(visibleItems.Count);

            foreach (var item in visibleItems)
            {
                int count = getCount(item);
                float originalPrice = getPrice(item);
                float fixedPrice = getFixedPrice(item);

                decimal basePrice = (decimal)originalPrice;
                decimal priceWithHidden = basePrice + hiddenCostPerUnit;

                decimal adjustedPrice = applyMarkup
                    ? priceWithHidden * (decimal)ratio * ((100 + (decimal)bonusRatio) / 100)
                    : priceWithHidden;

                decimal finalPrice = Math.Ceiling(adjustedPrice);

                if (finalPrice < (decimal)fixedPrice)
                    finalPrice = (decimal)fixedPrice;

                var outputItem = createOutput(item, (float)finalPrice);
                result.Add(outputItem);
            }

            return result;
        }
    }
}
