using System;
using System.IO;
using System.Text.RegularExpressions;

public static class FileSorter
{
    private static readonly string[] MonthNames = {
        "январь", "февраль", "март", "апрель", "май", "июнь",
        "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"
    };

    // Регулярка для формата: ЛЮБОЕ_ИМЯ_ГГГГ_ММ_ДД_... (например: vid_2025_06_03-123456.mp4)
    // Ищем 4 цифры, затем 2 цифры, затем 2 цифры — разделённые подчёркиванием
    private static readonly Regex FormatStructuredRegex = new Regex(
        @"(\d{4})_(\d{1,2})_(\d{1,2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Регулярка для формата: любое вхождение ГГГГММДД (8 цифр подряд)
    private static readonly Regex FormatCompactRegex = new Regex(
        @"(?<!\d)(\d{4})(\d{2})(\d{2})(?!\d)",
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

            // === ШАГ 1: Попробуем найти дату по структурированному формату (YYYY_MM_DD) ===
            var match1 = FormatStructuredRegex.Match(fileName);
            if (match1.Success)
            {
                string yearStr = match1.Groups[1].Value;
                string monthStr = match1.Groups[2].Value;
                string dayStr = match1.Groups[3].Value;

                if (TryExtractDateFromParts(yearStr, monthStr, dayStr, out DateTime date))
                {
                    parsedDate = date;
                    Console.WriteLine($"[Приоритет] Найдена структурированная дата: {date:yyyy-MM-dd} в файле {fileName}");
                }
            }

            // === ШАГ 2: Если структурированная дата не найдена — пробуем компактный формат (YYYYMMDD) ===
            if (!parsedDate.HasValue)
            {
                var matches = FormatCompactRegex.Matches(fileName);
                foreach (Match match in matches)
                {
                    string yearStr = match.Groups[1].Value;
                    string monthStr = match.Groups[2].Value;
                    string dayStr = match.Groups[3].Value;

                    if (TryExtractDateFromParts(yearStr, monthStr, dayStr, out DateTime date))
                    {
                        parsedDate = date;
                        Console.WriteLine($"[Резерв] Найдена компактная дата: {date:yyyy-MM-dd} в файле {fileName}");
                        break; // Берём первую подходящую
                    }
                }
            }

            // === Если ни одна дата не найдена ===
            if (!parsedDate.HasValue)
            {
                Console.WriteLine($"Пропущен файл (не удалось извлечь дату): {fileName}");
                continue;
            }

            // === Создание папки и копирование ===
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

    // Проверяет, является ли комбинация год/месяц/день корректной датой
    private static bool TryExtractDateFromParts(string yearStr, string monthStr, string dayStr, out DateTime result)
    {
        result = default;

        if (!int.TryParse(yearStr, out int year) ||
            !int.TryParse(monthStr, out int month) ||
            !int.TryParse(dayStr, out int day))
        {
            return false;
        }

        if (year < 1900 || year > 2100 || month < 1 || month > 12 || day < 1 || day > 31)
        {
            return false;
        }

        return DateTime.TryParse($"{year}-{month:D2}-{day:D2}", out result);
    }
}