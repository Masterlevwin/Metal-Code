using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для MetalSettings.xaml
    /// </summary>
    public partial class MetalSettings : Window
    {
        public Metal Metal {  get; set; }
        public MetalSettings(Metal metal)
        {
            InitializeComponent();
            Metal = metal;
            DataContext = Metal;
        }

        void Accept_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
