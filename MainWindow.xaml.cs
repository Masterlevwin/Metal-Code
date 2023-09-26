using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
            //Loaded += UpdateDrops;
        }

        // при загрузке окна
        private void UpdateDrops(object sender, RoutedEventArgs e)  // данный метод также вызывает событие GotFocus
                                                                    // это временная мера!
        {
            // гарантируем, что база данных создана
            dbWorks.Database.EnsureCreated();
            // загружаем данные из БД
            dbWorks.Works.Load();
            // и устанавливаем данные в качестве контекста
            WorkDrop.ItemsSource = dbWorks.Works.Local.ToObservableCollection();
            PriceView();                                            // временно

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
                ToolTip = "Введите название детали",

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

            TextBlock priceView = new()
            {
                Text = "Цена",
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ToolTip = "Цена за единицу, руб"
            };

            work.Margin = AddWorkBtn.Margin;
            Grid.SetColumn(work, 1);
            ProductGrid.Children.Add(work);

            priceView.Margin = new Thickness(175, AddWorkBtn.Margin.Top, 10, 0);
            Grid.SetColumn(priceView, 1);
            ProductGrid.Children.Add(priceView);

            AddWorkBtn.Margin = new Thickness(0, AddWorkBtn.Margin.Top + AddWorkBtn.Height + 5, 0, 0);
            AddDetailBtn.Margin = AddTypeBtn.Margin = AddWorkBtn.Margin;    // перемещаем все кнопки одновременно

            Focus();
        }

        private void PriceView()
        {
            if (WorkDrop.SelectedItem is not Work work) return;

            if (work.Name == "Покупка")
            {
                if (DetailDrop.SelectedItem is not TypeDetail typeDetail) PriceWork.Text = $"{0}";
                else PriceWork.Text = $"{typeDetail.Price}";
            }    
            else PriceWork.Text = $"{work.Price}";
        }
    }
}
