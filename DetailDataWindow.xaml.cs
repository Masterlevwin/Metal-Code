using System.Windows;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Linq;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для DetailDataWindow.xaml
    /// </summary>
    public partial class DetailDataWindow : Window
    {
        public Detail Detail { get; set; }
        public TypeDetailControl Billet { get; set; }
        public ObservableCollection<DetailData> Details { get; set; } = new();

        public DetailDataWindow(Detail detail, TypeDetailControl type)
        {
            InitializeComponent();
            Detail = detail;
            Billet = type;
            DataContext = this;
        }

        private void DetailDataWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BilletType.Text = Billet.TypeDetailDrop.Text;

            if (Billet.TypeDetailDrop.Text == "Лист металла")
            {
                foreach (Work w in MainWindow.M.Works)
                    if (w.Name == "Лазерная резка")
                    {
                        Billet.WorkControls[0].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
            else
            {
                foreach (Work w in MainWindow.M.Works)
                    if (w.Name == "Труборез")
                    {
                        Billet.WorkControls[0].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
        }

        private void Add_DetailData(object sender, RoutedEventArgs e)
        {
            if (Billet.TypeDetailDrop.Text == "Лист металла") Details.Add(new() { Number = Details.Count + 1, IsLaser = true, IsPipe = false });
            else Details.Add(new() { Number = Details.Count + 1, IsLaser = false, IsPipe = true });
        }

        private void Accept(object sender, RoutedEventArgs e)
        {
            //получаем количество заготовок
            if (Billet.TypeDetailDrop.Text == "Лист металла") Billet.Count = SheetsCalculate();
            else
            {
                Billet.Count = TubesCalculate();

                //в случае трубы заодно устанавливаем количество погонных метров
                if (Billet.WorkControls[0].workType is PipeControl pipe)
                    pipe.SetMold($"{Billet.L * Billet.Count * 0.95f / 1000}");
            }

            //получаем путь резки и количество проколов в базовом виде
            if (Billet.WorkControls.Count > 0 && Billet.WorkControls[0].workType is ICut cut)
            {
                cut.Way = WayCalculate();
                cut.Pinhole = Details.Sum(d => d.Count) * 2 * Detail.Count;
            }

            FocusMainWindow();
        }

        private int SheetsCalculate()
        {
            if (Details.Count == 0) return 0;

            return (int)Math.Ceiling(Details.Sum(d => (d.Width + 10) * (d.Height + 10) * d.Count)
                        * Detail.Count / (Billet.A * Billet.B));
        }

        private int TubesCalculate()
        {
            if (Details.Count == 0) return 0;

            return (int)Math.Ceiling(Details.Sum(d => (d.Length + 20) * d.Count)
                        * Detail.Count / Billet.L);
        }

        private float WayCalculate()
        {
            if (Billet.Count == 0) return 0;

            return (float)Math.Ceiling(Details.Sum(d => (d.Width + d.Height + 10) * d.Count)
                        * Detail.Count / 500);      
        }

        private void Cancel(object sender, RoutedEventArgs e) { FocusMainWindow(); }

        private void FocusMainWindow(object sender, EventArgs e) { FocusMainWindow(); }

        private void FocusMainWindow()
        {
            Hide();
            MainWindow.M.IsEnabled = true;
        }

    }

    public class DetailData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public int Number { get; set; }

        private bool isLaser;
        public bool IsLaser
        {
            get => isLaser;
            set => isLaser = value;
        }

        private bool isPipe;
        public bool IsPipe
        {
            get => isPipe;
            set => isPipe = value;
        }

        private float width;
        public float Width
        {
            get => width;
            set
            {
                if (value != width)
                {
                    width = value;
                    OnPropertyChanged(nameof(Width));
                }
            }
        }        
        
        private float height;
        public float Height
        {
            get => height;
            set
            {
                if (value != height)
                {
                    height = value;
                    OnPropertyChanged(nameof(Height));
                }
            }
        }        
        
        private float length;
        public float Length
        {
            get => length;
            set
            {
                if (value != length)
                {
                    length = value;
                    OnPropertyChanged(nameof(Length));
                }
            }
        }

        private int count = 1;
        public int Count
        {
            get => count;
            set
            {
                if (value != count)
                {
                    count = value;
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        public DetailData() { }
    }
}
