using System.Collections.Generic;
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

        public List<UserControl> UserControls = new();

        public PartControl(Part _part)
        {
            InitializeComponent();
            Part = _part;
            DataContext = Part;
        }

        private void AddControl(object sender, RoutedEventArgs e)
        {
            if (sender == Controls.Items[0]) AddControl(0);
            else if (sender == Controls.Items[1]) AddControl(1);
        }
        public void AddControl(int index)
        {
            switch (index)
            {
                case 0:
                    BendControl bend = new(this);
                    AddControl(bend);
                    break;
                case 1:
                    WeldControl weld = new(this);
                    AddControl(weld);
                    break;
            }
        }
        public void AddControl(UserControl uc)
        {
            if (UserControls.Count > 0) uc.Margin = new Thickness(0, UserControls[^1].Margin.Top + 25, 0, 0);

            UserControls.Add(uc);
            ControlGrid.Children.Add(uc);

            Grid.SetRow(uc, 1);
        }
    }
}
