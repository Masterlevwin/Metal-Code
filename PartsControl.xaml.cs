using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PartsControl.xaml
    /// </summary>
    public partial class PartsControl : UserControl
    {
        public readonly UserControl owner;
        public ObservableCollection<PartControl> Parts { get; set; }

        private readonly string[] standartParts = { "Прямоугольник", "Круг" };

        public PartsControl(UserControl _owner, ObservableCollection<PartControl> _parts)
        {
            InitializeComponent();
            owner = _owner;
            Parts = _parts;
            partsList.ItemsSource = Parts;
            StandartPartsDrop.ItemsSource = standartParts;

            if (Parts.Count > 0) StandartPartBtn.IsEnabled = false;

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
                                Height = 320
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

        private void SetDefaultBends(object sender, RoutedEventArgs e)
        {
            foreach (PartControl p in Parts)
                foreach (BendControl item in p.UserControls.OfType<BendControl>()) item.SetGroup("-");
        }

        //стандартные детали
        private void Add_StandartPart(object sender, RoutedEventArgs e)
        {
            if (StandartPartsDrop.SelectedItem is string title
                && owner is CutControl cut && cut.work.type.MetalDrop.SelectedItem is Metal metal)
            {
                StandartPartWindow standartPartWindow = new(new()
                {
                    Title = $"{title} {Parts.Count + 1}",
                    IsLaser = true,
                    IsPipe = false,
                    Geometries = title switch
                    {
                        "Круг" => GetCircleGeometryDescriptors(30),
                        _ => GetRectangleGeometryDescriptors(60, 60)
                    }
                });

                if (standartPartWindow.ShowDialog() == true)
                {
                    DetailData detailData = standartPartWindow.DetailData;

                    Part part = new()
                    {
                        Title = detailData.Title,
                        Count = detailData.Count,
                        Metal = metal.Name,
                        Destiny = cut.work.type.S,
                        Geometries = detailData.Geometries,
                        Way = (float)Math.Round(detailData.Width + detailData.Height / 1000, 3),
                        Mass = (float)Math.Round(detailData.Height * detailData.Width * cut.work.type.S * metal.Density / 1000000, 3),
                    };
                    part.PropsDict[100] = new() { $"{detailData.Width}", $"{detailData.Height}", $"{detailData.Width}x{detailData.Height}" };

                    PartControl partControl = new(owner, cut.work, part);
                    if (part.Geometries?.Count > 0)
                    {
                        partControl.Picture.Visibility = Visibility.Collapsed;
                        partControl.GeometryCanvas.Visibility = Visibility.Visible;

                        CanvasHelper.SetGeometryDescriptors(partControl.GeometryCanvas, part.Geometries);
                    }
                    Parts.Add(partControl);

                    if (cut.Parts != null && !cut.Parts.Contains(partControl)) cut.Parts.Add(partControl);
                    else cut.Parts = new() { partControl };

                    if (cut.PartDetails != null && !cut.PartDetails.Contains(part)) cut.PartDetails.Add(part);
                    else cut.PartDetails = new() { part };

                    cut.MassTotal = Parts.Select(p => p.Part).Sum(p => p.Mass * p.Count);
                    cut.WayTotal = Parts.Select(p => p.Part).Sum(p => p.Way * p.Count);

                    //определяем деталь как комплект деталей
                    if (!cut.work.type.det.Detail.IsComplect) cut.work.type.det.IsComplectChanged("Комплект деталей");
                }
            }
        }

        public static ObservableCollection<IGeometryDescriptor> GetRectangleGeometryDescriptors(
            double width,
            double height,
            double cornerRadius = 0,
            Point startPoint = new Point())
        {
            var descriptors = new ObservableCollection<IGeometryDescriptor>();

            if (width <= 0 || height <= 0)
                return descriptors;

            // Ограничиваем радиус
            if (cornerRadius > 0)
                cornerRadius = Math.Min(cornerRadius, Math.Min(width, height) / 2);

            // Определяем углы
            double x = startPoint.X;
            double y = startPoint.Y;
            double right = x + width;
            double bottom = y + height;
            double startX = x + cornerRadius;
            double endX = right - cornerRadius;
            double startY = y + cornerRadius;
            double endY = bottom - cornerRadius;

            Point prevPoint = new Point(startX, y);

            // Утилиты
            void AddLineTo(Point endPoint)
            {
                if (prevPoint != endPoint)
                {
                    descriptors.Add(new LineDescriptor
                    {
                        Start = prevPoint,
                        End = endPoint
                    });
                    prevPoint = endPoint;
                }
            }

            void AddArcTo(Point endPoint, Point centerArc, SweepDirection sweep)
            {
                descriptors.Add(new ArcDescriptor
                {
                    StartPoint = prevPoint,
                    EndPoint = endPoint,
                    Size = new Size(cornerRadius, cornerRadius),
                    IsLargeArc = false,
                    SweepDirection = sweep
                });
                prevPoint = endPoint;
            }

            // 1. Верхняя сторона
            AddLineTo(new Point(endX, y));

            if (cornerRadius > 0)
            {
                // 2. Верхний правый угол
                AddArcTo(new Point(right, startY), new Point(endX, startY), SweepDirection.Clockwise);

                // 3. Правая сторона
                AddLineTo(new Point(right, endY));

                // 4. Нижний правый угол
                AddArcTo(new Point(endX, bottom), new Point(endX, endY), SweepDirection.Clockwise);

                // 5. Нижняя сторона
                AddLineTo(new Point(startX, bottom));

                // 6. Нижний левый угол
                AddArcTo(new Point(x, endY), new Point(startX, endY), SweepDirection.Clockwise);

                // 7. Левая сторона
                AddLineTo(new Point(x, startY));

                // 8. Верхний левый угол
                AddArcTo(new Point(startX, y), new Point(startX, startY), SweepDirection.Clockwise);
            }
            else
            {
                // Прямые углы
                AddLineTo(new Point(right, y));
                AddLineTo(new Point(right, bottom));
                AddLineTo(new Point(x, bottom));
                AddLineTo(new Point(x, y));
            }

            return descriptors;
        }

        public static ObservableCollection<IGeometryDescriptor> GetCircleGeometryDescriptors(
            double radius,
            Point center = new Point())
        {
            var descriptors = new ObservableCollection<IGeometryDescriptor>();

            if (radius <= 0)
                return descriptors;

            // Нормализуем центр
            Point c = new Point(center.X + radius, center.Y + radius);

            // Точки для двух полуокружностей
            Point startPoint = new Point(c.X + radius, c.Y); // правая точка (0°)
            Point topPoint = new Point(c.X, c.Y - radius);   // верх (90°)
            Point leftPoint = new Point(c.X - radius, c.Y);  // лево (180°)
            Point bottomPoint = new Point(c.X, c.Y + radius); // низ (270°)

            // --- Первая половина: 0° → 180° (сверху)
            descriptors.Add(new ArcDescriptor
            {
                StartPoint = startPoint,
                EndPoint = leftPoint,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false // дуга < 180°
            });

            // --- Вторая половина: 180° → 360° (снизу)
            descriptors.Add(new ArcDescriptor
            {
                StartPoint = leftPoint,
                EndPoint = startPoint,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false
            });

            return descriptors;
        }
    }
}
