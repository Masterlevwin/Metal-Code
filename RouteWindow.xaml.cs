using System.Windows;
using System.IO;
using Microsoft.Win32;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для RouteWindow.xaml
    /// </summary>
    public partial class RouteWindow : Window
    {
        public RouteWindow()
        {
            InitializeComponent();
        }

        private void CreateBitmapFromVisual(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new() { Filter = "Изображение (*.bmp)|*.bmp|All files (*.*)|*.*" };

            if (saveFileDialog.ShowDialog() == true && saveFileDialog.FileNames != null)
            {
                string _path = Path.GetDirectoryName(saveFileDialog.FileNames[0])
                    + "\\" + Path.GetFileNameWithoutExtension(saveFileDialog.FileNames[0]);

                MainWindow.CreateBitmapFromVisual(this, _path + ".bmp");
                MainWindow.M.StatusBegin($"Маршрут производства для расчета {MainWindow.M.ActiveOffer?.N} сохранен");
            }
        }
    }
}
