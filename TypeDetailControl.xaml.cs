using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для TypeDetailControl.xaml
    /// </summary>
    public partial class TypeDetailControl : UserControl
    {
        //Text="{Binding Count, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:TypeDetailControl}}}"
        public static readonly DependencyProperty dPcount =
            DependencyProperty.Register("Count", typeof(int), typeof(TypeDetailControl));
        public int Count
        {
            get { return (int)GetValue(dPcount); }
            set { SetValue(dPcount, value); }
        }

        public readonly DetailControl det;
        public List<WorkControl> WorkControls = new();

        public TypeDetailControl(DetailControl d)
        {
            InitializeComponent();
            det = d;
            TypeDetailDrop.ItemsSource = MainWindow.M.dbTypeDetails.TypeDetails.Local.ToObservableCollection();
            foreach (string s in MetalDict.Keys) MetalDrop.Items.Add(s);
        }

        public Dictionary<string, float> MetalDict = new()
        {
            ["ст3"] = 7.85f,
            ["09г2с"] = 7.85f,
            ["хк"] = 7.85f,
            ["цинк"] = 7.85f,
            ["aisi430"] = 8f,
            ["aisi430шлиф"] = 8f,
            ["aisi430зерк"] = 8f,
            ["aisi304"] = 8f,
            ["aisi304шлиф"] = 8f,
            ["aisi304зерк"] = 8f,
            ["aisi321"] = 8f,
            ["амг2"] = 2.7f,
            ["амг5"] = 2.7f,
            ["амг6"] = 2.7f,
            ["д16"] = 2.7f,
            ["латунь"] = 8.5f,
            ["медь"] = 8.9f
        };
        private void SetMetal(object sender, SelectionChangedEventArgs e)
        {
            SetMetal();
        }
        private void SetMetal()
        {
            SetMetal(MetalDrop.SelectedIndex);
        }
        public void SetMetal(int ndx)
        {
            MetalDrop.SelectedIndex = ndx;
            Priced?.Invoke();
        }

        private void AddTypeDetail(object sender, RoutedEventArgs e)
        {
            det.AddTypeDetail();
        }

        public void AddWork()
        {
            WorkControl work = new(this);

            if (WorkControls.Count > 0) work.Margin = new Thickness(0, WorkControls[^1].Margin.Top + 25, 0, 0);

            WorkControls.Add(work);
            TypeDetailGrid.Children.Add(work);

            Grid.SetColumn(work, 2);

            work.UpdatePosition(true);
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (det.TypeDetailControls.Count == 1) return;
            Remove();
        }
        public void Remove()
        {
            while (WorkControls.Count > 0) WorkControls[^1].Remove();
            det.TypeDetailControls.Remove(this);
            det.DetailGrid.Children.Remove(this);
        }

        public void UpdatePosition(bool direction)
        {
            int numT = det.TypeDetailControls.IndexOf(this);
            if (det.TypeDetailControls.Count > 1)
            {
                for (int i = numT + 1; i < det.TypeDetailControls.Count; i++)
                {
                    det.TypeDetailControls[i].Margin = new Thickness(0,
                        direction ? det.TypeDetailControls[i].Margin.Top + 25 : det.TypeDetailControls[i].Margin.Top - 25, 0, 0);
                }
            }
            det.UpdatePosition(direction);
        }

        public delegate void PriceChanged();
        public event PriceChanged? Priced, Counted;
        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int count)) Count = count;
            Counted?.Invoke();
        }

        private void PriceView(object sender, SelectionChangedEventArgs e)
        {
            Priced?.Invoke();
        }
    }
}
