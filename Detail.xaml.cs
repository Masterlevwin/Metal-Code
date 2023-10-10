using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для Detail.xaml
    /// </summary>
    public partial class Detail : UserControl
    {
        //Text="{Binding Price, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:Detail}}}"
        public static readonly DependencyProperty MyPropertyProperty =
            DependencyProperty.Register("Price", typeof(float), typeof(Detail));
        public float Price
        {
            get { return (float)GetValue(MyPropertyProperty); }
            set { SetValue(MyPropertyProperty, value); }
        }

        public string? NameDetail { get; set; }
        public int Count { get; set; }

        public List<TypeDetailControl> TypeDetailControls = new();

        public Detail()
        {
            InitializeComponent();
        }

        private void AddDetail(object sender, RoutedEventArgs e)
        {
            MainWindow.M.AddDetail();
        }
        public void AddTypeDetail()
        {
            TypeDetailControl type = new(this);

            if (TypeDetailControls.Count > 0)
                type.Margin = new Thickness(0,
                    TypeDetailControls[^1].Margin.Top + 25 * TypeDetailControls[^1].WorkControls.Count, 0, 0);

            TypeDetailControls.Add(type);
            DetailGrid.Children.Add(type);

            Grid.SetColumn(type, 2);
            
            type.AddWork();   // при добавлении дропа типовой детали добавляем дроп работ
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (MainWindow.M.Details.Count == 1) return;
            Remove();
        }
        public void Remove()
        {
            for (int i = 0; i < TypeDetailControls.Count; i++) TypeDetailControls[i]?.Remove();
            MainWindow.M.Details.Remove(this);
            MainWindow.M.ProductGrid.Children.Remove(this);
        }

        public void UpdatePosition(bool direction)
        {
            int num = MainWindow.M.Details.IndexOf(this);
            if (MainWindow.M.Details.Count > 1)
            {
                for (int i = num + 1; i < MainWindow.M.Details.Count; i++)
                {
                    MainWindow.M.Details[i].Margin = new Thickness(0,
                        direction ? MainWindow.M.Details[i].Margin.Top + 25 : MainWindow.M.Details[i].Margin.Top - 25, 0, 0);
                }
            }
        }

        public delegate void CountChanged();
        public event CountChanged? Counted;
        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(CountText.Text, out int count)) Count = count;
            Counted?.Invoke();
        }

        public void PriceResult()
        {
            Price = 0;
            foreach (TypeDetailControl t in TypeDetailControls)
            {
                foreach (WorkControl w in t.WorkControls)
                {
                    Price += w.Result;
                }
            }
            MainWindow.M.TotalResult();
        }
    }
}
