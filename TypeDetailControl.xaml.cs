using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using System.Linq;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для TypeDetailControl.xaml
    /// </summary>
    public partial class TypeDetailControl : UserControl
    {
        public int Count { get; set; }
        public string? NameTypeDetail { get; set; }

        private readonly Detail det;
        public List<WorkControl> WorkControls = new();

        public TypeDetailControl(Detail d)
        {
            InitializeComponent();
            DataContext = this;
            det = d;
            det.TypeDetailControls.Add(this);
            TypeDetailDrop.ItemsSource = MainWindow.M.dbTypeDetails.TypeDetails.Local.ToObservableCollection();
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            Remove();
        }
        public void Remove()
        {
            foreach (WorkControl w in WorkControls) w.Remove();
            WorkControls.Clear();
            MainWindow.M.ProductGrid.Children.Remove(this);
        }

        public void UpdatePosition()
        {
            int num = det.TypeDetailControls.IndexOf(this);
            if (det.TypeDetailControls.Count > 1)
            {
                for (int i = num + 1; i < det.TypeDetailControls.Count; i++)
                {
                    det.TypeDetailControls[i].Margin = new Thickness(0, det.TypeDetailControls[i].Margin.Top - 25, 0, 0);
                    foreach (WorkControl _w in det.TypeDetailControls[i].WorkControls)
                        _w.Margin = new Thickness(0, _w.Margin.Top - 25, 0, 0);
                }
            }
            MainWindow.M.AddTypeBtn.Margin = new Thickness(0, MainWindow.M.AddTypeBtn.Margin.Top - 25, 0, 0);
            det.UpdatePosition();
        }

        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(CountText.Text, out int count)) Count = count;
        }
    }
}
