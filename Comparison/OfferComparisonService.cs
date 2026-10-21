using Metal_Code.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Metal_Code.Comparison
{
    public class OfferComparisonService
    {
        private const decimal MoneyTolerance = 0.01m;   // 1 копейка
        private const double RatioTolerance = 0.001;
        private const decimal SignificantDiffPercent = 5m;      // > 5% от суммы = критично

        public ComparisonResult Compare(
            Product a, Product b,
            decimal amountA, decimal amountB,
            string titleA, string titleB,
            IReadOnlyList<Metal>? metals = null,
            IReadOnlyList<TypeDetail>? typeDetails = null)
        {
            var result = new ComparisonResult
            {
                TitleA = titleA,
                TitleB = titleB,
                AmountA = amountA,
                AmountB = amountB
            };

            CompareGlobalParams(a, b, result);
            CompareDetails(a.Details, b.Details, result, metals, typeDetails);
            CompareBaskets(a.Baskets ?? new(), b.Baskets ?? new(), result);
            CompareAssemblies(a.Assemblies ?? new(), b.Assemblies ?? new(), result);

            BuildDiagnostics(result);
            return result;
        }

        // ============================================================
        // ГЛОБАЛЬНЫЕ ПАРАМЕТРЫ (только влияющие на цену)
        // ============================================================
        private void CompareGlobalParams(Product a, Product b, ComparisonResult r)
        {
            var root = new ComparisonItem { Category = "Параметры расчёта" };

            // Множители
            AddDecimal(root, "Коэффициент (Ratio)", (decimal)a.Ratio, (decimal)b.Ratio, "F2");
            AddDecimal(root, "Коэф. материала", (decimal)a.MaterialFactor, (decimal)b.MaterialFactor, "F2");
            AddDecimal(root, "Коэф. услуг", (decimal)a.ServiceFactor, (decimal)b.ServiceFactor, "F2");
            AddDecimal(root, "Бонус, %", (decimal)a.BonusRatio, (decimal)b.BonusRatio, "F1");
            AddDecimal(root, "Множитель конструктора",
                       (decimal)ParseConstructRatio(a.ConstructRatio),
                       (decimal)ParseConstructRatio(b.ConstructRatio), "F2");

            // Количество и доставка
            AddInt(root, "Количество", a.Count, b.Count);
            AddInt(root, "Доставка, ₽", a.Delivery, b.Delivery);
            AddInt(root, "Множитель доставки", a.DeliveryRatio, b.DeliveryRatio);

            // Флаги, включающие/выключающие блоки стоимости
            AddBool(root, "Агент (ИП) — влияет на НДС", a.IsAgent, b.IsAgent);
            AddBool(root, "Есть доставка", a.HasDelivery, b.HasDelivery);
            AddBool(root, "Есть конструктор", a.HasConstruct, b.HasConstruct);

            if (root.Children.Any(c => c.HasDifference))
            {
                root.Status = root.Children.Any(c =>
                    c.Status is ComparisonStatus.Warning or ComparisonStatus.Critical)
                    ? ComparisonStatus.Warning
                    : ComparisonStatus.Different;
                r.Items.Add(root);
            }
        }

        // ============================================================
        // ДЕТАЛИ (по названию как ключу)
        // ============================================================
        private void CompareDetails(
            IEnumerable<Detail> detailsA,
            IEnumerable<Detail> detailsB,
            ComparisonResult r,
            IReadOnlyList<Metal>? metals,
            IReadOnlyList<TypeDetail>? typeDetails)
        {
            var root = new ComparisonItem { Category = "Детали", IsExpanded = false };
            var mapA = IndexDetails(detailsA);
            var mapB = IndexDetails(detailsB);

            foreach (var key in mapA.Keys.Union(mapB.Keys))
            {
                var hasA = mapA.TryGetValue(key, out var da);
                var hasB = mapB.TryGetValue(key, out var db);

                if (hasA && !hasB)
                {
                    root.Children.Add(new ComparisonItem
                    {
                        Category = $"Деталь «{key}»",
                        Status = ComparisonStatus.OnlyInA,
                        Note = "отсутствует в B"
                    });
                    continue;
                }
                if (!hasA && hasB)
                {
                    root.Children.Add(new ComparisonItem
                    {
                        Category = $"Деталь «{key}»",
                        Status = ComparisonStatus.OnlyInB,
                        Note = "добавлена в B"
                    });
                    continue;
                }

                var item = CompareOneDetail(da!, db!, metals, typeDetails);
                if (item != null) root.Children.Add(item);
            }

            if (root.Children.Count > 0)
            {
                root.Status = root.Children.Any(c =>
                    c.Status is ComparisonStatus.Warning or ComparisonStatus.Critical)
                    ? ComparisonStatus.Warning
                    : ComparisonStatus.Different;
                root.IsExpanded = true;
                r.Items.Add(root);
            }
        }

        private Dictionary<string, Detail> IndexDetails(IEnumerable<Detail> details)
        {
            var map = new Dictionary<string, Detail>();
            var counters = new Dictionary<string, int>();
            foreach (var d in details)
            {
                string name = string.IsNullOrWhiteSpace(d.Title) ? "<без имени>" : d.Title!.Trim();
                int n = counters.GetValueOrDefault(name);
                counters[name] = n + 1;
                map[$"{name}" + (n > 0 ? $" #{n + 1}" : "")] = d;
            }
            return map;
        }

        private ComparisonItem? CompareOneDetail(
            Detail da, Detail db,
            IReadOnlyList<Metal>? metals,
            IReadOnlyList<TypeDetail>? typeDetails)
        {
            var item = new ComparisonItem { Category = $"Деталь «{da.Title}»", IsExpanded = false };

            AddInt(item, "Количество", da.Count, db.Count);

            CompareTypeDetails(da.TypeDetails, db.TypeDetails, item, metals, typeDetails);

            if (!item.Children.Any(c => c.HasDifference)) return null;
            item.Status = item.Children.Any(c =>
                c.Status is ComparisonStatus.Warning or ComparisonStatus.Critical)
                ? ComparisonStatus.Warning : ComparisonStatus.Different;
            item.IsExpanded = true;
            return item;
        }

        private void CompareTypeDetails(
            List<SaveTypeDetail> typesA,
            List<SaveTypeDetail> typesB,
            ComparisonItem parent,
            IReadOnlyList<Metal>? metals,
            IReadOnlyList<TypeDetail>? typeDetails)
        {
            int count = Math.Max(typesA.Count, typesB.Count);
            for (int i = 0; i < count; i++)
            {
                var ta = i < typesA.Count ? typesA[i] : null;
                var tb = i < typesB.Count ? typesB[i] : null;

                if (ta != null && tb == null)
                {
                    parent.Children.Add(new ComparisonItem
                    {
                        Category = $"Заготовка #{i + 1}",
                        Status = ComparisonStatus.OnlyInA,
                        Note = "отсутствует в B"
                    });
                    continue;
                }
                if (ta == null && tb != null)
                {
                    parent.Children.Add(new ComparisonItem
                    {
                        Category = $"Заготовка #{i + 1}",
                        Status = ComparisonStatus.OnlyInB,
                        Note = "добавлена в B"
                    });
                    continue;
                }

                var tg = new ComparisonItem { Category = $"Заготовка #{i + 1}", IsExpanded = false };
                AddInt(tg, "Кол-во", ta!.Count, tb!.Count);
                AddString(tg, "Тип заготовки",
                          ResolveTypeDetail(ta.Index, typeDetails),
                          ResolveTypeDetail(tb.Index, typeDetails));
                AddString(tg, "Металл",
                          ResolveMetal(ta.Metal, metals),
                          ResolveMetal(tb.Metal, metals));
                AddBool(tg, "Материал от исполнителя", ta.HasMetal, tb.HasMetal);
                AddFloat(tg, "Толщина S, мм", ta.Tuple.Item4, tb.Tuple.Item4, "F2");
                AddFloat(tg, "Стоимость материала, ₽", ta.ExtraResult, tb.ExtraResult, "N2");

                CompareWorks(ta.Works, tb.Works, tg);

                if (tg.Children.Any(c => c.HasDifference))
                {
                    tg.Status = tg.Children.Any(c =>
                        c.Status is ComparisonStatus.Warning or ComparisonStatus.Critical)
                        ? ComparisonStatus.Warning : ComparisonStatus.Different;
                    tg.IsExpanded = true;
                    parent.Children.Add(tg);
                }
            }
        }

        private void CompareWorks(List<SaveWork> worksA, List<SaveWork> worksB, ComparisonItem parent)
        {
            var byNameA = worksA.GroupBy(w => w.NameWork ?? "<без имени>")
                                .ToDictionary(g => g.Key, g => g.First());
            var byNameB = worksB.GroupBy(w => w.NameWork ?? "<без имени>")
                                .ToDictionary(g => g.Key, g => g.First());

            foreach (var name in byNameA.Keys.Union(byNameB.Keys))
            {
                var hasA = byNameA.TryGetValue(name, out var wa);
                var hasB = byNameB.TryGetValue(name, out var wb);

                if (hasA && !hasB) { parent.Children.Add(OnlyIn(name, "A")); continue; }
                if (!hasA && hasB) { parent.Children.Add(OnlyIn(name, "B")); continue; }

                var work = new ComparisonItem { Category = $"Работа «{name}»", IsExpanded = false };
                AddFloat(work, "Коэффициент", wa!.Ratio, wb!.Ratio, "F2");
                AddFloat(work, "Тех. коэффициент", wa.TechRatio, wb.TechRatio, "F2");
                AddFloat(work, "Доп. стоимость, ₽", wa.ExtraResult, wb.ExtraResult, "N2");

                if (wa.Parts?.Count > 0 || wb.Parts?.Count > 0)
                    CompareParts(wa.Parts ?? new(), wb.Parts ?? new(), work);

                if (work.Children.Any(c => c.HasDifference))
                {
                    work.Status = work.Children.Any(c =>
                        c.Status is ComparisonStatus.Warning or ComparisonStatus.Critical)
                        ? ComparisonStatus.Warning : ComparisonStatus.Different;
                    work.IsExpanded = true;
                    parent.Children.Add(work);
                }
            }
        }

        private void CompareParts(List<Part> partsA, List<Part> partsB, ComparisonItem parent)
        {
            var mapA = partsA.GroupBy(p => p.Title ?? "").ToDictionary(g => g.Key, g => g.First());
            var mapB = partsB.GroupBy(p => p.Title ?? "").ToDictionary(g => g.Key, g => g.First());

            foreach (var title in mapA.Keys.Union(mapB.Keys))
            {
                var hasA = mapA.TryGetValue(title, out var pa);
                var hasB = mapB.TryGetValue(title, out var pb);

                if (hasA && !hasB) { parent.Children.Add(OnlyIn(title, "A")); continue; }
                if (!hasA && hasB) { parent.Children.Add(OnlyIn(title, "B")); continue; }

                var part = new ComparisonItem { Category = $"Деталь «{title}»" };
                AddInt(part, "Кол-во", pa!.Count, pb!.Count);
                AddFloat(part, "Цена, ₽", pa.Price, pb.Price, "N2");

                if (part.Children.Any(c => c.HasDifference))
                {
                    part.Status = ComparisonStatus.Warning;
                    parent.Children.Add(part);
                }
            }
        }

        // ============================================================
        // ПОКУПНЫЕ ИЗДЕЛИЯ
        // ============================================================
        private void CompareBaskets(List<Part> basketsA, List<Part> basketsB, ComparisonResult r)
        {
            var root = new ComparisonItem { Category = "Покупные изделия", IsExpanded = false };
            var mapA = basketsA.GroupBy(p => p.Title ?? "").ToDictionary(g => g.Key, g => g.First());
            var mapB = basketsB.GroupBy(p => p.Title ?? "").ToDictionary(g => g.Key, g => g.First());

            foreach (var title in mapA.Keys.Union(mapB.Keys))
            {
                var hasA = mapA.TryGetValue(title, out var pa);
                var hasB = mapB.TryGetValue(title, out var pb);
                if (hasA && !hasB) { root.Children.Add(OnlyIn(title, "A")); continue; }
                if (!hasA && hasB) { root.Children.Add(OnlyIn(title, "B")); continue; }

                var item = new ComparisonItem { Category = $"«{title}»" };
                AddInt(item, "Кол-во", pa!.Count, pb!.Count);
                AddFloat(item, "Цена, ₽", pa.Price, pb.Price, "N2");
                if (item.Children.Any(c => c.HasDifference))
                {
                    item.Status = ComparisonStatus.Warning;
                    root.Children.Add(item);
                }
            }
            if (root.Children.Count > 0)
            {
                root.Status = ComparisonStatus.Warning;
                root.IsExpanded = true;
                r.Items.Add(root);
            }
        }

        // ============================================================
        // СБОРКИ
        // ============================================================
        private void CompareAssemblies(
            IEnumerable<Assembly> asmA, IEnumerable<Assembly> asmB, ComparisonResult r)
        {
            var root = new ComparisonItem { Category = "Сборки", IsExpanded = false };
            var mapA = asmA.GroupBy(x => x.Title ?? "").ToDictionary(g => g.Key, g => g.First());
            var mapB = asmB.GroupBy(x => x.Title ?? "").ToDictionary(g => g.Key, g => g.First());

            foreach (var title in mapA.Keys.Union(mapB.Keys))
            {
                var hasA = mapA.TryGetValue(title, out var a);
                var hasB = mapB.TryGetValue(title, out var b);
                if (hasA && !hasB) { root.Children.Add(OnlyIn(title, "A")); continue; }
                if (!hasA && hasB) { root.Children.Add(OnlyIn(title, "B")); continue; }

                var item = new ComparisonItem { Category = $"Сборка «{title}»" };
                AddInt(item, "Кол-во", a!.Count, b!.Count);
                AddFloat(item, "Сварка, ₽", a.WeldPrice, b.WeldPrice, "N2");
                AddFloat(item, "Окраска, ₽", a.PaintPrice, b.PaintPrice, "N2");

                if (item.Children.Any(c => c.HasDifference))
                {
                    item.Status = ComparisonStatus.Warning;
                    root.Children.Add(item);
                }
            }
            if (root.Children.Count > 0)
            {
                root.Status = ComparisonStatus.Warning;
                root.IsExpanded = true;
                r.Items.Add(root);
            }
        }

        // ============================================================
        // ДИАГНОСТИКА
        // ============================================================
        private void BuildDiagnostics(ComparisonResult r)
        {
            if (r.AmountA == 0 && r.AmountB == 0)
            {
                r.Diagnostics.Add(new DiagnosticMessage
                {
                    Text = "Оба расчёта пустые.",
                    Severity = ComparisonStatus.Different
                });
            }
            else
            {
                var absPercent = Math.Abs(r.DeltaPercent);
                if (absPercent > SignificantDiffPercent)
                    r.Diagnostics.Add(new DiagnosticMessage
                    {
                        Text = $"Итоговая сумма B {(r.DeltaTotal > 0 ? "больше" : "меньше")} A на " +
                               $"{Math.Abs(r.DeltaTotal):N0} ₽ ({absPercent:F1}%) — критично",
                        Severity = ComparisonStatus.Critical
                    });
                else if (Math.Abs(r.DeltaTotal) > MoneyTolerance)
                    r.Diagnostics.Add(new DiagnosticMessage
                    {
                        Text = $"Итоговая сумма B отличается от A на {Math.Abs(r.DeltaTotal):N0} ₽ " +
                               $"({absPercent:F1}%)",
                        Severity = ComparisonStatus.Warning
                    });
                else
                    r.Diagnostics.Add(new DiagnosticMessage
                    {
                        Text = $"Итоговые суммы совпадают: {r.AmountA:N0} ₽",
                        Severity = ComparisonStatus.Different
                    });
            }

            var addedDetails = new List<string>();
            var removedDetails = new List<string>();
            var addedBaskets = new List<string>();
            var removedBaskets = new List<string>();
            var addedAssemblies = new List<string>();
            var removedAssemblies = new List<string>();

            foreach (var item in r.Items)
            {
                if (item.Category == "Детали")
                {
                    foreach (var child in item.Children)
                    {
                        if (child.Status == ComparisonStatus.OnlyInA) removedDetails.Add(ExtractName(child.Category));
                        if (child.Status == ComparisonStatus.OnlyInB) addedDetails.Add(ExtractName(child.Category));
                    }
                }
                else if (item.Category == "Покупные изделия")
                {
                    foreach (var child in item.Children)
                    {
                        if (child.Status == ComparisonStatus.OnlyInA) removedBaskets.Add(ExtractName(child.Category));
                        if (child.Status == ComparisonStatus.OnlyInB) addedBaskets.Add(ExtractName(child.Category));
                    }
                }
                else if (item.Category == "Сборки")
                {
                    foreach (var child in item.Children)
                    {
                        if (child.Status == ComparisonStatus.OnlyInA) removedAssemblies.Add(ExtractName(child.Category));
                        if (child.Status == ComparisonStatus.OnlyInB) addedAssemblies.Add(ExtractName(child.Category));
                    }
                }
            }

            if (addedDetails.Count > 0)
                r.Diagnostics.Add(new DiagnosticMessage
                {
                    Text = $"В B добавлены детали: {string.Join(", ", addedDetails)}",
                    Severity = ComparisonStatus.Warning
                });
            if (removedDetails.Count > 0)
                r.Diagnostics.Add(new DiagnosticMessage
                {
                    Text = $"В B отсутствуют детали из A: {string.Join(", ", removedDetails)}",
                    Severity = ComparisonStatus.Warning
                });
            if (addedBaskets.Count > 0)
                r.Diagnostics.Add(new DiagnosticMessage
                {
                    Text = $"В B добавлены покупные: {string.Join(", ", addedBaskets)}",
                    Severity = ComparisonStatus.Warning
                });
            if (removedBaskets.Count > 0)
                r.Diagnostics.Add(new DiagnosticMessage
                {
                    Text = $"В B отсутствуют покупные из A: {string.Join(", ", removedBaskets)}",
                    Severity = ComparisonStatus.Warning
                });
            if (addedAssemblies.Count > 0)
                r.Diagnostics.Add(new DiagnosticMessage
                {
                    Text = $"В B добавлены сборки: {string.Join(", ", addedAssemblies)}",
                    Severity = ComparisonStatus.Warning
                });
            if (removedAssemblies.Count > 0)
                r.Diagnostics.Add(new DiagnosticMessage
                {
                    Text = $"В B отсутствуют сборки из A: {string.Join(", ", removedAssemblies)}",
                    Severity = ComparisonStatus.Warning
                });

            int onlyA = 0, onlyB = 0, warnings = 0, critical = 0, infos = 0;
            Traverse(r.Items, item =>
            {
                switch (item.Status)
                {
                    case ComparisonStatus.OnlyInA: onlyA++; break;
                    case ComparisonStatus.OnlyInB: onlyB++; break;
                    case ComparisonStatus.Warning: warnings++; break;
                    case ComparisonStatus.Critical: critical++; break;
                    case ComparisonStatus.Different: infos++; break;
                }
            });

            r.CriticalCount = critical;
            r.WarningCount = warnings;
            r.DifferenceCount = critical + warnings + infos + onlyA + onlyB;
        }

        private static string ExtractName(string category)
        {
            int first = category.IndexOf('«');
            int last = category.LastIndexOf('»');
            return (first >= 0 && last > first)
                ? category.Substring(first + 1, last - first - 1)
                : category;
        }

        private static void Traverse(IEnumerable<ComparisonItem> items, Action<ComparisonItem> action)
        {
            foreach (var it in items)
            {
                action(it);
                if (it.Children.Count > 0) Traverse(it.Children, action);
            }
        }

        // ============================================================
        // ХЕЛПЕРЫ
        // ============================================================
        private static ComparisonItem OnlyIn(string name, string side) => new()
        {
            Category = $"«{name}»",
            Status = side == "A" ? ComparisonStatus.OnlyInA : ComparisonStatus.OnlyInB,
            Note = side == "A" ? "отсутствует в B" : "добавлена в B"
        };

        // ❗ ИСПРАВЛЕНО: теперь в "Difference" пишется число со знаком,
        //   а не литералы "+F1" / "-N2".
        private static void AddDecimal(ComparisonItem p, string prop, decimal a, decimal b, string fmt)
        {
            bool eq = Math.Abs(a - b) <= MoneyTolerance;
            if (eq && fmt is "F1" or "F2")
                eq = Math.Abs(a - b) <= (decimal)RatioTolerance;

            string diff = "";
            if (!eq)
            {
                decimal delta = b - a;
                string absText = Math.Abs(delta).ToString(fmt, CultureInfo.CurrentCulture);
                diff = (delta > 0 ? "+" : "−") + absText;
            }

            p.Children.Add(new ComparisonItem
            {
                Property = prop,
                ValueA = a.ToString(fmt, CultureInfo.CurrentCulture),
                ValueB = b.ToString(fmt, CultureInfo.CurrentCulture),
                Difference = diff,
                Status = eq ? ComparisonStatus.Equal : ComparisonStatus.Different
            });
        }

        private static void AddFloat(ComparisonItem p, string prop, float a, float b, string fmt) =>
            AddDecimal(p, prop, (decimal)a, (decimal)b, fmt);

        private static void AddInt(ComparisonItem p, string prop, int a, int b)
        {
            p.Children.Add(new ComparisonItem
            {
                Property = prop,
                ValueA = a.ToString(CultureInfo.CurrentCulture),
                ValueB = b.ToString(CultureInfo.CurrentCulture),
                Difference = a == b ? "" : ((b - a) > 0 ? "+" : "−") +
                                        Math.Abs(b - a).ToString(CultureInfo.CurrentCulture),
                Status = a == b ? ComparisonStatus.Equal : ComparisonStatus.Different
            });
        }

        private static void AddString(ComparisonItem p, string prop, string? a, string? b)
        {
            a ??= "";
            b ??= "";
            bool eq = string.Equals(a, b, StringComparison.Ordinal);
            p.Children.Add(new ComparisonItem
            {
                Property = prop,
                ValueA = string.IsNullOrEmpty(a) ? "—" : a,
                ValueB = string.IsNullOrEmpty(b) ? "—" : b,
                Status = eq ? ComparisonStatus.Equal : ComparisonStatus.Different
            });
        }

        private static void AddBool(ComparisonItem p, string prop, bool? a, bool? b)
        {
            bool eq = a == b;
            p.Children.Add(new ComparisonItem
            {
                Property = prop,
                ValueA = a == null ? "—" : (a == true ? "да" : "нет"),
                ValueB = b == null ? "—" : (b == true ? "да" : "нет"),
                Status = eq ? ComparisonStatus.Equal : ComparisonStatus.Warning
            });
        }

        /// <summary>
        /// Возвращает множитель конструктора, приводя пустое значение или 0 к 1.
        /// При создании нового расчёта поле пустое — без этой нормализации сравнение
        /// показывало бы ложное расхождение «1 vs пусто/0».
        /// </summary>
        private static float ParseConstructRatio(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 1f;

            if (!float.TryParse(s.Replace(',', '.'),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out var f))
                return 1f;

            return f > 0f ? f : 1f;
        }

        /// <summary>
        /// Преобразует индекс металла в его наименование.
        /// -1 (не выбран) → «не выбран»; выход за границы → «#index».
        /// </summary>
        private static string ResolveMetal(int index, IReadOnlyList<Metal>? metals)
        {
            if (index < 0) return "не выбран";
            if (metals == null || index >= metals.Count) return $"#{index}";

            var name = metals[index]?.Name;
            return string.IsNullOrWhiteSpace(name) ? $"#{index}" : name!;
        }

        /// <summary>
        /// Преобразует индекс типа заготовки в его наименование.
        /// -1 (не выбран) → «не выбран»; выход за границы → «#index».
        /// </summary>
        private static string ResolveTypeDetail(int index, IReadOnlyList<TypeDetail>? types)
        {
            if (index < 0) return "не выбран";
            if (types == null || index >= types.Count) return $"#{index}";

            var name = types[index]?.Name;
            return string.IsNullOrWhiteSpace(name) ? $"#{index}" : name!;
        }
    }
}