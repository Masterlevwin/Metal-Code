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
        /// Принудительно перерисовывает все раскладки, добавляя элементы управления для каждого листа.
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

                    // Панель управления: NumericUpDown + Текст + Удалить
                    var controlPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 5, 0, 0)
                    };

                    // 1. NumericUpDown для количества
                    var qtyControl = new HandyControl.Controls.NumericUpDown
                    {
                        Value = item.sheets,
                        Minimum = 1,
                        Maximum = 9999,
                        Width = 60,
                        Height = 28,
                        Margin = new Thickness(0, 0, 10, 0),
                        ToolTip = "Количество одинаковых листов",
                        Tag = item.sheets // Сохраняем старое значение для вычисления delta
                    };

                    // 2. Текстовый блок с размерами (будем обновлять его вручную)
                    var infoText = new TextBlock
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };

                    // 🔥 ЛОКАЛЬНАЯ ФУНКЦИЯ: Мгновенно обновляет текст и стиль без INPC
                    void UpdateInfoText()
                    {
                        infoText.Text = $"{item.sheets} шт ({item.sheetSize} мм)";
                        infoText.FontWeight = item.sheets > 1 ? FontWeights.Bold : FontWeights.Normal;
                        infoText.Foreground = item.sheets > 1 ? Brushes.OrangeRed : Brushes.DimGray;
                    }

                    UpdateInfoText(); // Первичная инициализация

                    // Обработчик изменения количества
                    qtyControl.ValueChanged += (s, e) =>
                    {

                        var nud = s as HandyControl.Controls.NumericUpDown;
                        if (nud is null) return;

                        int newQty = Convert.ToInt32(nud.Value);
                        int oldQty = Convert.ToInt32(nud.Tag);

                        if (newQty != oldQty && item.NestingSheet != null)
                        {
                            int delta = newQty - oldQty;

                            // 1. Обновляем модель листа
                            item.sheets = newQty;

                            // 2. Корректируем количество деталей с учетом множителя
                            foreach (var placement in item.NestingSheet.Parts)
                            {
                                placement.Part.Count += delta;
                                placement.Part.NotifyTotalChanged();
                            }

                            // 3. Пересчитываем итоги и обновляем UI
                            RecalculateTotals(cut);

                            // 4. МГНОВЕННО обновляем визуальный текст
                            UpdateInfoText();

                            // Обновляем Tag для следующих изменений
                            nud.Tag = newQty;

                            string action = delta > 0 ? "добавлен" : "удален";
                            MainWindow.M.StatusBegin($"Количество листов {item.sheetSize} изменено. {Math.Abs(delta)} лист {action}.", MainWindow.StatusMessageType.Success);
                        }
                    };
                    // 3. Кнопка удаления
                    var deleteBtn = new Button
                    {
                        Content = "🗑",
                        ToolTip = "Удалить этот лист (детали вернутся в общий список)",
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
            RecalculateTotals(cut);
            RefreshNestingPreview();

            MainWindow.M.StatusBegin("Лист удалён, детали возвращены в общий список", MainWindow.StatusMessageType.Success);
        }

        /// <summary>
        /// Открывает диалог выбора DXF-файлов, парсит количество из имени и добавляет их как новые детали.
        /// </summary>
        private void AddPartsFromDxf_Click(object sender, RoutedEventArgs e)
        {
            if (owner is not CutControl cut || sender is not ToggleButton btn) return;

            btn.IsChecked = true;

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

            btn.IsChecked = false;
        }

        /// <summary>
        /// Пересчитывает раскладку (автонестинг) только для деталей, имеющих геометрию.
        /// Игнорирует детали, загруженные как изображения (imageBytes).
        /// </summary>
        private async void AutoNest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn) return;

            // 🔥 ВКЛЮЧАЕМ оранжевую подсветку
            btn.IsChecked = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                if (owner is not CutControl cut || cut.Items == null || cut.PartDetails == null)
                {
                    MainWindow.M.StatusBegin("Нет данных для пересчета раскладки", MainWindow.StatusMessageType.Warning);
                    return;
                }

                // 1. СБОР ДАННЫХ В UI-ПОТОКЕ (обращение к UI-элементам безопасно только здесь)
                var partsToNest = cut.PartDetails
                    .Where(p => p.DisplayGeometry != null && p.Count > 0)
                    .ToList();

                if (!partsToNest.Any())
                {
                    MessageBox.Show("В расчете нет деталей с геометрией для автоматической раскладки.\n(Детали, загруженные как изображения, игнорируются)",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string metalName = cut.work.type.MetalDrop.Text;
                float thickness = cut.work.type.S;

                if (string.IsNullOrEmpty(metalName) || thickness <= 0)
                {
                    MessageBox.Show("Не указан материал или толщина листа в настройках.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MainWindow.M.StatusBegin("Выполняется автоматическая раскладка...", MainWindow.StatusMessageType.Info);

                // 🔥 2. ТЯЖЕЛЫЕ ВЫЧИСЛЕНИЯ В ФОНОВОМ ПОТОКЕ
                // UI-поток освобождается и отрисовывает оранжевую кнопку
                var result = await System.Threading.Tasks.Task.Run(() =>
                {
                    // SkylineNestingHelper работает с замороженными Geometry, это безопасно
                    return SkylineNestingHelper.CreateNestingSkylineAutoSheet(partsToNest, metalName, thickness, 0);
                });

                // 🔥 3. МЫ СНОВА В UI-ПОТОКЕ (после await)
                if (result == null || !result.Any())
                {
                    MessageBox.Show("Алгоритму не удалось разместить детали на доступных листах.", "Ошибка раскладки", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 4. ОЧИСТКА СТАРЫХ РАСКЛАДОК
                var oldNestingItems = cut.Items.OfType<LaserItem>().Where(i => i.NestingSheet != null).ToList();
                foreach (var item in oldNestingItems)
                {
                    cut.Items.Remove(item);
                }

                // 5. ГРУППИРОВКА И СОЗДАНИЕ НОВЫХ LaserItem
                var groupedSheets = GroupIdenticalSheets(result);

                foreach (var group in groupedSheets)
                {
                    var sheet = group.Key;
                    int sheetCount = group.Value;

                    if (sheet.Parts.Count == 0) continue;

                    double sheetArea = sheet.OptimizedWidth * sheet.OptimizedHeight;
                    var metal = MainWindow.M.Metals?.FirstOrDefault(m => m.Name == metalName);
                    double density = metal?.Density ?? 0;
                    double sheetMass = sheetArea * thickness * density / 1_000_000;

                    double sheetWay = sheet.Parts.Sum(p => p.Part.Way);
                    int sheetPinholes = sheet.Parts.Sum(p => int.TryParse(p.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var val) ? val : 0);

                    var newItem = new LaserItem
                    {
                        sheets = sheetCount,
                        sheetSize = $"{sheet.OptimizedWidth:0}x{sheet.OptimizedHeight:0}",
                        way = (float)sheetWay,
                        pinholes = sheetPinholes,
                        mass = (float)sheetMass,
                        metal = metalName,
                        destiny = thickness.ToString(),
                        NestingSheet = sheet
                    };

                    cut.Items.Add(newItem);
                }

                // 6. ОБНОВЛЕНИЕ ИТОГОВ И UI
                RecalculateTotals(cut);
                RefreshNestingPreview();

                MainWindow.M.StatusBegin($"Авто-раскладка завершена. Создано листов: {result.Count}", MainWindow.StatusMessageType.Success);
            }
            catch (Exception ex)
            {
                MainWindow.M.StatusBegin($"Ошибка при авто-раскладке: {ex.Message}", MainWindow.StatusMessageType.Error);
                System.Diagnostics.Trace.WriteLine($"AutoNest Error: {ex}");
            }
            finally
            {
                // 🔥 ГАРАНТИРОВАННО ВОЗВРАЩАЕМ кнопку в исходное состояние
                btn.IsChecked = false;
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Пересчет общих итогов (WayTotal, MassTotal, Way, Pinholes, Mass) после изменения раскладок.
        /// </summary>
        private void RecalculateTotals(ICut cut)
        {
            if (cut is CutControl _cut && _cut.PartDetails is not null)
            {
                _cut.WayTotal = _cut.PartDetails.Sum(p => p.Way * p.Count);
                _cut.MassTotal = _cut.PartDetails.Sum(p => p.Mass * p.Count);

                _cut.SumProperties(_cut.Items ?? new());
                _cut.work.type.CreateSort();
            }
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
            if (owner is not CutControl cut || cut.Items == null || sender is not ToggleButton btn)
            {
                MainWindow.M.StatusBegin("Нет данных для экспорта", MainWindow.StatusMessageType.Warning);
                return;
            }

            btn.IsChecked = true;

            var sheetsToExport = cut.Items
                .Where(i => i.NestingSheet != null && i.NestingSheet.Parts.Count > 0)
                .ToList();

            if (!sheetsToExport.Any())
            {
                MessageBox.Show("Нет листов раскладки для экспорта.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                btn.IsChecked = false;
                return;
            }

            // Получаем материал и толщину из настроек типа работ
            string metalName = cut.work.type.MetalDrop.Text;
            float thickness = cut.work.type.S;

            //"Описание материала"
            string description = "";
            if ((metalName.Contains("ст") && thickness >= 3) || (metalName.Contains("хк") && thickness < 3)) description = $"s{thickness}";
            else if (metalName.Contains("амг2")) description = $"al{thickness}";
            else if (metalName.Contains("амг") || metalName.Contains("д16")) description = $"al{thickness} {metalName}";
            else if (metalName.Contains("латунь")) description = $"br{thickness}";
            else if (metalName.Contains("медь")) description = $"cu{thickness}";
            else description = $"s{thickness} {metalName}";

            //добавляем тэг рифленки при необходимости
            if (cut.IsGrooved) description += " рифл";

            //добавляем тэг давальческого материала
            if (cut.work.type.CheckMetal.IsChecked is false) description += " Давальч";

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = $"{description}.pdf",
                InitialDirectory = MainWindow.M.lastInputDirectory
            };

            if (saveDialog.ShowDialog() != true)
            {
                btn.IsChecked = false;
                return;
            }

            MainWindow.M.StatusBegin("Генерация PDF...", MainWindow.StatusMessageType.Info);

            // 🔥 Вызываем вынесенный метод генерации
            GenerateNestingPdf(saveDialog.FileName);

            MainWindow.M.StatusBegin(
                $"PDF успешно сохранен: {Path.GetFileName(saveDialog.FileName)}",
                MainWindow.StatusMessageType.Success);
            btn.IsChecked = false;
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

        private static QuestPDF.Infrastructure.IContainer TableHeaderStyle(QuestPDF.Infrastructure.IContainer container) => container
            .BorderBottom(1).BorderColor(Colors.Grey.Darken2)
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(4).PaddingHorizontal(4);

        private static QuestPDF.Infrastructure.IContainer TableRowStyle(QuestPDF.Infrastructure.IContainer container) => container
            .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3).PaddingHorizontal(4);

        private class PartTableData
        {
            public string Name { get; set; } = string.Empty;
            public string Dimensions { get; set; } = string.Empty;
            public int QtyOnSheet { get; set; }
            public int TotalQty { get; set; }
            public double WeightKg { get; set; }
        }
        #endregion

        //-------------Нестинг-------------//
        #region
        // Добавить стандартную деталь
        private void Add_StandartPart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn) return;

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
                            // 🔥 ПЕРЕДАЕМ ОТСТУП В МЕТОД
                            AddBatchToCutControl(cut, batchedParts, metal, window.UseAutoNesting, window.CustomSpacing);
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
                btn.IsChecked = false;
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
                "Круг" => owner is SawControl saw && saw.Tube == TubeType.circle ? PartType.RoundTube : PartType.Round,
                "Треугольник" => PartType.Triangle,
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
            if (partType == PartType.Round || partType == PartType.Rectangle || partType == PartType.Triangle)
                part.Width = part.Height = 100;
            else if (partType == PartType.RoundTube)
            {
                part.Width = part.Height = type != null ? type.A : 100;
                part.Length = 1000; // базовая длина 1 метр
            }
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
                double area = CalculateCrossSectionArea(part.PartType, part.Width, part.Height, 1);
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
        public void AddBatchToCutControl(CutControl cut, List<Part> parts, Metal metal, bool isAutoSheet = true, double customSpacing = 0)
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
            }

            // Обновляем итоговые значения
            RecalculateTotals(cut);
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
        public static double CalculateCrossSectionArea(PartType partType,
            double width, double height, double thickness)
        {
            // Валидация: толщина не должна превышать 1/3 от меньшего линейного размера
            double minDim = Math.Min(width, height > 0 ? height : width);
            double t = thickness >= minDim / 3 ? Math.Max(0.1, minDim / 3 - 0.1) : thickness;

            return partType switch
            {
                // === ЛИСТОВЫЕ И ПРОСТЫЕ СЕЧЕНИЯ ===
                PartType.Round => Math.PI * Math.Pow(width / 2, 2),
                PartType.Rectangle or PartType.SquareBar or PartType.Custom => width * height,
                PartType.Triangle => width * height / 2.0,

                // === ТРУБЫ ===
                PartType.RoundTube => Math.PI * (
                    Math.Pow(width / 2, 2) -
                    Math.Pow(Math.Max(0, width / 2 - t), 2)),

                PartType.RectangularTube =>
                    width * height -
                    Math.Max(0, width - 2 * t) * Math.Max(0, height - 2 * t),

                // === ПРОКАТ ===
                PartType.Angle => t * (width + height - t),
                PartType.Channel => t * (width + 2 * height - 2 * t),
                PartType.IBeam => t * (2 * width + height - 2 * t),

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
    }
}