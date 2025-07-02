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
    /// Логика взаимодействия для ZincControl.xaml
    /// </summary>
    public partial class ZincControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float mass;
        public float Mass
        {
            get { return mass; }
            set
            {
                mass = (float)Math.Round(value, 2);
                OnPropertyChanged(nameof(Mass));
            }
        }

        public List<PartControl>? Parts { get; set; }

        public readonly UserControl owner;
        public ZincControl(UserControl _control)
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
                MainWindow.M.IsLoadData = true;

                part.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла

                foreach (WorkControl w in part.work.type.WorkControls)
                    if (w.workType is ZincControl) return;

                part.work.type.AddWork();

                // добавляем "Цинкование" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Цинкование")
                    {
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
                MainWindow.M.IsLoadData = false;
            }
        }

        private void SetMass()
        {
            Mass = 0;
            if (owner is WorkControl work)
            {
                if (Parts?.Count > 0)
                {
                    foreach (PartControl p in Parts)
                        foreach (ZincControl item in p.UserControls.OfType<ZincControl>())
                            if (item.Mass > 0) Mass += item.Mass;
                }
                else Mass = work.type.Mass * work.type.Count;
            }
            else if (owner is PartControl part)
            {
                Mass = part.Part.Mass * part.Part.Count;
            }
        }

        public void OnPriceChanged()
        {
            SetMass();                      //обновляем текущий вес деталей, отправляемых на оцинковку

            if (owner is not WorkControl work || work.type.MetalDrop.SelectedItem is not Metal metal) return;

            if (metal.Name == "ст3" || metal.Name == "хк" || metal.Name == "09г2с")
            {
                float price = Mass * (work.type.S switch
                {
                    <= 3 => 150,
                    <= 5 => 140,
                    <= 8 => 125,
                    _ => 110
                });

                // стоимость данного цинкования должна быть не ниже минимальной
                if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

                work.SetResult(price, false);
            }
            else MainWindow.M.StatusBegin($"Детали из {metal.Name} на оцинковку не отправляем!");
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
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + Ц ")) p.Part.Description += " + Ц ";

                        int count = 0;      //счетчик общего количества деталей

                        if (p.owner is ICut _cut && _cut.PartsControl != null) foreach (PartControl _p in _cut.PartsControl.Parts)
                                foreach (ZincControl item in _p.UserControls.OfType<ZincControl>())
                                    if (item.Mass > 0) count += _p.Part.Count;

                        // стоимость всей работы должна быть не ниже минимальной
                        foreach (WorkControl _w in p.work.type.WorkControls)            // находим цинкование среди работ и получаем её минималку
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
                                    } * Mass * _w.Ratio * _w.TechRatio / p.Part.Count;

                                p.Part.Price += _send;
                                p.Part.PropsDict[67] = new() { $"{_send}", $"{Mass}" };

                                break;
                            }
                    }
                }
            }
        }

        private void Remove(object sender, RoutedEventArgs e) { if (owner is PartControl part) part.RemoveControl(this); }
    }
}
