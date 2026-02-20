using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class PartTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PartType partType && parameter is string param)
            {
                // Параметр может содержать несколько типов через запятую: "Round,Rectangle"
                var allowedTypes = param.Split(',').Select(s => s.Trim());

                foreach (var typeStr in allowedTypes)
                {
                    if (Enum.TryParse<PartType>(typeStr, out var allowedType) && partType == allowedType)
                    {
                        return Visibility.Visible;
                    }
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
