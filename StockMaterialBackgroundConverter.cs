using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Metal_Code
{
    public class StockMaterialBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string? stockType = values.Length > 0 && values[0] != DependencyProperty.UnsetValue
                ? values[0]?.ToString()
                : null;

            string? materialName = values.Length > 1 && values[1] != DependencyProperty.UnsetValue
                ? values[1]?.ToString()
                : null;

            // Определяем тип заготовки
            bool isSheetMetal = stockType == "Лист металла";

            // Определяем тип материала по подстрокам
            MaterialType materialType = MaterialType.Other;

            if (!string.IsNullOrEmpty(materialName))
            {
                string lower = materialName.ToLowerInvariant();
                if (lower.Contains("ст3"))
                    materialType = MaterialType.BlackSteel;
                else if (lower.Contains("aisi"))
                    materialType = MaterialType.StainlessSteel;
                else if (lower.Contains("амг") || lower.Contains("д16"))
                    materialType = MaterialType.Aluminum;
            }

            // Выбираем цвета
            Color baseColor = isSheetMetal
                ? Color.FromRgb(220, 240, 255)   // Лист: светло-голубой
                : Color.FromRgb(240, 240, 240);  // Остальное: нейтральный серо-белый (НЕ розовый!)

            Color accentColor = materialType switch
            {
                MaterialType.BlackSteel => Color.FromRgb(200, 200, 200),     // Серый
                MaterialType.StainlessSteel => Color.FromRgb(180, 210, 255),     // Голубой
                MaterialType.Aluminum => Color.FromRgb(255, 220, 190),     // Персиковый (тёплый, но не розовый)
                _ => Color.FromRgb(230, 255, 230)      // Светло-зелёный (для "остальное")
            };

            // Смешиваем 50/50 для чёткой разницы
            Color mixed = MixColors(baseColor, accentColor, ratio: 0.5);
            return new SolidColorBrush(mixed);
        }

        private static Color MixColors(Color c1, Color c2, double ratio)
        {
            ratio = Math.Clamp(ratio, 0.0, 1.0);
            byte r = (byte)(c1.R * (1 - ratio) + c2.R * ratio);
            byte g = (byte)(c1.G * (1 - ratio) + c2.G * ratio);
            byte b = (byte)(c1.B * (1 - ratio) + c2.B * ratio);
            return Color.FromRgb(r, g, b);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    internal enum MaterialType
    {
        BlackSteel,
        StainlessSteel,
        Aluminum,
        Other
    }
}