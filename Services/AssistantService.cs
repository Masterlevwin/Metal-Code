using Metal_Code.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Metal_Code.Services
{
    /// <summary>
    /// Детерминированный ассистент без AI.
    /// Работает с реальными справочниками из HybridDataService (через MetalDict и Works).
    /// </summary>
    public sealed class AssistantService
    {
        #region Поля и константы

        private const float MIN_PRICE_OOO = 4200f;
        private const float MIN_PRICE_IP = 2700f;

        // Данные из БД
        private readonly Dictionary<string, Dictionary<float, (float Way, float Pin, float Mold)>> _metalDict;
        private readonly List<float> _destinies;
        private readonly Dictionary<string, WorkInfo> _works;

        // Реестры и словари
        private readonly List<UiCommandDescriptor> _commands;
        private readonly Dictionary<string, EntityDescriptor> _entities;
        private readonly Dictionary<string, string> _materialSynonyms;
        private readonly Dictionary<string, string> _workSynonyms;
        private readonly HashSet<string> _stopWords;
        private readonly Dictionary<string, float> _massPrices;

        public AssistantService(
            Dictionary<string, Dictionary<float, (float, float, float)>> metalDict,
            List<float> destinies,
            ObservableCollection<Work> works,
            IEnumerable<Metal> metals)
        {
            _metalDict = metalDict ?? new();
            _destinies = destinies ?? new();
            _works = BuildWorksDict(works);
            _massPrices = BuildMassPrices(metals);
            _commands = BuildCommandRegistry();
            _entities = BuildEntities();
            _materialSynonyms = BuildMaterialSynonyms();
            _workSynonyms = BuildWorkSynonyms();
            _stopWords = BuildStopWords();
        }

        #endregion

        #region Главный метод обработки запроса

        /// <summary>
        /// Обрабатывает запрос пользователя и возвращает структурированный ответ.
        /// Приоритеты: минималка → сводка → сущности → материалы → работы → команды → общие вопросы.
        /// </summary>
        public AssistantResponse ProcessQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Error("Пожалуйста, задайте вопрос.");

            string q = query.Trim().ToLowerInvariant();

            // 1. Минималка (работы или КП) — самый высокий приоритет
            var minimum = TryParseMinimumRequest(q);
            if (minimum != null) return minimum;

            // 2. Сводка стоимости
            var costSummary = TryParseCostSummaryRequest(q);
            if (costSummary != null) return costSummary;

            // 3. Сущности (заказчик, материал, сборка) — панель действий
            var entity = TryParseEntityRequest(q);
            if (entity != null) return entity;

            // 4. Цены материалов
            var priceRequest = TryParsePriceRequest(q, query);
            if (priceRequest != null) return priceRequest;

            // 5. Цены работ
            var work = TryParseWorkRequest(q);
            if (work != null) return work;

            // 6. Команды интерфейса (по существительным)
            var command = FindCommand(q);
            if (command != null)
            {
                return new AssistantResponse
                {
                    Kind = ResponseKind.UiCommandFound,
                    Text = $"📍 {command.Title}\n\n{command.Description}\n\n📂 Где найти: {command.NavigationPath}",
                    IconPath = command.IconPath,
                    Actions = new List<AssistantAction>() { new() { Label = "Перейти", CommandId = command.Id } }
                };
            }

            // 7. Общие вопросы
            var general = TryAnswerGeneralQuestion(q);
            if (general != null) return general;

            // 8. Ничего не найдено
            return NotFound();
        }

        #endregion

        #region 1. Минималка

        private AssistantResponse? TryParseMinimumRequest(string q)
        {
            bool asksMin = q.Contains("минималк") || q.Contains("минимальн") || q.Contains("минимум");
            if (!asksMin) return null;

            // Явное указание на расчёт/КП
            bool asksCalc = q.Contains("расчет") || q.Contains("расчёт") ||
                            q.Contains("кп") || q.Contains("сумм") || q.Contains("заказ");

            string? workName = ExtractWork(q);

            // Минималка конкретной работы
            if (workName != null && !asksCalc)
                return CreateWorkResponse(workName, askMinPrice: true);

            // Минималка КП
            return new AssistantResponse
            {
                Kind = ResponseKind.GeneralAnswer,
                Text = $"📋 Минимальная сумма КП:\n\n" +
                       $"• С НДС (ООО): не менее {MIN_PRICE_OOO:N0} ₽\n" +
                       $"• Без НДС (ИП): не менее {MIN_PRICE_IP:N0} ₽\n\n" +
                       $"Если сумма меньше — включите галочку [Минималка], " +
                       $"программа автоматически подберёт коэффициент.",
                Actions = new List<AssistantAction>() { new() { Label = "Показать сводку стоимости", CommandId = "view.cost_summary" } }
            };
        }

        #endregion

        #region 2. Сводка стоимости

        private AssistantResponse? TryParseCostSummaryRequest(string q)
        {
            bool asksSummary = q.Contains("сводк") || q.Contains("детализац") ||
                               q.Contains("разбивк") || q.Contains("из чего складывается") ||
                               q.Contains("общую стоимость") || q.Contains("показать сводку");

            bool asksAssemblyCost = (q.Contains("сварка") || q.Contains("окраска")) &&
                                     (q.Contains("сборочн") || q.Contains("сб"));

            bool asksStructure = q.Contains("стоимость материала") || q.Contains("стоимость работ") ||
                                 q.Contains("сколько стоит материал") || q.Contains("сколько стоят работы");

            if (!asksSummary && !asksAssemblyCost && !asksStructure) return null;

            string hint = "";
            if (asksAssemblyCost)
            {
                hint = q.Contains("свар") && q.Contains("окрас")
                    ? "\n\n💡 Стоимость сварки и окраски СБ — в строках «Сварка СБ» и «Окраска СБ»."
                    : q.Contains("свар")
                    ? "\n\n💡 Стоимость сварки СБ — в строке «Сварка СБ»."
                    : "\n\n💡 Стоимость окраски СБ — в строке «Окраска СБ».";
            }
            else if (q.Contains("материал"))
            {
                hint = "\n\n💡 Материал разделён на:\n• «Металл» — основной материал;\n• «ПКИ» — покупные изделия.";
            }
            else if (q.Contains("работ"))
            {
                hint = "\n\n💡 Работы включают:\n• «Работы» — операции по деталям;\n• «Сварка СБ» и «Окраска СБ» — по сборочным единицам.";
            }

            return new AssistantResponse
            {
                Kind = ResponseKind.GeneralAnswer,
                Text = "📊 Сводка стоимости находится в верхней панели, справа от деталей.\n\n" +
                       "Нажмите на жёлтый тоггл с общей суммой — раскроется панель:\n" +
                       "• Металл — стоимость основного материала;\n" +
                       "• ПКИ — покупные изделия;\n" +
                       "• Работы — все операции;\n" +
                       "• Сварка СБ — сварка сборочных единиц;\n" +
                       "• Окраска СБ — окраска сборочных единиц." + hint,
                Actions = new List<AssistantAction>() { new() { Label = "Показать сводку", CommandId = "view.cost_summary" } }
            };
        }

        #endregion

        #region 3. Сущности

        private AssistantResponse? TryParseEntityRequest(string q)
        {
            var words = ExtractMeaningfulWords(q);
            if (words.Count == 0) return null;

            foreach (var word in words)
            {
                string? key = ResolveEntityKey(word);
                if (key == null) continue;

                var entity = _entities[key];
                // Разрешаем алиасы
                while (!string.IsNullOrEmpty(entity.AliasOf))
                    entity = _entities[entity.AliasOf];

                return new AssistantResponse
                {
                    Kind = ResponseKind.UiCommandFound,
                    Text = $"📍 {entity.Title}\n\n{entity.Description}",
                    IconPath = entity.IconPath,
                    Actions = entity.Actions
                };
            }
            return null;
        }

        private string? ResolveEntityKey(string word)
        {
            if (_entities.ContainsKey(word)) return word;

            // Убираем падежные окончания
            string[] suffixes = { "ами", "ов", "ам", "ах", "ы", "и", "а", "у", "е", "о", "й" };
            foreach (var s in suffixes)
            {
                if (word.EndsWith(s) && word.Length - s.Length >= 3)
                {
                    string trimmed = word[..^s.Length];
                    if (_entities.ContainsKey(trimmed)) return trimmed;
                }
            }
            return null;
        }

        private List<string> ExtractMeaningfulWords(string q)
        {
            return q.Split(new[] { ' ', '-', '?', '!', '.', ',', ':' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 2 && !_stopWords.Contains(w))
                    .ToList();
        }

        #endregion

        #region 4. Цены материалов

        private AssistantResponse? TryParsePriceRequest(string q, string original)
        {
            bool hasIntent = q.Contains("цен") || q.Contains("стои") || q.Contains("почём") ||
                             q.Contains("почем") || q.Contains("прайс") ||
                             (q.Contains("сколько") && q.Contains("метр"));
            if (!hasIntent) return null;

            string? material = ExtractMaterial(q);
            float? thickness = ExtractThickness(original);

            if (material == null && thickness == null) return null;

            // Материал не определён — показываем список
            if (material == null)
            {
                var list = _metalDict.Keys.OrderBy(n => n).Take(15);
                return new AssistantResponse
                {
                    Kind = ResponseKind.ClarificationRequired,
                    Text = $"Уточните материал.\n\nДоступные:\n{string.Join("\n", list.Select(m => $"• {m}"))}",
                    Actions = new List<AssistantAction>() { new() { Label = "Открыть справочник", CommandId = "settings.metals" } }
                };
            }

            // Материал не найден в БД
            if (!_metalDict.TryGetValue(material, out var prices) || prices.Count == 0)
            {
                return new AssistantResponse
                {
                    Kind = ResponseKind.PriceNotFound,
                    Text = $"Материал '{material}' не найден в базе.",
                    Actions = new List<AssistantAction>() { new() { Label = "Открыть справочник", CommandId = "settings.metals" } }
                };
            }

            // ⭐ Получаем РЕАЛЬНУЮ цену материала за кг из коллекции Metals
            float massPrice = _massPrices.TryGetValue(material, out float mp) ? mp : 0;
            string massPriceLine = massPrice > 0
                ? $"• Материал: {massPrice:N0} ₽/кг"
                : "• Материал: цена не указана в базе";

            // С точной толщиной
            if (thickness.HasValue)
            {
                float t = thickness.Value;
                if (!prices.TryGetValue(t, out var p))
                {
                    t = prices.Keys.Where(k => k >= t).OrderBy(k => k).FirstOrDefault();
                    if (t == 0) t = prices.Keys.Max();
                    p = prices[t];
                }

                return new AssistantResponse
                {
                    Kind = ResponseKind.PriceFound,
                    Text = $"💰 {material}, толщина {t} мм:\n\n" +
                           $"• Лазерная резка: {p.Way:N2} ₽/м\n" +
                           $"• Прокол: {p.Pin:N2} ₽\n" +
                           $"• Труборез: {p.Mold:N2} ₽/м\n" +
                           massPriceLine,
                    Actions = new List<AssistantAction>()
            {
                new()
                {
                    Label = "Открыть справочник",
                    CommandId = "settings.metals",
                    Parameters = new Dictionary<string, object?> { ["material"] = material, ["thickness"] = t }
                }
            }
                };
            }

            // Без толщины — таблица (показываем резку, прокол и цену за кг)
            var lines = prices.OrderBy(p => p.Key).Take(12)
                              .Select(p => $"• {p.Key} мм — лазер: {p.Value.Way:N2} ₽/м, прокол: {p.Value.Pin:N2} ₽");
            string more = prices.Count > 12 ? $"\n\n...и ещё {prices.Count - 12} толщин" : "";

            return new AssistantResponse
            {
                Kind = ResponseKind.PriceFound,
                Text = $"💰 Цены для {material}:\n\n" +
                       $"{string.Join("\n", lines)}{more}\n\n" +
                       massPriceLine +
                       $"\n\nУкажите толщину для точного ответа.",
                Actions = new List<AssistantAction>() { new() { Label = "Открыть справочник", CommandId = "settings.metals" } }
            };
        }

        private string? ExtractMaterial(string q)
        {
            string? best = null;
            int bestLen = 0;

            foreach (var syn in _materialSynonyms)
            {
                if (q.Contains(syn.Key) && syn.Key.Length > bestLen)
                {
                    best = syn.Value;
                    bestLen = syn.Key.Length;
                }
            }

            foreach (var name in _metalDict.Keys)
            {
                if (q.Contains(name.ToLowerInvariant()) && name.Length > bestLen)
                {
                    best = name;
                    bestLen = name.Length;
                }
            }
            return best;
        }

        private static float? ExtractThickness(string text)
        {
            var patterns = new[]
            {
                @"(\d+(?:[.,]\d+)?)\s*(?:мм|mm)",
                @"толщин[аой]\s*(\d+(?:[.,]\d+)?)",
                @"[sS](\d+(?:[.,]\d+)?)"
            };
            foreach (var p in patterns)
            {
                var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
                if (m.Success && float.TryParse(m.Groups[1].Value.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out float r))
                    return r;
            }
            return null;
        }

        private static Dictionary<string, float> BuildMassPrices(IEnumerable<Metal>? metals)
        {
            var dict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (metals == null) return dict;
            foreach (var m in metals)
            {
                if (!string.IsNullOrWhiteSpace(m.Name))
                    dict[m.Name] = m.MassPrice;
            }
            return dict;
        }

        #endregion

        #region 5. Цены работ

        private AssistantResponse? TryParseWorkRequest(string q)
        {
            // Защита от перехвата запросов о сводке
            if (q.Contains("сводк") || q.Contains("детализац") || q.Contains("общую стоимость"))
                return null;
            if ((q.Contains("сварка") || q.Contains("окраска")) &&
                (q.Contains("сборочн") || q.Contains("сб")))
                return null;

            bool hasIntent = q.Contains("минималк") || q.Contains("минимальн") ||
                             q.Contains("работа") || q.Contains("операци") ||
                             q.Contains("сколько стоит") || q.Contains("цена") ||
                             q.Contains("стоимость") || q.Contains("почём") || q.Contains("почем");
            if (!hasIntent) return null;

            string? workName = ExtractWork(q);
            if (workName == null) return null;

            bool askMin = q.Contains("минималк") || q.Contains("минимальн") || q.Contains("минимум");
            return CreateWorkResponse(workName, askMin);
        }

        private AssistantResponse CreateWorkResponse(string workName, bool askMinPrice)
        {
            if (!_works.TryGetValue(workName, out var w))
            {
                return new AssistantResponse
                {
                    Kind = ResponseKind.WorkNotFound,
                    Text = $"Работа '{workName}' не найдена в справочнике.",
                    Actions = new List<AssistantAction>() { new() { Label = "Открыть справочник работ", CommandId = "settings.works" } }
                };
            }

            if (w.MinPrice <= 0)
            {
                return new AssistantResponse
                {
                    Kind = ResponseKind.WorkFound,
                    Text = $"💼 {w.Name}\n\n⚠️ Минимальная стоимость не установлена.\nЦена рассчитывается индивидуально.",
                    Actions = new List<AssistantAction>() { new() { Label = "Открыть справочник работ", CommandId = "settings.works" } }
                };
            }

            string emoji = w.Category switch
            {
                "резка" => "✂️",
                "формоизменение" => "🔨",
                "сборка" => "🔩",
                "покрытие" => "🎨",
                "мехобработка" => "⚙️",
                "инжиниринг" => "📐",
                _ => "💼"
            };

            string priceLine = askMinPrice
                ? $"• 💵 Минималка: {w.MinPrice:N0} ₽"
                : $"• 💵 Минимальная стоимость: {w.MinPrice:N0} ₽";

            string text = $"{emoji} Работа: {w.Name}\n\n" +
                          $"Категория: {w.Category}\n\n" +
                          $"{priceLine}\n" +
                          $"• ⏱ Норматив: {w.TimeMinutes} мин";

            // Контекстные подсказки для частых работ
            text += w.Name switch
            {
                "Сварка" => "\n\n💡 Итог зависит от длины шва, толщины и типа соединения.",
                "Окраска" => "\n\n💡 Итог зависит от площади, RAL-цвета и типа краски.",
                "Гибка" => "\n\n💡 Итог зависит от числа гибов, толщины и длины.",
                "Конструкторские работы" => "\n\n💡 Сложные проекты рассчитываются индивидуально.",
                _ => ""
            };

            return new AssistantResponse
            {
                Kind = ResponseKind.WorkFound,
                Text = text,
                Actions = new List<AssistantAction>() { new() { Label = "Открыть справочник работ", CommandId = "settings.works" } }
            };
        }

        private string? ExtractWork(string q)
        {
            string? best = null;
            int bestLen = 0;

            // 1. Синонимы
            foreach (var syn in _workSynonyms)
            {
                if (q.Contains(syn.Key) && syn.Key.Length > bestLen)
                {
                    best = syn.Value;
                    bestLen = syn.Key.Length;
                }
            }

            // 2. Прямые названия из БД
            foreach (var name in _works.Keys)
            {
                if (q.Contains(name.ToLowerInvariant()) && name.Length > bestLen)
                {
                    best = name;
                    bestLen = name.Length;
                }
            }

            // 3. Сравнение по основе слова (для падежей: "окраски" → "окраска")
            if (best == null)
            {
                var words = q.Split(new [] { ' ', '-', '?', '!', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    if (word.Length < 4) continue;

                    if (_workSynonyms.TryGetValue(word, out var synMatch))
                        return synMatch;

                    string stem = word.Length >= 5 ? word[..5] : word;
                    foreach (var name in _works.Keys)
                    {
                        string nStem = name.Length >= 5 ? name.ToLowerInvariant()[..5] : name.ToLowerInvariant();
                        if (stem == nStem) return name;
                    }

                    // 4 символа для "гибки" → "гибка"
                    if (best == null && word.Length >= 4)
                    {
                        string stem4 = word[..4];
                        foreach (var name in _works.Keys)
                        {
                            string nStem4 = name.Length >= 4 ? name.ToLowerInvariant()[..4] : name.ToLowerInvariant();
                            if (stem4 == nStem4) return name;
                        }
                    }
                }
            }

            return best;
        }

        #endregion

        #region 6. Команды интерфейса

        private UiCommandDescriptor? FindCommand(string q)
        {
            var words = ExtractMeaningfulWords(q);
            if (words.Count == 0) return null;

            UiCommandDescriptor? bestMatch = null;
            int bestScore = 0;

            foreach (var cmd in _commands)
            {
                int score = 0;
                foreach (var word in words)
                {
                    if (cmd.Title.Contains(word, StringComparison.OrdinalIgnoreCase))
                        score += 100;
                    foreach (var kw in cmd.Keywords)
                    {
                        if (kw.Equals(word, StringComparison.OrdinalIgnoreCase)) score += 80;
                        else if (kw.Contains(word, StringComparison.OrdinalIgnoreCase)) score += 40;
                    }
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = cmd;
                }
            }
            return bestScore >= 40 ? bestMatch : null;
        }

        #endregion

        #region 7. Общие вопросы

        private AssistantResponse? TryAnswerGeneralQuestion(string q)
        {
            // Защита: если "минималка" + работа, не отвечаем общим вопросом
            if (q.Contains("минималк") || q.Contains("минимальн"))
            {
                bool mentionsWork = _works.Keys.Any(w =>
                    q.Split(' ').Any(qw => qw.Length >= 4 &&
                        (w.ToLowerInvariant().Contains(qw) || qw.Contains(w.ToLowerInvariant()[..Math.Min(w.Length, 4)]))));
                if (mentionsWork) return null;
            }

            if (q.Contains("коэфф") || q.Contains("наценк") || q.Contains("кэф"))
            {
                return new AssistantResponse
                {
                    Kind = ResponseKind.GeneralAnswer,
                    Text = "📊 Коэффициенты наценки:\n\n" +
                           "• Общий — на всё КП\n" +
                           "• Материал — только на металл\n" +
                           "• Услуги — только на работы\n" +
                           "• Бонус % — дополнительная наценка\n\n" +
                           "Находятся в верхней панели (раздел [Коэффициенты])."
                };
            }

            if (q.Contains("доставк"))
            {
                return new AssistantResponse
                {
                    Kind = ResponseKind.GeneralAnswer,
                    Text = "🚚 Доставка:\n\n" +
                           "1. Поставьте галочку [Доставка]\n" +
                           "2. Введите цену и количество доставок\n" +
                           "3. Заполните адрес\n\n" +
                           "[v] — отдельной строкой в КП\n[■] — включена в цены деталей"
                };
            }

            if (q.Contains("экспресс") || q.Contains("срочн"))
            {
                return new AssistantResponse
                {
                    Kind = ResponseKind.GeneralAnswer,
                    Text = "⚡ Режим ЭКСПРЕСС:\n\n" +
                           "Включается галочкой [Экспресс].\n\n" +
                           "• Коэффициент на услуги ≥ 2\n" +
                           "• Срок: 3 дня\n" +
                           "• Пометка о срочности в КП"
                };
            }

            return null;
        }

        #endregion

        #region 8. Валидация расчёта

        public ValidationResult ValidateCurrentCalculation(CalculationSnapshot snapshot)
        {
            var errors = new List<ValidationIssue>();

            if (snapshot.Result <= 0)
                errors.Add(Issue("CALC_ZERO_RESULT", ValidationSeverity.Error, "Result",
                    "Итоговая стоимость равна нулю", "Добавьте детали или работы"));

            if (snapshot.CuttingLengthMeters <= 0)
                errors.Add(Issue("CALC_ZERO_CUTTING", ValidationSeverity.Error, "CuttingLength",
                    "Длина реза равна нулю", "Загрузите DXF или добавьте детали с резкой"));

            if (snapshot.Quantity <= 0)
                errors.Add(Issue("CALC_ZERO_QUANTITY", ValidationSeverity.Warning, "Count",
                    "Количество равно нулю", "Укажите количество деталей"));

            if (string.IsNullOrWhiteSpace(snapshot.CustomerName))
                errors.Add(Issue("CALC_NO_CUSTOMER", ValidationSeverity.Warning, "CustomerDrop",
                    "Не выбран заказчик", "Выберите или добавьте заказчика"));

            if (string.IsNullOrWhiteSpace(snapshot.OrderNumber) && snapshot.Result > 0)
                errors.Add(Issue("CALC_NO_ORDER", ValidationSeverity.Info, "Order",
                    "Не указан номер заказа", "Заполните номер КП"));

            if (snapshot.Result > 0 && snapshot.Result < (snapshot.IsAgent ? MIN_PRICE_IP : MIN_PRICE_OOO))
                errors.Add(Issue("CALC_BELOW_MINIMUM", ValidationSeverity.Warning, "Result",
                    $"Стоимость ниже минимальной ({(snapshot.IsAgent ? MIN_PRICE_IP : MIN_PRICE_OOO):N0} ₽)",
                    "Включите [Минималка] для автоподбора коэффициента"));

            if (snapshot.SelectedThickness > 0 && _destinies.Count > 0 &&
                !_destinies.Contains(snapshot.SelectedThickness))
                errors.Add(Issue("CALC_INVALID_THICKNESS", ValidationSeverity.Warning, "Thickness",
                    $"Толщина {snapshot.SelectedThickness} мм отсутствует в справочнике",
                    "Выберите доступную толщину"));

            if (!string.IsNullOrEmpty(snapshot.SelectedMaterial))
            {
                if (_metalDict.Count > 0 && !_metalDict.ContainsKey(snapshot.SelectedMaterial))
                {
                    errors.Add(Issue("CALC_UNKNOWN_MATERIAL", ValidationSeverity.Warning, "Material",
                        $"Материал '{snapshot.SelectedMaterial}' не найден", "Выберите из списка"));
                }
                else if (_massPrices.Count > 0 &&
                         !_massPrices.ContainsKey(snapshot.SelectedMaterial))
                {
                    errors.Add(Issue("CALC_NO_MASS_PRICE", ValidationSeverity.Warning, "Material",
                        $"Для '{snapshot.SelectedMaterial}' не указана цена за кг",
                        "Проверьте справочник материалов"));
                }
            }

            if (snapshot.SelectedWorks != null && _works.Count > 0)
            {
                foreach (var w in snapshot.SelectedWorks.Where(w => !string.IsNullOrWhiteSpace(w)))
                {
                    if (!_works.ContainsKey(w))
                        errors.Add(Issue("CALC_UNKNOWN_WORK", ValidationSeverity.Warning, "Work",
                            $"Работа '{w}' не найдена", "Выберите из списка"));
                    else if (_works[w].MinPrice <= 0)
                        errors.Add(Issue("CALC_WORK_NO_PRICE", ValidationSeverity.Info, "Work",
                            $"Для '{w}' нет минимальной стоимости", "Цена рассчитается индивидуально"));
                }
            }

            return new ValidationResult
            {
                IsValid = !errors.Any(e => e.Severity == ValidationSeverity.Error),
                Issues = errors
            };
        }

        private static ValidationIssue Issue(string code, ValidationSeverity sev,
            string field, string msg, string rec) =>
            new() { Code = code, Severity = sev, FieldName = field, Message = msg, Recommendation = rec };

        #endregion

        #region Вспомогательные методы

        private static AssistantResponse Error(string text) =>
            new() { Kind = ResponseKind.Error, Text = text };

        private AssistantResponse NotFound() => new()
        {
            Kind = ResponseKind.NotFound,
            Text = "Не удалось найти ответ.\n\nПопробуйте:\n" +
                   "• «заказчик» — работа с заказчиками\n" +
                   "• «минималка сварки» — стоимость работы\n" +
                   "• «минималка» — минимальная сумма КП\n" +
                   "• «цена алюминия 8 мм» — цены материалов",
            Actions = new List<AssistantAction>
            {
                new() { Label = "Показать сводку", CommandId = "view.cost_summary" },
                new() { Label = "Справочник материалов", CommandId = "settings.metals" },
                new() { Label = "Справочник работ", CommandId = "settings.works" }
            }
        };

        #endregion

        #region Построение словарей

        private Dictionary<string, WorkInfo> BuildWorksDict(ObservableCollection<Work>? works)
        {
            var dict = new Dictionary<string, WorkInfo>(StringComparer.OrdinalIgnoreCase);
            if (works == null) return dict;

            foreach (var w in works)
            {
                if (string.IsNullOrWhiteSpace(w.Name)) continue;
                dict[w.Name] = new WorkInfo
                {
                    Name = w.Name,
                    MinPrice = w.Price,
                    TimeMinutes = w.Time,
                    Category = CategorizeWork(w.Name)
                };
            }
            return dict;
        }

        private static string CategorizeWork(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("лазер") || n.Contains("труборез") || n.Contains("лентопил")) return "резка";
            if (n.Contains("гиб") || n.Contains("вальц")) return "формоизменение";
            if (n.Contains("свар")) return "сборка";
            if (n.Contains("окрас") || n.Contains("цинк") || n.Contains("аква")) return "покрытие";
            if (n.Contains("сверл") || n.Contains("зенк") || n.Contains("резьб") ||
                n.Contains("заклеп") || n.Contains("фрез")) return "мехобработка";
            if (n.Contains("доп")) return "дополнительные";
            if (n.Contains("конструктор")) return "инжиниринг";
            return "прочее";
        }

        private static HashSet<string> BuildStopWords() => new(StringComparer.OrdinalIgnoreCase)
        {
            "добавить", "добавь", "создать", "создай", "открыть", "открой",
            "показать", "покажи", "найти", "найди", "сделать", "сделай",
            "загрузить", "загрузи", "сохранить", "сохрани", "изменить", "измени",
            "удалить", "удали", "редактировать", "посмотреть", "узнать",
            "проверить", "запустить", "перейти", "включить", "включи",
            "убрать", "получить", "рассчитать", "посчитать", "выбрать",
            "как", "где", "что", "какой", "какая", "какие", "сколько",
            "можно", "нужно", "надо", "есть", "будет", "это", "мне", "мной",
            "в", "на", "по", "из", "для", "с", "со", "к", "ко", "от", "до",
            "и", "или", "а", "но", "же", "бы", "ли", "то", "так", "не"
        };

        private static List<UiCommandDescriptor> BuildCommandRegistry() => new()
        {
            new() { Id = "detail.add", Title = "Добавить деталь", Category = "Расчёт",
                Description = "Создать новую деталь в текущем расчёте.",
                Keywords = new [] {"деталь", "детали", "деталей" },
                NavigationPath = "Верхняя панель → кнопка [Деталь]", IconPath = "Images/plus.png" },

            new() { Id = "assembly.create", Title = "Создать сборку", Category = "Расчёт",
                Description = "Открыть окно управления сборками для объединения деталей.",
                Keywords = new [] {"сборка", "сборки", "сборку" },
                NavigationPath = "Верхняя панель → кнопка [СБ]", IconPath = "Images/assembly.png" },

            new() { Id = "basket.add", Title = "Добавить ПКИ", Category = "Расчёт",
                Description = "Добавить покупное изделие в расчёт.",
                Keywords = new[] { "пки", "покупное изделие", "покупные изделия" },
                NavigationPath = "Верхняя панель → кнопка [ПКИ]", IconPath = "Images/basket.png" },

            new() { Id = "project.new", Title = "Новый расчёт", Category = "Файл",
                Description = "Создать новый расчёт (Ctrl+Q).",
                Keywords = new [] {"новый расчёт", "новый расчет", "расчёт", "расчет" },
                NavigationPath = "Файл → Новый расчёт (Ctrl+Q)", IconPath = "Images/new3.png" },

            new() { Id = "project.open", Title = "Открыть расчёт", Category = "Файл",
                Description = "Открыть ранее сохранённый расчёт (Ctrl+S).",
                Keywords = new[] { "открыть расчёт", "загрузить расчёт", "загрузить кп" },
                NavigationPath = "Файл → Открыть (Ctrl+S)", IconPath = "Images/open4.png" },

            new() { Id = "project.save", Title = "Сохранить расчёт", Category = "Файл",
                Description = "Сохранить текущий расчёт (Ctrl+D).",
                Keywords = new[] { "сохранить расчёт", "сохранить кп", "сохранение" },
                NavigationPath = "Файл → Сохранить (Ctrl+D)", IconPath = "Images/save.png" },

            new() { Id = "project.folder", Title = "Создать папку проекта", Category = "Файл",
                Description = "Создать структуру папок ТЗ/Исходник и ТЗ/Редакция.",
                Keywords = new[] { "папка", "папка проекта", "структура папок" },
                NavigationPath = "Верхняя панель → кнопка с иконкой папки", IconPath = "Images/save3.png" },

            new() { Id = "request.create", Title = "Создать заявку", Category = "Файл",
                Description = "Создать заявку на основе файлов заказчика.",
                Keywords = new[] { "заявка", "заявку", "заявки" },
                NavigationPath = "Верхняя панель → кнопка [комментарий]", IconPath = "Images/comment.png" },

            new() { Id = "layout.load", Title = "Загрузить раскладки", Category = "Файл",
                Description = "Импорт раскладок из файла Excel (Ctrl+F).",
                Keywords = new[] { "раскладки", "раскладка", "нестинг", "раскладок" },
                NavigationPath = "Верхняя панель → кнопка [Загрузить раскладки] (Ctrl+F)", IconPath = "Images/xlsx_load.png" },

            new() { Id = "tools.bends", Title = "Таблица гибов", Category = "Инструменты",
                Description = "Справочная таблица с инструкцией по расчёту гибов.",
                Keywords = new[] { "таблица гибов", "гибов" },
                NavigationPath = "Инструменты → Таблица гибов", IconPath = "Images/bends.png" },

            new() { Id = "tools.route", Title = "Маршрут производства", Category = "Инструменты",
                Description = "Визуальный маршрут изготовления по операциям.",
                Keywords = new[] { "маршрут", "маршрут производства" },
                NavigationPath = "Инструменты → Маршрут производства", IconPath = "Images/route.png" },

            new() { Id = "tools.spec", Title = "Спецификация", Category = "Инструменты",
                Description = "Формирование спецификации по текущему расчёту.",
                Keywords = new[] { "спецификация", "спецификацию" },
                NavigationPath = "Инструменты → Спецификация", IconPath = "Images/open.png" },

            new() { Id = "tools.passport", Title = "Паспорт качества", Category = "Инструменты",
                Description = "Формирование паспорта качества для заказа.",
                Keywords = new[] { "паспорт", "паспорт качества" },
                NavigationPath = "Инструменты → Паспорт качества", IconPath = "Images/list.png" },

            new() { Id = "tools.act", Title = "Акт приёма-передачи", Category = "Инструменты",
                Description = "Формирование акта приёма-передачи.",
                Keywords = new[] { "акт", "акт приёма", "приёма-передачи" },
                NavigationPath = "Инструменты → Акт приёма-передачи", IconPath = "Images/blueprint.png" },

            new() { Id = "tools.complect", Title = "Комплектация", Category = "Инструменты",
                Description = "Создание файла комплектации.",
                Keywords = new[] { "комплектация", "комплектацию" },
                NavigationPath = "Инструменты → Простая комплектация", IconPath = "Images/complect2.png" },

            new() { Id = "tools.convert", Title = "Конвертер DWG в DXF", Category = "Инструменты",
                Description = "Преобразование файлов AutoCAD.",
                Keywords = new[] { "конвертер", "dwg", "dxf" },
                NavigationPath = "Инструменты → Конвертер DWG в DXF", IconPath = "Images/convert.png" },

            new() { Id = "tools.tasks", Title = "Список задач", Category = "Инструменты",
                Description = "Формирование задач для Битрикс24.",
                Keywords = new[] { "задачи", "задач", "битрикс" },
                NavigationPath = "Инструменты → Список задач", IconPath = "Images/express.png" },

            new() { Id = "tools.sort", Title = "Сортировка файлов", Category = "Инструменты",
                Description = "Сортировка файлов по месяцам.",
                Keywords = new[] { "сортировка", "сортировка файлов" },
                NavigationPath = "Инструменты → Сортировка файлов", IconPath = "Images/screen.png" },

            new() { Id = "report.shipped", Title = "Отчёт по отгруженным", Category = "Отчёты",
                Description = "Отчёт по отгруженным заказам за период.",
                Keywords = new[] { "отгруженные", "отгрузке", "отгруженным" },
                NavigationPath = "Отчёты → 📊 По отгруженным заказам" },

            new() { Id = "report.production", Title = "Отчёт по производству", Category = "Отчёты",
                Description = "Отчёт по заказам в производстве.",
                Keywords = new[] { "в производстве", "производству" },
                NavigationPath = "Отчёты → 🏭 По заказам в производстве" },

            new() { Id = "settings.details", Title = "База заготовок", Category = "Настройки",
                Description = "Справочник типовых заготовок (лист, труба).",
                Keywords = new[] { "заготовки", "заготовок", "типовые детали" },
                NavigationPath = "Базы → База заготовок" },

            new() { Id = "settings.works", Title = "База работ", Category = "Настройки",
                Description = "Справочник работ с минимальной стоимостью.",
                Keywords = new[] { "база работ", "работ" },
                NavigationPath = "Базы → База работ" },

            new() { Id = "settings.managers", Title = "База менеджеров", Category = "Настройки",
                Description = "Список менеджеров и их права.",
                Keywords = new[] { "менеджеры", "менеджеров" },
                NavigationPath = "Базы → База менеджеров" },

            new() { Id = "settings.metals", Title = "База материалов", Category = "Настройки",
                Description = "Справочник металлов, толщин и цен.",
                Keywords = new[] { "база материалов", "материалов", "металлы" },
                NavigationPath = "Базы → База материалов" },

            new() { Id = "launch.work", Title = "Запустить в производство", Category = "Производство",
                Description = "Передать расчёт в производство с присвоением номера заказа.",
                Keywords = new[] { "производство", "запуск", "номер заказа" },
                NavigationPath = "Контекстное меню расчёта → Запустить в производство", IconPath = "Images/order.png" },

            new() { Id = "customer.add", Title = "Добавить заказчика", Category = "Заказчики",
                Description = "Добавить нового заказчика в базу.",
                Keywords = new[] { "заказчика", "клиента", "компанию" },
                NavigationPath = "Панель заказчика → кнопка [+]", IconPath = "Images/add.png" },

            new() { Id = "customer.edit", Title = "Изменить заказчика", Category = "Заказчики",
                Description = "Изменить данные выбранного заказчика.",
                Keywords = new [] {"изменить заказчика", "редактировать заказчика" },
                NavigationPath = "Панель заказчика → кнопка [✏️]", IconPath = "Images/edit.png" },

            new() { Id = "customer.delete", Title = "Удалить заказчика", Category = "Заказчики",
                Description = "Удалить выбранного заказчика из базы.",
                Keywords = new [] {"удалить заказчика", "удалить клиента" },
                NavigationPath = "Панель заказчика → кнопка [🗑]", IconPath = "Images/del.png" },

            new() { Id = "help.guide", Title = "Руководство", Category = "Помощь",
                Description = "Краткое руководство по программе.",
                Keywords = new [] {"руководство", "помощь", "инструкция", "справка" },
                NavigationPath = "О программе → Руководство", IconPath = "Images/lamp.png" },

            new() { Id = "view.cost_summary", Title = "Сводка стоимости", Category = "Просмотр",
                Description = "Разбивка общей стоимости на материалы и работы.",
                Keywords = new[] { "сводка", "сводку", "детализация" },
                NavigationPath = "Верхняя панель → жёлтый тоггл с суммой" }
        };

        private static Dictionary<string, EntityDescriptor> BuildEntities() => new(StringComparer.OrdinalIgnoreCase)
        {
            ["заказчик"] = new()
            {
                Title = "Работа с заказчиками",
                Description = "Панель управления заказчиками находится в правой части экрана, над расчётами.",
                IconPath = "Images/add.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "➕ Добавить заказчика", CommandId = "customer.add" },
            new() { Label = "✏️ Изменить заказчика", CommandId = "customer.edit" },
            new() { Label = "🗑 Удалить заказчика", CommandId = "customer.delete" }
        }
            },

            ["клиент"] = new() { AliasOf = "заказчик" },
            ["компания"] = new() { AliasOf = "заказчик" },

            ["материал"] = new()
            {
                Title = "Справочник материалов",
                Description = "Содержит металлы, толщины и цены на резку.",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Открыть справочник материалов", CommandId = "settings.metals" }
        }
            },

            ["металл"] = new() { AliasOf = "материал" },

            ["работа"] = new()
            {
                Title = "Справочник работ",
                Description = "Содержит операции (резка, гибка, сварка) с минимальной стоимостью.",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Открыть справочник работ", CommandId = "settings.works" }
        }
            },

            ["операция"] = new() { AliasOf = "работа" },

            ["деталь"] = new()
            {
                Title = "Добавление детали",
                Description = "Создать новую деталь в текущем расчёте.",
                IconPath = "Images/plus.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Добавить деталь", CommandId = "detail.add" }
        }
            },

            ["детали"] = new() { AliasOf = "деталь" },

            ["сборка"] = new()
            {
                Title = "Работа со сборками",
                Description = "Объединение деталей в сборочные единицы.",
                IconPath = "Images/assembly.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Открыть окно сборок", CommandId = "assembly.create" }
        }
            },

            ["сборки"] = new() { AliasOf = "сборка" },
            ["сб"] = new() { AliasOf = "сборка" },

            ["пки"] = new()
            {
                Title = "Покупные изделия",
                Description = "Добавление покупных изделий (ПКИ) в расчёт.",
                IconPath = "Images/basket.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Добавить ПКИ", CommandId = "basket.add" }
        }
            },

            ["раскладка"] = new()
            {
                Title = "Загрузка раскладок",
                Description = "Импорт раскладок из файла Excel (Ctrl+F).",
                IconPath = "Images/xlsx_load.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Загрузить раскладки", CommandId = "layout.load" }
        }
            },

            ["раскладки"] = new() { AliasOf = "раскладка" },
            ["нестинг"] = new() { AliasOf = "раскладка" },

            ["папка"] = new()
            {
                Title = "Папка проекта",
                Description = "Создание структуры папок ТЗ/Исходник и ТЗ/Редакция.",
                IconPath = "Images/save3.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Создать папку проекта", CommandId = "project.folder" }
        }
            },

            ["заявка"] = new()
            {
                Title = "Заявка за клиента",
                Description = "Создание заявки на основе файлов заказчика.",
                IconPath = "Images/comment.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Создать заявку", CommandId = "request.create" }
        }
            },

            ["маршрут"] = new()
            {
                Title = "Маршрут производства",
                Description = "Визуальный маршрут изготовления по операциям.",
                IconPath = "Images/route.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Показать маршрут", CommandId = "tools.route" }
        }
            },

            ["спецификация"] = new()
            {
                Title = "Спецификация",
                Description = "Формирование спецификации по текущему расчёту.",
                IconPath = "Images/open.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Создать спецификацию", CommandId = "tools.spec" }
        }
            },

            ["паспорт"] = new()
            {
                Title = "Паспорт качества",
                Description = "Формирование паспорта качества для заказа.",
                IconPath = "Images/list.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Создать паспорт качества", CommandId = "tools.passport" }
        }
            },

            ["акт"] = new()
            {
                Title = "Акт приёма-передачи",
                Description = "Формирование акта приёма-передачи.",
                IconPath = "Images/blueprint.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Создать акт", CommandId = "tools.act" }
        }
            },

            ["комплектация"] = new()
            {
                Title = "Комплектация",
                Description = "Создание файла комплектации.",
                IconPath = "Images/complect2.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Создать комплектацию", CommandId = "tools.complect" }
        }
            },

            ["конвертер"] = new()
            {
                Title = "Конвертер DWG в DXF",
                Description = "Преобразование файлов AutoCAD.",
                IconPath = "Images/convert.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Открыть конвертер", CommandId = "tools.convert" }
        }
            },

            ["задача"] = new()
            {
                Title = "Список задач",
                Description = "Формирование задач для Битрикс24.",
                IconPath = "Images/express.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Открыть список задач", CommandId = "tools.tasks" }
        }
            },

            ["задачи"] = new() { AliasOf = "задача" },
            ["битрикс"] = new() { AliasOf = "задача" },

            ["гиб"] = new()
            {
                Title = "Таблица гибов",
                Description = "Справочная таблица с инструкцией по расчёту гибов.",
                IconPath = "Images/bends.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Показать таблицу гибов", CommandId = "tools.bends" }
        }
            },

            ["менеджер"] = new()
            {
                Title = "Справочник менеджеров",
                Description = "Список менеджеров и их права.",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Открыть справочник менеджеров", CommandId = "settings.managers" }
        }
            },

            ["менеджеры"] = new() { AliasOf = "менеджер" },

            ["сводка"] = new()
            {
                Title = "Сводка стоимости",
                Description = "Разбивка общей стоимости на материалы и работы.",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Показать сводку", CommandId = "view.cost_summary" }
        }
            },

            ["производство"] = new()
            {
                Title = "Запуск в производство",
                Description = "Передача расчёта в производство с присвоением номера заказа.",
                IconPath = "Images/order.png",
                Actions = new List<AssistantAction>
        {
            new() { Label = "Запустить в производство", CommandId = "launch.work" }
        }
            }
        };

        private static Dictionary<string, string> BuildMaterialSynonyms() => new(StringComparer.OrdinalIgnoreCase)
        {
            // Конструкционные стали
            ["ст3"] = "ст3",
            ["ст 3"] = "ст3",
            ["ст-3"] = "ст3",
            ["сталь"] = "ст3",
            ["конструкционная"] = "ст3",
            ["углеродистая"] = "ст3",
            ["черная"] = "ст3",
            ["чёрная"] = "ст3",
            ["чернуха"] = "ст3",

            ["хк"] = "хк",
            ["холоднокатаная"] = "хк",
            ["холодная"] = "хк",

            ["рифл"] = "рифл",
            ["рифлёный"] = "рифл",
            ["рифленый"] = "рифл",
            ["чеканка"] = "рифл",

            ["09г2с"] = "09г2с",
            ["09 г2с"] = "09г2с",
            ["г2с"] = "09г2с",
            ["низколегированная"] = "09г2с",
            ["морозостойкая"] = "09г2с",

            // Нержавейки
            ["aisi201"] = "aisi201",
            ["aisi 201"] = "aisi201",
            ["201"] = "aisi201",
            ["aisi304"] = "aisi304",
            ["aisi 304"] = "aisi304",
            ["304"] = "aisi304",
            ["нержавейка"] = "aisi304",
            ["нерж"] = "aisi304",
            ["пищевая"] = "aisi304",
            ["08х18н10"] = "aisi304",
            ["aisi304зерк"] = "aisi304зерк",
            ["зеркальная нержавейка"] = "aisi304зерк",
            ["зеркало"] = "aisi304зерк",
            ["полированная"] = "aisi304зерк",
            ["aisi304шлиф"] = "aisi304шлиф",
            ["шлифованная нержавейка"] = "aisi304шлиф",
            ["сатин"] = "aisi304шлиф",
            ["матовая нержавейка"] = "aisi304шлиф",
            ["aisi316"] = "aisi316",
            ["aisi 316"] = "aisi316",
            ["316"] = "aisi316",
            ["кислотостойкая"] = "aisi316",
            ["морская нержавейка"] = "aisi316",
            ["aisi321"] = "aisi321",
            ["aisi 321"] = "aisi321",
            ["321"] = "aisi321",
            ["жаропрочная"] = "aisi321",
            ["aisi430"] = "aisi430",
            ["aisi 430"] = "aisi430",
            ["430"] = "aisi430",
            ["магнитная нержавейка"] = "aisi430",
            ["aisi430зерк"] = "aisi430зерк",
            ["зеркальная 430"] = "aisi430зерк",
            ["aisi430шлиф"] = "aisi430шлиф",
            ["шлифованная 430"] = "aisi430шлиф",

            // Алюминий
            ["амг2"] = "амг2",
            ["амг 2"] = "амг2",
            ["амг5"] = "амг5",
            ["амг 5"] = "амг5",
            ["амг6"] = "амг6",
            ["амг 6"] = "амг6",
            ["алюминий"] = "амг2",
            ["алюм"] = "амг2",
            ["ал"] = "амг2",

            // Дюраль
            ["д16ам"] = "д16АМ",
            ["д16 ам"] = "д16АМ",
            ["мягкий дюраль"] = "д16АМ",
            ["д16ат"] = "д16АТ",
            ["д16 ат"] = "д16АТ",
            ["твердый дюраль"] = "д16АТ",
            ["д16"] = "д16АТ",
            ["дюраль"] = "д16АТ",
            ["дюралюминий"] = "д16АТ",

            // Цветные
            ["латунь"] = "латунь",
            ["л63"] = "латунь",
            ["медь"] = "медь",
            ["м1"] = "медь",
            ["цинк"] = "цинк",
            ["оцинковка"] = "цинк",
            ["оцинкованная"] = "цинк"
        };

        private static Dictionary<string, string> BuildWorkSynonyms() => new(StringComparer.OrdinalIgnoreCase)
        {
            // Резка
            ["лазерная резка"] = "Лазерная резка",
            ["лазер"] = "Лазерная резка",
            ["резка"] = "Лазерная резка",
            ["лр"] = "Лазерная резка",
            ["лазерной резки"] = "Лазерная резка",
            ["лазерной резке"] = "Лазерная резка",
            ["труборез"] = "Труборез",
            ["труба"] = "Труборез",
            ["трубореза"] = "Труборез",
            ["труборезе"] = "Труборез",
            ["лентопил"] = "Лентопил",
            ["пила"] = "Лентопил",
            ["лентопила"] = "Лентопил",

            // Формоизменение
            ["гибка"] = "Гибка",
            ["гиб"] = "Гибка",
            ["гибы"] = "Гибка",
            ["гибки"] = "Гибка",
            ["гибке"] = "Гибка",
            ["гибку"] = "Гибка",
            ["вальцовка"] = "Вальцовка",
            ["вальцевание"] = "Вальцовка",
            ["вальцовки"] = "Вальцовка",
            ["вальцовке"] = "Вальцовка",
            ["вальцовку"] = "Вальцовка",

            // Сборка
            ["сварка"] = "Сварка",
            ["св"] = "Сварка",
            ["аргон"] = "Сварка",
            ["сварки"] = "Сварка",
            ["сварке"] = "Сварка",
            ["сварку"] = "Сварка",
            ["заклепки"] = "Заклепки",
            ["заклёпки"] = "Заклепки",
            ["клепка"] = "Заклепки",

            // Мехобработка
            ["сверловка"] = "Сверловка",
            ["сверление"] = "Сверловка",
            ["сверловки"] = "Сверловка",
            ["сверловке"] = "Сверловка",
            ["зенковка"] = "Зенковка",
            ["зенкер"] = "Зенковка",
            ["зенковки"] = "Зенковка",
            ["зенковке"] = "Зенковка",
            ["резьба"] = "Резьба",
            ["резьбы"] = "Резьба",
            ["резьбе"] = "Резьба",
            ["фрезеровка"] = "Фрезеровка",
            ["фреза"] = "Фрезеровка",
            ["фрезер"] = "Фрезеровка",
            ["фрезеровки"] = "Фрезеровка",
            ["фрезеровке"] = "Фрезеровка",

            // Покрытия
            ["окраска"] = "Окраска",
            ["покраска"] = "Окраска",
            ["краска"] = "Окраска",
            ["окраски"] = "Окраска",
            ["окраске"] = "Окраска",
            ["окраску"] = "Окраска",
            ["цинкование"] = "Цинкование",
            ["аквабластинг"] = "Аквабластинг",
            ["аква"] = "Аквабластинг",
            ["пескоструй"] = "Аквабластинг",
            ["аквабластинга"] = "Аквабластинг",
            ["аквабластинге"] = "Аквабластинг",

            // Прочее
            ["конструкторские работы"] = "Конструкторские работы",
            ["конструктор"] = "Конструкторские работы",
            ["кб"] = "Конструкторские работы",
            ["доп работа л"] = "Доп работа Л",
            ["доп работа п"] = "Доп работа П"
        };

        #endregion
    }

    #region Модели данных

    public sealed class UiCommandDescriptor
    {
        public string Id { get; init; } = default!;
        public string Title { get; init; } = default!;
        public string Category { get; init; } = default!;
        public string Description { get; init; } = default!;
        public string[] Keywords { get; init; } = Array.Empty<string>();
        public string NavigationPath { get; init; } = default!;
        public string? IconPath { get; init; }
    }

    public sealed class EntityDescriptor
    {
        public string? AliasOf { get; init; }
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public string? IconPath { get; init; }
        public List<AssistantAction> Actions { get; init; } = new();
    }

    public sealed class WorkInfo
    {
        public string Name { get; init; } = default!;
        public float MinPrice { get; init; }
        public float TimeMinutes { get; init; }
        public string Category { get; init; } = "прочее";
    }

    public sealed class AssistantResponse
    {
        public string Text { get; init; } = default!;
        public ResponseKind Kind { get; init; }
        public List<AssistantAction> Actions { get; init; } = new();
        public string? IconPath { get; init; }
    }

    public enum ResponseKind
    {
        UiCommandFound, UiCommandNotFound,
        PriceFound, PriceNotFound,
        WorkFound, WorkNotFound,
        ClarificationRequired, GeneralAnswer,
        ValidationError, Error, NotFound
    }

    public sealed class AssistantAction
    {
        public string Label { get; init; } = default!;
        public string CommandId { get; init; } = default!;
        public Dictionary<string, object?> Parameters { get; init; } = new();
    }

    public sealed class CalculationSnapshot
    {
        public float Result { get; init; }
        public float CuttingLengthMeters { get; init; }
        public int Quantity { get; init; }
        public string? CustomerName { get; init; }
        public string? OrderNumber { get; init; }
        public bool IsAgent { get; init; }
        public string? SelectedMaterial { get; init; }
        public float SelectedThickness { get; init; }
        public IReadOnlyList<string>? SelectedWorks { get; init; }
    }

    public sealed class ValidationResult
    {
        public bool IsValid { get; init; }
        public List<ValidationIssue> Issues { get; init; } = new();
    }

    public sealed class ValidationIssue
    {
        public string Code { get; init; } = default!;
        public ValidationSeverity Severity { get; init; }
        public string? FieldName { get; init; }
        public string Message { get; init; } = default!;
        public string Recommendation { get; init; } = default!;
    }

    public enum ValidationSeverity { Info, Warning, Error }

    #endregion
}