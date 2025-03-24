using System.Windows;
using System;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для DetailDataWindow.xaml
    /// </summary>
    public partial class DetailDataWindow : Window
    {
        public Detail Detail { get; set; }
        public TypeDetailControl Billet { get; set; }

        public DetailDataWindow(Detail detail, TypeDetailControl type)
        {
            InitializeComponent();
            Detail = detail;
            DataContext = Detail;

            Billet = type;
            if (Billet.TypeDetailDrop.Text == "Лист металла")
            {
                LengthStack.Visibility = Visibility.Hidden;
            }
            else
            {
                WidthStack.Visibility = Visibility.Hidden;
                HeightStack.Visibility = Visibility.Hidden;
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
            if (WidthDetail.Text == "" || HeightDetail.Text == "") return 0;

            int result = (int)Math.Ceiling(
                (MainWindow.Parser(WidthDetail.Text) * MainWindow.Parser(HeightDetail.Text) + 20) *
                Detail.Count / (Billet.A * Billet.B));

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
            if (LengthDetail.Text == "") return 0;

            int result = (int)Math.Ceiling((MainWindow.Parser(LengthDetail.Text) + 20) * Detail.Count / Billet.L);

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
}
