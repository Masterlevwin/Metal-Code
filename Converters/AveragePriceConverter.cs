using Metal_Code.Utils;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class AveragePriceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable groupItems)
            {
                // Фильтруем только цены за кг > 0, чтобы не искажать среднее значениями "шт" или "м2"
                var prices = groupItems.OfType<PriceListItem>()
                                       .Select(i => i.PricePerKg)
                                       .Where(p => p > 0)
                                       .ToList();

                if (prices.Any())
                    return prices.Average().ToString("N2") + " ₽";
            }
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
