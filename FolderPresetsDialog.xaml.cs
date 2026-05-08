using Metal_Code.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    public partial class FolderPresetsDialog : Window // 👈 Наследуемся от Window
    {
        private readonly ObservableCollection<FolderPreset> _sourcePresets;
        private readonly List<CheckBox> _checkBoxes = new();

        public FolderPresetsDialog(ObservableCollection<FolderPreset> sourcePresets)
        {
            _sourcePresets = sourcePresets;
            InitializeComponent();
            BuildCheckboxes();
        }

        private void BuildCheckboxes()
        {
            PresetsContainer.Children.Clear();
            _checkBoxes.Clear();

            foreach (var preset in _sourcePresets)
            {
                var cb = new CheckBox
                {
                    Content = preset.Name,
                    IsChecked = preset.IsEnabled,
                    Margin = new Thickness(0, 0, 0, 8),
                    FontSize = 13,
                    Tag = preset
                };

                cb.SetResourceReference(ForegroundProperty, "PrimaryTextBrush");

                _checkBoxes.Add(cb);
                PresetsContainer.Children.Add(cb);
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _checkBoxes) cb.IsChecked = true;
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _checkBoxes) cb.IsChecked = false;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // Применяем изменения обратно в источник
            foreach (var cb in _checkBoxes)
            {
                if (cb.Tag is FolderPreset preset)
                {
                    preset.IsEnabled = cb.IsChecked == true;
                }
            }

            // 👇 Устанавливаем результат и закрываем окно
            DialogResult = true;
            Close();
        }
    }
}