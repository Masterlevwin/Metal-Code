using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Metal_Code.Utils
{
    public class PriceListParserService
    {
        /// <summary>
        /// Парсит Excel-файл с использованием DataTable для точного доступа к ячейкам.
        /// </summary>
        public async Task<List<PriceListItem>> ParseAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Файл прайса не найден", filePath);

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            return await System.Threading.Tasks.Task.Run(() =>
            {
                var items = new List<PriceListItem>();
                using var stream = File.OpenRead(filePath);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                var result = reader.AsDataSet();
                if (result == null || result.Tables.Count == 0) return items;

                DataTable table = result.Tables[0];
                int rowCount = table.Rows.Count;

                string currentCategory = "Не определено";
                int segmentStartRow = 0;

                for (int i = 0; i < rowCount; i++)
                {
                    DataRow row = table.Rows[i];

                    string c0 = row[0]?.ToString()?.Trim() ?? "";
                    string c1 = row[1]?.ToString()?.Trim() ?? "";
                    string c2 = row[2]?.ToString()?.Trim() ?? "";
                    string c3 = row[3]?.ToString()?.Trim() ?? "";

                    // Пустая строка в левом блоке → счётчик
                    bool leftRowIsEmpty = string.IsNullOrWhiteSpace(c0) &&
                                         string.IsNullOrWhiteSpace(c1) &&
                                         string.IsNullOrWhiteSpace(c2) &&
                                         string.IsNullOrWhiteSpace(c3);
                    if (leftRowIsEmpty)
                    {
                        // 🔄 Обратный парсинг правого блока с начала сегмента
                        for (int j = segmentStartRow; j <= i; j++)
                        {
                            DataRow rightRow = table.Rows[j];
                            string rc5 = rightRow[5]?.ToString()?.Trim() ?? "";
                            string rc6 = rightRow[6]?.ToString()?.Trim() ?? "";
                            string rc7 = rightRow[7]?.ToString()?.Trim() ?? "";
                            string rc8 = rightRow[8]?.ToString()?.Trim() ?? "";

                            // Во время обратного парсинга заголовки в правом блоке ОБНОВЛЯЮТ категорию
                            if (IsCategoryHeader(rc5, rc6, rc7, rc8) && !IsSkipRow(rc5))
                            {
                                currentCategory = NormalizeCategoryName(rc5);
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(rc5) && !IsSubHeader(rc5) && !string.IsNullOrWhiteSpace(rc8))
                            {
                                ProcessDataLine(currentCategory, rc5, rc6, rc7, rc8, items);
                            }
                        }
                        segmentStartRow = i;
                    }
                    // 🔍 Заголовок ТОЛЬКО в левом блоке
                    else if (IsCategoryHeader(c0, c1, c2, c3) && !IsSkipRow(c0))
                    {
                        currentCategory = NormalizeCategoryName(c0);
                    }
                    else
                    {
                        // Парсим данные левого блока с ТЕКУЩЕЙ категорией
                        if (!string.IsNullOrWhiteSpace(c0) && !IsSubHeader(c0) && !string.IsNullOrWhiteSpace(c3))
                        {
                            ProcessDataLine(currentCategory, c0, c1, c2, c3, items);
                        }
                    }
                }
                return items;
            });
        }

        /// <summary>
        /// Определяет, является ли набор ячеек заголовком категории.
        /// Признак: 1-я ячейка содержит текст, остальные 3 пустые.
        /// </summary>
        private static bool IsCategoryHeader(string c0, string c1, string c2, string c3)
        {
            if (string.IsNullOrWhiteSpace(c0)) return false;

            // И соседние ячейки должны быть пустыми (имитация объединенной ячейки)
            bool isNextEmpty = string.IsNullOrWhiteSpace(c1) &&
                               string.IsNullOrWhiteSpace(c2) &&
                               string.IsNullOrWhiteSpace(c3);

            return isNextEmpty;
        }

        /// <summary>
        /// Фильтрует служебные строки шапки файла.
        /// 🔥 ИСПРАВЛЕНО: Убраны "." и ":", чтобы не блокировать категории типа "ТРУБЫ НЕРЖАВ."
        /// </summary>
        private static bool IsSkipRow(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;

            var skipPatterns = new[]
            {
                "прайс-лист", "от ", "2026",
                "цветной прокат", "чёрный прокат", "черный прокат",
                "http", "www", "@", "(812)", "+7", "333-20", "600-70",
                "|", "_"
            };

            var lower = text.ToLowerInvariant();
            return skipPatterns.Any(p => lower.Contains(p.ToLowerInvariant()));
        }

        /// <summary>
        /// Нормализует название категории (удаляет "продолжение").
        /// </summary>
        private static string NormalizeCategoryName(string rawCategory)
        {
            if (string.IsNullOrWhiteSpace(rawCategory)) return "Не определено";

            var normalized = rawCategory.Trim();
            var continuationPatterns = new[]
            {
                "(продолжение)", " (продолжение)", "[продолжение]", " [продолжение]",
                "(прод.)", " (прод.)", "[прод.]", " [прод.]",
                "- продолжение", " - продолжение", "– продолжение",
                "(ПРОДОЛЖЕНИЕ)", " (ПРОДОЛЖЕНИЕ)"
            };

            foreach (var pattern in continuationPatterns)
            {
                if (normalized.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(0, normalized.Length - pattern.Length).Trim();
                    break;
                }
            }

            return string.IsNullOrWhiteSpace(normalized) ? "Не определено" : normalized;
        }

        /// <summary>
        /// Парсит строку данных и добавляет в список.
        /// 🔥 Умеет пересчитывать цену трубы из погонных метров в кг.
        /// </summary>
        private static void ProcessDataLine(string category, string name, string dim, string unit, string priceStr, List<PriceListItem> list)
        {
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(priceStr)) return;
            if (!TryExtractPrice(priceStr, out decimal price)) return;

            decimal pricePerKg = 0;
            string cleanUnit = unit?.Trim().ToLowerInvariant() ?? "";

            // 🔥 1. Трубы в погонных метрах
            if (cleanUnit.Contains("м") && !cleanUnit.Contains("м2"))
            {
                string cleanName = name?.Trim() ?? "";
                // Извлекаем наружный диаметр из начала строки (например: "6.0 AISI...", "219.1 AISI...")
                var diameterMatch = Regex.Match(cleanName, @"^([\d.,]+)");

                if (diameterMatch.Success)
                {
                    var diameterStr = diameterMatch.Groups[1].Value.Replace(",", ".");
                    if (decimal.TryParse(diameterStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal diameter) && diameter > 0)
                    {
                        // Извлекаем толщину стенки из колонки Dimensions (берем первое значение, если указан диапазон)
                        var thicknessRaw = dim?.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrDefault()?.Trim().Replace(",", ".");

                        if (!string.IsNullOrEmpty(thicknessRaw) &&
                            decimal.TryParse(thicknessRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal thickness) &&
                            thickness > 0 && diameter > thickness)
                        {
                            // Формула теоретического веса 1 м.п. трубы из нержавеющей стали: (D - t) * t * 0.0249
                            decimal weightPerMeter = (diameter - thickness) * thickness * 0.0249m;

                            if (weightPerMeter > 0)
                            {
                                pricePerKg = price / weightPerMeter;
                            }
                        }
                    }
                }
            }
            //  2. Товары в тоннах (листы, круг, бесшовные трубы и т.д.)
            else if (cleanUnit.Contains("т"))
            {
                pricePerKg = price / 1000m;
            }

            list.Add(new PriceListItem
            {
                Category = category,
                ProductName = name,
                Dimensions = dim,
                Unit = unit,
                Price = price,
                PricePerKg = pricePerKg // Теперь будет > 0 для труб в метрах
            });
        }

        /// <summary>
        /// Проверяет, является ли текст подзаголовком таблицы (Марка, диаметр...)
        /// </summary>
        private static bool IsSubHeader(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var subHeaders = new[]
            {
                "марка", "диаметр", "размер", "толщина", "стенка", "хар-ка",
                "ед.изм", "цена", "наименование", "полка", "ширина", "высота",
                "технические характеристики"
            };

            return subHeaders.Any(h => text.Contains(h, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Извлекает цену из строки.
        /// </summary>
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
        public string Category { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Dimensions { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal PricePerKg { get; set; }
    }

    public class CategoryAveragePrice
    {
        public string CategoryName { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public decimal AveragePricePerKg { get; set; }
        public int ItemsCount { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal PriceWithMarkup { get; set; }
    }

    public class MatchedPriceItem
    {
        public string CategoryName { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public decimal AveragePricePerKg { get; set; }
        public int ItemsCount { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal PriceWithMarkup { get; set; }
    }

    public static class PriceAggregator
    {
        /// <summary>
        /// Агрегирует сырые данные в средние цены.
        /// </summary>
        public static List<CategoryAveragePrice> AggregateToAverages(List<PriceListItem> rawItems)
        {
            return rawItems
                // Базовая валидация цены
                .Where(i => i.PricePerKg > 0 && i.PricePerKg < 5000m)
                // Исключаем "неконд", "н/обр", "уценка"
                .Where(i => !i.ProductName.Contains("неконд", StringComparison.OrdinalIgnoreCase)
                         && !i.ProductName.Contains("уценка", StringComparison.OrdinalIgnoreCase))
                // Группируем по категории + марке сплава
                .GroupBy(i => new
                {
                    i.Category,
                    Grade = NormalizeGrade(ExtractGrade(i.ProductName, i.Category))
                })
                .Where(g => g.Key.Grade != null)
                // Считаем статистику
                .Select(g => new CategoryAveragePrice
                {
                    CategoryName = g.Key.Category,
                    Grade = g.Key.Grade,
                    AveragePricePerKg = Math.Round(g.Average(x => x.PricePerKg), 2),
                    ItemsCount = g.Count(),
                    MinPrice = Math.Round(g.Min(x => x.PricePerKg), 2),
                    MaxPrice = Math.Round(g.Max(x => x.PricePerKg), 2),
                    PriceWithMarkup = CalculatePriceWithMarkup(g.Average(x => x.PricePerKg), g.Key.Category)
                })
                // Сортировка
                .OrderByDescending(x => GetCategoryPriority(x.CategoryName))
                .ThenBy(x => x.Grade)
                .ToList();
        }

        private static bool IsNonFerrous(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return false;
            var nonFerrous = new[] { "алюминиевый", "алюминиевая", "медный", "медная", "латунный", "латунная", "бронзовый", "бронзовая", "дюралевый", "дюралевая" };
            return nonFerrous.Any(kw => categoryName.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        private static decimal CalculatePriceWithMarkup(decimal basePrice, string categoryName)
        {
            var markup = IsNonFerrous(categoryName) ? 1.25m : 1.20m;
            return Math.Round(basePrice * markup, 2);
        }

        private static int GetCategoryPriority(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return 0;
            var c = category.ToUpperInvariant();

            if (c.Contains("ТРУБ") && c.Contains("НЕРЖ")) return 95;
            if (c.Contains("ЛИСТ") && !c.Contains("АЛЮМИНИЕВЫЙ") && !c.Contains("МЕДНЫЙ") && !c.Contains("ЛАТУННЫЙ") && !c.Contains("ДЮРАЛЕВЫЙ")) return 100;
            if (c.Contains("ТРУБЫ") && (c.Contains("КВАДРАТ") || c.Contains("ПРЯМОУГ"))) return 90;
            if (c.Contains("ТРУБЫ") && !c.Contains("КВАДРАТ") && !c.Contains("ПРЯМОУГ")) return 80;
            if (c.Contains("УГОЛОК")) return 70;
            if (c.Contains("ШВЕЛЛЕР")) return 60;
            if (c.Contains("ДВУТАВР")) return 50;
            if (c.Contains("КРУГ") && !c.Contains("АЛЮМИНИЕВЫЙ") && !c.Contains("МЕДНЫЙ") && !c.Contains("ЛАТУННЫЙ") && !c.Contains("БРОНЗОВЫЙ") && !c.Contains("ДЮРАЛЕВЫЙ")) return 45;
            if (c.Contains("АЛЮМИНИЕВЫЙ ЛИСТ") || c.Contains("АЛЮМИНИЕВАЯ ПЛИТА")) return 40;
            if (c.Contains("АЛЮМИНИЕВЫЙ")) return 39;
            if (c.Contains("МЕДНЫЙ ЛИСТ") || c.Contains("МЕДНАЯ ЛЕНТА")) return 30;
            if (c.Contains("МЕДНЫЙ")) return 29;
            if (c.Contains("ЛАТУННЫЙ ЛИСТ") || c.Contains("ЛАТУННАЯ ЛЕНТА")) return 20;
            if (c.Contains("ЛАТУННЫЙ")) return 19;
            if (c.Contains("БРОНЗОВЫЙ")) return 15;
            if (c.Contains("ДЮРАЛЕВЫЙ ЛИСТ") || c.Contains("ДЮРАЛЕВАЯ ПЛИТА")) return 10;
            if (c.Contains("ДЮРАЛЕВЫЙ")) return 9;
            if (c.Contains("АРМАТУРА")) return 5;
            if (c.Contains("ПЕРЕХОДЫ")) return 4;
            if (c.Contains("ПРОВОЛОКА")) return 3;
            if (c.Contains("СЕТКА")) return 2;
            if (c.Contains("ЭЛЕКТРОДЫ")) return 1;
            return 0;
        }

        private static string ExtractGrade(string productName, string category = "")
        {
            if (string.IsNullOrWhiteSpace(productName))
                return ExtractGradeFromCategory(category);

            var cleaned = productName.Trim().ToUpperInvariant();

            // 🔥 1. Сначала ищем модификаторы поверхности в исходной строке
            string surfaceModifier = "";
            if (cleaned.Contains("ШЛИФ") || cleaned.Contains("4N") || cleaned.Contains("NO1"))
                surfaceModifier = "шлиф";
            else if (cleaned.Contains("ЗЕРК") || cleaned.Contains("BA") || cleaned.Contains("ЗЕРКАЛЬН"))
                surfaceModifier = "зерк";
            else if (cleaned.Contains("МАТ") || cleaned.Contains("2B"))
                surfaceModifier = "матовый";

            // Удаляем префиксы
            var prefixesToRemove = new[] { "н/обр", "н/обр ", "ОБР", "ОБР ", "неконд", "неконд ", "уценка", "уценка ", "импорт", "импорт ", "сертификат", "сертификат " };
            foreach (var prefix in prefixesToRemove)
            {
                if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(prefix.Length).Trim();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(cleaned))
                return ExtractGradeFromCategory(category) + surfaceModifier;

            // Обработка оцинковки
            if (cleaned.StartsWith("ZN", StringComparison.OrdinalIgnoreCase))
            {
                var parts = cleaned.Split(new[] { ' ', ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && parts[0].StartsWith("ZN", StringComparison.OrdinalIgnoreCase))
                    return parts[0].ToUpperInvariant() + surfaceModifier;
            }

            //  2. Regex для извлечения AISI XXX (ловит и с префиксом, и без)
            var aisiMatch = Regex.Match(cleaned, @"(?:AISI\s*)?(\d{3}[A-Z]*)", RegexOptions.IgnoreCase);
            if (aisiMatch.Success)
            {
                var aisiNum = aisiMatch.Groups[1].Value.ToUpperInvariant();
                return aisiNum + surfaceModifier;
            }

            // 🔥 3. ПРОВЕРКА РАЗМЕРНЫХ ПАТТЕРНОВ (вызывает ваши методы!)
            // Проверяем, является ли строка размером (например, "100х100", "60x40", "25х3")
            if (IsDimensionPattern(cleaned))
            {
                // Если категория сортовая (уголок, швеллер, труба и т.д.) → ст3
                if (IsStructuralCategory(category))
                    return "ст3" + surfaceModifier;

                // Иначе извлекаем марку из названия категории
                return ExtractGradeFromCategory(category) + surfaceModifier;
            }

            // Стандартная логика для остальных марок
            var gradeParts = cleaned.Split(new[] { ' ', ';', ',', '|', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (gradeParts.Length == 0)
                return ExtractGradeFromCategory(category) + surfaceModifier;

            var skipWords = new[] {
        "Г/К", "Х/К", "ТАГМЕТ", "ПЕЧНАЯ", "СВАРКА", "ФАСКА", "РЕЗ", "ОЦИНК", "М", "ПАС", "КР", "ПРОМ", "КЛАСС", "ГОСТ", "ТУ", "ИМП", "СЕРТ",
        "СТАЛЬ", "ЛИСТ", "ЛИСТОВАЯ", "ПРОКАТ", "КРУГ", "ТРУБА", "ТРУБЫ", "УГОЛОК", "ШВЕЛЛЕР", "БАЛКИ", "ДВУТАВР", "ПЛИТА", "ШИНА", "ЛЕНТА",
        "ШЕСТИГРАННИК", "ПРОФИЛЬ", "АРМАТУРА", "ПРОВОЛОКА", "СЕТКА", "ЭЛЕКТРОДЫ", "ПЕРЕХОДЫ", "КАЛИБРОВКА", "ФАСОН", "СОРТ", "КОНСТР",
        "НИЗКОЛЕГ", "НИЗКОЛЕГИР", "ОБЫЧ", "КАЧЕСТВА", "ОЦИНКОВАННАЯ", "РИФЛЕНЫЙ", "ПРОСЕЧНО", "ВЫТЯЖНОЙ", "ВОДОГАЗОПРОВ", "ЭЛЕКТРОСВАРНЫЕ",
        "КВАДРАТ", "ПРЯМОУГ", "КВАДРАТНЫЕ", "ПРЯМОУГОЛЬНЫЕ", "КРУГЛЫЕ", "ГНУТЫЙ", "ДВУТАВРОВЫЕ", "АЛЮМИНИЕВЫЙ", "АЛЮМИНИЕВАЯ", "МЕДНЫЙ",
        "МЕДНАЯ", "ЛАТУННЫЙ", "ЛАТУННАЯ", "БРОНЗОВЫЙ", "БРОНЗОВАЯ", "ДЮРАЛЕВЫЙ", "ДЮРАЛЕВАЯ", "ЦВЕТНОЙ", "ЧЁРНЫЙ", "ЧЕРНЫЙ", "ПРОДОЛЖЕНИЕ",
    };

            string? candidate = null;
            foreach (var part in gradeParts)
            {
                var trimmed = part.Trim().ToUpperInvariant();
                if (skipWords.Any(kw => trimmed.Equals(kw, StringComparison.OrdinalIgnoreCase))) continue;
                if (decimal.TryParse(trimmed.Replace(".", ","), out _)) continue;
                candidate = trimmed;
                break;
            }

            if (string.IsNullOrWhiteSpace(candidate))
                return ExtractGradeFromCategory(category) + surfaceModifier;

            var grade = new string(candidate.Where(c => char.IsLetterOrDigit(c) || "ГСХТМНРАБДЛЮЦЧШЩЪЫЬЭЯЁ".Contains(c)).ToArray());
            return (string.IsNullOrWhiteSpace(grade) ? ExtractGradeFromCategory(category) : grade) + surfaceModifier;
        }

        private static bool IsDimensionPattern(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // 🔥 Все возможные варианты разделителей размеров:
            // - Кириллические: Х (заглавная), х (строчная)
            // - Латинские: X (заглавная), x (строчная)  
            // - Знак умножения: ×
            var separators = new[] { 'Х', 'х', 'X', 'x', '×' };

            // Проверяем, содержит ли текст любой из разделителей
            if (!separators.Any(sep => text.Contains(sep)))
                return false;

            // Разбиваем по всем разделителям сразу
            var parts = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            // Если все части — числа, это размер (например, "100х100", "60x40", "180Х50")
            return parts.Length >= 2 && parts.All(p => decimal.TryParse(p.Trim().Replace(".", ","), out _));
        }

        private static bool IsStructuralCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return false;

            var cat = category.ToUpperInvariant();

            var structuralKeywords = new[]
            {
        "УГОЛОК", "ШВЕЛЛЕР", "ТРУБ", "КРУГ", "КВАДРАТ", "ПОЛОСА",
        "ШЕСТИГРАННИК", "ПРОФИЛЬ", "БАЛКИ", "ДВУТАВР", "АРМАТУРА",
        "ПРОВОЛОКА", "СЕТКА", "КАЛИБРОВКА", "ФАСОН"
            };

            return structuralKeywords.Any(kw => cat.Contains(kw));
        }

        private static string ExtractGradeFromCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return "Не указана";

            var cat = category.ToUpperInvariant();

            // 🔥 Приоритет: специальные типы сталей
            if (cat.Contains("ОЦИНК") || cat.Contains("ZN"))
                return "цинк";

            if (cat.Contains("Х/К") || cat.Contains("ХОЛОДНОКАТАН") || cat.Contains("ХК "))
                return "хк";

            if (cat.Contains("РИФЛ") || cat.Contains("РОМБ") || cat.Contains("ЧЕЧЕВ"))
                return "рифл";

            // Для цветных металлов
            if (cat.Contains("АЛЮМИНИЕВ"))
            {
                if (cat.Contains("ЛИСТ") || cat.Contains("ПЛИТА"))
                    return "алюминий";
                if (cat.Contains("ТРУБ"))
                    return "алюминий";
                if (cat.Contains("КРУГ") || cat.Contains("ПРОФИЛЬ"))
                    return "алюминий";
            }

            if (cat.Contains("МЕДН"))
                return "медь";

            if (cat.Contains("ЛАТУН"))
                return "латунь";

            if (cat.Contains("БРОНЗ"))
                return "бронза";

            if (cat.Contains("ДЮРАЛ"))
                return "дюраль";

            // По умолчанию для чёрных металлов
            return "ст3";
        }

        private static string? NormalizeGrade(string rawGrade)
        {
            if (string.IsNullOrWhiteSpace(rawGrade)) return rawGrade;

            // 🔥 Разделяем марку и модификатор
            var parts = rawGrade.Trim().ToLowerInvariant().Split(new[] { "шлиф", "зерк", "матовый" }, StringSplitOptions.None);
            var baseGrade = parts[0].ToUpperInvariant();
            var modifier = rawGrade.Length > baseGrade.Length ? rawGrade.Substring(baseGrade.Length).ToLowerInvariant() : "";

            var grade = baseGrade;

            // === НЕРЖАВЕЮЩИЕ СТАЛИ AISI ===

            // Русские марки → AISI
            if (grade.Contains("08Х18Н10") || grade.Contains("08Х18Н9"))
            {
                if (modifier == "шлиф") return "aisi304шлиф";
                if (modifier == "зерк") return "aisi304зерк";
                return "aisi304";
            }

            if (grade.Contains("12Х18Н10Т") || grade.Contains("12Х18Н9Т") || grade.Contains("08Х18Н10Т"))
            {
                if (modifier == "шлиф") return "aisi321шлиф";
                if (modifier == "зерк") return "aisi321зерк";
                return "aisi321";
            }

            if (grade.Contains("10Х17Н13М2Т") || grade.Contains("08Х17Н13М2Т") || grade.Contains("03Х17Н14М3"))
                return "aisi316";

            if (grade.Contains("03Х18Н11") || grade.Contains("03Х18Н10"))
                return "aisi304l";

            if (grade.Contains("20Х23Н18") || grade.Contains("10Х23Н18"))
                return "aisi310s";

            if (grade.Contains("08Х17") || grade.Contains("12Х17"))
            {
                if (modifier == "шлиф") return "aisi430шлиф";
                if (modifier == "зерк") return "aisi430зерк";
                return "aisi430";
            }

            // Regex для AISI XXX
            var aisiMatch = Regex.Match(grade, @"(?:AISI\s*)?(\d{3}[A-Z]*)", RegexOptions.IgnoreCase);
            if (aisiMatch.Success)
            {
                var aisiNum = aisiMatch.Groups[1].Value.ToUpperInvariant();

                if (aisiNum == "316L") return "aisi316";

                if (aisiNum == "304" || aisiNum == "304L")
                {
                    if (modifier == "шлиф") return "aisi304шлиф";
                    if (modifier == "зерк") return "aisi304зерк";
                    return aisiNum == "304L" ? "aisi304l" : "aisi304";
                }

                if (aisiNum == "316" || aisiNum == "316TI")
                    return "aisi316";

                if (aisiNum == "321")
                {
                    if (modifier == "шлиф") return "aisi321шлиф";
                    if (modifier == "зерк") return "aisi321зерк";
                    return "aisi321";
                }

                if (aisiNum == "430" || aisiNum == "430F")
                {
                    if (modifier == "шлиф") return "aisi430шлиф";
                    if (modifier == "зерк") return "aisi430зерк";
                    return "aisi430";
                }

                if (aisiNum == "201") return "aisi201";
                if (aisiNum == "310S" || aisiNum == "310") return "aisi310s";
                if (aisiNum == "410") return "aisi410";
                if (aisiNum == "431") return "aisi431";
                if (aisiNum == "904L") return "aisi904l";
            }

            // === ОСТАЛЬНЫЕ МАТЕРИАЛЫ ===
            if (grade.StartsWith("АМГ2")) return "амг2";
            if (grade.StartsWith("АМГ3")) return "амг3";
            if (grade.StartsWith("АМГ5")) return "амг5";
            if (grade.StartsWith("АМГ6")) return "амг6";
            if (grade.StartsWith("АМЦ")) return "амг6";
            if (grade.StartsWith("АД31Т")) return "ад31т";

            if (grade.StartsWith("Д16АМ") || grade.Contains("Д16АМ")) return "д16ам";
            if (grade.StartsWith("Д16АТ") || grade.Contains("Д16АТ")) return "д16ат";
            if (grade.StartsWith("Д16Т") || grade.Contains("Д16Т")) return "д16ат";
            if (grade.StartsWith("Д16М")) return "д16ам";
            if (grade.StartsWith("Д16") && !grade.Contains("А")) return "д16ат";
            if (grade.Contains("2024") && grade.Contains("Д16")) return "д16ат";

            if (grade.StartsWith("Л63") || grade.StartsWith("Л 63")) return "латунь";
            if (grade.StartsWith("ЛС59")) return "латунь";

            if (grade.StartsWith("М1") || grade.StartsWith("М 1") || grade.StartsWith("М2") || grade.StartsWith("М3"))
                return "медь";

            if (grade.Contains("Х/К") || grade.Contains("ХК") || grade.Contains("СТ08")) return "хк";
            if (grade.Contains("09Г2С") || grade.Contains("09Г2С-")) return "09г2с";
            if (grade.Contains("ОЦИНК") || grade.Contains("ZN") || grade.StartsWith("ZN")) return "цинк";
            if (grade.Contains("РИФЛ") || grade.Contains("РОМБ") || grade.Contains("ЧЕЧЕВ")) return "рифл";

            // 🔥 Фильтр электродов и нецелевых позиций
            if (grade.StartsWith("ER") || grade.StartsWith("Э") || grade.Contains("ЭЛЕКТРОД"))
                return null; // Вернёт null → отфильтруется в агрегаторе

            return (grade.ToLowerInvariant() + modifier).Trim();
        }
    }
}