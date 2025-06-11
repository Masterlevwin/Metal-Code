using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для MillingTotalControl.xaml
    /// </summary>
    public partial class MillingTotalControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private int totalTime;
        public int TotalTime
        {
            get => totalTime;
            set
            {
                if (totalTime != value)
                {
                    totalTime = value;
                    OnPropertyChanged(nameof(TotalTime));
                }
            }
        }

        public List<PartControl>? Parts { get; set; }

        public readonly UserControl owner;
        public MillingTotalControl(UserControl _control)
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
                    if (w.workType is MillingTotalControl) return;

                part.work.type.AddWork();

                // добавляем "Фрезеровку" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Фрезеровка")
                    {
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
        }

        private void BtnEnabled()
        {
            if (owner is WorkControl work && work.type.det.Detail.IsComplect) MilBtn.IsEnabled = false;
            else if (owner is PartControl && !ValidateSize())
            {
                MainWindow.M.StatusBegin($"Проверьте размеры и толщину детали!");
                Remove();
            }
            else MilBtn.IsEnabled = ValidateSize();
        }

        public bool ValidateSize()         //метод проверки размеров детали
        {
            if (owner is WorkControl work)
            {
                if (work.type.A > maxX || work.type.B > maxX) return false;
                else if (work.type.A > maxY && work.type.B > maxY) return false;
                else if (work.type.S < 2) return false;
            }
            else if (owner is PartControl part && part.Part.PropsDict[100].Count > 1)
            {
                float x = MainWindow.Parser(part.Part.PropsDict[100][0]);
                float y = MainWindow.Parser(part.Part.PropsDict[100][1]);

                if (x > maxX || y > maxX) return false;
                else if (x > maxY && y > maxY) return false;
                else if (part.Part.Destiny < 2) return false;
            }
            return true;
        }

        private const float tooth = 0.1f;   //подача на зуб
        private const int spindle = 1000,   //частота вращения шпинделя
            toothCount = 2,                 //количество зубьев
            passCount = 3,                  //количество проходов инструмента
            priceTime = 80,                 //стоимость минуты
            maxX = 450, maxY = 400;         //размеры стола

        private void OpenSettings(object sender, RoutedEventArgs e)
        {
            MillingWindow millingWindow = new(owner);
            millingWindow.Show();
        }

        public void OnPriceChanged()
        {
            BtnEnabled();

            if (owner is not WorkControl work || work.type.MetalDrop.SelectedItem is not Metal metal) return;

            //float price = 0;

            //if (Parts?.Count > 0)
            //{
            //    foreach (PartControl p in Parts)
            //        foreach (MillingControl item in p.UserControls.OfType<MillingControl>())
            //            if (item.ValidateSize())
            //                price += (float)Math.Ceiling((p.Part.Way + item.Holes * item.Wide * Math.PI) * p.Part.Destiny
            //                    * passCount * priceTime * p.Part.Count / (tooth * toothCount
            //                    * (metal.Name != null && (metal.Name.Contains("амг") || metal.Name.Contains("д16")) ? 3000 : 1000)));

            //    // стоимость фрезеровки должна быть не ниже минимальной
            //    if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

            //    work.SetResult(price, false);
            //}
            //else if (work.type.Mass > 0)
            //{
            //    if (!ValidateSize())
            //    {
            //        MainWindow.M.StatusBegin($"Проверьте размеры и толщину детали!");
            //        work.SetResult(0, false);
            //        return;
            //    }

            //    foreach (WorkControl w in work.type.WorkControls)
            //        if (w.workType != this && w.workType is ICut _cut && _cut.Way > 0)
            //        {
            //            work.SetResult((float)Math.Ceiling((_cut.Way * 1000 + Holes * Wide * Math.PI) * work.type.S
            //                * passCount * priceTime * work.type.Count/ (tooth * toothCount
            //                * (metal.Name != null && (metal.Name.Contains("амг") || metal.Name.Contains("д16")) ? 3000 : 1000))));
            //            break;
            //        }
            //}
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            //if (isSaved)
            //{
            //    if (uc is WorkControl w)
            //    {
            //        w.propsList.Clear();
            //        w.propsList.Add($"{Wide}");
            //        w.propsList.Add($"{Holes}");
            //    }
            //    else if (uc is PartControl p && p.work.type.MetalDrop.SelectedItem is Metal metal)
            //    {
            //        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{8}", $"{Wide}", $"{Holes}" };
            //        if (p.Part.Description != null && !p.Part.Description.Contains(" + Ф ")) p.Part.Description += " + Ф ";

            //        int count = 0;      //счетчик общего количества деталей

            //        if (p.owner is ICut _cut && _cut.PartsControl != null) foreach (PartControl _p in _cut.PartsControl.Parts)
            //                foreach (MillingControl item in _p.UserControls.OfType<MillingControl>()) count += _p.Part.Count;

            //        // стоимость всей работы должна быть не ниже минимальной
            //        foreach (WorkControl _w in p.work.type.WorkControls)                // находим фрезеровку среди работ и получаем её минималку
            //            if (_w.workType is MillingControl && _w.WorkDrop.SelectedItem is Work _work)
            //            {
            //                float _send;
            //                // если чистая стоимость работы ниже минимальной, к цене детали добавляем
            //                if (_w.Result / _w.Ratio / _w.TechRatio > 0 && _w.Result / _w.Ratio / _w.TechRatio <= _work.Price)
            //                    _send = _work.Price * _w.Ratio * _w.TechRatio / count;  // усредненную часть минималки от общего количества деталей
            //                else                                                        // иначе добавляем часть от количества именно этой детали
            //                    _send = (float)Math.Ceiling((p.Part.Way + Holes * Wide * Math.PI) * p.Part.Destiny
            //                        * passCount * priceTime * _w.Ratio * _w.TechRatio / (tooth * toothCount
            //                        * (metal.Name != null && (metal.Name.Contains("амг") || metal.Name.Contains("д16")) ? 3000 : 1000)));

            //                p.Part.Price += _send;
            //                //p.Part.PropsDict[65] = new() { $"{_send}" };
            //                break;
            //            }
            //    }
            //}
            //else
            //{
            //    if (uc is WorkControl w)
            //    {
            //        SetWide(w.propsList[0]);
            //        SetHoles(w.propsList[1]);
            //    }
            //    else if (uc is PartControl p && owner is PartControl _owner)
            //    {
            //        SetWide(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][1]);
            //        SetHoles(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][2]);
            //    }
            //}
        }

        private void Remove(object sender, RoutedEventArgs e) { Remove(); }
        public void Remove() { if (owner is PartControl part) part.RemoveControl(this); }
    }
}
