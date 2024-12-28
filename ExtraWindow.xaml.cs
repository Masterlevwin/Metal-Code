using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для ExtraWindow.xaml
    /// </summary>
    public partial class ExtraWindow : Window
    {
        public string Header { get; set; }
        public ExtraWindow(string header)
        {
            InitializeComponent();
            Header = header;

            DataContext = this;
        }
    }
}
