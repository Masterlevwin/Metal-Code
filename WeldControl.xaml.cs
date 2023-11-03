using System.Windows.Controls;
using System.Data;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WeldControl.xaml
    /// </summary>
    public partial class WeldControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float price;
        public float Price
        {
            get => price;
            set
            {
                price = value;
                OnPropertyChanged(nameof(Price));
            }
        }

        private float ratio;
        public float Ratio
        {
            get => ratio;
            set
            {
                if (value != ratio)
                {
                    ratio = value;
                    OnPropertyChanged(nameof(Ratio));
                }
            }
        }

        private string? weld;
        public string? Weld
        {
            get => weld;
            set
            {
                weld = value;
                OnPropertyChanged(nameof(Weld));
            }
        }

        private readonly WorkControl work;
        public WeldControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;

            work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
            work.type.Priced += PriceChanged;                   // подписка на изменение материала типовой детали
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
        private void SetRatio(string _ratio)
        {
            if (float.TryParse(_ratio, out float r)) Ratio = r;     // стандартный парсер избавляет от проблемы с запятой
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
                if (float.TryParse($"{result}", out float f)) _weld = f;
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

            if (work.type.MetalDrop.SelectedItem is Metal metal && weldDict.ContainsKey(metal.Name))
                Price = weldDict[metal.Name][sideRatio] * _weld * Ratio * work.type.Count;
            else Price = 0;

            work.SetResult(Price);
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{Weld}");
                w.propsList.Add($"{Ratio}");
            }
            else
            {
                SetWeld(w.propsList[0]);
                SetRatio(w.propsList[1]);
            }
        }

    }
}
