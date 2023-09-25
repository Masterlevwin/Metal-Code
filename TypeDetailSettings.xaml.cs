using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для TypeDetailSettings.xaml
    /// </summary>
    public partial class TypeDetailSettings : Window
    {
        public TypeDetail TypeDetail { get; set; }
        public TypeDetailSettings(TypeDetail typeDetail)
        {
            InitializeComponent();
            TypeDetail = typeDetail;
            DataContext = TypeDetail;
        }

        void Accept_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
