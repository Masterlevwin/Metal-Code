using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для ThreadControl.xaml
    /// </summary>
    public partial class ThreadControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string charName = string.Empty;
        public string CharName
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

        private float ratio = 1;
        public float Ratio
        {
            get => ratio;
            set
            {
                if (value != ratio)
                {
                    ratio = value;
                    OnPropertyChanged(nameof(Ratio));
                }
            }
        }

        public List<PartControl>? Parts { get; set; }

        public readonly UserControl owner;
        public ThreadControl(UserControl _control, string _charName)
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
                RatioBlock.Visibility = Visibility.Hidden;
            }
        }

        private void SetWide(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tBox && tBox.Text != "") SetWide(tBox.Text);
        }
        public void SetWide(string _wide)
        {
            if (float.TryParse(_wide, out float d)) Wide = d;
            if (owner is PartControl part)
            {
                MainWindow.M.IsLoadData = true;

                foreach (var item in part.work.type.WorkControls)
                    if (item.workType is ThreadControl thread && thread.CharName == CharName && thread.Wide == Wide) return;

                part.work.type.AddWork();

                // добавляем работу в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works)
                    switch (CharName)
                    {
                        case "Р":
                            part.work.type.WorkControls[^1].WorkDrop.SelectedItem = MainWindow.M.Works.SingleOrDefault(w => w.Name == "Резьба");
                            if (part.work.type.WorkControls[^1].workType is ThreadControl _threadR) _threadR.Wide = Wide;
                            break;
                        case "З":
                            part.work.type.WorkControls[^1].WorkDrop.SelectedItem = MainWindow.M.Works.SingleOrDefault(w => w.Name == "Зенковка");
                            if (part.work.type.WorkControls[^1].workType is ThreadControl _threadZ) _threadZ.Wide = Wide;
                            break;
                        case "С":
                            part.work.type.WorkControls[^1].WorkDrop.SelectedItem = MainWindow.M.Works.SingleOrDefault(w => w.Name == "Сверловка");
                            if (part.work.type.WorkControls[^1].workType is ThreadControl _threadS) _threadS.Wide = Wide;
                            break;
                        case "Зк":
                            part.work.type.WorkControls[^1].WorkDrop.SelectedItem = MainWindow.M.Works.SingleOrDefault(w => w.Name == "Заклепки");
                            if (part.work.type.WorkControls[^1].workType is ThreadControl _threadRz) _threadRz.Wide = Wide;
                            break;

                    }
                MainWindow.M.IsLoadData = false;
            }
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

        public void SetRatio(float _ratio)
        {
            if (_ratio <= 0) return;
            Ratio = _ratio;

            RatioBlock.Foreground = Brushes.Blue;
            RatioBlock.ToolTip = $"Доступная скидка за общее кол-во отверстий (по согласованию)";
        }

        public void OnPriceChanged()
        {
            if (owner is not WorkControl work || work.WorkDrop.SelectedItem is not Work _work) return;

            float price = 0;

            if (Parts?.Count > 0)
            {
                int count = 0;      //счетчик общего количества отверстий

                foreach (PartControl p in Parts)
                    foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>()) if (item.CharName == CharName && item.Holes > 0 && item.Wide == Wide)
                            count += p.Part.Count * item.Holes;

                SetRatio(MainWindow.RatioSale(count));

                foreach (PartControl p in Parts)
                    foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>()) if (item.CharName == CharName && item.Holes > 0 && item.Wide == Wide)
                            price += (_work.Price + item.Time(p.Part.Mass, item.Wide, work) * 2000 * p.Part.Count * item.Holes / 60) * Ratio;
            }
            else if (work.type.Mass > 0)
            {
                if (Wide == 0 || Holes == 0) return;

                SetRatio(MainWindow.RatioSale(work.type.det.Detail.Count * Holes));

                price = (_work.Price + Time(work.type.Mass, Wide, work) * 2000 * work.type.det.Detail.Count * Holes / 60) * Ratio;
            }
            work.SetResult(price, false);
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

            if (CharName == "Зк") return MainWindow.M.WideDict.ContainsKey(Math.Ceiling(_wide)) ?
                _work.Time * (MainWindow.M.WideDict[Math.Ceiling(_wide)] + MainWindow.MassRatio(_mass) - 1) : 0;

            return MainWindow.M.WideDict.ContainsKey(work.type.S) && MainWindow.M.WideDict.ContainsKey(Math.Ceiling(_wide)) ?
                _work.Time * (MainWindow.M.WideDict[work.type.S] + MainWindow.M.WideDict[Math.Ceiling(_wide)] + MainWindow.M.MetalRatioDict[metal] + MainWindow.MassRatio(_mass) - 3) : 0;
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
                    w.propsList.Add($"{Ratio}");
                }
                else if (uc is PartControl p)
                {
                    if (Wide == 0 || Holes == 0)
                    {
                        p.RemoveControl(this);
                        return;
                    }

                    int key = -1;
                    if (CharName == "Р")
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{3}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + Р ")) p.Part.Description += " + Р ";
                        key = 55;
                    }
                    else if (CharName == "З")
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{4}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + З ")) p.Part.Description += " + З ";
                        key = 56;
                    }
                    else if (CharName == "С")
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{5}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + С ")) p.Part.Description += " + С ";
                        key = 57;
                    }
                    else if (CharName == "Зк")
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{9}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + Зк ")) p.Part.Description += " + Зк ";
                        key = 65;
                    }

                    int count = 0;      //счетчик общего количества отверстий

                    if (p.owner is ICut _cut && _cut.PartsControl != null) foreach (PartControl _p in _cut.PartsControl.Parts)
                            foreach (ThreadControl item in _p.UserControls.OfType<ThreadControl>())
                                if (item.CharName == CharName && item.Holes > 0 && item.Wide == Wide) count += _p.Part.Count * item.Holes;

                    foreach (WorkControl _w in p.work.type.WorkControls)        // получаем минималку работы
                        if (_w.workType is ThreadControl thread && thread.CharName == CharName && thread.Wide == Wide && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            float _send = (_work.Price + Time(p.Part.Mass, Wide, _w) * 2000 * p.Part.Count * Holes/ 60) * MainWindow.RatioSale(count) * _w.Ratio * _w.TechRatio / p.Part.Count;
                            p.Part.Price += _send;

                            if (p.Part.PropsDict.ContainsKey(key) && float.TryParse(p.Part.PropsDict[key][0], out float value))
                                p.Part.PropsDict[key][0] = $"{value + _send}";  //блоков может быть несколько
                            else
                                p.Part.PropsDict[key] = new() { $"{_send}" };

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
                    if (w.propsList.Count > 2) SetRatio(MainWindow.Parser(w.propsList[2]));
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
