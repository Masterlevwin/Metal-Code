using System;
using System.Globalization;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class PartTypeToReadOnlyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PartType partType)
            {
                // Для произвольной формы поля Width/Height доступны только для чтения
                return partType == PartType.Custom;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}