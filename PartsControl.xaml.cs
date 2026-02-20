using Metal_Code.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public ObservableCollection<PartControl> Parts { get; set; }

        private string[] works = { "Выберите работу", "Гибка", "Сварка", "Окраска", "Резьба", "Зенковка", "Сверловка",
                                    "Вальцовка", "Цинкование", "Фрезеровка", "Заклепки", "Аквабластинг"};

        public PartsControl(UserControl _owner, ObservableCollection<PartControl> _parts)
        {
            InitializeComponent();
            owner = _owner;
            Parts = _parts;
            partsList.ItemsSource = Parts;
            InitializeStandartPartsDropdown();

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

        // инициализация списка стандартных деталей
        private void InitializeStandartPartsDropdown()
        {
            if (owner is CutControl)        // Только листовые детали для листового контроллера
            {
                StandartPartsDrop.ItemsSource = new List<string>
                {
                    "Прямоугольник",
                    "Круг",
                };
            }
            else if (owner is PipeControl pipe)     // Только трубы для трубного контроллера
            {
                StandartPartsDrop.ItemsSource = new List<string>
                {
                    pipe.Tube == TubeType.round ? "Круглая труба" : "Профильная труба"
                };
            }

            // Устанавливаем первый элемент по умолчанию
            if (StandartPartsDrop.Items.Count > 0)
                StandartPartsDrop.SelectedIndex = 0;
        }

        // добавить стандартную деталь
        private void Add_StandartPart(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string title && owner != null)
            {
                // === ШАГ 1: Получаем общие параметры от контроллера ===
                (Metal? metal, float thickness, string? metalName) = GetMetalAndThickness(owner);
                if (metal == null || string.IsNullOrEmpty(metalName))
                    return;

                // === ШАГ 2: Создаём и настраиваем деталь (общая логика) ===
                var part = CreateStandardPart(title, metalName, thickness, Parts.Count);
                if (part == null) return;

                PartPreviewGenerator.EnsureDisplayGeometry(part);

                // === ШАГ 3: Открываем окно редактирования ===
                var window = new StandartPartWindow(part);
                if (window.ShowDialog() != true) return;

                // === ШАГ 4: Обновляем геометрию и расчёты ===
                UpdatePartAfterEdit(part, metal, thickness);

                // === ШАГ 5: Добавляем деталь в соответствующий контроллер ===
                if (!AddPartToController(owner, part, metal))
                {
                    MessageBox.Show("Не удалось добавить деталь: неподдерживаемый тип контроллера",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Вспомогательные методы (вынесены для чистоты кода)

        private (Metal?, float, string?) GetMetalAndThickness(object controller)
        {
            if (controller is CutControl cut &&
                cut.work?.type?.MetalDrop?.SelectedItem is Metal m)
            {
                return (m, cut.work.type.S, m.Name);
            }

            if (controller is PipeControl pipe &&
                pipe.work?.type?.MetalDrop?.SelectedItem is Metal p)
            {
                return (p, pipe.work.type.S, p.Name);
            }

            return (null, 0, null);
        }

        private Part? CreateStandardPart(string title, string metalName, float thickness, int countIndex)
        {
            TypeDetailControl? type = owner is PipeControl pipe ? pipe.work.type : null;

            var partType = title switch
            {
                "Круг" => PartType.Round,
                "Прямоугольник" => PartType.Rectangle,
                "Круглая труба" => PartType.RoundTube,
                "Профильная труба" => PartType.RectangularTube,
                _ => PartType.Rectangle
            };

            var part = new Part
            {
                Title = $"{title} {countIndex + 1}",
                Count = 1,
                Metal = metalName,
                Destiny = thickness,
                PartType = partType
            };

            // Устанавливаем базовые размеры
            if (partType == PartType.Round)
            {
                part.Width = part.Height = 30; // диаметр
            }
            else if (partType == PartType.Rectangle)
            {
                part.Width = 60;
                part.Height = 40;
            }
            else if (partType == PartType.RoundTube)
            {
                part.Width = part.Height = type != null ? type.A : 30; // внешний диаметр
                part.Length = 1000; // базовая длина 1 метр
            }
            else if (partType == PartType.RectangularTube)
            {
                part.Width = type != null ? type.A : 60;
                part.Height = type != null ? type.B : 40;
                part.Length = 1000; // базовая длина 1 метр
            }

            return part;
        }

        private void UpdatePartAfterEdit(Part part, Metal metal, float thickness)
        {
            // Сбрасываем кэш для перегенерации геометрии
            part.DisplayGeometry = null;
            PartPreviewGenerator.EnsureDisplayGeometry(part);

            int pinholes = 0;
            if (part.DisplayGeometry != null)
            {
                // Для труб длина реза = периметр сечения × количество прорезей
                // Для листов - просто периметр фигуры
                part.Way = (float)Math.Round(
                    TechItemCalculator.CalculateCuttingLength(part.DisplayGeometry) / 1000, 3);
                pinholes = TechItemCalculator.CalculatePiercingCount(part.DisplayGeometry);
            }

            // Расчёт массы в зависимости от типа
            if (part.PartType == PartType.Round || part.PartType == PartType.Rectangle)
            {
                // Листовые детали - площадь × толщина × плотность
                part.Mass = part.PartType switch
                {
                    PartType.Round => (float)Math.Round(
                        Math.PI * Math.Pow(part.Width / 2, 2) * thickness * metal.Density / 1000000, 3),

                    _ => (float)Math.Round( // Rectangle
                        part.Width * part.Height * thickness * metal.Density / 1000000, 3)
                };

                // Обновляем PropsDict для листов
                part.PropsDict[100] = new()
                {
                    $"{part.Width}",
                    $"{part.Height}",
                    $"{part.Width}x{part.Height}"
                };
            }
            else
            {
                // Трубные детали - площадь сечения × длина × плотность
                double crossSectionArea = part.PartType switch
                {
                    PartType.RoundTube => Math.PI * (
                        Math.Pow(part.Width / 2, 2) -
                        Math.Pow(part.Width / 2 - thickness, 2)),

                    PartType.RectangularTube =>
                        (part.Width * part.Height) -
                        Math.Max(0, part.Width - 2 * thickness) * Math.Max(0, part.Height - 2 * thickness),

                    _ => 0
                };

                part.Mass = (float)Math.Round(
                    crossSectionArea * part.Length * metal.Density / 1000000, 3);

                // Для труб длина реза = периметр внешнего + периметр внутреннего
                if (part.PartType == PartType.RoundTube)
                {
                    double outerPerimeter = Math.PI * part.Width;
                    double innerPerimeter = Math.PI * (part.Width - 2 * thickness);
                    part.Way = (float)Math.Round((outerPerimeter + innerPerimeter) / 1000, 3);
                }
                else if (part.PartType == PartType.RectangularTube)
                {
                    double outerPerimeter = 2 * (part.Width + part.Height);
                    double innerW = Math.Max(0, part.Width - 2 * thickness);
                    double innerH = Math.Max(0, part.Height - 2 * thickness);
                    double innerPerimeter = 2 * (innerW + innerH);
                    part.Way = (float)Math.Round((outerPerimeter + innerPerimeter) / 1000, 3);
                }

                // Обновляем PropsDict для труб
                part.PropsDict[100] = new() { $"{SquareToPaint(part)}", "", $"{part.Length}" };
            }

            // Сохраняем pinholes для последующего использования
            part.PropsDict[200] = new() { pinholes.ToString() };
        }

        private double SquareToPaint(Part part)
        {
            if (owner is PipeControl pipe)
            {
                return pipe.Tube switch
                {
                    TubeType.rect => part.Length * (pipe.work.type.A + pipe.work.type.B) * 2 / 1000000,
                    TubeType.round => (float)(part.Length * pipe.work.type.A * Math.PI / 1000000),
                    TubeType.circle => (float)(2 * part.Length * pipe.work.type.A * Math.PI / 1000000),
                    TubeType.square => part.Length * (pipe.work.type.A + pipe.work.type.B) * 2 / 1000000,
                    TubeType.rod => 2 * (part.Length * pipe.work.type.A + part.Way * pipe.work.type.B + pipe.work.type.A * pipe.work.type.B) / 1000000,
                    TubeType.channel => pipe.work.type.ChannelsSquare[pipe.work.type.SortDrop.SelectedIndex] * part.Mass / 1000,
                    TubeType.corner => part.Length * pipe.work.type.S * (pipe.work.type.A + pipe.work.type.A - pipe.work.type.S) / 1000000,
                    TubeType.freeform => part.Length * pipe.work.type.S * (pipe.work.type.A + pipe.work.type.B - pipe.work.type.S) / 1000000,
                    TubeType.hbeam => pipe.work.type.BeamDict[pipe.work.type.TypeDetailDrop.Text][pipe.work.type.SortDrop.SelectedIndex].Item2 * part.Mass / 1000,
                    _ => 0,
                };
            }

            return 0;
        }

        private bool AddPartToController(object controller, Part part, Metal metal)
        {
            if (controller is CutControl cut)
            {
                return AddToCutControl(cut, part, metal);
            }

            if (controller is PipeControl pipe)
            {
                return AddToPipeControl(pipe, part, metal);
            }

            return false;
        }

        private bool AddToCutControl(CutControl cut, Part part, Metal metal)
        {
            var partControl = new PartControl(owner, cut.work, part);
            Parts.Add(partControl);

            cut.Parts ??= new();
            if (!cut.Parts.Contains(partControl)) cut.Parts.Add(partControl);

            cut.PartDetails ??= new();
            if (!cut.PartDetails.Contains(part)) cut.PartDetails.Add(part);

            if (cut.TabItem?.Header is TextBlock block)
                block.Text = $"s{cut.work?.type?.S} {cut.work?.type?.MetalDrop?.Text} ({cut.PartDetails?.Sum(x => x.Count)} шт)";

            cut.WayTotal += part.Way * part.Count;
            cut.MassTotal += part.Mass * part.Count;

            // Специфичные для листа операции
            int pinholes = int.TryParse(part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var p) ? p : 0;

            cut.Items?.Add(new()
            {
                sheets = 1,
                sheetSize = $"{part.Width}x{part.Height}",
                way = part.Way * part.Count,
                pinholes = pinholes * part.Count,
                mass = part.Mass * part.Count,
            });

            if (cut.Items?.Count > 0) cut.SumProperties(cut.Items);
            cut.work?.type?.MassCalculate();

            return true;
        }

        private bool AddToPipeControl(PipeControl pipe, Part part, Metal metal)
        {
            var partControl = new PartControl(owner, pipe.work, part);
            Parts.Add(partControl);

            pipe.Parts ??= new();
            if (!pipe.Parts.Contains(partControl)) pipe.Parts.Add(partControl);

            pipe.PartDetails ??= new();
            if (!pipe.PartDetails.Contains(part)) pipe.PartDetails.Add(part);

            if (pipe.TabItem?.Header is TextBlock block)
                block.Text = $"s{pipe.work?.type?.S} {pipe.work?.type?.MetalDrop?.Text} ({pipe.PartDetails?.Sum(x => x.Count)} шт)";

            float mold = (float)Math.Round(part.Length * part.Count * 0.95f / 1000, 1);
            pipe.Mold += mold;

            if (pipe.work != null)
            {
                int count = (int)Math.Ceiling((double)(mold * 1000 / pipe.work.type.L));

                int pinholes = int.TryParse(part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var p) ? p : 0;
                pipe.Pinhole += pinholes * part.Count;

                pipe.Way += (float)Math.Ceiling(part.Way * part.Count);

                pipe.Items?.Add(new()
                {
                    sheets = count,
                    sheetSize = $"{pipe.work?.type.L}",
                    way = part.Way * part.Count,
                    pinholes = pinholes * part.Count,
                    mass = part.Mass * part.Count,
                });

                if (pipe.work != null) pipe.work.type.Count += count;
                pipe?.SetTotalProperties();
            }
            
            return true;
        }
    }
}
