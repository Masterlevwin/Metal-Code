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
    /// Логика взаимодействия для MillingWindow.xaml
    /// </summary>
    public partial class MillingWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public delegate void Changed();
        public event Changed? SlowdownChanged;

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

        private int contourTime;
        public int ContourTime
        {
            get => contourTime;
            set
            {
                contourTime = value;
                OnPropertyChanged(nameof(ContourTime));
            }
        }

        private float way;
        public float Way
        {
            get => way;
            set
            {
                if (value != way)
                {
                    way = (float)Math.Ceiling(value);
                    OnPropertyChanged(nameof(Way));
                }
            }
        }

        private string? metal;
        public string? Metal
        {
            get => metal;
            set
            {
                if (value != metal)
                {
                    metal = value;
                    OnPropertyChanged(nameof(Metal));
                }
            }
        }

        private float destiny;
        public float Destiny
        {
            get => destiny;
            set
            {
                if (value != destiny)
                {
                    destiny = value;
                    OnPropertyChanged(nameof(Destiny));
                }
            }
        }

        private int cutter;
        public int Cutter
        {
            get => cutter;
            set
            {
                cutter = value;
                OnPropertyChanged(nameof(Cutter));
            }
        }

        private float slowdown;
        public float Slowdown
        {
            get => slowdown;
            set
            {
                slowdown = value;
                OnPropertyChanged(nameof(Slowdown));
            }
        }
        
        private int indexQualityOrRoughness;
        public int IndexQualityOrRoughness
        {
            get => indexQualityOrRoughness;
            set
            {
                if (indexQualityOrRoughness != value)
                {
                    indexQualityOrRoughness = value;
                    OnPropertyChanged(nameof(IndexQualityOrRoughness));
                }
            }
        }

        private readonly string[] quality = { "12 (±0.010 мм)", "11 (±0.015 мм)", "10 (±0.025 мм)", "9 (±0.040 мм)", "8 (±0.060 мм)", "7 (±0.100 мм)" };
        private readonly string[] roughness = { "12.5 - предвар", "12.5 - черн обр", "6.3 - груб фрез", "6.3 - черн фрез", "3.2 - чист фрез", "1.6 - чист фрез" };
        private readonly float[] slowdowns = { 1, 0.9f, 0.8f, 0.7f, 0.5f, 0.3f };

        public Dictionary<string, Dictionary<int, int>> MinutesDict = new()
        {
            ["сталь"] = new() { [2] = 1000, [3] = 800, [4] = 600, [5] = 500, [6] = 400, [8] = 300, [10] = 240, [12] = 200, [14] = 180, [16] = 160 },
            ["алюминий"] = new() { [2] = 2000, [3] = 1600, [4] = 1200, [5] = 1000, [6] = 800, [8] = 600, [10] = 500, [12] = 400, [14] = 360, [16] = 300 }
        };

        public readonly MillingTotalControl owner;

        public MillingWindow(MillingTotalControl _milling)
        {
            InitializeComponent();
            owner = _milling;
            DataContext = this;

            Unloaded += CloseWindow;  //подписка на закрытие окна в случае удаления связанного контрола
            SetContourTime();
        }

        private void MillingWindow_Loaded(object sender, RoutedEventArgs e)     //настройка окна при загрузке
        {
            if (owner.owner is WorkControl work)
            {
                DetailName.Text = work.type.det.Detail.Title;
                foreach (WorkControl w in work.type.WorkControls)
                    if (w.workType is ICut _cut)
                    {
                        Way = _cut.Way;
                        break;
                    }
                Metal = work.type.MetalDrop.Text;
                Destiny = work.type.S;
            }
            else if (owner.owner is PartControl part)
            {
                DetailName.Text = part.Part.Title;
                Way = part.Part.Way;
                Metal = part.Part.Metal;
                Destiny = part.Part.Destiny;
            }
            QualityDrop.ItemsSource = quality;
            RoughnessDrop.ItemsSource = roughness;
        }

        private void HideWindow(object? sender, CancelEventArgs e)      //метод скрытия окна вместо закрытия
        {
            Hide();
            e.Cancel = true;
        }

        private void CloseWindow(object sender, RoutedEventArgs e)      //метод полного закрытия окна
        {
            Closing -= HideWindow;
            Close();
        }

        private void AddMilling(object sender, RoutedEventArgs e)       //метод добавления контрола обработки
        {
            MillingControl milling = new(this);
            MillingStack.Children.Insert(MillingStack.Children.Count - 1, milling);
        }

        public void RemoveControl(MillingControl milling)
        {
            MillingStack.Children.Remove(milling);
            SetTotalTime();
        }

        private void SetSlowdown(object sender, SelectionChangedEventArgs e)
        {
            Slowdown = slowdowns[IndexQualityOrRoughness];
            SetContourTime();
            SlowdownChanged?.Invoke();
        }

        private void SetContourTime()
        {
            if (Metal is null ||
                !MinutesDict["сталь"].ContainsKey((int)MainWindow.M.CorrectDestiny(0.75f * Destiny)))
            {
                Cutter = ContourTime = 0;
                return;
            }

            Cutter = (int)MainWindow.M.CorrectDestiny(0.75f * Destiny);

            if (Metal.Contains("амг", StringComparison.OrdinalIgnoreCase)
                || Metal.Contains("д16", StringComparison.OrdinalIgnoreCase))
                ContourTime = (int)Math.Ceiling((Way + 10) * Destiny / Slowdown / MinutesDict["алюминий"][Cutter]);
            else ContourTime = (int)Math.Ceiling((Way + 10) * Destiny / Slowdown / MinutesDict["сталь"][Cutter]);

            SetTotalTime();
        }

        public void SetTotalTime()
        {
            int time = ContourTime;

            if (MillingStack.Children.Count > 0)
                foreach (MillingControl milling in MillingStack.Children.OfType<MillingControl>())
                    if (milling.Time > 0) time += milling.Time;

            TotalTime = owner.TotalTime = time;
        }
    }
}