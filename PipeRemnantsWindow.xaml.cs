using Metal_Code.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Metal_Code
{
    public partial class PipeRemnantsWindow : Window
    {
        private readonly string _baseProfileName;

        public ObservableCollection<PipeRemnant> Remnants { get; } = new();
        public bool IsConfirmed { get; private set; }

        // 🔥 Добавляем параметр currentProfileName
        public PipeRemnantsWindow(string currentProfileName, ObservableCollection<PipeRemnant> existingRemnants = null)
        {
            InitializeComponent();

            // Защита от null, если по какой-то причине имя не передано
            _baseProfileName = string.IsNullOrWhiteSpace(currentProfileName) ? "Профиль" : currentProfileName;

            RemnantsDataGrid.ItemsSource = Remnants;

            if (existingRemnants != null)
            {
                foreach (var r in existingRemnants)
                {
                    Remnants.Add(new PipeRemnant
                    {
                        ProfileName = r.ProfileName,
                        Length = r.Length,
                        Count = r.Count
                    });
                }
            }
        }

        private void AddRemnant_Click(object sender, RoutedEventArgs e)
        {
            double length = NumLength.Value;
            int count = (int)NumCount.Value;

            // 🔥 АВТОГЕНЕРАЦИЯ ИМЕНИ: "Профиль_Длина" (например: "40x40x3 aisi304_4000")
            string generatedProfileName = $"{_baseProfileName}_{length:0}";

            // Проверяем, есть ли уже такой остаток, и если да - суммируем количество
            var existing = Remnants.FirstOrDefault(r => r.ProfileName == generatedProfileName && r.Length == length);
            if (existing != null)
            {
                existing.Count += count;
            }
            else
            {
                Remnants.Add(new PipeRemnant { ProfileName = generatedProfileName, Length = length, Count = count });
            }

            // Сброс полей для быстрого добавления следующего
            NumLength.Value = 4000;
            NumCount.Value = 1;
            NumLength.Focus();
        }

        private void DeleteRemnant_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PipeRemnant remnant)
            {
                Remnants.Remove(remnant);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
}