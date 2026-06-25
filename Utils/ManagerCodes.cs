using Metal_Code.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Metal_Code
{
    /// <summary>
    /// Правила определения менеджера по номеру заказа.
    /// Использует имя менеджера (стабильный идентификатор), а не Id.
    /// </summary>
    public static class ManagerCodes
    {
        // ⭐ Правила: (длина номера, префикс, имя менеджера)
        // ⚠️ Имена должны ТОЧНО совпадать с именами в базе данных
        private static readonly List<(int Length, string Prefix, string ManagerName)> Rules = new()
        {
            // 6-значные номера
            (6, "11", "Еремин Андрей"),
            
            // 5-значные номера
            (5, "1",  "Сергеев Юрий"),
            (5, "2",  "Сергеев Алексей"),
            (5, "3",  "Спильная Марина"),
            (5, "4",  "Серых Михаил"),
            (5, "5",  "Барабанов Дмитрий"),
            (5, "7",  "Андрейченко Алексей"),
            (5, "8",  "Абрамова Анна"),
            (5, "9",  "Гамолина Светлана"),
        };

        /// <summary>
        /// Возвращает префикс (код) для указанного менеджера.
        /// Используется при выборе менеджера в дропе — записываем префикс в Order.
        /// </summary>
        public static string GetCode(Manager manager)
        {
            if (manager?.Name == null) return "";

            // ⭐ Ищем правило по имени менеджера (сравнение без учёта регистра)
            var rule = Rules.FirstOrDefault(r =>
                string.Equals(r.ManagerName, manager.Name, StringComparison.OrdinalIgnoreCase));

            return rule.Prefix;
        }

        /// <summary>
        /// Определяет имя менеджера по номеру заказа с учётом длины и префикса.
        /// </summary>
        public static string? ExtractManagerNameFromOrder(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber)) return null;

            // ⭐ Извлекаем только цифры из начала строки
            var match = Regex.Match(orderNumber.Trim(), @"^\d+");
            if (!match.Success) return null;

            string digits = match.Value;
            int length = digits.Length;

            // ⭐ Ищем правило по длине и префиксу
            foreach (var rule in Rules)
            {
                if (rule.Length == length && digits.StartsWith(rule.Prefix))
                    return rule.ManagerName;
            }

            return null;
        }

        /// <summary>
        /// Находит менеджера по имени из списка (сравнение без учёта регистра и пробелов).
        /// </summary>
        public static Manager? FindByName(string managerName, IEnumerable<Manager> managers)
        {
            if (string.IsNullOrWhiteSpace(managerName) || managers == null) return null;

            return managers.FirstOrDefault(m =>
                string.Equals(m.Name?.Trim(), managerName.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}