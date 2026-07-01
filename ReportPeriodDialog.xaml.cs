using System;
using System.Windows;

namespace Metal_Code
{
    public partial class ReportPeriodDialog : Window
    {
        public DateTime SelectedFrom { get; private set; }
        public DateTime SelectedTo { get; private set; }
        public bool IsConfirmed { get; private set; }

        // ⭐ НОВОЕ: тип отчёта
        public ReportType ReportType { get; }

        /// <summary>
        /// Конструктор диалога выбора периода отчёта.
        /// </summary>
        /// <param name="isAdmin">Является ли текущий пользователь администратором.</param>
        /// <param name="managerName">Имя текущего менеджера.</param>
        /// <param name="reportType">Тип отчёта: Sales (с выбором периода) или Production (все расчёты).</param>
        public ReportPeriodDialog(bool isAdmin, string managerName, ReportType reportType = ReportType.Sales)
        {
            InitializeComponent();
            ReportType = reportType;

            // ⭐ Настраиваем интерфейс в зависимости от типа отчёта
            if (reportType == ReportType.Production)
            {
                Title = "Отчет по расчетам в производстве";
                TitleText.Text = "Отчет по расчетам в производстве";

                // ⭐ Скрываем блок выбора периода
                PeriodPanel.Visibility = Visibility.Collapsed;

                // ⭐ Устанавливаем маркеры "все расчёты"
                SelectedFrom = DateTime.MinValue;
                SelectedTo = DateTime.MaxValue;

                ModeInfoText.Text = isAdmin
                    ? "🔓 Режим администратора: будут показаны ВСЕ расчёты в производстве (с номером заказа, но не отгруженные)."
                    : $"👤 Режим менеджера: будут показаны только ваши расчёты в производстве ({managerName}).";
            }
            else
            {
                Title = "Выбор периода отчета";
                TitleText.Text = "Выберите период для отчета по продажам:";

                // ⭐ Период по умолчанию: если после 14-го — текущий месяц, иначе — прошлый
                SetDefaultPeriod();

                ModeInfoText.Text = isAdmin
                    ? "🔓 Режим администратора: будут показаны продажи ВСЕХ менеджеров."
                    : $"👤 Режим менеджера: будут показаны только ваши продажи ({managerName}).";
            }
        }

        private void SetDefaultPeriod()
        {
            bool isAfter15th = DateTime.Today.Day > 14;
            var referenceMonth = isAfter15th ? DateTime.Today : DateTime.Today.AddMonths(-1);
            var firstDay = new DateTime(referenceMonth.Year, referenceMonth.Month, 1);
            var lastDay = DateTime.Today;

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
            // ⭐ Для отчёта по производству даты уже установлены (MinValue/MaxValue)
            if (ReportType == ReportType.Production)
            {
                IsConfirmed = true;
                DialogResult = true;
                Close();
                return;
            }

            // ⭐ Для отчёта по продажам — проверяем введённые даты
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