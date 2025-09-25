using System;
using System.IO;
using System.Text.RegularExpressions;

public static class FileSorter
{
    // Регулярное выражение для парсинга имён файлов: image_год_месяц_день
    private static readonly Regex FileNameRegex = new Regex(
        @"^image_(\d{4})_(\d{1,2})_(\d{1,2})\.",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Сортирует файлы по папкам "год_месяц" на основе имени файла.
    /// </summary>
    /// <param name="sourceDirectory">Путь к папке с неотсортированными файлами</param>
    public static void SortFilesByMonth(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Директория не найдена: {sourceDirectory}");
        }

        var files = Directory.GetFiles(sourceDirectory);

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var match = FileNameRegex.Match(fileName);

            if (!match.Success)
            {
                Console.WriteLine($"Пропущен файл (не соответствует формату): {fileName}");
                continue; // Пропускаем файлы с неправильным именем
            }

            // Извлекаем год и месяц
            if (!int.TryParse(match.Groups[1].Value, out int year) ||
                !int.TryParse(match.Groups[2].Value, out int month))
            {
                Console.WriteLine($"Не удалось распарсить дату из имени: {fileName}");
                continue;
            }

            // Проверяем корректность даты
            if (year < 1900 || year > 2100 || month < 1 || month > 12)
            {
                Console.WriteLine($"Некорректная дата в имени файла: {fileName}");
                continue;
            }

            // Формируем имя папки: "2023_10"
            string targetFolderName = $"{year:D4}_{month:D2}";
            string targetFolderPath = Path.Combine(sourceDirectory, targetFolderName);

            // Создаём папку, если её нет
            Directory.CreateDirectory(targetFolderPath);

            // Формируем путь копирования
            string destinationFile = Path.Combine(targetFolderPath, fileName);

            try
            {
                // Копируем файл (если уже есть — перезаписываем)
                File.Copy(file, destinationFile, true);
                Console.WriteLine($"Файл скопирован: {fileName} -> {targetFolderName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при копировании файла {fileName}: {ex.Message}");
            }
        }

        Console.WriteLine("Сортировка завершена.");
    }
}