using Metal_Code.Models;
using Metal_Code.Utils;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    public partial class FolderPresetSettingsWindow : Window
    {
        public ObservableCollection<FolderPreset> Presets { get; }

        public FolderPresetSettingsWindow(ObservableCollection<FolderPreset> presets)
        {
            Presets = presets;
            InitializeComponent();
            DataContext = this;
            PresetsList.ItemsSource = Presets;
        }

        private void AddPreset_Click(object sender, RoutedEventArgs e)
        {
            Presets.Add(new FolderPreset($"Папка {Presets.Count + 1}"));
            PresetsList.ScrollIntoView(Presets[^1]);
        }

        private void RemovePreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is FolderPreset preset)
                Presets.Remove(preset);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Сбросить список к стандартному?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Presets.Clear();
                foreach (var p in FolderPresetManager.GetDefaults()) Presets.Add(p);
            }
        }

        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            // Валидация: убрать пустые имена
            foreach (var p in Presets)
                if (string.IsNullOrWhiteSpace(p.Name)) p.Name = "Без названия";

            FolderPresetManager.SavePresets(Presets);
            DialogResult = true;
            Close();
        }
    }
}