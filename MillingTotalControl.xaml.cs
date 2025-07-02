using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        private const int maxX = 450, maxY = 400,       //рабочие габариты стола
                                priceMinute = 80;       //цена за минуту фрезерования

        MillingWindow? MillingWindow;

        public List<PartControl>? Parts { get; set; }

        public readonly UserControl owner;
        public MillingTotalControl(UserControl _control)
        {
            InitializeComponent();
            owner = _control;
            Tuning();
            OnPriceChanged();
        }

        private void Tuning()               //настройка блока после инициализации
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
                    if (w.workType is MillingTotalControl) return;

                part.work.type.AddWork();

                // добавляем "Фрезеровку" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Фрезеровка")
                    {
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
                MainWindow.M.IsLoadData = false;
            }
        }

        private void BtnEnabled()           //метод определения активности кнопки
        {
            if (owner is WorkControl work && work.type.det.Detail.IsComplect) MilBtn.IsEnabled = false;
            else if (owner is PartControl && !ValidateSize())
            {
                MainWindow.M.StatusBegin($"Проверьте размеры и толщину детали!");
                Remove();
            }
            else MilBtn.IsEnabled = ValidateSize();
        }

        public bool ValidateSize()          //метод проверки размеров детали
        {
            if (owner is WorkControl work)
            {
                if (work.type.A > maxX || work.type.B > maxX) return false;
                else if (work.type.A > maxY && work.type.B > maxY) return false;
                else if (work.type.S < 3) return false;
            }
            else if (owner is PartControl part && part.Part.PropsDict[100].Count > 1)
            {
                float x = MainWindow.Parser(part.Part.PropsDict[100][0]);
                float y = MainWindow.Parser(part.Part.PropsDict[100][1]);

                if (x > maxX || y > maxX) return false;
                else if (x > maxY && y > maxY) return false;
                else if (part.Part.Destiny < 3) return false;
            }
            return true;
        }

        private void SetTotalTime(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetTotalTime(tBox.Text);
        }
        public void SetTotalTime(string totalTime)
        {
            if (int.TryParse(totalTime, out int t)) TotalTime = t;
            OnPriceChanged();
        }

        private void ShowMillingWindow(object sender, RoutedEventArgs e)
        {
            if (MillingWindow != null)
            {
                // Если окно свернуто, разворачиваем его
                if (MillingWindow.WindowState == WindowState.Minimized)
                    MillingWindow.WindowState = WindowState.Normal;
                MillingWindow.Show();
                MillingWindow.Activate();
            }
            else
            {
                MillingWindow millingWindow = new(this);
                millingWindow.Show();
                MillingWindow = millingWindow;
            }
        }

        public void OnPriceChanged()
        {
            BtnEnabled();

            if (owner is not WorkControl work) return;

            float price = 0;

            if (Parts?.Count > 0)
            {
                int totalTime = 0;      //счетчик общего времени фрезеровки всех нарезанных деталей

                foreach (PartControl p in Parts)
                    foreach (MillingTotalControl item in p.UserControls.OfType<MillingTotalControl>())
                        if (item.TotalTime > 0)
                        {
                            price += item.TotalTime * priceMinute * p.Part.Count;
                            totalTime += item.TotalTime * p.Part.Count;
                        }
                TotalTime = totalTime;

                // стоимость фрезеровки должна быть не ниже минимальной
                if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

                work.SetResult(price, false);
            }
            else if (work.type.Mass > 0)
            {
                if (TotalTime <= 0) return;

                if (!ValidateSize())
                {
                    MainWindow.M.StatusBegin($"Проверьте размеры и толщину детали!");
                    work.SetResult(0, false);
                    return;
                }
                work.SetResult(TotalTime * priceMinute * work.type.det.Detail.Count);
            }
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (isSaved)
            {
                if (uc is WorkControl w)
                {
                    w.propsList.Clear();
                    w.propsList.Add($"{TotalTime}");
                    w.propsList.Add($"{MillingWindow?.IndexQualityOrRoughness}");
                    w.propsList.Add($"{MillingWindow?.Way}");

                    if (MillingWindow?.MillingHoles.Count > 0) w.type.det.Detail.MillingHoles = MillingWindow.MillingHoles;
                    if (MillingWindow?.MillingGrooves.Count > 0) w.type.det.Detail.MillingGrooves = MillingWindow.MillingGrooves;
                }
                else if (uc is PartControl p)
                {
                    if (TotalTime == 0)
                    {
                        p.RemoveControl(this);
                        return;
                    }

                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{8}", $"{TotalTime}", $"{MillingWindow?.IndexQualityOrRoughness}", $"{MillingWindow?.Way}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + Ф ")) p.Part.Description += " + Ф ";

                    if (MillingWindow?.MillingHoles.Count > 0) p.Part.MillingHoles = MillingWindow.MillingHoles;
                    if (MillingWindow?.MillingGrooves.Count > 0) p.Part.MillingGrooves = MillingWindow.MillingGrooves;

                    int count = 0;      //счетчик общего количества деталей

                    if (p.owner is ICut _cut && _cut.PartsControl != null) foreach (PartControl _p in _cut.PartsControl.Parts)
                            foreach (MillingTotalControl item in _p.UserControls.OfType<MillingTotalControl>()) count += _p.Part.Count;

                    // стоимость всей работы должна быть не ниже минимальной
                    foreach (WorkControl _w in p.work.type.WorkControls)        // находим фрезеровку среди работ и получаем её минималку
                        if (_w.workType is MillingTotalControl && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            float _send;
                            // если чистая стоимость работы ниже минимальной, к цене детали добавляем
                            if (_w.Result / _w.Ratio / _w.TechRatio > 0 && _w.Result / _w.Ratio / _w.TechRatio <= _work.Price)
                                _send = _work.Price * _w.Ratio * _w.TechRatio / count;  // усредненную часть минималки от общего количества деталей
                            else                                                        // иначе добавляем часть от количества именно этой детали
                                _send = TotalTime * priceMinute * _w.Ratio * _w.TechRatio;

                            p.Part.Price += _send;
                            p.Part.PropsDict[64] = new() { $"{_send}" };
                            break;
                        }
                }
            }
            else
            {
                MillingWindow millingWindow = new(this);
                MillingWindow = millingWindow;
                MillingWindow.WayIsInitialized = true;

                if (uc is WorkControl w)
                {
                    SetTotalTime(w.propsList[0]);
                    MillingWindow.IndexQualityOrRoughness = (int)MainWindow.Parser(w.propsList[1]);
                    MillingWindow.SetWay(w.propsList[2]);

                    if (w.type.det.Detail.MillingHoles?.Count > 0)
                    {
                        MillingWindow.MillingHoles = w.type.det.Detail.MillingHoles;
                        MillingWindow.SubscriptionMillingHoles();
                    }

                    if (w.type.det.Detail.MillingGrooves?.Count > 0)
                    {
                        MillingWindow.MillingGrooves = w.type.det.Detail.MillingGrooves;
                        MillingWindow.SubscriptionMillingGrooves();
                    }
                }
                else if (uc is PartControl p && owner is PartControl _owner)
                {
                    SetTotalTime(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][1]);
                    MillingWindow.IndexQualityOrRoughness = (int)MainWindow.Parser(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][2]);
                    MillingWindow.SetWay(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][3]);

                    if (p.Part.MillingHoles?.Count > 0)
                    {
                        MillingWindow.MillingHoles = p.Part.MillingHoles;
                        MillingWindow.SubscriptionMillingHoles();
                    }

                    if (p.Part.MillingGrooves?.Count > 0)
                    {
                        MillingWindow.MillingGrooves = p.Part.MillingGrooves;
                        MillingWindow.SubscriptionMillingGrooves();
                    }
                }
            }
        }

        private void Remove(object sender, RoutedEventArgs e) { Remove(); }
        public void Remove() { if (owner is PartControl part) part.RemoveControl(this); }
    }
}
