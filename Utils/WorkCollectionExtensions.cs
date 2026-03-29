using System;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code.Utils
{
    public static class WorkCollectionExtensions
    {
        /// <summary>
        /// Возвращает коллекцию, где указанные работы идут в заданном порядке первыми,
        /// остальные — в исходном порядке следования.
        /// </summary>
        public static IEnumerable<T> OrderByPriority<T>(
            this IEnumerable<T> source,
            Func<T, string> nameSelector,
            params string[] priorityNames)
        {
            var priorityMap = priorityNames
                .Select((name, index) => new { name, index })
                .ToDictionary(x => x.name, x => x.index);

            var priorityItems = source
                .Where(item => priorityMap.ContainsKey(nameSelector(item)))
                .OrderBy(item => priorityMap[nameSelector(item)]);

            var otherItems = source
                .Where(item => !priorityMap.ContainsKey(nameSelector(item)));

            return priorityItems.Concat(otherItems);
        }
    }
}