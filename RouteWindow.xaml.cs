using System.Windows;
using System.IO;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для RouteWindow.xaml
    /// </summary>
    public partial class RouteWindow : Window
    {
        private DefaultDialogService dialogService = new();
        public RouteWindow()
        {
            InitializeComponent();
        }

        private void CreateBitmapFromVisual(object sender, RoutedEventArgs e)
        {
            if (dialogService.SaveFileDialog() == true && dialogService.FilePaths != null)
            {
                string _path = Path.GetDirectoryName(dialogService.FilePaths[0])
                    + "\\" + Path.GetFileNameWithoutExtension(dialogService.FilePaths[0]);

                MainWindow.CreateBitmapFromVisual(this, _path + ".bmp");
                MainWindow.M.StatusBegin($"Маршрут производства для расчета {MainWindow.M.ActiveOffer?.N} сохранен");
            }
        }
    }
}
