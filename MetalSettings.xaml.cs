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
            if (Metal.Name != null && (Metal.WayPrice == "" || Metal.PinholePrice == "" || Metal.MoldPrice == ""))
            {
                MessageBox.Show("Недостаточно данных.\nПроверьте цены - все поля должны быть заполнены!");
                return;
            }
            DialogResult = true;
        }
    }
}
