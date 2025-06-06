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

        private int extraTime;
        public int ExtraTime
        {
            get => extraTime;
            set
            {
                if (extraTime != value)
                {
                    extraTime = value;
                    OnPropertyChanged(nameof(ExtraTime));
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
            OnPriceChanged();
        }

        private void Tuning()               // настройка блока после инициализации
        {
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
                    if (w.workType is ZincControl) return;

                part.work.type.AddWork();

                // добавляем "Фрезеровку" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Фрезеровка")
                    {
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
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

        private void SetExtraTime(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetExtraTime(tBox.Text);
        }
        public void SetExtraTime(string _time)
        {
            if (int.TryParse(_time, out int t)) ExtraTime = t;
            OnPriceChanged();
        }

        private const float tooth = 0.2f, priceTime = 80;
        private const int spindle = 3000, toothCount = 3;

        public void OnPriceChanged()
        {
            if (owner is not WorkControl work) return;

            float price = 0;

            if (Parts?.Count > 0)
            {
                foreach (PartControl p in Parts)
                    foreach (MillingControl item in p.UserControls.OfType<MillingControl>())
                        if (item.ExtraTime > 0 && Holes > 0)
                        {
                            price += ((p.Part.Way + work.type.S * Holes) * 2 / (tooth * spindle * toothCount) + item.ExtraTime) * priceTime;
                        }

            }

        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (isSaved)
            {
                if (uc is WorkControl w)
                {
                    w.propsList.Clear();
                }
                else if (uc is PartControl p)
                {
                    if (p.work.type.MetalDrop.SelectedItem is Metal metal &&
                        (metal.Name == "ст3" || metal.Name == "хк" || metal.Name == "09г2с"))
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{7}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + Ф ")) p.Part.Description += " + Ф ";

                        int count = 0;      //счетчик общего количества деталей

                        if (p.owner is ICut _cut && _cut.PartsControl != null) foreach (PartControl _p in _cut.PartsControl.Parts)
                                foreach (ZincControl item in _p.UserControls.OfType<ZincControl>())
                                    if (item.Mass > 0) count += _p.Part.Count;

                        // стоимость всей работы должна быть не ниже минимальной
                        foreach (WorkControl _w in p.work.type.WorkControls)            // находим фрезеровку среди работ и получаем её минималку
                            if (_w.workType is ZincControl && _w.WorkDrop.SelectedItem is Work _work)
                            {
                                float _send;
                                // если чистая стоимость работы ниже минимальной, к цене детали добавляем
                                if (_w.Result / _w.Ratio / _w.TechRatio > 0 && _w.Result / _w.Ratio / _w.TechRatio <= _work.Price)
                                    _send = _work.Price * _w.Ratio * _w.TechRatio / count;  // усредненную часть минималки от общего количества деталей
                                else                                                        // иначе добавляем часть от количества именно этой детали
                                    _send = p.work.type.S switch
                                    {
                                        <= 3 => 150,
                                        <= 5 => 140,
                                        <= 8 => 125,
                                        _ => 110
                                    } * p.Part.Count * p.Part.Mass * _w.Ratio * _w.TechRatio / p.Part.Count;

                                p.Part.Price += _send;
                                p.Part.PropsDict[64] = new() { $"{_send}" };

                                break;
                            }
                    }
                }
            }
        }

        private void Remove(object sender, RoutedEventArgs e) { if (owner is PartControl part) part.RemoveControl(this); }
    }
}
