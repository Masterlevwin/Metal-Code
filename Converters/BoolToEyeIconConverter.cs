using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class BoolToEyeIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string resourceName = (bool)value ? "EyeCloseGeometry" : "EyeOpenGeometry";
            return Application.Current.FindResource(resourceName);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}