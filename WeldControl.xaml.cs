﻿using System.Windows.Controls;
using System.Data;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Linq;
using SQLitePCL;
using System;

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

        public List<PartControl>? Parts { get; set; }

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
        public Dictionary<string, float> TypeDict = new()
        {
            ["одн"] = 1,
            ["дву"] = 1.7f,
        };

        private readonly UserControl owner;
        public WeldControl(UserControl _control)
        {
            InitializeComponent();
            owner = _control;
            Tuning();
        }

        private void Tuning()               // настройка блока после инициализации
        {
            // формирование списка типов расчета сварки
            foreach (string s in TypeDict.Keys) TypeDrop.Items.Add(s);

            if (owner is WorkControl work)
            {
                work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
                work.type.Priced += OnPriceChanged;                 // подписка на изменение материала типовой детали

                foreach (WorkControl w in work.type.WorkControls)
                    if (w.workType != this && w.workType is CutControl cut && cut.PartsControl != null)
                    {
                        Parts = new(cut.PartsControl.Parts);
                        break;
                    }
            }
            else if (owner is PartControl part)
            {
                part.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
                                
                //PartBtn.Visibility = Visibility.Visible;
                //PartBtn.Click += (o, e) => { part.RemoveControl(this); };

                foreach (WorkControl w in part.Cut.work.type.WorkControls)
                    if (w.workType is WeldControl) return;

                part.Cut.work.type.AddWork();

                // добавляем "Сварку" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Сварка")
                    {
                        part.Cut.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
        }

        private void SetWeld(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetWeld(tBox.Text);
        }
        public void SetWeld(string _weld)
        {
            Weld = _weld;
            OnPriceChanged();
        }

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

            float price = 0;

            if (Parts != null && Parts.Count > 0)
            {
                foreach (PartControl p in Parts)
                    foreach (WeldControl item in p.UserControls.OfType<WeldControl>())
                        if (item.Weld != null) price += item.Price(ParserWeld(item.Weld) * p.Part.Count, work);

                // стоимость данной сварки должна быть не ниже минимальной
                if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

                work.SetResult(price, false);
            }
            else if (Weld != null && Weld != "" && work.type.Mass > 0)
            {
                if (HasMinPrice()) work.SetResult(Price(ParserWeld(Weld) * work.type.Count, work), false);
                else work.SetResult(Price(ParserWeld(Weld) * work.type.Count, work));               
            }
        }

        private bool HasMinPrice()
        {
            foreach (DetailControl det in MainWindow.M.DetailControls)
                foreach (TypeDetailControl t in det.TypeDetailControls)
                    foreach (WorkControl w in t.WorkControls)
                        if (w != owner && w.workType is WeldControl && w.WorkDrop.SelectedItem is Work work && w.Result >= work.Price) return true;
            return false;
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
                MainWindow.M.StatusBegin("В поле длины свариваемой поверхности должно быть число или математическое выражение");
            }
            return 0;
        }

        private float Price(float _count, WorkControl work)
        {
            var sideRatio = _count switch
            {
                < 1000 => 1,
                < 3000 => 3,
                < 10000 => 10,
                _ => 100,
            };

            return work.type.MetalDrop.SelectedItem is Metal metal && metal.Name != null && WeldDict.ContainsKey(metal.Name) ?
                WeldDict[metal.Name][sideRatio] * _count / 10 * TypeDict[$"{TypeDrop.SelectedItem}"] : 0;
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
                else if (uc is PartControl p)
                {
                    if (Weld == null || Weld == "")
                    {
                        p.RemoveControl(this);
                        return;
                    }

                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{1}", $"{Weld}", $"{TypeDrop.SelectedIndex}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + Св ")) p.Part.Description += " + Св ";

                    int count = 0;      //счетчик общего количества деталей

                    if (p.Cut.PartsControl != null) foreach (PartControl _p in p.Cut.PartsControl.Parts)
                            foreach (WeldControl item in _p.UserControls.OfType<WeldControl>())
                                if (item.Weld != null) count += _p.Part.Count;

                    // стоимость всей работы должна быть не ниже минимальной
                    foreach (WorkControl _w in p.Cut.work.type.WorkControls)            // находим сварку среди работ и получаем её минималку
                        if (_w.workType is WeldControl && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            float _send;
                            if (_w.Result > 0 && _w.Result <= _work.Price)              // если стоимость работы ниже минимальной, к цене детали добавляем
                                _send = _work.Price * _w.Ratio * _w.TechRatio / count;  // усредненную часть минималки от общего количества деталей
                            else                                                        // иначе добавляем часть от количества именно этой детали
                                _send = Price(ParserWeld(Weld) * p.Part.Count, p.Cut.work) * _w.Ratio * _w.TechRatio / p.Part.Count;

                            p.Part.Price += _send;
                            p.Part.PropsDict[53] = new() { $"{_send}" };

                            break;
                        }
                }
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
    }
}
