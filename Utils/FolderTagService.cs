using Metal_Code.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Metal_Code.Utils
{
    public static class FolderTagService
    {
        /// <summary>
        /// Извлекает из комментария тэги, которые создают папки
        /// </summary>
        public static List<string> GetFolderTagsFromComment(string? comment, IEnumerable<CommentTag> allTags)
        {
            if (string.IsNullOrWhiteSpace(comment))
                return new List<string>();

            var folderTags = new List<string>();

            foreach (var tag in allTags.Where(t => t.CreatesFolder))
            {
                // Проверяем, есть ли текст тэга в комментарии
                if (comment.Contains(tag.Text, System.StringComparison.OrdinalIgnoreCase))
                {
                    // Очищаем имя папки от недопустимых символов
                    string folderName = SanitizeFolderName(tag.Text);
                    if (!string.IsNullOrWhiteSpace(folderName))
                        folderTags.Add(folderName);
                }
            }

            return folderTags;
        }

        /// <summary>
        /// Создаёт папки для тэгов в папке расчета
        /// </summary>
        public static List<string> CreateFolderTags(string calculationFolderPath, List<string> folderTagNames)
        {
            var createdFolders = new List<string>();

            if (string.IsNullOrWhiteSpace(calculationFolderPath) || folderTagNames == null)
                return createdFolders;

            foreach (var folderName in folderTagNames)
            {
                try
                {
                    string fullPath = Path.Combine(calculationFolderPath, folderName);

                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                        createdFolders.Add(folderName);
                    }
                }
                catch (System.Exception ex)
                {
                    // Логируем ошибку, но продолжаем создание остальных папок
                    System.Diagnostics.Debug.WriteLine(
                        $"Не удалось создать папку '{folderName}': {ex.Message}");
                }
            }

            return createdFolders;
        }

        /// <summary>
        /// Очищает имя от недопустимых символов для папки
        /// </summary>
        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Получаем недопустимые символы для имен папок
            char[] invalidChars = Path.GetInvalidFileNameChars();
            invalidChars = invalidChars.Concat(new[] { ':', '*', '?', '"', '<', '>', '|' }).Distinct().ToArray();

            string result = name;
            foreach (char c in invalidChars)
            {
                result = result.Replace(c, '_');
            }

            // Убираем пробелы в начале и конце
            result = result.Trim();

            // Заменяем множественные пробелы на один
            result = Regex.Replace(result, @"\s+", " ");

            return result;
        }

        /// <summary>
        /// Проверяет, есть ли в комментарии тэги-папки
        /// </summary>
        public static bool HasFolderTags(string? comment, IEnumerable<CommentTag> allTags)
        {
            return GetFolderTagsFromComment(comment, allTags).Any();
        }
    }
}