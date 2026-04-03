using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class GroupCompanyNamesConverter : IValueConverter
    {
        // Кэш по экземпляру группы. CollectionViewGroup уникален на время жизни DataGrid.
        private readonly Dictionary<object, string> _cache = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            // Возвращаем кэшированный результат, если уже вычисляли
            if (_cache.TryGetValue(value, out string? cached)) return cached;

            string result = string.Empty;

            if (value is CollectionViewGroup group)
            {
                var companies = group.Items.OfType<Offer>()
                    .Select(o => o.Company?.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct()
                    .ToList();

                if (companies.Count > 2)
                    result = $" • {string.Join(", ", companies.Take(2))} и ещё {companies.Count - 2}";
                else if (companies.Count > 0)
                    result = $" • {string.Join(", ", companies)}";
            }

            _cache[value] = result;
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();

        public void ClearCache() => _cache.Clear();
    }
}