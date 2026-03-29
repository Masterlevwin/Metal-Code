using HandyControl.Controls;
using HandyControl.Data;
using Metal_Code.Utils;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для BasketControl.xaml
    /// </summary>
    public partial class BasketControl : UserControl
    {
        public Part Basket { get; set; }
        public BasketControl(Part basket)
        {
            InitializeComponent();
            Basket = basket;
            Basket.ImageBytes = DefaultResources.DefaultPartImage;
            DataContext = Basket;

            MetalDrop.ItemsSource = MainWindow.M.Metals.OrderBy(x => x.Id);
        }

        private void Remove(object sender, RoutedEventArgs e) { Remove(); }
        public void Remove()
        {
            MainWindow.M.BasketControls.Remove(this);
            MainWindow.M.DetailsStack.Children.Remove(this);
        }

        private void NumericUpDown_Count_ValueChanged(object sender, FunctionEventArgs<double> e)
        {
            if (sender is NumericUpDown nud && nud.DataContext is Part part)
            {
                part.NotifyTotalChanged();
            }
        }
    }
}