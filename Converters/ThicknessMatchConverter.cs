using System;
using System.Globalization;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    /// <summary>Возвращает true, если толщина строки совпадает с выбранной толщиной.</summary>
    public class ThicknessMatchConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return false;
            if (values[0] is not double rowThickness) return false;
            if (values[1] is not double selectedThickness) return false;
            return Math.Abs(rowThickness - selectedThickness) < 0.001;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
