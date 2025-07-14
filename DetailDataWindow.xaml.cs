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
            DataContext = Detail;

            Billet = type;
        }

        private void Add_DetailData(object sender, RoutedEventArgs e)
        {
            if (Billet.TypeDetailDrop.Text == "Лист металла")
            {
                Details.Add(new() { Width = 100, Height = 200, Count = 5, IsPipe = false });
                MessageBox.Show($"{Details.Count}");
            }
            else
            {
                Details.Add(new() { Length = 2000, Count = 5, IsPipe = true });
                MessageBox.Show($"{Details.Count}");
            }
        }

        private void Accept(object sender, RoutedEventArgs e)
        {
            if (Billet.TypeDetailDrop.Text == "Лист металла") Billet.Count = SheetsCalculate();
            else Billet.Count = TubesCalculate();

            FocusMainWindow();
        }

        private int SheetsCalculate()
        {
            if (Details.Count == 0) return 0;

            int result = (int)Math.Ceiling(Details.Sum(d => (d.Width * d.Height + 20) * d.Count)
                        * Detail.Count / (Billet.A * Billet.B));

            if (result > 0)
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Лазерная резка")
                    {
                        Billet.WorkControls[0].WorkDrop.SelectedItem = w;
                        break;
                    }
            return result;
        }

        private int TubesCalculate()
        {
            if (Details.Count == 0) return 0;

            int result = (int)Math.Ceiling(Details.Sum(d => (d.Length + 20) * d.Count)
                        * Detail.Count / Billet.L);

            if (result > 0)
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Труборез")
                    {
                        Billet.WorkControls[0].WorkDrop.SelectedItem = w;
                        if (Billet.WorkControls[0].workType is PipeControl pipe)
                            pipe.SetMold($"{Billet.L * Billet.Count * 0.95f / 1000}");
                        break;
                    }
            return result;
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            FocusMainWindow();
        }

        private void FocusMainWindow(object sender, EventArgs e)
        {
            FocusMainWindow();
        }

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

        private bool isPipe;
        public bool IsPipe
        {
            get => isPipe;
            set => isPipe = value;
        }

        public DetailData() { }
    }
}
