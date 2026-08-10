using Metal_Code.Models;
using Metal_Code.Utils;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using IContainer = QuestPDF.Infrastructure.IContainer;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для SpecWindow.xaml
    /// </summary>
    public partial class SpecWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public readonly string OutputPath = string.Empty;

        private ProviderInfo? _selectedProvider;
        public ProviderInfo? SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (_selectedProvider != value)
                {
                    _selectedProvider = value;
                    OnPropertyChanged(nameof(SelectedProvider));
                    OnPropertyChanged(nameof(CurrentPrintImage));
                    OnPropertyChanged(nameof(CurrentSignatureImage));

                    // Синхронизируем строковое значение с CurrentTemplate для сохранения в БД
                    if (CurrentTemplate != null)
                        CurrentTemplate.Provider = value?.Name ?? string.Empty;
                }
            }
        }

        // Свойства для превью в UI
        public System.Windows.Media.ImageSource? CurrentPrintImage => GetWpfImage(SelectedProvider?.PrintResource);
        public System.Windows.Media.ImageSource? CurrentSignatureImage => GetWpfImage(SelectedProvider?.SignatureResource);

        public SpecTemplate CurrentTemplate { get; set; } = new();
        public Customer TargetCustomer { get; set; } = new();

        public SpecWindow(string outputPath)
        {
            InitializeComponent();

            OutputPath = outputPath;
            DataContext = this;
        }

        private void Loaded_Window(object sender, RoutedEventArgs e)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            TargetCustomer = MainWindow.M.TargetCustomer;
            CurrentTemplate = TargetCustomer.SpecTemplate;

            SelectedProvider = ProviderRegistry.GetByName(CurrentTemplate.Provider)
                            ?? ProviderRegistry.GetByFlags(MainWindow.M.IsLaser, MainWindow.M.IsAgent)
                            ?? ProviderRegistry.Providers.FirstOrDefault();

            Agent.Text = TargetCustomer.Agent ? "ИП" : "ООО";
            EndDate.Text = $"до {MainWindow.M.EndDate()?.ToString("d")}";
            Delivery.Text = MainWindow.M.HasDelivery is not false ?
                $"Доставка производится силами Поставщика до склада Покупателя, расположенного по адресу: {TargetCustomer.Address}."
                : "Cамовывоз со склада Поставщика по адресу: Ленинградская область, Всеволожский район, Колтуши, деревня Мяглово, ул. Дорожная, уч. 4Б.";

            if (CurrentTemplate.CustomFields == null)
                CurrentTemplate.CustomFields = new ObservableCollection<CustomSpecField>();

            var tempDataContext = DataContext;
            DataContext = null;
            DataContext = tempDataContext;
        }

        private void AddCustomField_Click(object sender, RoutedEventArgs e)
        {
            CurrentTemplate.CustomFields.Add(new CustomSpecField
            {
                Name = "Новое условие",
                Value = ""
            });
        }

        private void RemoveCustomField_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CustomSpecField field)
            {
                CurrentTemplate.CustomFields.Remove(field);
            }
        }

        private void Create_Spec(object sender, RoutedEventArgs e) { Create_Spec(OutputPath); }
        public void Create_Spec(string outputPath)
        {
            // 1. ОПРЕДЕЛЯЕМ ФОРМАТ СПЕЦИФИКАЦИИ (Сборочная или Обычная)
            if (AssemblyWindow.A.Assemblies != null && AssemblyWindow.A.Assemblies.Count > 0)
            {
                MessageBoxResult response = MessageBox.Show(
                    "Сформировать сборочную спецификацию?\nЕсли \"Да\", в спецификации будут отражены СБОРКИ.\nЕсли \"Нет\", в спецификации будут отражены ОТДЕЛЬНЫЕ ДЕТАЛИ!",
                    "Выбор формата спецификации", MessageBoxButton.YesNo, MessageBoxImage.Question);

                MainWindow.M.isAssemblyOffer = response == MessageBoxResult.Yes;
            }
            else
            {
                // Если сборок нет, принудительно ставим false для стандартной логики
                MainWindow.M.isAssemblyOffer = false;
            }

            // 2. ГЕНЕРИРУЕМ ИМЯ ФАЙЛА И ПУТЬ
            string specFileName = $"{MainWindow.M.Order.Text} {MainWindow.M.CustomerDrop.Text} - спецификация № {CurrentTemplate.Number}.pdf";
            outputPath = $"{Path.GetDirectoryName(outputPath)}\\{specFileName}";
            outputPath = GetAvailableFilePath(outputPath);

            // Получаем провайдера один раз для всего документа
            var currentProvider = ProviderRegistry.GetByName(CurrentTemplate.Provider)
                               ?? ProviderRegistry.Providers.FirstOrDefault();
            string taxRateText = $"НДС {currentProvider?.TaxRate ?? 22}%";

            // 3. СОЗДАЕМ PDF ДОКУМЕНТ
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40, Unit.Point);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    page.Content().Column(content =>
                    {
                        content.Spacing(10);

                        // Заголовок
                        content.Item().PaddingLeft(280).Text($"{CurrentTemplate.Header}").AlignRight().Bold();
                        content.Item().PaddingTop(30).Text($"СПЕЦИФИКАЦИЯ № {CurrentTemplate.Number} от {DateTime.Now:dd MMMM yyyy} г.").AlignCenter().Bold();

                        float totalSum = 0;
                        int rowIndex = 1; // Счетчик строк для колонки "№"

                        // Таблица товаров
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(20);  // №
                                columns.RelativeColumn(150); // Наименование товара
                                columns.RelativeColumn(50);  // Количество, шт
                                columns.RelativeColumn(60);  // Стоимость в руб., в т.ч. НДС 22%
                            });

                            // Заголовок таблицы
                            table.Header(header =>
                            {
                                StyleHeaderCell(header.Cell(), "№");
                                StyleHeaderCell(header.Cell(), "Наименование товара");
                                StyleHeaderCell(header.Cell(), "Количество, шт");
                                StyleHeaderCell(header.Cell(), $"Стоимость в руб., в т.ч. {taxRateText}");
                            });

                            // === ДАННЫЕ ТАБЛИЦЫ ===
                            if (!MainWindow.M.isAssemblyOffer)
                            {
                                // --- ЛОГИКА ДЛЯ ОБЫЧНОГО КП ---
                                var visiblePartsForExport = OfferCalculator.PrepareVisiblePartsForOffer(
                                    MainWindow.M.Parts,
                                    (float)MainWindow.M.Ratio,
                                    MainWindow.M.BonusRatio,
                                    applyMarkup: false); // Цены уже с наценкой, избегаем двойного умножения

                                if (visiblePartsForExport.Count > 0)
                                {
                                    foreach (var part in visiblePartsForExport)
                                    {
                                        totalSum += part.Total;
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text((rowIndex++).ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(part.Title ?? ""));
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Count.ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Total.ToString("N2"));
                                    }
                                }

                                // Детали (не покупные)
                                var details = MainWindow.M.ProductModel.Product.Details.Where(d => !d.IsComplect).ToList();
                                foreach (var detail in details)
                                {
                                    totalSum += detail.Total;
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text((rowIndex++).ToString());
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(detail.Title ?? ""));
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Count.ToString());
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Total.ToString("N2"));
                                }

                                // Покупные изделия
                                if (MainWindow.M.ProductModel.Product.Baskets?.Count > 0)
                                {
                                    foreach (Part basket in MainWindow.M.ProductModel.Product.Baskets)
                                    {
                                        float basketTotal = basket.Count * (float)Math.Ceiling(basket.Price * MainWindow.M.Ratio * ((100 + MainWindow.M.BonusRatio) / 100));
                                        totalSum += basketTotal;

                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text((rowIndex++).ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(basket.Title);
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basket.Count.ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketTotal.ToString("N2"));
                                    }
                                }
                            }
                            else
                            {
                                // --- ЛОГИКА ДЛЯ СБОРОЧНОГО КП ---
                                if (AssemblyWindow.A.Assemblies is null) return;

                                // 1. Сборочные единицы
                                if (AssemblyWindow.A.Assemblies.Count > 0)
                                {
                                    foreach (var assembly in AssemblyWindow.A.Assemblies)
                                    {
                                        if (assembly.Count <= 0) continue;

                                        totalSum += assembly.Total;

                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text((rowIndex++).ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text($"Сборочная единица: {assembly.Title}").Bold();
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(assembly.Count.ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(assembly.Total.ToString("N2"));
                                    }
                                }

                                // 2. Дополнительные (несборочные) детали
                                var assemblyParticleTitles = AssemblyWindow.A.Assemblies
                                    .SelectMany(a => a.Particles)
                                    .Select(p => p.Title)
                                    .ToHashSet();

                                var looseParts = MainWindow.M.Parts.Where(p => !assemblyParticleTitles.Contains(p.Title)).ToList();

                                if (looseParts.Count > 0)
                                {
                                    // Заголовок группы
                                    table.Cell().ColumnSpan(4).Border(1).BorderColor(Colors.Black).Padding(4).Text("Дополнительные детали:").Bold();

                                    foreach (var lp in looseParts)
                                    {
                                        // Применяем ту же логику наценки и проверки FixedPrice, что и в Excel-КП
                                        float lpPrice = (float)Math.Ceiling(lp.Price * MainWindow.M.Ratio * ((100 + MainWindow.M.BonusRatio) / 100));
                                        lpPrice = lpPrice < lp.FixedPrice ? lp.FixedPrice : lpPrice;
                                        float lpTotal = lpPrice * lp.Count;

                                        totalSum += lpTotal;

                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text((rowIndex++).ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(lp.Title ?? ""));
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(lp.Count.ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(lpTotal.ToString("N2"));
                                    }
                                }
                            }

                            // 3. Доставка (применима к обоим типам)
                            if (MainWindow.M.HasDelivery is true)
                            {
                                float deliveryTotal = (float)(MainWindow.M.Delivery * MainWindow.M.DeliveryRatio * MainWindow.M.Ratio);
                                totalSum += deliveryTotal;

                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text("Доставка").Bold();
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(MainWindow.M.DeliveryRatio.ToString());
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(deliveryTotal.ToString("N2"));
                            }

                            // Итоговая строка
                            table.Cell().ColumnSpan(3).Border(1).BorderColor(Colors.Black);
                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(totalSum.ToString("N2")).Bold();
                        });

                        // Блок условий
                        content.Item().Row(row =>
                        {
                            row.RelativeItem().Column(center =>
                            {
                                string totalLine = NumberToWordsHelper.NumberToWords(totalSum);

                                // Извлекаем часть до "рублей"
                                int rubIndex = totalLine.IndexOf("рублей");
                                string rubText = rubIndex > 0 ? totalLine[..rubIndex].Trim() : totalLine;

                                // Извлекаем копейки
                                int kopStart = totalLine.IndexOf("копеек");
                                string kopValue = "00";
                                if (kopStart > 0)
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(totalLine.Substring(kopStart), @"\d+");
                                    kopValue = match.Success ? match.Value.PadLeft(2, '0') : "00";
                                }

                                // Формируем итоговую строку
                                center.Item().Text($"ИТОГО: {totalSum:N0} ({rubText}) рублей {kopValue} коп., в т.ч. {taxRateText}").Bold();
                                
                                center.Item().PaddingTop(15).Text("Срок поставки: " + EndDate.Text);
                                center.Item().PaddingTop(5).Text(Delivery.Text);
                                center.Item().PaddingVertical(5).Text($"Условия оплаты: {CurrentTemplate.Terms}");

                                // Вывод пользовательских полей
                                if (CurrentTemplate.CustomFields != null && CurrentTemplate.CustomFields.Count > 0)
                                {
                                    foreach (var field in CurrentTemplate.CustomFields)
                                    {
                                        // Проверяем, что поле не пустое, чтобы не выводить лишние строки
                                        if (!string.IsNullOrWhiteSpace(field.Name) || !string.IsNullOrWhiteSpace(field.Value))
                                        {
                                            center.Item().PaddingTop(5).Text($"{field.Name}: {field.Value}");
                                        }
                                    }
                                }
                            });
                        });

                        // Блок подписей
                        content.Item().ShowEntire().Row(row =>
                        {
                            // Поставщик
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text("ПОСТАВЩИК").Bold();
                                left.Item().Text(CurrentTemplate.Provider);

                                var provider = ProviderRegistry.GetByName(CurrentTemplate.Provider);
                                string directorName = provider?.DirectorName ?? "Мешеронова М.С.";
                                string printRes = provider?.PrintResource ?? "Metal_Code.Images.ooo_laserflex.jpg";
                                string sigRes = provider?.SignatureResource ?? "Metal_Code.Images.signature_mesheronova.jpg";

                                left.Item().Layers(layers =>
                                {
                                    layers.PrimaryLayer().Height(120);

                                    layers.Layer().Column(column =>
                                    {
                                        using var printStream = WpfImageHelper.GetStream(printRes);
                                        if (printStream != null)
                                        {
                                            column.Item().Row(r =>
                                            {
                                                r.ConstantItem(30);
                                                r.ConstantItem(120).Image(printStream).FitWidth();
                                            });
                                        }
                                    });

                                    layers.Layer().Column(column =>
                                    {
                                        using var sigStream = WpfImageHelper.GetStream(sigRes);
                                        if (sigStream != null)
                                        {
                                            column.Item().Row(r =>
                                            {
                                                r.ConstantItem(40).Image(sigStream).FitWidth();
                                                r.RelativeItem();
                                                r.RelativeItem().AlignBottom().Text($"/ {directorName}");
                                            });

                                            column.Item().Row(r =>
                                            {
                                                r.RelativeItem().AlignTop().LineHorizontal(1).LineColor(Colors.Black);
                                            });
                                        }
                                        else
                                        {
                                            // Если изображение не найдено, рисуем только ФИО и линию
                                            column.Item().Row(r =>
                                            {
                                                r.RelativeItem();
                                                r.RelativeItem().AlignBottom().Text($"/ {directorName}");
                                            });

                                            column.Item().Row(r =>
                                            {
                                                r.RelativeItem().AlignTop().LineHorizontal(1).LineColor(Colors.Black);
                                            });
                                        }
                                    });
                                });
                            });

                            // Покупатель
                            row.RelativeItem().PaddingLeft(50).Column(right =>
                            {
                                right.Item().Text("ПОКУПАТЕЛЬ").Bold();
                                right.Item().Text($"{Agent.Text} {TargetCustomer.Name}");

                                right.Item().Row(r =>
                                {
                                    r.RelativeItem().AlignRight().AlignBottom().Text($"/ {CurrentTemplate.Buyer}");
                                });

                                right.Item().Row(r =>
                                {
                                    r.RelativeItem().AlignTop().LineHorizontal(1).LineColor(Colors.Black);
                                });
                            });
                        });
                    });
                });
            }).GeneratePdf(outputPath);

            // 4. Создаём Excel
            ExportSpecToExcel(OutputPath);

            // Сохранение шаблона в БД
            using ManagerContext db = new(MainWindow.M.connections[0]);
            Customer? _customer = db.Customers.FirstOrDefault(x => x.Id == TargetCustomer.Id);
            if (_customer is not null)
            {
                _customer.SpecTemplate = CurrentTemplate;
                db.SaveChanges();
            }

            MainWindow.M.StatusBegin($"Создана спецификация для текущего расчета: {MainWindow.M.Order.Text} {TargetCustomer.Name}");
            Close();
        }

        public void ExportSpecToExcel(string outputPath)
        {
            // Генерируем имя файла
            string specFileName = $"{MainWindow.M.Order.Text} {MainWindow.M.CustomerDrop.Text} - спецификация № {CurrentTemplate.Number}.xlsx";
            outputPath = $"{Path.GetDirectoryName(outputPath)}\\{specFileName}";
            outputPath = GetAvailableFilePath(outputPath);

            var currentProvider = ProviderRegistry.GetByName(CurrentTemplate.Provider)
                   ?? ProviderRegistry.Providers.FirstOrDefault();
            string taxRateText = $"НДС {currentProvider?.TaxRate ?? 22}%";

            // Лицензия EPPlus (измените на Commercial при наличии лицензии)
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Спецификация");

            // Ширина колонок
            ws.Column(1).Width = 6;    // №
            ws.Column(2).Width = 50;   // Наименование
            ws.Column(3).Width = 14;   // Количество
            ws.Column(4).Width = 22;   // Стоимость

            int row = 1;

            // === ЗАГОЛОВОК ===
            ws.Cells[row, 1, row, 4].Merge = true;
            ws.Cells[row, 1].Value = CurrentTemplate.Header;
            ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[row, 1].Style.Font.Bold = true;
            row += 2;

            // === НОМЕР И ДАТА ===
            ws.Cells[row, 1, row, 4].Merge = true;
            ws.Cells[row, 1].Value = $"СПЕЦИФИКАЦИЯ № {CurrentTemplate.Number} от {DateTime.Now:dd MMMM yyyy} г.";
            ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 12;
            row += 2;

            // === ШАПКА ТАБЛИЦЫ ===
            ws.Cells[row, 1].Value = "№";
            ws.Cells[row, 2].Value = "Наименование товара";
            ws.Cells[row, 3].Value = "Количество, шт";
            ws.Cells[row, 4].Value = $"Стоимость в руб., в т.ч. {taxRateText}";

            for (int col = 1; col <= 4; col++)
            {
                ws.Cells[row, col].Style.Font.Bold = true;
                ws.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[row, col].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                ws.Cells[row, col].Style.WrapText = true;
            }
            row++;

            // === ДАННЫЕ ТАБЛИЦЫ ===
            float totalSum = 0;
            int rowIndex = 1;

            if (!MainWindow.M.isAssemblyOffer)
            {
                // --- Обычное КП ---
                var visiblePartsForExport = OfferCalculator.PrepareVisiblePartsForOffer(
                    MainWindow.M.Parts,
                    (float)MainWindow.M.Ratio,
                    MainWindow.M.BonusRatio,
                    applyMarkup: false);

                if (visiblePartsForExport.Count > 0)
                {
                    foreach (var part in visiblePartsForExport)
                    {
                        totalSum += part.Total;
                        WriteTableRow(ws, row++, rowIndex++, Prefix(part.Title ?? ""), part.Count, part.Total);
                    }
                }

                var details = MainWindow.M.ProductModel.Product.Details.Where(d => !d.IsComplect).ToList();
                foreach (var detail in details)
                {
                    totalSum += detail.Total;
                    WriteTableRow(ws, row++, rowIndex++, Prefix(detail.Title ?? ""), detail.Count, detail.Total);
                }

                if (MainWindow.M.ProductModel.Product.Baskets?.Count > 0)
                {
                    foreach (Part basket in MainWindow.M.ProductModel.Product.Baskets)
                    {
                        float basketTotal = basket.Count * (float)Math.Ceiling(
                            basket.Price * MainWindow.M.Ratio * ((100 + MainWindow.M.BonusRatio) / 100));
                        totalSum += basketTotal;
                        WriteTableRow(ws, row++, rowIndex++, basket.Title != null ? basket.Title : "", basket.Count, basketTotal);
                    }
                }
            }
            else
            {
                // --- Сборочное КП ---
                if (AssemblyWindow.A.Assemblies is null) return;

                if (AssemblyWindow.A.Assemblies.Count > 0)
                {
                    foreach (var assembly in AssemblyWindow.A.Assemblies)
                    {
                        if (assembly.Count <= 0) continue;
                        totalSum += assembly.Total;
                        WriteTableRow(ws, row, rowIndex++, $"Сборочная единица: {assembly.Title}",
                            assembly.Count, assembly.Total, bold: true);
                        row++;
                    }
                }

                var assemblyParticleTitles = AssemblyWindow.A.Assemblies
                    .SelectMany(a => a.Particles)
                    .Select(p => p.Title)
                    .ToHashSet();

                var looseParts = MainWindow.M.Parts.Where(p => !assemblyParticleTitles.Contains(p.Title)).ToList();

                if (looseParts.Count > 0)
                {
                    // Заголовок группы
                    ws.Cells[row, 1, row, 4].Merge = true;
                    ws.Cells[row, 1].Value = "Дополнительные детали:";
                    ws.Cells[row, 1].Style.Font.Bold = true;
                    for (int col = 1; col <= 4; col++)
                        ws.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    row++;

                    foreach (var lp in looseParts)
                    {
                        float lpPrice = (float)Math.Ceiling(lp.Price * MainWindow.M.Ratio * ((100 + MainWindow.M.BonusRatio) / 100));
                        lpPrice = lpPrice < lp.FixedPrice ? lp.FixedPrice : lpPrice;
                        float lpTotal = lpPrice * lp.Count;
                        totalSum += lpTotal;
                        WriteTableRow(ws, row++, rowIndex++, Prefix(lp.Title ?? ""), lp.Count, lpTotal);
                    }
                }
            }

            // === ДОСТАВКА ===
            if (MainWindow.M.HasDelivery is true)
            {
                float deliveryTotal = (float)(MainWindow.M.Delivery * MainWindow.M.DeliveryRatio * MainWindow.M.Ratio);
                totalSum += deliveryTotal;

                ws.Cells[row, 1].Value = "";
                ws.Cells[row, 2].Value = "Доставка";
                ws.Cells[row, 2].Style.Font.Bold = true;
                ws.Cells[row, 3].Value = MainWindow.M.DeliveryRatio;
                ws.Cells[row, 4].Value = deliveryTotal;
                ws.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";

                for (int col = 1; col <= 4; col++)
                {
                    ws.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
                row++;
            }

            // === ИТОГО ===
            ws.Cells[row, 1, row, 3].Merge = true;
            ws.Cells[row, 1].Value = "ИТОГО:";
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 4].Value = totalSum;
            ws.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
            ws.Cells[row, 4].Style.Font.Bold = true;
            for (int col = 1; col <= 4; col++)
                ws.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            row += 2;

            // === СУММА ПРОПИСЬЮ ===
            string totalLine = NumberToWordsHelper.NumberToWords(totalSum);
            int rubIndex = totalLine.IndexOf("рублей");
            string rubText = rubIndex > 0 ? totalLine[..rubIndex].Trim() : totalLine;
            int kopStart = totalLine.IndexOf("копеек");
            string kopValue = "00";
            if (kopStart > 0)
            {
                var match = System.Text.RegularExpressions.Regex.Match(totalLine.Substring(kopStart), @"\d+");
                kopValue = match.Success ? match.Value.PadLeft(2, '0') : "00";
            }

            ws.Cells[row, 1, row, 4].Merge = true;
            ws.Cells[row, 1].Value = $"ИТОГО: {totalSum:N0} ({rubText}) рублей {kopValue} коп., в т.ч. {taxRateText}";
            ws.Cells[row, 1].Style.Font.Bold = true;
            row += 2;

            // === УСЛОВИЯ ===
            ws.Cells[row, 1, row, 4].Merge = true;
            ws.Cells[row, 1].Value = "Срок поставки: " + EndDate.Text;
            row++;

            ws.Cells[row, 1, row, 4].Merge = true;
            ws.Cells[row, 1].Value = Delivery.Text;
            ws.Cells[row, 1].Style.WrapText = true;
            row++;

            ws.Cells[row, 1, row, 4].Merge = true;
            ws.Cells[row, 1].Value = $"Условия оплаты: {CurrentTemplate.Terms}";
            row++;

            // Пользовательские поля
            if (CurrentTemplate.CustomFields != null)
            {
                foreach (var field in CurrentTemplate.CustomFields)
                {
                    if (!string.IsNullOrWhiteSpace(field.Name) || !string.IsNullOrWhiteSpace(field.Value))
                    {
                        ws.Cells[row, 1, row, 4].Merge = true;
                        ws.Cells[row, 1].Value = $"{field.Name}: {field.Value}";
                        ws.Cells[row, 1].Style.WrapText = true;
                        row++;
                    }
                }
            }

            row += 2;

            // === ПОДПИСИ ===
            int signatureRow = row;

            ws.Cells[row, 1, row, 2].Merge = true;
            ws.Cells[row, 1].Value = "ПОСТАВЩИК";
            ws.Cells[row, 1].Style.Font.Bold = true;

            ws.Cells[row, 3, row, 4].Merge = true;
            ws.Cells[row, 3].Value = "ПОКУПАТЕЛЬ";
            ws.Cells[row, 3].Style.Font.Bold = true;
            row++;

            ws.Cells[row, 1, row, 2].Merge = true;
            ws.Cells[row, 1].Value = CurrentTemplate.Provider;

            ws.Cells[row, 3, row, 4].Merge = true;
            ws.Cells[row, 3].Value = $"{Agent.Text} {TargetCustomer.Name}";

            // Получаем информацию о провайдере
            var provider = ProviderRegistry.GetByName(CurrentTemplate.Provider);
            string directorName = provider?.DirectorName ?? "Мешеронова М.С.";

            // Устанавливаем высоту строк для размещения изображений
            ws.Row(row + 2).Height = 20;
            row += 2;

            // Вставка изображений поставщика
            if (provider != null)
            {
                // 1. СНАЧАЛА ПОДПИСЬ
                try
                {
                    using var sigStream = WpfImageHelper.GetStream(provider.SignatureResource);
                    if (sigStream != null)
                    {
                        var sigPic = ws.Drawings.AddPicture("Signature", sigStream);
                        sigPic.SetPosition(signatureRow + 1, 0, 0, 30);
                        sigPic.SetSize(96, 40);
                    }
                }
                catch { /* Изображение не найдено */ }

                // 2. ПОТОМ ПЕЧАТЬ
                try
                {
                    using var printStream = WpfImageHelper.GetStream(provider.PrintResource);
                    if (printStream != null)
                    {
                        var printPic = ws.Drawings.AddPicture("Print", printStream);
                        printPic.SetPosition(signatureRow + 3, 0, 0, 20);
                        printPic.SetSize(120, 120);
                    }
                }
                catch { /* Изображение не найдено */ }
            }

            // Строки для подписей
            ws.Cells[row, 1, row, 2].Merge = true;
            ws.Cells[row, 1].Value = $"___________________ / {directorName}";

            ws.Cells[row, 3, row, 4].Merge = true;
            ws.Cells[row, 3].Value = $"___________________ / {CurrentTemplate.Buyer}";
            // === СОХРАНЕНИЕ ===
            package.SaveAs(new FileInfo(outputPath));
        }

        // Вспомогательный метод для записи строки таблицы
        private static void WriteTableRow(
        ExcelWorksheet ws, int row, int num, string name, int count, float total, bool bold = false)
        {
            ws.Cells[row, 1].Value = num;
            ws.Cells[row, 2].Value = name;
            ws.Cells[row, 3].Value = count;
            ws.Cells[row, 4].Value = total;
            ws.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";

            if (bold)
            {
                ws.Cells[row, 2].Style.Font.Bold = true;
            }

            for (int col = 1; col <= 4; col++)
            {
                ws.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                ws.Cells[row, col].Style.WrapText = true;
            }

            ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        // метод для стилизации заголовка таблицы
        private static void StyleHeaderCell(IContainer cell, string text)
        {
            cell.DefaultTextStyle(x => x.SemiBold())
                .Border(1).BorderColor(Colors.Black)
                .Padding(4)
                .AlignCenter()
                .Text(text);
        }

        // метод для нормализации наименования детали
        private static string Prefix(string title)
        {
            // Префикс в зависимости от типа контрагента
            string prefix = MainWindow.M.IsAgent ? "Изготовление детали " : "Деталь ";

            string? value = title;

            // 1. Добавляем префикс
            value = prefix + value;

            // 2. Удаляем название металла (первое совпадение)
            foreach (Metal metal in MainWindow.M.Metals)
            {
                if (metal.Name != null && value.Contains(metal.Name, StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Replace(metal.Name, "");
                    break;
                }
            }

            // 3. Обрезаем по последнему 's' (если есть)
            int lastSIndex = value.ToLowerInvariant().LastIndexOf('s');
            if (lastSIndex > 0) value = value[..lastSIndex];

            // 4. Убираем лишние пробелы
            return value.Trim();
        }

        // метод создания безопасного пути файла
        public static string GetAvailableFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("Путь к файлу не может быть пустым.");

            string? directory = Path.GetDirectoryName(filePath) ?? throw new ArgumentException("Некорректный путь к файлу.");
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);
            string newFilePath = filePath;

            int counter = 1;

            while (true)
            {
                try
                {
                    using FileStream fs = new(newFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                    fs.Close();
                    break;
                }
                catch (IOException)
                {
                    newFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}_{counter}{extension}");
                    counter++;
                }
                catch (UnauthorizedAccessException)
                {
                    throw new UnauthorizedAccessException($"Нет прав на запись в файл: {newFilePath}");
                }
            }

            return newFilePath;
        }

        private System.Windows.Media.ImageSource? GetWpfImage(string? resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return null;

            try
            {
                using var originalStream = WpfImageHelper.GetStream(resourceName);
                if (originalStream == null) return null;

                using var memoryStream = new MemoryStream();
                originalStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }

    public static class NumberToWordsHelper
    {
        public static string NumberToWords(double number)
        {
            if (number == 0) return "ноль рублей 00 копеек";

            int rub = (int)Math.Floor(Math.Abs(number)); // поддержка отрицательных
            int kop = (int)Math.Round((Math.Abs(number) - rub) * 100);

            string rubText = ConvertNumberToWords(rub);
            string kopText = ConvertNumberToWords(kop);

            // Падежи для рублей
            string rubCase = GetRublesCase(rub);
            // Падежи для копеек
            string kopCase = GetKopecksCase(kop);

            return $"{rubText} {rubCase} {kop:00} {kopCase}";
        }

        private static string ConvertNumberToWords(int number)
        {
            if (number == 0) return "ноль";

            string result = "";

            // Миллионы
            if (number >= 1_000_000)
            {
                int millions = number / 1_000_000;
                int remainder = number % 1_000_000;

                string millionsText = ConvertNumberToWords(millions);

                // Склонение "миллион"
                string millionCase = millions % 10 == 1 && millions % 100 != 11 ? "миллион" :
                                     (millions % 10 >= 2 && millions % 10 <= 4 && (millions % 100 < 10 || millions % 100 >= 20)) ? "миллиона" : "миллионов";

                result += $"{millionsText} {millionCase}";
                if (remainder > 0)
                {
                    result += " ";
                }
                else
                {
                    return result;
                }
            }

            // Тысячи
            if (number >= 1_000 && number < 1_000_000 || (number >= 1_000_000 && number % 1_000_000 != 0))
            {
                int thousands = (number / 1_000) % 1_000;
                int remainder = number % 1_000;

                if (thousands > 0)
                {
                    string thousandsText = ConvertNumberToWordsForFeminine(thousands);

                    // Склонение "тысяча"
                    string thousandCase;
                    int t = thousands % 100;

                    if (t == 11 || t == 12 || t == 13 || t == 14)
                        thousandCase = "тысяч";
                    else
                    {
                        switch (t % 10)
                        {
                            case 1: thousandCase = "тысяча"; break;
                            case 2:
                            case 3:
                            case 4: thousandCase = "тысячи"; break;
                            default: thousandCase = "тысяч"; break;
                        }
                    }

                    result += $"{thousandsText} {thousandCase}";
                    if (remainder > 0) result += " ";
                }
                else if (number >= 1_000 && number % 1_000_000 != 0)
                {
                    result += "тысяч ";
                }
            }

            // Сотни, десятки, единицы
            int n = number % 1000;

            if (n == 0)
            {
                return result.Trim();
            }

            string[] units = { "", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять", "десять",
                           "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать",
                           "семнадцать", "восемнадцать", "девятнадцать" };

            string[] tens = { "", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто" };

            string[] hundreds = { "", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот" };

            if (n >= 100)
            {
                result += hundreds[n / 100];
                n %= 100;
                if (n > 0) result += " ";
            }

            if (n >= 20)
            {
                result += tens[n / 10];
                if (n % 10 > 0)
                {
                    int digit = n % 10;
                    if (digit == 1) result += " один";
                    else if (digit == 2) result += " два";
                    else result += " " + units[digit];
                }
            }
            else if (n > 0)
            {
                result += units[n];
            }

            return result.Trim();
        }

        private static string ConvertNumberToWordsForFeminine(int number)
        {
            if (number == 0) return "";

            string[] unitsFem = { "", "одна", "две", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять", "десять",
                          "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать",
                          "семнадцать", "восемнадцать", "девятнадцать" };

            string[] tens = { "", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто" };

            string[] hundreds = { "", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот" };

            if (number == 1) return "одна";
            if (number == 2) return "две";

            string result = "";

            // Сотни
            if (number >= 100)
            {
                result += hundreds[number / 100];
                number %= 100;
                if (number > 0) result += " ";
            }

            // Десятки и единицы
            if (number >= 20)
            {
                result += tens[number / 10];
                if (number % 10 > 0)
                {
                    int digit = number % 10;
                    if (digit >= 1 && digit <= 9)
                        result += " " + unitsFem[digit];
                }
            }
            else if (number > 0)
            {
                result += unitsFem[number];
            }

            return result.Trim();
        }

        private static string GetRublesCase(int n)
        {
            n = Math.Abs(n) % 100;
            if (n >= 11 && n <= 19) return "рублей";
            n %= 10;
            return n == 1 ? "рубль" : (n >= 2 && n <= 4) ? "рубля" : "рублей";
        }

        private static string GetKopecksCase(int n)
        {
            n = Math.Abs(n) % 100;
            if (n >= 11 && n <= 19) return "копеек";
            n %= 10;
            return n == 1 ? "копейка" : (n >= 2 && n <= 4) ? "копейки" : "копеек";
        }
    }

    // ==================== ProviderInfo ====================
    public class ProviderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DirectorName { get; set; } = string.Empty;
        public string PrintResource { get; set; } = string.Empty;
        public string SignatureResource { get; set; } = string.Empty;
        public int TaxRate { get; set; } = 22;

        public override string ToString() => Name;
    }

    public static class ProviderRegistry
    {
        public static ObservableCollection<ProviderInfo> Providers { get; } = new()
    {
        new ProviderInfo {
            Name = "ООО ЛАЗЕРФЛЕКС",
            DirectorName = "Мешеронова М.С.",
            PrintResource = "Metal_Code.Images.ooo_laserflex.jpg",
            SignatureResource = "Metal_Code.Images.signature_mesheronova.jpg",
            TaxRate = 22
        },
        new ProviderInfo {
            Name = "ООО ПРОВЭЛД",
            DirectorName = "Сергеев Ю.А.",
            PrintResource = "Metal_Code.Images.ooo_pk_laserflex.jpg",
            SignatureResource = "Metal_Code.Images.signature_sergeev.jpg",
            TaxRate = 22
        },
        new ProviderInfo {
            Name = "ООО ПК ЛАЗЕРФЛЕКС",
            DirectorName = "Сергеев Ю.А.",
            PrintResource = "Metal_Code.Images.ooo_pk_laserflex.jpg",
            SignatureResource = "Metal_Code.Images.signature_sergeev.jpg",
            TaxRate = 5
        },
        new ProviderInfo {
            Name = "ИП МЕШЕРОНОВА",
            DirectorName = "Мешеронова М.С.",
            PrintResource = "Metal_Code.Images.ip_mesheronova.jpg",
            SignatureResource = "Metal_Code.Images.signature_mesheronova.jpg",
            TaxRate = 5
        }
    };

        public static ProviderInfo? GetByName(string? name)
        {
            return string.IsNullOrEmpty(name) ? null : Providers.FirstOrDefault(p => p.Name == name);
        }

        /// <summary>
        /// Определяет провайдера по комбинации флагов IsLaser и IsAgent.
        /// </summary>
        /// <param name="isLaser">Признак подписанта</param>
        /// <param name="isAgent">Признак НДС (5% или 22%)</param>
        /// <returns>Соответствующий провайдер из реестра</returns>
        public static ProviderInfo? GetByFlags(bool isLaser, bool isAgent)
        {
            string targetName = (isLaser, isAgent) switch
            {
                (true, true) => "ИП МЕШЕРОНОВА",
                (true, false) => "ООО ЛАЗЕРФЛЕКС",
                (false, true) => "ООО ПК ЛАЗЕРФЛЕКС",
                (false, false) => "ООО ПРОВЭЛД",
            };

            return GetByName(targetName);
        }
    }
}