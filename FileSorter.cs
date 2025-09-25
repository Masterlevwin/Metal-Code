using System;
using System.IO;
using System.Text.RegularExpressions;

public static class FileSorter
{
    private static readonly string[] MonthNames = {
        "январь", "февраль", "март", "апрель", "май", "июнь",
        "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"
    };

    // Регулярка для имён типа: image_2025_04_06.jpg
    private static readonly Regex Format1Regex = new Regex(
        @"^_(\d{4})_(\d{1,2})_(\d{1,2})\.",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Регулярка для имён типа: ...20250406..., например: photo_20250406.jpg, IMG_20250406_abc.png
    private static readonly Regex Format2Regex = new Regex(
        @"(\d{4})(\d{2})(\d{2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
            if (string.IsNullOrEmpty(fileName)) continue;

            DateTime? parsedDate = null;

            // Попробуем первый формат: image_YYYY_MM_DD
            var match1 = Format1Regex.Match(fileName);
            if (match1.Success)
            {
                if (TryExtractDateFromParts(match1.Groups[1].Value, match1.Groups[2].Value, match1.Groups[3].Value, out DateTime date))
                {
                    parsedDate = date;
                }
            }
            else
            {
                // Попробуем второй формат: любое имя с 8 цифрами подряд: YYYYMMDD
                var matches = Format2Regex.Matches(fileName);
                foreach (Match match in matches)
                {
                    string yearStr = match.Groups[1].Value;
                    string monthStr = match.Groups[2].Value;
                    string dayStr = match.Groups[3].Value;

                    if (TryExtractDateFromParts(yearStr, monthStr, dayStr, out DateTime date))
                    {
                        parsedDate = date;
                        break; // Нашли первую подходящую дату
                    }
                }
            }

            if (!parsedDate.HasValue)
            {
                Console.WriteLine($"Пропущен файл (не удалось извлечь дату): {fileName}");
                continue;
            }

            int year = parsedDate.Value.Year;
            int month = parsedDate.Value.Month;
            string monthName = MonthNames[month - 1];
            string targetFolderName = $"{year:D4}_{monthName}";
            string targetFolderPath = Path.Combine(sourceDirectory, targetFolderName);

            Directory.CreateDirectory(targetFolderPath);

            string destinationFile = Path.Combine(targetFolderPath, fileName);

            try
            {
                File.Copy(file, destinationFile, true);
                Console.WriteLine($"Файл скопирован: {fileName} → {targetFolderName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при копировании файла {fileName}: {ex.Message}");
            }
        }

        Console.WriteLine("Сортировка завершена.");
    }

    // Вспомогательный метод: проверяет, является ли тройка год/месяц/день корректной датой
    private static bool TryExtractDateFromParts(string yearStr, string monthStr, string dayStr, out DateTime result)
    {
        result = default;

        if (!int.TryParse(yearStr, out int year) ||
            !int.TryParse(monthStr, out int month) ||
            !int.TryParse(dayStr, out int day))
        {
            return false;
        }

        // Базовые проверки диапазонов
        if (year < 1900 || year > 2100 || month < 1 || month > 12 || day < 1 || day > 31)
        {
            return false;
        }

        // Полная валидация даты через DateTime
        return DateTime.TryParse($"{year}-{month:D2}-{day:D2}", out result);
    }
}