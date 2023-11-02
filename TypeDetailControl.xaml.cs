using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для TypeDetailControl.xaml
    /// </summary>
    public partial class TypeDetailControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private int count;
        public int Count
        {
            get => count;
            set
            {
                if (value != count)
                {
                    count = value;
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        private bool hasMetal;
        public bool HasMetal
        {
            get => hasMetal;
            set
            {
                if (value != hasMetal)
                {
                    hasMetal = value;
                    OnPropertyChanged(nameof(HasMetal));
                }
            }
        }

        public readonly DetailControl det;
        public List<WorkControl> WorkControls = new();

        public TypeDetailControl(DetailControl d)
        {
            InitializeComponent();
            det = d;
            TypeDetailDrop.ItemsSource = MainWindow.M.dbTypeDetails.TypeDetails.Local.ToObservableCollection();
            MetalDrop.ItemsSource = MainWindow.M.dbMetals.Metals.Local.ToObservableCollection();
            HasMetal = true;
            AddProperty();
        }

        private void AddTypeDetail(object sender, RoutedEventArgs e)
        {
            det.AddTypeDetail();
        }

        private void AddProperty()
        {
            PropertyControl prop = new();
            TypeDetailGrid.Children.Add(prop);
            Grid.SetColumn(prop, 2);
        }

        public void AddWork()
        {
            WorkControl work = new(this);

            if (WorkControls.Count > 0) work.Margin = new Thickness(0, WorkControls[^1].Margin.Top + 25, 0, 0);

            WorkControls.Add(work);
            TypeDetailGrid.Children.Add(work);

            Grid.SetColumn(work, 3);

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
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int count)) SetCount(count);
        }
        public void SetCount(int _count)
        {
            Count = _count;
            Counted?.Invoke();
        }

        private void CheckMetal(object sender, RoutedEventArgs e)
        {
            Counted?.Invoke();
        }

        public void UpdateTotal()
        {
            Counted?.Invoke();      // Priced? - нельзя, так как обновит вид типовой детали
        }

        private void PriceView(object sender, SelectionChangedEventArgs e)
        {
            Priced?.Invoke();
        }
    }
}
