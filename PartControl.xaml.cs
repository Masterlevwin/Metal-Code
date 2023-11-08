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
            if (sender == Controls.Items[0])
            {
                BendControl bend = new(this);
                AddControl(bend);
            }
            else if (sender == Controls.Items[1])
            {
                WeldControl weld = new(this);
                AddControl(weld);
            }
        }
        public void AddControl(UserControl uc)
        {
            if (UserControls.Count > 0) uc.Margin = new Thickness(0, UserControls[^1].Margin.Top + 25, 0, 0);

            UserControls.Add(uc);
            ControlGrid.Children.Add(uc);

            Grid.SetRow(uc, 1);
        }

        /*private void Remove(object sender, RoutedEventArgs e)
        {
            if (UserControls.Count == 1) return;
            Remove();
        }
        public void Remove()
        {
            UpdatePosition(false);
            UserControls.Remove(this);
            BendGrid.Children.Remove(this);
        }

        public void UpdatePosition(bool direction)
        {
            int numB = UserControls.IndexOf(this);
            if (UserControls.Count > 1)
            {
                for (int i = numB + 1; i < UserControls.Count; i++)
                {
                    UserControls[i].Margin = new Thickness(0,
                        direction ? UserControls[i].Margin.Top + 25 : UserControls[i].Margin.Top - 25, 0, 0);
                }
            }
        }*/
    }
}
