using Metal_Code.Models;
using Metal_Code.Utils;
using Microsoft.Win32;
using netDxf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Colors = QuestPDF.Helpers.Colors;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PartsControl.xaml
    /// </summary>
    public partial class PartsControl : UserControl
    {
        public readonly UserControl owner;
        public ObservableCollection<PartControl> Parts { get; set; }

        private readonly string[] works = { "Выберите работу", "Гибка", "Сварка", "Окраска", "Резьба", "Зенковка", "Сверловка",
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

            UpdateEmptyState();
        }

        //-------------Работы--------------//
        #region
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

        // обработчик кнопки сброса групп гибки
        private void SetDefaultBends(object sender, RoutedEventArgs e)
        {
            foreach (PartControl p in Parts)
                foreach (BendControl item in p.UserControls.OfType<BendControl>()) item.SetGroup("-");
        }

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
        #endregion

        //-------------Сайдбар-------------//
        #region
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
        /// Принудительно перерисовывает все раскладки, разделяя листы и трубы на разные зоны.
        /// </summary>
        public void RefreshNestingPreview()
        {
            if (owner is not ICut cut || cut.Items is null)
            {
                SheetImagesStack.Children.Clear();
                PipeImagesStack.Children.Clear();
                return;
            }

            SheetImagesStack.Children.Clear();
            PipeImagesStack.Children.Clear();

            foreach (LaserItem item in cut.Items)
            {
                // ==========================================
                // 1. ЛИСТОВОЙ МЕТАЛЛ
                // ==========================================
                if (item.NestingSheet is not null)
                {
                    var preview = new NestingPreviewControl { Height = 320 };
                    preview.ShowSheet(item.NestingSheet);
                    preview.AddContinuousCopyToggle();

                    var border = new Border
                    {
                        Child = preview,
                        BorderBrush = item.sheets > 1 ? Brushes.Orange : Brushes.Gray,
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

                    var controlPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 5, 0, 0)
                    };

                    var qtyControl = new HandyControl.Controls.NumericUpDown
                    {
                        Value = item.sheets,
                        Minimum = 1,
                        Maximum = 9999,
                        Width = 60,
                        Height = 28,
                        Margin = new Thickness(0, 0, 10, 0),
                        ToolTip = "Количество одинаковых листов",
                        Tag = item.sheets
                    };

                    var infoText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

                    void UpdateInfoText()
                    {
                        infoText.Text = $"{item.sheets} шт ({item.sheetSize} мм)";
                        infoText.FontWeight = item.sheets > 1 ? FontWeights.Bold : FontWeights.Normal;
                        infoText.Foreground = item.sheets > 1 ? Brushes.OrangeRed : Brushes.DimGray;
                    }
                    UpdateInfoText();

                    qtyControl.ValueChanged += (s, e) =>
                    {
                        var nud = s as HandyControl.Controls.NumericUpDown;
                        if (nud is null) return;

                        int newQty = Convert.ToInt32(nud.Value);
                        int oldQty = Convert.ToInt32(nud.Tag);

                        if (newQty != oldQty && item.NestingSheet != null)
                        {
                            int delta = newQty - oldQty;
                            item.sheets = newQty;

                            foreach (var placement in item.NestingSheet.Parts)
                            {
                                placement.Part.Count += delta;
                                placement.Part.NotifyTotalChanged();
                            }

                            RecalculateTotalsUniversal(cut);
                            UpdateInfoText();
                            nud.Tag = newQty;

                            string action = delta > 0 ? "добавлен" : "удален";
                            MainWindow.M.StatusBegin($"Количество листов {item.sheetSize} изменено. {Math.Abs(delta)} лист {action}.", MainWindow.StatusMessageType.Success);
                        }
                    };

                    var deleteBtn = new Button
                    {
                        Content = "🗑",
                        ToolTip = "Удалить этот лист",
                        Foreground = Brushes.Red,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        DataContext = item
                    };
                    deleteBtn.Click += DeleteSpecificSheet_Click;

                    controlPanel.Children.Add(qtyControl);
                    controlPanel.Children.Add(infoText);
                    controlPanel.Children.Add(deleteBtn);
                    stack.Children.Add(controlPanel);

                    // 🔥 ДОБАВЛЯЕМ В ГОРИЗОНТАЛЬНЫЙ СТЕК ЛИСТОВ
                    SheetImagesStack.Children.Add(stack);
                }
                // ==========================================
                // 2. ТРУБНЫЙ ПРОКАТ (ХЛЫСТЫ)
                // ==========================================
                else if (item.PipeStocks != null && item.PipeStocks.Count > 0)
                {
                    var stock = item.PipeStocks[0];
                    var preview = new PipeStockVisualizationControl
                    {
                        Stock = stock,
                        Width = 820,
                        Height = 70,
                        Margin = new Thickness(5)
                    };

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

                    // 🔥 УЛУЧШЕННАЯ ИНФОРМАЦИОННАЯ СТРОКА: количество, оптимизированная длина, зажим и пропил
                    string stockInfo = $"{item.sheets} шт (опт. {stock.OptimizedLength:0} мм | зажим: {stock.ClampZone:0} мм, пропил: {stock.CutLoss:0} мм)";

                    var infoText = new TextBlock
                    {
                        Text = stockInfo,
                        FontSize = 11, // Чуть увеличили шрифт для лучшей читаемости деталей
                        FontWeight = item.sheets > 1 ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = item.sheets > 1 ? Brushes.OrangeRed : Brushes.DimGray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 5, 0, 0),
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap // 🔥 Гарантирует, что текст перенесется, а не обрежется
                    };
                    stack.Children.Add(infoText);

                    // Добавляем в вертикальный WrapPanel труб
                    PipeImagesStack.Children.Add(stack);
                }
                // ==========================================
                // 3. ИЗОБРАЖЕНИЯ (если есть)
                // ==========================================
                else if (item.imageBytes is not null)
                {
                    var img = new Image
                    {
                        Source = MainWindow.CreateBitmap(item.imageBytes),
                        Height = 320,
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

                    // 🔥 ДОБАВЛЯЕМ В ГОРИЗОНТАЛЬНЫЙ СТЕК ЛИСТОВ
                    SheetImagesStack.Children.Add(stack);
                }
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

            // Читаем отступ (с защитой от отрицательных значений и пустой строки)
            double spacing = 10;
            if (double.TryParse(SheetSpacingInput.Text, out double parsedSpacing))
            {
                spacing = Math.Max(0, parsedSpacing); // Гарантируем неотрицательный отступ
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
                Spacing = spacing,
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
        /// Удаляет последний лист раскладки.
        /// </summary>
        private void RemoveSheet_Click(object sender, RoutedEventArgs e)
        {
            if (owner is not CutControl cut || cut.Items == null) return;

            var itemToRemove = cut.Items.LastOrDefault(i => i.NestingSheet != null);

            if (itemToRemove == null)
            {
                MainWindow.M.StatusBegin("Нет листов для удаления", MainWindow.StatusMessageType.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Удалить последний лист? Все детали на нём будут удалены с раскладки (их общее количество уменьшится).",
                "Удаление листа", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ExecuteSheetRemoval(itemToRemove, cut);
            }
        }

        /// <summary>
        /// Удаляет конкретный лист по клику на кнопку в интерфейсе.
        /// </summary>
        private void DeleteSpecificSheet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not LaserItem itemToRemove) return;
            if (owner is not CutControl cut || cut.Items == null) return;

            int totalPartsOnSheet = itemToRemove.NestingSheet?.Parts.Count ?? 0;
            int totalInstances = totalPartsOnSheet * itemToRemove.sheets;

            var result = MessageBox.Show(
                $"Удалить лист {itemToRemove.sheetSize}? Все детали на нём ({totalInstances} шт) будут удалены с раскладки, а их общее количество уменьшится.",
                "Удаление листа", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ExecuteSheetRemoval(itemToRemove, cut);
            }
        }

        /// <summary>
        /// Единый метод для удаления листа.
        /// </summary>
        private void ExecuteSheetRemoval(LaserItem itemToRemove, CutControl cut)
        {
            if (itemToRemove?.NestingSheet == null) return;

            // 1. Уменьшаем счетчик деталей с учетом количества одинаковых листов в группе
            foreach (var placement in itemToRemove.NestingSheet.Parts)
            {
                placement.Part.Count -= itemToRemove.sheets;

                // Страховка от случайных отрицательных значений
                if (placement.Part.Count < 0) placement.Part.Count = 0;

                placement.Part.NotifyTotalChanged();
            }

            // 2. Удаляем сам лист из коллекции
            cut.Items?.Remove(itemToRemove);

            // 3. Пересчитываем итоги и обновляем UI
            RecalculateTotalsUniversal(cut);
            RefreshNestingPreview();

            MainWindow.M.StatusBegin("Лист удалён, детали возвращены в общий список", MainWindow.StatusMessageType.Success);
        }

        /// <summary>
        /// Открывает диалог выбора DXF-файлов, парсит количество из имени и добавляет их как новые детали.
        /// </summary>
        private void AddPartsFromDxf_Click(object sender, RoutedEventArgs e)
        {
            if (owner is not CutControl cut) return;

            // Подсвечиваем соответствующую кнопку в сайдбаре
            DxfToggle.IsChecked = true;

            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите DXF-файлы деталей",
                Filter = "DXF файлы (*.dxf)|*.dxf|Все файлы (*.*)|*.*",
                Multiselect = true,
                InitialDirectory = MainWindow.M.lastInputDirectory
            };

            if (openFileDialog.ShowDialog() == true)
            {
                int addedCount = 0;
                int parsedCount = 0;
                var failedFiles = new List<string>();
                var zeroQuantityFiles = new List<string>();
                var (metal, thickness, metalName) = GetMetalAndThickness(owner);

                Regex countRegex = new Regex(@"(?i)n(\d+)|(\d+)\s*шт");

                foreach (var filePath in openFileDialog.FileNames)
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        int detectedCount = 0;

                        Match countMatch = countRegex.Match(fileName);
                        if (countMatch.Success)
                        {
                            string numStr = countMatch.Groups[1].Success ? countMatch.Groups[1].Value : countMatch.Groups[2].Value;

                            if (int.TryParse(numStr, out int qty) && qty > 0)
                            {
                                detectedCount = qty;
                                parsedCount++;
                            }
                            else
                            {
                                zeroQuantityFiles.Add(fileName);
                            }
                        }
                        else
                        {
                            zeroQuantityFiles.Add(fileName);
                        }

                        var doc = DxfDocument.Load(filePath);
                        var calculationGeometry = DxfToWpfConverter.ConvertToPathGeometryWithClosedContours(doc);

                        if (calculationGeometry == null || calculationGeometry.IsEmpty())
                            throw new InvalidOperationException("DXF не содержит распознаваемых замкнутых контуров.");

                        PartType detectedType = GeometryAnalyzer.DetectPartType(calculationGeometry);
                        var bounds = calculationGeometry.Bounds;

                        var part = new Part(fileName)
                        {
                            Count = detectedCount,
                            Metal = metalName,
                            Destiny = thickness,
                            Width = Math.Ceiling(bounds.Width),
                            Height = Math.Ceiling(bounds.Height),
                            PartType = detectedType,
                            DisplayGeometry = calculationGeometry,
                        };

                        if (part.DisplayGeometry.CanFreeze)
                            part.DisplayGeometry.Freeze();

                        if (metal is not null)
                            UpdatePartAfterEdit(part, metal, thickness, true);

                        var partControl = new PartControl(owner, cut.work, part);
                        Parts.Add(partControl);

                        cut.Parts ??= new();
                        if (!cut.Parts.Contains(partControl)) cut.Parts.Add(partControl);

                        cut.PartDetails ??= new();
                        if (!cut.PartDetails.Contains(part)) cut.PartDetails.Add(part);

                        addedCount++;
                    }
                    catch (Exception ex)
                    {
                        failedFiles.Add(Path.GetFileName(filePath));
                        System.Diagnostics.Trace.WriteLine($"Ошибка импорта {filePath}: {ex.Message}");
                    }
                }

                if (addedCount > 0)
                {
                    if (parsedCount > 0 && zeroQuantityFiles.Count > 0)
                    {
                        string zeroFilesList = string.Join("\n", zeroQuantityFiles.Take(5));
                        string moreText = zeroQuantityFiles.Count > 5 ? $"\n...и ещё {zeroQuantityFiles.Count - 5} файлов." : "";

                        MessageBox.Show(
                            $"Успешно добавлено деталей: {addedCount}.\n\n" +
                            $"✅ Количество распознано для {parsedCount} шт.\n" +
                            $"⚠️ Для следующих деталей количество не найдено (установлено 0):\n{zeroFilesList}{moreText}\n\n" +
                            $"Пожалуйста, проверьте и установите количество вручную.",
                            "Импорт завершен с предупреждением",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    else if (parsedCount > 0)
                    {
                        MainWindow.M.StatusBegin($"Успешно добавлено деталей: {addedCount}. Количество распознано.", MainWindow.StatusMessageType.Success);
                    }
                    else
                    {
                        MainWindow.M.StatusBegin($"Успешно добавлено деталей: {addedCount}. Количество не распознано, установите его вручную.", MainWindow.StatusMessageType.Warning);
                    }
                }

                if (failedFiles.Count > 0)
                {
                    string fileList = string.Join("\n", failedFiles.Take(5));
                    string moreText = failedFiles.Count > 5 ? $"\n...и ещё {failedFiles.Count - 5} файлов." : "";
                    MessageBox.Show($"Не удалось прочитать следующие файлы:\n{fileList}{moreText}\n\n" +
                                    "Пересохраните их в CAD-программе и попробуйте снова.",
                                    "Ошибка импорта", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // Сбрасываем подсветку кнопки в сайдбаре
            DxfToggle.IsChecked = false;
            UpdateEmptyState();
        }

        /// <summary>
        /// Пересчитывает раскладку (автонестинг) для листового металла или труб.
        /// </summary>
        private async void AutoNest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn) return;

            // 🔥 1. ПОДГОТОВКА ДАННЫХ И ВЫЗОВ ДИАЛОГА
            if (owner is not ICut cut || cut.Items == null || cut.PartDetails == null)
            {
                btn.IsChecked = false;
                MainWindow.M.StatusBegin("Нет данных для пересчета раскладки", MainWindow.StatusMessageType.Warning);
                return;
            }

            var (metal, thickness, metalName) = GetMetalAndThickness(owner);
            if (metal == null || string.IsNullOrEmpty(metalName) || thickness <= 0)
            {
                btn.IsChecked = false;
                MessageBox.Show("Не указан материал или толщина заготовки в настройках.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var partsToNest = cut.PartDetails.Where(p => p.Count > 0).ToList();
            if (!partsToNest.Any())
            {
                btn.IsChecked = false;
                MessageBox.Show("В расчете нет деталей для автоматической раскладки.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 🔥 ПОДГОТОВКА ЗНАЧЕНИЙ ПО УМОЛЧАНИЮ ИЗ НАСТРОЕК
            bool isSheet = cut is CutControl;
            double defWidth = isSheet ? (cut as CutControl)?.work.type.A ?? 3000 : 0;
            double defHeight = isSheet ? (cut as CutControl)?.work.type.B ?? 1500 : 0;
            double defSpacing = isSheet ? 10 : 0; // Значение по умолчанию

            double defLength = !isSheet ? (cut as PipeControl)?.work.type.L ?? 6000 : 0;
            double defClamp = !isSheet ? 340 : 0;
            double defLoss = !isSheet ? 10 : 0;

            // 🔥 ВЫЗОВ ДИАЛОГОВОГО ОКНА
            var settingsDialog = new AutoNestSettingsWindow(
                isSheetMode: isSheet,
                defaultWidth: defWidth, defaultHeight: defHeight, defaultSpacing: defSpacing,
                defaultLength: defLength, defaultClamp: defClamp, defaultLoss: defLoss)
            {
                Owner = Window.GetWindow(this)
            };

            if (settingsDialog.ShowDialog() != true || !settingsDialog.IsConfirmed)
            {
                // Пользователь нажал "Отмена"
                btn.IsChecked = false;
                return;
            }

            // 🔥 2. ВКЛЮЧАЕМ оранжевую подсветку и курсор ожидания
            btn.IsChecked = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                MainWindow.M.StatusBegin("Выполняется автоматическая раскладка...", MainWindow.StatusMessageType.Info);

                object? result = null;

                if (cut is CutControl)
                {
                    var sheetParts = partsToNest.Where(p => p.DisplayGeometry != null).ToList();
                    if (!sheetParts.Any())
                    {
                        MessageBox.Show("Нет деталей с геометрией для листовой раскладки.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    result = await System.Threading.Tasks.Task.Run(() =>
                    {
                        //  Проверяем UseAutoNesting из диалога
                        if (settingsDialog.UseAutoNesting)
                        {
                            // Алгоритм сам подберет размеры листов
                            return SkylineNestingHelper.CreateNestingSkylineAutoSheet(
                                sheetParts,
                                metalName,
                                thickness,
                                settingsDialog.Spacing);
                        }
                        else
                        {
                            // Используем размеры, заданные пользователем вручную
                            return SkylineNestingHelper.CreateNestingSkyline(
                                sheetParts,
                                sheetWidth: settingsDialog.SheetWidth,
                                sheetHeight: settingsDialog.SheetHeight,
                                spacing: settingsDialog.Spacing);
                        }
                    });
                }
                else if (cut is PipeControl or SawControl)
                {
                    result = await System.Threading.Tasks.Task.Run(() =>
                    {
                        return NestingHelper.CreateNestingForPipeBatch(
                            partsToNest,
                            settingsDialog.PipeLength,
                            settingsDialog.ClampZone,
                            settingsDialog.CutLoss);
                    });
                }

                if (result == null)
                {
                    MessageBox.Show("Алгоритму не удалось выполнить раскладку.", "Ошибка раскладки", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (cut is CutControl cc)
                {
                    var oldNestingItems = cc.Items!.OfType<LaserItem>().Where(i => i.NestingSheet != null).ToList();
                    foreach (var item in oldNestingItems) cc.Items!.Remove(item);
                }
                else if (cut is PipeControl pc)
                {
                    var oldPipeItems = pc.Items!.OfType<LaserItem>().Where(i => i.PipeStocks != null && i.PipeStocks.Count > 0).ToList();
                    foreach (var item in oldPipeItems) pc.Items!.Remove(item);
                }
                else if (cut is SawControl sc)
                {
                    var oldPipeItems = sc.Items!.OfType<LaserItem>().Where(i => i.PipeStocks != null && i.PipeStocks.Count > 0).ToList();
                    foreach (var item in oldPipeItems) sc.Items!.Remove(item);
                }

                if (cut is CutControl cc2 && result is List<NestingSheet> sheets)
                {
                    var groupedSheets = GroupIdenticalSheets(sheets); // Ваш существующий метод для листов

                    foreach (var group in groupedSheets)
                    {
                        var sheet = group.Key;
                        int sheetCount = group.Value;
                        if (sheet.Parts.Count == 0) continue;

                        double sheetArea = sheet.OptimizedWidth * sheet.OptimizedHeight;
                        double density = metal?.Density ?? 0;
                        double sheetMass = sheetArea * thickness * density / 1_000_000;

                        double sheetWay = sheet.Parts.Sum(p => p.Part.Way);
                        int sheetPinholes = sheet.Parts.Sum(p => int.TryParse(p.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var val) ? val : 0);

                        cc2.Items?.Add(new LaserItem
                        {
                            sheets = sheetCount,
                            sheetSize = $"{sheet.OptimizedWidth:0}x{sheet.OptimizedHeight:0}",
                            way = (float)sheetWay,
                            pinholes = sheetPinholes,
                            mass = (float)sheetMass,
                            metal = metalName,
                            destiny = thickness.ToString(),
                            NestingSheet = sheet
                        });
                    }
                }
                else if ((cut is PipeControl pc2 || cut is SawControl sc2) && result is List<PipeStock> pipeStocks)
                {
                    if (pipeStocks.Count > 0)
                    {
                        var groupedStocks = GroupIdenticalStocks(pipeStocks);

                        foreach (var group in groupedStocks)
                        {
                            var stock = group.Key;
                            int stockCount = group.Value;
                            if (stock.Placements.Count == 0) continue;

                            double stockMass = CalculatePipeStockMass(stock, metal, thickness);
                            double stockWay = stock.Placements.Sum(p => p.Part.Way);
                            int stockPinholes = stock.Placements.Sum(p => int.TryParse(p.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var pin) ? pin : 0);

                            var newItem = new LaserItem
                            {
                                sheets = stockCount,
                                sheetSize = $"{stock.OptimizedLength:0}",
                                way = (float)stockWay,
                                pinholes = stockPinholes,
                                mass = (float)stockMass,
                                metal = metalName,
                                destiny = thickness.ToString(),
                                PipeStocks = new List<PipeStock> { stock }
                            };

                            // Добавляем в нужный контрол в зависимости от типа
                            if (cut is PipeControl p) p.Items?.Add(newItem);
                            else if (cut is SawControl s) s.Items?.Add(newItem);
                        }
                    }
                }

                RecalculateTotalsUniversal(cut);
                RefreshNestingPreview();

                MainWindow.M.StatusBegin("Авто-раскладка завершена.", MainWindow.StatusMessageType.Success);
            }
            catch (Exception ex)
            {
                MainWindow.M.StatusBegin($"Ошибка при авто-раскладке: {ex.Message}", MainWindow.StatusMessageType.Error);
            }
            finally
            {
                btn.IsChecked = false;
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Универсальный пересчет итогов для CutControl и PipeControl после автонестинга
        /// </summary>
        private static void RecalculateTotalsUniversal(object controller)
        {
            if (controller is CutControl cut)
            {
                if (cut.PartDetails is null) return;

                cut.WayTotal = cut.PartDetails.Sum(p => p.Way * p.Count);
                cut.MassTotal = cut.PartDetails.Sum(p => p.Mass * p.Count);

                cut.SumProperties(cut.Items!);
                cut.work.type.CreateSort();
            }
            else if (controller is PipeControl pipe)
            {
                if (pipe.PartDetails is null) return;

                pipe.WayTotal = pipe.PartDetails.Sum(p => p.Way * p.Count);
                pipe.MassTotal = pipe.PartDetails.Sum(p => p.Mass * p.Count);

                double totalWay = 0;
                int totalPinholes = 0;
                int totalStocks = 0;
                double stockLength = pipe.work.type.L; // Значение по умолчанию

                var pipeItems = pipe.Items?.OfType<LaserItem>()
                    .Where(i => i.PipeStocks != null && i.PipeStocks.Count > 0)
                    .ToList() ?? new List<LaserItem>();

                foreach (var item in pipeItems)
                {
                    totalWay += item.way * item.sheets;
                    totalPinholes += item.pinholes * item.sheets;
                    totalStocks += item.sheets;

                    if (double.TryParse(item.sheetSize, out double len))
                    {
                        stockLength = len;
                    }
                }

                // Обновляем значения по аналогии с AddBatchToPipeControl
                pipe.Way = (float)totalWay;
                pipe.Pinhole = totalPinholes;
                pipe.work.type.Count = totalStocks;

                // Пересчет длины заготовки (Mold)
                pipe.Mold = (float)Math.Round(stockLength * totalStocks * 0.95f / 1000, 1);

                pipe.SetTotalProperties();
            }
            // 🔥 НОВАЯ ВЕТКА: Полная аналогия с PipeControl для лентопила
            else if (controller is SawControl saw)
            {
                if (saw.PartDetails is null) return;

                double totalWay = 0;
                int totalPinholes = 0;
                int totalStocks = 0;
                double stockLength = saw.work.type.L; // Значение по умолчанию для лентопила

                var sawItems = saw.Items?.OfType<LaserItem>()
                    .Where(i => i.PipeStocks != null && i.PipeStocks.Count > 0)
                    .ToList() ?? new List<LaserItem>();

                foreach (var item in sawItems)
                {
                    totalWay += item.way * item.sheets;
                    totalPinholes += item.pinholes * item.sheets;
                    totalStocks += item.sheets;

                    if (double.TryParse(item.sheetSize, out double len))
                    {
                        stockLength = len;
                    }
                }

                // Обновляем значения
                saw.Way = (float)totalWay;
                saw.Pinhole = totalPinholes;
                saw.work.type.Count = totalStocks;

                saw.SetTotalProperties();
            }
        }

        /// <summary>
        /// Показывает большие кнопки добавления деталей, если список пуст.
        /// Скрывает кнопки, если в списке есть детали.
        /// </summary>
        public void UpdateEmptyState()
        {
            if (partsList == null || partsList.Items == null) return;

            bool hasParts = partsList.Items.Count > 0;

            if (hasParts)
            {
                EmptyStateGrid.Visibility = Visibility.Collapsed;
                partsScroll.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyStateGrid.Visibility = Visibility.Visible;
                partsScroll.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        //-------------Нестинг-------------//
        #region
        // Добавить стандартную деталь
        private void Add_StandartPart_Click(object sender, RoutedEventArgs e)
        {
            // Подсвечиваем соответствующую кнопку в сайдбаре
            StandardPartsToggle.IsChecked = true;

            try
            {
                (Metal? metal, float thickness, string? metalName) = GetMetalAndThickness(owner);
                if (metal == null || string.IsNullOrEmpty(metalName)) return;

                var templatePart = CreateStandardPart(metalName, thickness, 0);
                if (templatePart == null) return;

                PartPreviewGenerator.EnsureDisplayGeometry(templatePart);

                var window = new StandartPartWindow(templatePart);
                if (window.ShowDialog() == true)
                {
                    var batchedParts = window.GetBatchedParts();
                    var bendsInfo = window.GetBendsInfoForBatch();

                    if (batchedParts.Count > 0)
                    {
                        foreach (var part in batchedParts)
                        {
                            UpdatePartAfterEdit(part, metal, thickness);
                        }

                        if (owner is CutControl cut)
                        {
                            AddBatchToCutControl(cut, batchedParts, metal, window.UseAutoNesting, window.CustomSpacing, bendsInfo);
                        }
                        else if (owner is PipeControl pipe)
                        {
                            AddBatchToPipeControl(pipe, batchedParts, metal, window.CustomClampZone, window.CustomCutLoss);
                        }
                        else if (owner is SawControl saw)
                        {
                            AddBatchToSawControl(saw, batchedParts, metal, window.CustomClampZone, window.CustomCutLoss);
                        }
                    }
                }
            }
            finally
            {
                // Сбрасываем подсветку кнопки в сайдбаре
                StandardPartsToggle.IsChecked = false;
                UpdateEmptyState();
            }
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
                "Круг" => PartType.Circle,
                "Квадрат" => PartType.SquareBar,
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
            if (partType == PartType.Rectangle) part.Width = part.Height = 100;
            else
            {
                part.Width = type != null ? type.A : 100;
                part.Height = type != null ? type.B : 100;
                part.Length = 1000; // базовая длина 1 метр
            }

            return part;
        }

        public void UpdatePartAfterEdit(Part part, Metal metal, float thickness, bool isOriginal = false)
        {
            if (part.PropsDict == null) part.PropsDict = new Dictionary<int, List<string>>();

            bool isSheetPart = part.PartType == PartType.Round ||
                               part.PartType == PartType.Rectangle ||
                               part.PartType == PartType.Triangle ||
                               part.PartType == PartType.Custom;

            int pinholes = 0;
            double cuttingLength = 0;

            // === ШАГ 1: Генерация геометрии, если ее нет ===
            if (!isOriginal)
            {
                if (part.PartType != PartType.Custom || part.DisplayGeometry == null)
                {
                    part.DisplayGeometry = null;
                    if (isSheetPart)
                        PartPreviewGenerator.EnsureDisplayGeometryWithHoles(part);
                    else
                        PartPreviewGenerator.EnsureDisplayGeometry(part);
                }
            }

            // === ШАГ 2: Расчёт длины реза и проколов ===
            if (part.DisplayGeometry != null)
            {
                cuttingLength = TechItemCalculator.CalculateCuttingLength(part.DisplayGeometry);

                if (!isSheetPart && part.HoleGroups?.Count > 0)
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
                double area = 0;

                if (part.DisplayGeometry != null)
                {
                    try
                    {
                        area = CalculateExact2DArea(part.DisplayGeometry);
                    }
                    catch
                    {
                        area = 0; // Игнорируем исключения, если геометрия "битая"
                    }
                }

                // Фоллбэк: если точная площадь не посчиталась, равна 0 или ушла в минус
                if (area <= 0)
                {
                    area = CalculateCrossSectionArea(part.PartType, part.Width, part.Height, 1);
                }

                part.Mass = (float)Math.Round(area * thickness * metal.Density / 1_000_000, 3);

                string dimensionString = part.PartType switch
                {
                    PartType.Round => $"Ø{part.Width}",
                    _ => $"{part.Width}x{part.Height}"
                };

                part.PropsDict[100] = new List<string>
        {
            $"{part.Width}",
            $"{part.Height}",
            dimensionString
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
        public void AddBatchToCutControl(CutControl cut, List<Part> parts, Metal metal, bool isAutoSheet = true,
            double customSpacing = 0, Dictionary<Part, Dictionary<double, (int Count, double BendLength)>>? bendsInfo = null)
        {
            if (parts == null || parts.Count == 0)
                return;

            // === СОЗДАЁМ ЕДИНУЮ РАСКЛАДКУ ДЛЯ ВСЕХ ДЕТАЛЕЙ ===
            var nestingSheets = isAutoSheet ?
                SkylineNestingHelper.CreateNestingSkylineAutoSheet(parts, cut.work.type.MetalDrop.Text, cut.work.type.S, customSpacing)
                :
                SkylineNestingHelper.CreateNestingSkyline(parts, cut.work.type.A, cut.work.type.B, customSpacing);

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

                // 🔥 АВТОМАТИЧЕСКОЕ СОЗДАНИЕ БЛОКОВ ГИБКИ
                if (bendsInfo != null && bendsInfo.TryGetValue(part, out var partBends))
                {
                    foreach (var bendData in partBends.Values)
                    {
                        var bendControl = new BendControl(partControl);
                        partControl.AddControl(bendControl);

                        // Устанавливаем количество гибов
                        bendControl.SetBend(bendData.Count.ToString());

                        // Устанавливаем диапазон длины гиба (полку)
                        bendControl.SetShelf(GetShelfIndex(bendData.BendLength));
                    }
                }
            }

            // Обновляем итоговые значения
            RecalculateTotalsUniversal(cut);
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
        /// Определяет индекс диапазона длины гиба для ComboBox (ShelfDrop) на основе длины в мм.
        /// Диапазоны в BendDict: "до 0.5" (<500), "0.5-1" (<1000), "1-1.3" (<1300), "1.3-2.55" (<2550)
        /// </summary>
        private int GetShelfIndex(double bendLengthMm)
        {
            if (bendLengthMm < 500) return 0;      // "до 0.5"
            if (bendLengthMm < 1000) return 1;     // "0.5-1"
            if (bendLengthMm < 1300) return 2;     // "1-1.3"
            return 3;                               // "1.3-2.55"
        }

        /// <summary>
        /// Добавляет КОЛЛЕКЦИЮ трубных деталей с оптимальным нестингом и группировкой одинаковых хлыстов
        /// </summary>
        public void AddBatchToPipeControl(PipeControl pipe, List<Part> parts, Metal metal, double clampZone = 340, double cutLoss = 10)
        {
            if (parts == null || parts.Count == 0 || pipe.work?.type == null)
                return;

            // Создаём раскладку по хлыстам
            var pipeStocks = NestingHelper.CreateNestingForPipeBatch(parts, pipe.work.type.L, clampZone, cutLoss);

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
        private void AddBatchToSawControl(SawControl saw, List<Part> parts, Metal metal, double clampZone = 340, double cutLoss = 10)
        {
            if (parts == null || parts.Count == 0 || saw.work?.type == null)
                return;

            // Создаём раскладку по хлыстам
            var pipeStocks = NestingHelper.CreateNestingForPipeBatch(parts, saw.work.type.L, clampZone, cutLoss);

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

        // Возвращает сам металл, толщину и наименование металла родителя
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

        // Расчет площади окрашивания детали
        private double SquareToPaint(Part part)
        {
            // 1. Извлекаем общие данные через pattern matching, чтобы избежать дублирования switch
            var (tubeType, length, way, mass, a, b, s, selectedIndex, typeDetailText, channelsSquare, beamDict) = owner switch
            {
                PipeControl pipe => (
                    pipe.Tube,
                    part.Length,
                    part.Way,
                    part.Mass,
                    (double)pipe.work.type.A,
                    (double)pipe.work.type.B,
                    (double)pipe.work.type.S,
                    pipe.work.type.SortDrop.SelectedIndex,
                    pipe.work.type.TypeDetailDrop.Text,
                    pipe.work.type.ChannelsSquare,
                    pipe.work.type.BeamDict
                ),
                SawControl saw => (
                    saw.Tube,
                    part.Length,
                    part.Way,
                    part.Mass,
                    (double)saw.work.type.A,
                    (double)saw.work.type.B,
                    (double)saw.work.type.S,
                    saw.work.type.SortDrop.SelectedIndex,
                    saw.work.type.TypeDetailDrop.Text,
                    saw.work.type.ChannelsSquare,
                    saw.work.type.BeamDict
                ),
                _ => throw new InvalidOperationException("Неподдерживаемый тип владельца (owner)")
            };

            // 2. Единый расчет площади (мм² переводим в м² делением на 1_000_000.0)
            return tubeType switch
            {
                // Стандартные профили
                TubeType.rect => length * (a + b) * 2 / 1_000_000.0,
                TubeType.square => length * (a + b) * 2 / 1_000_000.0,
                TubeType.round => length * a * Math.PI / 1_000_000.0,
                TubeType.circle => 2 * length * a * Math.PI / 1_000_000.0,
                TubeType.rod => 2 * (length * a + way * b + a * b) / 1_000_000.0,

                // Упрощенные формулы развертки для уголков и фасонных профилей
                TubeType.corner => length * s * (a + a - s) / 1_000_000.0,
                TubeType.freeform => length * s * (a + b - s) / 1_000_000.0,

                // Справочные данные для швеллеров и двутавров 
                // (масса в кг делится на 1000.0 для перевода в тонны, так как справочник дает м²/т)
                TubeType.channel => channelsSquare[selectedIndex] * (mass / 1000.0),
                TubeType.ibeam => beamDict[typeDetailText][selectedIndex].Item2 * (mass / 1000.0),

                _ => 0
            };
        }

        /// <summary>
        /// Рассчитывает площадь поперечного сечения профиля в мм²
        /// </summary>
        /// <param name="partType">Тип профиля</param>
        /// <param name="width">Ширина/диаметр/полка (мм)</param>
        /// <param name="height">Высота/вторая полка (мм), для круглых и квадратных прутков — не используется</param>
        /// <param name="thickness">Толщина стенки/полки (мм), для сплошных прутков — игнорируется</param>
        /// <returns>Площадь сечения в мм²</returns>
        public static double CalculateCrossSectionArea(PartType partType,
            double width, double height, double thickness)
        {
            // Защита от отрицательных и нулевых размеров
            if (width <= 0) return 0;

            // Валидация: толщина не должна превышать 1/3 от меньшего линейного размера
            // (применяется только к полым и профильным сечениям, на сплошные прутки не влияет)
            double minDim = Math.Min(width, height > 0 ? height : width);
            double t = thickness >= minDim / 3 ? Math.Max(0.1, minDim / 3 - 0.1) : thickness;

            return partType switch
            {
                PartType.RoundTube => Math.PI * (
                    Math.Pow(width / 2, 2) -
                    Math.Pow(Math.Max(0, width / 2 - t), 2)),

                PartType.RectangularTube =>
                    width * height -
                    Math.Max(0, width - 2 * t) * Math.Max(0, height - 2 * t),

                PartType.Angle => t * (width + height - t),
                PartType.Channel => t * (width + 2 * height - 2 * t),
                PartType.IBeam => t * (2 * width + height - 2 * t),

                PartType.Circle => Math.PI * Math.Pow(width / 2.0, 2),
                PartType.SquareBar => width * width,

                _ => 0
            };
        }

        /// <summary>
        /// Рассчитывает площадь сечения детали
        /// </summary>
        public static double CalculateCrossSectionArea(Part part, double thickness)
        {
            return CalculateCrossSectionArea(part.PartType,
                part.Width, part.Height, thickness);
        }

        public static double CalculateExact2DArea(PathGeometry geometry)
        {
            if (geometry == null || geometry.Figures.Count == 0) return 0;

            var flattened = geometry.GetFlattenedPathGeometry();
            var contours = new List<List<Point>>();

            foreach (var figure in flattened.Figures)
            {
                if (!figure.IsClosed) continue;

                var points = new List<Point> { figure.StartPoint };
                foreach (var segment in figure.Segments)
                {
                    if (segment is LineSegment line)
                        points.Add(line.Point);
                    else if (segment is PolyLineSegment poly)
                        points.AddRange(poly.Points);
                }

                // Убираем дублирующую замыкающую точку, если она есть
                if (points.Count > 1 && (points[0] - points[points.Count - 1]).Length < 1e-7)
                    points.RemoveAt(points.Count - 1);

                if (points.Count >= 3)
                    contours.Add(points);
            }

            if (contours.Count == 0) return 0;

            double totalArea = 0;

            for (int i = 0; i < contours.Count; i++)
            {
                double area = Math.Abs(ShoelaceArea(contours[i]));

                // Глубина вложенности: сколько других контуров содержат данный
                int depth = 0;
                for (int j = 0; j < contours.Count; j++)
                {
                    if (i == j) continue;
                    if (IsPointInPolygon(contours[i][0], contours[j]))
                        depth++;
                }

                // Чётная глубина -> материал (+), нечётная -> отверстие (−).
                // Это в точности правило EvenOdd, которым WPF заполняет геометрию.
                totalArea += (depth % 2 == 0) ? area : -area;
            }

            return Math.Abs(totalArea);
        }

        private static double ShoelaceArea(List<Point> pts)
        {
            double s = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p1 = pts[i];
                var p2 = pts[(i + 1) % pts.Count];
                s += p1.X * p2.Y - p2.X * p1.Y;
            }
            return s / 2.0;
        }

        private static bool IsPointInPolygon(Point p, List<Point> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) &&
                    p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                {
                    inside = !inside;
                }
            }
            return inside;
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
        #endregion

        //-------------Экспорт-------------//
        #region
        /// <summary>
        /// Экспортирует все листы раскладки в PDF-файл: общая сводка, материал/толщина, 
        /// затем каждый лист с картинкой и компактной таблицей деталей справа.
        /// </summary>
        private void ExportNestingToPdf_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn) return;
            btn.IsChecked = true;

            try
            {
                if (owner is CutControl cut && cut.Items != null)
                {
                    // === ВАШ СУЩЕСТВУЮЩИЙ КОД ДЛЯ ЛИСТОВ ===
                    var sheetsToExport = cut.Items.Where(i => i.NestingSheet != null && i.NestingSheet.Parts.Count > 0).ToList();
                    if (!sheetsToExport.Any())
                    {
                        MessageBox.Show("Нет листов раскладки для экспорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    string metalName = cut.work.type.MetalDrop.Text;
                    float thickness = cut.work.type.S;
                    string description = "";
                    if ((metalName.Contains("ст") && thickness >= 3) || (metalName.Contains("хк") && thickness < 3)) description = $"s{thickness}";
                    else if (metalName.Contains("амг2")) description = $"al{thickness}";
                    else if (metalName.Contains("амг") || metalName.Contains("д16")) description = $"al{thickness} {metalName}";
                    else if (metalName.Contains("латунь")) description = $"br{thickness}";
                    else if (metalName.Contains("медь")) description = $"cu{thickness}";
                    else description = $"s{thickness} {metalName}";

                    if (cut.IsGrooved) description += " рифл";
                    if (cut.work.type.CheckMetal.IsChecked is false) description += " Давальч";

                    var saveDialog = new SaveFileDialog { Filter = "PDF файлы (*.pdf)|*.pdf", FileName = $"{description}.pdf", InitialDirectory = MainWindow.M.lastInputDirectory };
                    if (saveDialog.ShowDialog() != true) return;

                    MainWindow.M.StatusBegin("Генерация PDF...", MainWindow.StatusMessageType.Info);
                    GenerateNestingPdf(saveDialog.FileName);
                    MainWindow.M.StatusBegin($"PDF успешно сохранен: {Path.GetFileName(saveDialog.FileName)}", MainWindow.StatusMessageType.Success);
                }
                else if (owner is PipeControl pipe && pipe.Items != null)
                {
                    ExportPipeOrSaw(pipe, isSaw: false);
                }
                else if (owner is SawControl saw && saw.Items != null)
                {
                    ExportPipeOrSaw(saw, isSaw: true);
                }
                else
                {
                    MainWindow.M.StatusBegin("Нет данных для экспорта", MainWindow.StatusMessageType.Warning);
                }
            }
            finally
            {
                btn.IsChecked = false;
            }
        }

        private void ExportPipeOrSaw(ICut cut, bool isSaw)
        {
            var stocks = cut.Items!.OfType<LaserItem>().Where(i => i.PipeStocks != null && i.PipeStocks.Count > 0).ToList();
            if (!stocks.Any())
            {
                MessageBox.Show("Нет раскладок хлыстов для экспорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string fileName = GetPipeNestingFileName(isSaw);

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = fileName,
                InitialDirectory = MainWindow.M.lastInputDirectory
            };

            if (saveDialog.ShowDialog() == true)
            {
                MainWindow.M.StatusBegin("Генерация PDF...", MainWindow.StatusMessageType.Info);
                GeneratePipeNestingPdf(saveDialog.FileName, isSaw);
                MainWindow.M.StatusBegin($"PDF успешно сохранен: {Path.GetFileName(saveDialog.FileName)}", MainWindow.StatusMessageType.Success);
            }
        }

        /// <summary>
        /// Генерирует PDF-файл раскладки по указанному пути (без диалоговых окон).
        /// Обновленная структура: Сводка сверху -> Листы (картинка во всю ширину + таблица под ней) -> Общая спецификация в конце.
        /// </summary>
        public void GenerateNestingPdf(string filePath)
        {
            if (owner is not CutControl cut || cut.Items == null) return;

            var sheetsToExport = cut.Items
                .Where(i => i.NestingSheet != null && i.NestingSheet.Parts.Count > 0)
                .ToList();

            if (!sheetsToExport.Any()) return;

            try
            {
                QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

                string metalName = cut.work.type.MetalDrop.Text;
                float thickness = cut.work.type.S;

                var sheetData = new List<(
                    byte[] ImageBytes, string Title, int SheetCount, string? SheetSize, double Spacing, int PartsCount, List<PartTableData> PartsTable)>();

                int index = 1;
                foreach (var item in sheetsToExport)
                {
                    if (item.NestingSheet is null) continue;

                    var preview = new NestingPreviewControl();
                    preview.ShowSheet(item.NestingSheet);

                    byte[] pngBytes = WpfImageHelper.RenderVisualToPng(preview, 1920, 1080);

                    var partsTable = item.NestingSheet.Parts
                        .GroupBy(p => new { p.Part.Title, p.Part.Width, p.Part.Height, p.Part.PartType })
                        .Select(g => new PartTableData
                        {
                            Name = g.Key.Title,
                            Dimensions = $"{g.Key.Width:0} × {g.Key.Height:0}",
                            QtyOnSheet = g.Count(),
                            TotalQty = g.Count() * item.sheets,
                            WeightKg = g.Sum(p => (double)p.Part.Mass)
                        })
                        .OrderByDescending(p => p.TotalQty)
                        .ToList();

                    sheetData.Add((pngBytes, $"Лист {index} из {sheetsToExport.Count}", item.sheets, item.sheetSize, item.NestingSheet.Spacing, item.NestingSheet.Parts.Count, partsTable));
                    index++;
                }

                // 🔥 1. ФОРМИРУЕМ ВЕРХНЮЮ СВОДКУ ПО ЛИСТАМ
                var sheetSummary = sheetData
                    .GroupBy(s => new { s.SheetSize, s.SheetCount })
                    .Select(g => new
                    {
                        Size = g.Key.SheetSize,
                        TotalSheets = g.Sum(x => x.SheetCount) // Суммируем физическое количество листов этого размера
                    })
                    .OrderByDescending(x => x.TotalSheets)
                    .ToList();

                string totalSheetsText = string.Join(", ", sheetSummary.Select(g => $"{g.TotalSheets} шт. {g.Size}"));
                int totalPhysicalSheets = sheetSummary.Sum(x => x.TotalSheets);

                // 🔥 2. ФОРМИРУЕМ ОБЩУЮ СВОДНУЮ ТАБЛИЦУ (для размещения в конце)
                var masterPartsList = sheetData
                    .SelectMany(s => s.PartsTable)
                    .GroupBy(p => new { p.Name, p.Dimensions })
                    .Select(g => new PartTableData
                    {
                        Name = g.Key.Name,
                        Dimensions = g.Key.Dimensions,
                        TotalQty = g.Sum(p => p.TotalQty),
                        WeightKg = g.Sum(p => p.WeightKg)
                    })
                    .OrderByDescending(p => p.TotalQty)
                    .ToList();

                // 🔥 3. ГЕНЕРАЦИЯ PDF С НОВОЙ СТРУКТУРОЙ
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(20, QuestPDF.Infrastructure.Unit.Millimetre);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Text("Карта раскроя материала").FontSize(16).Bold().FontColor(Colors.Black);
                            row.RelativeItem().AlignRight().Text($"Дата: {DateTime.Now:dd.MM.yyyy}").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });

                        page.Content().Column(column =>
                        {
                            // ==========================================
                            // ЧАСТЬ А: МАТЕРИАЛ, ТОЛЩИНА И ОБЩАЯ СВОДКА ПО ЛИСТАМ (ВВЕРХУ)
                            // ==========================================
                            column.Item().Border(1).BorderColor(Colors.Blue.Lighten2).Background(Colors.Blue.Lighten4).Padding(10).Row(row =>
                            {
                                row.RelativeItem().Text(x =>
                                {
                                    x.Span("Материал: ").FontSize(11);
                                    x.Span(metalName).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                    x.Span("  |  Толщина: ").FontSize(11);
                                    x.Span($"{thickness} мм").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                });

                                row.RelativeItem().AlignRight().Text(x =>
                                {
                                    x.Span("Всего листов: ").FontSize(11);
                                    x.Span(totalPhysicalSheets.ToString()).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                    x.Span($" ({totalSheetsText})").FontSize(11);
                                });
                            });
                            column.Item().PaddingVertical(10);

                            // ==========================================
                            // ЧАСТЬ Б: ЛИСТЫ РАСКЛАДКИ (КАРТИНКА ВО ВСЮ ШИРИНУ + ТАБЛИЦА ПОД НЕЙ)
                            // ==========================================
                            for (int i = 0; i < sheetData.Count; i++)
                            {
                                var data = sheetData[i];

                                // Заголовок листа
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(data.Title).FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                                    row.RelativeItem().AlignRight().Text(x =>
                                    {
                                        x.Span($"Размер: {data.SheetSize} мм | Отступы: {data.Spacing:0} мм | Деталей: {data.PartsCount} | Листов в группе: ")
                                            .FontSize(10).FontColor(Colors.Grey.Darken1);
                                        x.Span(data.SheetCount.ToString()).FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
                                    });
                                });
                                column.Item().PaddingVertical(5);

                                // 🔥 КАРТИНКА ВО ВСЮ ШИРИНУ (максимально крупно и четко)
                                column.Item()
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Image(data.ImageBytes)
                                    .FitArea();

                                column.Item().PaddingVertical(10);

                                // 🔥 ТАБЛИЦА СПЕЦИФИКАЦИИ ПОД КАРТИНКОЙ
                                column.Item().Text($"Спецификация деталей на листе:").FontSize(11).Bold().FontColor(Colors.Grey.Darken2);
                                column.Item().PaddingVertical(3);

                                column.Item().Table(sheetTable =>
                                {
                                    // Поскольку таблица теперь во всю ширину, делаем колонки пропорционально шире для читаемости
                                    sheetTable.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(4);   // Деталь (широкая)
                                        columns.RelativeColumn(2);   // Размер
                                        columns.RelativeColumn(1.5f); // Вес, кг
                                        columns.RelativeColumn(1);   // На листе
                                    });

                                    sheetTable.Header(header =>
                                    {
                                        header.Cell().Element(TableHeaderStyle).Text("Наименование детали");
                                        header.Cell().Element(TableHeaderStyle).Text("Габариты (мм)");
                                        header.Cell().Element(TableHeaderStyle).AlignCenter().Text("Вес, кг");
                                        header.Cell().Element(TableHeaderStyle).AlignCenter().Text("Кол-во на листе");
                                    });

                                    foreach (var part in data.PartsTable)
                                    {
                                        sheetTable.Cell().Element(TableRowStyle).Text(part.Name);
                                        sheetTable.Cell().Element(TableRowStyle).Text(part.Dimensions);
                                        sheetTable.Cell().Element(TableRowStyle).AlignCenter().Text(part.WeightKg.ToString("0.###"));
                                        sheetTable.Cell().Element(TableRowStyle).AlignCenter().Text(part.QtyOnSheet.ToString());
                                    }
                                });

                                // Разрыв страницы между листами (кроме последнего)
                                if (i < sheetData.Count - 1)
                                {
                                    column.Item().PageBreak();
                                }
                            }

                            // ==========================================
                            // ЧАСТЬ В: ОБЩАЯ СПЕЦИФИКАЦИЯ (В САМОМ КОНЦЕ ДОКУМЕНТА)
                            // ==========================================
                            column.Item().PageBreak(); // Гарантируем, что общая сводка начинается с новой страницы

                            column.Item().Text("Общая спецификация деталей по всему заказу")
                                .FontSize(14).Bold().FontColor(Colors.Black);
                            column.Item().PaddingVertical(5);

                            column.Item().Table(masterTable =>
                            {
                                masterTable.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);   // Наименование
                                    columns.RelativeColumn(2);   // Габариты
                                    columns.RelativeColumn(1.5f); // Вес, кг
                                    columns.RelativeColumn(1);   // Общее кол-во
                                });

                                masterTable.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderStyle).Text("Наименование");
                                    header.Cell().Element(TableHeaderStyle).Text("Габариты (мм)");
                                    header.Cell().Element(TableHeaderStyle).AlignCenter().Text("Общий вес, кг");
                                    header.Cell().Element(TableHeaderStyle).AlignCenter().Text("Общее кол-во");
                                });

                                foreach (var part in masterPartsList)
                                {
                                    masterTable.Cell().Element(TableRowStyle).Text(part.Name);
                                    masterTable.Cell().Element(TableRowStyle).Text(part.Dimensions);
                                    masterTable.Cell().Element(TableRowStyle).AlignCenter().Text(part.WeightKg.ToString("0.###"));
                                    masterTable.Cell().Element(TableRowStyle).AlignCenter().Text(part.TotalQty.ToString()).Bold();
                                }
                            });
                        });

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("Страница ").FontSize(8);
                            x.CurrentPageNumber().FontSize(8);
                            x.Span(" из ").FontSize(8);
                            x.TotalPages().FontSize(8);
                        });
                    });
                }).GeneratePdf(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Auto PDF Export Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Генерирует PDF-файл раскладки труб или лентопила по указанному пути.
        /// </summary>
        public void GeneratePipeNestingPdf(string filePath, bool isSaw = false)
        {
            ICut? cut = isSaw ? (owner as SawControl) : (owner as PipeControl);
            if (cut == null || cut.Items == null) return;

            var allStocks = cut.Items
                .Where(i => i.PipeStocks != null && i.PipeStocks.Count > 0)
                .ToList();

            if (!allStocks.Any()) return;

            try
            {
                QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

                var (metal, thickness, metalName) = GetMetalAndThickness(owner);

                string processName = isSaw ? "Лентопил" : "Труборез";

                // 🔥 1. ИЗВЛЕЧЕНИЕ ТИПА И СЕЧЕНИЯ ПРОФИЛЯ
                string pipeType = "Профиль";
                string section = "";

                if (cut is PipeControl pc && pc.work.type != null)
                {
                    pipeType = pc.work.type.TypeDetailDrop?.Text ?? "Труба";
                    if (pipeType.Contains("Круглая") || pipeType.Contains("Круг"))
                        section = $"Ø{pc.work.type.A:0} × {pc.work.type.S:0} мм";
                    else
                        section = $"{pc.work.type.A:0} × {pc.work.type.B:0} × {pc.work.type.S:0} мм";
                }

                var stockData = new List<(byte[] ImageBytes, string Title, double Length, int StockCount, List<PartTableData> PartsTable)>();
                int index = 1;

                foreach (var item in allStocks)
                {
                    if (item.PipeStocks is null) continue;

                    var stock = item.PipeStocks[0];

                    var preview = new PipeStockVisualizationControl
                    {
                        Stock = stock,
                        Width = 820,
                        Height = 70
                    };

                    byte[] pngBytes = WpfImageHelper.RenderVisualToPng(preview, 850, 100);

                    // 🔥 2. РАСЧЕТ КОЛИЧЕСТВ И ВЕСА
                    var partsTable = stock.Placements
                        .GroupBy(p => new { p.Part.Title, p.Part.Length })
                        .Select(g => new PartTableData
                        {
                            Name = g.Key.Title,
                            Dimensions = $"{g.Key.Length:0} мм",
                            QtyOnSheet = g.Count(), // Количество на ОДНОМ хлысте
                            TotalQty = g.Count() * item.sheets, // Общее количество с учетом повторов хлыста
                            WeightKg = g.Sum(p => (double)p.Part.Mass) // 🔥 Вес ОДНОГО хлыста (не умножаем на sheets)
                        })
                        .OrderByDescending(p => p.TotalQty)
                        .ToList();

                    stockData.Add((pngBytes, $"Хлыст {index} из {allStocks.Count} (повторов: {item.sheets})", stock.StockLength, item.sheets, partsTable));
                    index++;
                }

                // Общая сводка по всем хлыстам
                var masterPartsList = stockData
                    .SelectMany(s => s.PartsTable)
                    .GroupBy(p => new { p.Name, p.Dimensions })
                    .Select(g => new PartTableData
                    {
                        Name = g.Key.Name,
                        Dimensions = g.Key.Dimensions,
                        TotalQty = g.Sum(p => p.TotalQty),
                        WeightKg = g.Sum(p => p.WeightKg * p.TotalQty / p.QtyOnSheet) // 🔥 Пересчитываем общий вес
                    })
                    .OrderByDescending(p => p.TotalQty)
                    .ToList();

                // 🔥 3. ИСПРАВЛЕНИЕ: УМНОЖАЕМ НА sheets ДЛЯ ОБЩЕЙ ДЛИНЫ
                double exactTotalLengthMm = allStocks.Sum(s => s.PipeStocks![0].StockLength * s.sheets);
                double exactTotalLengthM = exactTotalLengthMm / 1000.0;
                int totalStocksCount = allStocks.Sum(g => g.sheets);

                double clampZone = allStocks.FirstOrDefault()?.PipeStocks![0].ClampZone ?? 0;
                double cutLoss = allStocks.FirstOrDefault()?.PipeStocks![0].CutLoss ?? 10;

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(20, QuestPDF.Infrastructure.Unit.Millimetre);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Text($"Карта раскроя: {processName}").FontSize(16).Bold().FontColor(Colors.Black);
                            row.RelativeItem().AlignRight().Text($"Дата: {DateTime.Now:dd.MM.yyyy}").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });

                        page.Content().Column(column =>
                        {
                            column.Item().Border(1).BorderColor(Colors.Green.Lighten2).Background(Colors.Green.Lighten4).Padding(10).Column(headerCol =>
                            {
                                headerCol.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(x =>
                                    {
                                        x.Span("Материал: ").FontSize(11);
                                        x.Span(metalName).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                        x.Span("  |  Тип: ").FontSize(11);
                                        x.Span(pipeType).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                        x.Span("  |  Сечение: ").FontSize(11);
                                        x.Span(section).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                    });
                                });

                                headerCol.Item().PaddingVertical(5);

                                headerCol.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(x =>
                                    {
                                        x.Span("Всего хлыстов: ").FontSize(11);
                                        x.Span(totalStocksCount.ToString()).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                        x.Span($" (зажим: {clampZone:0} мм, пропил: {cutLoss:0} мм)").FontSize(11);
                                    });
                                    row.RelativeItem().AlignRight().Text(x =>
                                    {
                                        x.Span("Общая длина: ").FontSize(11);
                                        x.Span($"{exactTotalLengthM:0.00} м").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                    });
                                });
                            });
                            column.Item().PaddingVertical(10);

                            for (int i = 0; i < stockData.Count; i++)
                            {
                                var data = stockData[i];

                                column.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(data.Title).FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                                    row.RelativeItem().AlignRight().Text($"Длина заготовки: {data.Length:0} мм").FontSize(10).FontColor(Colors.Grey.Darken1);
                                });
                                column.Item().PaddingVertical(5);

                                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Image(data.ImageBytes).FitArea();
                                column.Item().PaddingVertical(10);

                                column.Item().Text($"Детали на данном типе хлыста:").FontSize(11).Bold().FontColor(Colors.Grey.Darken2);
                                column.Item().PaddingVertical(3);

                                column.Item().Table(sheetTable =>
                                {
                                    sheetTable.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1.5f);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });

                                    sheetTable.Header(header =>
                                    {
                                        header.Cell().Element(TableHeaderStyle).Text("Наименование");
                                        header.Cell().Element(TableHeaderStyle).Text("Длина");
                                        header.Cell().Element(TableHeaderStyle).AlignCenter().Text("Вес 1 хлыста, кг");
                                        header.Cell().Element(TableHeaderStyle).AlignCenter().Text("На 1 хлыст");
                                        header.Cell().Element(TableHeaderStyle).AlignCenter().Text("Всего (с повт.)");
                                    });

                                    foreach (var part in data.PartsTable)
                                    {
                                        sheetTable.Cell().Element(TableRowStyle).Text(part.Name);
                                        sheetTable.Cell().Element(TableRowStyle).Text(part.Dimensions);
                                        sheetTable.Cell().Element(TableRowStyle).AlignCenter().Text(part.WeightKg.ToString("0.###"));
                                        // 🔥 Используем сохраненное значение вместо деления
                                        sheetTable.Cell().Element(TableRowStyle).AlignCenter().Text(part.QtyOnSheet.ToString());
                                        sheetTable.Cell().Element(TableRowStyle).AlignCenter().Text(part.TotalQty.ToString()).Bold();
                                    }
                                });

                                if (i < stockData.Count - 1) column.Item().PageBreak();
                            }

                            column.Item().PageBreak();
                            column.Item().Text("Общая спецификация деталей по всему заказу").FontSize(14).Bold().FontColor(Colors.Black);
                            column.Item().PaddingVertical(5);

                            column.Item().Table(masterTable =>
                            {
                                masterTable.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(1);
                                });

                                masterTable.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderStyle).Text("Наименование");
                                    header.Cell().Element(TableHeaderStyle).Text("Длина (мм)");
                                    header.Cell().Element(TableHeaderStyle).AlignCenter().Text("Общий вес, кг");
                                    header.Cell().Element(TableHeaderStyle).AlignCenter().Text("Общее кол-во");
                                });

                                foreach (var part in masterPartsList)
                                {
                                    masterTable.Cell().Element(TableRowStyle).Text(part.Name);
                                    masterTable.Cell().Element(TableRowStyle).Text(part.Dimensions);
                                    masterTable.Cell().Element(TableRowStyle).AlignCenter().Text(part.WeightKg.ToString("0.###"));
                                    masterTable.Cell().Element(TableRowStyle).AlignCenter().Text(part.TotalQty.ToString()).Bold();
                                }
                            });
                        });

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("Страница ").FontSize(8);
                            x.CurrentPageNumber().FontSize(8);
                            x.Span(" из ").FontSize(8);
                            x.TotalPages().FontSize(8);
                        });
                    });
                }).GeneratePdf(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Auto Pipe PDF Export Error: {ex.Message}");
            }
        }

        private static QuestPDF.Infrastructure.IContainer TableHeaderStyle(QuestPDF.Infrastructure.IContainer container) => container
            .BorderBottom(1).BorderColor(Colors.Grey.Darken2)
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(4).PaddingHorizontal(4);

        private static QuestPDF.Infrastructure.IContainer TableRowStyle(QuestPDF.Infrastructure.IContainer container) => container
            .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3).PaddingHorizontal(4);

        /// <summary>
        /// Формирует корректное имя файла для раскладки труб/профиля на основе параметров заготовки.
        /// Пример: "Профильная труба 80x40x3_ст3.pdf" или "Уголок равнополочный 50x50x3_aisi430.pdf"
        /// </summary>
        public string GetPipeNestingFileName(bool isSaw = false)
        {
            ICut? cut = isSaw ? (owner as SawControl) : (owner as PipeControl);
            if (cut == null) return $"Раскладка_Профиль.pdf";

            var (metal, thickness, metalName) = GetMetalAndThickness(owner);
            string pipeType = "Профиль";
            string section = "";

            if (cut is PipeControl pc && pc.work.type != null)
            {
                pipeType = pc.work.type.TypeDetailDrop?.Text ?? "Труба";
                if (pipeType.Contains("Круг", StringComparison.OrdinalIgnoreCase))
                    section = $"{pc.work.type.A:0}x{pc.work.type.S:0}";
                else
                    section = $"{pc.work.type.A:0}x{pc.work.type.B:0}x{pc.work.type.S:0}";
            }
            else if (cut is SawControl sc)
            {
                pipeType = sc.work.type.TypeDetailDrop?.Text?.Trim() ?? "Профиль";

                if (pipeType.Contains("Круг", StringComparison.OrdinalIgnoreCase))
                    section = $"{sc.work.type.A:0}x{sc.work.type.S:0}";
                else
                    section = $"{sc.work.type.A:0}x{sc.work.type.B:0}x{sc.work.type.S:0}";
            }

            // 🔥 Безопасная очистка: удаляем только строго недопустимые символы (\, /, :, *, ?, ", <, >, |)
            // Пробелы оставляем для красоты, как в примере "Уголок равнополочный 50x50x3"
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string cleanType = string.Concat(pipeType.Split(invalidChars)).Trim();
            string cleanMetal = string.Concat(metalName!.Split(invalidChars)).Trim().Replace(" ", "_"); // В металле пробелы лучше заменить на _

            // Если это лентопил, можно добавить префикс, чтобы файлы не путались, 
            // но если хотите строго как в примере, уберите "Лентопил_"
            string prefix = isSaw ? "Лентопил " : "";

            return $"{prefix}{cleanType} {section}_{cleanMetal}.pdf";
        }

        private class PartTableData
        {
            public string Name { get; set; } = string.Empty;
            public string Dimensions { get; set; } = string.Empty;
            public int QtyOnSheet { get; set; }
            public int TotalQty { get; set; }
            public double WeightKg { get; set; }
        }
        #endregion
    }
}