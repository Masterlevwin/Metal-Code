using Metal_Code.Utils;
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
                    imagesList.Items.Clear();

                    foreach (LaserItem item in cut.Items)
                    {
                        if (item.NestingSheets != null && item.NestingSheets.Count > 0)
                        {

                            var sheet = item.NestingSheets[0]; // Берём первый (единственный) лист из группы

                            var preview = new NestingPreviewControl
                            {
                                Height = 320,
                                Margin = new Thickness(15, 0, 5, 0)
                            };
                            preview.ShowSheet(sheet);

                            // Добавляем подпись с типом листа
                            var border = new Border
                            {
                                Child = preview,
                                BorderBrush = item.sheets > 1 ? Brushes.Orange : Brushes.Gray,
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(3),
                                Padding = new Thickness(5)
                            };

                            // Добавляем информацию о листе
                            var stack = new StackPanel { Orientation = Orientation.Vertical };
                            stack.Children.Add(border);

                            // Подпись с размером, количеством деталей и количеством листов
                            string sheetInfo = $"{item.sheetSize} ({item.sheets} шт)\n{sheet.Parts.Count} деталей";

                            var infoText = new TextBlock
                            {
                                Text = sheetInfo,
                                FontSize = 10,
                                FontWeight = item.sheets > 1 ? FontWeights.Bold : FontWeights.Normal,
                                Foreground = item.sheets > 1 ? Brushes.OrangeRed : Brushes.Gray,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Margin = new Thickness(0, 5, 0, 5),
                                TextAlignment = TextAlignment.Center
                            };
                            stack.Children.Add(infoText);

                            imagesList.Items.Add(stack);

                        }
                        else if (item.imageBytes is not null)
                        {
                            Image _img = new()
                            {
                                Source = MainWindow.CreateBitmap(item.imageBytes),
                                Margin = new Thickness(5),
                                Height = 320
                            };
                            imagesList.Items.Add(_img);
                        }
                    }

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
                (Metal? metal, float thickness, string? metalName) = GetMetalAndThickness(owner);
                if (metal == null || string.IsNullOrEmpty(metalName))
                    return;

                // Создаём шаблонную деталь для редактирования
                var templatePart = CreateStandardPart(title, metalName, thickness, 0);
                if (templatePart == null) return;

                PartPreviewGenerator.EnsureDisplayGeometry(templatePart);

                // Открываем окно
                var window = new StandartPartWindow(templatePart);
                if (window.ShowDialog() == true)
                {
                    // Получаем ВСЕ детали из буфера
                    var batchedParts = window.GetBatchedParts();

                    if (batchedParts.Count > 0)
                    {
                        // Обновляем геометрию и расчёты для каждой детали
                        foreach (var part in batchedParts)
                        {
                            UpdatePartAfterEdit(part, metal, thickness);
                        }

                        // === КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: пакетная обработка ===
                        if (owner is CutControl cut)
                        {
                            AddBatchToCutControl(cut, batchedParts, metal);
                        }
                        else if (owner is PipeControl pipe)
                        {
                            // Для труб — поштучная обработка (трубы не требуют 2D-нестинга)
                            foreach (var part in batchedParts)
                            {
                                AddToPipeControl(pipe, part, metal);
                            }
                        }
                    }
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
            // Сбрасываем кэш геометрии
            part.DisplayGeometry = null;

            bool isSheetPart = part.PartType == PartType.Round || part.PartType == PartType.Rectangle;
            bool isPipePart = part.PartType == PartType.RoundTube || part.PartType == PartType.RectangularTube;

            int pinholes = 0;
            double cuttingLength = 0;

            // === ШАГ 1: Генерация геометрии ДЛЯ ВИЗУАЛИЗАЦИИ ===
            if (isSheetPart)
            {
                // Листы: визуализируем с отверстиями
                PartPreviewGenerator.EnsureDisplayGeometryWithHoles(part);
            }
            else if (isPipePart)
            {
                // Трубы: визуализируем ТОЛЬКО сечение (без отверстий!)
                PartPreviewGenerator.EnsureDisplayGeometry(part);
            }

            // === ШАГ 2: Расчёт длины реза и проколов ===
            if (part.DisplayGeometry != null)
            {
                // Базовая длина реза из геометрии (периметр сечения)
                cuttingLength = TechItemCalculator.CalculateCuttingLength(part.DisplayGeometry);

                // Дополнительная длина реза за счёт отверстий
                if (part.HoleGroups?.Count > 0)
                {
                    foreach (var group in part.HoleGroups)
                    {
                        double holePerimeter = Math.PI * group.Diameter; // Периметр одного отверстия
                        cuttingLength += holePerimeter * group.Count;   // × количество отверстий в группе
                    }
                }

                // Расчёт проколов
                if (isSheetPart)
                {
                    // Для листов — проколы = количество замкнутых контуров в геометрии
                    pinholes = TechItemCalculator.CalculatePiercingCount(part.DisplayGeometry);
                }
                else if (isPipePart)
                {
                    // Для труб — проколы = количество отверстий (каждое отверстие = 1 прокол при сверлении)
                    if (part.HoleGroups != null) pinholes = part.HoleGroups.Sum(g => g.Count);
                }
            }

            part.Way = (float)Math.Round(cuttingLength / 1000, 3);

            // === ШАГ 3: Расчёт массы ===
            if (isSheetPart)
            {
                part.Mass = part.PartType switch
                {
                    PartType.Round => (float)Math.Round(
                        Math.PI * Math.Pow(part.Width / 2, 2) * thickness * metal.Density / 1000000, 3),
                    _ => (float)Math.Round(
                        part.Width * part.Height * thickness * metal.Density / 1000000, 3)
                };

                part.PropsDict[100] = new()
                {
                    $"{part.Width}",
                    $"{part.Height}",
                    part.PartType == PartType.Round
                                    ? $"Ø{part.Width}"
                                    : $"{part.Width}x{part.Height}"
                };
            }
            else if (isPipePart)
            {
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

                part.PropsDict[100] = new() {
                    $"{SquareToPaint(part)}",
                    "",
                    $"{part.Length}"
                };
            }

            // === ШАГ 4: Сохранение данных об отверстиях ===
            part.PropsDict[200] = new() { pinholes.ToString() };
        }

        /// <summary>
        /// Добавляет КОЛЛЕКЦИЮ деталей с общим нестингом на минимальное количество листов
        /// </summary>
        private void AddBatchToCutControl(CutControl cut, List<Part> parts, Metal metal)
        {
            if (parts == null || parts.Count == 0)
                return;

            // === СОЗДАЁМ ЕДИНУЮ РАСКЛАДКУ ДЛЯ ВСЕХ ДЕТАЛЕЙ ===
            var nestingSheets = NestingHelper.CreateNestingForBatch(parts);

            if (nestingSheets == null || nestingSheets.Count == 0)
            {
                MessageBox.Show("Не удалось создать раскладку", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // === ГРУППИРУЕМ ОДИНАКОВЫЕ ЛИСТЫ ===
            var groupedSheets = GroupIdenticalSheets(nestingSheets);

            double totalWay = 0;
            double totalMass = 0;

            foreach (var group in groupedSheets)
            {
                var sheet = group.Key;
                int sheetCount = group.Value;

                if (sheet.Parts.Count == 0) continue;

                // Рассчитываем параметры для группы листов
                double sheetArea = sheet.Width * sheet.Height;
                double sheetMass = sheetArea * parts[0].Destiny * metal.Density / 1000000 * sheetCount;

                // Суммируем длину реза всех деталей на листе
                double sheetWay = 0;
                int sheetPinholes = 0;

                foreach (var placement in sheet.Parts)
                {
                    sheetWay += placement.Part.Way;
                    sheetPinholes += int.TryParse(
                        placement.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(),
                        out var p) ? p : 0;
                }

                sheetWay *= sheetCount;      // Умножаем на количество одинаковых листов
                sheetPinholes *= sheetCount;

                // Создаём один LaserItem для группы одинаковых листов
                var laserItem = new LaserItem
                {
                    sheets = sheetCount,
                    sheetSize = $"{sheet.Width:0}x{sheet.Height:0}",
                    way = (float)sheetWay,
                    pinholes = sheetPinholes,
                    mass = (float)sheetMass,
                    metal = metal.Name,
                    destiny = parts[0].Destiny.ToString(),
                    NestingSheets = new List<NestingSheet> { sheet }
                };

                cut.Items?.Add(laserItem);

                totalWay += sheetWay;
                totalMass += sheetMass;
            }

            // === ДОБАВЛЯЕМ КОНТРОЛЫ ТОЛЬКО ДЛЯ УНИКАЛЬНЫХ ТИПОВ ДЕТАЛЕЙ ===
            var uniqueParts = parts.GroupBy(p => new { p.Title, p.Width, p.Height, p.PartType })
                                   .Select(g => g.First())
                                   .ToList();

            foreach (var part in uniqueParts)
            {
                var partControl = new PartControl(owner, cut.work, part);
                Parts.Add(partControl);

                if (cut.Parts != null && !cut.Parts.Contains(partControl))
                    cut.Parts.Add(partControl);

                if (cut.PartDetails != null && !cut.PartDetails.Contains(part))
                    cut.PartDetails.Add(part);
            }

            // Обновляем итоговые значения
            cut.WayTotal += (float)totalWay;
            cut.MassTotal += (float)totalMass;

            if (cut.Items?.Count > 0)
                cut.SumProperties(cut.Items);

            cut.work?.type?.MassCalculate();

            // Обновляем заголовок вкладки
            if (cut.TabItem?.Header is TextBlock block)
                block.Text = $"s{cut.work?.type?.S} {cut.work?.type?.MetalDrop?.Text} ({cut.PartDetails?.Sum(x => x.Count)} шт)";
        }

        /// <summary>
        /// Группирует одинаковые листы в словарь (лист -> количество)
        /// </summary>
        private Dictionary<NestingSheet, int> GroupIdenticalSheets(List<NestingSheet> sheets)
        {
            var groups = new Dictionary<NestingSheet, int>(new NestingSheetComparer());

            foreach (var sheet in sheets)
            {
                bool foundMatch = false;

                foreach (var key in groups.Keys.ToList())
                {
                    if (NestingHelper.AreSheetsEqual(sheet, key))
                    {
                        groups[key]++;
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                {
                    groups[sheet] = 1;
                }
            }

            return groups;
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
    }
}