using MathNet.Numerics.Statistics.Mcmc;
using Metal_Code.Utils;
using NPOI.SS.UserModel;
using Org.BouncyCastle.Bcpg;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

        private string[] works = { "Выберите работу", "Гибка", "Сварка", "Окраска", "Резьба", "Зенковка", "Сверловка",
                                    "Вальцовка", "Цинкование", "Фрезеровка", "Заклепки", "Аквабластинг"};

        public PartsControl(UserControl _owner, ObservableCollection<PartControl> _parts)
        {
            InitializeComponent();
            owner = _owner;
            Parts = _parts;
            partsList.ItemsSource = Parts;
            StandartPartsDrop.ItemsSource = standartParts;

            // Заполняем ComboBox работами
            WorksDrop.ItemsSource = works;

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

        // показываем выбранный блок работы
        private void SetVisibleControl(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string workName)
            {
                // Находим индекс работы в массиве `works`
                int index = Array.IndexOf(works, workName);
                if (index >= 1)
                {
                    WorksDrop.SelectedIndex = 0;        //сбрасываем выбор

                    //активируем выбранную работу
                    WorksGrid.Children[index - 1].Visibility = Visibility.Visible;

                    //остальные работы скрываем
                    foreach (StackPanel stack in WorksGrid.Children)
                        if (stack != WorksGrid.Children[index - 1])
                            stack.Visibility = Visibility.Collapsed;

                    //добавляем блок на каждую деталь
                    foreach (PartControl p in Parts)
                        p.AddControl(index - 1);
                }
            }
        }

        // сортировка по выбранной работе
        private void SortDetails(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string workName)
                return;

            int index = Array.IndexOf(works, workName);
            if (index < 1) return;

            IEnumerable<PartControl> sortedParts = index switch
            {
                1 => SortByControlType<BendControl>(),
                2 => SortByControlType<WeldControl>(),
                3 => SortByControlType<PaintControl>(),
                4 => SortByThreadChar("Р"),
                5 => SortByThreadChar("З"),
                6 => SortByThreadChar("С"),
                7 => SortByControlType<RollingControl>(),
                8 => SortByControlType<ZincControl>(),
                9 => SortByControlType<MillingTotalControl>(),
                10 => SortByThreadChar("Зк"),
                11 => SortByControlType<AquaControl>(),
                _ => Parts
            };

            partsList.ItemsSource = sortedParts.ToList();
        }

        // Вспомогательный метод: сортировка по типу UserControl
        private IEnumerable<PartControl> SortByControlType<T>() where T : UserControl
        {
            var withControl = Parts.Where(p => p.UserControls.OfType<T>().Any());
            var withoutControl = Parts.Except(withControl);
            return withControl.Concat(withoutControl);
        }

        // Вспомогательный метод: сортировка по ThreadControl с заданным CharName
        private IEnumerable<PartControl> SortByThreadChar(string charName)
        {
            var withControl = Parts.Where(p => p.UserControls.OfType<ThreadControl>()
                .Any(tc => tc.CharName == charName));
            var withoutControl = Parts.Except(withControl);
            return withControl.Concat(withoutControl);
        }

        // сбрасываем выбор работы
        private void SelectedWork(object sender, SelectionChangedEventArgs e) { WorksDrop.SelectedIndex = 0; }

        // добавляем работу во все детали
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

        // обработчик события LostFocus для текстовых полей диаметра отверстий
        private void SetPropertyThread(object sender, RoutedEventArgs e)
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
        
        // обработчик события LostFocus для текстового поля окраски
        private void SetProperty(object sender, RoutedEventArgs e)
        {
            if (Parts.Count > 0 && sender is TextBox tBox && tBox.Text != "")
                foreach (PartControl p in Parts)
                    foreach (PaintControl item in p.UserControls.OfType<PaintControl>()) item.SetRal(tBox.Text);
        }

        // обработчик события TextChangeds для текстовых полей
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

        // обработчик события SelectionChanged для дропов работ
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

        // показать или скрыть раскладки
        private void ShowNesting(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if ($"{btn.Content}" == "Показать раскладки" && owner is ICut cut && cut.Items?.Count > 0)
                {
                    List<Image> images = new();
                    foreach (LaserItem item in cut.Items)
                    {
                        if (item.imageBytes is not null)
                        {
                            Image _img = new()
                            {
                                Source = MainWindow.CreateBitmap(item.imageBytes),
                                Margin = new Thickness(5),
                                Height = 320
                            };
                            images.Add(_img);
                        }
                    }
                    imagesList.ItemsSource = images;

                    imagesScroll.Visibility = Visibility.Visible;
                    partsScroll.Visibility = Visibility.Collapsed;
                    btn.Content = "Скрыть раскладки";
                }
                else
                {
                    partsScroll.Visibility = Visibility.Visible;
                    imagesScroll.Visibility = Visibility.Collapsed;
                    btn.Content = "Показать раскладки";
                }
            }
        }

        // обработчик кнопки сброса групп гибки
        private void SetDefaultBends(object sender, RoutedEventArgs e)
        {
            foreach (PartControl p in Parts)
                foreach (BendControl item in p.UserControls.OfType<BendControl>()) item.SetGroup("-");
        }

        // добавить стандартную деталь
        private void Add_StandartPart(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string title
                && owner is CutControl cut
                && cut.work.type.MetalDrop.SelectedItem is Metal metal)
            {
                // Создаём параметризованную стандартную деталь
                var standartPart = new Part
                {
                    Title = $"{title} {Parts.Count + 1}",
                    Count = 1,
                    Metal = metal.Name,
                    Destiny = cut.work.type.S,
                    PartType = title switch
                    {
                        "Круг" => PartType.Round,
                        "Квадрат" => PartType.Rectangle,
                        "Круглая труба" => PartType.RoundTube,
                        "Прямоугольная труба" => PartType.RectangularTube,
                        _ => PartType.Rectangle
                    }
                };

                // Устанавливаем базовые размеры
                if (standartPart.PartType == PartType.Round || standartPart.PartType == PartType.RoundTube)
                {
                    standartPart.Width = standartPart.Height = 30;
                }
                else if (standartPart.PartType == PartType.Rectangle || standartPart.PartType == PartType.RectangularTube)
                {
                    standartPart.Width = 60;
                    standartPart.Height = 40;
                }

                PartPreviewGenerator.EnsureDisplayGeometry(standartPart);

                // Открываем окно с контекстом StandartPart
                var standartPartWindow = new StandartPartWindow(standartPart);
                if (standartPartWindow.ShowDialog() == true)
                {
                    // Сбрасываем кэш, чтобы геометрия пересоздалась с новыми размерами
                    standartPart.DisplayGeometry = null;

                    // После редактирования в окне все данные уже обновлены в объекте standartPart
                    PartPreviewGenerator.EnsureDisplayGeometry(standartPart);

                    standartPart.PropsDict[100] = new()
                    {
                        $"{standartPart.Width}",
                        $"{standartPart.Height}",
                        $"{standartPart.Width}x{standartPart.Height}"
                    };

                    int pinholes = 0;

                    // Пересчёт массы и длины реза
                    if (standartPart.DisplayGeometry != null)
                    {
                        standartPart.Way = (float)Math.Round(TechItemCalculator.CalculateCuttingLength(standartPart.DisplayGeometry) / 1000, 3);
                        pinholes = TechItemCalculator.CalculatePiercingCount(standartPart.DisplayGeometry);
                    }

                    if (standartPart.PartType == PartType.Round)
                    {
                        standartPart.Mass = (float)Math.Round(
                            Math.PI * Math.Pow(standartPart.Width / 2, 2) * cut.work.type.S * metal.Density / 1000000,
                            3);
                    }
                    else if (standartPart.PartType == PartType.RoundTube)
                    {
                        double outerR = standartPart.Width / 2;
                        double innerR = outerR - standartPart.Destiny;
                        standartPart.Way = (float)Math.Round(Math.PI * (outerR + innerR) * 2 / 1000, 3);
                        standartPart.Mass = (float)Math.Round(
                            Math.PI * (Math.Pow(outerR, 2) - Math.Pow(innerR, 2)) * cut.work.type.S * metal.Density / 1000000,
                            3);
                    }
                    else if (standartPart.PartType == PartType.RectangularTube)
                    {
                        double innerW = standartPart.Width - 2 * standartPart.Destiny;
                        double innerH = standartPart.Height - 2 * standartPart.Destiny;
                        standartPart.Way = (float)Math.Round(
                            (standartPart.Width + standartPart.Height + Math.Max(0, innerW) + Math.Max(0, innerH)) / 1000,
                            3);
                        standartPart.Mass = (float)Math.Round(
                            (standartPart.Width * standartPart.Height - Math.Max(0, innerW) * Math.Max(0, innerH))
                            * cut.work.type.S * metal.Density / 1000000,
                            3);
                    }
                    else
                    {
                        standartPart.Mass = (float)Math.Round(
                            standartPart.Width * standartPart.Height * cut.work.type.S * metal.Density / 1000000,
                            3);
                    }

                    // Создаём контрол
                    var partControl = new PartControl(owner, cut.work, standartPart);
                    if (standartPart.DisplayGeometry != null)
                    {
                        partControl.Picture.Visibility = Visibility.Collapsed;
                        partControl.GeometryViewbox.Visibility = Visibility.Visible;
                    }

                    // Добавляем в коллекции
                    Parts.Add(partControl);
                    cut.Parts ??= new();
                    if (!cut.Parts.Contains(partControl)) cut.Parts.Add(partControl);

                    cut.PartDetails ??= new();
                    if (!cut.PartDetails.Contains(standartPart)) cut.PartDetails.Add(standartPart);

                    // Обновляем интерфейс
                    if (cut.TabItem?.Header is TextBlock block)
                        block.Text = $"s{cut.work.type.S} {cut.work.type.MetalDrop.Text} ({cut.PartDetails.Sum(x => x.Count)} шт)";

                    // Обновляем расчетные данные
                    cut.WayTotal += standartPart.Way * standartPart.Count;
                    cut.MassTotal += standartPart.Mass * standartPart.Count;

                    cut.Items?.Add(new()
                    {
                        sheets = 1,
                        sheetSize = $"{standartPart.Width}x{standartPart.Height}",
                        way = standartPart.Way * standartPart.Count,
                        pinholes = pinholes * standartPart.Count,
                        mass = standartPart.Mass * standartPart.Count,
                    });

                    if (cut.Items?.Count > 0) cut.SumProperties(cut.Items);
                    cut.work.type.MassCalculate();      // обновляем значение массы заготовки
                }
            }
        }
    }
}
