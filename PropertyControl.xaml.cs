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
            work.type.Priced += CreateSort;
            work.type.Counted += PriceChanged;
            work.PropertiesChanged += SaveOrLoadProperties;
            Loaded += CreateSort;
        }

        Dictionary<string, (string, string, string)> Dict = new();

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
                for (int i = 0; i < strings.Length; i += 4) Dict[strings[i]] = (strings[i + 1], strings[i + 2], strings[i + 3]);

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
                S_prop.Text = Dict[$"{SortDrop.SelectedItem}"].Item3;
                if (MainWindow.Parser(S_prop.Text) <= 0) S_prop.Text = "1";
            }
            SetMass();
        }

        public void SetMass()
        {
            if (work.type.TypeDetailDrop.SelectedItem is not TypeDetail type ||
                !work.type.MetalDict.ContainsKey($"{work.type.MetalDrop.SelectedItem}")) return;

            Mass = type.Name switch
            {
                "Швеллер" => (float)(MainWindow.Parser(A_prop.Text) * MainWindow.Parser(B_prop.Text)
                    * work.type.MetalDict[$"{work.type.MetalDrop.SelectedItem}"] / 1000),
                "Квадрат" => (float)(MainWindow.Parser(A_prop.Text) * MainWindow.Parser(A_prop.Text)
                    * work.type.MetalDict[$"{work.type.MetalDrop.SelectedItem}"] / 1000),
                _ => 1,
            };

            Mass = (float)Math.Round(Mass, 2);
            PriceChanged();
        }

        public void PriceChanged()
        {
            Price = work.Result = work.type.Count * work.Price * Mass;

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
