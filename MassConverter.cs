using System;
using System.Windows.Data;

namespace Metal_Code
{
    [ValueConversion(typeof(float), typeof(string))]
    public class MassConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if ((float)value >= 1000) return ((float)Math.Ceiling((float)value / 1000)).ToString(culture) + " т";
            return ((float)value).ToString(culture) + " кг";
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (float.TryParse($"{value}", System.Globalization.NumberStyles.Any,
                         culture, out float result))
            {
                return result;
            }
            else if (float.TryParse($"{value}".Replace(" кг", ""), System.Globalization.NumberStyles.Any,
                         culture, out result))
            {
                return result;
            }
            else if (float.TryParse($"{value}".Replace(" т", ""), System.Globalization.NumberStyles.Any,
                         culture, out result))
            {
                return result * 1000;
            }
            return value;
        }
    }
}