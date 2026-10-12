using Metal_Code.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public Guid Id { get; } = Guid.NewGuid();
        public ObservableCollection<PartControl>? Parts { get; set; }

        public readonly UserControl owner;
        public ThreadControl(UserControl _control, string _charName, Guid? id = null)
        {
            InitializeComponent();
            owner = _control;
            Id = id ?? Guid.NewGuid();
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
                // Проверка на дубликат (работает всегда)
                foreach (var item in part.work.type.WorkControls)
                {
                    if (item.workType is ThreadControl thread &&
                        thread.CharName == CharName &&
                        Math.Abs(thread.Wide - Wide) < 0.001f)
                    {
                        return;
                    }
                }

                part.work.type.AddWork();
                var newWorkControl = part.work.type.WorkControls[^1];

                string? targetWorkName = CharName switch
                {
                    "Р" => "Резьба",
                    "З" => "Зенковка",
                    "С" => "Сверловка",
                    "Зк" => "Заклепки",
                    _ => null
                };

                if (!string.IsNullOrEmpty(targetWorkName))
                {
                    var workItem = MainWindow.M.Works.FirstOrDefault(w => w.Name == targetWorkName);
                    if (workItem != null && newWorkControl.WorkDrop != null)
                    {
                        bool originalState = MainWindow.M.IsLoadData;
                        try
                        {
                            MainWindow.M.IsLoadData = true;
                            newWorkControl.WorkDrop.SelectedItem = workItem;
                        }
                        finally
                        {
                            MainWindow.M.IsLoadData = originalState;
                        }

                        if (newWorkControl.workType is ThreadControl targetThread)
                        {
                            targetThread.Wide = Wide;
                            newWorkControl.ApplyReadOnlyState();
                        }
                            
                    }
                }
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
                int count = 0;          // счетчик общего количества отверстий
                float totalTimeCost = 0; // накопитель стоимости машинного времени

                foreach (PartControl p in Parts)
                {
                    foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
                    {
                        if (item.CharName == CharName && item.Holes > 0 && item.Wide == Wide)
                        {
                            int partHoles = p.Part.Count * item.Holes;
                            count += partHoles;
                            // Считаем время и сразу переводим в стоимость (2000 руб/час)
                            totalTimeCost += item.Time(p.Part.Mass, item.Wide, work) * 2000 * partHoles / 60f;
                        }
                    }
                }

                if (count > 0)
                {
                    SetRatio(MainWindow.RatioSale(count));
                    price = (_work.Price + totalTimeCost) * Ratio;
                }
            }
            else if (work.type.Mass > 0)
            {
                if (Wide == 0 || Holes == 0) return;

                int count = work.type.det.Detail.Count * Holes;
                SetRatio(MainWindow.RatioSale(count));

                float totalTimeCost = Time(work.type.Mass, Wide, work) * 2000 * count / 60f;

                price = (_work.Price + totalTimeCost) * Ratio;
            }

            work.SetResult(price, false);
        }

        private float Time(float _mass, float _wide, WorkControl work)
        {
            if (work.WorkDrop.SelectedItem is not Work _work || work.type.MetalDrop.SelectedItem is not Metal metal) return 0;

            if (_wide > 30) return 0; // условие выхода

            float searchWide = _wide;
            while (searchWide <= 30 && !MainWindow.M.WideDict.ContainsKey(Math.Ceiling(searchWide)))
            {
                searchWide++;
            }

            // Если так и не нашли подходящий диаметр в словаре
            if (searchWide > 30 || !MainWindow.M.WideDict.ContainsKey(Math.Ceiling(searchWide)))
                return 0;

            float ceilWide = (float)Math.Ceiling(searchWide);

            if (CharName == "Зк")
            {
                return _work.Time * (MainWindow.M.WideDict[ceilWide] + MainWindow.MassRatio(_mass) - 1);
            }

            float destiny = work.type.S;
            var cutWork = work.type.WorkControls.FirstOrDefault(w => w.workType is CutControl);
            if (cutWork != null && cutWork.workType is CutControl cut)
                destiny = MainWindow.M.CorrectDestiny(work.type.S, cut.IsGrooved);

            if (MainWindow.M.WideDict.ContainsKey(destiny) && MainWindow.M.WideDict.ContainsKey(ceilWide))
            {
                return _work.Time * (MainWindow.M.WideDict[destiny] + MainWindow.M.WideDict[ceilWide] + MainWindow.M.MetalRatioDict[metal] + MainWindow.MassRatio(_mass) - 3);
            }

            return 0;
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
                        p.Part.WorksDict[Id] = new() { $"{3}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + Р ")) p.Part.Description += " + Р ";
                        key = 55;
                    }
                    else if (CharName == "З")
                    {
                        p.Part.WorksDict[Id] = new() { $"{4}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + З ")) p.Part.Description += " + З ";
                        key = 56;
                    }
                    else if (CharName == "С")
                    {
                        p.Part.WorksDict[Id] = new() { $"{5}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + С ")) p.Part.Description += " + С ";
                        key = 57;
                    }
                    else if (CharName == "Зк")
                    {
                        p.Part.WorksDict[Id] = new() { $"{9}", $"{Wide}", $"{Holes}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + Зк ")) p.Part.Description += " + Зк ";
                        key = 65;
                    }

                    int totalCount = 0;
                    float totalAssemblyTimeCost = 0;
                    WorkControl? matchingWorkControl = null;
                    Work? matchingWork = null;

                    // 1. Находим соответствующий WorkControl, чтобы получить _work и _w
                    foreach (WorkControl _w in p.work.type.WorkControls)
                    {
                        if (_w.workType is ThreadControl thread && thread.CharName == CharName && thread.Wide == Wide && _w.WorkDrop.SelectedItem is Work workItem)
                        {
                            matchingWorkControl = _w;
                            matchingWork = workItem;
                            break;
                        }
                    }

                    if (matchingWorkControl != null && matchingWork != null)
                    {
                        // 2. Считаем общее кол-во отверстий и время на ВСЮ сборку (или на одну деталь, если это не сборка)
                        if (p.owner is ICut _cut && _cut.PartsControl != null)
                        {
                            foreach (PartControl _p in _cut.PartsControl.Parts)
                            {
                                foreach (ThreadControl item in _p.UserControls.OfType<ThreadControl>())
                                {
                                    if (item.CharName == CharName && item.Holes > 0 && item.Wide == Wide)
                                    {
                                        int partHoles = _p.Part.Count * item.Holes;
                                        totalCount += partHoles;
                                        totalAssemblyTimeCost += item.Time(_p.Part.Mass, item.Wide, matchingWorkControl) * 2000 * partHoles / 60f;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Если это одиночная деталь (не в составе ICut)
                            totalCount = p.Part.Count * Holes;
                            totalAssemblyTimeCost = Time(p.Part.Mass, Wide, matchingWorkControl) * 2000 * totalCount / 60f;
                        }
                    }

                    // 3. Рассчитываем итоговую стоимость партии и распределяем на ОДНУ ШТУКУ детали
                    if (totalCount > 0 && matchingWork != null && matchingWorkControl != null)
                    {
                        // Полная стоимость операции на всю партию (минималка + время) * скидки
                        float totalBatchCost = (matchingWork.Price + totalAssemblyTimeCost)
                                               * MainWindow.RatioSale(totalCount)
                                               * matchingWorkControl.Ratio
                                               * matchingWorkControl.TechRatio;

                        // Стоимость для ОДНОЙ ШТУКИ текущей детали.
                        // Формула: (Общая стоимость) * (Кол-во отверстий в 1 шт) / (Общее кол-во отверстий)
                        float _send = totalBatchCost * Holes / totalCount;

                        p.Part.Price += _send;

                        if (p.Part.PropsDict.ContainsKey(key) && float.TryParse(p.Part.PropsDict[key][0], out float value))
                            p.Part.PropsDict[key][0] = $"{value + _send}"; // блоков может быть несколько
                        else
                            p.Part.PropsDict[key] = new() { $"{_send}" };
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
                    if (p.Part.WorksDict != null && p.Part.WorksDict.TryGetValue(Id, out List<string>? value))
                    {
                        SetWide(value[1]);
                        SetHoles(value[2]);
                    }
                    else if (p.Part.PropsDict.ContainsKey(_owner.UserControls.IndexOf(this)))
                    {
                        SetWide(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][1]);
                        SetHoles(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][2]);
                    }
                }
            }
        }

        private void Remove(object sender, RoutedEventArgs e) { if (owner is PartControl part) part.RemoveControl(this); }
    }
}
