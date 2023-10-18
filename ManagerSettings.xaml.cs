using System.Windows;


namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для ManagerSettings.xaml
    /// </summary>
    public partial class ManagerSettings : Window
    {
        public Manager Manager { get; set; }
        public ManagerSettings(Manager manager)
        {
            InitializeComponent();
            Manager = manager;
            DataContext = Manager;
        }

        void Accept_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
