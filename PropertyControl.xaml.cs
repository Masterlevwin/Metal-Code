using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PropertyControl.xaml
    /// </summary>
    public partial class PropertyControl : UserControl
    {
        //Text="{Binding Стоимость, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:PropertyControl}}}"
        public static readonly DependencyProperty MyPropertyProperty =
            DependencyProperty.Register("Price", typeof(float), typeof(PropertyControl));
        public float Price
        {
            get { return (float)GetValue(MyPropertyProperty); }
            set { SetValue(MyPropertyProperty, value); }
        }

        private readonly WorkControl work;
        public PropertyControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;
            work.type.Priced += PriceChanged;
        }

        public void PriceChanged()
        {
            Price = work.Result = work.type.Count * work.type.det.Count * work.Price;

            work.type.det.PriceResult();
        }
    }
}
