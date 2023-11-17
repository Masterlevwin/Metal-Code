using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;

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

        private float a;
        public float A
        {
            get => a;
            set
            {
                if (value != a)
                {
                    a = value;
                    OnPropertyChanged(nameof(A));
                }
            }
        }

        private float b;
        public float B
        {
            get => b;
            set
            {
                if (value != b)
                {
                    b = value;
                    OnPropertyChanged(nameof(B));
                }
            }
        }

        private float s;
        public float S
        {
            get => s;
            set
            {
                if (value != s)
                {
                    s = value;
                    OnPropertyChanged(nameof(S));
                }
            }
        }

        private float l;
        public float L
        {
            get => l;
            set
            {
                if (value != l)
                {
                    l = value;
                    OnPropertyChanged(nameof(L));
                }
            }
        }

        private float mass;
        public float Mass
        {
            get { return mass; }
            set
            {
                mass = value;
                OnPropertyChanged(nameof(Mass));
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

        private float price;
        public float Price
        {
            get { return price; }
            set
            {
                price = value;
                OnPropertyChanged(nameof(Price));
            }
        }

        private float result;
        public float Result
        {
            get { return result; }
            set
            {
                result = value;
                OnPropertyChanged(nameof(Result));
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
            CreateSort();
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

            Grid.SetColumn(work, 1);

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


        public delegate void Changed();
        public event Changed? Priced;
        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int count)) SetCount(count);
        }
        public void SetCount(int _count)
        {
            Count = _count;
            PriceChanged();
        }

        Dictionary<string, (string, string)> Kinds = new();
        private void CreateSort(object sender, SelectionChangedEventArgs e)
        {
            CreateSort();
        }
        public void CreateSort(int ndx = 0)
        {
            Kinds.Clear();
            SortDrop.Items.Clear();
            if (TypeDetailDrop.SelectedItem is not TypeDetail type || type.Sort == null)
            {
                SortDrop.SelectedIndex = -1;
                A = B = S = L = 0;
                return;
            }

            string[] strings = type.Sort.Split(',');
            for (int i = 0; i < strings.Length; i += 3) Kinds[strings[i]] = (strings[i + 1], strings[i + 2]);

            foreach (string s in Kinds.Keys) SortDrop.Items.Add(s);
            SortDrop.SelectedIndex = ndx;
            ChangeSort();
        }

        private void ChangeSort(object sender, SelectionChangedEventArgs e)
        {
            ChangeSort();
        }
        public void ChangeSort()
        {
            if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
            {
                A = MainWindow.Parser(Kinds[$"{SortDrop.SelectedItem}"].Item1);
                B = MainWindow.Parser(Kinds[$"{SortDrop.SelectedItem}"].Item2);
            }
            MassCalculate();
        }

        private void SetProperty(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetProperty(tBox.Name, tBox.Text);
        }
        public void SetProperty(string _prop, string _value)
        {
            switch (_prop)
            {
                case "A_prop": if (float.TryParse(_value, out float a)) A = a; break;
                case "B_prop": if (float.TryParse(_value, out float b)) B = b; break;
                case "S_prop": if (float.TryParse(_value, out float s)) S = s; break;
                case "L_prop": if (float.TryParse(_value, out float l)) L = l; break;
            }
            MassCalculate();
        }

        private void MassCalculate(object sender, SelectionChangedEventArgs e)
        {
            MassCalculate();
        }
        public void MassCalculate()
        {
            if (TypeDetailDrop.SelectedItem is not TypeDetail type || MetalDrop.SelectedItem is not Metal metal) return;

            if (type.Name == "Лист металла") Price = metal.MassPrice;       // || (type.Name != null && type.Name.Contains("Труба"))
            else Price = type.Price;

            if (MainWindow.M.IsLaser)
            {
                foreach (WorkControl w in WorkControls)
                    if (w.workType is CutControl cut)
                    {
                        Mass = cut.Mass;
                        break;
                    }
            } 
            else Mass = (float)Math.Round(A * B * S * L * metal.Density / 1000000, 2);

            if (type.Name != null && type.Name.Contains("Труба")) Mass = metal.Density / 7850 * 0.0157f * S * (A + B - 2.86f * S) * L; 

            PriceChanged();
        }

        private void HasMetalChanged(object sender, RoutedEventArgs e)
        {
            PriceChanged();
        }

        public void PriceChanged()
        {
            Result = HasMetal ? (float)Math.Round((MainWindow.M.IsLaser ? 1 : Count) * Price * Mass, 2) : 0;

            Priced?.Invoke();
        }
    }
}
