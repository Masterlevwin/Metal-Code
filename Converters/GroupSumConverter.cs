using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Markup;

namespace Metal_Code.Converters
{
    public class GroupSumConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CollectionViewGroup group)
            {
                var sum = group.Items.OfType<OfferReportPreviewItem>().Sum(i => i.TotalWorks);
                return $"Сумма работ: {sum:N2} ₽";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }

    public class BoolToTaxConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? "ИП/ПК" : "ООО";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}