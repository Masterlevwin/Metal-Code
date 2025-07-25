﻿using ACadSharp;
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
                    var reader = new DxfReader(techItem.PathToModel);
                    CadDocument dxf = reader.Read();
                    Rect drawingBounds = MainWindow.GetDrawingBounds(dxf);

                    techItem.Sizes = $"{Math.Ceiling(drawingBounds.Width)}x{Math.Ceiling(drawingBounds.Height)}";

                    //заполняем геометрию для отрисовки
                    techItem.Geometries = MainWindow.GetGeometries(dxf, drawingBounds);
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
        private void Launch_Tech(object sender, RoutedEventArgs e)
        {
            Create_Request();
            Create_Tech();
        }

        //-----создание заявки в формате Excel-----//
        private void Create_Request(object sender, RoutedEventArgs e) { Create_Request(); }
        private void Create_Request()
        {
            if (TechItems.Count == 0)
            {
                MainWindow.M.StatusBegin("Чтобы создать заявку, запустите анализ файлов.");
                return;
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
                requestsheet.Cells[i + 3, 5].Value = TechItems[i].Destiny;
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

            try
            {
                MainWindow.M.NewProject();

                //группируем детали по материалу и толщине
                var groups = TechItems.Where(d => d.Destiny != "").GroupBy(m => new { m.Material, m.Destiny });

                foreach (var group in groups)
                {
                    int sortIndex = 0;

                    //определяем раскрой листа группы по умолчанию
                    if (group.Key.Material.ToLower() == "br" ||
                        group.Key.Material.ToLower() == "cu")
                        sortIndex = 3;
                    else if (group.Key.Material.ToLower().Contains("aisi") ||
                        group.Key.Material.ToLower().Contains("цинк") ||
                        MainWindow.Parser(group.Key.Destiny) < 3)
                        sortIndex = 1;
                    else sortIndex = 0;

                    TypeDetailControl type = MainWindow.M.DetailControls[0].TypeDetailControls[^1];

                    float density = 7.8f;      //плотность материала по умолчанию

                    //устанавливаем "Лист металла" и заполняем эту заготовку
                    foreach (TypeDetail t in MainWindow.M.TypeDetails)
                        if (t.Name == "Лист металла")
                        {
                            type.TypeDetailDrop.SelectedItem = t;
                            type.CreateSort(sortIndex);
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

                                float square = 0,   //площадь деталей
                                        way = 0,    //путь резки
                                    indent = 10;    //отступ
                                int pinholes = 0;   //проколы

                                //рассчитываем количество листов заготовки, путь резки и проколы, заодно заполняем коллекцию деталей
                                foreach (var item in group)
                                {
                                    string[] properties = item.Sizes.ToLower().Split('x');
                                    if (properties.Length > 1)
                                    {
                                        square += (MainWindow.Parser(properties[0]) + indent)
                                                * (MainWindow.Parser(properties[1]) + indent)
                                                * MainWindow.Parser(item.Count);
                                        way += (MainWindow.Parser(properties[0]) + MainWindow.Parser(properties[1]) + indent)
                                                * 2 * MainWindow.Parser(item.Count);
                                        pinholes += (int)MainWindow.Parser(item.Count);

                                        Part part = new(item.NumberName, (int)MainWindow.Parser(item.Count))
                                        {
                                            Metal = group.Key.Material.ToLower(),
                                            Destiny = MainWindow.Parser(group.Key.Destiny),
                                            Way = (float)Math.Round((MainWindow.Parser(properties[0]) + MainWindow.Parser(properties[1]) + indent) / 500, 3),
                                            Mass = (float)Math.Round((MainWindow.Parser(properties[0]) + indent) * (MainWindow.Parser(properties[1]) + indent) * type.S * density / 1000000, 3)
                                        };
                                        //записываем полученные габариты и саму строку для их отображения в словарь свойств
                                        part.PropsDict[100] = new() { properties[0], properties[1], item.Sizes.ToLower() };
                                        parts.Add(part);
                                    }
                                }

                                //получаем количество листов на основе общей площади деталей
                                type.Count = (int)Math.Ceiling(square / type.A / type.B);

                                //алгоритм определения обрезка
                                var sizes = parts.SelectMany(p => p.PropsDict[100]);    //получаем габариты всех деталей как строки
                                List<float> _sizes = new();                             //список габаритов в виде чисел кратных 100
                                foreach (string size in sizes)
                                {
                                    float _size = MainWindow.Parser(size);
                                    if (_size > 0) _sizes.Add((float)Math.Ceiling(_size / 100) * 100);
                                }
                                _sizes = _sizes.Distinct().OrderDescending().ToList();  //очищаем список от дубликатов и сортируем его

                                float width = 0;                                        //ширина обрезка
                                foreach (float _size in _sizes)
                                {                                                       //располагаем габариты по ширине листа
                                    width += _size;                                     //до тех пор, пока полученной площади листа
                                    if (width * type.B * type.Count - square > 0)       //не хватит для обеспечений общей площади деталей
                                    {
                                        type.A = width;                                 //таким образом определяем нужную ширину обрезка
                                        break;
                                    }
                                }

                                cut.Way = (int)Math.Ceiling(way / 1000);
                                cut.WayTotal = parts.Sum(p => p.Way * p.Count);
                                cut.Pinhole = pinholes * 2;
                                cut.Mass = type.Count * type.A * type.B * type.S * density / 1000000;
                                cut.MassTotal = parts.Sum(p => p.Mass * p.Count);

                                LaserItem laser = new() { sheetSize = $"{type.A}X{type.B}", sheets = type.Count };
                                laser.way = cut.Way / laser.sheets;
                                laser.pinholes = cut.Pinhole / laser.sheets;
                                laser.mass = cut.Mass / laser.sheets;
                                cut.Items?.Add(laser);
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