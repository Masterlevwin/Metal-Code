using Metal_Code.Utils;
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
        }

        private void Remove(object sender, RoutedEventArgs e) { Remove(); }
        public void Remove()
        {
            MainWindow.M.BasketControls.Remove(this);
            MainWindow.M.DetailsStack.Children.Remove(this);
        }
    }
}