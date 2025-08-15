using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для StandartPartWindow.xaml
    /// </summary>
    public partial class StandartPartWindow : Window
    {
        public DetailData DetailData { get; set; }
        public StandartPartWindow(DetailData detailData)
        {
            InitializeComponent();
            DetailData = detailData;
            DataContext = DetailData;
        }

        private void Accept(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
