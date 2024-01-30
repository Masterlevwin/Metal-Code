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
        public delegate void PropsChanged(UserControl uc, bool b);
        public PropsChanged? PropertiesChanged;

        public readonly CutControl Cut;
        public Part Part { get; set; }

        public List<UserControl> UserControls = new();

        public PartControl(CutControl _cut, Part _part)
        {
            InitializeComponent();
            Cut = _cut;
            Part = _part;
            DataContext = Part;
        }

        private void AddControl(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                switch (btn.Name)
                {
                    case "BendBtn":
                        AddControl(0);
                        break;
                    case "WeldBtn":
                        AddControl(1);
                        break;
                    case "PaintBtn":
                        AddControl(2);
                        break;
                    case "ThreadBtn":
                        AddControl(3);
                        break;
                }
        }
        public void AddControl(int index)
        {
            switch (index)
            {
                case 0:
                    AddControl(new BendControl(this));
                    break;
                case 1:
                    AddControl(new WeldControl(this));
                    break;
                case 2:
                    AddControl(new PaintControl(this));
                    break;
                case 3:
                    AddControl(new ThreadControl(this));
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

        public void RemoveControl(UserControl uc)
        {
            if (uc is IPriceChanged work) PropertiesChanged -= work.SaveOrLoadProperties;
            UserControls.Remove(uc);
            ControlGrid.Children.Remove(uc);
        }
    }
}
