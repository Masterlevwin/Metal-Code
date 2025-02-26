using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Media;

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

        public List<PartControl>? Parts { get; set; }

        public Dictionary<float, Dictionary<string, float>> BendDict = new()
        {
            [.5f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.55"] = 70
            },
            [.7f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.55"] = 70
            },
            [.8f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.55"] = 70
            },
            [1] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.55"] = 70
            },
            [1.2f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.55"] = 70
            },
            [1.5f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.55"] = 70
            },
            [2] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.55"] = 70
            },
            [2.5f] = new Dictionary<string, float>
            {
                ["до 0.5"] = 15,
                ["0.5-1"] = 20,
                ["1-1.3"] = 25,
                ["1.3-2.55"] = 70
            },
            [3] = new Dictionary<string, float>
            {
                ["до 0.5"] = 20,
                ["0.5-1"] = 30,
                ["1-1.3"] = 35,
                ["1.3-2.55"] = 100
            },
            [4] = new Dictionary<string, float>
            {
                ["до 0.5"] = 20,
                ["0.5-1"] = 30,
                ["1-1.3"] = 35,
                ["1.3-2.55"] = 100
            },
            [5] = new Dictionary<string, float>
            {
                ["до 0.5"] = 30,
                ["0.5-1"] = 45,
                ["1-1.3"] = 65,
                ["1.3-2.55"] = 200
            },
            [6] = new Dictionary<string, float>
            {
                ["до 0.5"] = 45,
                ["0.5-1"] = 67.5f,
                ["1-1.3"] = 97.5f,
                ["1.3-2.55"] = 300
            },
            [8] = new Dictionary<string, float>
            {
                ["до 0.5"] = 45,
                ["0.5-1"] = 67.5f,
                ["1-1.3"] = 97.5f,
                ["1.3-2.55"] = 300
            },
            [10] = new Dictionary<string, float>
            {
                ["до 0.5"] = 45,
                ["0.5-1"] = 67.5f,
                ["1-1.3"] = 97.5f,
                ["1.3-2.55"] = 300
            },
            [12] = new Dictionary<string, float>
            {
                ["до 0.5"] = 45,
                ["0.5-1"] = 67.5f,
                ["1-1.3"] = 97.5f,
                ["1.3-2.55"] = 300
            },
            [14] = new Dictionary<string, float>
            {
                ["до 0.5"] = 45,
                ["0.5-1"] = 67.5f,
                ["1-1.3"] = 97.5f,
                ["1.3-2.55"] = 300
            },
            [16] = new Dictionary<string, float>
            {
                ["до 0.5"] = 45,
                ["0.5-1"] = 67.5f,
                ["1-1.3"] = 97.5f,
                ["1.3-2.55"] = 300
            }
        };

        public readonly UserControl owner;
        public BendControl(UserControl _control)
        {
            InitializeComponent();
            owner = _control;
            Tuning();
        }

        private void Tuning()               // настройка блока после инициализации
        {
            // формирование списка длин стороны гиба
            foreach (string s in BendDict[0.5f].Keys) ShelfDrop.Items.Add(s);

            if (owner is WorkControl work)
            {
                work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
                work.type.Priced += OnPriceChanged;                 // подписка на изменение материала типовой детали

                foreach (WorkControl w in work.type.WorkControls)
                    if (w.workType != this && w.workType is ICut _cut && _cut.PartsControl != null)
                    {
                        Parts = new(_cut.PartsControl.Parts);
                        break;
                    }
            }
            else if (owner is PartControl part)
            {
                part.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла

                foreach (WorkControl w in part.work.type.WorkControls)
                    if (w.workType is BendControl) return;

                part.work.type.AddWork();

                // добавляем "Гибку" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Гибка")
                    {
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
        }

        private void SetBend(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetBend(tBox.Text);
        }
        public void SetBend(string _bend)
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
            ValidateForce();
            OnPriceChanged();
        }

        public void OnPriceChanged()
        {
            if (owner is not WorkControl work || work.WorkDrop.SelectedItem is not Work _work) return;

            float price = 0;

            if (Parts != null && Parts.Count > 0)
            {
                foreach (PartControl p in Parts)
                    foreach (BendControl item in p.UserControls.OfType<BendControl>())
                    {
                        float _price = item.Price(item.Bend * p.Part.Count, work, p.Part.Mass, p.Square);

                        // стоимость данной гибки должна быть не ниже минимальной
                        _price = _price > 0 && _price < _work.Price ? _work.Price : _price;

                        price += _price * Difficult(p.UserControls.OfType<BendControl>().Count());
                    }

                // стоимость всей гибки должна быть не ниже 3-x минималок
                price = price > 0 && price < _work.Price * 3 ? _work.Price * 3 : price;

                work.SetResult(price, false);
            }
            else work.SetResult(Price(Bend * work.type.Count, work, work.type.Mass, work.type.Square));
        }

        private float Difficult(int bends)
        {
            return bends switch
            {
                <= 4 => 1,
                5 => 1.2f,
                6 => 1.4f,
                7 => 1.6f,
                8 => 1.8f,
                _ => 2
            };
        }

        private float Price(float _count, WorkControl work, float _mass, float _square)
        {
            float _squareRatio = _square switch             //рассчитываем наценку за площадь детали
            {
                <= 0.8f => 1,
                <= 1.4f => 1.5f,
                _ => 2,
            };

            float _bendRatio = _count switch
            {
                <= 10 => 3,
                <= 20 => 2,
                <= 50 => 1.5f,
                <= 200 => 1,
                _ => 0.8f,
            };

            return BendDict.ContainsKey(work.type.S) ?
                _bendRatio * _count * BendDict[work.type.S][$"{ShelfDrop.SelectedItem}"] * MainWindow.MassRatio(_mass) * _squareRatio : 0;
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
                else if (uc is PartControl p)
                {
                    if (Bend == 0)
                    {
                        p.RemoveControl(this);
                        return;
                    }

                    ValidateShelf();

                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{0}", $"{Bend}", $"{ShelfDrop.SelectedIndex}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + Г ")) p.Part.Description += " + Г ";

                    int count = 0;      //счетчик общего количества деталей

                    if (p.owner is ICut _cut && _cut.PartsControl != null) foreach (PartControl _p in _cut.PartsControl.Parts)
                            foreach (BendControl item in _p.UserControls.OfType<BendControl>())
                                if (item.Bend > 0) count += _p.Part.Count;

                    // стоимость всей работы должна быть не ниже минимальной
                    foreach (WorkControl _w in p.work.type.WorkControls)                // находим гибку среди работ и получаем её минималку
                        if (_w.workType is BendControl && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            float _send;
                            if (_w.Result > 0 && _w.Result <= _work.Price * 3 * _w.Ratio * _w.TechRatio)    // если стоимость работы ниже 3-x минималок, к цене детали добавляем
                                _send = _work.Price * 3 * _w.Ratio * _w.TechRatio / count;  // усредненную часть минималки от общего количества деталей
                            else                                                            // иначе добавляем часть от количества именно этой детали
                            {
                                float _price = Price(Bend * p.Part.Count, p.work, p.Part.Mass, p.Square);
                                
                                // стоимость данной гибки должна быть не ниже минимальной
                                _price = _price > 0 && _price < _work.Price ?
                                    _work.Price * Difficult(p.UserControls.OfType<BendControl>().Count()) * _w.Ratio * _w.TechRatio
                                    : _price * Difficult(p.UserControls.OfType<BendControl>().Count()) * _w.Ratio * _w.TechRatio;

                                _send = _price / p.Part.Count;
                            }                                                       

                            p.Part.Price += _send;

                            if (p.Part.PropsDict.ContainsKey(52) && float.TryParse(p.Part.PropsDict[52][0], out float value))
                                p.Part.PropsDict[52].Insert(0, $"{value +_send}");  //суммируем гибку, но пока не удаляю предыдущие значения (возможно пригодятся)
                            else p.Part.PropsDict[52] = new() { $"{_send}" };

                            break;
                        }
                }
            }
            else
            {
                if (uc is WorkControl w)
                {
                    SetBend(w.propsList[0]);
                    SetShelf((int)MainWindow.Parser(w.propsList[1]));
                }
                else if (uc is PartControl p && owner is PartControl _owner)
                {
                    SetBend(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][1]);
                    SetShelf((int)MainWindow.Parser(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][2]));
                }
            }
        }

        //матрица гибов
        public Dictionary<float, Dictionary<int, int>> MatrixDict = new()
        {
            [0.7f] = new Dictionary<int, int>
            {
                [6] = 3,
                [8] = 2,
            },
            [0.8f] = new Dictionary<int, int>
            {
                [6] = 7,
                [8] = 5,
                [10] = 4,
            },
            [1] = new Dictionary<int, int>
            {
                [6] = 11,
                [8] = 8,
                [10] = 6,
                [13] = 5
            },
            [1.2f] = new Dictionary<int, int>
            {
                [6] = 18,
                [8] = 12,
                [10] = 9,
                [13] = 7,
                [16] = 5
            },
            [1.5f] = new Dictionary<int, int>
            {
                [8] = 21,
                [10] = 15,
                [13] = 12,
                [16] = 8
            },
            [2] = new Dictionary<int, int>
            {
                [10] = 30,
                [13] = 23,
                [16] = 16,
                [22] = 9
            },
            [2.5f] = new Dictionary<int, int>
            {
                [13] = 39,
                [16] = 27,
                [22] = 14,
                [35] = 11
            },
            [3] = new Dictionary<int, int>
            {
                [16] = 43,
                [22] = 23,
                [35] = 16
            },
            [4] = new Dictionary<int, int>
            {
                [22] = 44,
                [35] = 32,
                [50] = 18
            },
            [5] = new Dictionary<int, int>
            {
                [22] = 76,
                [35] = 54,
                [50] = 29
            },
            [6] = new Dictionary<int, int>
            {
                [35] = 85,
                [50] = 45
            },
            [8] = new Dictionary<int, int>
            {
                [50] = 88
            },
            [10] = new Dictionary<int, int>
            {
                [50] = 151
            },
        };

        private void ValidateForce()        //метод, в котором проверяется возможность гибки
        {
            if (owner is not PartControl p || !MatrixDict.ContainsKey(p.work.type.S)) return;

            Regex shelf = new(@"((\d+\.?\d*)|(\.\d+))");

            List<Match> matches = shelf.Matches($"{ShelfDrop.SelectedItem}").ToList();

            if (matches.Count > 0)
            {
                float _shelf = MainWindow.Parser($"{matches[^1]}");             //максимальная длина гиба выбранного диапазона

                int force = MatrixDict[p.work.type.S][MatrixDict[p.work.type.S].Keys.ToArray()[0]];     //максимальное усилие гибочного станка

                if (_shelf * force >= 100)                                      //100 тонн - максимальное давление станка
                {
                    ShelfDrop.BorderBrush = new SolidColorBrush(Colors.Red);
                    ShelfDrop.BorderThickness = new Thickness(2);
                    MainWindow.M.StatusBegin($"Возможно мы не согнём некоторые детали. Уточните возможность гибки у специалиста!");
                }
                else
                {
                    ShelfDrop.BorderBrush = new SolidColorBrush(Colors.Gray);
                    ShelfDrop.BorderThickness = new Thickness(1);
                }
            }
        }

        private void ValidateShelf()        //метод, в котором проверяется выбранная длина гиба
        {
            if (owner is not PartControl p || !MatrixDict.ContainsKey(p.work.type.S)) return;

            Regex shelf = new(@"((\d+\.?\d*)|(\.\d+))");

            List<Match> matches = shelf.Matches($"{ShelfDrop.SelectedItem}").ToList();

            if (matches.Count > 0)
            {
                //максимальная длина гиба выбранного диапазона
                float _shelf = MainWindow.Parser($"{matches[^1]}") * 1000;     

                float _height = MainWindow.Parser($"{p.Part.PropsDict[100][0]}");
                float _width = MainWindow.Parser($"{p.Part.PropsDict[100][1]}");

                if (_shelf < _height || _shelf < _width)    //если выбранная максимальная длина гиба меньше любой из сторон детали
                {                                           //выдаем предупреждение
                    ShelfDrop.BorderBrush = new SolidColorBrush(Colors.Red);
                    ShelfDrop.BorderThickness = new Thickness(2);
                    if (MainWindow.M.Log is null || !MainWindow.M.Log.Contains("Проверьте выбранные стороны гиба у деталей!"))
                        MainWindow.M.Log += "\nПроверьте выбранные стороны гиба у деталей!\n";
                }
                else
                {
                    ShelfDrop.BorderBrush = new SolidColorBrush(Colors.Gray);
                    ShelfDrop.BorderThickness = new Thickness(1);
                }
            }
        }

        private void Remove(object sender, RoutedEventArgs e) { if (owner is PartControl part) part.RemoveControl(this); }
    }
}
