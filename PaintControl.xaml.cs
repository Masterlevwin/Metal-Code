using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PaintControl.xaml
    /// </summary>
    public partial class PaintControl : UserControl
    {
        //Text="{Binding Total, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:PaintControl}}}"
        public static readonly DependencyProperty MyPropertyPrice =
            DependencyProperty.Register("Price", typeof(float), typeof(PaintControl));
        public float Price
        {
            get { return (float)GetValue(MyPropertyPrice); }
            set { SetValue(MyPropertyPrice, value); }
        }

        //Text="{Binding Ratio, Mode=TwoWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:PaintControl}}}"
        public static readonly DependencyProperty MyPropertyRatio =
            DependencyProperty.Register("Ratio", typeof(string), typeof(PaintControl));
        public string Ratio
        {
            get { return (string)GetValue(MyPropertyRatio); }
            set { SetValue(MyPropertyRatio, value); }
        }

        //Text="{Binding Square, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:PaintControl}}}"
        public static readonly DependencyProperty MyPropertySquare =
            DependencyProperty.Register("Square", typeof(string), typeof(PaintControl));
        public string Square
        {
            get { return (string)GetValue(MyPropertySquare); }
            set { SetValue(MyPropertySquare, value); }
        }

        private readonly WorkControl work;
        public PaintControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;
            work.type.Priced += PriceChanged;
            work.PropertiesChanged += SaveOrLoadProperties;
        }

        private void SetSquare(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetSquare(tBox.Text);
        }
        public void SetSquare(string _square)
        {
            Square = _square;
            PriceChanged();
        }

        private void SetRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRatio(tBox.Text);
        }
        private void SetRatio(string _ratio)
        {
            Ratio = _ratio;
            PriceChanged();
        }

        private void PriceChanged()
        {
            float _paint = 0;
            if (float.TryParse(Square, out float p)) _paint = p;

            float _ratio = 1;
            if (float.TryParse(Ratio, out float r)) _ratio = r;

            Price = work.Result = _paint * _ratio * work.type.Count * work.type.det.Count + work.Price;

            work.type.det.PriceResult();
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add(Square);
                w.propsList.Add(Ratio);
            }
            else
            {
                SetSquare(w.propsList[0]);
                SetRatio(w.propsList[1]);
            }
        }
    }
}
