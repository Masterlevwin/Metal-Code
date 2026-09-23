using Metal_Code.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Metal_Code
{
    public partial class PipeRemnantsWindow : Window
    {
        public ObservableCollection<PipeRemnant> Remnants { get; } = new();
        public bool IsConfirmed { get; private set; }

        // Конструктор принимает текущий список остатков (если он уже где-то хранится)
        public PipeRemnantsWindow(ObservableCollection<PipeRemnant>? existingRemnants = null)
        {
            InitializeComponent();
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
            string profile = TxtProfile.Text.Trim();
            if (string.IsNullOrEmpty(profile))
            {
                MessageBox.Show("Укажите профиль трубы.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double length = NumLength.Value;
            int count = (int)NumCount.Value;

            // Проверяем, есть ли уже такой остаток, и если да - суммируем количество
            var existing = Remnants.FirstOrDefault(r => r.ProfileName == profile && r.Length == length);
            if (existing != null)
            {
                existing.Count += count;
            }
            else
            {
                Remnants.Add(new PipeRemnant { ProfileName = profile, Length = length, Count = count });
            }

            TxtProfile.Clear();
            NumLength.Value = 4000;
            NumCount.Value = 1;
            TxtProfile.Focus();
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