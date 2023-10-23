using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для DetailControl.xaml
    /// </summary>
    public partial class DetailControl : UserControl
    {
        //Text="{Binding NameDetail, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:DetailControl}}}"
        public static readonly DependencyProperty dPname =
            DependencyProperty.Register("NameDetail", typeof(string), typeof(DetailControl));
        public string? NameDetail
        {
            get { return (string)GetValue(dPname); }
            set { SetValue(dPname, value); }
        }

        //Text="{Binding Count, Mode=TwoWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:DetailControl}}}"
        public static readonly DependencyProperty dPcount =
            DependencyProperty.Register("Count", typeof(int), typeof(DetailControl));
        public int Count
        {
            get { return (int)GetValue(dPcount); }
            set { SetValue(dPcount, value); }
        }

        public float Price { get; set; }

        public List<TypeDetailControl> TypeDetailControls = new();

        public DetailControl()
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
            if (MainWindow.M.DetailControls.Count == 1) return;
            Remove();
        }
        public void Remove()
        {
            for (int i = 0; i < TypeDetailControls.Count; i++) TypeDetailControls[i]?.Remove();
            MainWindow.M.DetailControls.Remove(this);
            MainWindow.M.ProductGrid.Children.Remove(this);
        }

        public void UpdatePosition(bool direction)
        {
            int num = MainWindow.M.DetailControls.IndexOf(this);
            if (MainWindow.M.DetailControls.Count > 1)
            {
                for (int i = num + 1; i < MainWindow.M.DetailControls.Count; i++)
                {
                    MainWindow.M.DetailControls[i].Margin = new Thickness(0,
                        direction ? MainWindow.M.DetailControls[i].Margin.Top + 25 : MainWindow.M.DetailControls[i].Margin.Top - 25, 0, 0);
                }
            }
        }

        private void SetName(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) NameDetail = tBox.Text;
        }

        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int count)) Count = count;
            MainWindow.M.TotalResult();
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
