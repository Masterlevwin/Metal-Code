using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WorkSettings.xaml
    /// </summary>
    public partial class WorkSettings : Window
    {
        public Work Work { get; set; }
        public WorkSettings(Work work)
        {
            InitializeComponent();
            Work = work;
            DataContext = Work;
        }

        void Accept_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
