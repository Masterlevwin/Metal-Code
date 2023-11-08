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
    }
}
