using Metal_Code.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PriceMetalWindow.xaml
    /// </summary>
    public partial class PriceMetalWindow : Window
    {
        public TypeDetailControl TypeDetailControl {  get; set; }

        public PriceMetalWindow(TypeDetailControl typeDetailControl)
        {
            InitializeComponent();
            DataContext = this;
            TypeDetailControl = typeDetailControl;
        }

        // Коллекция для агрегированных данных
        public List<CategoryAveragePrice> AggregatedPrices { get; set; } = new();

        // Метод для удобной загрузки данных извне
        public void LoadData(List<CategoryAveragePrice> aggregatedList)
        {
            AggregatedPrices = aggregatedList;
            AggregatedDataGrid.ItemsSource = AggregatedPrices;

            // Совпадающие позиции (верхняя таблица)
            if (MainWindow.M.Metals != null && MainWindow.M.Metals.Any())
            {
                var matched = aggregatedList
                    .Where(a => MainWindow.M.Metals.Any(m => m.Name?.Equals(a.Grade, StringComparison.OrdinalIgnoreCase) == true))
                    .Select(a => new MatchedPriceItem
                    {
                        CategoryName = a.CategoryName,
                        Grade = a.Grade,
                        AveragePricePerKg = a.AveragePricePerKg,
                        ItemsCount = a.ItemsCount,
                        MinPrice = a.MinPrice,
                        MaxPrice = a.MaxPrice,
                        PriceWithMarkup = a.PriceWithMarkup
                    })
                    .OrderByDescending(x => x.CategoryName)
                    .ThenBy(x => x.Grade)
                    .ToList();

                MatchedDataGrid.ItemsSource = matched;
                Title = $"Прайс: {matched.Count} совпадений | {aggregatedList.Count} категорий";
            }
            else
            {
                MatchedDataGrid.ItemsSource = new List<MatchedPriceItem>();
                Title = $"Прайс: {aggregatedList.Count} категорий";
            }

            // 🔥 Подсветка строк через код
            if (MainWindow.M.Metals != null && MainWindow.M.Metals.Any())
            {
                AggregatedDataGrid.LoadingRow += (s, args) =>
                {
                    if (args.Row.Item is CategoryAveragePrice item && MainWindow.M.Metals != null)
                    {
                        bool isMatch = MainWindow.M.Metals.Any(m => m.Name?.Equals(item.Grade, StringComparison.OrdinalIgnoreCase) == true);

                        if (isMatch)
                        {
                            args.Row.Background = new SolidColorBrush(Color.FromRgb(255, 235, 59));
                        }
                        else
                        {
                            // 🔥 Ключевая строка: сбрасываем локальное значение фона
                            args.Row.ClearValue(BackgroundProperty);
                        }
                    }
                };
            }
        }

        private void BtnApplyPrice_Click(object sender, RoutedEventArgs e)
        {
            // 1. Проверяем источник события
            if (sender is not Button button) return;

            // 2. Получаем данные из строки (только MatchedPriceItem)
            var item = button.Tag as MatchedPriceItem ?? button.DataContext as MatchedPriceItem;
            if (item == null)
            {
                MessageBox.Show("Не удалось определить данные для применения цены", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Подтверждение применения
            var result = MessageBox.Show(
                $"Применить цену {item.PriceWithMarkup:N2} ₽/кг для марки «{item.Grade}»?\n" +
                $"Категория: {item.CategoryName}",
                "Подтверждение применения",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // 4. Расчёт и применение цены
            try
            {
                // Работаем с decimal для точности, затем округляем вверх
                decimal mass = (decimal)TypeDetailControl.Mass;
                decimal extraResult = Math.Ceiling(item.PriceWithMarkup * mass);

                // Обновляем заготовку через публичный метод (единая точка обновления)
                TypeDetailControl.SetExtraResult((float)extraResult);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при расчёте: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
