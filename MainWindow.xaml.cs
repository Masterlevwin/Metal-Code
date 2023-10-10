using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Windows.Controls;

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

            AddDetail();
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

        public string? Product { get; set; }
        public int Count { get; set; }
        public float Price { get; set; }

        public List<Detail> Details = new();
        public void AddDetail()
        {
            Detail detail = new();

            if (Details.Count > 0)
                detail.Margin = new Thickness(0,
                    Details[^1].Margin.Top + 25 * Details[^1].TypeDetailControls.Sum(t => t.WorkControls.Count), 0, 0);
            
            Details.Add(detail);
            ProductGrid.Children.Add(detail);

            detail.AddTypeDetail();   // при добавлении новой детали добавляем дроп комплектации
        }

        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int c)) Count = c;
            if (Count > 0) TotalResult();
        }

        public void TotalResult()
        {
            Price = 0;
            foreach (Detail d in Details) Price += d.Price;
            Total.Text = $"{Price * Count}";
            DetailsGrid.ItemsSource = Details;
        }
    }
}
