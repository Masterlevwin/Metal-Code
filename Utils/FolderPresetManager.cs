using Metal_Code.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Metal_Code.Utils
{
    public static class FolderPresetManager
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MetalCode", "folder_presets.json");

        private static ObservableCollection<FolderPreset>? _cachedPresets;

        public static ObservableCollection<FolderPreset> GetDefaults() => new()
        {
            new("Чертежи на согласование"),
            new("Проверить!"),
        };

        public static ObservableCollection<FolderPreset> LoadPresets()
        {
            if (_cachedPresets != null) return _cachedPresets;

            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    _cachedPresets = JsonSerializer.Deserialize<ObservableCollection<FolderPreset>>(json) ?? GetDefaults();
                }
                else
                {
                    _cachedPresets = GetDefaults();
                    SavePresets(_cachedPresets);
                }
            }
            catch
            {
                _cachedPresets = GetDefaults();
            }
            return _cachedPresets;
        }

        public static void SavePresets(ObservableCollection<FolderPreset> presets)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                var json = JsonSerializer.Serialize(presets, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(ConfigPath, json);
                _cachedPresets = presets;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить шаблоны папок: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public static void ResetToDefault()
        {
            _cachedPresets = GetDefaults();
            SavePresets(_cachedPresets);
        }
    }
}