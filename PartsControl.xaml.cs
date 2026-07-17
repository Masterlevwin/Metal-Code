using Metal_Code.Models;
using Metal_Code.Utils;
using netDxf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

        /// <summary>
        /// Позволяет ScrollViewer'у прокручиваться только по горизонтали, 
        /// а вертикальную прокрутку колесиком мыши передает родительскому окну.
        /// </summary>
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;

            // Если это вертикальная прокрутка колесиком
            if (e.Delta != 0)
            {
                // Создаем новое событие прокрутки
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };

                // Находим родительский элемент (обычно это Grid или главное окно)
                var parent = scrollViewer.Parent as UIElement;

                // И "всплываем" событие к нему, минуя блокировку текущего ScrollViewer
                parent?.RaiseEvent(eventArg);

                // Помечаем исходное событие как обработанное, чтобы ScrollViewer не пытался скроллить сам
                e.Handled = true;
            }
        }

        private void UpdateSheetManagementButtonsVisibility()
        {
            if (SheetManagementButtons != null)
            {
                bool isVisible = NestingToggle.IsChecked == true;
                SheetManagementButtons.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

                // При скрытии кнопок — форма тоже должна исчезнуть
                if (!isVisible && AddSheetForm != null)
                {
                    AddSheetForm.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Обработчик изменения состояния переключателя раскладок.
        /// </summary>
        private void NestingToggle_Checked(object sender, RoutedEventArgs e)
        {
            UpdateSheetManagementButtonsVisibility();
        }

        private void NestingToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateSheetManagementButtonsVisibility();
        }

        /// <summary>
        /// Показывает inline-форму для ввода размеров нового листа.
        /// </summary>
        private void AddSheet_Click(object sender, RoutedEventArgs e)
        {
            // Предзаполняем размерами последнего листа, если он есть
            if (owner is CutControl cut && cut.Items?.Any(i => i.NestingSheet != null) == true)
            {
                var lastSheet = cut.Items.Last(i => i.NestingSheet != null).NestingSheet;
                SheetWidthInput.Text = lastSheet?.StockWidth.ToString("F0");
                SheetHeightInput.Text = lastSheet?.StockHeight.ToString("F0");
            }
            else
            {
                SheetWidthInput.Text = "2000";
                SheetHeightInput.Text = "1000";
            }

            AddSheetForm.Visibility = Visibility.Visible;
            SheetWidthInput.Focus();
            SheetWidthInput.SelectAll();
        }

        /// <summary>
        /// Подтверждение создания листа с введенными размерами.
        /// </summary>
        private void ConfirmAddSheet_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(SheetWidthInput.Text.Replace('.', ','), out double width) || width <= 0)
            {
                MainWindow.M.StatusBegin("Некорректная ширина листа", MainWindow.StatusMessageType.Warning);
                SheetWidthInput.Focus();
                return;
            }

            if (!double.TryParse(SheetHeightInput.Text.Replace('.', ','), out double height) || height <= 0)
            {
                MainWindow.M.StatusBegin("Некорректная высота листа", MainWindow.StatusMessageType.Warning);
                SheetHeightInput.Focus();
                return;
            }

            if (owner is not CutControl cut || cut.Items is null)
            {
                MainWindow.M.StatusBegin("Не удалось добавить лист: владелец не является ICut", MainWindow.StatusMessageType.Error);
                return;
            }

            var (metal, thickness, metalName) = GetMetalAndThickness(owner);
            if (metal == null || string.IsNullOrEmpty(metalName))
            {
                MainWindow.M.StatusBegin("Не выбран материал или толщина", MainWindow.StatusMessageType.Warning);
                return;
            }

            // Создаем новый лист
            var newSheet = new NestingSheet
            {
                Id = Guid.NewGuid(),
                StockWidth = width,
                StockHeight = height,
                OptimizedWidth = width,
                OptimizedHeight = height,
                Parts = new List<PartPlacement>()
            };

            var newItem = new LaserItem
            {
                NestingSheet = newSheet,
                sheets = 1,
                sheetSize = $"{width:0}x{height:0}",
                metal = metalName,
                destiny = thickness.ToString()
            };

            cut.Items.Add(newItem);
            cut.SumProperties(cut.Items);
            cut.work.type.MassCalculate();

            // 🔥 Принудительно перерисовываем раскладки (не переключая видимость)
            RefreshNestingPreview();

            // Скрываем форму и обновляем отображение
            AddSheetForm.Visibility = Visibility.Collapsed;
            MainWindow.M.StatusBegin($"Добавлен лист {width:0}×{height:0} мм", MainWindow.StatusMessageType.Success);
        }

        /// <summary>
        /// Отмена добавления листа — скрывает форму.
        /// </summary>
        private void CancelAddSheet_Click(object sender, RoutedEventArgs e)
        {
            AddSheetForm.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Удаляет последний лист раскладки с подтверждением и корректно уменьшает количество деталей.
        /// </summary>
        private void RemoveSheet_Click(object sender, RoutedEventArgs e)
        {
            if (owner is not CutControl cut || cut.Items == null || !cut.Items.Any(i => i.NestingSheet != null))
            {
                MainWindow.M.StatusBegin("Нет листов для удаления", MainWindow.StatusMessageType.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Удалить последний лист? Все детали на нём будут удалены с раскладки (их общее количество уменьшится).",
                "Удаление листа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            var itemToRemove = cut.Items.Last(i => i.NestingSheet != null);

            // 🔥 КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Уменьшаем счетчик для каждой детали, которая была на этом листе
            if (itemToRemove.NestingSheet?.Parts != null)
            {
                foreach (var placement in itemToRemove.NestingSheet.Parts)
                {
                    placement.Part.Count--;
                    placement.Part.NotifyTotalChanged(); // Уведомляем интерфейс об изменении количества
                }
            }

            // Теперь безопасно удаляем сам лист из списка
            cut.Items.Remove(itemToRemove);

            // Пересчитываем итоги
            cut.SumProperties(cut.Items);
            cut.work.type.MassCalculate();

            // Принудительно перерисовываем раскладки
            RefreshNestingPreview();

            MainWindow.M.StatusBegin("Лист удалён, детали возвращены в общий список", MainWindow.StatusMessageType.Success);
        }


        /// <summary>
        /// Переключатель видимости раскладок (вызывается по клику на кнопку-тоггл).
        /// </summary>
        private void ShowNesting_Click(object sender, RoutedEventArgs e)
        {
            bool shouldShow = imagesScroll.Visibility != Visibility.Visible;

            if (shouldShow)
            {
                RefreshNestingPreview();
                imagesScroll.Visibility = Visibility.Visible;
                NestingToggle.IsChecked = true;
            }
            else
            {
                imagesScroll.Visibility = Visibility.Collapsed;
                NestingToggle.IsChecked = false;
            }

            UpdateSheetManagementButtonsVisibility();
        }

        /// <summary>
        /// Принудительно перерисовывает все раскладки (вызывается при добавлении/удалении листа).
        /// </summary>
        public void RefreshNestingPreview()
        {
            if (owner is not ICut cut || cut.Items is null)
            {
                imagesStack.Children.Clear();
                return;
            }

            imagesStack.Children.Clear();

            foreach (LaserItem item in cut.Items)
            {
                if (item.NestingSheet is not null)
                {
                    var preview = new NestingPreviewControl { Height = 320, Margin = new Thickness(0) };
                    preview.ShowSheet(item.NestingSheet);
                    preview.AddContinuousCopyToggle();

                    var border = new Border
                    {
                        Child = preview,
                        BorderBrush = item.sheets > 1 ? Brushes.Orange : Brushes.Gray,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(5)
                    };

                    var stack = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(5)
                    };
                    stack.Children.Add(border);

                    string sheetInfo = $"{item.sheets} шт ({item.sheetSize} мм)";
                    var infoText = new TextBlock
                    {
                        Text = sheetInfo,
                        FontSize = 10,
                        FontWeight = item.sheets > 1 ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = item.sheets > 1 ? Brushes.OrangeRed : Brushes.Gray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 5, 0, 0),
                        TextAlignment = TextAlignment.Center
                    };
                    stack.Children.Add(infoText);
                    imagesStack.Children.Add(stack);
                }
                else if (item.PipeStocks != null && item.PipeStocks.Count > 0)
                {
                    var stock = item.PipeStocks[0];
                    var preview = new PipeStockVisualizationControl { Stock = stock, Width = 820, Height = 70, Margin = new Thickness(5) };

                    var border = new Border
                    {
                        Child = preview,
                        BorderBrush = item.sheets > 1 ? Brushes.Orange : Brushes.Gray,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(5)
                    };

                    var stack = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(5)
                    };
                    stack.Children.Add(border);

                    string stockInfo = $"{item.sheets} шт ({stock.OptimizedLength:0} мм)";
                    var infoText = new TextBlock
                    {
                        Text = stockInfo,
                        FontSize = 10,
                        FontWeight = item.sheets > 1 ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = item.sheets > 1 ? Brushes.OrangeRed : Brushes.Gray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 5, 0, 0),
                        TextAlignment = TextAlignment.Center
                    };
                    stack.Children.Add(infoText);
                    imagesStack.Children.Add(stack);
                }
                else if (item.imageBytes is not null)
                {
                    var img = new Image
                    {
                        Source = MainWindow.CreateBitmap(item.imageBytes),
                        Height = 320,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    var border = new Border
                    {
                        Child = img,
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(5),
                        Margin = new Thickness(5)
                    };

                    var stack = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(5)
                    };
                    stack.Children.Add(border);
                    imagesStack.Children.Add(stack);
                }
            }
        }

        /// <summary>
        /// Открывает диалог выбора DXF-файлов и добавляет их как новые детали.
        /// </summary>
        private void AddPartsFromDxf_Click(object sender, RoutedEventArgs e)
        {
            if (owner is not CutControl cut) return;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите DXF-файлы деталей",
                Filter = "DXF файлы (*.dxf)|*.dxf|Все файлы (*.*)|*.*",
                Multiselect = true,
                InitialDirectory = MainWindow.M.lastInputDirectory
            };

            if (openFileDialog.ShowDialog() == true)
            {
                int addedCount = 0;
                var failedFiles = new List<string>();
                var (metal, thickness, metalName) = GetMetalAndThickness(owner);

                foreach (var filePath in openFileDialog.FileNames)
                {
                    try
                    {
                        var doc = DxfDocument.Load(filePath);
                        var calculationGeometry = DxfToWpfConverter.ConvertToPathGeometryWithClosedContours(doc);

                        if (calculationGeometry == null || calculationGeometry.IsEmpty())
                            throw new InvalidOperationException("DXF не содержит распознаваемых замкнутых контуров.");

                        var part = new Part(Path.GetFileNameWithoutExtension(filePath))
                        {
                            Count = 0,
                            Metal = metalName,
                            Destiny = thickness,
                            Width = Math.Ceiling(calculationGeometry.Bounds.Width),
                            Height = Math.Ceiling(calculationGeometry.Bounds.Height),
                            PartType = PartType.Rectangle, // Или логика определения типа
                            DisplayGeometry = calculationGeometry,
                        };

                        if (part.DisplayGeometry.CanFreeze)
                            part.DisplayGeometry.Freeze();

                        if (metal is not null)
                            UpdatePartAfterEdit(part, metal, thickness, true);

                        var partControl = new PartControl(owner, cut.work, part);

                        // Добавляем во все необходимые коллекции
                        Parts.Add(partControl);
                        cut.Parts?.Add(partControl);
                        cut.PartDetails?.Add(part);

                        addedCount++;
                    }
                    catch (Exception ex)
                    {
                        // 🔥 Собираем ошибки, чтобы не прерывать цикл и не спамить пользователя окнами
                        failedFiles.Add(System.IO.Path.GetFileName(filePath));
                        System.Diagnostics.Debug.WriteLine($"Ошибка импорта {filePath}: {ex.Message}");
                    }
                }

                if (addedCount > 0)
                {
                    MainWindow.M.StatusBegin($"Успешно добавлено деталей: {addedCount}", MainWindow.StatusMessageType.Success);
                }

                if (failedFiles.Count > 0)
                {
                    string fileList = string.Join("\n", failedFiles.Take(5)); // Показываем первые 5
                    string moreText = failedFiles.Count > 5 ? $"\n...и ещё {failedFiles.Count - 5} файлов." : "";

                    MessageBox.Show($"Не удалось прочитать следующие файлы:\n{fileList}{moreText}\n\n" +
                                    "Пересохраните их в CAD-программе (например, в DXF R12 или R14) и попробуйте снова.",
                                    "Ошибка импорта", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // Добавить стандартную деталь
        private void Add_StandartPart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn) return;

            // 🔥 Включаем оранжевую подсветку на время работы с окном
            btn.IsChecked = true;

            try
            {
                (Metal? metal, float thickness, string? metalName) = GetMetalAndThickness(owner);
                if (metal == null || string.IsNullOrEmpty(metalName))
                    return;

                var templatePart = CreateStandardPart(metalName, thickness, 0);
                if (templatePart == null) return;

                PartPreviewGenerator.EnsureDisplayGeometry(templatePart);

                var window = new StandartPartWindow(templatePart);
                if (window.ShowDialog() == true)
                {
                    var batchedParts = window.GetBatchedParts();

                    if (batchedParts.Count > 0)
                    {
                        foreach (var part in batchedParts)
                        {
                            UpdatePartAfterEdit(part, metal, thickness);
                        }

                        if (owner is CutControl cut)
                        {
                            AddBatchToCutControl(cut, batchedParts, metal, window.UseAutoNesting);
                        }
                        else if (owner is PipeControl pipe)
                        {
                            AddBatchToPipeControl(pipe, batchedParts, metal);
                        }
                        else if (owner is SawControl saw)
                        {
                            AddBatchToSawControl(saw, batchedParts, metal);
                        }
                    }
                }
            }
            finally
            {
                // 🔥 Гарантированно выключаем подсветку после закрытия окна (даже если произошла ошибка)
                btn.IsChecked = false;
            }
        }

        // обработчик кнопки сброса групп гибки
        private void SetDefaultBends(object sender, RoutedEventArgs e)
        {
            foreach (PartControl p in Parts)
                foreach (BendControl item in p.UserControls.OfType<BendControl>()) item.SetGroup("-");
        }

        public static (Metal?, float, string?) GetMetalAndThickness(object controller)
        {
            if (controller is CutControl cut &&
                cut.work.type.MetalDrop.SelectedItem is Metal m)
            {
                return (m, cut.work.type.S, m.Name);
            }

            if (controller is PipeControl pipe &&
                pipe.work.type.MetalDrop.SelectedItem is Metal p)
            {
                return (p, pipe.work.type.S, p.Name);
            }

            if (controller is SawControl saw &&
                saw.work.type.MetalDrop.SelectedItem is Metal s)
            {
                return (s, saw.work.type.S, s.Name);
            }

            return (null, 0, null);
        }

        private Part? CreateStandardPart(string metalName, float thickness, int countIndex)
        {
            string title = "Деталь";
            TypeDetailControl? type;

            if (owner is PipeControl pipe)
            {
                type = pipe.work.type;
                title = pipe.Tube switch
                {
                    TubeType.rect => "Профильная труба",
                    TubeType.round => "Круглая труба",
                    TubeType.corner => "Уголок",
                    TubeType.freeform => "Уголок",
                    TubeType.channel => "Швеллер",
                    TubeType.ibeam => "Двутавр",
                    _ => "Не определено"
                };
            }
            else if (owner is SawControl saw)
            {
                type = saw.work.type;
                title = saw.Tube switch
                {
                    TubeType.circle => "Круг",
                    TubeType.rod => "Квадрат",
                    TubeType.rect => "Профильная труба",
                    TubeType.round => "Круглая труба",
                    TubeType.corner => "Уголок",
                    TubeType.freeform => "Уголок",
                    TubeType.channel => "Швеллер",
                    TubeType.ibeam => "Двутавр",
                    _ => "Не определено"
                };
            }
            else if (owner is CutControl cut)
            {
                type = cut.work.type;
                title = "Прямоугольник";
            }
            else type = null;

            var partType = title switch
            {
                "Прямоугольник" => PartType.Rectangle,
                "Круг" => owner is SawControl saw && saw.Tube == TubeType.circle ? PartType.RoundTube : PartType.Round,
                "Квадрат" => PartType.RectangularTube,
                "Профильная труба" => PartType.RectangularTube,
                "Круглая труба" => PartType.RoundTube,
                "Уголок" => PartType.Angle,
                "Швеллер" => PartType.Channel,
                "Двутавр" => PartType.IBeam,
                _ => PartType.Unknown
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
            if (partType == PartType.Round || partType == PartType.Rectangle)
                part.Width = part.Height = 50;
            else if (partType == PartType.RoundTube)
            {
                part.Width = part.Height = type != null ? type.A : 50;
                part.Length = 1000; // базовая длина 1 метр
            }
            else
            {
                part.Width = type != null ? type.A : 50;
                part.Height = type != null ? type.B : 50;
                part.Length = 1000; // базовая длина 1 метр
            }

            return part;
        }

        public void UpdatePartAfterEdit(Part part, Metal metal, float thickness, bool isOriginal = false)
        {
            if (part.PropsDict == null) part.PropsDict = new Dictionary<int, List<string>>();

            bool isSheetPart = part.PartType == PartType.Round || part.PartType == PartType.Rectangle;
            int pinholes = 0;
            double cuttingLength = 0;

            // === ШАГ 1: Генерация геометрии, если ее нет ===
            if (!isOriginal)
            {
                part.DisplayGeometry = null;
                if (isSheetPart)
                    PartPreviewGenerator.EnsureDisplayGeometryWithHoles(part);
                else
                    PartPreviewGenerator.EnsureDisplayGeometry(part);
            }

            // === ШАГ 2: Расчёт длины реза и проколов ===
            if (part.DisplayGeometry != null)
            {
                cuttingLength = TechItemCalculator.CalculateCuttingLength(part.DisplayGeometry);

                if (part.HoleGroups?.Count > 0)
                {
                    foreach (var group in part.HoleGroups)
                    {
                        double holePerimeter = Math.PI * group.Diameter;
                        cuttingLength += holePerimeter * group.Count;
                    }
                }

                pinholes = isSheetPart
                    ? TechItemCalculator.CalculatePiercingCount(part.DisplayGeometry)
                    : (part.HoleGroups?.Sum(g => g.Count) + 2 ?? 0);
            }

            part.Way = (float)Math.Round(cuttingLength / 1000, 3);

            // === ШАГ 3: Расчёт массы ===
            if (isSheetPart)
            {
                double area = CalculateCrossSectionArea(part.PartType, part.Width, part.Height, 1);
                part.Mass = (float)Math.Round(area * thickness * metal.Density / 1_000_000, 3);

                part.PropsDict[100] = new List<string>
        {
            $"{part.Width}",
            $"{part.Height}",
            part.PartType == PartType.Round ? $"Ø{part.Width}" : $"{part.Width}x{part.Height}"
        };
            }
            else
            {
                double area = CalculateCrossSectionArea(part, thickness);
                part.Mass = (float)Math.Round(area * part.Length * metal.Density / 1_000_000, 3);

                part.PropsDict[100] = new List<string>
        {
            $"{SquareToPaint(part)}", "", $"{part.Length}"
        };
            }

            // === ШАГ 4: Сохранение данных об отверстиях ===
            part.PropsDict[200] = new List<string> { pinholes.ToString() };
        }

        /// <summary>
        /// Добавляет КОЛЛЕКЦИЮ деталей с общим нестингом на минимальное количество листов
        /// </summary>
        public void AddBatchToCutControl(CutControl cut, List<Part> parts, Metal metal, bool isAutoSheet = true)
        {
            if (parts == null || parts.Count == 0)
                return;

            // === СОЗДАЁМ ЕДИНУЮ РАСКЛАДКУ ДЛЯ ВСЕХ ДЕТАЛЕЙ ===
            var nestingSheets = isAutoSheet ?
                SkylineNestingHelper.CreateNestingSkylineAutoSheet(parts, cut.work.type.MetalDrop.Text, cut.work.type.S)
                : SkylineNestingHelper.CreateNestingSkyline(parts, cut.work.type.A, cut.work.type.B);

            if (nestingSheets == null || nestingSheets.Count == 0)
            {
                MessageBox.Show("Не удалось создать раскладку", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // === ГРУППИРУЕМ ОДИНАКОВЫЕ ЛИСТЫ ===
            var groupedSheets = GroupIdenticalSheets(nestingSheets);

            foreach (var group in groupedSheets)
            {
                var sheet = group.Key;
                int sheetCount = group.Value;

                if (sheet.Parts.Count == 0) continue;

                // Рассчитываем параметры для группы листов
                double sheetArea = sheet.OptimizedWidth * sheet.OptimizedHeight;
                double sheetMass = sheetArea * parts[0].Destiny * metal.Density / 1000000;

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

                // Создаём один LaserItem для группы одинаковых листов
                var laserItem = new LaserItem
                {
                    sheets = sheetCount,
                    sheetSize = $"{sheet.OptimizedWidth:0}x{sheet.OptimizedHeight:0}",
                    way = (float)sheetWay,
                    pinholes = sheetPinholes,
                    mass = (float)sheetMass,
                    metal = metal.Name,
                    destiny = parts[0].Destiny.ToString(),
                    NestingSheet = sheet
                };

                cut.Items?.Add(laserItem);
            }

            // === ДОБАВЛЯЕМ КОНТРОЛЫ ТОЛЬКО ДЛЯ УНИКАЛЬНЫХ ТИПОВ ДЕТАЛЕЙ ===
            var uniqueParts = parts.GroupBy(p => new { p.Title, p.Width, p.Height, p.PartType })
                                   .Select(g => g.First())
                                   .ToList();

            foreach (var part in uniqueParts)
            {
                var partControl = new PartControl(owner, cut.work, part);
                Parts.Add(partControl);

                cut.Parts ??= new();
                if (!cut.Parts.Contains(partControl)) cut.Parts.Add(partControl);

                cut.PartDetails ??= new();
                if (!cut.PartDetails.Contains(part)) cut.PartDetails.Add(part);  
            }

            // Обновляем итоговые значения
            cut.WayTotal += parts.Sum(p => p.Way * p.Count);
            cut.MassTotal += parts.Sum(p => p.Mass * p.Count);

            if (cut.Items?.Count > 0)
            {
                cut.SumProperties(cut.Items);
                cut.work.type.CreateSort();
            }
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

        /// <summary>
        /// Добавляет КОЛЛЕКЦИЮ трубных деталей с оптимальным нестингом и группировкой одинаковых хлыстов
        /// </summary>
        public void AddBatchToPipeControl(PipeControl pipe, List<Part> parts, Metal metal)
        {
            if (parts == null || parts.Count == 0 || pipe.work?.type == null)
                return;

            // Создаём раскладку по хлыстам
            var pipeStocks = NestingHelper.CreateNestingForPipeBatch(parts, pipe.work.type.L);

            if (pipeStocks == null || pipeStocks.Count == 0)
            {
                MessageBox.Show("Не удалось создать раскладку для труб", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // === ГРУППИРУЕМ ОДИНАКОВЫЕ ХЛЫСТЫ ===
            var groupedStocks = GroupIdenticalStocks(pipeStocks);

            double totalWay = 0;
            int totalPinholes = 0;

            foreach (var group in groupedStocks)
            {
                var stock = group.Key;
                int stockCount = group.Value;

                if (stock.Placements.Count == 0) continue;

                // Рассчитываем параметры для группы хлыстов
                double stockMass = CalculatePipeStockMass(stock, metal, parts[0].Destiny);
                double stockWay = stock.Placements.Sum(p => p.Part.Way);
                int stockPinholes = stock.Placements.Sum(p =>
                    int.TryParse(p.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var pin) ? pin : 0);

                // Создаём один LaserItem для группы одинаковых хлыстов (полная аналогия с листами!)
                var laserItem = new LaserItem
                {
                    sheets = stockCount, // Количество одинаковых хлыстов
                    sheetSize = $"{stock.OptimizedLength}", // Длина хлыста
                    way = (float)stockWay,
                    pinholes = stockPinholes,
                    mass = (float)stockMass,
                    metal = metal.Name,
                    destiny = parts[0].Destiny.ToString(),
                    PipeStocks = new List<PipeStock> { stock } // Один представитель группы
                };

                pipe.Items?.Add(laserItem);

                totalWay += stockWay * stockCount;
                totalPinholes += stockPinholes * stockCount;
            }

            // === ДОБАВЛЯЕМ КОНТРОЛЫ ТОЛЬКО ДЛЯ УНИКАЛЬНЫХ ТИПОВ ДЕТАЛЕЙ ===
            var uniqueParts = parts
                .GroupBy(p => new { p.Title, p.Width, p.Height, p.Length, p.PartType, p.Destiny })
                .Select(g => g.First())
                .ToList();

            foreach (var part in uniqueParts)
            {
                var partControl = new PartControl(owner, pipe.work, part);
                Parts.Add(partControl);

                pipe.Parts ??= new();
                if (!pipe.Parts.Contains(partControl)) pipe.Parts.Add(partControl);

                pipe.PartDetails ??= new();
                if (!pipe.PartDetails.Contains(part)) pipe.PartDetails.Add(part);    
            }

            // Обновляем итоговые значения
            pipe.Way += (float)totalWay;
            pipe.Pinhole += totalPinholes;

            // Обновляем количество хлыстов в типе заготовки
            pipe.work.type.Count += groupedStocks.Sum(g => g.Value);
            pipe.Mold += (float)Math.Round(pipe.work.type.L * groupedStocks.Sum(g => g.Value) * 0.95f / 1000, 1);

            pipe.SetTotalProperties();
        }

        /// <summary>
        /// Добавляет КОЛЛЕКЦИЮ трубных деталей для ЛЕНТОПИЛА
        /// </summary>
        private void AddBatchToSawControl(SawControl saw, List<Part> parts, Metal metal)
        {
            if (parts == null || parts.Count == 0 || saw.work?.type == null)
                return;

            // Создаём раскладку по хлыстам
            var pipeStocks = NestingHelper.CreateNestingForPipeBatch(parts, saw.work.type.L);

            if (pipeStocks == null || pipeStocks.Count == 0)
            {
                MessageBox.Show("Не удалось создать раскладку для труб", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // === ГРУППИРУЕМ ОДИНАКОВЫЕ ХЛЫСТЫ ===
            var groupedStocks = GroupIdenticalStocks(pipeStocks);

            double totalWay = 0;
            int totalPinholes = 0;

            foreach (var group in groupedStocks)
            {
                var stock = group.Key;
                int stockCount = group.Value;

                if (stock.Placements.Count == 0) continue;

                // Рассчитываем параметры для группы хлыстов
                double stockMass = CalculatePipeStockMass(stock, metal, parts[0].Destiny);
                double stockWay = stock.Placements.Sum(p => p.Part.Way);
                int stockPinholes = stock.Placements.Sum(p =>
                    int.TryParse(p.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var pin) ? pin : 0);

                // Создаём один LaserItem для группы одинаковых хлыстов (полная аналогия с листами!)
                var laserItem = new LaserItem
                {
                    sheets = stockCount, // Количество одинаковых хлыстов
                    sheetSize = $"{stock.OptimizedLength}", // Длина хлыста
                    way = (float)stockWay,
                    pinholes = stockPinholes,
                    mass = (float)stockMass,
                    metal = metal.Name,
                    destiny = parts[0].Destiny.ToString(),
                    PipeStocks = new List<PipeStock> { stock } // Один представитель группы
                };

                saw.Items?.Add(laserItem);

                totalWay += stockWay * stockCount;
                totalPinholes += stockPinholes * stockCount;
            }

            // === ДОБАВЛЯЕМ КОНТРОЛЫ ТОЛЬКО ДЛЯ УНИКАЛЬНЫХ ТИПОВ ДЕТАЛЕЙ ===
            var uniqueParts = parts
                .GroupBy(p => new { p.Title, p.Width, p.Height, p.Length, p.PartType, p.Destiny })
                .Select(g => g.First())
                .ToList();

            foreach (var part in uniqueParts)
            {
                var partControl = new PartControl(owner, saw.work, part);
                Parts.Add(partControl);

                saw.Parts ??= new();
                if (!saw.Parts.Contains(partControl)) saw.Parts.Add(partControl);

                saw.PartDetails ??= new();
                if (!saw.PartDetails.Contains(part)) saw.PartDetails.Add(part);
            }

            // Обновляем итоговые значения
            saw.Way += (float)totalWay;
            saw.Pinhole += totalPinholes;

            // Обновляем количество хлыстов в типе заготовки
            saw.work.type.Count += groupedStocks.Sum(g => g.Value);
            saw.SetTotalProperties();
        }

        /// <summary>
        /// Группирует одинаковые хлысты в словарь (хлыст -> количество)
        /// </summary>
        private Dictionary<PipeStock, int> GroupIdenticalStocks(List<PipeStock> stocks)
        {
            var groups = new Dictionary<PipeStock, int>(new PipeStockComparer());

            foreach (var stock in stocks)
            {
                bool foundMatch = false;

                foreach (var key in groups.Keys.ToList())
                {
                    if (NestingHelper.AreStocksEqual(stock, key))
                    {
                        groups[key]++;
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                {
                    groups[stock] = 1;
                }
            }

            return groups;
        }

        // Расчет площади окрашивания детали
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
                    TubeType.ibeam => pipe.work.type.BeamDict[pipe.work.type.TypeDetailDrop.Text][pipe.work.type.SortDrop.SelectedIndex].Item2 * part.Mass / 1000,
                    _ => 0,
                };
            }
            else if (owner is SawControl saw)
            {
                return saw.Tube switch
                {
                    TubeType.rect => part.Length * (saw.work.type.A + saw.work.type.B) * 2 / 1000000,
                    TubeType.round => (float)(part.Length * saw.work.type.A * Math.PI / 1000000),
                    TubeType.circle => (float)(2 * part.Length * saw.work.type.A * Math.PI / 1000000),
                    TubeType.square => part.Length * (saw.work.type.A + saw.work.type.B) * 2 / 1000000,
                    TubeType.rod => 2 * (part.Length * saw.work.type.A + part.Way * saw.work.type.B + saw.work.type.A * saw.work.type.B) / 1000000,
                    TubeType.channel => saw.work.type.ChannelsSquare[saw.work.type.SortDrop.SelectedIndex] * part.Mass / 1000,
                    TubeType.corner => part.Length * saw.work.type.S * (saw.work.type.A + saw.work.type.A - saw.work.type.S) / 1000000,
                    TubeType.freeform => part.Length * saw.work.type.S * (saw.work.type.A + saw.work.type.B - saw.work.type.S) / 1000000,
                    TubeType.ibeam => saw.work.type.BeamDict[saw.work.type.TypeDetailDrop.Text][saw.work.type.SortDrop.SelectedIndex].Item2 * part.Mass / 1000,
                    _ => 0,
                };
            }

            return 0;
        }

        /// <summary>
        /// Рассчитывает площадь поперечного сечения профиля в мм²
        /// </summary>
        /// <param name="partType">Тип профиля</param>
        /// <param name="width">Ширина/диаметр/полка (мм)</param>
        /// <param name="height">Высота/вторая полка (мм), для круглых — не используется</param>
        /// <param name="thickness">Толщина стенки/полки (мм)</param>
        /// <returns>Площадь сечения в мм²</returns>
        public static double CalculateCrossSectionArea(
            PartType partType,
            double width,
            double height,
            double thickness)
        {
            // Валидация: толщина не должна превышать 1/3 от меньшего линейного размера
            double minDim = Math.Min(width, height > 0 ? height : width);
            double t = thickness >= minDim / 3 ? Math.Max(0.1, minDim / 3 - 0.1) : thickness;

            return partType switch
            {
                // === ЛИСТОВЫЕ И ПРОСТЫЕ СЕЧЕНИЯ ===
                PartType.Round => Math.PI * Math.Pow(width / 2, 2),
                PartType.Rectangle or PartType.SquareBar => width * height,

                // === ТРУБЫ ===
                PartType.RoundTube => Math.PI * (
                    Math.Pow(width / 2, 2) -
                    Math.Pow(Math.Max(0, width / 2 - t), 2)),

                PartType.RectangularTube =>
                    width * height -
                    Math.Max(0, width - 2 * t) * Math.Max(0, height - 2 * t),

                // === ПРОКАТ ===
                // Уголок: A = t × (b₁ + b₂ − t) — учитываем перекрытие в углу
                PartType.Angle => t * (width + height - t),

                // Швеллер: A = t × (W + 2H − 2t) = основание + две стенки
                PartType.Channel => t * (width + 2 * height - 2 * t),

                // Двутавр: A = t × (2W + H − 2t) = две полки + стенка
                PartType.IBeam => t * (2 * width + height - 2 * t),

                _ => 0
            };
        }

        /// <summary>
        /// Рассчитывает площадь сечения детали
        /// </summary>
        public static double CalculateCrossSectionArea(Part part, double thickness)
        {
            return CalculateCrossSectionArea(
                part.PartType,
                part.Width,
                part.Height,
                thickness);
        }

        /// <summary>
        /// Рассчитывает массу хлыста трубы/профиля
        /// </summary>
        /// <param name="stock">Хлыст с размещёнными деталями</param>
        /// <param name="metal">Материал с плотностью</param>
        /// <param name="thickness">Толщина стенки (берётся из первой детали, если не задана)</param>
        /// <returns>Масса хлыста в кг</returns>
        public static double CalculatePipeStockMass(PipeStock stock, Metal metal, double? thickness = null)
        {
            if (stock?.Placements == null || stock.Placements.Count == 0)
                return 0;

            // Берём первую деталь для определения параметров сечения
            var firstPart = stock.Placements[0].Part;
            double t = thickness ?? firstPart.Destiny; // если толщина не передана — берём из детали

            double crossSectionArea = CalculateCrossSectionArea(
                firstPart.PartType,
                firstPart.Width,
                firstPart.Height,
                t);

            // Масса = площадь сечения × длина хлыста × плотность / 1_000_000 (мм³ → см³)
            return crossSectionArea * stock.StockLength * metal.Density / 1_000_000;
        }
    }
}