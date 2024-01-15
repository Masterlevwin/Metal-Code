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
    /// Логика взаимодействия для PaintControl.xaml
    /// </summary>
    public partial class PaintControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string? ral;
        public string? Ral
        {
            get => ral;
            set
            {
                ral = value;
                OnPropertyChanged(nameof(Ral));
            }
        }

        public List<PartControl>? Parts { get; set; }

        public Dictionary<string, float> TypeDict = new()
        {
            ["м²"] = 812,
            ["шт"] = 350,
            ["пог"] = 87
        };

        public readonly UserControl owner;
        public PaintControl(UserControl _work)
        {
            InitializeComponent();
            owner = _work;
            Tuning();
        }

        private void Tuning()               // настройка блока после инициализации
        {
            // формирование списка типов расчета окраски
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
                    if (w.workType is PaintControl) return;

                part.Cut.work.type.AddWork();

                // добавляем "Окраску" в список общих работ "Комплекта деталей"
                if (MainWindow.M.dbWorks.Works.Contains(MainWindow.M.dbWorks.Works.FirstOrDefault(n => n.Name == "Окраска"))
                    && MainWindow.M.dbWorks.Works.FirstOrDefault(n => n.Name == "Окраска") is Work _w)
                    part.Cut.work.type.WorkControls[^1].WorkDrop.SelectedItem = _w;
            }
        }

        private void SetRal(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRal(tBox.Text);
        }
        public void SetRal(string _ral)
        {
            Ral = _ral;
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
                    foreach (PaintControl item in p.UserControls.OfType<PaintControl>())
                        if (item.Ral != null) price += item.Price(p.Part.Mass, p.Part.Count, work);

                // стоимость данной окраски должна быть не ниже минимальной
                if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

                work.SetResult(price, false);
            }
            else if (Ral != null && Ral != "" && work.type.Mass > 0)
            {
                foreach (WorkControl w in work.type.WorkControls)
                    if (w != work && w.workType is PipeControl pipe)
                    {
                        SetType(2);
                        work.SetResult(Price(pipe.Mold, work.type.Count, work));
                        return;
                    }
                work.SetResult(Price(work.type.Mass, work.type.Count, work));
            }
        }

        private float Price(float _mass, float _count, WorkControl work)
        {
            if (work.type.MetalDrop.SelectedItem is not Metal metal) return 0;

            return TypeDrop.SelectedItem switch
            {
                "м²" => TypeDict[$"{TypeDrop.SelectedItem}"] * _mass * _count / work.type.S / metal.Density,
                "шт" => TypeDict[$"{TypeDrop.SelectedItem}"] * _count,
                "пог" => TypeDict[$"{TypeDrop.SelectedItem}"] * _mass,     // здесь нужна формула расчета пог.м
                _ => 0,
            };
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (isSaved)
            {
                if (uc is WorkControl w)
                {
                    w.propsList.Clear();
                    w.propsList.Add($"{Ral}");
                    w.propsList.Add($"{TypeDrop.SelectedIndex}");
                }
                else if (uc is PartControl p)       // первый элемент списка {2} - это (MenuItem)PartControl.Controls.Items[2]
                {
                    if (Ral == null || Ral == "")
                    {
                        p.RemoveControl(this);
                        return;
                    }

                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{2}", $"{Ral}", $"{TypeDrop.SelectedIndex}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + О ")) p.Part.Description += $" + О (цвет - {Ral}) ";

                    float price = 0, count = 0;             // переменные для расчета части цены отдельной детали
                    if (p.Cut.PartsControl != null) foreach (PartControl _p in p.Cut.PartsControl.Parts)
                            foreach (PaintControl item in _p.UserControls.OfType<PaintControl>())
                                if (item.Ral != null)       // перебираем все используемые блоки окраски
                                {                           // считаем общую стоимость всей окраски этого листа и кол-во окрашиваемых деталей
                                    price += item.Price(_p.Part.Mass, _p.Part.Count, p.Cut.work);
                                    count += _p.Part.Count;
                                }
                    // стоимость всей окраски должна быть не ниже минимальной
                    foreach (WorkControl _w in p.Cut.work.type.WorkControls)        // находим окраску среди работ и получаем её минималку
                        if (_w.workType is PaintControl && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            if (price > 0 && price < _work.Price) p.Part.Price += _work.Price * _w.Ratio / count;  // если расчетная стоимость ниже минимальной,
                                                                            // к цене детали добавляем усредненную часть минималки от общего количества деталей
                            else p.Part.Price += Price(p.Part.Mass, p.Part.Count, p.Cut.work) * _w.Ratio / p.Part.Count;   // иначе добавляем часть от количества именно этой детали                
                            break;
                        }
                }
            }
            else
            {
                if (uc is WorkControl w)
                {
                    SetRal(w.propsList[0]);
                    SetType((int)MainWindow.Parser(w.propsList[1]));
                }
                else if (uc is PartControl p)
                {
                    SetRal(p.Part.PropsDict[p.UserControls.IndexOf(this)][1]);
                    SetType((int)MainWindow.Parser(p.Part.PropsDict[p.UserControls.IndexOf(this)][2]));
                }
            }
        }
    }
}
