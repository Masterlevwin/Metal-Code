using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для SawControl.xaml
    /// </summary>
    public partial class SawControl : UserControl, INotifyPropertyChanged, IPriceChanged, ICut
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private bool usedAssistant = false;
        public bool UsedAssistant
        {
            get => usedAssistant;
            set
            {
                usedAssistant = value;
                OnPropertyChanged(nameof(UsedAssistant));
                OnPriceChanged();
            }
        }

        public bool HaveCut {  get; set; }
        public float Way { get; set; }
        public int Pinhole { get; set; }
        public float Mass { get; set; }

        public Guid Id { get; }
        public TubeType Tube { get; set; }
        public ObservableCollection<PartControl>? Parts { get; set; }
        public PartsControl? PartsControl { get; set; }
        public TabItem TabItem { get; set; } = new();
        public List<Part>? PartDetails { get; set; } = new();
        public List<LaserItem>? Items { get; set; } = new();

        public Dictionary<double, float> DestinyDict = new()
        {
            [.5f] = 1,
            [.7f] = 1,
            [.8f] = 1,
            [1] = 1,
            [1.2f] = 1,
            [1.5f] = 1,
            [2] = 1.3f,
            [2.5] = 1.3f,
            [3] = 1.6f,
            [4] = 2,
            [5] = 2.5f,
            [6] = 3,
            [8] = 4,
            [10] = 6,
            [12] = 8,
            [14] = 10,
        };

        public readonly WorkControl work;

        public SawControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;
            DataContext = this;

            work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
            work.type.Priced += OnPriceChanged;                 // подписка на изменение типовой детали

            SetTube();
        }

        private void SetTube()
        {
            if (work.type.TypeDetailDrop.SelectedItem is TypeDetail type && type.Name != "Лист металла")
                Tube = type.Name switch
                {
                    "Труба профильная" => TubeType.rect,
                    "Труба круглая" => TubeType.round,
                    "Труба круглая ВГП" => TubeType.round,
                    "Уголок неравнополочный" => TubeType.freeform,
                    "Уголок равнополочный" => TubeType.corner,
                    "Круг" => TubeType.circle,
                    "Квадрат" => TubeType.rod,
                    "Швеллер П" => TubeType.channel,
                    "Швеллер У" => TubeType.channel,
                    "Двутавр" => TubeType.ibeam,
                    "Двутавр парал" => TubeType.ibeam,
                    "Двутавр широк" => TubeType.ibeam,
                    "Двутавр колон" => TubeType.ibeam,
                    _ => TubeType.rect,
                };
        }

        public ObservableCollection<PartControl> PartList()
        {
            ObservableCollection<PartControl> _parts = new();

            if (PartDetails?.Count > 0) foreach (var part in PartDetails) _parts.Add(new(this, work, part));

            return _parts;
        }

        public void AddPartsControl() => work.type.PartsStack.Children.Add(PartsControl);

        public void SetTotalProperties()
        {
            Mass = 0;

            if (work.type.MetalDrop.SelectedItem is Metal metal && work.type.S >= 0)
                switch (Tube)
                {
                    case TubeType.rect:
                        Mass = (float)Math.Round(0.0157f * work.type.S * (work.type.A + work.type.B - 2.86f * work.type.S) * work.type.L * work.type.Count * metal.Density / 7850, 3);
                        break;
                    case TubeType.round:
                        Mass = (float)Math.Round(Math.PI * work.type.S * (work.type.A - work.type.S) * work.type.L * work.type.Count * metal.Density / 1000000, 3);
                        break;
                    case TubeType.circle:
                        Mass = (float)Math.Round(Math.PI * work.type.A * work.type.A * work.type.L / 4 * work.type.Count * metal.Density / 1000000, 3);
                        break;
                    case TubeType.square:
                        Mass = (float)Math.Round(0.0157f * work.type.S * (work.type.A + work.type.B - 2.86f * work.type.S) * work.type.L * work.type.Count * metal.Density / 7850, 3);
                        break;
                    case TubeType.rod:
                        Mass = (float)Math.Round(work.type.A * work.type.A * work.type.L * work.type.Count * metal.Density / 1000000, 3);
                        break;
                    case TubeType.channel:
                        Mass = (float)Math.Round(work.type.Channels[work.type.SortDrop.SelectedIndex] * work.type.L * work.type.Count / 1000, 3);
                        break;
                    case TubeType.corner:
                        Mass = (float)Math.Round((work.type.S * (work.type.A + work.type.A - work.type.S) + 0.2146f * (work.type.Corners[work.type.SortDrop.SelectedIndex].Item1
                            * work.type.Corners[work.type.SortDrop.SelectedIndex].Item1 - 2 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2
                            * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2)) * work.type.L * work.type.Count * metal.Density / 1000000, 3);
                        break;
                    case TubeType.freeform:
                        Mass = (float)Math.Round((work.type.S * (work.type.A + work.type.B - work.type.S) + 0.2146f * (work.type.Corners[work.type.SortDrop.SelectedIndex].Item1
                            * work.type.Corners[work.type.SortDrop.SelectedIndex].Item1 - 2 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2
                            * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2)) * work.type.L * work.type.Count * metal.Density / 1000000, 3);
                        break;
                    case TubeType.ibeam:
                        Mass = (float)Math.Round(work.type.BeamDict[work.type.TypeDetailDrop.Text][work.type.SortDrop.SelectedIndex].Item1 * work.type.L * work.type.Count / 1000, 3);
                        break;
                }

            work.type.MassCalculate();
        }

        public void OnPriceChanged()
        {
            float destiny = MainWindow.M.CorrectDestiny(work.type.S);    //получаем расчетную толщину

            if (work.WorkDrop.SelectedItem is not Work _work
                || work.type.MetalDrop.SelectedItem is not Metal _metal
                || !DestinyDict.ContainsKey(destiny))
            {
                work.SetResult(0, false);
                MainWindow.M.StatusBegin($"Для толщины {work.type.S} лентопил не доступен!",
                                            MainWindow.StatusMessageType.Error);
                return;
            }

            work.SetResult(_work.Price +                    //минимальная стоимость работы +
                (_work.Time + DestinyDict[destiny]          //(минимальное время работы + коэф за толщину
                + MainWindow.M.MetalRatioDict[_metal]       //+ коэф за металл
                + MainWindow.MassRatio(work.type.Mass
                                    / work.type.Count))     //+ коэф за вес одной заготовки)
            * (UsedAssistant ? 1.5f : 1) * 2000 / 60        //* коэф за помощника
            * (PartDetails?.Count > 0 ? PartDetails.Sum(p => p.Count)
                : work.type.det.Detail.Count)               //* количество деталей
            , false);
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (uc is not WorkControl w) return;
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{UsedAssistant}");
                w.propsList.Add($"{Tube}");
                w.propsList.Add($"{Way}");

                if (PartDetails?.Count > 0)
                {
                    var massTotal = PartDetails.Sum(p => p.Mass * p.Count);

                    foreach (Part p in PartDetails)
                    {
                        p.Price += work.type.Result * p.Mass / massTotal;
                        p.Price += work.Result * p.Way / Way;

                        p.PropsDict[50] = new() { $"{work.type.Result * p.Mass / massTotal}" };
                        p.PropsDict[61] = new() { $"{work.Result * p.Way / Way}" };
                    }
                }
            }
            else
            {
                if (w.propsList.Count > 0 && bool.TryParse(w.propsList[0], out bool prop)) UsedAssistant = prop;
                if (w.propsList.Count > 1 && Enum.TryParse(w.propsList[1], out TubeType tube)) Tube = tube;
                if (w.propsList.Count > 2 && float.TryParse(w.propsList[2], out float way)) Way = way;
            }
        }
    }
}
