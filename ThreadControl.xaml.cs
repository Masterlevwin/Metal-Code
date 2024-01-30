using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для ThreadControl.xaml
    /// </summary>
    public partial class ThreadControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private int holes;
        public int Holes
        {
            get => holes;
            set
            {
                if (value != holes)
                {
                    holes = value;
                    OnPropertyChanged(nameof(Holes));
                }
            }
        }

        private float wide;
        public float Wide
        {
            get => wide;
            set
            {
                if (value != wide)
                {
                    wide = value;
                    OnPropertyChanged(nameof(Wide));
                }
            }
        }

        public List<PartControl>? Parts { get; set; }

        public readonly UserControl owner;
        public ThreadControl(UserControl _control)
        {
            InitializeComponent();
            owner = _control;
            Tuning();
        }

        private void Tuning()               // настройка блока после инициализации
        {
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
                    if (w.workType is ThreadControl) return;

                part.Cut.work.type.AddWork();

                // добавляем "Резьбу" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Резьба")
                    {
                        part.Cut.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
            }

            string dict = string.Empty;
            for (int i = 0; i < MainWindow.M.Destinies.Count; i++)
            {
                if (MainWindow.M.Destinies[i] <= 3) WideDict[MainWindow.M.Destinies[i]] = 1;
                else WideDict[MainWindow.M.Destinies[i]] = 0.1f * (i + 2);
            }

            for (int j = 0; j < MainWindow.M.Metals.Count; j++)
            {
                if (j < 3) MetalRatioDict[MainWindow.M.Metals[j]] = 1;
                else if (j < 11) MetalRatioDict[MainWindow.M.Metals[j]] = 1.5f;
                else MetalRatioDict[MainWindow.M.Metals[j]] = 3;
                dict += $"{MainWindow.M.Metals[j]}-{MetalRatioDict[MainWindow.M.Metals[j]]}\n";
            }
            MessageBox.Show(dict);
        }

        private void SetWide(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetWide(tBox.Text);
        }
        private void SetWide(string _wide)
        {
            if (int.TryParse(_wide, out int d)) Wide = d;
            OnPriceChanged();
        }

        private void SetHoles(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetHoles(tBox.Text);
        }
        public void SetHoles(string _holes)
        {
            if (int.TryParse(_holes, out int h)) Holes = h;
            OnPriceChanged();
        }

        private int minute = 4;     // минимальное время изготовления одного изделия, мин
        //public void OnPriceChanged()
        //{
        //    if (owner is not WorkControl work) return;

        //    if (work.WorkDrop.SelectedItem is Work _work)
        //    {
        //        if (Parts != null && Parts.Count > 0)
        //        {
        //            int _count = 0;
        //            foreach (PartControl p in Parts)
        //                foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
        //                    _count += item.Holes * p.Part.Count;

        //            float price = (float)Math.Ceiling(_count * _work.Price / 12);

        //            // стоимость данной резьбовки должна быть не ниже минимальной
        //            price = price > 0 && price < _work.Price ? _work.Price : price;

        //            work.SetResult(price, false);
        //        }
        //        else work.SetResult((float)Math.Ceiling(Holes * work.type.Count * _work.Price / 12));
        //    }
        //}

        //public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        //{
        //    if (isSaved)
        //    {
        //        if (uc is WorkControl w)
        //        {
        //            w.propsList.Clear();
        //            w.propsList.Add($"{Holes}");
        //        }
        //        else if (uc is PartControl p)       // первый элемент списка {3} - это (MenuItem)PartControl.Controls.Items[3]
        //        {
        //            if (Holes == 0)
        //            {
        //                p.RemoveControl(this);
        //                return;
        //            }

        //            p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{3}", $"{Holes}" };
        //            if (p.Part.Description != null && !p.Part.Description.Contains(" + Р ")) p.Part.Description += " + Р ";                   

        //            int _count = 0;
        //            if (p.Cut.PartsControl != null)
        //                foreach (PartControl _p in p.Cut.PartsControl.Parts)
        //                    foreach (ThreadControl item in _p.UserControls.OfType<ThreadControl>())
        //                    {
        //                        _count += _p.Part.Count;
        //                        break;
        //                    }

        //            foreach (WorkControl _w in p.Cut.work.type.WorkControls)    // находим резьбовку среди работ и получаем её общую стоимость
        //                if (_w.workType is ThreadControl)
        //                {
        //                    p.Part.Price += _w.Result / _count;                 // добавляем часть от количества именно этой детали
        //                    p.Part.Accuracy += $" + {(float)Math.Round(_w.Result / _count, 2)}(р)";
        //                }
        //        }
        //    }
        //    else
        //    {
        //        if (uc is WorkControl w)
        //        {
        //            SetHoles(w.propsList[0]);
        //        }
        //        else if (uc is PartControl p)
        //        {
        //            SetHoles(p.Part.PropsDict[p.UserControls.IndexOf(this)][1]);
        //        }
        //    }
        //}

        Dictionary<double, float> WideDict = new();
        Dictionary<Metal, float> MetalRatioDict = new();
        public void OnPriceChanged()
        {
            if (owner is not WorkControl work || work.WorkDrop.SelectedItem is not Work _work || work.type.MetalDrop.SelectedItem is not Metal _metal) return;

            float price = 0;

            if (Parts != null && Parts.Count > 0)
            {
                foreach (PartControl p in Parts)
                    foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
                    {
                        if (WideDict.ContainsKey(work.type.S) && WideDict.ContainsKey(item.Wide))
                        {
                            float _time = minute * (WideDict[work.type.S] + WideDict[item.Wide] + MetalRatioDict[_metal] + MainWindow.MassRatio(p.Part.Mass));

                            // стоимость резьбы одного диаметра должна быть не ниже минимальной
                            price += _work.Price / p.Part.Count + _time * 2000 / 60;
                        }
                    }
            }
            else if (WideDict.ContainsKey(work.type.S) && WideDict.ContainsKey(Wide))
            {
                float _time = minute * (WideDict[work.type.S] + WideDict[Wide] + MetalRatioDict[_metal] + MainWindow.MassRatio(work.type.Mass));

                price = _work.Price / work.type.Count + _time * 2000 / 60;
            }
            work.SetResult(price, false);
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (isSaved)
            {
                if (uc is WorkControl w)
                {
                    w.propsList.Clear();
                    w.propsList.Add($"{Wide}");
                    w.propsList.Add($"{Holes}");
                }
                else if (uc is PartControl p)
                {
                    if (Wide == 0 || Holes == 0)
                    {
                        p.RemoveControl(this);
                        return;
                    }

                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{3}", $"{Wide}", $"{Holes}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + Р ")) p.Part.Description += " + Р ";

                    foreach (WorkControl _w in p.Cut.work.type.WorkControls)        // находим резьбу среди работ и получаем её минималку
                        if (_w.workType is ThreadControl && _w.WorkDrop.SelectedItem is Work _work && _w.type.MetalDrop.SelectedItem is Metal _metal)
                        {
                            if (WideDict.ContainsKey(_w.type.S) && WideDict.ContainsKey(Wide))
                            {
                                float _time = minute * (WideDict[_w.type.S] + WideDict[Wide] + MetalRatioDict[_metal] + MainWindow.MassRatio(p.Part.Mass));

                                p.Part.Price += _work.Price / p.Part.Count + _time * 2000 / 60;
                                p.Part.Accuracy += $" + {(float)Math.Round(_work.Price / p.Part.Count + _time * 2000 / 60, 2)}(р)";
                                break;
                            }
                        }
                }
            }
            else
            {
                if (uc is WorkControl w)
                {
                    SetWide(w.propsList[0]);
                    SetHoles(w.propsList[1]);
                }
                else if (uc is PartControl p)
                {
                    SetWide(p.Part.PropsDict[p.UserControls.IndexOf(this)][1]);
                    SetHoles(p.Part.PropsDict[p.UserControls.IndexOf(this)][2]);
                }
            }
        }

    }
}
