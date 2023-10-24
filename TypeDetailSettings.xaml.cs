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
            string[] strings = TypeDetail.Sort.Split(',');
            if (strings.Length % 3 != 0)
            {
                MessageBox.Show("Недостаточно данных.\nПроверьте: Виды - количество значений должно быть кратно 3-м!");
                return;
            }
            else DialogResult = true;
        }
    }
}
