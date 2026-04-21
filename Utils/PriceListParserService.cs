using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Metal_Code.Utils
{
    public class PriceListParserService
    {
        // Ключевые слова для определения секций категорий (используются ТОЛЬКО для поиска в сырых данных)
        private static readonly HashSet<string> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "АРМАТУРА", "УГОЛОК", "КРУГ", "КВАДРАТ", "ПОЛОСА", "ШВЕЛЛЕР",
            "ЛИСТ", "ТРУБЫ", "БАЛКИ", "ПРОФИЛЬ", "СЕТКА", "ЭЛЕКТРОДЫ", "ПЕРЕХОДЫ",
            "СТАЛЬ ЛИСТОВАЯ", "СТАЛЬ СОРТ", "ПРОВОЛОКА", "КАЛИБРОВКА", "ФАСОН",
            "ЦВЕТНОЙ ПРОКАТ", "АЛЮМИНИЕВЫЙ", "МЕДНЫЙ", "ЛАТУННЫЙ", "БРОНЗОВЫЙ", "ДЮРАЛЕВЫЙ"
        };

        // Подзаголовки таблиц, которые нужно пропускать
        private static readonly HashSet<string> SubHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "марка", "диаметр", "размер", "толщина", "стенка", "хар-ка",
            "ед.изм", "цена", "наименование", "полка", "ширина", "высота", "технические характеристики"
        };

        /// <summary>
        /// Парсит Excel-файл прайс-листа и возвращает коллекцию позиций.
        /// </summary>
        public async Task<List<PriceListItem>> ParseAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Файл прайса не найден", filePath);

            // Регистрация кодировки для корректного чтения кириллицы в старых .xls
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            return await System.Threading.Tasks.Task.Run(() =>
            {
                var items = new List<PriceListItem>();
                using var stream = File.OpenRead(filePath);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                // Нормализованная категория (значение из CategoryMap, а не сырая строка)
                string currentCategory = "Не определено";

                do // Читаем все листы книги
                {
                    while (reader.Read())
                    {
                        // Извлекаем значения двух горизонтальных блоков
                        string c0 = reader.GetValue(0)?.ToString()?.Trim() ?? "";
                        string c1 = reader.GetValue(1)?.ToString()?.Trim() ?? "";
                        string c2 = reader.GetValue(2)?.ToString()?.Trim() ?? "";
                        string c3 = reader.GetValue(3)?.ToString()?.Trim() ?? "";

                        string c5 = reader.GetValue(5)?.ToString()?.Trim() ?? "";
                        string c6 = reader.GetValue(6)?.ToString()?.Trim() ?? "";
                        string c7 = reader.GetValue(7)?.ToString()?.Trim() ?? "";
                        string c8 = reader.GetValue(8)?.ToString()?.Trim() ?? "";

                        // 🔥 1. Проверяем, является ли строка ЗАГОЛОВКОМ СЕКЦИИ (левый блок)
                        if (IsCategoryHeader(c0, c1, c2, c3))
                        {
                            // Нормализуем категорию через словарь (если есть) или оставляем как есть
                            currentCategory = NormalizeCategory(c0);
                            continue;
                        }

                        // 🔥 2. Проверяем заголовок секции в правом блоке (колонка 5)
                        if (IsCategoryHeader(c5, c6, c7, c8))
                        {
                            currentCategory = NormalizeCategory(c5);
                            continue;
                        }

                        // 3. Парсим данные из обоих блоков
                        ProcessBlock(currentCategory, c0, c1, c2, c3, items);
                        ProcessBlock(currentCategory, c5, c6, c7, c8, items);
                    }
                } while (reader.NextResult());

                return items;
            });
        }

        /// <summary>
        /// Определяет, является ли строка заголовком категории.
        /// Признаки: колонка 0 содержит текст, а 1-3 пустые или содержат служебные слова.
        /// </summary>
        private static bool IsCategoryHeader(string c0, string c1, string c2, string c3)
        {
            if (string.IsNullOrWhiteSpace(c0))
                return false;

            // Если следующие колонки пустые или содержат только служебные слова → это заголовок
            bool nextColumnsEmpty = string.IsNullOrWhiteSpace(c1) &&
                                   string.IsNullOrWhiteSpace(c2) &&
                                   string.IsNullOrWhiteSpace(c3);

            // Или если содержат только подзаголовки (Марка, диаметр и т.д.)
            bool nextColumnsAreSubHeaders = SubHeaderKeywords.Any(kw =>
                (c1?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (c2?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (c3?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true));

            // Заголовок должен содержать хотя бы одно ключевое слово категории
            bool containsCategoryKeyword = CategoryKeywords.Any(kw =>
                c0.Contains(kw, StringComparison.OrdinalIgnoreCase));

            return (nextColumnsEmpty || nextColumnsAreSubHeaders) && containsCategoryKeyword;
        }

        /// <summary>
        /// Нормализует название категории через словарь.
        /// Если не найдено → возвращает исходное название.
        /// </summary>
        private static string NormalizeCategory(string rawCategory)
        {
            if (string.IsNullOrWhiteSpace(rawCategory))
                return "Не определено";

            // Ищем первое совпадение по ключевому слову
            var matched = PriceAggregator.CategoryMap.FirstOrDefault(kvp =>
                rawCategory.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

            // Если нашли в словаре → возвращаем нормализованное значение
            // Иначе → возвращаем очищенное исходное название
            return matched.Value ?? rawCategory.Trim();
        }

        private void ProcessBlock(string category, string name, string dim, string unit, string priceStr, List<PriceListItem> list)
        {
            // Пропускаем пустые строки и подзаголовки
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(priceStr)) return;
            if (SubHeaderKeywords.Any(kw => name.Contains(kw, StringComparison.OrdinalIgnoreCase))) return;

            if (!TryExtractPrice(priceStr, out decimal price)) return;

            list.Add(new PriceListItem
            {
                Category = category, // Уже нормализованная категория
                ProductName = name,
                Dimensions = dim,
                Unit = unit,
                Price = price,
                // Цена за кг: для "т" и "теор.т" делим на 1000
                PricePerKg = unit.Contains("т", StringComparison.OrdinalIgnoreCase) ? price / 1000m : 0m
            });
        }

        private static bool TryExtractPrice(string raw, out decimal price)
        {
            price = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var clean = raw.Replace(" ", "").Replace("\u00A0", "").Replace(",", ".");
            return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out price);
        }
    }

    public class PriceListItem
    {
        public string Category { get; set; } = string.Empty;      // Нормализованная категория ("Лист", "Труба профильная" и т.д.)
        public string ProductName { get; set; } = string.Empty;   // Марка / Наименование (Ст3, 09Г2С, АМГ5, М1...)
        public string Dimensions { get; set; } = string.Empty;    // Размеры / Толщина
        public string Unit { get; set; } = string.Empty;          // Ед. измерения (т, теор.т, шт, м2)
        public decimal Price { get; set; }                        // Цена за единицу из прайса
        public decimal PricePerKg { get; set; }                   // Рассчитанная цена за кг
        public string SourceFile { get; set; } = string.Empty;    // Имя файла-источника (для отладки)
    }

    public class MatchedPriceItem
    {
        public string CategoryName { get; set; } = string.Empty;  // "Лист", "Труба профильная" и т.д.
        public string Grade { get; set; } = string.Empty;         // Марка (Ст3, 09Г2С, АМГ5...)
        public decimal AveragePricePerKg { get; set; }            // Средняя цена за кг
        public int ItemsCount { get; set; }                       // Сколько позиций учтено
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
    }

    public class CategoryAveragePrice
    {
        public string CategoryName { get; set; } = string.Empty;  // "Лист", "Труба профильная" и т.д.
        public string Grade { get; set; } = string.Empty;         // Марка сплава (АМГ5, М1, Л63, 09Г2С, Ст3...)
        public decimal AveragePricePerKg { get; set; }            // Средняя цена за кг
        public int ItemsCount { get; set; }                       // Сколько позиций учтено в среднем
        public decimal MinPrice { get; set; }                     // Мин. цена за кг в группе
        public decimal MaxPrice { get; set; }                     // Макс. цена за кг в группе
    }

    public static class PriceAggregator
    {
        public static readonly Dictionary<string, string> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Основные категории для нормализации (не обязательно все)
            { "ЛИСТ", "Лист" },
            { "ТРУБЫ ЭЛЕКТРОСВАРНЫЕ КВАДРАТ", "Труба профильная" },
            { "ТРУБЫ ЭЛЕКТРОСВАРНЫЕ ПРЯМОУГ", "Труба профильная" },
            { "ТРУБЫ", "Труба круглая" },
            { "УГОЛОК", "Уголок" },
            { "ШВЕЛЛЕР", "Швеллер" },
            { "БАЛКИ ДВУТАВРОВЫЕ", "Двутавр" },
            { "КРУГ", "Круг" },
            { "ЛАТУННЫЙ ЛИСТ", "Латунный лист" },
            { "ЛАТУННАЯ ЛЕНТА", "Латунная лента" },
            { "МЕДНЫЙ ЛИСТ", "Медный лист" },
            { "АЛЮМИНИЕВЫЙ ЛИСТ", "Алюминиевый лист" },
            { "ДЮРАЛЕВЫЙ ЛИСТ", "Дюралевый лист" }
            // Можно добавить ещё, но не обязательно — парсер будет работать и без них
        };

        /// <summary>
        /// Агрегирует сырые данные парсера в средние цены по целевым категориям.
        /// </summary>
        public static List<CategoryAveragePrice> AggregateToAverages(List<PriceListItem> rawItems)
        {
            // Допустимые нормализованные категории (значения из CategoryMap)
            var validCategories = new HashSet<string>(CategoryMap.Values, StringComparer.OrdinalIgnoreCase);

            return rawItems
                // 1. Фильтруем только целевые категории (чёрные + цветные)
                .Where(i => validCategories.Contains(i.Category))
                // 2. Базовая валидация цены (защита от мусора и аномалий)
                .Where(i => i.PricePerKg > 0 && i.PricePerKg < 5000m)
                // 3. Исключаем "неконд", "н/обр", "уценка" — особенно важно для цветмета
                .Where(i => !i.ProductName.Contains("неконд", StringComparison.OrdinalIgnoreCase)
                         && !i.ProductName.Contains("н/обр", StringComparison.OrdinalIgnoreCase)
                         && !i.ProductName.Contains("уценка", StringComparison.OrdinalIgnoreCase))
                // 4. Группируем по категории + марке сплава
                .GroupBy(i => new
                {
                    i.Category,
                    // 🔥 Сначала извлекаем марку, затем нормализуем её под вашу БД
                    Grade = NormalizeGrade(ExtractGrade(i.ProductName))
                })
                // 5. Считаем статистику
                .Select(g => new CategoryAveragePrice
                {
                    CategoryName = g.Key.Category,
                    Grade = g.Key.Grade,
                    AveragePricePerKg = Math.Round(g.Average(x => x.PricePerKg), 2),
                    ItemsCount = g.Count(),
                    MinPrice = Math.Round(g.Min(x => x.PricePerKg), 2),
                    MaxPrice = Math.Round(g.Max(x => x.PricePerKg), 2)
                })
                // 6. Сортировка: приоритетные категории сначала
                .OrderByDescending(x => x.CategoryName switch
                {
                    "Лист" => 100,
                    "Труба профильная" => 90,
                    "Труба круглая" => 80,
                    "Уголок" => 70,
                    "Швеллер" => 60,
                    "Двутавр" => 50,
                    "Круг" => 45,
                    "Алюминиевый лист" => 40,
                    "Алюминиевый профиль" => 39,
                    "Медный лист" => 30,
                    "Медный профиль" => 29,
                    "Латунный лист" => 20,
                    "Латунный профиль" => 19,
                    "Бронзовый профиль" => 15,
                    "Дюралевый лист" => 10,
                    "Дюралевый профиль" => 9,
                    _ => 0
                })
                .ThenBy(x => x.Grade)
                .ToList();
        }

        /// <summary>
        /// Извлекает марку сплава из наименования продукта.
        /// </summary>
        private static string ExtractGrade(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName)) return "Не указана";

            // Цветмет: марки обычно в начале (АМГ5, М1, Л63, Д16Т, БрАЖ9-4)
            // Чёрные: Ст3, 09Г2С, Ст20, 40Х и т.д.
            var parts = productName.Split(new[] { ' ', ';', ',', '|', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0)
            {
                var first = parts[0].Trim().ToUpper();

                // Фильтруем служебные слова, которые не являются марками
                var skipWords = new[] { "ИМПОРТ", "СЕРТИФИКАТ", "УЦЕНКА", "ОЦИНК", "Н/ОБР", "НЕКОНД",
                                       "ОБР", "Г/К", "Х/К", "ТАГМЕТ", "ПЕЧНАЯ", "СВАРКА", "ФАСКА", "РЕЗ" };

                if (!skipWords.Any(kw => first.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                {
                    // Нормализуем марку: убираем лишние символы, оставляем буквы и цифры
                    var grade = new string(first.Where(c => char.IsLetterOrDigit(c) || c == 'Г' || c == 'С' || c == 'Х' || c == 'Т' || c == 'М' || c == 'Н' || c == 'Р' || c == 'А' || c == 'Б' || c == 'Д' || c == 'Л' || c == 'Ю' || c == 'Ц' || c == 'Ч' || c == 'Ш' || c == 'Щ' || c == 'Ъ' || c == 'Ы' || c == 'Ь' || c == 'Э' || c == 'Я' || c == 'Ё').ToArray());
                    return string.IsNullOrWhiteSpace(grade) ? "Не указана" : grade;
                }
            }
            return "Не указана";
        }

        // Маппинг марок из прайса → названия в вашей БД
        private static readonly Dictionary<string, string> GradeNormalizationMap = new(StringComparer.OrdinalIgnoreCase)
            {
                // Латунь
                { "Л63", "латунь" }, { "Л63М", "латунь" }, { "ЛС59-1", "латунь" }, { "ЛС59-1М", "латунь" },
                // Медь
                { "М1", "медь" }, { "М1Р", "медь" }, { "М2", "медь" }, { "М3", "медь" },
            };

        private static string NormalizeGrade(string rawGrade)
        {
            if (string.IsNullOrWhiteSpace(rawGrade)) return rawGrade;
            // Если марка есть в словаре → возвращаем нормализованное имя из БД
            return GradeNormalizationMap.TryGetValue(rawGrade, out var normalized) ? normalized : rawGrade;
        }
    }
}