using OfficeOpenXml.Style;
using OfficeOpenXml;
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

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WorkWindow.xaml
    /// </summary>
    public partial class RequestWindow : Window
    {
        public string[] Paths { get; set; }
        public RequestTemplate CurrentTemplate { get; set; } = new();
        public ObservableCollection<RequestTemplate> Templates { get; set; } = new();
        public ObservableCollection<TechItem> TechItems { get; set; } = new();

        public RequestWindow(string[] paths)
        {
            InitializeComponent();
            Paths = paths;
            DataContext = this;
            Templates = MainWindow.M.Templates;
        }

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

            RequestTemplate _template = new()
            {
                Name = name,
                DestinyPattern = CurrentTemplate.DestinyPattern,
                CountPattern = CurrentTemplate.CountPattern,
                PosDestiny = CurrentTemplate.PosDestiny,
                PosCount = CurrentTemplate.PosCount
            };

            

            Templates.Add(_template);
        }

        private void Template_Selected(object sender, SelectionChangedEventArgs e)
        {
            if (TemplatesList.SelectedItem is RequestTemplate template) CurrentTemplate = template;
        }

        private void Analyze_Paths(object sender, RoutedEventArgs e) { Analyze_Paths(); }
        private void Analyze_Paths()
        {
            if (Paths.Length == 0) return;

            if (TechItems.Count > 0) TechItems.Clear();

            foreach (string path in Paths)
            {
                TechItem techItem = new() { NumberName = Path.GetFileNameWithoutExtension(path) };

                //определяем материал
                foreach (Metal metal in MainWindow.M.Metals)
                    if (metal.Name != null && path.Contains(metal.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        techItem.Material = metal.Name;
                        break;
                    }

                //определяем толщину
                string destinyPattern;
                if (CurrentTemplate.PosDestiny) destinyPattern = $@"{Regex.Escape(CurrentTemplate.DestinyPattern)}\s*([\d.]+)";
                else destinyPattern = $@"([\d.]+)\s*{Regex.Escape(CurrentTemplate.DestinyPattern)}";

                Match matchDestiny = Regex.Match(path, destinyPattern);
                if (matchDestiny.Success) techItem.Destiny = matchDestiny.Groups[1].Value;

                //определяем количество
                string countPattern;
                if (CurrentTemplate.PosCount) countPattern = $@"{Regex.Escape(CurrentTemplate.CountPattern)}\s*(\d+)";
                else countPattern = $@"(\d+)\s*{Regex.Escape(CurrentTemplate.CountPattern)}";

                Match matchCount = Regex.Match(path, countPattern);
                if (matchCount.Success) techItem.Count = matchCount.Groups[1].Value;

                //записываем путь к файлу
                techItem.DxfPath = path;

                TechItems.Add(techItem);
            }

            if (TechItems.Count > 0) MainWindow.M.StatusBegin("Файлы успешно проанализированы");
        }

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

            //копируем файлы с новыми именами
            foreach (TechItem techItem in TechItems)
            {
                if (techItem.DxfPath is null || !File.Exists(techItem.DxfPath)) continue;

                string? directory = Path.GetDirectoryName(techItem.DxfPath);
                if (directory != null)
                {
                    bool success = RenameFile(techItem.DxfPath, techItem.NumberName + Path.GetExtension(techItem.DxfPath));
                    if (!success) MessageBox.Show("Переименование завершилось с ошибкой.");
                    else techItem.DxfPath = Path.Combine(directory, techItem.NumberName + Path.GetExtension(techItem.DxfPath));
                }
            }

            RequestGrid.ItemsSource = null;
            RequestGrid.ItemsSource = TechItems;
            MainWindow.M.StatusBegin("Файлы скопированы с новыми именами");
        }

        private void Create_Request(object sender, RoutedEventArgs e) { Create_Request(); }
        private void Create_Request()
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet requestsheet = workbook.Workbook.Worksheets.Add($"Заявка Лазер");

            //оформляем статичные ячейки по умолчанию
            requestsheet.Cells[1, 1].Value = "Расшифровка работ: гиб - гибка, вальц - вальцовка, зен - зенковка, рез - резьба," +
                " свар - сварка,\nокр - окраска, оц - оцинковка, грав - гравировка";
            requestsheet.Cells[1, 1, 1, 8].Merge = true;
            requestsheet.Cells[1, 1, 1, 8].Style.WrapText = true;
            requestsheet.Cells[1, 1, 1, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            requestsheet.Cells[1, 1, 1, 8].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            requestsheet.Row(1).Height = 30;
            requestsheet.Cells[TechItems.Count + 3, 5].Value = "Кол-во комплектов";
            requestsheet.Cells[TechItems.Count + 3, 6].Value = 1;
            requestsheet.Cells[TechItems.Count + 3, 6].Style.Font.Color.SetColor(System.Drawing.Color.Red);
            requestsheet.Cells[TechItems.Count + 3, 5, TechItems.Count + 3, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            requestsheet.Cells[TechItems.Count + 3, 5, TechItems.Count + 3, 6].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            requestsheet.Cells[TechItems.Count + 3, 5].Style.Border.Right.Style = ExcelBorderStyle.Thin;
            requestsheet.Cells[TechItems.Count + 3, 5, TechItems.Count + 3, 6].Style.Border.BorderAround(ExcelBorderStyle.Medium);

            //устанавливаем заголовки таблицы
            List<string> _heads = new() { "№", "№ чертежа", "Размеры", "Металл", "Толщина", "Кол-во деталей", "Маршрут", "Давальч" };
            for (int head = 0; head < _heads.Count; head++) requestsheet.Cells[2, head + 1].Value = _heads[head];

            string message = "";
            for (int i = 0; i < TechItems.Count; i++)
            {
                requestsheet.Cells[i + 3, 1].Value = i + 1;
                requestsheet.Cells[i + 3, 2].Value = TechItems[i].NumberName;
                requestsheet.Cells[i + 3, 3].Value = MainWindow.GetSizes(TechItems[i].DxfPath);
                if ($"{requestsheet.Cells[i + 3, 3].Value}" == "") message += $"\n{requestsheet.Cells[i + 3, 2].Value}";
                requestsheet.Cells[i + 3, 4].Value = TechItems[i].Material;
                requestsheet.Cells[i + 3, 5].Value = TechItems[i].Destiny;
                requestsheet.Cells[i + 3, 6].Value = TechItems[i].Count;
                requestsheet.Cells[i + 3, 7].Value = TechItems[i].Route;
                requestsheet.Cells[i + 3, 8].Value = TechItems[i].HasMaterial;
            }
            if (message != "") MessageBox.Show(message.Insert(0, "Не удалось получить габариты следующих деталей:"));

            ExcelRange details = requestsheet.Cells[2, 1, TechItems.Count + 2, 8];     //получаем таблицу деталей для оформления

            //обводка границ и авторастягивание столбцов
            details.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            details.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            details.Style.Border.Right.Style = details.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            details.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            requestsheet.Cells.AutoFitColumns();
            requestsheet.Cells[2, 1, 2, 8].Style.WrapText = true;
            requestsheet.Cells[2, 1, 2, 8].Style.Font.Bold = true;

            //устанавливаем настройки для печати, чтобы сохранение в формате .pdf выводило весь документ по ширине страницы
            requestsheet.PrinterSettings.FitToPage = true;
            requestsheet.PrinterSettings.FitToWidth = 1;
            requestsheet.PrinterSettings.FitToHeight = 0;
            requestsheet.PrinterSettings.HorizontalCentered = true;

            //сохраняем книгу в файл Excel
            workbook.SaveAs($"{Path.GetDirectoryName(Paths[0])}\\Заявка Лазер.xlsx");

            MainWindow.M.StatusBegin($"Создана заявка в папке {Path.GetDirectoryName(Paths[0])}");

            //Process.Start("explorer.exe", $"{Path.GetDirectoryName(Paths[0])}\\Заявка Лазер.xlsx");
        }

        public static bool RenameFile(string oldPath, string newName, bool copy = true)
        {
            try
            {
                // 1. Проверка существования исходного файла
                if (!File.Exists(oldPath))
                {
                    MessageBox.Show("Ошибка: Исходный файл не существует.");
                    return false;
                }

                // 2. Получение пути к каталогу исходного файла
                string? directory = Path.GetDirectoryName(oldPath);
                if (string.IsNullOrEmpty(directory))
                {
                    MessageBox.Show("Ошибка: Не удалось определить каталог исходного файла.");
                    return false;
                }

                // 3. Формирование полного пути для нового имени файла
                string newPath = Path.Combine(directory, newName);

                // 4. Проверка корректности нового имени файла
                if (string.IsNullOrWhiteSpace(newName) || Path.GetFileName(newName) != newName)
                {
                    MessageBox.Show("Ошибка: Новое имя файла некорректно.");
                    return false;
                }

                // 5. Проверка, что новый файл не перезапишет существующий
                if (File.Exists(newPath))
                {
                    MessageBox.Show($"Ошибка: Файл с именем '{newName}' уже существует.");
                    return false;
                }

                // 6.Попытка переименовать файл
                if (copy) File.Copy(oldPath, newPath);
                else File.Move(oldPath, newPath);

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Ошибка: Недостаточно прав для выполнения операции.");
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Ошибка ввода/вывода: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Неожиданная ошибка: {ex.Message}");
            }

            return false;
        }

        private void DataGrid_AutoGeneratedColumn(object sender, EventArgs e)
        {

        }

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
        }

        private void RequestGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {

        }

        private void Create_Tech(object sender, RoutedEventArgs e)
        {
            if (!File.Exists($"{Path.GetDirectoryName(Paths[0])}\\Заявка Лазер.xlsx"))
            {
                MessageBox.Show("Не удалось найти подходящую заявку для анализа.");
                return;
            }

            Tech tech = new($"{Path.GetDirectoryName(Paths[0])}\\Заявка Лазер.xlsx");
            MainWindow.M.StatusBegin(tech.Run());
        }
    }

    public class RequestTemplate : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

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