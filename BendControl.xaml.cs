using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для BendControl.xaml
    /// </summary>
    public partial class BendControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private int bend;
        public int Bend
        {
            get => bend;
            set
            {
                if (value != bend)
                {
                    bend = value;
                    OnPropertyChanged(nameof(Bend));
                }
            }
        }

        public readonly UserControl owner;

        public BendControl(UserControl _control)
        {
            InitializeComponent();
            owner = _control;

            // формирование списка длин стороны гиба
            foreach (string s in BendDict[0.5f].Keys) ShelfDrop.Items.Add(s);

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
                                BendControl bend = new(part);
                                part.AddControl(bend);
                            }*/
                            CreateParts(cut.WindowParts.Parts);
                        }
                        PartBtn.IsEnabled = true;
                        break;
                    }
            }
            else PartBtn.IsEnabled = false;
        }

        public Dictionary<float, Dictionary<string, float>> BendDict = new()
        {
            [.5f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.45"] = 70
            },
            [.7f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.45"] = 70
            },
            [.8f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.45"] = 70
            },
            [1] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.45"] = 70
            },
            [1.2f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.45"] = 70
            },
            [1.5f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.45"] = 70
            },
            [2] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.45"] = 70
            },
            [2.5f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.45"] = 70
            },
            [3] = new Dictionary<string, float>
            {
                ["до 0.5"] = 20,
                ["0.5-1"] = 30,
                ["1-1.3"] = 35,
                ["1.3-2.45"] = 100
            },
            [4] = new Dictionary<string, float>
            {
                ["до 0.5"] = 20,
                ["0.5-1"] = 30,
                ["1-1.3"] = 35,
                ["1.3-2.45"] = 100
            },
            [5] = new Dictionary<string, float>
            {
                ["до 0.5"] = 30,
                ["0.5-1"] = 45,
                ["1-1.3"] = 65,
                ["1.3-2.45"] = 200
            },
            [6] = new Dictionary<string, float>
            {
                ["до 0.5"] = 45,
                ["0.5-1"] = 67.5f,
                ["1-1.3"] = 97.5f,
                ["1.3-2.45"] = 300
            },
            [8] = new Dictionary<string, float>
            {
                ["до 0.5"] = 45,
                ["0.5-1"] = 67.5f,
                ["1-1.3"] = 97.5f,
                ["1.3-2.45"] = 300
            },
            [10] = new Dictionary<string, float>
            {
                ["до 0.5"] = 45,
                ["0.5-1"] = 67.5f,
                ["1-1.3"] = 97.5f,
                ["1.3-2.45"] = 300
            }
        };

        private void SetBend(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetBend(tBox.Text);
        }
        private void SetBend(string _bend)
        {
            if (int.TryParse(_bend, out int b)) Bend = b;
            OnPriceChanged();
        }

        private void SetShelf(object sender, SelectionChangedEventArgs e)
        {
            SetShelf(ShelfDrop.SelectedIndex);
        }
        public void SetShelf(int ndx = 0)
        {
            ShelfDrop.SelectedIndex = ndx;
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
                    foreach (BendControl item in p.UserControls.OfType<BendControl>())
                    {
                        float _price = item.Price(item.Bend * p.Part.Count, work);

                        // стоимость данной гибки должна быть не ниже минимальной
                        if (work.WorkDrop.SelectedItem is Work _work) _price = _price > 0 && _price < _work.Price ? _work.Price : _price;

                        price += _price;
                    }
                work.SetResult(price, false);
            }
            else work.SetResult(Price(Bend * work.type.Count, work));
        }

        private float Price(float _count, WorkControl work)
        {
            float _bendRatio = _count switch
            {
                <= 10 => 3,
                <= 20 => 2,
                <= 50 => 1.5f,
                <= 200 => 1,
                _ => 0.8f,
            };

            return BendDict.ContainsKey(work.type.S) ?
                _bendRatio * _count * BendDict[work.type.S][$"{ShelfDrop.SelectedItem}"] : 0;
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (isSaved)
            {
                if (uc is WorkControl w)
                {
                    w.propsList.Clear();
                    w.propsList.Add($"{Bend}");
                    w.propsList.Add($"{ShelfDrop.SelectedIndex}");
                }
                else if (uc is PartControl p)       // первый элемент списка {0} - это (MenuItem)PartControl.Controls.Items[0]
                {
                    if (Bend == 0) return;

                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{0}", $"{Bend}", $"{ShelfDrop.SelectedIndex}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + Г")) p.Part.Description += " + Г";

                    MessageBox.Show($"{p.UserControls.IndexOf(this)}");

                    float _price = Price(Bend * p.Part.Count, p.Cut.work);
                    // стоимость данной гибки должна быть не ниже минимальной
                    if (p.Cut.work.WorkDrop.SelectedItem is Work _work) _price = _price > 0 && _price < _work.Price ? _work.Price : _price;
                    p.Part.Price += _price / p.Part.Count;
                }
            }
            else
            {
                if (uc is WorkControl w)
                {
                    SetBend(w.propsList[0]);
                    SetShelf((int)MainWindow.Parser(w.propsList[1]));
                }
                else if (uc is PartControl p)
                {
                    MessageBox.Show($"{p.UserControls.IndexOf(this)}");

                    SetBend(p.Part.PropsDict[p.UserControls.IndexOf(this)][1]);
                    SetShelf((int)MainWindow.Parser(p.Part.PropsDict[p.UserControls.IndexOf(this)][2]));
                }
            }
        }

        public List<PartControl> Parts = new();
        private void ViewPartWindow(object sender, RoutedEventArgs e)
        {
            if (owner is not WorkControl work || work.type.TypeDetailDrop.SelectedItem is not TypeDetail typeDetail || typeDetail.Name != "Лист металла") return;
            
            foreach (WorkControl w in work.type.WorkControls)
                if ( w != work && w.workType is CutControl cut && cut.WindowParts != null) cut.WindowParts.ShowDialog();
        }
        public void CreateParts(List<PartControl> parts) { Parts.AddRange(parts); }     // метод, необходимый для корректной загрузки расчета
    }
}
