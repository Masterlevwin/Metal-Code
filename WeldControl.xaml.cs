using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WeldControl.xaml
    /// </summary>
    public partial class WeldControl : UserControl
    {
        private readonly WorkControl work;
        public WeldControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;
        }
    }
}
