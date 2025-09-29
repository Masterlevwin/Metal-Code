using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

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
                                columns.RelativeColumn(30);  // Работы
                                columns.RelativeColumn(35);  // Размеры
                                columns.RelativeColumn(20);  // №
                                columns.RelativeColumn(80);  // Наименование
                                columns.RelativeColumn(25);  // Кол-во
                                columns.RelativeColumn(30);  // Цена
                                columns.RelativeColumn(40);  // Стоимость
                            });

                            // Заголовок таблицы
                            table.Header(header =>
                            {
                                StyleHeaderCell(header.Cell(), "Материал");
                                StyleHeaderCell(header.Cell(), "Толщина");
                                StyleHeaderCell(header.Cell(), "Работы");
                                StyleHeaderCell(header.Cell(), "Размеры, мм");
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
                                if (AssemblyWindow.A.Assemblies.Count > 0)
                                    foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
                                    {
                                        totalSum += assembly.Total;

                                        table.Cell().ColumnSpan(4).Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}").Bold();
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(assembly.Title ?? "")).Bold();
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{assembly.Count}").Bold();
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{assembly.Price}").Bold();
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{assembly.Total}").Bold();
                                        row++;

                                        for (int p = 0; p < assembly.Particles.Count; p++)
                                        {
                                            Particle particle = assembly.Particles[p];
                                            Part? part = MainWindow.M.Parts.FirstOrDefault(p => p.Title == particle.Title);

                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part is not null ? part.Metal : "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part is not null ? part.Destiny.ToString() : "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part is not null ? part.Description : "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part is not null ? part.Accuracy : "");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                            table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(particle.Title ?? ""));
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
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(loosePart.Destiny.ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(loosePart.Description ?? "");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(loosePart.Accuracy ?? "");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(loosePart.Title ?? ""));
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{loosePart.Count}");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{loosePart.Price}");
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{loosePart.Total}");
                                        row++;
                                    }
                                }
                            }
                            // Иначе если есть нарезанные детали, вычисляем их общую стоимость, и оформляем их в КП
                            else if (MainWindow.M.Parts.Count > 0)
                                for (int i = 0; i < MainWindow.M.Parts.Count; i++)
                                {
                                    var part = MainWindow.M.Parts[i];
                                    totalSum += part.Total;

                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Metal);
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Destiny.ToString());
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Description ?? "");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Accuracy ?? "");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(part.Title ?? ""));
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Count.ToString());
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Price.ToString("N2"));
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Total.ToString("N2"));
                                    row++;
                                }

                            ObservableCollection<Detail> details = new(MainWindow.M.ProductModel.Product.Details.Where(d => !d.IsComplect));
                            if (details.Count > 0)
                                for (int i = 0; i < details.Count; i++)
                                {
                                    Detail detail = details[i];
                                    totalSum += detail.Total;

                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Metal);
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Destiny.ToString());
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Description ?? "");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Accuracy ?? "");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text($"{row}");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(detail.Title ?? ""));
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Count.ToString());
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Price.ToString("N2"));
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Total.ToString("N2"));
                                    row++;
                                }

                            if (MainWindow.M.HasDelivery is true)
                            {
                                float delivery = MainWindow.M.Delivery * MainWindow.M.Ratio;
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
                                left.Item().Text($"Материал: {(MainWindow.M.DetailControls[0].TypeDetailControls[0].HasMetal ?
                                    "Исполнителя" : "Заказчика (внимание: остатки давальческого материала забираются вместе с заказом, иначе эти остатки утилизируются!)")}").SemiBold();
                                
                                left.Item().PaddingVertical(5).Text($"Срок изготовления: {MainWindow.M.DateProduction.Text} раб/дней" +
                                    $"{(MainWindow.M.HasAssembly ? " (ЭКСПРЕСС)." : ".")}").SemiBold();

                                left.Item().PaddingVertical(5).Text("Условия оплаты: предоплата 100% по счету Исполнителя.");
                                
                                if (MainWindow.M.HasDelivery is true)
                                    left.Item().PaddingVertical(5).Text($"Порядок отгрузки: доставка силами Исполнителя по адресу: {MainWindow.M.Adress.Text}.");
                                else left.Item().PaddingVertical(5).Text("Порядок отгрузки: самовывоз со склада Исполнителя по адресу: Ленинградская область, Всеволожский район, " +
                                                "Колтуши, деревня Мяглово, ул. Дорожная, уч. 4Б.");
                                
                                left.Item().PaddingVertical(5).Text("Точность: H14/h14 ±IT14/2");

                                left.Item().PaddingVertical(5).Text($"Расшифровка работ: {descriptionWorks}");

                                left.Item().PaddingVertical(5).Text($"Примечание: {MainWindow.M.Comment.Text}").Bold();

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
    }
}