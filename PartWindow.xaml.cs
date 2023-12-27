using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

            BendControl Bend = new(Cut);
            // формирование списка длин стороны гиба
            foreach (string s in Bend.BendDict[0.5f].Keys) BendDrop.Items.Add(s);
            WeldControl Weld = new(Cut);
            // формирование списка типов расчета сварки
            foreach (string s in Weld.TypeDict.Keys) WeldDrop.Items.Add(s);
            PaintControl Paint = new(Cut);
            // формирование списка типов расчета окраски
            foreach (string s in Paint.TypeDict.Keys) PaintDrop.Items.Add(s);
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
                //else if (sender == Controls.Items[3]) p.AddControl(3);
            }
        }

        public void AddBlockControl(int index)
        {
            foreach (PartControl p in Parts) p.AddControl(index);
        }

        private void SetProperty(object sender, TextChangedEventArgs e)
        {
            if (Parts.Count > 0 && sender is TextBox tBox)
            {
                switch (tBox.Name)
                {
                    case "Bend":
                        foreach (PartControl p in Parts)
                            foreach (BendControl item in p.UserControls.OfType<BendControl>()) item.SetBend(tBox.Text);
                        break;
                    case "Weld":
                        foreach (PartControl p in Parts)
                            foreach (WeldControl item in p.UserControls.OfType<WeldControl>()) item.SetWeld(tBox.Text);
                        break;
                    case "Paint":
                        foreach (PartControl p in Parts)
                            foreach (PaintControl item in p.UserControls.OfType<PaintControl>()) item.SetRal(tBox.Text);
                        break;
                }
            }
        }

        private void SetType(object sender, SelectionChangedEventArgs e)
        {
            if (Parts.Count > 0 && sender is ComboBox cBox)
            {
                switch (cBox.Name)
                {
                    case "BendDrop":
                        foreach (PartControl p in Parts)
                            foreach (BendControl item in p.UserControls.OfType<BendControl>()) item.SetShelf(cBox.SelectedIndex);
                        break;
                    case "WeldDrop":
                        foreach (PartControl p in Parts)
                            foreach (WeldControl item in p.UserControls.OfType<WeldControl>()) item.SetType(cBox.SelectedIndex);
                        break;
                    case "PaintDrop":
                        foreach (PartControl p in Parts)
                            foreach (PaintControl item in p.UserControls.OfType<PaintControl>()) item.SetType(cBox.SelectedIndex);
                        break;
                }
            }
        }
    }
}
