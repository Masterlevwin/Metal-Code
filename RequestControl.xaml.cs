using ACadSharp;
using ACadSharp.IO;
using ExcelDataReader;
using Metal_Code.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using netDxf;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для RequestControl.xaml
    /// </summary>
    public partial class RequestControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private bool isAvailable = false;
        public bool IsAvailable     //доступен ли предварительный расчет
        {
            get => isAvailable;
            set
            {
                if (value != isAvailable)
                {
                    isAvailable = value;
                    OnPropertyChanged(nameof(IsAvailable));
                }
            }
        }

        private TechItem? targetTechItem;
        public TechItem? TargetTechItem
        {
            get => targetTechItem;
            set
            {
                targetTechItem = value;
                OnPropertyChanged(nameof(TargetTechItem));
            }
        }

        private readonly RequestContext db = new(MainWindow.M.connections[12]);
        public List<string> Paths { get; set; } = new();
        public List<string> Works { get; set; } = new()
        {
            "гиб - гибка",
            "вальц - вальцовка",
            "зен - зенковка",
            "рез - резьба",
            "зак - заклепки",
            "свар - сварка",
            "окр - окраска",
            "оц - оцинковка",
            "грав - гравировка",
            "фрез - фрезеровка",
            "аква - аквабластинг",
            "лен - лентопил",
            "свер - сверловка"
        };

        public RequestTemplate CurrentTemplate { get; set; } = new();
        public ObservableCollection<RequestTemplate> Templates { get; set; } = new();

        // Коллекция деталей
        private ObservableCollection<TechItem> _techItems = new();
        public ObservableCollection<TechItem> TechItems
        {
            get => _techItems;
            set
            {
                if (_techItems != value)
                {
                    // Отписываемся от старых элементов
                    if (_techItems != null)
                    {
                        foreach (var item in _techItems)
                            item.PropertyChanged -= OnTechItemPropertyChanged;
                        _techItems.CollectionChanged -= OnTechItemsCollectionChanged;
                    }

                    _techItems = value;

                    // Подписываемся на новые
                    if (_techItems != null)
                    {
                        foreach (var item in _techItems)
                            item.PropertyChanged += OnTechItemPropertyChanged;
                        _techItems.CollectionChanged += OnTechItemsCollectionChanged;
                    }

                    UpdateIsAvailable(); // Обновляем сразу
                    OnPropertyChanged(nameof(TechItems));
                }
            }
        }

        private void OnTechItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (TechItem item in e.OldItems)
                    item.PropertyChanged -= OnTechItemPropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (TechItem item in e.NewItems)
                    item.PropertyChanged += OnTechItemPropertyChanged;
            }

            UpdateIsAvailable();
        }

        private void OnTechItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Проверяем только нужные свойства для оптимизации
            if (e.PropertyName is nameof(TechItem.Sizes) or nameof(TechItem.Destiny) or nameof(TechItem.Count))
            {
                UpdateIsAvailable();
            }
        }

        private void UpdateIsAvailable()
        {
            bool isValid = TechItems.Count > 0 && TechItems.All(IsTechItemValid);
            IsAvailable = isValid;
        }

        private bool IsTechItemValid(TechItem item)
        {
            return !string.IsNullOrWhiteSpace(item?.Sizes)
                && !string.IsNullOrWhiteSpace(item?.Destiny)
                && !string.IsNullOrWhiteSpace(item?.Count);
        }


        // Конструктор
        public RequestControl(List<string> paths)
        {
            InitializeComponent();
            DataContext = this;
            TechItems = new ObservableCollection<TechItem>();
            Update_Paths(paths);
        }


        //-----настройка контрола при загрузке-----//
        private void RequestControl_Loaded(object sender, RoutedEventArgs e)
        {
            db.Templates.Load();
            Templates = db.Templates.Local.ToObservableCollection();
            TemplatesList.ItemsSource = Templates;

            MetalsDrop.ItemsSource = MainWindow.M.Metals.Select(m => m.Name);
            DestinyDrop.ItemsSource = MainWindow.M.Destinies;
        }
        private void Update_Paths(List<string> paths)
        {
            if (paths.Count == 0) return;

            Paths = paths;
            PathsList.ItemsSource = Paths.Select(x => Path.GetFileNameWithoutExtension(x));

            //если выбранный файл и есть заявка, загружаем ее данные
            if (Paths.Count == 1 && (Paths[0].Contains("Заявка") || Paths[0].Contains("Ведомость"))) Load_Request(Paths[0]);
        }


        //-----загрузка данных заявки для редактирования-----//
        public void Load_Request(string path)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            try
            {
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTable table = result.Tables[0];

                // количество комплектов
                if ($"{table.Rows[^1].ItemArray[5]}" == "Кол-во комплектов"
                    && $"{table.Rows[^1].ItemArray[6]}" != null
                    && int.TryParse($"{table.Rows[^1].ItemArray[6]}", out int value) && value > 0)
                    CountText.Text = $"{value}";

                // === ОПРЕДЕЛЕНИЕ СТАРТОВОЙ СТРОКИ ===
                // Проверяем строку 1: если в столбце 0 есть число — данные начинаются с неё
                int startRow = 2; // по умолчанию
                if (table.Rows.Count > 1)
                {
                    var firstCell = table.Rows[1].ItemArray[0]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(firstCell) && int.TryParse(firstCell, out _))
                        startRow = 1;
                }

                // перебираем строки таблицы и заполняем список объектами TechItem
                for (int i = startRow; i < table.Rows.Count; i++)
                {
                    if ($"{table.Rows[i].ItemArray[1]}" is null || $"{table.Rows[i].ItemArray[1]}" == "") continue;

                    TechItem techItem = new(
                        $"{table.Rows[i].ItemArray[1]}",        // номер чертежа
                        $"{table.Rows[i].ItemArray[2]}",        // профиль
                        $"{table.Rows[i].ItemArray[3]}",        // размеры
                        $"{table.Rows[i].ItemArray[4]}",        // материал
                        $"{table.Rows[i].ItemArray[5]}",        // толщина
                        $"{table.Rows[i].ItemArray[6]}",        // количество
                        $"{table.Rows[i].ItemArray[7]}",        // маршрут
                        $"{table.Rows[i].ItemArray[8]}",        // давальческий материал
                        $"{table.Rows[i].ItemArray[9]}",        // оригинальное наименование от заказчика
                        $"{table.Rows[i].ItemArray[10]}",       // путь к файлу модели
                        $"{table.Rows[i].ItemArray[11]}",       // сгенерирован ли номер чертежа
                        $"{table.Rows[i].ItemArray[12]}");      // гравировка

                    if (!string.IsNullOrWhiteSpace(techItem.Profile)) ProfileParser.ParseToTechItem(techItem);
                    TechItems.Add(techItem);
                }

                if (result.Tables.Count > 1)
                {
                    MainWindow.M.Order.Text = $"{result.Tables[1].Rows[0].ItemArray[1]}";

                    if ($"{result.Tables[1].Rows[2].ItemArray[1]}" != "")
                        foreach (var man in MainWindow.M.ManagerDrop.Items)
                            if (man is Manager _man && _man.Name == $"{result.Tables[1].Rows[2].ItemArray[1]}")
                            {
                                MainWindow.M.ManagerDrop.SelectedItem = _man;
                                break;
                            }

                    if ($"{result.Tables[1].Rows[1].ItemArray[1]}" != "")
                        foreach (var customer in MainWindow.M.CustomerDrop.Items)
                            if (customer is Customer _customer && _customer.Name == $"{result.Tables[1].Rows[1].ItemArray[1]}")
                            {
                                MainWindow.M.CustomerDrop.SelectedItem = _customer;
                                break;
                            }
                }
                stream.Close();

                if (TechItems.Count > 0)
                    MainWindow.M.StatusBegin("Заявка загружена", MainWindow.StatusMessageType.Success);
            }
            catch (Exception ex) { MessageBox.Show($"{ex.Message}\nФорма этой заявки не поддерживается функцией загрузки."); }
        }

        //-----загрузка новых файлов для обработки-----//
        private void Load_Models(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "All files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames.Length > 0)
            {
                Update_Paths(openFileDialog.FileNames.ToList());
            }
            else MainWindow.M.StatusBegin($"Не выбрано ни одного файла", MainWindow.StatusMessageType.Warning);
        }

        //-----выход из режима заявки-----//
        private void Close_RequestControl(object sender, RoutedEventArgs e)
        {
            MessageBoxResult response = MessageBox.Show("Выйти из режима заявки?", "Закрытие заявки",
                               MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

            if (response == MessageBoxResult.Yes) MainWindow.M.CloseRequestControl();
            else return;
        }


        //-----шаблон распознавания толщин и количества-----//
        private void Save_Template(object sender, RoutedEventArgs e)
        {
            string name = "";
            if (TemplateNameStack.Children.Count > 0)
                foreach (TextBlock child in TemplateNameStack.Children)
                    if (child.Visibility == Visibility.Visible) name += $"{child.Text} ";

            RequestTemplate? template = Templates.FirstOrDefault(x => x.Name == name);

            if (template != null)
            {
                MessageBox.Show($"Шаблон типа {name} уже добавлен");
                return;
            }
            else
            {
                template = new()
                {
                    Name = name,
                    DestinyPattern = CurrentTemplate.DestinyPattern,
                    CountPattern = CurrentTemplate.CountPattern,
                    PosDestiny = CurrentTemplate.PosDestiny,
                    PosCount = CurrentTemplate.PosCount
                };
            }

            db.Templates.Add(template);
            db.SaveChanges();
        }
        private void Remove_Template(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete || TemplatesList.SelectedItem is not RequestTemplate template) return;

            RequestTemplate? _template = Templates.FirstOrDefault(x => x.Id == template.Id);

            if (template != null)
            {
                db.Templates.Remove(template);
                db.SaveChanges();
            }
        }


        //-----анализ файлов по выбранному шаблону-----//
        private void Analyze_Paths(object sender, RoutedEventArgs e) { Analyze_Paths(); }
        private void Analyze_Paths()
        {
            if (Paths.Count == 0) return;

            RequestTemplate template = TemplatesList.SelectedItem is not RequestTemplate ?
                new RequestTemplate() : (RequestTemplate)TemplatesList.SelectedItem;

            foreach (string path in Paths)
            {
                TechItem techItem = new()
                {
                    NumberName = Path.GetFileNameWithoutExtension(path),
                    OriginalName = Path.GetFileNameWithoutExtension(path),
                    PathToModel = path
                };

                //если заявка уже содержит строку этого файла, пропускаем его анализ
                var _techItem = TechItems.FirstOrDefault(x => x.PathToModel == techItem.PathToModel);
                if (_techItem != null) continue;

                //определяем материал
                string aisiPattern = @"(aisi\s*(\d+)\s*зер)|(aisi\s*(\d+)\s*шлиф)|(aisi\s*(\d+))";
                string d16atPattern = @"д\s*16\s*(?:а\s*т|т)";
                string d16amPattern = @"д\s*16\s*(?:а\s*м|м)";
                string amgPattern = @"амг\s*(\d+)";

                foreach (Metal metal in MainWindow.M.Metals)
                    if (metal.Name != null && metal.Name.Contains("aisi"))
                    {
                        Match match = Regex.Match(path, aisiPattern, RegexOptions.IgnoreCase);
                        if (match.Success && metal.Name.Contains(match.Value.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                        {
                            techItem.Material = metal.Name;
                            techItem.NumberName = Regex.Replace(techItem.NumberName, aisiPattern, "", RegexOptions.IgnoreCase);
                            break;
                        }
                    }
                    else if (metal.Name != null && metal.Name.Contains("д16АТ"))
                    {
                        Match match = Regex.Match(path, d16atPattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            techItem.Material = metal.Name;
                            techItem.NumberName = Regex.Replace(techItem.NumberName, d16atPattern, "", RegexOptions.IgnoreCase);
                            break;
                        }
                    }
                    else if (metal.Name != null && metal.Name.Contains("д16АМ"))
                    {
                        Match match = Regex.Match(path, d16amPattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            techItem.Material = metal.Name;
                            techItem.NumberName = Regex.Replace(techItem.NumberName, d16amPattern, "", RegexOptions.IgnoreCase);
                            break;
                        }
                    }
                    else if (metal.Name != null && metal.Name.Contains("амг"))
                    {
                        Match match = Regex.Match(path, amgPattern, RegexOptions.IgnoreCase);
                        if (match.Success && metal.Name.Contains(match.Value.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                        {
                            techItem.Material = metal.Name;
                            techItem.NumberName = Regex.Replace(techItem.NumberName, amgPattern, "", RegexOptions.IgnoreCase);
                            break;
                        }
                    }
                    else if (metal.Name != null && path.Contains(metal.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        techItem.Material = metal.Name;
                        techItem.NumberName = Regex.Replace(techItem.NumberName, metal.Name, "", RegexOptions.IgnoreCase);
                        break;
                    }

                //определяем толщину
                string destinyPattern;
                if (template.PosDestiny)
                {
                    destinyPattern = $@"{Regex.Escape(template.DestinyPattern)}\s*(?<!\d)(\d{{1,2}}(?:[,.]\d+)?)(?!\d)";
                }
                else
                {
                    destinyPattern = $@"(?<!\d)(\d{{1,2}}(?:[,.]\d+)?)(?!\d)\s*{Regex.Escape(template.DestinyPattern)}";
                }

                Match matchDestiny = Regex.Match(path, destinyPattern, RegexOptions.IgnoreCase);
                if (matchDestiny.Success)
                {
                    techItem.Destiny = matchDestiny.Groups[1].Value.Replace(",", ".");
                    techItem.NumberName = Regex.Replace(techItem.NumberName, destinyPattern, "", RegexOptions.IgnoreCase);
                }

                //определяем количество
                string countPattern;
                if (template.PosCount) countPattern = $@"{Regex.Escape(template.CountPattern)}\s*(\d+)";
                else countPattern = $@"(\d+)\s*{Regex.Escape(template.CountPattern)}";

                Match matchCount = Regex.Match(path, countPattern, RegexOptions.IgnoreCase);
                if (matchCount.Success)
                {
                    techItem.Count = matchCount.Groups[1].Value;
                    techItem.NumberName = Regex.Replace(techItem.NumberName, countPattern, "", RegexOptions.IgnoreCase);
                }

                //очищаем наименование
                techItem.NumberName = Regex.Replace(techItem.NumberName, @"[^\p{L}\p{Nd}]+$", "").Trim();

                //определяем размеры
                if (string.Equals(Path.GetExtension(path), ".dxf", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var doc = DxfDocument.Load(path);
                        var displayGeometry = DxfToWpfConverter.ConvertToPathGeometryAsIs(doc);

                        if (displayGeometry == null || displayGeometry.IsEmpty())
                            throw new InvalidOperationException("DXF не содержит распознаваемых контуров.");

                        var calculationGeometry = DxfToWpfConverter.ConvertToPathGeometryWithClosedContours(doc);

                        techItem.DisplayGeometry = displayGeometry;
                        if (techItem.DisplayGeometry.CanFreeze)
                            techItem.DisplayGeometry.Freeze();
                        techItem.CalculationGeometry = calculationGeometry;

                        TechItemCalculator.UpdateFromGeometry(techItem);
                    }
                    catch
                    {
                        MessageBox.Show($"Не удалось прочитать dxf ({path}).\n" +
                        $"Пересохраните файл в CAD-программе и попробуйте снова.");
                    }
                }

                TechItems.Add(techItem);
            }

            if (TechItems.Count > 0)
                MainWindow.M.StatusBegin("Файлы успешно проанализированы", MainWindow.StatusMessageType.Success);
        }


        //-----генерация имён строк заявки-----//
        private void Rename_Details(object sender, RoutedEventArgs e)
        {
            if (TechItems.Count == 0) return;

            foreach (TechItem techItem in TechItems)
            {
                int metalIndex = 0;
                //генерируем новое название строки из индекса материала, толщины и количества
                foreach (Metal metal in MainWindow.M.Metals)
                    if (metal.Name != null && metal.Name == techItem.Material)
                    {
                        metalIndex = metal.Id;
                        break;
                    }
                techItem.NumberName = $"{metalIndex}.{techItem.Destiny}.{techItem.Count}";
                techItem.IsGenerated = true;
            }

            //при совпадении сгенерированных имён добавляем порядковый индекс к имени
            var collect = TechItems.GroupBy(x => x.NumberName);
            foreach (var item in collect)
                if (item.Count() > 1)
                    foreach (var _item in item)
                        _item.NumberName += $".{item.ToList().IndexOf(_item)}";

            MainWindow.M.StatusBegin("Наименования деталей сгенерированы.", MainWindow.StatusMessageType.Success);
        }
        private void ShowPopup_Gen(object sender, MouseEventArgs e)
        {
            Popup.IsOpen = true;

            Details.Text = "Генерирует новые имена деталей\n" +
                "в виде децимального номера.\n" +
                "После загрузки раскладок из Ажанкам эти имена\n" +
                "будут заменены обратно на исходные имена от заказчика.";
        }


        //-----получение геометрии выбранной детали-----//
        private void Set_TargetTechItem(object sender, SelectedCellsChangedEventArgs e)
        {
            if (RequestGrid.SelectedCells.Count > 0)
                if (RequestGrid.SelectedCells[0].Item is TechItem techItem && TechItems.Contains(techItem))
                    TargetTechItem = techItem;
        }

        //-----метод копирования данных в выделенные ячейки после отпускания мыши-----//
        private void CopyValue_MouseUp(object sender, MouseButtonEventArgs e) { CopyValue(); }
        private void CopyValue()
        {
            if (RequestGrid.SelectedCells.Count < 2) return;

            // Берем первую ячейку как источник
            var sourceCell = RequestGrid.SelectedCells[0];
            var sourceItem = sourceCell.Item;
            var sourceColumn = sourceCell.Column;

            // Получаем имя свойства по заголовку столбца (или можно хранить в Tag/привязке)
            string? propertyName = GetPropertyNameFromColumn(sourceColumn);
            if (string.IsNullOrEmpty(propertyName) || propertyName == "OriginalName")
            {
                MainWindow.M.StatusBegin("Не удалось определить свойство для копирования, или такое копирование запрещено.", MainWindow.StatusMessageType.Error);
                return;
            }

            var propertyInfo = sourceItem.GetType().GetProperty(propertyName);
            if (propertyInfo == null) return;

            var valueToCopy = propertyInfo.GetValue(sourceItem);

            // Проверяем, все ли ячейки принадлежат одной строке
            bool sameRow = true;
            var firstItem = sourceItem;

            foreach (var cell in RequestGrid.SelectedCells.Skip(1))
            {
                if (!ReferenceEquals(cell.Item, firstItem))
                {
                    sameRow = false;
                    break;
                }
            }

            if (sameRow)
            {
                MainWindow.M.StatusBegin("Копирование по горизонтали запрещено.", MainWindow.StatusMessageType.Error);
                return;
            }

            // Проходим по всем выделенным ячейкам, кроме первой
            foreach (var cell in RequestGrid.SelectedCells.Skip(1))
            {
                var item = cell.Item;
                var column = cell.Column;

                string? targetPropertyName = GetPropertyNameFromColumn(column);
                if (string.IsNullOrEmpty(targetPropertyName)) continue;

                var targetProperty = item.GetType().GetProperty(targetPropertyName);
                if (targetProperty == null || !targetProperty.CanWrite) continue;

                if (column != sourceColumn) continue; // Запрет копирования в другие столбцы

                // Устанавливаем новое значение
                targetProperty.SetValue(item, valueToCopy);
            }
        }

        //-----вспомогательный метод для получения имени свойства из колонки-----//
        private static string? GetPropertyNameFromColumn(DataGridColumn column)
        {
            if (column is DataGridTextColumn textColumn &&
                textColumn.Binding is Binding binding)
            {
                return binding.Path.Path;
            }

            // Добавь поддержку других типов колонок при необходимости
            return null;
        }


        //-----метод укорачивания наименований-----//
        private void Delete_WithoutNames(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(DeleteText.Text) || TechItems.Count == 0) return;

            foreach (TechItem item in TechItems)
                if (item.NumberName.Contains(DeleteText.Text, StringComparison.OrdinalIgnoreCase))
                    item.NumberName = item.NumberName.Replace(DeleteText.Text, "");
        }
        private void ShowPopup_Del(object sender, MouseEventArgs e)
        {
            Popup.IsOpen = true;

            Details.Text = "Функция укорачивания наименований.\n" +
                "Введите символ или часть текста, и ,если программа\n" +
                "найдет совпадение, то удалит это из каждого наименования.";
        }

        //-----добавление пустой строки в список деталей-----//
        private void Add_TechItem(object sender, RoutedEventArgs e) { TechItems.Add(new()); }

        //-----удаление строки из списка деталей-----//
        private void Remove_TechItem(object sender, RoutedEventArgs e)
        {
            if (RequestGrid.SelectedCells.Count > 0)
                if (RequestGrid.SelectedCells[0].Item is TechItem techItem && TechItems.Contains(techItem))
                    TechItems.Remove(techItem);
        }

        //-----очистка всего списка деталей-----//
        private void Clear_TechItems(object sender, RoutedEventArgs e)
        {
            TechItems.Clear();
            TargetTechItem = null;
            IsAvailable = false;
        }
        private void ShowPopup_DataGrid(object sender, MouseEventArgs e)
        {
            Popup.IsOpen = true;

            Details.Text = $"Кнопка \"Очистить\" удаляет ВСЕ строки из таблицы.\n" +
                "Чтобы удалить одну строку, нажмите по строке правой кнопкой мыши" +
                "и левой по команде \"Удалить строку\".";
        }


        //-----создание заявки и подготовка папок одновременно-----//
        private void Launch_Tech(object sender, RoutedEventArgs e) { if (Create_Request()) Create_Tech(); }

        //-----создание заявки в формате Excel-----//
        private void Create_Request(object sender, RoutedEventArgs e) { Create_Request(); }
        private bool Create_Request()
        {
            if (TechItems.Count == 0)
            {
                MainWindow.M.StatusBegin("Чтобы создать заявку, запустите анализ файлов.", MainWindow.StatusMessageType.Error);
                return false;
            }

            if (Directory.Exists(Path.GetDirectoryName(Paths[0])))
            {
                var baseDir = Directory.GetParent(Paths[0]);
                if (baseDir != null && !baseDir.Name.Contains("ТЗ", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBoxResult response = MessageBox.Show(
                        "Папка с моделями, в которой будет создана заявка, должна называться \"ТЗ\"!",
                        "Создание заявки", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (response == MessageBoxResult.Yes) return true;
                    else return false;
                }
            }

            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet requestsheet = workbook.Workbook.Worksheets.Add($"Заявка");

            //оформляем статичные ячейки по умолчанию
            requestsheet.Cells[1, 1].Value = "Расшифровка работ: гиб - гибка, вальц - вальцовка, зен - зенковка," +
                "рез - резьба, свар - сварка, окр - окраска,\nоц - оцинковка, грав - гравировка, фрез - фрезеровка, " +
                "аква - аквабластинг, лен - лентопил, свер - сверловка";
            requestsheet.Cells[1, 1, 1, 8].Merge = true;
            requestsheet.Cells[1, 1, 1, 8].Style.WrapText = true;
            requestsheet.Cells[1, 1, 1, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            requestsheet.Cells[1, 1, 1, 8].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            requestsheet.Row(1).Height = 40;
            requestsheet.Cells[TechItems.Count + 3, 6].Value = "Кол-во комплектов";
            requestsheet.Cells[TechItems.Count + 3, 7].Value = CountText.Text;
            requestsheet.Cells[TechItems.Count + 3, 7].Style.Font.Color.SetColor(System.Drawing.Color.Red);
            requestsheet.Cells[TechItems.Count + 3, 6, TechItems.Count + 3, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            requestsheet.Cells[TechItems.Count + 3, 6, TechItems.Count + 3, 7].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            requestsheet.Cells[TechItems.Count + 3, 6].Style.Border.Right.Style = ExcelBorderStyle.Thin;
            requestsheet.Cells[TechItems.Count + 3, 6, TechItems.Count + 3, 7].Style.Border.BorderAround(ExcelBorderStyle.Medium);

            //устанавливаем заголовки таблицы
            List<string> _heads = new() { "№", "№ чертежа", "Профиль", "Размеры", "Металл", "Толщина", "Кол-во деталей", "Маршрут", "Давальч", "Исходник", "Путь к модели", "Сген", "Гравировка" };
            for (int head = 0; head < _heads.Count; head++) requestsheet.Cells[2, head + 1].Value = _heads[head];

            //string message = "";
            for (int i = 0; i < TechItems.Count; i++)
            {
                requestsheet.Cells[i + 3, 1].Value = i + 1;
                requestsheet.Cells[i + 3, 2].Value = TechItems[i].NumberName;
                requestsheet.Cells[i + 3, 3].Value = TechItems[i].Profile;
                requestsheet.Cells[i + 3, 4].Value = TechItems[i].Sizes;
                requestsheet.Cells[i + 3, 5].Value = TechItems[i].Material;
                requestsheet.Cells[i + 3, 6].Value = NormalizeSeparator(TechItems[i].Destiny);
                requestsheet.Cells[i + 3, 7].Value = TechItems[i].Count;
                requestsheet.Cells[i + 3, 8].Value = TechItems[i].Route;
                requestsheet.Cells[i + 3, 9].Value = TechItems[i].HasMaterial;
                requestsheet.Cells[i + 3, 10].Value = TechItems[i].OriginalName;
                requestsheet.Cells[i + 3, 11].Value = TechItems[i].PathToModel;
                requestsheet.Cells[i + 3, 12].Value = TechItems[i].IsGenerated ? "да" : "";
                requestsheet.Cells[i + 3, 13].Value = TechItems[i].TextMarking;
            }

            requestsheet.Column(10).Hidden = true;
            requestsheet.Column(11).Hidden = true;
            requestsheet.Column(12).Hidden = true;
            requestsheet.Column(13).Hidden = true;

            ExcelWorksheet ordersheet = workbook.Workbook.Worksheets.Add($"КП");
            ordersheet.Cells[1, 1].Value = "КП №";
            ordersheet.Cells[1, 2].Value = MainWindow.M.Order.Text;
            ordersheet.Cells[2, 1].Value = "для заказчика";
            ordersheet.Cells[2, 2].Value = MainWindow.M.CustomerDrop.Text;
            ordersheet.Cells[3, 1].Value = "менеджер";
            ordersheet.Cells[3, 2].Value = MainWindow.M.ManagerDrop.Text;

            ExcelRange order = ordersheet.Cells[1, 1, 3, 2];                            //получаем данные КП для оформления
            ExcelRange details = requestsheet.Cells[2, 1, TechItems.Count + 2, 13];     //получаем таблицу деталей для оформления

            //обводка границ и авторастягивание столбцов
            order.Style.HorizontalAlignment = details.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            order.Style.VerticalAlignment = details.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            order.Style.Border.Right.Style = order.Style.Border.Bottom.Style = details.Style.Border.Right.Style = details.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            order.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            details.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            requestsheet.Cells[2, 1, 2, 13].Style.WrapText = true;
            requestsheet.Cells[2, 1, 2, 13].Style.Font.Bold = true;
            requestsheet.Cells.AutoFitColumns();
            ordersheet.Cells.AutoFitColumns();

            //сохраняем книгу в файл Excel
            try { workbook.SaveAs($"{Path.GetDirectoryName(Paths[0])}\\Заявка.xlsx"); }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}\n" +
                $"Возможно файл заявки уже открыт, поэтому ее не создать!");
            }

            MainWindow.M.StatusBegin($"Создана заявка в папке {Path.GetDirectoryName(Paths[0])}", MainWindow.StatusMessageType.Success);
            return true;
        }

        public static string? NormalizeSeparator(string input)
        {
            return input?.Replace('х', 'x').Replace('Х', 'X'); // заменяем кириллические на латинские
        }

        //-----подготовка папок в работу-----//
        private void Create_Tech()
        {
            if (!File.Exists($"{Path.GetDirectoryName(Paths[0])}\\Заявка.xlsx"))
            {
                MessageBox.Show("Не удалось найти подходящую заявку для формирования папок.");
                return;
            }

            Tech tech = new($"{Path.GetDirectoryName(Paths[0])}\\Заявка.xlsx");
            MainWindow.M.StatusBegin(tech.Run(), MainWindow.StatusMessageType.Success);
        }


        //-----метод создания предварительного расчета-----//
        private void Create_ExpressOffer(object sender, RoutedEventArgs e)
        {
            if (TechItems.Count == 0) return;
            Show_ExpressOffer();
            //запускаем алгоритм автоматического раскроя деталей в фоновом режиме
            //MainWindow.M.CreateWorker(Nesting, MainWindow.ActionState.express);
        }

        public void Show_ExpressOffer()
        {
            try
            {
                MainWindow.M.ClearDetails();     // удаляем все детали
                MainWindow.M.ClearCalculate();   // очищаем расчет

                float density = 7.85f;           // плотность материала по умолчанию

                // группируем детали по материалу и толщине
                var groups = TechItems.GroupBy(m => new { m.Material, m.Destiny });

                foreach (var group in groups)
                {
                    var item = group.FirstOrDefault();
                    if (item is null) continue;

                    string profileType = ProfileParser.GetLocalizedTypeName(item);  //проверить
                    string metalName = group.Key.Material.ToLower() switch
                    {
                        "br" => "латунь",
                        "cu" => "медь",
                        "al" => "амг2",
                        "" => "ст3",
                        _ => group.Key.Material.ToLower()       //здесь нужна нормализация без пробелов
                    };
                    float destiny = item.Thickness > 0 ? (float)item.Thickness : MainWindow.Parser(group.Key.Destiny);

                    // Заготовка
                    TypeDetailControl typeControl;

                    foreach (TypeDetail t in MainWindow.M.TypeDetails)
                        if (t.Name == profileType)
                        {
                            if (t.Name == "Лист металла")
                            {
                                var laserComplect = MainWindow.M.DetailControls.FirstOrDefault(d => d.Detail.Title == "Комплект деталей");
                                if (laserComplect != null)
                                {
                                    laserComplect.AddTypeDetail();
                                    typeControl = laserComplect.TypeDetailControls[^1];
                                }
                                else
                                {
                                    MainWindow.M.AddDetail();
                                    typeControl = MainWindow.M.DetailControls[^1].TypeDetailControls[^1];
                                    typeControl.det.IsComplectChanged("Комплект деталей");
                                }

                                // тип заготовки - "Лист металла" по умолчанию

                                // определяем толщину заготовки
                                typeControl.S = destiny;

                                // определяем материал заготовки
                                foreach (Metal met in typeControl.MetalDrop.Items)
                                    if (met.Name == metalName)
                                    {
                                        typeControl.MetalDrop.SelectedItem = met;
                                        density = met.Density;
                                        break;
                                    }

                                // Резка
                                foreach (Work w in MainWindow.M.Works)
                                    if (w.Name == "Лазерная резка")
                                    {
                                        typeControl.WorkControls[^1].WorkDrop.SelectedItem = w;
                                        break;
                                    }

                                if (typeControl.WorkControls[^1].workType is CutControl cut && cut.work.type.MetalDrop.SelectedItem is Metal m)
                                {
                                    cut.PartsControl = new(cut, new());
                                    cut.AddPartsControl();

                                    List<Part> parts = new();
                                    foreach (var techItem in group)
                                    {
                                        // Нормализуем разделитель: русская 'х' (U+0445) → английская 'x' (U+0078)
                                        var normalized = techItem.Sizes.Replace('х', 'x').Trim();

                                        if (normalized.Contains('x'))
                                        {
                                            string[] sizes = normalized.Split('x', StringSplitOptions.RemoveEmptyEntries);
                                            if (sizes.Length >= 2)
                                            {
                                                techItem.Width = MainWindow.Parser(sizes[0].Trim());
                                                techItem.Height = MainWindow.Parser(sizes[1].Trim());
                                            }
                                        }
                                        else techItem.Height = MainWindow.Parser(techItem.Sizes);

                                        Part part = new(techItem.NumberName)
                                        {
                                            Count = (int)MainWindow.Parser(techItem.Count),
                                            Metal = m.Name,
                                            Destiny = destiny,
                                            Width = techItem.Width,
                                            Height = techItem.Height,
                                            PartType = PartType.Rectangle
                                        };

                                        cut.PartsControl?.UpdatePartAfterEdit(part, m, destiny);
                                        parts.Add(part);
                                    }

                                    cut.PartsControl?.AddBatchToCutControl(cut, parts, m);
                                }
                            }
                            else        // Трубы
                            {
                                var pipeComplect = MainWindow.M.DetailControls.FirstOrDefault(d => d.Detail.Title == "Комплект труб");
                                if (pipeComplect != null)
                                {
                                    pipeComplect.AddTypeDetail();
                                    typeControl = pipeComplect.TypeDetailControls[^1];
                                }
                                else
                                {
                                    MainWindow.M.AddDetail();
                                    typeControl = MainWindow.M.DetailControls[^1].TypeDetailControls[^1];
                                    typeControl.det.IsComplectChanged("Комплект труб");
                                }

                                // определяем тип заготовки и сечение
                                typeControl.TypeDetailDrop.SelectedItem = t;

                                if (t.Name.Contains("Уголок") || t.Name.Contains("Квадрат"))
                                {
                                    foreach (string s in typeControl.SortDrop.Items)
                                        if (s == $"{item.Width}")
                                        {
                                            typeControl.SortDrop.SelectedItem = s;
                                            break;
                                        }
                                }
                                else if (t.Name.Contains("Двутавр") || t.Name.Contains("Швеллер"))
                                {
                                    bool isFounded = false;
                                    foreach (string s in typeControl.SortDrop.Items)
                                        if (item.Destiny.Contains(s))
                                        {
                                            typeControl.SortDrop.SelectedItem = s;
                                            isFounded = true;
                                            break;
                                        }

                                    if (!isFounded)
                                    {
                                        typeControl.TypeDetailDrop.SelectedItem = MainWindow.M.TypeDetails
                                                                        .FirstOrDefault(t => t.Name == "Двутавр");
                                        foreach (string s in typeControl.SortDrop.Items)
                                            if (s == $"{item.Width}")
                                            {
                                                typeControl.SortDrop.SelectedItem = s;
                                                break;
                                            }
                                    }
                                }
                                else
                                {
                                    typeControl.A = (float)item.Width;
                                    typeControl.B = (float)item.Height;
                                }

                                // определяем толщину заготовки
                                if (destiny > 0) typeControl.S = destiny;

                                // определяем материал заготовки
                                foreach (Metal met in typeControl.MetalDrop.Items)
                                    if (met.Name == metalName)
                                    {
                                        typeControl.MetalDrop.SelectedItem = met;
                                        density = met.Density;
                                        break;
                                    }

                                // Труборез
                                foreach (Work w in MainWindow.M.Works)
                                    if (w.Name == "Труборез")
                                    {
                                        typeControl.WorkControls[^1].WorkDrop.SelectedItem = w;
                                        break;
                                    }

                                if (typeControl.WorkControls[^1].workType is PipeControl pipe && pipe.work.type.MetalDrop.SelectedItem is Metal m)
                                {
                                    pipe.PartsControl = new(pipe, new());
                                    pipe.AddPartsControl();

                                    List<Part> parts = new();
                                    foreach (var techItem in group)
                                    {
                                        Part part = new(techItem.NumberName)
                                        {
                                            Count = (int)MainWindow.Parser(techItem.Count),
                                            Metal = m.Name,
                                            Destiny = typeControl.S,
                                            Width = techItem.Width,
                                            Height = techItem.Height,
                                            Length = MainWindow.Parser(techItem.Sizes),
                                            PartType = techItem.PartType
                                        };

                                        pipe.PartsControl?.UpdatePartAfterEdit(part, m, typeControl.S);
                                        parts.Add(part);
                                    }

                                    pipe.PartsControl?.AddBatchToPipeControl(pipe, parts, m);
                                }
                            }
                        }
                }

                MainWindow.M.CloseRequestControl();

                MainWindow.M.IsExpressOffer = true;
                MessageBox.Show($"Предварительный расчет создан.\nПроверьте все данные по списку нарезанных деталей!");
            }
            catch (Exception ex)
            {
                MainWindow.M.IsExpressOffer = false;
                MessageBox.Show($"Не удалось создать быстрый расчет.\n{ex.Message}");
            }
        }

        private void ShowPopup_Geometries(object sender, MouseEventArgs e)
        {
            Popup.IsOpen = true;

            Details.Text = $"Изображение приблизительно, и может отличаться от исходной модели.";
        }

        private void ToggleRowDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var row = MainWindow.FindVisualParent<DataGridRow>(element);
                if (row != null)
                {
                    row.DetailsVisibility = row.DetailsVisibility == Visibility.Visible
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
            }
        }

        private void ShowPopup_Rules(object sender, MouseEventArgs e)
        {
            Popup.IsOpen = true;

            Details.Text = $"В соответствующем выпадающем списке можно увидеть,\n" +
                $"как правильно называть материалы деталей\n" +
                $"и добавляемые к ним работы.";
        }


        //-----метод добавления металла, толщины или работ в выбранные строки-----//
        private void Copy_Metal(object sender, RoutedEventArgs e)
        {
            var selectedMetal = MetalsDrop.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedMetal)) return;

            foreach (var item in GetUniqueSelectedItems().OfType<TechItem>())
            {
                item.Material = selectedMetal;
            }
        }

        private void Copy_Thickness(object sender, RoutedEventArgs e)
        {
            var selectedThickness = DestinyDrop.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedThickness)) return;

            foreach (var item in GetUniqueSelectedItems().OfType<TechItem>())
            {
                item.Destiny = selectedThickness;
            }
        }

        private void Copy_Work(object sender, RoutedEventArgs e)
        {
            if (WorksDrop.SelectedItems == null || WorksDrop.SelectedItems.Count == 0)
                return;

            // Извлекаем сокращённые названия (до "-")
            var selectedShortNames = WorksDrop.SelectedItems.Cast<string>()
                .Where(w => w.Contains('-'))
                .Select(w => w[..(w.IndexOf('-') - 1)].Trim())
                .ToList();

            if (selectedShortNames.Count == 0) return;

            // Получаем уникальные выделенные строки
            var selectedItems = GetUniqueSelectedItems().OfType<TechItem>().ToList();
            if (selectedItems.Count == 0) return;

            foreach (var item in selectedItems)
            {
                var currentRoute = (item.Route ?? "").Trim();
                var currentWorks = string.IsNullOrEmpty(currentRoute)
                    ? new HashSet<string>()
                    : new HashSet<string>(currentRoute.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

                // Добавляем только новые работы
                bool updated = false;
                foreach (var work in selectedShortNames)
                {
                    if (currentWorks.Add(work))
                        updated = true;
                }

                if (updated)
                {
                    item.Route = string.Join(" ", currentWorks);
                }
            }
        }

        private IEnumerable<object> GetUniqueSelectedItems()
        {
            return RequestGrid.SelectedCells
                .Select(cell => cell.Item)
                .Distinct();
        }


        //-----методы добавления гравировки в центр детали-----//
        private void AddEngraving(object sender, RoutedEventArgs e)
        {
            if (RequestGrid.SelectedCells.Count > 0)
                if (RequestGrid.SelectedCells[0].Item is TechItem techItem && TechItems.Contains(techItem))
                {
                    var engravingWindow = new EngravingWindow(EngravingMode.SingleItem, techItem);
                    if (engravingWindow.ShowDialog() == true)
                    {
                        string engravingText = engravingWindow.TextMarking;
                        string font = engravingWindow.SelectedFont;
                        double? fontSize = engravingWindow.FontSizeOverride;

                        try
                        {
                            CadDocument dxf;
                            using (var reader = new DxfReader(techItem.PathToModel))
                            {
                                dxf = reader.Read();
                            }

                            (Rect, float, int) data = MainWindow.GetDrawingBounds(dxf);

                            Rect partBoundsWpf = new(
                                data.Item1.X,
                                -data.Item1.Y - data.Item1.Height, // инверсия
                                data.Item1.Width,
                                data.Item1.Height
                                );

                            Engraving.AddEngravingAsPolylines(dxf, engravingText, partBoundsWpf, font, fontSize);

                            string? directory = Path.GetDirectoryName(techItem.PathToModel);
                            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(techItem.PathToModel);
                            if (Directory.Exists(directory))
                            {
                                string newPath = Path.Combine(directory, fileNameWithoutExt + " (грав).dxf");
                                using var writer = new DxfWriter(newPath, dxf);
                                writer.Write();
                            }
                        }
                        catch
                        {
                            MessageBox.Show($"Не удалось прочитать dxf ({techItem.PathToModel}).\n" +
                            $"Пересохраните файл в CAD-программе и попробуйте снова.");
                        }
                    }
                }
        }

        private void Shields_Engraving(object sender, RoutedEventArgs e)
        {
            // Выбор входного файла
            var inputDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Выберите \"shields_input.txt\"",
                Multiselect = false
            };

            if (inputDialog.ShowDialog() != true) return;

            var lines = File.ReadAllLines(inputDialog.FileName);

            // Выбор DXF шаблона
            var templateDialog = new OpenFileDialog
            {
                Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*",
                Title = "Выберите \"Шильд пустой.dxf\"",
                Multiselect = false
            };

            if (templateDialog.ShowDialog() != true) return;

            string templatePath = templateDialog.FileName;
            string outputDir = Path.GetDirectoryName(templatePath)!;

            // Кэшируем данные границ один раз (если они зависят только от шаблона)
            CadDocument templateDxf;
            using (var reader = new DxfReader(templatePath))
                templateDxf = reader.Read();

            var boundsData = MainWindow.GetDrawingBounds(templateDxf);
            Rect partBoundsWpf = new(
                boundsData.Item1.X,
                -boundsData.Item1.Y - boundsData.Item1.Height,
                boundsData.Item1.Width,
                boundsData.Item1.Height
            );

            // Для быстрого пересоздания — читаем шаблон в байты
            byte[] templateBytes = File.ReadAllBytes(templatePath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;

                string engraving = parts[0];
                if (!int.TryParse(parts[1], out int count) || count <= 0) continue;

                for (int i = 1; i <= count; i++)
                {
                    string line1 = engraving;
                    string line2 = $"№ в партии {i}";
                    string engravingText = $"{line1}\n{line2}";
                    string filename = $"{line1}_{line2}.dxf";
                    string fullPath = Path.Combine(outputDir, filename);

                    // ⚠️ Создаём НОВЫЙ документ из шаблона
                    CadDocument dxf;
                    using (var ms = new MemoryStream(templateBytes))
                    using (var reader = new DxfReader(ms))
                    {
                        dxf = reader.Read();
                    }

                    // Добавляем гравировку
                    Engraving.AddEngravingAsPolylines(dxf, engravingText, partBoundsWpf);

                    // Сохраняем
                    using var writer = new DxfWriter(fullPath, dxf);
                    writer.Write();
                }
            }

            MainWindow.M.StatusBegin($"Создано {lines.Sum(l => l.Split(' ').Length > 1 && int.TryParse(l.Split(' ')[1], out int n) ? n : 0)} файлов", MainWindow.StatusMessageType.Info);
        }

        private void ShowPopup_Shield(object sender, MouseEventArgs e)
        {
            Popup.IsOpen = true;

            Details.Text = $"Функция нанесения гравировки на шаблон шильды.\n" +
                $"Подготовьте txt-файл с необходимыми строчками\n" +
                $"и dxf-файл с шаблоном шильды.\n" +
                $"Программа создаст шильду в формате dxf\n" +
                $"на каждую строчку текста.";
        }

        private void AddEngraving_ToAllTechItems(object sender, RoutedEventArgs e)
        {
            if (TechItems.Count == 0 || !TechItems.Any(t => t.TextMarking != "")) return;

            var engravingWindow = new EngravingWindow(EngravingMode.BatchPreview);
            if (engravingWindow.ShowDialog() == true)
            {
                string font = engravingWindow.SelectedFont;
                double? fontSize = engravingWindow.FontSizeOverride;

                foreach (TechItem techItem in TechItems)
                    if (techItem.TextMarking != "")
                        try
                        {
                            CadDocument dxf;
                            using (var reader = new DxfReader(techItem.PathToModel))
                            {
                                dxf = reader.Read();
                            }

                            (Rect, float, int) data = MainWindow.GetDrawingBounds(dxf);

                            Rect partBoundsWpf = new(
                                data.Item1.X,
                                -data.Item1.Y - data.Item1.Height,
                                data.Item1.Width,
                                data.Item1.Height
                                );

                            Engraving.AddEngravingAsPolylines(dxf, techItem.TextMarking, partBoundsWpf, font, fontSize);

                            string? directory = Path.GetDirectoryName(techItem.PathToModel);
                            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(techItem.PathToModel);
                            if (Directory.Exists(directory))
                            {
                                string newPath = Path.Combine(directory, fileNameWithoutExt + " (грав).dxf");
                                using var writer = new DxfWriter(newPath, dxf);
                                writer.Write();
                            }
                        }
                        catch
                        {
                            MessageBox.Show($"Не удалось прочитать dxf ({techItem.PathToModel}).\n" +
                            $"Пересохраните файл в CAD-программе и попробуйте снова.");
                        }
            }
        }
    }


    public class RequestTemplate : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public int Id { get; set; }

        private string name = "по умолчанию";
        public string Name
        {
            get => name;
            set
            {
                if (name != value)
                {
                    name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private string destinyPattern = "s";
        public string DestinyPattern
        {
            get => destinyPattern;
            set
            {
                if (destinyPattern != value)
                {
                    destinyPattern = value;
                    OnPropertyChanged(nameof(DestinyPattern));
                }
            }
        }

        private string countPattern = "n";
        public string CountPattern
        {
            get => countPattern;
            set
            {
                if (countPattern != value)
                {
                    countPattern = value;
                    OnPropertyChanged(nameof(CountPattern));
                }
            }
        }

        private bool posDestiny = true;
        public bool PosDestiny
        {
            get => posDestiny;
            set
            {
                if (posDestiny != value)
                {
                    posDestiny = value;
                    OnPropertyChanged(nameof(PosDestiny));
                }
            }
        }

        private bool posCount = true;
        public bool PosCount
        {
            get => posCount;
            set
            {
                if (posCount != value)
                {
                    posCount = value;
                    OnPropertyChanged(nameof(PosCount));
                }
            }
        }

        public RequestTemplate() { }
    }
}