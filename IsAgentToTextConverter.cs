using System;
using System.Globalization;
using System.Windows.Data;

namespace Metal_Code
{
    public class IsAgentToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true ? "без НДС" : "с НДС";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
