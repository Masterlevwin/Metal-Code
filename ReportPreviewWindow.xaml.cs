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

        private ObservableCollection<OfferReportPreviewItem> _items = new();
        public ObservableCollection<OfferReportPreviewItem> Items
        {
            get => _items;
            set { _items = value; OnPropertyChanged(nameof(Items)); }
        }

        private string _periodText = "";
        public string PeriodText
        {
            get => _periodText;
            set { _periodText = value; OnPropertyChanged(nameof(PeriodText)); }
        }

        private string _modeText = "";
        public string ModeText
        {
            get => _modeText;
            set { _modeText = value; OnPropertyChanged(nameof(ModeText)); }
        }

        // Итоги
        public float TotalMaterial { get; private set; }
        public float TotalLaser { get; private set; }
        public float TotalBending { get; private set; }
        public float TotalPipe { get; private set; }
        public float TotalProduction { get; private set; }
        public float TotalWorks => TotalLaser + TotalBending + TotalPipe + TotalProduction;

        // Команды
        public RelayCommand ExportCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand DeleteCommand { get; }

        private readonly DateTime _from;
        private readonly DateTime _to;

        public ReportPreviewWindow(DateTime from, DateTime to)
        {
            InitializeComponent();
            DataContext = this;

            _from = from;
            _to = to;

            PeriodText = $"{from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
            ModeText = MainWindow.M.CurrentManager.IsAdmin
                ? "🔓 Режим администратора"
                : $"👤 {MainWindow.M.CurrentManager.Name}";

            ExportCommand = new RelayCommand(
                execute: _ => ExportToExcelWithDialog(),
                canExecute: _ => Items?.Count > 0);
            CloseCommand = new RelayCommand(_ => Close());
            DeleteCommand = new RelayCommand(
                execute: obj => RemoveItemFromReport(obj as OfferReportPreviewItem),
                canExecute: obj => obj is OfferReportPreviewItem);

            // ⭐ Загружаем данные через сервис
            _ = LoadReportAsync();
        }

        private async System.Threading.Tasks.Task LoadReportAsync()
        {
            try
            {
                MainWindow.M.StatusBegin("Формирование отчета...", MainWindow.StatusMessageType.Info);

                bool isAdmin = MainWindow.M.CurrentManager.IsAdmin;
                string managerName = MainWindow.M.CurrentManager.Name ?? "";

                // ⭐ Запрос к сервису (только PG!)
                var offers = await MainWindow.M.DataService.GetSalesReportAsync(_from, _to, isAdmin, managerName);

                if (offers.Count == 0)
                {
                    MainWindow.M.StatusBegin("За выбранного период расчётов не найдено", MainWindow.StatusMessageType.Warning);
                    Items = new ObservableCollection<OfferReportPreviewItem>();
                    RecalculateTotals();
                    return;
                }

                // ⭐ Десериализация и заполнение DTO
                var previewItems = new List<OfferReportPreviewItem>();
                int skippedCount = 0;

                foreach (var offer in offers)
                {
                    var item = new OfferReportPreviewItem
                    {
                        Number = offer.N,
                        Company = offer.Company,
                        TotalAmount = offer.Amount,
                        MaterialAmount = offer.Material,
                        IsCash = offer.Agent,
                        Invoice = offer.Invoice,
                        Order = offer.Order,
                        EndDate = offer.EndDate?.ToLocalTime(),
                        Author = offer.Autor,
                        ManagerName = offer.Manager?.Name ?? offer.Autor ?? "—"
                    };

                    if (!string.IsNullOrWhiteSpace(offer.Data))
                    {
                        try
                        {
                            var product = MainWindow.OpenOfferDataSafe(offer.Data, out _);
                            if (product != null)
                            {
                                ExtractWorkCosts(item, product);
                            }
                            else
                            {
                                skippedCount++;
                            }
                        }
                        catch
                        {
                            skippedCount++;
                        }
                    }

                    previewItems.Add(item);
                }

                Items = new ObservableCollection<OfferReportPreviewItem>(previewItems);
                RecalculateTotals();

                // Группировка по менеджеру
                var view = CollectionViewSource.GetDefaultView(Items);
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(OfferReportPreviewItem.ManagerName)));
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(nameof(OfferReportPreviewItem.ManagerName), ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription(nameof(OfferReportPreviewItem.EndDate), ListSortDirection.Descending));

                string status = skippedCount > 0
                    ? $"Отчет сформирован: {previewItems.Count} расчётов ({skippedCount} с ошибками данных)"
                    : $"Отчет сформирован: {previewItems.Count} расчётов";

                MainWindow.M.StatusBegin(status, MainWindow.StatusMessageType.Success);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка формирования отчета: {ex.GetBaseException().Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecalculateTotals()
        {
            if (Items == null || !Items.Any())
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

            OnPropertyChanged(nameof(TotalMaterial));
            OnPropertyChanged(nameof(TotalLaser));
            OnPropertyChanged(nameof(TotalBending));
            OnPropertyChanged(nameof(TotalPipe));
            OnPropertyChanged(nameof(TotalProduction));
            OnPropertyChanged(nameof(TotalWorks));
        }

        private static void ExtractWorkCosts(OfferReportPreviewItem item, Product product)
        {
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
                    var contextParts = new HashSet<Part>();
                    foreach (var w in td.Works)
                    {
                        if (w.Parts?.Any() == true)
                        {
                            foreach (var p in w.Parts)
                                if (p != null) contextParts.Add(p);
                        }
                    }

                    foreach (var work in td.Works)
                    {
                        var workName = work.NameWork?.Trim();
                        if (string.IsNullOrWhiteSpace(workName) || !workKeys.TryGetValue(workName, out int key))
                            continue;

                        float cost = ExtractWorkCost(contextParts, key);

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
                if (MainWindow.Parser(values[0]) is float price && price > 0)
                    total += price * part.Count;
            }
            return total;
        }

        private void RemoveItemFromReport(OfferReportPreviewItem? item)
        {
            if (item == null) return;
            if (MessageBox.Show(this, $"Удалить расчет №{item.Number} из отчета?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Items.Remove(item);
                RecalculateTotals();
            }
        }

        private void ExportToExcelWithDialog()
        {
            var dlg = new SaveFileDialog
            {
                FileName = $"Отчет_продаж_{_from:yyyy-MM-dd}_{_to:yyyy-MM-dd}",
                DefaultExt = ".xlsx",
                Filter = "Excel файлы (*.xlsx)|*.xlsx"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    ExportToExcel(dlg.FileName);
                    MessageBox.Show(this, $"Отчет сохранён:\n{dlg.FileName}", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Ошибка экспорта: {ex.GetBaseException().Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void ExportToExcel(string filePath)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Отчет по продажам");

            ws.Cells[1, 1].Value = $"Отчет по продажам за период {_from:dd.MM.yyyy} — {_to:dd.MM.yyyy}";
            ws.Cells[1, 1, 1, 14].Merge = true;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            var headers = new[]
            {
                "№", "Компания", "Сумма", "Нал", "Счёт", "Заказ", "Отгружен", "Автор", "Материал",
                "Лазер", "Гибка", "Труборез", "Производство", "Всего работ"
            };

            ws.Cells[3, 1].LoadFromArrays(new[] { headers });
            var headerRange = ws.Cells[3, 1, 3, headers.Length];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            var groupedItems = Items.GroupBy(i => i.ManagerName).OrderBy(g => g.Key);

            int row = 4;
            foreach (var group in groupedItems)
            {
                var groupList = group.ToList();
                float groupWorksSum = groupList.Sum(i => i.TotalWorks);

                ws.Cells[row, 1].Value = $"👤 {group.Key} ({groupList.Count} расчётов, работы: {groupWorksSum:N2} ₽)";
                ws.Cells[row, 1, row, 14].Merge = true;
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(230, 230, 240));
                row++;

                foreach (var item in groupList.OrderBy(i => i.EndDate))
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
                    ws.Cells[row, 14].Value = item.TotalWorks;
                    row++;
                }
                row++;
            }

            ws.Cells[row, 1].Value = "📈 ИТОГО:";
            ws.Cells[row, 1, row, 8].Merge = true;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 9].Value = TotalMaterial;
            ws.Cells[row, 10].Value = TotalLaser;
            ws.Cells[row, 11].Value = TotalBending;
            ws.Cells[row, 12].Value = TotalPipe;
            ws.Cells[row, 13].Value = TotalProduction;
            ws.Cells[row, 14].Value = TotalWorks;

            var totalsRange = ws.Cells[row, 9, row, 14];
            totalsRange.Style.Font.Bold = true;
            totalsRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            totalsRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 215, 0));
            totalsRange.Style.Numberformat.Format = "# ### ##0.00 ₽";

            var moneyCols = new[] { 3, 9, 10, 11, 12, 13, 14 };
            foreach (var col in moneyCols)
            {
                ws.Cells[4, col, row, col].Style.Numberformat.Format = "# ### ##0.00 ₽";
            }

            ws.Cells[1, 1, row, 14].AutoFitColumns();
            ws.View.FreezePanes(4, 1);
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

        // ⭐ Вычисляемое свойство: общая стоимость всех работ
        public float TotalWorks => LaserCost + BendingCost + PipeCost + ProductionCost;

        // Для группировки
        public string GroupKey => !string.IsNullOrWhiteSpace(ManagerName)
            ? ManagerName
            : (!string.IsNullOrWhiteSpace(Author) ? Author : "Без ответственного");
    }
}