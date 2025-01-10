using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для ThreadControl.xaml
    /// </summary>
    public partial class ThreadControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private char charName;
        public char CharName
        {
            get => charName;
            set
            {
                if (value != charName)
                {
                    charName = value;
                    OnPropertyChanged(nameof(CharName));
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
        public ThreadControl(UserControl _control, char _charName)
        {
            InitializeComponent();
            owner = _control;
            CharName = _charName;
            Tuning();
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
                    if (w.workType is ThreadControl thread && thread.CharName == CharName) return;

                part.work.type.AddWork();

                // добавляем "Резьбу" или "Зенковку" в список общих работ "Комплекта деталей"
                switch (CharName)
                {
                    case 'Р':
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = MainWindow.M.Works.SingleOrDefault(w => w.Name == "Резьба");
                        break;
                    case 'З':
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = MainWindow.M.Works.SingleOrDefault(w => w.Name == "Зенковка");
                        break;
                    case 'С':
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = MainWindow.M.Works.SingleOrDefault(w => w.Name == "Сверловка");
                        break;

                }
            }
        }

        private void SetWide(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetWide(tBox.Text);
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

        public void OnPriceChanged()
        {
            if (owner is not WorkControl work || work.WorkDrop.SelectedItem is not Work _work) return;

            float price = 0;

            if (Parts != null && Parts.Count > 0)
            {
                foreach (PartControl p in Parts)
                    foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>()) if (item.CharName == CharName && item.Holes > 0)
                        price += (_work.Price + Time(p.Part.Mass, item.Wide, work) * 2000 / 60 * p.Part.Count * item.Holes) * RatioHoles(p.Part.Count * item.Holes);
            }
            else if (work.type.Mass > 0)
            {
                if (Wide == 0 || Holes == 0) return;

                price = (_work.Price + Time(work.type.Mass, Wide, work) * 2000 / 60 * work.type.Count * Holes) * RatioHoles(work.type.Count * Holes);
            }
            work.SetResult(price, false);
        }

        private float RatioHoles(int _count)
        {
            float _threadRatio = _count switch
            {
                <= 50 => 1,
                <= 100 => 0.9f,
                <= 500 => 0.8f,
                <= 1000 => 0.7f,
                <= 2000 => 0.6f,
                _ => 0.5f
            };

            return _threadRatio;
        }

        private float Time(float _mass, float _wide, WorkControl work)
        {
            if (work.WorkDrop.SelectedItem is not Work _work || work.type.MetalDrop.SelectedItem is not Metal metal) return 0;

            if (_wide > 30) return 0;                                       //условие выхода из рекурсии

            if (!MainWindow.M.WideDict.ContainsKey(Math.Ceiling(_wide)))    //если диаметр отверстия, округленный до большего целого,                                                                
            {                                                               //не соответствует возможной толщине,
                _wide++;                                                    //увеличиваем диаметр на единицу
                Time(_mass, _wide, work);                                   //и запускаем метод заново (рекурсия)
            }

            return MainWindow.M.WideDict.ContainsKey(work.type.S) && MainWindow.M.WideDict.ContainsKey(Math.Ceiling(_wide)) ?
                _work.Time + MainWindow.M.WideDict[work.type.S] + MainWindow.M.WideDict[Math.Ceiling(_wide)] + MainWindow.M.MetalRatioDict[metal] + MainWindow.MassRatio(_mass) - 1 : 0;
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

                    int key = -1;
                    if (CharName == 'Р')
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{3}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + Р ")) p.Part.Description += " + Р ";
                        key = 55;
                    }
                    else if (CharName == 'З')
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{4}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + З ")) p.Part.Description += " + З ";
                        key = 56;
                    }
                    else if (CharName == 'С')
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{5}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + С ")) p.Part.Description += " + С ";
                        key = 57;
                    }

                    foreach (WorkControl _w in p.work.type.WorkControls)        // получаем минималку работы
                        if (_w.workType is ThreadControl thread && thread.CharName == CharName && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            float _send = (_work.Price + Time(p.Part.Mass, Wide, _w) * 2000 / 60 * p.Part.Count * Holes) * RatioHoles(p.Part.Count * Holes) * _w.Ratio * _w.TechRatio / p.Part.Count;
                            p.Part.Price += _send;

                            if (p.Part.PropsDict.ContainsKey(key) && float.TryParse(p.Part.PropsDict[key][0], out float value))
                                p.Part.PropsDict[key].Insert(0, $"{value + _send}"); //блоков резьбы, зенковки или сверловки может быть несколько
                            else p.Part.PropsDict[key] = new() { $"{_send}" };

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

        private void Remove(object sender, RoutedEventArgs e) { if (owner is PartControl part) part.RemoveControl(this); }
    }
}
