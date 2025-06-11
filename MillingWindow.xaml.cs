using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для MillingWindow.xaml
    /// </summary>
    public partial class MillingWindow : Window
    {
        public readonly UserControl owner;

        public MillingWindow(UserControl _control)
        {
            InitializeComponent();
            owner = _control;
        }

        private void MillingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (owner is WorkControl work) DataContext = work.type.det.Detail;
            else if (owner is PartControl part) DataContext = part.Part;
        }

        private void AddMilling(object sender, RoutedEventArgs e)
        {

        }
    }
}