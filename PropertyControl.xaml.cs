using System.Collections.Generic;
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
        public void CreateSort()
        {
            if (work.type.TypeDetailDrop.SelectedItem is TypeDetail type)
            {
                Dict.Clear();

                string[] strings = type.Sort.Split(',');
                for (int i = 0; i < strings.Length; i += 4) Dict[strings[i]] = (strings[i + 1], strings[i + 2], strings[i + 3]);

                SortDrop.Items.Clear();
                foreach (string s in Dict.Keys) SortDrop.Items.Add(s);
            }
            SortDrop.SelectedIndex = 0;
            ChangeSort();
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
            if (Dict.Count > 0 && SortDrop.Items.Count > 0)
            {
                A_prop.Text = Dict[$"{SortDrop.SelectedItem}"].Item1;
                B_prop.Text = Dict[$"{SortDrop.SelectedItem}"].Item2;
                S_prop.Text = Dict[$"{SortDrop.SelectedItem}"].Item3;
            }
            SetMass();
        }

        public void SetMass()
        {
            if (work.type.TypeDetailDrop.SelectedItem is not TypeDetail type) return;

            Mass = type.Name switch
            {
                "Швеллер" => (float)(MainWindow.Parser(A_prop.Text) * MainWindow.Parser(B_prop.Text) * 7.86 / 1000),
                "Квадрат" => (float)(MainWindow.Parser(A_prop.Text) * MainWindow.Parser(A_prop.Text) * 7.86 / 1000),
                _ => 1,
            };

            PriceChanged();
        }

        public void PriceChanged()
        {
            Price = work.Result = work.type.Count * work.type.det.Count * work.Price * Mass;

            work.type.det.PriceResult();
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{SortDrop.SelectedIndex}");
                MessageBox.Show(w.propsList[0]);
            }
            else
            {
                if (int.TryParse(w.propsList[0], out int ndx)) CreateSort(ndx);
                MessageBox.Show($"{SortDrop.SelectedIndex}");
                //ChangeSort();
            }
        }
    }
}
