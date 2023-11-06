using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PartControl.xaml
    /// </summary>
    public partial class PartControl : UserControl
    {
        public Part Part { get; set; }
        public PartControl(Part _part)
        {
            InitializeComponent();
            Part = _part;
            DataContext = Part;
        }

        public List<PartBendControl> Bends = new();
        public void AddBend(BendControl _bendControl)
        {
            PartBendControl partBend = new(_bendControl, this);

            if (Bends.Count > 0) partBend.Margin = new Thickness(0, Bends[^1].Margin.Top + 25, 0, 0);

            Bends.Add(partBend);
            BendGrid.Children.Add(partBend);

            Grid.SetRow(partBend, 1);
        }
    }
}
