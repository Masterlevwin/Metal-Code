using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PartViewControl.xaml
    /// </summary>
    public partial class PartViewControl : UserControl
    {
        public Part Part { get; set; }

        public PartViewControl(Part part)
        {
            InitializeComponent();
            Part = part;
            DataContext = Part;
        }

        private void SetPicture(object sender, RoutedEventArgs e)       //метод вызывается при загрузке элемента Image (Image.Loaded) 
        {
            if (Part.ImageBytes != null) Picture.Source = MainWindow.CreateBitmap(Part.ImageBytes);
        }
    }
}
