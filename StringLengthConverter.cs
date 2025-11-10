using System;
using System.Globalization;
using System.Windows.Data;

namespace Metal_Code
{
    public class StringLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int max = 100;
            if (parameter is string paramStr && int.TryParse(paramStr, out int p))
                max = p;

            int length = (value as string)?.Length ?? 0;
            return $"({length}/{max})";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}