using System;
using System.Collections.Generic;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для BendWindow.xaml
    /// </summary>
    public partial class BendWindow : Window
    {
        public BendControl BendControl { get; set; }
        public BendWindow(BendControl _bendControl, List<PartControl> _parts)
        {
            InitializeComponent();
            BendControl = _bendControl;

            MainWindow.M.IsEnabled = false;
            partsList.ItemsSource = _parts;
            foreach (PartControl part in _parts) if (part.Bends.Count == 0) part.AddBend(_bendControl);
        }

        private void FocusMainWindow(object sender, EventArgs e)
        {
            BendControl.PriceChanged();
            MainWindow.M.IsEnabled = true;
        }
    }
}
