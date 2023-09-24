using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow M = new();

        ApplicationContext db = new();
        public MainWindow()
        {
            InitializeComponent();
            M = this;
            Loaded += UpdateDetailDropItems;
        }

        // при загрузке окна
        private void UpdateDetailDropItems(object sender, RoutedEventArgs e)
        {
            // гарантируем, что база данных создана
            db.Database.EnsureCreated();
            // загружаем данные из БД
            db.TypeDetails.Load();
            // и устанавливаем данные в качестве контекста
            DetailDrop.ItemsSource = db.TypeDetails.Local.ToObservableCollection();
        }

        private void OpenSettings(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            TypeDetailWindow typeDetailWindow = new();
            typeDetailWindow.Show();
        }

        private void AddDetail(object sender, RoutedEventArgs e)
        {
            TextBox detailName = new()
            {
                Text = "Деталь",
                TextWrapping = TextWrapping.Wrap,
                MaxLength = 20,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Width = 150,
                Height = 20,
                MinWidth = 150,
                MinHeight = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ToolTip = "Введите название детали"
            };

            detailName.SpellCheck.IsEnabled = true;
            detailName.Margin = AddDetailBtn.Margin;
            Grid.SetColumn(detailName, 0);
            DetailGrid.Children.Add(detailName);
            detailName.Focus();

            AddTypeDetail(sender, e);   // при добавлении новой детали добавляем дроп комплектации
        }

        private void AddTypeDetail(object sender, RoutedEventArgs e)
        {
            ComboBox typeDetail = new()
            {
                ItemsSource = db.TypeDetails.Local.ToObservableCollection(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Height = 20,
                Width = 200,
                MinHeight = 20,
                MinWidth = 200,
                DisplayMemberPath = "Name",
                SelectedIndex = 0,
                ToolTip = "Выберите заготовку",
                IsEditable = true
            };

            typeDetail.Margin = AddTypeBtn.Margin;
            Grid.SetColumn(typeDetail, 1);
            DetailGrid.Children.Add(typeDetail);
            typeDetail.Focus();
            AddTypeBtn.Margin = new Thickness(0, AddTypeBtn.Margin.Top + AddTypeBtn.Height + 5, 0, 0);

            AddDetailBtn.Margin = AddTypeBtn.Margin;    // перемещаем обе кнопки одновременно
        }
    }
}
