using System;
using System.Globalization;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class PartTypeToLocalizedNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null) return string.Empty;

            string type = $"{value}";

            return type switch
            {
                "Rectangle" => "Прямоугольник",
                "Round" => "Круг",
                "RectangularTube" => "Профильная труба",
                "RoundTube" => "Круглая труба",
                _ => type // Если тип неизвестен, оставляем как есть
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}