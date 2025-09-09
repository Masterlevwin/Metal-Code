using ACadSharp;
using ACadSharp.IO;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

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

                //if (targetTechItem?.Geometries.Count == 0)
                //    targetTechItem.NumberName = "Нет данных";
            }
        }

        private readonly RequestContext db = new(MainWindow.M.connections[12]);
        public List<string> Paths { get; set; } = new();
        public RequestTemplate CurrentTemplate { get; set; } = new();
        public ObservableCollection<RequestTemplate> Templates { get; set; } = new();
        public ObservableCollection<TechItem> TechItems { get; set; } = new();

        public RequestControl(List<string> paths)
        {
            InitializeComponent();
            DataContext = this;
            Update_Paths(paths);
        }


        //-----настройка контрола при загрузке-----//
        private void RequestControl_Loaded(object sender, RoutedEventArgs e)
        {
            db.Templates.Load();
            Templates = db.Templates.Local.ToObservableCollection();
            TemplatesList.ItemsSource = Templates;
        }
        private void Update_Paths(List<string> paths)
        {
            if (paths.Count == 0) return;

            Paths = paths;
            PathsList.ItemsSource = Paths.Select(x => Path.GetFileNameWithoutExtension(x));

            //если выбранный файл и есть заявка, загружаем ее данные
            if (Paths.Count == 1 && Paths[0].Contains("Заявка")) Load_Request(Paths[0]);
        }


        //-----загрузка данных заявки для редактирования-----//
        public void Load_Request(string path)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            try
            {
                //преобразуем открытый Excel-файл в DataTable для парсинга
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTable table = result.Tables[0];

                //количество комплектов
                if ($"{table.Rows[^1].ItemArray[4]}" == "Кол-во комплектов"
                    && $"{table.Rows[^1].ItemArray[5]}" != null
                    && int.TryParse($"{table.Rows[^1].ItemArray[5]}", out int value) && value > 0)
                    CountText.Text = $"{value}";

                //перебираем строки таблицы и заполняем список объектами TechItem
                for (int i = 2; i < table.Rows.Count; i++)
                {
                    if ($"{table.Rows[i].ItemArray[1]}" is null || $"{table.Rows[i].ItemArray[1]}" == "") continue;

                    TechItem techItem = new(
                        $"{table.Rows[i].ItemArray[1]}",        //номер чертежа
                        $"{table.Rows[i].ItemArray[2]}",        //размеры
                        $"{table.Rows[i].ItemArray[3]}",        //материал
                        $"{table.Rows[i].ItemArray[4]}",        //толщина
                        $"{table.Rows[i].ItemArray[5]}",        //количество
                        $"{table.Rows[i].ItemArray[6]}",        //маршрут
                        $"{table.Rows[i].ItemArray[7]}",        //давальческий материал      
                        $"{table.Rows[i].ItemArray[8]}",        //оригинальное наименование от заказчика
                        $"{table.Rows[i].ItemArray[9]}",        //путь к файлу модели
                        $"{table.Rows[i].ItemArray[10]}");      //сгенерирован ли номер чертежа
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
                {
                    var item = TechItems.FirstOrDefault(s => s.Sizes is null || s.Sizes == "");
                    IsAvailable = item is null;
                    MainWindow.M.StatusBegin("Заявка загружена");
                }
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
            else MainWindow.M.StatusBegin($"Не выбрано ни одного файла");
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
                if (template.PosDestiny) destinyPattern = $@"{Regex.Escape(template.DestinyPattern)}\s*(\d+(?:[,.]\d+)?)";
                else destinyPattern = $@"(\d+(?:[,.]\d+)?)\s*{Regex.Escape(template.DestinyPattern)}";

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
                if (Path.GetExtension(path) == ".dxf")
                {
                    try
                    {
                        var reader = new DxfReader(techItem.PathToModel);
                        CadDocument dxf = reader.Read();

                        (Rect, float, int) data = MainWindow.GetDrawingBounds(dxf);

                        techItem.Sizes = $"{Math.Ceiling(data.Item1.Width)}x{Math.Ceiling(data.Item1.Height)}";
                        techItem.Width = (float)Math.Ceiling(data.Item1.Width);
                        techItem.Height = (float)Math.Ceiling(data.Item1.Height);
                        techItem.Way = data.Item2;
                        techItem.Pinhole = data.Item3;

                        //заполняем геометрию для отрисовки
                        techItem.Geometries = MainWindow.GetGeometries(dxf, data.Item1, 60, 60);
                    }
                    catch { MessageBox.Show($"Не удалось прочитать dxf ({path}).\n" +
                        $"Пересохраните файл в CAD-программе и попробуйте снова."); }
                }

                TechItems.Add(techItem);
            }

            if (TechItems.Count > 0)
            {
                var item = TechItems.FirstOrDefault(s => s.Sizes is null || s.Sizes == "");
                IsAvailable = item is null;

                MainWindow.M.StatusBegin("Файлы успешно проанализированы");
            }
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

            MainWindow.M.StatusBegin("Наименования деталей сгенерированы.");
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

        //-----переименование заголовков при генерации колонок Datagrid-----//
        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (((PropertyDescriptor)e.PropertyDescriptor).IsBrowsable == false) e.Cancel = true;   //скрываем свойства с атрибутом [IsBrowsable]

            if (e.PropertyName == "N") e.Column.Header = "№";
            if (e.PropertyName == "NumberName") e.Column.Header = "№ чертежа";
            if (e.PropertyName == "Sizes") e.Column.Header = "Размеры";
            if (e.PropertyName == "Material") e.Column.Header = "Металл";
            if (e.PropertyName == "Destiny") e.Column.Header = "Толщина";
            if (e.PropertyName == "Count") e.Column.Header = "Кол-во деталей";
            if (e.PropertyName == "Route") e.Column.Header = "Маршрут";
            if (e.PropertyName == "HasMaterial") e.Column.Header = "Давальч";
            if (e.PropertyName == "OriginalName") e.Column.Header = "Исходник";
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
                MainWindow.M.StatusBegin("Не удалось определить свойство для копирования, или такое копирование запрещено.");
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
                MainWindow.M.StatusBegin("Копирование по горизонтали запрещено.");
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


        //-----удаление строки из списка деталей-----//
        private void Remove_TechItem(object sender, MouseButtonEventArgs e)
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
                "Чтобы удалить одну строку, дважды нажмите по строке правой кнопкой мыши.";
        }


        //-----создание заявки и подготовка папок одновременно-----//
        private void Launch_Tech(object sender, RoutedEventArgs e) { if (Create_Request()) Create_Tech(); }

        //-----создание заявки в формате Excel-----//
        private void Create_Request(object sender, RoutedEventArgs e) { Create_Request(); }
        private bool Create_Request()
        {
            if (TechItems.Count == 0)
            {
                MainWindow.M.StatusBegin("Чтобы создать заявку, запустите анализ файлов.");
                return false;
            }

            if (Directory.Exists(Path.GetDirectoryName(Paths[0])))
            {
                var baseDir = Directory.GetParent(Paths[0]);
                if (baseDir != null && !baseDir.Name.Contains("ТЗ", StringComparison.OrdinalIgnoreCase))
                {
                    MainWindow.M.StatusBegin("Папка с моделями, в которой будет создана заявка, должна называться \"ТЗ\".");
                    return false;
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
            requestsheet.Cells[TechItems.Count + 3, 5].Value = "Кол-во комплектов";
            requestsheet.Cells[TechItems.Count + 3, 6].Value = CountText.Text;
            requestsheet.Cells[TechItems.Count + 3, 6].Style.Font.Color.SetColor(System.Drawing.Color.Red);
            requestsheet.Cells[TechItems.Count + 3, 5, TechItems.Count + 3, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            requestsheet.Cells[TechItems.Count + 3, 5, TechItems.Count + 3, 6].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            requestsheet.Cells[TechItems.Count + 3, 5].Style.Border.Right.Style = ExcelBorderStyle.Thin;
            requestsheet.Cells[TechItems.Count + 3, 5, TechItems.Count + 3, 6].Style.Border.BorderAround(ExcelBorderStyle.Medium);

            //устанавливаем заголовки таблицы
            List<string> _heads = new() { "№", "№ чертежа", "Размеры", "Металл", "Толщина", "Кол-во деталей", "Маршрут", "Давальч", "Исходник", "Путь к модели", "Сген" };
            for (int head = 0; head < _heads.Count; head++) requestsheet.Cells[2, head + 1].Value = _heads[head];

            //string message = "";
            for (int i = 0; i < TechItems.Count; i++)
            {
                requestsheet.Cells[i + 3, 1].Value = i + 1;
                requestsheet.Cells[i + 3, 2].Value = TechItems[i].NumberName;
                requestsheet.Cells[i + 3, 3].Value = TechItems[i].Sizes;
                requestsheet.Cells[i + 3, 4].Value = TechItems[i].Material;
                requestsheet.Cells[i + 3, 5].Value = NormalizeSeparator(TechItems[i].Destiny);
                requestsheet.Cells[i + 3, 6].Value = TechItems[i].Count;
                requestsheet.Cells[i + 3, 7].Value = TechItems[i].Route;
                requestsheet.Cells[i + 3, 8].Value = TechItems[i].HasMaterial;
                requestsheet.Cells[i + 3, 9].Value = TechItems[i].OriginalName;
                requestsheet.Cells[i + 3, 10].Value = TechItems[i].PathToModel;
                requestsheet.Cells[i + 3, 11].Value = TechItems[i].IsGenerated ? "да" : "";
            }

            requestsheet.Column(9).Hidden = true;
            requestsheet.Column(10).Hidden = true;
            requestsheet.Column(11).Hidden = true;

            ExcelWorksheet ordersheet = workbook.Workbook.Worksheets.Add($"КП");
            ordersheet.Cells[1, 1].Value = "КП №";
            ordersheet.Cells[1, 2].Value = MainWindow.M.Order.Text;
            ordersheet.Cells[2, 1].Value = "для заказчика";
            ordersheet.Cells[2, 2].Value = MainWindow.M.CustomerDrop.Text;
            ordersheet.Cells[3, 1].Value = "менеджер";
            ordersheet.Cells[3, 2].Value = MainWindow.M.ManagerDrop.Text;

            ExcelRange order = ordersheet.Cells[1, 1, 3, 2];                            //получаем данные КП для оформления
            ExcelRange details = requestsheet.Cells[2, 1, TechItems.Count + 2, 8];      //получаем таблицу деталей для оформления

            //обводка границ и авторастягивание столбцов
            order.Style.HorizontalAlignment = details.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            order.Style.VerticalAlignment = details.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            order.Style.Border.Right.Style = order.Style.Border.Bottom.Style = details.Style.Border.Right.Style = details.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            order.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            details.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            requestsheet.Cells[2, 1, 2, 8].Style.WrapText = true;
            requestsheet.Cells[2, 1, 2, 8].Style.Font.Bold = true;
            requestsheet.Cells.AutoFitColumns();
            ordersheet.Cells.AutoFitColumns();

            //сохраняем книгу в файл Excel
            try { workbook.SaveAs($"{Path.GetDirectoryName(Paths[0])}\\Заявка.xlsx"); }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}\n" +
                $"Возможно файл заявки уже открыт, поэтому ее не создать!");
            }

            MainWindow.M.StatusBegin($"Создана заявка в папке {Path.GetDirectoryName(Paths[0])}");
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
            MainWindow.M.StatusBegin(tech.Run());
        }


        //-----метод создания предварительного расчета-----//
        private void Create_ExpressOffer(object sender, RoutedEventArgs e)
        {
            if (TechItems.Count == 0) return;

            //запускаем алгоритм автоматического раскроя деталей в фоновом режиме
            MainWindow.M.CreateWorker(Nesting, MainWindow.ActionState.express);
        }

        private List<SheetPacker> Packers = new();

        private string Nesting(string? message = null)
        {
            Packers.Clear();

            //группируем детали по материалу и толщине
            var groups = TechItems.Where(d => d.Destiny != "").GroupBy(m => new { m.Material, m.Destiny });

            //на каждую группу формируем список листов для укладки деталей
            foreach (var group in groups)
            {
                //инициализируем упаковку
                var packer = new SheetPacker(group.Key.Destiny, group.Key.Material);

                //получаем полное количество деталей по одному экземпляру для визуализации раскладки
                var expandedParts = new List<TechItem>();
                foreach (var item in group)
                {
                    int count = (int)MainWindow.Parser(item.Count);
                    for (int i = 0; i < count; i++)
                    {
                        // Создаём копию, чтобы не модифицировать оригинал
                        expandedParts.Add(new TechItem
                        {
                            NumberName = item.NumberName,
                            Width = item.Width,
                            Height = item.Height,
                            Material = item.Material,
                            Destiny = item.Destiny,
                            Geometries = item.Geometries,
                            Way = item.Way,
                            Pinhole = item.Pinhole,
                            Sizes = item.Sizes
                            // X, Y, Color — будут установлены при укладке
                        });
                    }
                }

                //получаем список листов данной группы
                packer.Pack(expandedParts);

                //сохраняемем изображение каждого листа
                if (packer.sheets.Count > 0)
                    foreach (var sheet in packer.sheets) sheet.GenerateImage(400, 200);

                //добавим упаковку в список для дальнейшего использования
                Packers.Add(packer);
            }

            message = "Раскладка выполнена успешно.";

            return message;
        }

        public void Show_ExpressOffer()
        {
            try
            {
                MainWindow.M.NewProject();

                //группируем детали по материалу и толщине
                var groups = TechItems.Where(d => d.Destiny != "").GroupBy(m => new { m.Material, m.Destiny });

                foreach (var group in groups)
                {
                    TypeDetailControl type = MainWindow.M.DetailControls[0].TypeDetailControls[^1];

                    var packer = Packers.FirstOrDefault(p => p.destiny == group.Key.Destiny && p.material == group.Key.Material);

                    if (packer is not null)
                    {
                        float density = 7.8f;      //плотность материала по умолчанию

                        //устанавливаем "Лист металла" и заполняем эту заготовку
                        foreach (TypeDetail t in MainWindow.M.TypeDetails)
                            if (t.Name == "Лист металла")
                            {
                                type.TypeDetailDrop.SelectedItem = t;
                                type.CreateSort(packer.sheetWidth switch
                                {
                                    3000 => 0,
                                    2500 => 1,
                                    _ => 3
                                });
                                type.S = MainWindow.Parser(group.Key.Destiny);

                                //определяем материал заготовки
                                string met = group.Key.Material.ToLower() switch
                                {
                                    "br" => "латунь",
                                    "cu" => "медь",
                                    "al" => "амг2",
                                    "" => "ст3",
                                    _ => group.Key.Material.ToLower()
                                };
                                foreach (Metal metal in type.MetalDrop.Items)
                                    if (metal.Name == met)
                                    {
                                        type.MetalDrop.SelectedItem = metal;
                                        density = metal.Density;
                                        break;
                                    }
                            }
                        //устанавливаем "Лазерная резка" и заполняем эту резку
                        foreach (Work w in MainWindow.M.Works)
                            if (w.Name == "Лазерная резка")
                            {
                                type.WorkControls[^1].WorkDrop.SelectedItem = w;
                                if (type.WorkControls[^1].workType is CutControl cut)
                                {
                                    List<Part> parts = new();       //список нарезанных деталей

                                    float way = 0;                  //путь резки
                                    int pinholes = 0;               //проколы

                                    //рассчитываем количество листов заготовки, путь резки и проколы, заодно заполняем коллекцию деталей
                                    foreach (var item in group)
                                    {
                                        int count = (int)MainWindow.Parser(item.Count);

                                        if (item.Geometries.Count > 0)
                                        {
                                            way += item.Way * count;
                                            pinholes += item.Pinhole * count;
                                        }
                                        else
                                        {
                                            way += (item.Width + item.Height) * 2 * count;
                                            pinholes += 2 * count;
                                        }

                                        Part part = new(item.NumberName, count)
                                        {
                                            Metal = group.Key.Material.ToLower(),
                                            Destiny = type.S,
                                            Way = item.Way > 0 ? item.Way : (float)Math.Round((item.Width + item.Height) / 1000, 3),
                                            Mass = (float)Math.Round(item.Width * item.Height * type.S * density / 1000000, 3),
                                            Geometries = item.Geometries
                                        };

                                        //записываем полученные габариты и саму строку для их отображения в словарь свойств
                                        part.PropsDict[100] = new() { $"{item.Width}", $"{item.Height}", item.Sizes.ToLower() };
                                        parts.Add(part);
                                    }

                                    type.Count = packer.sheets.Count;

                                    cut.Way = (int)Math.Ceiling(way / 1000);
                                    cut.WayTotal = parts.Sum(p => p.Way * p.Count);
                                    cut.Pinhole = pinholes;
                                    cut.Mass = packer.sheets.Sum(s => s.CutWidth * s.CutHeight) * type.Count * type.S * density / 1000000;
                                    cut.MassTotal = parts.Sum(p => p.Mass * p.Count);

                                    if (packer.sheets.Count > 0)
                                        foreach (var sheet in packer.sheets)
                                            cut.Items?.Add(new()
                                            {
                                                sheets = 1,
                                                sheetSize = $"{sheet.Width}x{sheet.Height}",
                                                way = cut.Way / packer.sheets.Count,
                                                pinholes = cut.Pinhole / packer.sheets.Count,
                                                mass = sheet.CutWidth * sheet.CutHeight * type.S * density / 1000000,
                                                imageBytes = sheet.ImageBytes
                                            });

                                    if (cut.Items?.Count > 0) cut.SumProperties(cut.Items);

                                    cut.PartDetails = parts;
                                    cut.Parts = cut.PartList();
                                    cut.PartsControl = new(cut, cut.Parts);
                                    cut.AddPartsTab();
                                }
                                break;
                            }

                        if (groups.Count() > MainWindow.M.DetailControls[0].TypeDetailControls.Count)
                            MainWindow.M.DetailControls[0].AddTypeDetail();
                    }
                }

                //определяем деталь, в которой загрузили раскладки, как комплект деталей
                if (!MainWindow.M.DetailControls[0].Detail.IsComplect) MainWindow.M.DetailControls[0].IsComplectChanged("Комплект деталей");
                if (MainWindow.M.DetailControls[0].TypeDetailControls.Count > 0)
                    foreach (TypeDetailControl type in MainWindow.M.DetailControls[0].TypeDetailControls)
                        type.CreateSort();

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
    }

    public class SheetPacker
    {
        public float sheetWidth;
        public float sheetHeight;
        public float indent;
        public string destiny;
        public string material;
        public List<Sheet> sheets = new();

        public SheetPacker(string _destiny, string _material)
        {
            destiny = _destiny;
            material = _material;

            Tuning();   //определяем раскрой листа по умолчанию и отступ между деталями
        }

        private void Tuning()
        {

            if (material.ToLower() == "br" || material.ToLower() == "cu")
            {
                sheetWidth = 1500;
                sheetHeight = 600;
            }
            else if (material.ToLower().Contains("aisi") ||
                material.ToLower().Contains("цинк") ||
                MainWindow.Parser(destiny) < 3)
            {
                sheetWidth = 2500;
                sheetHeight = 1250;
            }
            else
            {
                sheetWidth = 3000;
                sheetHeight = 1500;
            }

            indent = MainWindow.Parser(destiny) > 10 ? MainWindow.Parser(destiny) : 10;
        }

        public void Pack(List<TechItem> parts)
        {
            var sortedParts = parts
                .OrderByDescending(p => p.Width * p.Height)
                .ToList();

            foreach (var part in sortedParts)
            {
                float propW = (float)part.Width + indent;
                float propH = (float)part.Height + indent;
                bool placed = false;

                // Попробовать существующие листы
                foreach (var sheet in sheets)
                {
                    if (TryPlace(sheet, propW, propH, part))
                    {
                        placed = true;
                        break;
                    }
                }

                // Если не поместилось — новый лист
                if (!placed)
                {
                    var newSheet = new Sheet(sheetWidth, sheetHeight, indent);
                    if (TryPlace(newSheet, propW, propH, part))
                    {
                        sheets.Add(newSheet);
                    }
                    //else
                    //{
                    //    throw new InvalidOperationException($"Деталь {part.NumberName} слишком большая для листа.");
                    //}
                }
            }
        }

        private static bool TryPlace(Sheet sheet, float w, float h, TechItem part)
        {
            // Попробовать без поворота
            if (sheet.CanFit(w, h, out float x, out float y))
            {
                sheet.Place(w, h, part, x, y);
                return true;
            }

            // Попробовать с поворотом
            if (sheet.CanFit(h, w, out x, out y))
            {
                part.IsRotated = true;
                (part.Width, part.Height) = (part.Height, part.Width);
                sheet.Place(h, w, part, x, y);
                return true;
            }

            return false;
        }
    }

    public class Sheet
    {
        public float Width { get; }  // Исходная ширина листа
        public float Height { get; } // Исходная высота листа
        public float CutWidth { get; private set; }  // После обрезки
        public float CutHeight { get; private set; } // После обрезки

        // Хранит координаты размещённых деталей
        public List<(float x, float y, float w, float h)> UsedAreas = new();

        // Для визуализации
        public List<TechItem> TechItems { get; } = new();
        public byte[] ImageBytes { get; set; } = null!;

        private readonly float _indent;     //отступ

        public Sheet(float width, float height, float indent)
        {
            Width = CutWidth = width;
            Height = CutHeight = height;
            _indent = indent;
        }

        public bool CanFit(float w, float h, out float x, out float y)
        {
            x = 0; y = 0;

            // Начинаем снизу слева
            while (y + h <= Height)
            {
                while (x + w <= Width)
                {
                    if (!IsOverlapping(x, y, w, h))
                    {
                        return true; // Нашли место
                    }
                    x += 10; // Мелкий шаг поиска
                }
                x = 0;
                y += 10;
            }

            return false;
        }

        private bool IsOverlapping(float x, float y, float w, float h)
        {
            foreach (var (ux, uy, uw, uh) in UsedAreas)
            {
                if (x < ux + uw && x + w > ux &&
                    y < uy + uh && y + h > uy)
                {
                    return true;
                }
            }
            return false;
        }

        public void Place(float w, float h, TechItem part, float x, float y)
        {
            UsedAreas.Add((x, y, w, h));
            part.X = x;
            part.Y = y;
            part.Color = GetColorForMaterial(part.Material);
            TechItems.Add(part);
        }

        private static Brush GetColorForMaterial(string material)
        {
            return material.ToLower() switch
            {
                "ст3" => Brushes.LightGray,
                "латунь" => Brushes.Gold,
                "медь" => Brushes.OrangeRed,
                "амг2" => Brushes.LightSteelBlue,
                _ => new SolidColorBrush(Color.FromRgb(
                    (byte)(200 + (material.GetHashCode() % 56)),
                    (byte)(100 + (material.GetHashCode() % 156)),
                    (byte)(150 + (material.GetHashCode() % 106))))
            };
        }

        public void GenerateImage(int widthImage, int heightImage)
        {
            OptimizeAndTrim();     // Обрезка

            var renderTarget = new RenderTargetBitmap(widthImage, heightImage, 96, 96, PixelFormats.Pbgra32);
            var drawingVisual = new DrawingVisual();

            using (var context = drawingVisual.RenderOpen())
            {
                // Фон
                context.DrawRectangle(Brushes.LightGray, null, new Rect(0, 0, widthImage, heightImage));

                // Масштаб
                double scaleX = widthImage / Width;
                double scaleY = heightImage / Height;
                double scale = Math.Min(scaleX, scaleY);

                // Центрирование
                double offsetX = (widthImage - Width * scale) / 2;
                double offsetY = (heightImage - Height * scale) / 2;

                // Лист
                var pen = new Pen(Brushes.Black, 2);

                // Рисуем обрезанный лист (не весь исходный!)
                var actualSheetRect = new Rect(
                    offsetX,
                    offsetY + (Height - CutHeight) * scale, // снизу вверх
                    CutWidth * scale,
                    CutHeight * scale
                );
                context.DrawRectangle(null, pen, actualSheetRect);

                // Оригинальный лист — тонкой линией
                context.DrawRectangle(
                    null,
                    new Pen(Brushes.LightGray, 1),
                    new Rect(offsetX, offsetY, Width * scale, Height * scale)
                );

                // Ось Y: (0,0) — нижний левый угол
                foreach (var part in TechItems)
                {
                    // Учитываем отступ при отрисовке
                    double x = offsetX + part.X * scale;
                    double y = offsetY + (Height - (part.Y + part.Height)) * scale; // снизу вверх
                    double w = (part.Width - _indent) * scale; // уменьшаем ширину
                    double h = (part.Height - _indent) * scale; // уменьшаем высоту

                    // Ограничиваем минимальный размер
                    w = Math.Max(w, 1);
                    h = Math.Max(h, 1);

                    // Фон детали
                    context.DrawRectangle(part.Color, pen, new Rect(x, y, w, h));

                    // Текст (с учётом отступа)
                    if (w > 20 && h > 15) // только если место
                    {
                        string label = part.NumberName.Length * 6 > w ? "..." : part.NumberName;

                        var typeface = new Typeface("Arial");
                        var formattedText = new FormattedText(
                            label,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            10,
                            Brushes.Black,
                            1.0);

                        // Центрируем текст в уменьшенной области
                        double textX = x + (w - formattedText.Width) / 2;
                        double textY = y + (h - formattedText.Height) / 2;

                        context.DrawText(formattedText, new Point(textX, textY));
                    }
                }
            }

            renderTarget.Render(drawingVisual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            ImageBytes = stream.ToArray();
        }
        
        public void OptimizeAndTrim()
        {
            if (TechItems.Count == 0)
            {
                CutWidth = 100;
                CutHeight = 100;
            }
            else
            {
                // Находим максимальные границы занятой области
                double maxX = 0, maxY = 0;
                foreach (var item in TechItems)
                {
                    double right = item.X + item.Width;
                    double top = item.Y + item.Height;
                    if (right > maxX) maxX = right;
                    if (top > maxY) maxY = top;
                }

                // Добавляем отступ
                maxX += _indent;
                maxY += _indent;

                // Обрезаем кратно 100 мм (вверх)
                CutWidth = (float)Math.Ceiling(maxX / 100.0) * 100;
                CutHeight = (float)Math.Ceiling(maxY / 100.0) * 100;

                // Ограничиваем размеры исходным листом
                CutWidth = Math.Min(CutWidth, Width);
                CutHeight = Math.Min(CutHeight, Height);
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