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
            Detail detail = new();
            detail.Margin = new Thickness(0, AddDetailBtn.Margin.Top + 25, 0, 0);

            Details.Add(detail);
            ProductGrid.Children.Add(detail);

            detail.AddTypeDetail();   // при добавлении новой детали добавляем дроп комплектации
        }
    }
}
