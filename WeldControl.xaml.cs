using System.Windows.Controls;
using System.Windows;
using System.Data;
using System.Collections.Generic;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WeldControl.xaml
    /// </summary>
    public partial class WeldControl : UserControl
    {
        public float Price { get; set; }

        private readonly WorkControl work;
        public WeldControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;
            DataContext = this;
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

        private void SetWeld(string _weld)
        {
            float weld = 0;
            try
            {
                object result = new DataTable().Compute(_weld, null);
                if (float.TryParse(result.ToString(), out float f)) weld = f;
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("Исправьте длину свариваемой поверхности \nили поставьте 0", "Ошибка",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Hand);
                return;
            }

            float sideRatio;
            switch (weld * work.type.Count)
            {
                
                case < 300:
                    sideRatio = 3;
                    break;
                case < 1000:
                    sideRatio = 10;
                    break;
                case < 10000:
                    sideRatio = 100;
                    break;
                default:
                    sideRatio = 1;
                    break;
            }

            Price = weldDict["aisi430"][sideRatio] * weld * work.type.Count + work.Price;

            WeldPrice.Text = $"{Price}";
        }

        private void SetWeld(object sender, RoutedEventArgs e)
        {
            SetWeld(WeldText.Text);
        }
    }
}
