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

        TypeDetailContext dbTypeDetails = new();
        WorkContext dbWorks = new();
        public MainWindow()
        {
            InitializeComponent();
            M = this;
            Loaded += UpdateDrops;
        }

        // при загрузке окна
        private void UpdateDrops(object sender, RoutedEventArgs e)
        {
            // гарантируем, что база данных создана
            dbWorks.Database.EnsureCreated();
            // загружаем данные из БД
            dbWorks.Works.Load();
            // и устанавливаем данные в качестве контекста
            WorkDrop.ItemsSource = dbWorks.Works.Local.ToObservableCollection();

            dbTypeDetails.Database.EnsureCreated();
            dbTypeDetails.TypeDetails.Load();
            DetailDrop.ItemsSource = dbTypeDetails.TypeDetails.Local.ToObservableCollection();
        }

        private void OpenSettings(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            if (sender == Settings.Items[0])
            {
                TypeDetailWindow typeDetailWindow = new();
                typeDetailWindow.Show();
            }
            else if (sender == Settings.Items[1])
            {
                WorkWindow workWindow = new();
                workWindow.Show();
            }
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
                Width = 120,
                Height = 20,
                MinWidth = 120,
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
                ItemsSource = dbTypeDetails.TypeDetails.Local.ToObservableCollection(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Height = 20,
                Width = 150,
                MinHeight = 20,
                MinWidth = 150,
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

            AddWork(sender, e);   // при добавлении дропа типовой детали добавляем дроп работ
        }

        private void AddWork(object sender, RoutedEventArgs e)
        {
            ComboBox work = new()
            {
                ItemsSource = dbWorks.Works.Local.ToObservableCollection(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Height = 20,
                Width = 150,
                MinHeight = 20,
                MinWidth = 150,
                DisplayMemberPath = "Name",
                SelectedIndex = 0,
                ToolTip = "Выберите работу",
                IsEditable = true
            };

            work.Margin = AddWorkBtn.Margin;
            Grid.SetColumn(work, 1);
            ProductGrid.Children.Add(work);
            work.Focus();
            AddWorkBtn.Margin = new Thickness(0, AddWorkBtn.Margin.Top + AddWorkBtn.Height + 5, 0, 0);

            AddDetailBtn.Margin = AddTypeBtn.Margin = AddWorkBtn.Margin;    // перемещаем все кнопки одновременно
        }
    }
}
