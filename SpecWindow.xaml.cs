using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
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

        public ObservableCollection<string> Providers { get; } = new() {
            "ООО ЛАЗЕРФЛЕКС","ООО ПРОВЭЛД","ООО ПК ЛАЗЕРФЛЕКС","ИП МЕШЕРОНОВА" };

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

            Agent.Text = TargetCustomer.Agent ? "ИП" : "ООО";
            EndDate.Text = $"до {MainWindow.M.EndDate()?.ToString("d")}";
            Delivery.Text = MainWindow.M.HasDelivery is not false ?
                $"Доставка производится силами Поставщика до склада Покупателя, расположенного по адресу: {TargetCustomer.Address}."
                : "Cамовывоз со склада Поставщика по адресу: Ленинградская область, Всеволожский район, Колтуши, деревня Мяглово, ул. Дорожная, уч. 4Б.";
        }

        private void Create_Spec(object sender, RoutedEventArgs e) { Create_Spec(OutputPath); }
        public void Create_Spec(string outputPath)
        {
            //генерируем имя файла спецификации
            outputPath = $"{Path.GetDirectoryName(outputPath)}\\{MainWindow.M.Order.Text} {MainWindow.M.CustomerDrop.Text} - спецификация № {CurrentTemplate.Number}.pdf";

            //проверяем, не открыт ли уже такой файл
            outputPath = GetAvailableFilePath(outputPath);

            //создаем безопасный файл в папке с КП
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40, Unit.Point);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    // Основное содержимое
                    page.Content().Column(content =>
                    {
                        content.Spacing(10);

                        // Заголовок
                        content.Item().PaddingLeft(280).Text($"{CurrentTemplate.Header}").AlignRight().Bold();

                        content.Item().PaddingTop(30).Text($"СПЕЦИФИКАЦИЯ № {CurrentTemplate.Number} от {DateTime.Now:dd MMMM yyyy} г.").AlignCenter().Bold();

                        float totalSum = 0;

                        // Таблица товаров
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(20);  // №
                                columns.RelativeColumn(150); // Наименование товара
                                columns.RelativeColumn(50);  // Количество, шт
                                columns.RelativeColumn(60);  // Стоимость в руб., в т.ч. НДС 20%
                            });

                            // Заголовок таблицы
                            table.Header(header =>
                            {
                                StyleHeaderCell(header.Cell(), "№");
                                StyleHeaderCell(header.Cell(), "Наименование товара");
                                StyleHeaderCell(header.Cell(), "Количество, шт");
                                StyleHeaderCell(header.Cell(), "Стоимость в руб., в т.ч. НДС 20%");
                            });

                            // Данные
                            if (!MainWindow.M.isAssemblyOffer)
                            {
                                if (MainWindow.M.Parts.Count > 0)
                                    for (int i = 0; i < MainWindow.M.Parts.Count; i++)
                                    {
                                        var part = MainWindow.M.Parts[i];
                                        totalSum += part.Total;

                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text((i + 1).ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(part.Title ?? ""));
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Count.ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(part.Total.ToString("N2"));
                                    }

                                ObservableCollection<Detail> details = new(MainWindow.M.ProductModel.Product.Details.Where(d => !d.IsComplect));
                                if (details.Count > 0)
                                    for (int i = 0; i < details.Count; i++)
                                    {
                                        Detail detail = details[i];
                                        totalSum += detail.Total;

                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text((MainWindow.M.Parts.Count + i + 1).ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).Text(Prefix(detail.Title ?? ""));
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Count.ToString());
                                        table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(detail.Total.ToString("N2"));
                                    }

                                if (MainWindow.M.HasDelivery is true)
                                {
                                    float deliveryTotal = (float)(MainWindow.M.Delivery  * MainWindow.M.DeliveryRatio * MainWindow.M.Ratio);
                                    totalSum += deliveryTotal;

                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text("Доставка");
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(MainWindow.M.DeliveryRatio.ToString());
                                    table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(deliveryTotal.ToString("N2"));
                                }
                            }

                            // Итоговая строка
                            table.Cell().ColumnSpan(3).Border(1).BorderColor(Colors.Black);

                            table.Cell().Border(1).BorderColor(Colors.Black)
                                .Padding(4).AlignCenter().Text(totalSum.ToString("N2")).Bold();
                        });

                        // Блок условий
                        content.Item().Row(row =>
                        {
                            row.RelativeItem().Column(center =>
                            {
                                string totalLine = NumberToWordsHelper.NumberToWords(totalSum);

                                // Извлекаем часть до "рублей"
                                int rubIndex = totalLine.IndexOf("рублей");
                                string rubText = rubIndex > 0 ? totalLine.Substring(0, rubIndex).Trim() : totalLine;

                                // Извлекаем копейки
                                int kopStart = totalLine.IndexOf("копеек");
                                string kopValue = "00";
                                if (kopStart > 0)
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(totalLine.Substring(kopStart), @"\d+");
                                    kopValue = match.Success ? match.Value.PadLeft(2, '0') : "00";
                                }

                                // Формируем итоговую строку
                                center.Item().Text($"ИТОГО: {totalSum:N0} ({rubText}) рублей {kopValue} коп., в т.ч. НДС 20%").Bold();

                                center.Item().PaddingTop(15).Text("Срок поставки: " + EndDate.Text);

                                center.Item().PaddingTop(5).Text(Delivery.Text);

                                center.Item().PaddingVertical(5).Text($"Условия оплаты: {CurrentTemplate.Terms}");
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

                                left.Item().Layers(layers =>
                                {
                                    layers.PrimaryLayer().Height(120);

                                    layers.Layer().Column(column =>
                                    {
                                        column.Item().Row(row =>
                                        {
                                            row.ConstantItem(30);
                                            row.ConstantItem(120).Image(GetImageStream("Metal_Code.Images.print1.jpg")).FitWidth();
                                        });
                                    });

                                    layers.Layer().Column(column =>
                                    {
                                        column.Item().Row(row =>
                                        {
                                            row.ConstantItem(40).Image(GetImageStream("Metal_Code.Images.signature1.jpg")).FitWidth();

                                            row.RelativeItem();

                                            row.RelativeItem().AlignBottom().Text("/ Мешеронова М.С.");
                                        });

                                        column.Item().Row(row =>
                                        {
                                            row.RelativeItem().AlignTop().LineHorizontal(1).LineColor(Colors.Black);
                                        });
                                    });
                                });
                            });

                            // Покупатель
                            row.RelativeItem().PaddingHorizontal(50).Column(right =>
                            {
                                right.Item().Text("ПОКУПАТЕЛЬ").Bold();
                                right.Item().Text($"{Agent.Text} {TargetCustomer.Name}");

                                right.Item().PaddingVertical(20).Row(row =>
                                {
                                    // Линия
                                    row.RelativeItem().AlignBottom().LineHorizontal(1).LineColor(Colors.Black);

                                    // Текст подписи
                                    row.RelativeItem().AlignBottom().Text($"/ {CurrentTemplate.Buyer}");
                                });
                            });
                        });
                    });  
                });
            }).GeneratePdf(outputPath);

            using ManagerContext db = new(MainWindow.M.IsLocal ? MainWindow.M.connections[0] : MainWindow.M.connections[1]);
            Customer? _customer = db.Customers.FirstOrDefault(x => x.Id == TargetCustomer.Id);
            if (_customer is not null)
            {
                _customer.SpecTemplate = CurrentTemplate;
                db.SaveChanges();
            }

            MainWindow.M.StatusBegin($"Создана спецификация для текущего расчета: {MainWindow.M.Order.Text} {TargetCustomer.Name}");

            Close();
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
            if (string.IsNullOrEmpty(filePath) || Path.GetExtension(filePath).ToLower() != ".pdf")
                throw new ArgumentException("Путь должен вести к PDF-файлу.");

            string? directory = Path.GetDirectoryName(filePath) ?? throw new ArgumentException("Путь должен вести к PDF-файлу.");
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);
            string newFilePath = filePath;

            int counter = 1;

            while (true)
            {
                try
                {
                    // Пытаемся открыть файл на запись в режиме исключения
                    using FileStream fs = new(newFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                    
                    fs.Close();     // Закрываем, если получилось открыть
                    break;          // Успех — файл доступен
                }
                catch (IOException)
                {
                    // Файл занят — пробуем следующее имя
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

        private static Stream GetImageStream(string resourceName)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(resourceName);

            return stream ?? throw new InvalidOperationException($"Ресурс '{resourceName}' не найден. Проверьте имя и действие при сборке.");
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

    public class SpecTemplate : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string header = "Приложение № 1 к договору поставки №";
        public string Header
        {
            get => header;
            set
            {
                if (header != value)
                {
                    header = value;
                    OnPropertyChanged(nameof(Header));
                }
            }
        }

        private int number = 1;
        public int Number
        {
            get => number;
            set
            {
                if (number != value)
                {
                    number = value;
                    OnPropertyChanged(nameof(Number));
                }
            }
        }

        private string terms = "100% предоплата.";
        public string Terms
        {
            get => terms;
            set
            {
                if (terms != value)
                {
                    terms = value;
                    OnPropertyChanged(nameof(Terms));
                }
            }
        }

        private string provider = "ООО ЛАЗЕРФЛЕКС";
        public string Provider
        {
            get => provider;
            set
            {
                if (provider != value)
                {
                    provider = value;
                    OnPropertyChanged(nameof(Provider));
                }
            }
        }

        private string buyer = string.Empty;
        public string Buyer
        {
            get => buyer;
            set
            {
                if (buyer != value)
                {
                    buyer = value;
                    OnPropertyChanged(nameof(Buyer));
                }
            }
        }

        public SpecTemplate() { }
    }
}