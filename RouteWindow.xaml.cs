using System.Windows;
using System.IO;
using System;

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
            if (MainWindow.M.ActiveOffer?.Act is null)
            {
                MessageBox.Show($"Не удалось создать маршрут производства для текущего расчета. Попробуйте пересохранить расчет заново.");
                return;
            }

            DirectoryInfo dir = Directory.CreateDirectory(Path.GetDirectoryName(MainWindow.M.ActiveOffer.Act) + "\\" + "Маршрут");

            decimal pagesCount = Math.Ceiling(decimal.Parse($"{DetailStack.Children.Count / 9}"));
            for (int i = 0; i < pagesCount + 1; i++)
            {
                MainWindow.CreateBitmapFromVisual(this, dir + "\\" + $"{i}.bmp");
                ScrollContent.PageDown();
                UpdateLayout();
            }
            MainWindow.M.StatusBegin($"Маршрут производства для расчета {MainWindow.M.ActiveOffer?.N} сохранен");
        }
    }
}
