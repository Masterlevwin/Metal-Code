using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PartWindow.xaml
    /// </summary>
    public partial class PartWindow : Window
    {
        public CutControl Cut { get; set; }
        public List<PartControl> Parts { get; set; }
        public PartWindow(CutControl _cut, List<PartControl> _parts)
        {
            InitializeComponent();
            Cut = _cut;
            Parts = _parts;
            partsList.ItemsSource = Parts;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            DialogResult = true;
            Hide();
        }

        private void AddControl(object sender, RoutedEventArgs e)
        {
            foreach (PartControl p in Parts)
            {
                if (sender == Controls.Items[0]) p.AddControl(0);
                else if (sender == Controls.Items[1]) p.AddControl(1);
                else if (sender == Controls.Items[2]) p.AddControl(2);
            }
        }
    }
}
