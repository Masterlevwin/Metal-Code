using System;
using System.Windows;

namespace Metal_Code
{
    public partial class ReportPeriodDialog : Window
    {
        public DateTime SelectedFrom { get; private set; }
        public DateTime SelectedTo { get; private set; }
        public bool IsConfirmed { get; private set; }

        public ReportPeriodDialog(bool isAdmin, string managerName)
        {
            InitializeComponent();

            // ⭐ Период по умолчанию: если после 14-го — текущий месяц, иначе — прошлый
            SetDefaultPeriod();

            // Информация о режиме
            ModeInfoText.Text = isAdmin
                ? "🔓 Режим администратора: будут показаны продажи ВСЕХ менеджеров."
                : $"👤 Режим менеджера: будут показаны только ваши продажи ({managerName}).";
        }

        private void SetDefaultPeriod()
        {
            bool isAfter15th = DateTime.Today.Day > 14;
            var referenceMonth = isAfter15th ? DateTime.Today : DateTime.Today.AddMonths(-1);
            var firstDay = new DateTime(referenceMonth.Year, referenceMonth.Month, 1);
            var lastDay = DateTime.Today; // По текущую дату

            DateFrom.SelectedDate = firstDay;
            DateTo.SelectedDate = lastDay;
        }

        private void SetCurrentMonth_Click(object sender, RoutedEventArgs e)
        {
            var firstDay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateFrom.SelectedDate = firstDay;
            DateTo.SelectedDate = DateTime.Today;
        }

        private void SetLastMonth_Click(object sender, RoutedEventArgs e)
        {
            var lastMonth = DateTime.Today.AddMonths(-1);
            var firstDay = new DateTime(lastMonth.Year, lastMonth.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            DateFrom.SelectedDate = firstDay;
            DateTo.SelectedDate = lastDay;
        }

        private void SetQuarter_Click(object sender, RoutedEventArgs e)
        {
            int quarterStartMonth = ((DateTime.Today.Month - 1) / 3) * 3 + 1;
            var firstDay = new DateTime(DateTime.Today.Year, quarterStartMonth, 1);
            DateFrom.SelectedDate = firstDay;
            DateTo.SelectedDate = DateTime.Today;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (DateFrom.SelectedDate == null || DateTo.SelectedDate == null)
            {
                MessageBox.Show(this, "Укажите обе даты периода", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DateFrom.SelectedDate > DateTo.SelectedDate)
            {
                MessageBox.Show(this, "Дата начала не может быть позже даты окончания",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedFrom = DateFrom.SelectedDate.Value.Date;
            SelectedTo = DateTo.SelectedDate.Value.Date;
            IsConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
}