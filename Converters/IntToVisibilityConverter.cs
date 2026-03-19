using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                int threshold = 0;
                if (parameter != null && int.TryParse(parameter.ToString(), out int p)) threshold = p;
                return count > threshold ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
