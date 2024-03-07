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
                    if (w.workType is ThreadControl thread && thread.CharName == CharName) return;

                part.Cut.work.type.AddWork();

                // добавляем "Резьбу" или "Зенковку" в список общих работ "Комплекта деталей"
                switch (CharName)
                {
                    case 'Р':
                        part.Cut.work.type.WorkControls[^1].WorkDrop.SelectedItem = MainWindow.M.Works.SingleOrDefault(w => w.Name == "Резьба");
                        break;
                    case 'З':
                        part.Cut.work.type.WorkControls[^1].WorkDrop.SelectedItem = MainWindow.M.Works.SingleOrDefault(w => w.Name == "Зенковка");
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
                        price += (_work.Price / p.Part.Count / item.Holes + Time(p.Part.Mass, item.Wide, work) * 2000 / 60) * p.Part.Count * item.Holes;
            }
            else if (work.type.Mass > 0)
            {
                if (Wide == 0 || Holes == 0) return;

                price = (_work.Price / work.type.Count / Holes + Time(work.type.Mass, Wide, work) * 2000 / 60) * work.type.Count * Holes;
            }
            work.SetResult(price, false);
        }

        private float Time(float _mass, float _wide, WorkControl work)
        {
            if (work.WorkDrop.SelectedItem is not Work _work || work.type.MetalDrop.SelectedItem is not Metal metal) return 0;

            return MainWindow.M.WideDict.ContainsKey(work.type.S) && MainWindow.M.WideDict.ContainsKey(Math.Round(_wide)) ?
                _work.Time * (MainWindow.M.WideDict[work.type.S] + MainWindow.M.WideDict[Math.Round(_wide)] + MainWindow.M.MetalRatioDict[metal] + MainWindow.MassRatio(_mass)) : 0;
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

                    if (CharName == 'Р')
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{3}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + Р ")) p.Part.Description += " + Р ";
                    }
                    else if (CharName == 'З')
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{4}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + З ")) p.Part.Description += " + З ";
                    }

                    foreach (WorkControl _w in p.Cut.work.type.WorkControls)        // находим резьбу или зенковку среди работ и получаем её минималку
                        if (_w.workType is ThreadControl thread && thread.CharName == CharName && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            p.Part.Price += (_work.Price / p.Part.Count / Holes + Time(p.Part.Mass, Wide, _w) * 2000 / 60) * Holes * _w.Ratio * _w.TechRatio;
                            p.Part.Accuracy += $" + {(float)Math.Round((_work.Price / p.Part.Count / Holes
                                + Time(p.Part.Mass, Wide, _w) * 2000 / 60) * Holes * _w.Ratio * _w.TechRatio, 2)}({CharName})";
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
                else if (uc is PartControl p)
                {
                    SetWide(p.Part.PropsDict[p.UserControls.IndexOf(this)][1]);
                    SetHoles(p.Part.PropsDict[p.UserControls.IndexOf(this)][2]);
                }
            }
        }

    }
}
