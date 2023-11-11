using System.Windows.Controls;
using System.Data;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Linq;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WeldControl.xaml
    /// </summary>
    public partial class WeldControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

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

        private readonly UserControl owner;
        public WeldControl(UserControl _control)
        {
            InitializeComponent();
            owner = _control;

            // формирование списка типов расчета сварки
            foreach (string s in TypeDict.Keys) TypeDrop.Items.Add(s);

            if (owner is WorkControl work)
            {
                work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
                work.type.Priced += OnPriceChanged;                 // подписка на изменение материала типовой детали
                BtnEnable();       // проверяем типовую деталь: если не "Лист металла", делаем кнопку неактивной и наоборот
            }
            else if (owner is PartControl part)
            {
                part.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
                PartBtn.IsEnabled = false;
            }
        }

        private void BtnEnable()
        {
            if (owner is WorkControl work && work.type.TypeDetailDrop.SelectedItem is TypeDetail typeDetail && typeDetail.Name == "Лист металла")
            {
                foreach (WorkControl w in work.type.WorkControls)
                    if (w != owner && w.workType is CutControl cut && cut.WindowParts != null)
                    {
                        if (Parts.Count == 0)
                        {
                            /*foreach (PartControl part in cut.WindowParts.Parts)
                            {
                                WeldControl weld = new(part);
                                part.AddControl(weld);
                            }*/
                            CreateParts(cut.WindowParts.Parts);
                        }
                        PartBtn.IsEnabled = true;
                        break;
                    }   
            }
            else PartBtn.IsEnabled = false;
        }

        public Dictionary<string, Dictionary<float, float>> WeldDict = new()
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

        private void SetWeld(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetWeld(tBox.Text);
        }
        private void SetWeld(string _weld)
        {
            Weld = _weld;
            OnPriceChanged();
        }

        private Dictionary<string, float> TypeDict = new()
        {
            ["дву"] = 1.7f,
            ["одн"] = 1,
        };
        private void SetType(object sender, SelectionChangedEventArgs e)
        {
            SetType(TypeDrop.SelectedIndex);
        }
        public void SetType(int ndx = 0)
        {
            TypeDrop.SelectedIndex = ndx;
            OnPriceChanged();
        }

        public void OnPriceChanged()
        {
            if (owner is not WorkControl work) return;

            BtnEnable();
            float price = 0;

            if (Parts.Count > 0)
            {
                foreach (PartControl p in Parts)
                    foreach (WeldControl item in p.UserControls.OfType<WeldControl>())
                        price += item.Price(ParserWeld(item.Weld) * p.Part.Count, work);
                work.SetResult(price);
            }
            else work.SetResult(Price(ParserWeld(Weld) * work.type.Count, work));

        }

        private float ParserWeld(string _weld)
        {
            try
            {
                object result = new DataTable().Compute(_weld, null);
                if (float.TryParse($"{result}", out float f)) return f;
            }
            catch
            {
                //System.Windows.Forms.MessageBox.Show("Исправьте длину свариваемой поверхности \nили поставьте 0", "Ошибка",
                //System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Hand);
            }
            return 0;
        }

        private float Price(float _count, WorkControl work)
        {
            var sideRatio = _count switch
            {
                < 100 => 1,
                < 300 => 3,
                < 1000 => 10,
                _ => 100,
            };

            return work.type.MetalDrop.SelectedItem is Metal metal && WeldDict.ContainsKey(metal.Name) ?
                WeldDict[metal.Name][sideRatio] * _count * TypeDict[$"{TypeDrop.SelectedItem}"] : 0;
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (isSaved)
            {
                if (uc is WorkControl w)
                {
                    w.propsList.Clear();
                    w.propsList.Add($"{Weld}");
                    w.propsList.Add($"{TypeDrop.SelectedIndex}");
                }
                else if (uc is PartControl p)       // первый элемент списка {1} - это (MenuItem)PartControl.Controls.Items[1]
                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{1}", $"{Weld}", $"{TypeDrop.SelectedIndex}" };   
            }
            else
            {
                if (uc is WorkControl w)
                {
                    SetWeld(w.propsList[0]);
                    SetType((int)MainWindow.Parser(w.propsList[1]));
                }
                else if (uc is PartControl p)
                {
                    SetWeld(p.Part.PropsDict[p.UserControls.IndexOf(this)][1]);
                    SetType((int)MainWindow.Parser(p.Part.PropsDict[p.UserControls.IndexOf(this)][2]));
                }
            }
        }

        public List<PartControl> Parts = new();
        private void ViewPartWindow(object sender, RoutedEventArgs e)
        {
            if (owner is not WorkControl work || work.type.TypeDetailDrop.SelectedItem is not TypeDetail typeDetail || typeDetail.Name != "Лист металла") return;

            foreach (WorkControl w in work.type.WorkControls)
                if (w != work && w.workType is CutControl cut && cut.WindowParts != null) cut.WindowParts.ShowDialog();
        }

        public void CreateParts(List<PartControl> parts) { Parts.AddRange(parts); }     // метод, необходимый для корректной загрузки расчета
    }
}
