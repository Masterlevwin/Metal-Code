using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    /// Логика взаимодействия для WorkWindow.xaml
    /// </summary>
    public partial class RequestWindow : Window
    {
        private readonly RequestContext db = new(MainWindow.M.connections[12]);
        public string[] Paths { get; set; }
        public RequestTemplate CurrentTemplate { get; set; } = new();
        public ObservableCollection<RequestTemplate> Templates { get; set; } = new();
        public ObservableCollection<TechItem> TechItems { get; set; } = new();

        public RequestWindow(string[] paths)
        {
            InitializeComponent();
            Paths = paths;
            DataContext = this;
        }

        //-----настройка окна при загрузке-----//
        private void RequestWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PathsList.ItemsSource = Paths.Select(x => Path.GetFileNameWithoutExtension(x));

            db.Templates.Load();
            Templates = db.Templates.Local.ToObservableCollection();
            TemplatesList.ItemsSource = Templates;
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
            if (Paths.Length == 0 || TemplatesList.SelectedItem is not RequestTemplate template) return;

            if (TechItems.Count > 0) TechItems.Clear();

            foreach (string path in Paths)
            {
                TechItem techItem = new() { NumberName = Path.GetFileNameWithoutExtension(path),
                                            OriginalName = Path.GetFileNameWithoutExtension(path) };
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
                techItem.NumberName = Regex.Replace(techItem.NumberName, @"[^\p{L}\p{Nd}]+$", "");

                //записываем путь к файлу
                techItem.DxfPath = path;

                TechItems.Add(techItem);
            }

            if (TechItems.Count > 0) MainWindow.M.StatusBegin("Файлы успешно проанализированы");
        }

        //-----генерация имён скопированных файлов-----//
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
            }

            //при совпадении сгенерированных имён добавляем порядковый индекс к имени
            var collect = TechItems.GroupBy(x => x.NumberName);
                foreach (var item in collect)
                    if (item.Count() > 1)
                        foreach (var _item in item)
                            _item.NumberName += $".{item.ToList().IndexOf(_item)}";

            //создаем папку "Сгенерированные файлы" в директории заявки
            DirectoryInfo dirGen = Directory.CreateDirectory(Path.GetDirectoryName(Paths[0]) + "\\" + "Сгенерированные файлы");

            //копируем файлы с новыми именами в эту папку
            foreach (TechItem techItem in TechItems)
            {
                if (techItem.DxfPath is null || !File.Exists(techItem.DxfPath)) continue;

                string newPath = dirGen + "\\" + techItem.NumberName + Path.GetExtension(techItem.DxfPath);
                if (File.Exists(newPath)) continue;

                File.Copy(techItem.DxfPath, newPath);
                techItem.DxfPath = newPath;
            }

            RequestGrid.ItemsSource = null;
            RequestGrid.ItemsSource = TechItems;
            MainWindow.M.StatusBegin("Файлы скопированы с новыми именами");
        }
        private void ShowPopup_Gen(object sender, MouseEventArgs e)
        {
            Popup.IsOpen = true;

            Details.Text = "Функция генерации копирует выбранные файлы\n" +
                "с новыми именами в виде децимального номера.\n" +
                "После загрузки раскладок из Ажанкам эти имена\n" +
                "будут заменены обратно на исходные имена от заказчика.";
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
            ExcelWorksheet requestsheet = workbook.Workbook.Worksheets.Add($"Заявка Лазер");

            //оформляем статичные ячейки по умолчанию
            requestsheet.Cells[1, 1].Value = "Расшифровка работ: гиб - гибка, вальц - вальцовка, зен - зенковка," +
                "рез - резьба, свар - сварка,\nокр - окраска, оц - оцинковка, грав - гравировка, фрез - фрезеровка, " +
                "аква - аквабластинг, лен - лентопил";
            requestsheet.Cells[1, 1, 1, 9].Merge = true;
            requestsheet.Cells[1, 1, 1, 9].Style.WrapText = true;
            requestsheet.Cells[1, 1, 1, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            requestsheet.Cells[1, 1, 1, 9].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            requestsheet.Row(1).Height = 30;
            requestsheet.Cells[TechItems.Count + 3, 5].Value = "Кол-во комплектов";
            requestsheet.Cells[TechItems.Count + 3, 6].Value = CountText.Text;
            requestsheet.Cells[TechItems.Count + 3, 6].Style.Font.Color.SetColor(System.Drawing.Color.Red);
            requestsheet.Cells[TechItems.Count + 3, 5, TechItems.Count + 3, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            requestsheet.Cells[TechItems.Count + 3, 5, TechItems.Count + 3, 6].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            requestsheet.Cells[TechItems.Count + 3, 5].Style.Border.Right.Style = ExcelBorderStyle.Thin;
            requestsheet.Cells[TechItems.Count + 3, 5, TechItems.Count + 3, 6].Style.Border.BorderAround(ExcelBorderStyle.Medium);

            //устанавливаем заголовки таблицы
            List<string> _heads = new() { "№", "№ чертежа", "Размеры", "Металл", "Толщина", "Кол-во деталей", "Маршрут", "Давальч", "Исходник" };
            for (int head = 0; head < _heads.Count; head++) requestsheet.Cells[2, head + 1].Value = _heads[head];

            //string message = "";
            for (int i = 0; i < TechItems.Count; i++)
            {
                requestsheet.Cells[i + 3, 1].Value = i + 1;
                requestsheet.Cells[i + 3, 2].Value = TechItems[i].NumberName;
                requestsheet.Cells[i + 3, 3].Value = MainWindow.GetSizes(TechItems[i].DxfPath);
                //if ($"{requestsheet.Cells[i + 3, 3].Value}" == "") message += $"\n{requestsheet.Cells[i + 3, 2].Value}";
                requestsheet.Cells[i + 3, 4].Value = TechItems[i].Material;
                requestsheet.Cells[i + 3, 5].Value = TechItems[i].Destiny;
                requestsheet.Cells[i + 3, 6].Value = TechItems[i].Count;
                requestsheet.Cells[i + 3, 7].Value = TechItems[i].Route;
                requestsheet.Cells[i + 3, 8].Value = TechItems[i].HasMaterial;
                requestsheet.Cells[i + 3, 9].Value = TechItems[i].OriginalName;
            }
            //if (message != "") MessageBox.Show(message.Insert(0, "Не удалось получить габариты следующих деталей:"));

            ExcelRange details = requestsheet.Cells[2, 1, TechItems.Count + 2, 9];     //получаем таблицу деталей для оформления

            //обводка границ и авторастягивание столбцов
            details.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            details.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            details.Style.Border.Right.Style = details.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            details.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            requestsheet.Cells.AutoFitColumns();
            requestsheet.Cells[2, 1, 2, 9].Style.WrapText = true;
            requestsheet.Cells[2, 1, 2, 9].Style.Font.Bold = true;

            //устанавливаем настройки для печати, чтобы сохранение в формате .pdf выводило весь документ по ширине страницы
            requestsheet.PrinterSettings.FitToPage = true;
            requestsheet.PrinterSettings.FitToWidth = 1;
            requestsheet.PrinterSettings.FitToHeight = 0;
            requestsheet.PrinterSettings.HorizontalCentered = true;

            //сохраняем книгу в файл Excel
            workbook.SaveAs($"{Path.GetDirectoryName(Paths[0])}\\Заявка Лазер.xlsx");

            MainWindow.M.StatusBegin($"Создана заявка в папке {Path.GetDirectoryName(Paths[0])}");
        }

        //переименование заголовков при генерации колонок Datagrid
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

        //метод копирования данных в выделенные ячейки после отпускания мыши
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

        //вспомогательный метод для получения имени свойства из колонки
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

        //метод укорачивания наименований
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

        //-----подготовка папок в работу-----//
        private void Create_Tech(object sender, RoutedEventArgs e) { Create_Tech(); }
        private void Create_Tech()
        {
            if (!File.Exists($"{Path.GetDirectoryName(Paths[0])}\\Заявка Лазер.xlsx"))
            {
                MessageBox.Show("Не удалось найти подходящую заявку для формирования папок.");
                return;
            }

            Tech tech = new($"{Path.GetDirectoryName(Paths[0])}\\Заявка Лазер.xlsx");
            MainWindow.M.StatusBegin(tech.Run());
        }

        //-----создание заявки и подготовка папок одновременно-----//
        private void Launch_Tech(object sender, RoutedEventArgs e)
        {
            Create_Request();
            Create_Tech();
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
                if(countPattern != value)
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

        public RequestTemplate() {}
    }
}