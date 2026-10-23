using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Metal_Code.Converters
{
    /// <summary>
    /// Возвращает цвет обводки ячейки усилия в зависимости от допустимости
    /// для текущей длины гиба. Зелёный — согнём, красный — превышаем усилие станка,
    /// прозрачный — пустая/недопустимая комбинация.
    /// </summary>
    public class AllowedBorderConverter : IMultiValueConverter
    {
        private const double MaxPressTonnage = 100;

        private static readonly Brush AllowedBrush = CreateFrozenBrush(Color.FromRgb(76, 175, 80));
        private static readonly Brush ExceededBrush = CreateFrozenBrush(Color.FromRgb(244, 67, 54));

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return Brushes.Transparent;

            // значения могут прийти как DependencyProperty.UnsetValue
            if (values[0] is not string forceText || string.IsNullOrWhiteSpace(forceText))
                return Brushes.Transparent;

            if (values[1] is not double bendLength || bendLength <= 0)
                return Brushes.Transparent;

            // Значение в ячейке — усилие на 1 метр гиба
            if (!double.TryParse(forceText, NumberStyles.Any, CultureInfo.CurrentCulture, out double forcePerMeter))
                return Brushes.Transparent;

            double force = forcePerMeter * (bendLength / 1000.0);

            return force <= MaxPressTonnage ? AllowedBrush : ExceededBrush;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
