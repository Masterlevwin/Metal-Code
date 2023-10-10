using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using System;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для TypeDetailControl.xaml
    /// </summary>
    public partial class TypeDetailControl : UserControl
    {
        public int Count { get; set; }
        public string? NameTypeDetail { get; set; }

        public readonly Detail det;
        public List<WorkControl> WorkControls = new();

        public TypeDetailControl(Detail d)
        {
            InitializeComponent();
            det = d;
            det.Counted += CountChanged;
            TypeDetailDrop.ItemsSource = MainWindow.M.dbTypeDetails.TypeDetails.Local.ToObservableCollection();
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
            for (int i = 0; i < WorkControls.Count; i++) WorkControls[i]?.Remove();
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
        public event PriceChanged? Priced;
        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(CountText.Text, out int count)) Count = count;
            CountChanged();
        }
        public void CountChanged()
        {
            Priced?.Invoke();
        }

        private void PriceView(object sender, RoutedEventArgs e)
        {
            foreach (WorkControl w in WorkControls) w.PriceView();
        }
    }
}
