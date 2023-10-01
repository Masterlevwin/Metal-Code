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
        public int Count { get; set; }
        public string? NameDetail { get; set; }

        public List<TypeDetailControl> TypeDetailControls = new();

        public Detail()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            Remove();
        }
        public void Remove()
        {
            foreach (TypeDetailControl t in TypeDetailControls) t.Remove();
            MainWindow.M.ProductGrid.Children.Remove(this);
        }

        public void Update()
        {
            int num = MainWindow.M.Details.IndexOf(this);
            for (int i = num + 1; i < MainWindow.M.Details.Count; i++)
            {
                MainWindow.M.Details[i].Margin = new Thickness(0, MainWindow.M.Details[i].Margin.Top - 25, 0, 0);
                foreach (TypeDetailControl _t in MainWindow.M.Details[i].TypeDetailControls)
                {
                    _t.Margin = new Thickness(0, _t.Margin.Top - 25, 0, 0);
                    foreach (WorkControl _w in _t.WorkControls)
                        _w.Margin = new Thickness(0, _w.Margin.Top - 25, 0, 0);
                }       
            }
            MainWindow.M.AddDetailBtn.Margin = new Thickness(0, MainWindow.M.AddDetailBtn.Margin.Top - 25, 0, 0);
        }
    }
}
