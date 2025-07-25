﻿using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PartsControl.xaml
    /// </summary>
    public partial class PartsControl : UserControl
    {
        public readonly UserControl owner;
        public List<PartControl> Parts { get; set; }

        public PartsControl(UserControl _owner, List<PartControl> _parts)
        {
            InitializeComponent();
            owner = _owner;
            Parts = _parts.OrderBy(p => p.Part.Title).ToList();
            partsList.ItemsSource = Parts;

            BendControl Bend = new(owner);
            // формирование списка длин стороны гиба
            foreach (string s in Bend.BendDict[0.5f].Keys) BendDrop.Items.Add(s);
            WeldControl Weld = new(owner);
            // формирование списка типов расчета сварки
            foreach (string s in Weld.TypeDict.Keys) WeldDrop.Items.Add(s);
            PaintControl Paint = new(owner);
            // формирование списка типов расчета окраски
            foreach (string s in Paint.structures) PaintDrop.Items.Add(s);
            RollingControl Roll = new(owner);
            // формирование списка сторон расчета вальцовки
            foreach (string s in Roll.Sides) RollDrop.Items.Add(s);
        }

        private void AddControl(object sender, RoutedEventArgs e)
        {
            foreach (PartControl p in Parts)
            {
                if (sender is Button btn)
                    switch (btn.Name)
                    {
                        case "BendBtn":
                            p.AddControl(0);
                            break;
                        case "WeldBtn":
                            p.AddControl(1);
                            break;
                        case "PaintBtn":
                            p.AddControl(2);
                            break;
                        case "ThreadBtn":
                            p.AddControl(3);
                            break;
                        case "CountersinkBtn":
                            p.AddControl(4);
                            break;
                        case "DrillingBtn":
                            p.AddControl(5);
                            break;
                        case "RollingBtn":
                            p.AddControl(6);
                            break;
                        case "ZincBtn":
                            p.AddControl(7);
                            break;
                        case "MillingBtn":
                            p.AddControl(8);
                            break;
                        case "RivetsBtn":
                            p.AddControl(9);
                            break;
                        case "AquaBtn":
                            p.AddControl(10);
                            break;
                    }
            }
        }

        private void SetPropertyThread(object sender, RoutedEventArgs e)      //обработчик события LostFocus для текстовых полей диаметра отверстий
        {
            if (Parts.Count > 0 && sender is TextBox tBox && tBox.Text != "")
            {
                switch (tBox.Name)
                {
                    case "Wide1":
                        foreach (PartControl p in Parts)
                            foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
                                if (item.CharName == "Р") item.SetWide(tBox.Text);
                        break;
                    case "Wide2":
                        foreach (PartControl p in Parts)
                            foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
                                if (item.CharName == "З") item.SetWide(tBox.Text);
                        break;
                    case "Wide3":
                        foreach (PartControl p in Parts)
                            foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
                                if (item.CharName == "С") item.SetWide(tBox.Text);
                        break;
                    case "Wide4":
                        foreach (PartControl p in Parts)
                            foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
                                if (item.CharName == "Зк") item.SetWide(tBox.Text);
                        break;
                }
            }
        }

        private void SetProperty(object sender, RoutedEventArgs e)      //обработчик события LostFocus для текстового поля окраски
        {
            if (Parts.Count > 0 && sender is TextBox tBox && tBox.Text != "")
                foreach (PartControl p in Parts)
                    foreach (PaintControl item in p.UserControls.OfType<PaintControl>()) item.SetRal(tBox.Text);
        }

        private void SetProperty(object sender, TextChangedEventArgs e)
        {
            if (Parts.Count > 0 && sender is TextBox tBox && tBox.Text != "")
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
                    case "Holes1":
                        foreach (PartControl p in Parts)
                            foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
                                if (item.CharName == "Р") item.SetHoles(tBox.Text);
                        break;
                    case "Holes2":
                        foreach (PartControl p in Parts)
                            foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
                                if (item.CharName == "З") item.SetHoles(tBox.Text);
                        break;
                    case "Holes3":
                        foreach (PartControl p in Parts)
                            foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
                                if (item.CharName == "С") item.SetHoles(tBox.Text);
                        break;
                    case "Holes4":
                        foreach (PartControl p in Parts)
                            foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>())
                                if (item.CharName == "Зк") item.SetHoles(tBox.Text);
                        break;
                    case "TotalTime":
                        foreach (PartControl p in Parts)
                            foreach (MillingTotalControl item in p.UserControls.OfType<MillingTotalControl>())
                                item.SetTotalTime(tBox.Text);
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
                    case "RollDrop":
                        foreach (PartControl p in Parts)
                            foreach (RollingControl item in p.UserControls.OfType<RollingControl>()) item.SetType(cBox.SelectedIndex);
                        break;
                }
            }
        }

        private void SortDetails(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                switch (btn.Content)
                {
                    //case "И>":
                    //    partsList.ItemsSource = Parts.OrderBy(p => p.Part.Title).ToList();
                    //    break;
                    case "Г>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is BendControl))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is BendControl)))).ToList();
                        break;
                    case "Св>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is WeldControl))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is WeldControl)))).ToList();
                        break;
                    case "О>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is PaintControl))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is PaintControl)))).ToList();
                        break;
                    case "Р>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is ThreadControl thread && thread.CharName == "Р"))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is ThreadControl thread && thread.CharName == "Р")))).ToList();
                        break;
                    case "З>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is ThreadControl thread && thread.CharName == "З"))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is ThreadControl thread && thread.CharName == "З")))).ToList();
                        break;
                    case "С>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is ThreadControl thread && thread.CharName == "С"))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is ThreadControl thread && thread.CharName == "С")))).ToList();
                        break;
                    case "Зк>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is ThreadControl thread && thread.CharName == "Зк"))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is ThreadControl thread && thread.CharName == "Зк")))).ToList();
                        break;
                    case "В>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is RollingControl))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is RollingControl)))).ToList();
                        break;
                    case "Ц>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is ZincControl))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is ZincControl)))).ToList();
                        break;
                    case "Ф>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is MillingTotalControl))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is MillingTotalControl)))).ToList();
                        break;
                    case "А>":
                        partsList.ItemsSource = Parts.Where(p => p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is AquaControl))).Union(Parts.Where(p => !p.UserControls.Contains(
                            p.UserControls.FirstOrDefault(u => u is AquaControl)))).ToList();
                        break;
                }
        }

        private void ShowNesting(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cBox && cBox is not null)
            {
                if (cBox.IsChecked is true && owner is ICut cut && cut.Items?.Count > 0)
                {
                    List<Image> images = new();
                    foreach (LaserItem item in cut.Items)
                    {
                        if (item.imageBytes is not null)
                        {
                            Image _img = new()
                            {
                                Source = MainWindow.CreateBitmap(item.imageBytes),
                                Margin = new Thickness(10, 5, 0, 5),
                                Height = 370
                            };
                            images.Add(_img);
                        }
                    }
                    imagesList.ItemsSource = images;

                    imagesList.Visibility = Visibility.Visible;
                    partsList.Visibility = Visibility.Collapsed;

                }
                else
                {
                    partsList.Visibility = Visibility.Visible;
                    imagesList.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ShowAssemblyWindow(object sender, RoutedEventArgs e)
        {
            AssemblyWindow.A.CurrentParts.Clear();
            foreach (Part part in Parts.Select(p => p.Part))
                AssemblyWindow.A.CurrentParts.Add(part);
            AssemblyWindow.A.Show();
        }

        private void SetDefaultBends(object sender, RoutedEventArgs e)
        {
            foreach (PartControl p in Parts)
                foreach (BendControl item in p.UserControls.OfType<BendControl>()) item.SetGroup("-");
        }
    }
}
