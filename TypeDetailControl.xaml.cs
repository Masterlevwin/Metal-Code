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
                A_prop.IsEnabled = B_prop.IsEnabled = true;
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
                S = L = 1;
                A_prop.IsEnabled = B_prop.IsEnabled = false;
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

        private List<(float, float)> Corners = new()
        {
            (3.5f, 1.2f), (3.5f, 1.2f), (4, 1.3f), (4, 1.3f), (5, 1.7f), (5.5f, 1.8f), (6, 2), (7, 2.3f),
            (7.5f, 2.5f), (7.5f, 2.5f), (8, 2.7f), (8, 2.7f), (9, 3), (9, 3), (10, 3.3f), (12, 4), (12, 4),
            (14, 4.6f), (14, 4.6f), (16, 5.3f)
        };

        private List<float> Channels = new()
        {
            4.84f, 5.9f, 7.05f, 8.59f, 10.4f, 12.3f, 14.2f, 15.3f, 16.3f, 17.4f, 18.4f, 21, 24, 27.7f, 31.8f, 36.5f, 41.9f, 48.3f
        };

        private void MassCalculate(object sender, SelectionChangedEventArgs e)
        {
            MassCalculate();
        }
        public void MassCalculate()
        {
            if (TypeDetailDrop.SelectedItem is not TypeDetail type || MetalDrop.SelectedItem is not Metal metal) return;

            if (type.Name == "Лист металла") Price = metal.MassPrice;
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
            else
            {
                switch (type.Name)
                {
                    case "Труба профильная":
                        Mass = 0.0157f * S * (A + B - 2.86f * S) * L * metal.Density / 7850;
                        break;
                    case "Труба круглая":
                        Mass = (float)Math.PI * S * (A - S) * L * metal.Density / 1000;
                        break;
                    case "Труба круглая ВГП":
                        Mass = (float)Math.PI * S * (A - S) * L * metal.Density / 1000;
                        break;
                    case "Уголок равнополочный":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
                            Mass = (S * (A + B - S) + 0.2146f * (Corners[SortDrop.SelectedIndex].Item1 * Corners[SortDrop.SelectedIndex].Item1
                                - 2 * Corners[SortDrop.SelectedIndex].Item2 * Corners[SortDrop.SelectedIndex].Item2)) * metal.Density / 1000;
                        break;
                    case "Уголок неравнополочный":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
                            Mass = (S * (A + B - S) + 0.2146f * (Corners[SortDrop.SelectedIndex].Item1 * Corners[SortDrop.SelectedIndex].Item1
                                - 2 * Corners[SortDrop.SelectedIndex].Item2 * Corners[SortDrop.SelectedIndex].Item2)) * metal.Density / 1000;
                        break;
                    case "Круг":
                        Mass = (float)Math.PI * A * A / 4 * metal.Density / 1000;
                        break;
                    case "Квадрат":
                        Mass = A * A * metal.Density / 1000000;
                        break;
                    case "Швеллер":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1) Mass = Channels[SortDrop.SelectedIndex];
                        break;
                    case "Полиуретан":
                        Mass = A * B * S * 1.2f / 1000000;
                        break;
                    case "Фторопласт":
                        Mass = A * B * S * 1.2f / 1000000;
                        break;
                    case "Капролон":
                        Mass = A * B * S * 1.2f / 1000000;
                        break;
                    case "ЛДСП":
                        Mass = 680 * S / 1000;      //680*F56*0,001 плюс дичь в формировании цены! сомнительная позиция...
                        break;
                    default:
                        Mass = A * B * S * L * metal.Density / 1000000;
                        break;
                }
            }

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

        private void AddComment(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("В разработке");
        }
    }
}
