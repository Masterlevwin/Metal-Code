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
                    if (w.workType is MillingControl) return;

                part.work.type.AddWork();

                // добавляем "Фрезеровку" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Фрезеровка")
                    {
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
        }
        
        private void SetWide(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox && tBox.Text != "") SetWide(tBox.Text);
        }
        public void SetWide(string _wide)
        {
            if (float.TryParse(_wide, out float d)) Wide = d;
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

        public bool ValidateSize()         //метод проверки размеров детали
        {
            if (owner is WorkControl work)
            {
                if (work.type.A > maxX || work.type.B > maxX) return false;
                else if (work.type.A > maxY && work.type.B > maxY) return false;
            }
            else if (owner is PartControl part && part.Part.PropsDict[100].Count > 1)
            {
                float x = MainWindow.Parser(part.Part.PropsDict[100][0]);
                float y = MainWindow.Parser(part.Part.PropsDict[100][1]);

                if (x > maxX || y > maxX) return false;
                else if (x > maxY && y > maxY) return false;
            }
            return true;
        }

        private const float tooth = 0.2f;   //подача на зуб
        private const int spindle = 1000,   //частота вращения шпинделя
            toothCount = 2,                 //количество зубьев
            passCount = 3,                  //количество проходов инструмента
            priceTime = 80,                 //стоимость минуты
            maxX = 450, maxY = 400;         //размеры стола

        public void OnPriceChanged()
        {
            if (owner is PartControl && !ValidateSize())
            {
                MainWindow.M.StatusBegin($"Проверьте габаритные размеры детали!");
                Remove();
            }
            if (owner is not WorkControl work) return;

            float price = 0;

            if (Parts?.Count > 0)
            {
                foreach (PartControl p in Parts)
                    foreach (MillingControl item in p.UserControls.OfType<MillingControl>())
                        if (item.ValidateSize())
                            price += (float)Math.Ceiling((p.Part.Way + item.Holes * item.Wide * Math.PI * passCount)
                                * passCount * priceTime * p.Part.Count / (tooth * spindle * toothCount));

                // стоимость фрезеровки должна быть не ниже минимальной
                if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

                work.SetResult(price, false);
            }
            else if (work.type.Mass > 0)
            {
                if (!ValidateSize())
                {
                    MainWindow.M.StatusBegin($"Проверьте габаритные размеры детали!");
                    work.SetResult(0, false);
                    return;
                }

                foreach (WorkControl w in work.type.WorkControls)
                    if (w.workType != this && w.workType is ICut _cut && _cut.Way > 0)
                    {
                        work.SetResult((float)Math.Ceiling((_cut.Way * 1000 + Holes * Wide * Math.PI * passCount)
                            * passCount * priceTime * work.type.Count/ (tooth * spindle * toothCount)));
                        break;
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
                    w.propsList.Add($"{Wide}");
                    w.propsList.Add($"{Holes}");
                }
                else if (uc is PartControl p)
                {
                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{8}", $"{Wide}", $"{Holes}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + Ф ")) p.Part.Description += " + Ф ";

                    int count = 0;      //счетчик общего количества деталей

                    if (p.owner is ICut _cut && _cut.PartsControl != null) foreach (PartControl _p in _cut.PartsControl.Parts)
                            foreach (MillingControl item in _p.UserControls.OfType<MillingControl>()) count += _p.Part.Count;

                    // стоимость всей работы должна быть не ниже минимальной
                    foreach (WorkControl _w in p.work.type.WorkControls)                // находим фрезеровку среди работ и получаем её минималку
                        if (_w.workType is MillingControl && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            float _send;
                            // если чистая стоимость работы ниже минимальной, к цене детали добавляем
                            if (_w.Result / _w.Ratio / _w.TechRatio > 0 && _w.Result / _w.Ratio / _w.TechRatio <= _work.Price)
                                _send = _work.Price * _w.Ratio * _w.TechRatio / count;  // усредненную часть минималки от общего количества деталей
                            else                                                        // иначе добавляем часть от количества именно этой детали
                                _send = (float)Math.Ceiling((p.Part.Way + Holes * Wide * Math.PI * passCount)
                                    * passCount * priceTime * _w.Ratio * _w.TechRatio / (tooth * spindle * toothCount));

                            p.Part.Price += _send;
                            //p.Part.PropsDict[65] = new() { $"{_send}" };
                            break;
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
                else if (uc is PartControl p && owner is PartControl _owner)
                {
                    SetWide(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][1]);
                    SetHoles(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][2]);
                }
            }
        }

        private void Remove(object sender, RoutedEventArgs e) { Remove(); }
        private void Remove() { if (owner is PartControl part) part.RemoveControl(this); }
    }
}
