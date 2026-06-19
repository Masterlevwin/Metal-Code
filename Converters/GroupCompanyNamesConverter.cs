using Metal_Code.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Metal_Code.Converters
{
    public class GroupCompanyNamesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not CollectionViewGroup group) return string.Empty;

            var companies = group.Items
                .OfType<Offer>()
                .Select(o => o.Company?.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            if (companies.Count == 0) return string.Empty;
            if (companies.Count <= 2) return $" • {string.Join(", ", companies)}";

            return $" • {string.Join(", ", companies.Take(2))} и ещё {companies.Count - 2}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}