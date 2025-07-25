﻿using System;
using System.Windows.Data;
using System.Windows.Media;

namespace Metal_Code
{
    public class DestinyConverter : IValueConverter
    {
        public Brush HighlightBrush { get; set; } = Brushes.Red;
        public Brush DefaultBrush { get; set; } = Brushes.Blue;

        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (!MainWindow.M.Destinies.Contains((float)value))
                return HighlightBrush;
            else
                return DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}