using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Metal_Code.Converters
{
    public class LocalOfferColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b
                ? Color.FromRgb(255, 165, 0)  // Оранжевый
                : Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}