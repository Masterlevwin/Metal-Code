using Metal_Code.Models;
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

        // ⭐ НОВОЕ: Итоговая сумма всех расчётов
        public float TotalAmountSum { get; private set; }

        // Сводка по менеджерам
        private List<dynamic> _managersSummary = new();
        public List<dynamic> ManagersSummary
        {
            get => _managersSummary;
            private set { _managersSummary = value; OnPropertyChanged(nameof(ManagersSummary)); }
        }

        // Итого по всем менеджерам
        public float TotalWorksSum { get; private set; }
        public float TotalPercent { get; private set; }
        public float TotalBonus { get; private set; }

        // Команды
        public RelayCommand ExportCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand DeleteCommand { get; }

        private readonly DateTime _from;
        private readonly DateTime _to;
        private readonly ReportType _reportType;

        public string DateColumnHeader => _reportType == ReportType.Sales ? "Отгружен" : "Создан";

        private string ReportTitle => _reportType switch
        {
            ReportType.Sales => "Отчет по продажам",
            ReportType.Production => "Отчет по производству",
            _ => "Отчет"
        };

        private string ReportSheetName => _reportType switch
        {
            ReportType.Sales => "Отчет по продажам",
            ReportType.Production => "В производстве",
            _ => "Отчет"
        };

        public ReportPreviewWindow(DateTime from, DateTime to, ReportType reportType = ReportType.Sales)
        {
            InitializeComponent();
            DataContext = this;

            _from = from;
            _to = to;
            _reportType = reportType;

            Title = $"{ReportTitle} за {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";

            DateColumn.Header = DateColumnHeader;

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

            _ = LoadReportAsync();
        }

        private async System.Threading.Tasks.Task LoadReportAsync()
        {
            try
            {
                string statusText = _reportType == ReportType.Sales
                    ? "Формирование отчета по продажам..."
                    : "Формирование отчета по производству...";

                MainWindow.M.StatusBegin(statusText, MainWindow.StatusMessageType.Info);

                bool isAdmin = MainWindow.M.CurrentManager.IsAdmin;
                string managerName = MainWindow.M.CurrentManager.Name ?? "";

                List<Offer> offers;
                if (_reportType == ReportType.Sales)
                    offers = await MainWindow.M.DataService.GetSalesReportAsync(_from, _to, isAdmin, managerName);
                else
                    offers = await MainWindow.M.DataService.GetProductionReportAsync(_from, _to, isAdmin, managerName);

                if (offers.Count == 0)
                {
                    string emptyMessage = _reportType == ReportType.Sales
                        ? "За выбранный период отгруженных расчётов не найдено"
                        : "Расчётов в производстве не найдено";

                    MainWindow.M.StatusBegin(emptyMessage, MainWindow.StatusMessageType.Warning);
                    Items = new ObservableCollection<OfferReportPreviewItem>();
                    RecalculateTotals();
                    return;
                }

                if (_reportType == ReportType.Production)
                {
                    var dates = offers
                        .Where(o => o.CreatedDate.HasValue)
                        .Select(o => o.CreatedDate!.Value.ToLocalTime().Date)
                        .ToList();

                    if (dates.Any())
                    {
                        DateTime minDate = dates.Min();
                        DateTime maxDate = dates.Max();
                        Title = PeriodText = $"расчёты с {minDate:dd.MM.yyyy} по {maxDate:dd.MM.yyyy} ({offers.Count} шт.)";
                    }
                    else
                    {
                        Title = PeriodText = $"все расчёты ({offers.Count} шт.)";
                    }
                }

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
                        CreatedDate = offer.CreatedDate?.ToLocalTime(),
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
                                // Сохраняем коэффициенты для отображения
                                item.RatioValue = (float)product.Ratio;
                                item.BonusRatioValue = product.BonusRatio;

                                // ⭐ ВЫЧИСЛЯЕМ БОНУС В РУБЛЯХ ПРАВИЛЬНО
                                // Бонус = (базовые материалы + базовые работы - доставка) × BonusRatio / 100
                                float baseSum = offer.Material + offer.Services - product.Delivery * product.DeliveryRatio;
                                float bonusAmount = baseSum * item.BonusRatioValue / 100f;
                                item.BonusAmount = Math.Max(0, bonusAmount);

                                // Считаем детализированные работы с итоговым коэффициентом
                                ExtractWorkCosts(item, product);

                                // Применяем итоговый коэффициент к материалам
                                float ratio = item.RatioValue * (1 + item.BonusRatioValue / 100);
                                item.MaterialAmount *= ratio;
                            }
                            else
                                skippedCount++;
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

                var view = CollectionViewSource.GetDefaultView(Items);
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(OfferReportPreviewItem.ManagerName)));
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(nameof(OfferReportPreviewItem.ManagerName), ListSortDirection.Ascending));

                string dateProperty = _reportType == ReportType.Sales
                    ? nameof(OfferReportPreviewItem.EndDate)
                    : nameof(OfferReportPreviewItem.CreatedDate);
                view.SortDescriptions.Add(new SortDescription(dateProperty, ListSortDirection.Descending));

                string status = skippedCount > 0
                    ? $"{ReportTitle} сформирован: {previewItems.Count} расчётов ({skippedCount} с ошибками данных)"
                    : $"{ReportTitle} сформирован: {previewItems.Count} расчётов";

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
                TotalWorksSum = TotalPercent = TotalBonus = 0;
                TotalAmountSum = 0;
                ManagersSummary = new List<dynamic>();
            }
            else
            {
                TotalMaterial = Items.Sum(i => i.MaterialAmount);
                TotalLaser = Items.Sum(i => i.LaserCost);
                TotalBending = Items.Sum(i => i.BendingCost);
                TotalPipe = Items.Sum(i => i.PipeCost);
                TotalProduction = Items.Sum(i => i.ProductionCost);

                TotalAmountSum = Items.Sum(i => i.TotalAmount);

                // ⭐ Сводка по менеджерам
                float planTarget = 7_000_000f;
                float planBonus = 100_000f;

                var summary = Items
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.ManagerName) ? "Без ответственного" : i.ManagerName)
                    .Select(g =>
                    {
                        float totalWorks = g.Sum(i => i.TotalWorks);
                        float totalAmount = g.Sum(i => i.TotalAmount);
                        float percent = totalWorks / planTarget * 100f;

                        // ✅ ИСПРАВЛЕНО: убран Math.Min, теперь премия растет пропорционально при перевыполнении
                        float bonus = planBonus * percent / 100f;

                        return new
                        {
                            ManagerName = g.Key,
                            TotalWorks = totalWorks,
                            TotalAmount = totalAmount,
                            Percent = percent,
                            Bonus = bonus
                        };
                    })
                    .OrderByDescending(m => m.Percent)
                    .ToList<dynamic>();

                ManagersSummary = summary;

                TotalWorksSum = Items.Sum(i => i.TotalWorks);
                TotalPercent = TotalWorksSum / planTarget * 100f;

                // ✅ ИСПРАВЛЕНО: убран Math.Min для общей суммы премии
                TotalBonus = planBonus * TotalPercent / 100f;
            }

            OnPropertyChanged(nameof(TotalMaterial));
            OnPropertyChanged(nameof(TotalLaser));
            OnPropertyChanged(nameof(TotalBending));
            OnPropertyChanged(nameof(TotalPipe));
            OnPropertyChanged(nameof(TotalProduction));
            OnPropertyChanged(nameof(TotalWorks));
            OnPropertyChanged(nameof(TotalAmountSum));
            OnPropertyChanged(nameof(TotalWorksSum));
            OnPropertyChanged(nameof(TotalPercent));
            OnPropertyChanged(nameof(TotalBonus));
        }

        private static void ExtractWorkCosts(OfferReportPreviewItem item, Product product)
        {
            float ratio = item.RatioValue * (1 + item.BonusRatioValue / 100);

            var workKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Лазерная резка", 51 }, { "Гибка", 52 }, { "Сварка", 53 }, { "Окраска", 54 },
                { "Резьба", 55 }, { "Зенковка", 56 }, { "Сверловка", 57 }, { "Вальцовка", 58 },
                { "Доп работа П", 59 }, { "Доп работа Л", 60 }, { "Труборез", 61 }, { "Лентопил", 61 },
                { "Фрезеровка", 64 }, { "Заклепки", 65 }, { "Аквабластинг", 66 }, { "Цинкование", 67 },
            };

            var dedupWorkNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Резьба", "Зенковка", "Сверловка", "Заклепки", "Гибка", "Окраска", "Доп работа Л", "Доп работа П",
            };

            if (product.Assemblies != null && product.Assemblies.Count > 0)
            {
                foreach (var assembly in product.Assemblies)
                {
                    if (assembly == null) continue;
                    if (assembly.WeldPrice > 0) item.ProductionCost += assembly.WeldPrice;
                    if (assembly.PaintPrice > 0) item.ProductionCost += assembly.PaintPrice;
                }
            }

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

                    var processedWorks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var work in td.Works)
                    {
                        var workName = work.NameWork?.Trim();
                        if (string.IsNullOrWhiteSpace(workName) || !workKeys.TryGetValue(workName, out int key))
                            continue;

                        if (dedupWorkNames.Contains(workName))
                        {
                            if (processedWorks.Contains(workName)) continue;
                            processedWorks.Add(workName);
                        }

                        float cost = ExtractWorkCost(contextParts, key) * ratio;

                        switch (workName)
                        {
                            case var n when n.Equals("Лазерная резка", StringComparison.OrdinalIgnoreCase):
                                item.LaserCost += cost; break;
                            case var n when n.Equals("Гибка", StringComparison.OrdinalIgnoreCase):
                                item.BendingCost += cost; break;
                            case var n when n.Equals("Труборез", StringComparison.OrdinalIgnoreCase):
                                item.PipeCost += cost; break;
                            case var n when n.Equals("Лентопил", StringComparison.OrdinalIgnoreCase):
                                item.PipeCost += cost; break;
                            default:
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
            string fileName = _reportType == ReportType.Sales
                ? $"Отчет_продаж_{_from:yyyy-MM-dd}_{_to:yyyy-MM-dd}"
                : $"Отчет_производство_{DateTime.Now:yyyy-MM-dd}";

            var dlg = new SaveFileDialog
            {
                FileName = fileName,
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
            var ws = package.Workbook.Worksheets.Add(ReportSheetName);

            string headerText = _reportType == ReportType.Sales
                ? $"{ReportTitle} за период {PeriodText}"
                : $"{ReportTitle}: {PeriodText}";

            ws.Cells[1, 1].Value = headerText;
            ws.Cells[1, 1, 1, 17].Merge = true;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // ⭐ Заголовки: добавлена колонка "Бонус ₽"
            var headers = new[]
            {
                "№", "Компания", "Сумма", "Нал", "Счёт", "Заказ",
                _reportType == ReportType.Sales ? "Отгружен" : "Создан",
                "Автор", "Коэфф.", "Бонус %", "Бонус ₽",
                "Материал", "Лазер", "Гибка", "Труборез", "Производство", "Всего работ"
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
                float groupAmountSum = groupList.Sum(i => i.TotalAmount); // ⭐ НОВОЕ

                // ⭐ Обновлённый заголовок группы
                ws.Cells[row, 1].Value = $"👤 {group.Key} ({groupList.Count} расчётов) | Работы: {groupWorksSum:N2} ₽ из Всего: {groupAmountSum:N2} ₽";
                ws.Cells[row, 1, row, 17].Merge = true;
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(230, 230, 240));
                row++;

                foreach (var item in groupList.OrderBy(i => i.DisplayDate))
                {
                    ws.Cells[row, 1].Value = item.Number;
                    ws.Cells[row, 2].Value = item.Company;
                    ws.Cells[row, 3].Value = item.TotalAmount;
                    ws.Cells[row, 4].Value = item.IsCash ? "ИП/ПК" : "ООО";
                    ws.Cells[row, 5].Value = item.Invoice;
                    ws.Cells[row, 6].Value = item.Order;
                    ws.Cells[row, 7].Value = item.DisplayDate?.ToString("dd.MM.yyyy");
                    ws.Cells[row, 8].Value = item.Author;

                    ws.Cells[row, 9].Value = item.RatioValue;
                    ws.Cells[row, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[row, 9].Style.Font.Bold = true;
                    ws.Cells[row, 9].Style.Font.Color.SetColor(System.Drawing.Color.DarkSlateBlue);

                    ws.Cells[row, 10].Value = item.BonusRatioValue / 100;
                    ws.Cells[row, 10].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[row, 10].Style.Font.Color.SetColor(System.Drawing.Color.DarkOrange);

                    // ⭐ НОВАЯ колонка 11: Бонус ₽
                    ws.Cells[row, 11].Value = item.BonusAmount;
                    ws.Cells[row, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    ws.Cells[row, 11].Style.Font.Bold = true;
                    ws.Cells[row, 11].Style.Font.Color.SetColor(System.Drawing.Color.DarkOrange);

                    ws.Cells[row, 12].Value = item.MaterialAmount;
                    ws.Cells[row, 13].Value = item.LaserCost;
                    ws.Cells[row, 14].Value = item.BendingCost;
                    ws.Cells[row, 15].Value = item.PipeCost;
                    ws.Cells[row, 16].Value = item.ProductionCost;
                    ws.Cells[row, 17].Value = item.TotalWorks;
                    row++;
                }
                row++;
            }

            // Итоговая строка
            ws.Cells[row, 1].Value = "📈 ИТОГО:";
            ws.Cells[row, 1, row, 2].Merge = true;  // ⭐ Объединяем только 1-2, чтобы колонка 3 была свободна
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 12;

            // ⭐ Итоговая сумма всех расчётов в колонке "Сумма" (колонка 3)
            ws.Cells[row, 3].Value = TotalAmountSum;
            ws.Cells[row, 3].Style.Font.Bold = true;
            ws.Cells[row, 3].Style.Font.Size = 12;
            ws.Cells[row, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            ws.Cells[row, 12].Value = TotalMaterial;
            ws.Cells[row, 13].Value = TotalLaser;
            ws.Cells[row, 14].Value = TotalBending;
            ws.Cells[row, 15].Value = TotalPipe;
            ws.Cells[row, 16].Value = TotalProduction;
            ws.Cells[row, 17].Value = TotalWorks;

            // ⭐ Золотой фон для колонки 3 (Сумма)
            var amountTotalRange = ws.Cells[row, 3, row, 3];
            amountTotalRange.Style.Font.Bold = true;
            amountTotalRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            amountTotalRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 215, 0));
            amountTotalRange.Style.Numberformat.Format = "# ### ##0.00 ₽";

            // ⭐ Золотой фон для колонок 12-17 (Материал...Всего работ)
            var totalsRange = ws.Cells[row, 12, row, 17];
            totalsRange.Style.Font.Bold = true;
            totalsRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            totalsRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 215, 0));
            totalsRange.Style.Numberformat.Format = "# ### ##0.00 ₽";

            var moneyCols = new[] { 3, 11, 12, 13, 14, 15, 16, 17 };
            foreach (var col in moneyCols)
            {
                ws.Cells[4, col, row, col].Style.Numberformat.Format = "# ### ##0.00 ₽";
            }

            ws.Cells[4, 9, row, 9].Style.Numberformat.Format = "0.00";
            ws.Cells[4, 10, row, 10].Style.Numberformat.Format = "0%";

            ws.Cells[1, 1, row, 17].AutoFitColumns();
            ws.View.FreezePanes(4, 1);

            // ⭐ СВОДНАЯ ТАБЛИЦА ПО МЕНЕДЖЕРАМ
            row += 2;

            float planTarget = 7_000_000f;
            float planBonus = 100_000f;

            ws.Cells[row, 1].Value = "📊 Выполнение плана";
            ws.Cells[row, 1, row, 5].Merge = true;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 13;
            row++;

            ws.Cells[row, 1].Value = $"План услуг: {planTarget:N0} ₽ | Макс. премия: {planBonus:N0} ₽";
            ws.Cells[row, 1, row, 5].Merge = true;
            ws.Cells[row, 1].Style.Font.Italic = true;
            ws.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
            row++;

            var summaryHeaders = new[] { "Менеджер", "Расчётов", "Работы", "План %", "Премия" };
            ws.Cells[row, 1].LoadFromArrays(new[] { summaryHeaders });
            var summaryHeaderRange = ws.Cells[row, 1, row, summaryHeaders.Length];
            summaryHeaderRange.Style.Font.Bold = true;
            summaryHeaderRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            summaryHeaderRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            summaryHeaderRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            row++;

            var managersSummary = Items
                .GroupBy(i => string.IsNullOrWhiteSpace(i.ManagerName) ? "Без ответственного" : i.ManagerName)
                .Select(g =>
                {
                    float totalWorks = g.Sum(i => i.TotalWorks);
                    float percent = totalWorks / planTarget * 100f;
                    float bonus = planBonus * percent / 100f;
                    return new
                    {
                        Name = g.Key,
                        Count = g.Count(),
                        TotalWorks = totalWorks,
                        Percent = percent,
                        Bonus = bonus
                    };
                })
                .OrderByDescending(m => m.Percent)
                .ToList();

            int summaryStartRow = row;
            foreach (var m in managersSummary)
            {
                ws.Cells[row, 1].Value = m.Name;
                ws.Cells[row, 1].Style.Font.Bold = true;

                ws.Cells[row, 2].Value = m.Count;
                ws.Cells[row, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                ws.Cells[row, 3].Value = m.TotalWorks;
                ws.Cells[row, 3].Style.Numberformat.Format = "# ### ##0.00 ₽";
                ws.Cells[row, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[row, 3].Style.Font.Color.SetColor(System.Drawing.Color.DarkGreen);

                ws.Cells[row, 4].Value = m.Percent / 100f;
                ws.Cells[row, 4].Style.Numberformat.Format = "0.0%";
                ws.Cells[row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[row, 4].Style.Font.Bold = true;

                ws.Cells[row, 5].Value = m.Bonus;
                ws.Cells[row, 5].Style.Numberformat.Format = "# ### ##0.00 ₽";
                ws.Cells[row, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[row, 5].Style.Font.Bold = true;
                ws.Cells[row, 5].Style.Font.Color.SetColor(System.Drawing.Color.DarkGreen);

                row++;
            }

            float totalWorksAll = managersSummary.Sum(m => m.TotalWorks);
            float totalPercentAll = totalWorksAll / planTarget * 100f;
            float totalBonusAll = planBonus * totalPercentAll / 100f;

            ws.Cells[row, 1].Value = "ИТОГО:";
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 12;

            ws.Cells[row, 2].Value = managersSummary.Sum(m => m.Count);
            ws.Cells[row, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 2].Style.Font.Bold = true;

            ws.Cells[row, 3].Value = totalWorksAll;
            ws.Cells[row, 3].Style.Numberformat.Format = "# ### ##0.00 ₽";
            ws.Cells[row, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[row, 3].Style.Font.Bold = true;
            ws.Cells[row, 3].Style.Font.Color.SetColor(System.Drawing.Color.DarkGreen);

            ws.Cells[row, 4].Value = totalPercentAll / 100f;
            ws.Cells[row, 4].Style.Numberformat.Format = "0.0%";
            ws.Cells[row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 4].Style.Font.Bold = true;

            ws.Cells[row, 5].Value = totalBonusAll;
            ws.Cells[row, 5].Style.Numberformat.Format = "# ### ##0.00 ₽";
            ws.Cells[row, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[row, 5].Style.Font.Bold = true;
            ws.Cells[row, 5].Style.Font.Color.SetColor(System.Drawing.Color.DarkGreen);

            var summaryTotalRange = ws.Cells[row, 1, row, 5];
            summaryTotalRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            summaryTotalRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 215, 0));

            var summaryDataRange = ws.Cells[summaryStartRow - 1, 1, row, 5];
            summaryDataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            summaryDataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            summaryDataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            summaryDataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            ws.Cells[summaryStartRow - 1, 1, row, 5].AutoFitColumns();

            package.SaveAs(new FileInfo(filePath));
        }
    }

    public class OfferReportPreviewItem
    {
        public string Number { get; set; } = null!;
        public string Company { get; set; } = null!;
        public float TotalAmount { get; set; }
        public float MaterialAmount { get; set; }
        public bool IsCash { get; set; }
        public string Invoice { get; set; } = null!;
        public string Order { get; set; } = null!;
        public DateTime? EndDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string Author { get; set; } = null!;
        public string ManagerName { get; set; } = null!;

        public float RatioValue { get; set; } = 1f;
        public float BonusRatioValue { get; set; } = 100f;
        public float BonusAmount { get; set; }

        public float LaserCost { get; set; }
        public float BendingCost { get; set; }
        public float PipeCost { get; set; }
        public float ProductionCost { get; set; }

        public float TotalWorks => LaserCost + BendingCost + PipeCost + ProductionCost;
        public DateTime? DisplayDate => EndDate ?? CreatedDate;
        public string GroupKey => !string.IsNullOrWhiteSpace(ManagerName)
            ? ManagerName
            : (!string.IsNullOrWhiteSpace(Author) ? Author : "Без ответственного");
    }

    public enum ReportType
    {
        Sales,
        Production
    }
}