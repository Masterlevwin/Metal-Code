using System.Windows.Controls;
using System.Data;
using System.Collections.Generic;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WeldControl.xaml
    /// </summary>
    public partial class WeldControl : UserControl
    {
        //Text="{Binding Price, Mode=TwoWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:WeldControl}}}"
        public static readonly DependencyProperty MyPropertyPrice =
            DependencyProperty.Register("Price", typeof(float), typeof(WeldControl));
        public float Price
        {
            get { return (float)GetValue(MyPropertyPrice); }
            set { SetValue(MyPropertyPrice, value); }
        }

        //Text="{Binding Ratio, Mode=TwoWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:WeldControl}}}"
        public static readonly DependencyProperty MyPropertyRatio =
            DependencyProperty.Register("Ratio", typeof(string), typeof(WeldControl));
        public string Ratio
        {
            get { return (string)GetValue(MyPropertyRatio); }
            set { SetValue(MyPropertyRatio, value); }
        }

        //Text="{Binding Weld, Mode=TwoWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:WeldControl}}}"
        public static readonly DependencyProperty MyPropertyWeld =
            DependencyProperty.Register("Weld", typeof(string), typeof(WeldControl));
        public string Weld
        {
            get { return (string)GetValue(MyPropertyWeld); }
            set { SetValue(MyPropertyWeld, value); }
        }

        private readonly WorkControl work;
        public WeldControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;
            work.type.Counted += PriceChanged;
            work.type.Priced += PriceChanged;
            work.PropertiesChanged += SaveOrLoadProperties;
        }

        public Dictionary<string, Dictionary<float, float>> weldDict = new()
        {
            ["ст3"] = new Dictionary<float, float>()
            {
                [1] = 10,
                [3] = 8,
                [10] = 7,
                [100] = 5
            },
            ["09г2с"] = new Dictionary<float, float>()
            {
                [1] = 10,
                [3] = 8,
                [10] = 7,
                [100] = 5
            },
            ["хк"] = new Dictionary<float, float>()
            {
                [1] = 10,
                [3] = 8,
                [10] = 7,
                [100] = 5
            },
            ["цинк"] = new Dictionary<float, float>()
            {
                [1] = 15,
                [3] = 12,
                [10] = 11,
                [100] = 7
            },
            ["aisi430"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi430шлиф"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi430зерк"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi304"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi304шлиф"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi304зерк"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi321"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["амг2"] = new Dictionary<float, float>()
            {
                [1] = 30,
                [3] = 27,
                [10] = 25,
                [100] = 20
            },
            ["амг5"] = new Dictionary<float, float>()
            {
                [1] = 30,
                [3] = 27,
                [10] = 25,
                [100] = 20
            },
            ["амг6"] = new Dictionary<float, float>()
            {
                [1] = 30,
                [3] = 27,
                [10] = 25,
                [100] = 20
            },
            ["д16"] = new Dictionary<float, float>()
            {
                [1] = 30,
                [3] = 27,
                [10] = 25,
                [100] = 20
            }
        };

        private void SetRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRatio(tBox.Text);
        }
        private void SetRatio(string? _ratio)
        {
            Ratio = _ratio;
            PriceChanged();
        }

        private void SetWeld(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetWeld(tBox.Text);
        }
        private void SetWeld(string _weld)
        {
            Weld = _weld;
            PriceChanged();
        }

        private void PriceChanged()
        {  
            float _weld = 0;
            try
            {
                object result = new DataTable().Compute(Weld, null);
                if (float.TryParse(result.ToString(), out float f)) _weld = f;
            }
            catch
            {
                //System.Windows.Forms.MessageBox.Show("Исправьте длину свариваемой поверхности \nили поставьте 0", "Ошибка",
                    //System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Hand);
                return;
            }

            var sideRatio = (float)(_weld * work.type.Count) switch
            {
                < 300 => 3,
                < 1000 => 10,
                < 10000 => 100,
                _ => (float)1,
            };

            float _ratio = 1;
            if (float.TryParse(Ratio, out float r)) _ratio = r;

            if (weldDict.ContainsKey($"{work.type.MetalDrop.SelectedItem}"))
                Price = work.Result = weldDict[$"{work.type.MetalDrop.SelectedItem}"][sideRatio]
                    * _weld * _ratio * work.type.Count + work.Price;
            else
            {
                Price = work.Result = 0;
                //MessageBox.Show("Данная работа невозможна!");
            }

            work.type.det.PriceResult();
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add(Weld);
                w.propsList.Add(Ratio);
            }
            else
            {
                SetWeld(w.propsList[0]);
                SetRatio(w.propsList[1]);
            }
        }
    }
}
