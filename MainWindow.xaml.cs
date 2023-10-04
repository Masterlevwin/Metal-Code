using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace Metal_Code
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow M = new();

        public readonly TypeDetailContext dbTypeDetails = new();
        public readonly WorkContext dbWorks = new();
        public MainWindow()
        {
            InitializeComponent();
            M = this;
            Loaded += UpdateDrops;
        }

        private void UpdateDrops(object sender, RoutedEventArgs e)  // при загрузке окна
        {
            dbWorks.Database.EnsureCreated();
            dbWorks.Works.Load();

            dbTypeDetails.Database.EnsureCreated();
            dbTypeDetails.TypeDetails.Load();
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

        public List<Detail> Details = new();
        private void AddDetail(object sender, RoutedEventArgs e)
        {
            if (AddTypeBtn.Visibility != Visibility.Visible) AddTypeBtn.Visibility = Visibility.Visible;

            Detail detail = new();
            detail.Margin = AddDetailBtn.Margin;
            detail.NameDetail = $"{detail.Margin}";
            ProductGrid.Children.Add(detail);
            Details.Add(detail);

            AddDetailBtn.Margin = new Thickness(0, AddDetailBtn.Margin.Top + AddDetailBtn.Height + 5, 0, 0);
            AddTypeDetail(sender, e);   // при добавлении новой детали добавляем дроп комплектации
        }

        private void AddTypeDetail(object sender, RoutedEventArgs e)
        {
            if (AddWorkBtn.Visibility != Visibility.Visible) AddWorkBtn.Visibility = Visibility.Visible;

            TypeDetailControl type = new(Details[^1]);
            type.Margin = AddTypeBtn.Margin;
            ProductGrid.Children.Add(type);
            Grid.SetColumn(type, 1);

            AddTypeBtn.Margin = new Thickness(0, AddTypeBtn.Margin.Top + 25, 0, 0);
            AddDetailBtn.Margin = AddTypeBtn.Margin;    // перемещаем обе кнопки одновременно
            AddWork(sender, e);   // при добавлении дропа типовой детали добавляем дроп работ
        }

        private void AddWork(object sender, RoutedEventArgs e)
        {
            WorkControl work = new(Details[^1].TypeDetailControls[^1]);
            work.Margin = AddWorkBtn.Margin;
            ProductGrid.Children.Add(work);
            Grid.SetColumn(work, 2);

            AddWorkBtn.Margin = new Thickness(0, AddWorkBtn.Margin.Top + 25, 0, 0);
            AddDetailBtn.Margin = AddTypeBtn.Margin = AddWorkBtn.Margin;    // перемещаем все кнопки одновременно
        }
    }
}
