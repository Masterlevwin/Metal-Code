using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Metal_Code.Converters
{
    public class ShipmentButtonTextConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
                return date.ToString("d.MM.y");

            return "Отгрузить";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}