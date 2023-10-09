using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PaintControl.xaml
    /// </summary>
    public partial class PaintControl : UserControl
    {
        //Text="{Binding Price, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:PaintControl}}}"
        public static readonly DependencyProperty MyPropertyProperty =
            DependencyProperty.Register("Price", typeof(float), typeof(PaintControl));
        public float Price
        {
            get { return (float)GetValue(MyPropertyProperty); }
            set { SetValue(MyPropertyProperty, value); }
        }

        public float Square { get; set; }

        private readonly WorkControl work;
        public PaintControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;
            work.type.Priced += PriceChanged;
        }

        private void SetSquare(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (float.TryParse(tBox.Text, out float p)) SetSquare(p);
        }
        public void SetSquare(float _square)
        {
            Square = _square;
            PriceChanged();
        }

        private void PriceChanged()
        {
            Price = work.Result = Square * work.type.Count * work.type.det.Count + work.Price;

            work.type.det.PriceResult();
        }
    }
}
