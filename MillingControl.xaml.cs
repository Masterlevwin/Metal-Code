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

        public readonly UserControl owner;

        public MillingControl(UserControl _control)
        {
            InitializeComponent();
            owner = _control;

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
                        if (Parts.Count == 0) CreateParts(cut.WindowParts.Parts);
                        PartBtn.IsEnabled = true;
                        break;
                    }
            }
            else PartBtn.IsEnabled = false;
        }

        private void SetHoles(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetHoles(tBox.Text);
        }
        public void SetHoles(string _hours)
        {
            if (int.TryParse(_hours, out int h)) Holes = h;
            OnPriceChanged();
        }

        public void OnPriceChanged()
        {
            if (owner is not WorkControl work) return;

            BtnEnable();

            if (work.WorkDrop.SelectedItem is Work _work)
            {
                if (Parts.Count > 0)
                {
                    int _count = 0;
                    foreach (PartControl p in Parts)
                        foreach (MillingControl item in p.UserControls.OfType<MillingControl>())
                            _count += item.Holes * p.Part.Count;

                    work.SetResult((float)Math.Ceiling(_count * _work.Price / 6));
                }
                else work.SetResult((float)Math.Ceiling(Holes * work.type.Count * _work.Price / 6));
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
                    if (Holes == 0) return;

                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{3}", $"{Holes}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + М ")) p.Part.Description += " + М ";                   

                    int _count = 0;
                    if (p.Cut.WindowParts != null)
                        foreach (PartControl _p in p.Cut.WindowParts.Parts)
                            foreach (MillingControl item in _p.UserControls.OfType<MillingControl>())
                            {
                                _count += _p.Part.Count;
                                break;
                            }

                    foreach (WorkControl _w in p.Cut.work.type.WorkControls)    // находим мех обработку среди работ и получаем её общую стоимость
                        if (_w.workType is MillingControl)
                            p.Part.Price += _w.Result / _count;                 // добавляем часть от количества именно этой детали
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

        public List<PartControl> Parts = new();
        private void ViewPartWindow(object sender, RoutedEventArgs e)
        {
            if (owner is not WorkControl work || work.type.TypeDetailDrop.SelectedItem is not TypeDetail typeDetail || typeDetail.Name != "Лист металла") return;

            foreach (WorkControl w in work.type.WorkControls)
                if (w != work && w.workType is CutControl cut && cut.WindowParts != null) cut.WindowParts.ShowDialog();
        }
        public void CreateParts(List<PartControl> parts) { Parts.AddRange(parts); }     // метод, необходимый для корректной загрузки расчета

    }
}
