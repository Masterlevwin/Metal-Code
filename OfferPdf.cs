using Metal_Code.Models;
using Metal_Code.Utils;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace Metal_Code
{
    public class OfferPdf
    {
        private readonly string outputPath, descriptionWorks = string.Empty;
        private readonly Color color = new(MainWindow.M.IsLaser ? 0xFF78B4FF : 0xFFFFAA00);

        public OfferPdf(string path, string? _descriptionWorks)
        {
            outputPath = path;
            descriptionWorks = _descriptionWorks ?? string.Empty;

            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20, Unit.Point);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    page.PageColor(Colors.White);

                    // Заголовок: логотип + контакты
                    page.Header().Column(header =>
                    {
                        header.Item().Row(row =>
                        {
                            row.RelativeItem().Image(MainWindow.M.IsLaser ? "laser_logo.png" : "app_logo.png"); // Логотип
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text(MainWindow.M.IsLaser ? "ЛАЗЕРФЛЕКС" : "ПРОВЭЛД").SemiBold().FontSize(12).AlignRight();
                                col.Item().PaddingVertical(5).Text(MainWindow.M.IsLaser ? "тел: 8 (812) 509-60-11" : "тел: 8 (812) 603 - 45 - 33").FontSize(8).AlignRight();
                            });
                        });

                        // Разделитель
                        header.Item().PaddingVertical(5).LineHorizontal(1).LineColor(color);

                        // КП и клиент
                        header.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().AlignCenter().Text("КП № " + MainWindow.M.Order.Text + " для " + MainWindow.M.CustomerDrop.Text + " от " + DateTime.Now.ToString("d")).Bold().FontSize(14);

                                // Разделитель
                                header.Item().PaddingVertical(5).LineHorizontal(1).LineColor(color);

                                // Предупреждение
                                header.Item().PaddingVertical(5).Text("Данный расчет действителен в течение 2-х банковских дней")
                                    .Italic().FontSize(8).FontColor(Colors.Red.Darken2);
                            });
                        });
                    });

                    // Основное содержимое
                    page.Content().Column(content =>
                    {
                        content.Spacing(10);

                        // Таблица
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(40);  // Материал
                                columns.RelativeColumn(30);  // Толщина
                                columns.RelativeColumn(35);  // Размеры
                                columns.RelativeColumn(30);  // Работы
                                columns.RelativeColumn(20);  // №
                                columns.RelativeColumn(70);  // Наименование
                                columns.RelativeColumn(25);  // Кол-во
                                columns.RelativeColumn(35);  // Цена
                                columns.RelativeColumn(45);  // Стоимость
                            });

                            // Заголовок таблицы
                            table.Header(header =>
                            {
                                StyleHeaderCell(header.Cell(), "Материал");
                                StyleHeaderCell(header.Cell(), "Толщина");
                                StyleHeaderCell(header.Cell(), "Размеры, мм");
                                StyleHeaderCell(header.Cell(), "Работы");
                                StyleHeaderCell(header.Cell(), "№");
                                StyleHeaderCell(header.Cell(), "Наименование");
                                StyleHeaderCell(header.Cell(), "Кол-во, шт");
                                StyleHeaderCell(header.Cell(), "Цена за шт, руб");
                                StyleHeaderCell(header.Cell(), "Стоимость, руб");
                            });

                            // Данные
                            float totalSum = 0;

                            int row = 1;

                            // Если выбран формат сборочного КП
                            if (MainWindow.M.isAssemblyOffer)
                            {
                                var parts = MainWindow.M.Parts.Union(MainWindow.M.BasketControls.Select(b => b.Basket));

                                if (AssemblyWindow.A.Assemblies.Count > 0)
                                    foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
                                    {
                                        totalSum += assembly.Total;

                                        table.Cell().ColumnSpan(3).Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(assembly.Description ?? "").Bold();
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}").Bold();
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(assembly.Title ?? "")).Bold();
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{assembly.Count}").Bold();
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(assembly.Price.ToString("N2")).Bold();
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(assembly.Total.ToString("N2")).Bold();
                                        row++;

                                        for (int p = 0; p < assembly.Particles.Count; p++)
                                        {
                                            Particle particle = assembly.Particles[p];
                                            Part? part = parts.FirstOrDefault(p => p.Title == particle.Title);

                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part?.Metal ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part?.Destiny > 0 ? part?.Destiny.ToString() ?? "" : "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part?.Accuracy ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part?.Description ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(particle.Title ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{particle.Count}");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                        }
                                    }

                                if (MainWindow.M.LooseParts.Count > 0)
                                {
                                    table.Cell().ColumnSpan(9).Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("Дополнительные детали:").Bold();

                                    for (int i = 0; i < MainWindow.M.LooseParts.Count; i++)
                                    {
                                        Part loosePart = MainWindow.M.LooseParts[i];
                                        totalSum += loosePart.Total;

                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(loosePart.Metal);
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(loosePart.Destiny > 0 ? loosePart.Destiny.ToString() : "");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(loosePart.Accuracy ?? "");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(loosePart.Description ?? "");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(loosePart.Title ?? ""));
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{loosePart.Count}");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(loosePart.Price.ToString("N2"));
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(loosePart.Total.ToString("N2"));
                                        row++;
                                    }
                                }
                            }
                            // Иначе если есть нарезанные детали, вычисляем их общую стоимость, и оформляем их в КП
                            else if (MainWindow.M.Parts.Count > 0)
                            {
                                var visiblePartsForExport = OfferCalculator.PrepareVisiblePartsForOffer(MainWindow.M.Parts);
                                for (int i = 0; i < visiblePartsForExport.Count; i++)
                                {
                                    var part = visiblePartsForExport[i];
                                    totalSum += part.Total;

                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Metal);
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Destiny.ToString());
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Accuracy ?? "");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Description ?? "");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(part.Title ?? ""));
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Count.ToString());
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Price.ToString("N2"));
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Total.ToString("N2"));
                                    row++;
                                }
                            }

                            ObservableCollection<Detail> details = new(MainWindow.M.ProductModel.Product.Details.Where(d => !d.IsComplect));
                            if (details.Count > 0)
                            {
                                for (int i = 0; i < details.Count; i++)
                                {
                                    Detail detail = details[i];
                                    totalSum += detail.Total;

                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Metal ?? "");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(MainWindow.Parser(detail.Destiny) > 0 ? detail.Destiny.ToString() : "");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Accuracy ?? "");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Description ?? "");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(detail.Title ?? ""));
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Count.ToString());
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Price.ToString("N2"));
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Total.ToString("N2"));
                                    row++;
                                }
                            }

                            // Добавляем покупные изделия
                            if (MainWindow.M.isAssemblyOffer)
                            {
                                // Режим со сборками: используем CurrentBaskets
                                if (AssemblyWindow.A.CurrentBaskets?.Count > 0)
                                {
                                    var basketsWithWork = AssemblyWindow.A.CurrentBaskets.Where(b => b.Count > 0 && !string.IsNullOrEmpty(b.Description)).ToList();
                                    if (basketsWithWork.Count > 0)
                                    {
                                        foreach (Part basketWithWork in basketsWithWork)
                                        {
                                            float total = basketWithWork.Price * basketWithWork.Count;
                                            totalSum += total;

                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Metal ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Destiny > 0 ? basketWithWork.Destiny.ToString() : "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Accuracy ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Description ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(basketWithWork.Title ?? ""));
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Count.ToString());
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Price.ToString("N2"));
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(total.ToString("N2"));
                                            row++;
                                        }
                                    }

                                    var basketsExtra = basketsWithWork.Count > 0
                                        ? AssemblyWindow.A.CurrentBaskets.Where(b => b.Count > 0 && !basketsWithWork.Any(bw => bw.Title == b.Title)).ToList()
                                        : AssemblyWindow.A.CurrentBaskets.Where(b => b.Count > 0).ToList();

                                    if (basketsExtra.Count > 0)
                                    {
                                        table.Cell().ColumnSpan(9).Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("Покупные изделия:").Bold();

                                        foreach (Part basket in basketsExtra)
                                        {
                                            float total = basket.Price * basket.Count;
                                            totalSum += total;

                                            table.Cell().ColumnSpan(4).Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basket.Title ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basket.Count.ToString());
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basket.Price.ToString("N2"));
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(total.ToString("N2"));
                                            row++;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Режим без сборок: используем оригинальные данные
                                var allBaskets = MainWindow.M.BasketControls.Select(b => b.Basket).ToList();
                                if (allBaskets.Count > 0)
                                {
                                    var basketsWithWork = allBaskets.Where(b => b.Count > 0 && !string.IsNullOrEmpty(b.Description)).ToList();
                                    if (basketsWithWork.Count > 0)
                                    {
                                        foreach (Part basketWithWork in basketsWithWork)
                                        {
                                            float total = basketWithWork.Price * basketWithWork.Count;
                                            totalSum += total;

                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Metal ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Destiny > 0 ? basketWithWork.Destiny.ToString() : "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Accuracy ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Description ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(basketWithWork.Title ?? ""));
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Count.ToString());
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basketWithWork.Price.ToString("N2"));
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(total.ToString("N2"));
                                            row++;
                                        }
                                    }

                                    var basketsExtra = basketsWithWork.Count > 0
                                        ? allBaskets.Where(b => b.Count > 0 && !basketsWithWork.Any(bw => bw.Title == b.Title)).ToList()
                                        : allBaskets.Where(b => b.Count > 0).ToList();

                                    if (basketsExtra.Count > 0)
                                    {
                                        table.Cell().ColumnSpan(9).Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("Покупные изделия:").Bold();

                                        foreach (Part basket in basketsExtra)
                                        {
                                            float total = basket.Price * basket.Count;
                                            totalSum += total;

                                            table.Cell().ColumnSpan(4).Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basket.Title ?? "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basket.Count.ToString());
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(basket.Price.ToString("N2"));
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(total.ToString("N2"));
                                            row++;
                                        }
                                    }
                                }
                            }

                            if (MainWindow.M.CheckConstruct.IsChecked == null)
                            {
                                float constructRatio = MainWindow.Parser(MainWindow.M.ConstructRatio.Text);
                                constructRatio = constructRatio > 1 ? constructRatio : 1;

                                float constructTotal = (float)(MainWindow.M.Construct * MainWindow.M.Ratio * ((100 + MainWindow.M.BonusRatio) / 100));
                                totalSum += constructTotal;

                                float constructPrice = (float)Math.Ceiling(constructTotal / constructRatio);

                                table.Cell().ColumnSpan(5).Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("Конструкторские работы").Bold();
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(constructRatio.ToString());
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(constructPrice.ToString("N2"));
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(constructTotal.ToString("N2"));
                            }

                            if (MainWindow.M.HasDelivery is true)
                            {
                                float delivery = (float)(MainWindow.M.Delivery * MainWindow.M.Ratio);
                                float deliveryTotal = delivery * MainWindow.M.DeliveryRatio;
                                totalSum += deliveryTotal;

                                table.Cell().ColumnSpan(5).Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("Доставка").Bold();
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(MainWindow.M.DeliveryRatio.ToString());
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(delivery.ToString("N2"));
                                table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(deliveryTotal.ToString("N2"));
                            }

                            // Итоговая строка
                            table.Cell().ColumnSpan(8).Border(1).BorderColor(Colors.Black)
                                .Padding(4).AlignRight().Text("ИТОГО: ").Bold();

                            table.Cell().Border(1).BorderColor(Colors.Black)
                                .Padding(4).AlignCenter().Text(totalSum.ToString("N2")).Bold();
                        });

                        // Блок параметров (Материал, Точность и т.д.)
                        content.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                // Определяем источник материала по всем заготовкам
                                var allTypeDetails = MainWindow.M.DetailControls
                                    .SelectMany(dc => dc.TypeDetailControls)
                                    .ToList();

                                bool allFromExecutor = allTypeDetails.All(t => t.HasMetal);
                                bool allFromCustomer = allTypeDetails.All(t => !t.HasMetal);
                                bool mixedSituation = !allFromExecutor && !allFromCustomer;

                                // Проверяем наличие алюминиевых листов от исполнителя
                                bool hasAluminumSheets = allTypeDetails.Any(t =>
                                    t.TypeDetailDrop?.Text == "Лист металла" &&
                                    t.HasMetal &&
                                    t.MetalDrop?.Text != null &&
                                    (t.MetalDrop.Text.Contains("амг2", StringComparison.OrdinalIgnoreCase) ||
                                     t.MetalDrop.Text.Contains("амг5", StringComparison.OrdinalIgnoreCase) ||
                                     t.MetalDrop.Text.Contains("амг6", StringComparison.OrdinalIgnoreCase) ||
                                     t.MetalDrop.Text.Contains("д16АТ", StringComparison.OrdinalIgnoreCase) ||
                                     t.MetalDrop.Text.Contains("д16АМ", StringComparison.OrdinalIgnoreCase)));

                                string aluminumWarning = hasAluminumSheets ? " (возможны неглубокие царапины от обрезков)" : "";

                                string materialText;
                                if (allFromExecutor)
                                {
                                    materialText = "Материал: Исполнителя" + aluminumWarning;
                                }
                                else if (allFromCustomer)
                                {
                                    materialText = "Материал: Заказчика (внимание: остатки давальческого материала забираются вместе с заказом, иначе эти остатки утилизируются!)";
                                }
                                else // mixedSituation
                                {
                                    materialText = "Материал: Частично исполнителя, частично заказчика" + aluminumWarning +
                                                  " (внимание: остатки давальческого материала забираются вместе с заказом, иначе эти остатки утилизируются!)";
                                }

                                left.Item().Text(materialText).SemiBold();

                                left.Item().PaddingVertical(5).Text($"Срок изготовления: {MainWindow.M.DateProduction.Text} раб/дней" +
                                    $"{(MainWindow.M.HasAssembly ? " (ЭКСПРЕСС)." : ".")}").SemiBold();

                                left.Item().PaddingVertical(5).Text("Условия оплаты: предоплата 100% по счету Исполнителя.");

                                float totalWeight = MainWindow.M.GetTotalMass();

                                if (MainWindow.M.HasDelivery is true)
                                {

                                    left.Item().PaddingVertical(5).Text(textBlock =>
                                    {
                                        textBlock.Span("Порядок отгрузки: доставка силами Исполнителя по адресу: " +
                                                      $"{MainWindow.M.Adress.Text}. Вес деталей - примерно ");
                                        textBlock.Span($"{totalWeight:N0} кг").Bold();
                                        textBlock.Span(".");
                                    });
                                }
                                else
                                {
                                    left.Item().PaddingVertical(5).Text(textBlock =>
                                    {
                                        textBlock.Span("Порядок отгрузки: самовывоз со склада Исполнителя по адресу: Ленинградская область, Всеволожский район, " +
                                                      "Колтуши, деревня Мяглово, ул. Дорожная, уч. 4Б. Вес деталей - примерно ");
                                        textBlock.Span($"{totalWeight:N0} кг").Bold();
                                        textBlock.Span(".");
                                    });
                                }
                                left.Item().PaddingVertical(5).Text("Точность: H14/h14 ±IT14/2 (резка осуществляется воздухом).");

                                left.Item().PaddingVertical(5).Text($"Расшифровка работ: {descriptionWorks}");

                                string disclaimer = "Изделия изготавливаются строго по предоставленным Заказчиком чертежам. " +
                                                    "Исполнитель не несёт ответственности за корректность конструкторской документации.";

                                // Проверяем, есть ли хотя бы один хлыст с нестандартной зоной зажима
                                bool hasNonDefaultClamp = MainWindow.M.DetailControls
                                    .SelectMany(dc => dc.TypeDetailControls)
                                    .SelectMany(tc => tc.WorkControls)
                                    .Where(wc => wc.workType is PipeControl)              // Фильтруем по свойству workType
                                    .Select(wc => (PipeControl)wc.workType!)              // Приводим к PipeControl
                                    .Where(pc => pc.Items?.Count > 0)                     // Только с заполненными Items
                                    .SelectMany(pc => pc.Items!)
                                    .SelectMany(item => item.PipeStocks!)
                                    .Any(stock => stock.ClampZone < 340);                 // Меньше 340

                                string clampWarning = hasNonDefaultClamp
                                    ? "\nДля сортового проката применен уменьшенный зажим, поэтому возможен провис деталей с погрешностью в размерах."
                                    : string.Empty;

                                string baseNote = disclaimer + clampWarning;
                                var userComment = MainWindow.M.Comment.Text?.Trim();

                                string finalNote = string.IsNullOrEmpty(userComment)
                                    ? baseNote
                                    : $"{baseNote}\n{userComment}";
                                left.Item().PaddingVertical(5).Text($"Примечание: {finalNote}").SemiBold();

                                left.Item().PaddingVertical(5).Text($"Ваш менеджер: {MainWindow.M.ManagerDrop.Text}");

                                left.Item().Text($"версия: {MainWindow.M.Version}").AlignRight();
                            });
                        });
                    });
                });
            }).GeneratePdf(outputPath);
        }

        // Вспомогательный метод для стилизации заголовка таблицы
        void StyleHeaderCell(IContainer cell, string text)
        {
            cell.DefaultTextStyle(x => x.SemiBold())
                .Border(1).BorderColor(Colors.Black)
                .Background(color)
                .Padding(4)
                .AlignCenter()
                .Text(text);
        }

        // Вспомогательный метод для нормализации наименования детали
        static string Prefix(string title)
        {
            // Префикс в зависимости от типа контрагента
            string prefix = MainWindow.M.IsAgent ? "Изготовление детали " : "Деталь ";

            string? value = title;

            // 1. Добавляем префикс
            value = prefix + value;

            // 2. Удаляем название металла (первое совпадение)
            foreach (Metal metal in MainWindow.M.Metals)
            {
                if (string.IsNullOrEmpty(metal.Name)) continue;

                int index = value.IndexOf(metal.Name, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    value = value.Remove(index, metal.Name.Length);
                    break;
                }
            }

            // 3. Обрезаем
            value = Regex.Replace(value, @"\.[a-z]{2,5}$", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\s*\([^)]+\)\s*$", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\s+[sn]\d+(?:\.\d+)?\b", "", RegexOptions.IgnoreCase);

            // 4. Убираем лишние пробелы
            return value.Trim();
        }
    }
}