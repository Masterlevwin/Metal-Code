using System.Collections.Generic;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PropertyControl.xaml
    /// </summary>
    public partial class PropertyControl : UserControl
    {
        //Text="{Binding Price, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:PropertyControl}}}"
        public static readonly DependencyProperty MyPropertyPrice =
            DependencyProperty.Register("Price", typeof(float), typeof(PropertyControl));
        public float Price
        {
            get { return (float)GetValue(MyPropertyPrice); }
            set { SetValue(MyPropertyPrice, value); }
        }

        //Text="{Binding Mass, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:PropertyControl}}}"
        public static readonly DependencyProperty MyPropertyMass =
            DependencyProperty.Register("Mass", typeof(float), typeof(PropertyControl));
        public float Mass
        {
            get { return (float)GetValue(MyPropertyMass); }
            set { SetValue(MyPropertyMass, value); }
        }

        private readonly WorkControl work;
        public PropertyControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;

            Loaded += CreateSort;                           // при загрузке формируем словарь видов типовой детали по умолчанию
            work.PropertiesChanged += SaveOrLoadProperties; // подписка на сохранение и загрузку файла
            work.type.Priced += CreateSort;                 // подписка на изменение материала типовой детали
            work.type.Counted += PriceChanged;              // подписка на изменение количества типовых деталей
        }

        Dictionary<string, (string, string)> Dict = new();

        private void CreateSort(object sender, RoutedEventArgs e)
        {
            CreateSort();
        }
        private void CreateSort()
        {
            CreateSort(SortDrop.SelectedIndex);
        }
        public void CreateSort(int ndx)
        {
            if (work.type.TypeDetailDrop.SelectedItem is TypeDetail type)
            {
                Dict.Clear();

                string[] strings = type.Sort.Split(',');
                for (int i = 0; i < strings.Length; i += 3) Dict[strings[i]] = (strings[i + 1], strings[i + 2]);

                SortDrop.Items.Clear();
                foreach (string s in Dict.Keys) SortDrop.Items.Add(s);
            }
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
                A_prop.Text = Dict[$"{SortDrop.SelectedItem}"].Item1;
                B_prop.Text = Dict[$"{SortDrop.SelectedItem}"].Item2;
            }
            MassCalculate();
        }


        public delegate void Changed(PropertyControl prop);
        public event Changed? MassChanged;       // событие на изменение массы типовой детали
        public void MassCalculate()
        {
            if (work.type.TypeDetailDrop.SelectedItem is not TypeDetail type ||
                !work.type.MetalDict.ContainsKey($"{work.type.MetalDrop.SelectedItem}")) return;

            if (MainWindow.Parser(S_prop.Text) <= 0) S_prop.Text = "1";
            if (MainWindow.Parser(L_prop.Text) <= 0) L_prop.Text = "1000";

            Mass = (float)Math.Round(
                MainWindow.Parser(A_prop.Text)
                * MainWindow.Parser(B_prop.Text)
                * MainWindow.Parser(S_prop.Text)
                * MainWindow.Parser(L_prop.Text)
                * work.type.MetalDict[$"{work.type.MetalDrop.SelectedItem}"] / 1000000, 2);
            
            MassChanged?.Invoke(this);

            PriceChanged();
        }

        public void PriceChanged()
        {
            Price = work.Result = (float)Math.Round(work.type.Count * work.Price * Mass, 2);

            work.type.det.PriceResult();
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{SortDrop.SelectedIndex}");
            }
            else
            {
                CreateSort((int)MainWindow.Parser(w.propsList[0]));
            }
        }
    }
}
