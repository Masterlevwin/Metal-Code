using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для MillingControl.xaml
    /// </summary>
    public partial class MillingControl : UserControl, INotifyPropertyChanged, IPriceChanged
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

        public List<PartControl>? Parts { get; set; }

        public readonly UserControl owner;
        public MillingControl(UserControl _control)
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
                    if (w.workType is MillingControl) return;

                part.Cut.work.type.AddWork();

                // добавляем "Мех обработку" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Мех обработка")
                    {
                        part.Cut.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }

                //if (MainWindow.M.dbWorks.Works.Contains(MainWindow.M.dbWorks.Works.FirstOrDefault(n => n.Name == "Мех обработка"))
                //    && MainWindow.M.dbWorks.Works.FirstOrDefault(n => n.Name == "Мех обработка") is Work _w)
                //    part.Cut.work.type.WorkControls[^1].WorkDrop.SelectedItem = _w;
            }
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

        public void OnPriceChanged()
        {
            if (owner is not WorkControl work) return;

            if (work.WorkDrop.SelectedItem is Work _work)
            {
                if (Parts != null && Parts.Count > 0)
                {
                    int _count = 0;
                    foreach (PartControl p in Parts)
                        foreach (MillingControl item in p.UserControls.OfType<MillingControl>())
                            _count += item.Holes * p.Part.Count;

                    float price = (float)Math.Ceiling(_count * _work.Price / 12);

                    // стоимость данной мех обработки должна быть не ниже минимальной
                    price = price > 0 && price < _work.Price ? _work.Price : price;

                    work.SetResult(price, false);
                }
                else work.SetResult((float)Math.Ceiling(Holes * work.type.Count * _work.Price / 12));
            }
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (isSaved)
            {
                if (uc is WorkControl w)
                {
                    w.propsList.Clear();
                    w.propsList.Add($"{Holes}");
                }
                else if (uc is PartControl p)       // первый элемент списка {3} - это (MenuItem)PartControl.Controls.Items[3]
                {
                    if (Holes == 0)
                    {
                        p.RemoveControl(this);
                        return;
                    }

                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{3}", $"{Holes}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + М ")) p.Part.Description += " + М ";                   

                    int _count = 0;
                    if (p.Cut.PartsControl != null)
                        foreach (PartControl _p in p.Cut.PartsControl.Parts)
                            foreach (MillingControl item in _p.UserControls.OfType<MillingControl>())
                            {
                                _count += _p.Part.Count;
                                break;
                            }

                    foreach (WorkControl _w in p.Cut.work.type.WorkControls)    // находим мех обработку среди работ и получаем её общую стоимость
                        if (_w.workType is MillingControl)
                        {
                            p.Part.Price += _w.Result / _count;                 // добавляем часть от количества именно этой детали
                            p.Part.Accuracy += $" + {(float)Math.Round(_w.Result / _count, 2)}(м)";
                        }
                }
            }
            else
            {
                if (uc is WorkControl w)
                {
                    SetHoles(w.propsList[0]);
                }
                else if (uc is PartControl p)
                {
                    SetHoles(p.Part.PropsDict[p.UserControls.IndexOf(this)][1]);
                }
            }
        }
    }
}
