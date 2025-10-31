using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для BasketControl.xaml
    /// </summary>
    public partial class BasketControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string _name = string.Empty;
        private float _price;
        private int _quantity = 1;

        public string NameBasket
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(NameBasket));
                }
            }
        }

        public float Price
        {
            get => _price;
            set
            {
                if (_price != value)
                {
                    _price = value;
                    OnPropertyChanged(nameof(Price));
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value && value >= 0)
                {
                    _quantity = value;
                    OnPropertyChanged(nameof(Quantity));
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        // Для отображения в UI (например, "Наименование (кол-во шт)")
        public string DisplayName => $"{NameBasket} ({Quantity} шт)";
        public float Total => Price * Quantity;

        public BasketControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Remove(object sender, System.Windows.RoutedEventArgs e)
        {

        }
    }

    public class Basket
    {
        public string Name { get; set; } = string.Empty;
        public float Price { get; set; }
        public int Quantity { get; set; }
    }
}
