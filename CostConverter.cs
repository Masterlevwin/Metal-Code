using System;
using System.Windows.Data;

namespace Metal_Code
{
    [ValueConversion(typeof(float), typeof(string))]
    public class CostConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            // Возвращаем строку в формате 123 456 789 руб.
            return ((float)value).ToString("# ### ###", culture) + " руб";
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (float.TryParse($"{value}", System.Globalization.NumberStyles.Any,
                         culture, out float result))
            {
                return result;
            }
            else if (float.TryParse($"{value}".Replace(" руб", ""), System.Globalization.NumberStyles.Any,
                         culture, out result))
            {
                return result;
            }
            return value;
        }
    }
}
