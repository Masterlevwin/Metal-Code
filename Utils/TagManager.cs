using Metal_Code.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Metal_Code.Utils
{
    public static class TagManager
    {
        private static readonly string TagsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MetalCode", "comment_tags.json");

        private static ObservableCollection<CommentTag>? _cachedTags;

        public static ObservableCollection<CommentTag> GetDefaultTags() => new()
        {
            new("рифл", " Рифленка!", false),
            new("азот", " Азот!", false),
            new("шлиф", " Внимание на направление шлифовки!", false),
            new("плен", " Пленку не снимать!", false),
            new("чист", " Чистый материал! Без царапин!", false)
        };

        public static ObservableCollection<CommentTag> LoadTags()
        {
            if (_cachedTags != null) return _cachedTags;

            try
            {
                if (File.Exists(TagsPath))
                {
                    var json = File.ReadAllText(TagsPath);
                    var tags = JsonSerializer.Deserialize<ObservableCollection<CommentTag>>(json);
                    _cachedTags = tags ?? GetDefaultTags();
                }
                else
                {
                    _cachedTags = GetDefaultTags();
                    SaveTags(_cachedTags);
                }
            }
            catch
            {
                _cachedTags = GetDefaultTags();
            }
            return _cachedTags;
        }

        public static void SaveTags(ObservableCollection<CommentTag> tags)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TagsPath)!);
                var json = JsonSerializer.Serialize(tags, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(TagsPath, json);
                _cachedTags = tags;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить тэги: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public static void ResetToDefault()
        {
            _cachedTags = GetDefaultTags();
            SaveTags(_cachedTags);
        }
    }
}