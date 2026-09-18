using System;
using System.Globalization;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class MassToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float mass && mass > 0)
            {
                return $"{mass:0.###}"; // Показываем массу, если она есть
            }
            return "—"; // Показываем прочерк, если масса 0
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}