using System.Collections.Generic;
using System;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PropertyControl.xaml
    /// </summary>
    public partial class PropertyControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

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

        private readonly WorkControl work;
        public PropertyControl(WorkControl? _work = null)
        {
            InitializeComponent();
            work = _work;

            //work.PropertiesChanged += SaveOrLoadProperties; // подписка на сохранение и загрузку файла
            //work.type.Counted += PriceChanged;              // подписка на изменение количества типовых деталей
            //work.type.Priced += CreateSort;                 // подписка на изменение материала типовой детали
            //CreateSort();                  // при загрузке формируем словарь видов типовой детали по умолчанию
        }

        Dictionary<string, (string, string)> Dict = new();

        private void CreateSort()       // метод-прокладка между событием от типовой детали и формированием ее сорта
        {
            CreateSort(0);
        }
        public void CreateSort(int ndx)
        {
            if (work.type.TypeDetailDrop.SelectedItem is not TypeDetail type || type.Sort == null) return;

            Dict.Clear();

            string[] strings = type.Sort.Split(',');
            for (int i = 0; i < strings.Length; i += 3) Dict[strings[i]] = (strings[i + 1], strings[i + 2]);

            SortDrop.Items.Clear();
            foreach (string s in Dict.Keys) SortDrop.Items.Add(s);
            SortDrop.SelectedIndex = ndx;
            ChangeSort();
        }

        private void ChangeSort(object sender, SelectionChangedEventArgs e)
        {
            ChangeSort();
        }
        public void ChangeSort()
        {
            if (Dict.Count > 0 && SortDrop.SelectedIndex != -1)
            {
                A = MainWindow.Parser(Dict[$"{SortDrop.SelectedItem}"].Item1);
                B = MainWindow.Parser(Dict[$"{SortDrop.SelectedItem}"].Item2);
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
            //MassCalculate();
        }

        public delegate void Changed(PropertyControl prop);
        public event Changed? MassChanged;       // событие на изменение массы типовой детали
        public void MassCalculate()
        {
            if (work.type.MetalDrop.SelectedItem is not Metal metal) return;

            Mass = (float)Math.Round(A * B * S * L * metal.Density / 1000000, 2);

            MassChanged?.Invoke(this);

            PriceChanged();
        }

        public void PriceChanged()
        {
            Price = work.Result = work.type.HasMetal ? (float)Math.Round(work.type.Count * work.Price * Mass, 2) : 0;

            work.type.det.PriceResult();
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{SortDrop.SelectedIndex}");
                w.propsList.Add($"{S}");
                w.propsList.Add($"{L}");
            }
            else
            {
                CreateSort((int)MainWindow.Parser(w.propsList[0]));
                SetProperty("S_prop", w.propsList[1]);
                SetProperty("L_prop", w.propsList[2]);
            }
        }

    }
}
