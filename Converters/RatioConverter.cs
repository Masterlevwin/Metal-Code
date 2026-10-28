using System;
using System.Globalization;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class RatioConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && double.TryParse(parameter?.ToString(),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double ratio))
                return d * ratio;
            return double.PositiveInfinity;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}