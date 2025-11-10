using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Metal_Code
{
    public class TitleLengthToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int max = 0;
            if (parameter is string paramStr && int.TryParse(paramStr, out int p))
                max = p;

            if (value is string title)
            {
                return title.Length > max ? new SolidColorBrush(Color.FromArgb(255, 240, 100, 100)) : Brushes.Black;
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}