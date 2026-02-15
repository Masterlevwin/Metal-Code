using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для StandartPartWindow.xaml
    /// </summary>
    public partial class StandartPartWindow : Window
    {
        public Part StandartPart { get; set; }
        public StandartPartWindow(Part standartPart)
        {
            InitializeComponent();
            StandartPart = standartPart;
            DataContext = StandartPart;
        }

        private void Accept(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
