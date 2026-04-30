using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Metal_Code
{
    public partial class ReportPreviewWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly string _connectionString;

        private ObservableCollection<OfferReportPreviewItem> _items = new();
        // 🔹 Данные
        public ObservableCollection<OfferReportPreviewItem> Items
        {
            get => _items;
            set { _items = value; OnPropertyChanged(nameof(Items)); }
        }

        // 🔹 Команды
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand DeleteCommand { get; }

        // 🔹 Свойства для итогов (с уведомлением)
        public float TotalMaterial {  get; private set; }
        public float TotalLaser { get; private set; }
        public float TotalBending { get; private set; }
        public float TotalPipe { get; private set; }
        public float TotalProduction { get; private set; }
        public float TotalWorks => TotalLaser + TotalBending + TotalPipe + TotalProduction;

        // 🔹 Метод пересчёта итогов
        private void RecalculateTotals()
        {
            if (Items == null)
            {
                TotalMaterial = TotalLaser = TotalBending = TotalPipe = TotalProduction = 0;
            }
            else
            {
                TotalMaterial = Items.Sum(i => i.MaterialAmount);
                TotalLaser = Items.Sum(i => i.LaserCost);
                TotalBending = Items.Sum(i => i.BendingCost);
                TotalPipe = Items.Sum(i => i.PipeCost);
                TotalProduction = Items.Sum(i => i.ProductionCost);
            }

            // 🔹 Уведомляем об изменении свойств
            OnPropertyChanged(nameof(TotalMaterial));
            OnPropertyChanged(nameof(TotalLaser));
            OnPropertyChanged(nameof(TotalBending));
            OnPropertyChanged(nameof(TotalPipe));
            OnPropertyChanged(nameof(TotalProduction));
            OnPropertyChanged(nameof(TotalWorks));
        }

        public ReportPreviewWindow(string connectionString)
        {
            InitializeComponent();
            DataContext = this;
            _connectionString = connectionString;

            RefreshCommand = new RelayCommand(_ => LoadDataAsync());
            ExportCommand = new RelayCommand(
                execute: _ => ExportToExcelWithDialog(),
                canExecute: _ => Items?.Count > 0);
            CloseCommand = new RelayCommand(_ => Close());
            DeleteCommand = new RelayCommand(
                execute: obj => RemoveItemFromReport(obj as OfferReportPreviewItem),
                canExecute: obj => obj is OfferReportPreviewItem);

            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            try
            {
                using var db = new ManagerContext(_connectionString);

                // 🎯 Логика: если после 14-го — текущий месяц, иначе — прошлый
                bool isAfter15th = DateTime.Today.Day > 14;
                var referenceMonth = isAfter15th ? DateTime.Today : DateTime.Today.AddMonths(-1);
                var startOfPeriod = new DateTime(referenceMonth.Year, referenceMonth.Month, 1);

                // 🔹 Базовый запрос
                var query = db.Offers
                    .AsNoTracking()
                    .Where(o => !string.IsNullOrWhiteSpace(o.Order) && o.EndDate >= startOfPeriod);

                // 🔹 🔥 КЛЮЧЕВОЕ: фильтр по текущему менеджеру, если он не админ
                if (MainWindow.M.CurrentManager.IsAdmin &&
                    (MainWindow.M.CurrentManager.Name == "Серых Михаил"
                    || MainWindow.M.CurrentManager.Name == "Сергеев Юрий"
                    || MainWindow.M.CurrentManager.Name == "Еремин Андрей"))
                {
                    MainWindow.M.StatusBegin("Отчет по всем менеджерам (режим администратора)", MainWindow.StatusMessageType.Info);
                }
                else
                {
                    query = query.Where(o => o.Manager != null && o.Manager.Name == MainWindow.M.CurrentManager.Name);
                    MainWindow.M.StatusBegin($"Отчет по заказам менеджера: {MainWindow.M.CurrentManager.Name}", MainWindow.StatusMessageType.Info);
                }


                // 1️. Загружаем только нужные поля с расширенным фильтром
                var rawData = await query
                    .Select(o => new
                    {
                        o.Id,
                        o.N,
                        o.Company,
                        o.Amount,
                        o.Material,
                        o.Agent,
                        o.Invoice,
                        o.Order,
                        o.EndDate,
                        o.Autor,
                        o.Data,
                        ManagerName = o.Manager != null ? o.Manager.Name : null
                    })
                    .ToListAsync();

                // 2️. Убираем дубликаты по ключу: Номер + Компания + Сумма + Заказ
                // Если дубли есть, берём самый свежий (по EndDate)
                var distinctData = rawData
                    .GroupBy(x => new { x.N, x.Company, x.Amount, x.Order })
                    .Select(g => g.OrderByDescending(x => x.EndDate).First())
                    .ToList();

                // 3️. Преобразуем в DTO для UI
                var previewItems = new List<OfferReportPreviewItem>();
                int skippedCount = 0;
                var skippedErrors = new List<string>();  // для отладки

                foreach (var r in distinctData)
                {
                    try
                    {
                        var item = new OfferReportPreviewItem
                        {
                            Number = r.N,
                            Company = r.Company,
                            TotalAmount = r.Amount,
                            MaterialAmount = r.Material,
                            IsCash = r.Agent,
                            Invoice = r.Invoice,
                            Order = r.Order,
                            EndDate = r.EndDate?.ToLocalTime(),
                            Author = r.Autor,
                            ManagerName = r.ManagerName
                        };
                        // 🔹 Парсинг Data с защитой от сбоев
                        if (!string.IsNullOrWhiteSpace(r.Data))
                        {
                            var product = MainWindow.OpenOfferDataSafe(r.Data, out var error);

                            if (product != null)
                            {
                                // ✅ Успешно — извлекаем стоимости работ
                                ExtractWorkCosts(item, product);
                            }
                            else if (error != null)
                            {
                                // ❌ Ошибка десериализации — логируем и пропускаем
                                skippedCount++;
                                skippedErrors.Add($"#{r.Id} [{r.N}]: {error}");
                            }
                        }

                        previewItems.Add(item);
                    }
                    catch (Exception ex)
                    {
                        // 🔹 Катастрофическая ошибка в одной записи — тоже пропускаем
                        skippedCount++;
                        skippedErrors.Add($"#{r.Id} [{r.N}]: {ex.GetBaseException().Message}");
                        continue;
                    }
                }

                // 🔹 Показываем статистику пропущенных (только если их много)
                if (skippedCount > 0)
                {
                    string summary = $"Загружено: {previewItems.Count}, пропущено: {skippedCount}";
                    if (skippedCount <= 5)
                    {
                        // Показать детали, если ошибок мало
                        summary += "\n\nПропущенные записи:\n" + string.Join("\n", skippedErrors);
                    }
                    MessageBox.Show(this, summary, "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                Items = new ObservableCollection<OfferReportPreviewItem>(previewItems);
                RecalculateTotals();

                // Группировка и сортировка
                var view = CollectionViewSource.GetDefaultView(Items);
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(OfferReportPreviewItem.GroupKey)));
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(nameof(OfferReportPreviewItem.GroupKey), ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription(nameof(OfferReportPreviewItem.EndDate), ListSortDirection.Descending));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Критическая ошибка загрузки: {ex.GetBaseException().Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ExtractWorkCosts(OfferReportPreviewItem item, Product product)
        {
            // Словарь: тип работы → ключ в PropsDict
            var workKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        { "Лазерная резка", 51 },
        { "Гибка", 52 },
        { "Сварка", 53 },
        { "Окраска", 54 },
        { "Труборез", 61 },
    };

            foreach (var detail in product.Details)
            {
                foreach (var td in detail.TypeDetails)
                {
                    // 🔹 1. Предварительно собираем ВСЕ уникальные части из этого TypeDetail
                    // (из работ, у которых есть список Parts — обычно это Лазер/Труборез)
                    var contextParts = new HashSet<Part>();
                    foreach (var w in td.Works)
                    {
                        if (w.Parts?.Any() == true)
                        {
                            foreach (var p in w.Parts)
                                if (p != null) contextParts.Add(p);
                        }
                    }

                    // 🔹 2. Обрабатываем каждую работу в контексте
                    foreach (var work in td.Works)
                    {
                        var workName = work.NameWork?.Trim();
                        if (string.IsNullOrWhiteSpace(workName) || !workKeys.TryGetValue(workName, out int key))
                            continue;

                        float cost = ExtractWorkCost(contextParts, key);

                        // 🔹 4. Распределяем стоимость по свойствам DTO
                        switch (workName)
                        {
                            case var n when n.Equals("Лазерная резка", StringComparison.OrdinalIgnoreCase):
                                item.LaserCost += cost; break;
                            case var n when n.Equals("Гибка", StringComparison.OrdinalIgnoreCase):
                                item.BendingCost += cost; break;
                            case var n when n.Equals("Труборез", StringComparison.OrdinalIgnoreCase):
                                item.PipeCost += cost; break;
                            case var n when n.Equals("Сварка", StringComparison.OrdinalIgnoreCase):
                                item.ProductionCost += cost; break;
                            case var n when n.Equals("Окраска", StringComparison.OrdinalIgnoreCase):
                                item.ProductionCost += cost; break;
                        }
                    }
                }
            }
        }

        private static float ExtractWorkCost(IEnumerable<Part> parts, int propsDictKey)
        {
            if (parts == null) return 0;

            float total = 0;
            foreach (var part in parts)
            {
                if (part?.PropsDict?.TryGetValue(propsDictKey, out var values) != true || values is null || values.Count == 0)
                    continue;

                // 🔹 Используем ваш Parser, он уже должен обрабатывать форматы с пробелами/запятыми
                if (MainWindow.Parser(values[0]) is float price && price > 0)
                {
                    total += price * part.Count;
                }
            }
            return total;
        }


        /// <summary>
        /// Удаляет расчет из текущей выборки отчета (не затрагивает БД)
        /// </summary>
        private void RemoveItemFromReport(OfferReportPreviewItem? item)
        {
            if (item == null) return;

            var result = MessageBox.Show(
                this,
                $"Удалить расчет №{item.Number} из отчета?\n\nДанные в базе данных не изменятся.",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 🔹 Удаляем из коллекции (UI обновится автоматически)
                Items.Remove(item);

                // 🔹 Пересчитываем итоги
                RecalculateTotals();

                // 🔹 Обновляем представление (на случай, если группа стала пустой)
                if (Items.Count == 0) CollectionViewSource.GetDefaultView(Items).Refresh();
            }
        }

        /// <summary>
        /// Показывает диалог сохранения и запускает экспорт
        /// </summary>
        private void ExportToExcelWithDialog()
        {
            var dlg = new SaveFileDialog
            {
                FileName = $"Отчет_работы_{DateTime.Now:yyyy-MM-dd}",
                DefaultExt = ".xlsx",
                Filter = "Excel файлы (*.xlsx)|*.xlsx"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // 🔹 Показываем индикатор загрузки (как в главном окне)
                    if (Owner is Window owner)
                    {
                        // Если у главного окна есть методы анимации — используем их
                        // Или показываем простой MessageBox
                        MessageBox.Show(this, "Формирование файла...", "Экспорт",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    ExportToExcel(dlg.FileName);

                    MessageBox.Show(this, $"Отчет сохранён:\n{dlg.FileName}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Опционально: открыть файл после сохранения
                    // System.Diagnostics.Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Ошибка экспорта: {ex.GetBaseException().Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Экспортирует данные отчета в Excel-файл через EPPlus.
        /// Структура идентична таблице в окне предпросмотра.
        /// </summary>
        /// <param name="filePath">Путь для сохранения файла</param>
        public void ExportToExcel(string filePath)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Отчет по работам");

            // 🔹 1. Заголовки колонок (в том же порядке, что в DataGrid)
            var headers = new[]
            {
        "№", "Компания", "Сумма", "Нал", "Счёт", "Заказ", "Отгружен", "Автор", "Материал",
        "Лазер", "Гибка", "Труборез", "Производство", "Всего работ"
    };

            // Записываем заголовки
            ws.Cells[1, 1].LoadFromArrays(new[] { headers });

            // Стиль заголовков
            var headerRange = ws.Cells[1, 1, 1, headers.Length];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

            // 🔹 2. Группируем данные по менеджеру (как в UI)
            var groupedItems = Items
                .GroupBy(i => i.GroupKey)
                .OrderBy(g => g.Key)
                .ThenByDescending(g => g.Max(i => i.EndDate));

            int row = 2; // Начинаем со второй строки

            foreach (var group in groupedItems)
            {
                // 🔹 Заголовок группы (имя менеджера + статистика)
                var groupList = group.ToList();
                int count = groupList.Count;
                float groupWorksSum = groupList.Sum(i => i.LaserCost + i.BendingCost + i.PipeCost + i.ProductionCost);

                ws.Cells[row, 1].Value = $"👤 {group.Key}";
                ws.Cells[row, 1, row, 14].Merge = true;
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(230, 230, 240));
                ws.Cells[row, 1].Value = $"👤 {group.Key} → {count} расчет{(count % 10 == 1 && count % 100 != 11 ? "" : count % 10 >= 2 && count % 10 <= 4 && count % 100 != 12 && count % 100 != 14 ? "а" : "ов")} на сумму работ {groupWorksSum:C}";

                row++;

                // 🔹 Строки данных внутри группы
                foreach (var item in groupList.OrderBy(i => i.EndDate).ThenBy(i => i.Number))
                {
                    ws.Cells[row, 1].Value = item.Number;
                    ws.Cells[row, 2].Value = item.Company;
                    ws.Cells[row, 3].Value = item.TotalAmount;
                    ws.Cells[row, 4].Value = item.IsCash ? "ИП/ПК" : "ООО";
                    ws.Cells[row, 5].Value = item.Invoice;
                    ws.Cells[row, 6].Value = item.Order;
                    ws.Cells[row, 7].Value = item.EndDate?.ToString("dd.MM.yyyy");
                    ws.Cells[row, 8].Value = item.Author;
                    ws.Cells[row, 9].Value = item.MaterialAmount;
                    ws.Cells[row, 10].Value = item.LaserCost;
                    ws.Cells[row, 11].Value = item.BendingCost;
                    ws.Cells[row, 12].Value = item.PipeCost;
                    ws.Cells[row, 13].Value = item.ProductionCost;
                    ws.Cells[row, 14].Value = item.LaserCost + item.BendingCost + item.PipeCost + item.ProductionCost;

                    row++;
                }

                // Пустая строка между группами для визуального разделения
                ws.Cells[row, 1].Value = "";
                row++;
            }

            // 🔹 3. Итоговая строка (суммы по всем менеджерам)
            int lastDataRow = row - 1;
            ws.Cells[row, 1].Value = "📈 ИТОГО ПО ПРОИЗВОДСТВУ:";
            ws.Cells[row, 1, row, 8].Merge = true;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(44, 62, 80));
            ws.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);

            // Суммы работ с формулами (чтобы в Excel можно было пересчитать)
            ws.Cells[row, 9].Value = TotalMaterial;
            ws.Cells[row, 10].Value = TotalLaser;
            ws.Cells[row, 11].Value = TotalBending;
            ws.Cells[row, 12].Value = TotalPipe;
            ws.Cells[row, 13].Value = TotalProduction;
            ws.Cells[row, 14].Value = TotalWorks;

            // Стиль итоговой строки
            var totalsRange = ws.Cells[row, 9, row, 14];
            totalsRange.Style.Font.Bold = true;
            totalsRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            totalsRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 215, 0)); // Золотой
            totalsRange.Style.Numberformat.Format = "# ### ##0.00 ₽";
            totalsRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            // 🔹 4. Форматирование
            // Денежные колонки (3, 9-14)
            var moneyCols = new[] { 3, 9, 10, 11, 12, 13, 14 };
            foreach (var col in moneyCols)
            {
                var range = ws.Cells[2, col, lastDataRow, col];
                range.Style.Numberformat.Format = "# ### ##0.00 ₽";
                range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            }

            // Чекбокс "Нал" (колонка 4) — центрируем
            var cashRange = ws.Cells[2, 4, lastDataRow, 4];
            cashRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // Дата (колонка 7)
            var dateRange = ws.Cells[2, 7, lastDataRow, 7];
            dateRange.Style.Numberformat.Format = "dd.mm.yyyy";

            // 🔹 5. Автоширина колонок + фиксация шапки
            ws.Cells[1, 1, row, 14].AutoFitColumns();

            // Минимальная ширина для важных колонок
            ws.Column(1).Width = Math.Max(ws.Column(1).Width, 12);      // №
            ws.Column(2).Width = Math.Max(ws.Column(2).Width, 25);      // Компания
            ws.Column(3).Width = Math.Max(ws.Column(3).Width, 14);      // Сумма
            ws.Column(9).Width = Math.Max(ws.Column(9).Width, 12);      // Материал
            ws.Column(10).Width = Math.Max(ws.Column(10).Width, 12);    // Лазер
            ws.Column(11).Width = Math.Max(ws.Column(11).Width, 12);    // Гибка
            ws.Column(12).Width = Math.Max(ws.Column(12).Width, 12);    // Труборез
            ws.Column(13).Width = Math.Max(ws.Column(13).Width, 12);    // Производство
            ws.Column(14).Width = Math.Max(ws.Column(14).Width, 14);    // Всего работ

            // Заморозка шапки и первого столбца
            ws.View.FreezePanes(2, 1);

            // 🔹 6. Сохранение
            package.SaveAs(new FileInfo(filePath));
        }
    }

    public class OfferReportPreviewItem
    {
        // Основные поля
        public string Number { get; set; } = null!;
        public string Company { get; set; } = null!;
        public float TotalAmount { get; set; }
        public float MaterialAmount { get; set; }
        public bool IsCash { get; set; }
        public string Invoice { get; set; } = null!;
        public string Order { get; set; } = null!;
        public DateTime? EndDate { get; set; }
        public string Author { get; set; } = null!;
        public string ManagerName { get; set; } = null!;

        // 🔹 Поля для детализации работ
        public float LaserCost { get; set; }      // стоимость лазерной резки (51)
        public float BendingCost { get; set; }    // стоимость гибки (52)
        public float PipeCost { get; set; }       // стоимость трубореза (61)
        public float ProductionCost { get; set; } // стоимость производства (53 и 54)

        // Для группировки
        public string GroupKey => !string.IsNullOrWhiteSpace(ManagerName)
            ? ManagerName
            : (!string.IsNullOrWhiteSpace(Author) ? Author : "Без ответственного");
    }
}