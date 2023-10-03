using System.Windows.Controls;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PaintControl.xaml
    /// </summary>
    public partial class PaintControl : UserControl
    {
        public float Price { get; set; }

        private readonly WorkControl work;
        public PaintControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;
            DataContext = this;
        }

        private void SetPaint(string s)
        {
            if(float.TryParse(s, out float p)) Price += p;
            PaintPrice.Text = $"{Price}";
        }

        private void SetPaint(object sender, RoutedEventArgs e)
        {
            SetPaint(PaintText.Text);
        }
    }
}
