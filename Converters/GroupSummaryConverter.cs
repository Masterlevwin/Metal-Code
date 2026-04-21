using System;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class GroupSummaryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is IEnumerable items)
            {
                int count = 0;
                float sum = 0;

                foreach (var item in items)
                {
                    if (item is OfferReportPreviewItem offer)
                    {
                        count++;
                        sum += offer.TotalAmount;
                    }
                }

                // Форматирование суммы и склонение слова "расчет"
                string formattedSum = sum.ToString("# ### ##0 ₽", culture);
                string word = count % 10 == 1 && count % 100 != 11 ? "расчет"
                            : count % 10 >= 2 && count % 10 <= 4 && count % 100 != 12 && count % 100 != 14 ? "расчета"
                            : "расчетов";

                return $"{count} {word} на сумму {formattedSum}";
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}