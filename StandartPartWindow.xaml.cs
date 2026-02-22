using Metal_Code.Utils;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для StandartPartWindow.xaml
    /// </summary>
    public partial class StandartPartWindow : Window
    {
        private readonly Part _part;
        private bool _isUpdatingPreview = false;

        public StandartPartWindow(Part part)
        {
            InitializeComponent();
            _part = part;
            DataContext = part;

            // Подписываемся на изменения
            if (part.HoleGroups is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += (s, e) => UpdatePreview();
            }

            // Начальное обновление
            UpdatePreview();
        }

        private void AddHoleGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(DiameterInput.Text, out double diameter) || diameter <= 0)
            {
                ShowError("Введите корректный диаметр отверстия");
                return;
            }

            if (!int.TryParse(CountInput.Text, out int count) || count <= 0)
            {
                ShowError("Введите корректное количество отверстий");
                return;
            }

            // Ограничения
            if (diameter < 1 || diameter > 100)
            {
                ShowError("Диаметр должен быть от 1 до 100 мм");
                return;
            }

            if (count > 50)
            {
                ShowError("Максимальное количество отверстий в группе - 50");
                return;
            }

            // Добавляем группу
            _part.HoleGroups.Add(new HoleGroup(diameter, count));

            // Сбрасываем поля ввода
            DiameterInput.Text = "10";
            CountInput.Text = "1";

            UpdatePreview();
        }

        private void RemoveHoleGroup_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is HoleGroup group)
            {
                _part.HoleGroups.Remove(group);
                UpdatePreview();
            }
        }

        private void Accept(object sender, RoutedEventArgs e)
        {
            // Финальная валидация
            var (isValid, error) = PartPreviewGenerator.ValidateHolesPlacement(_part);

            if (!isValid)
            {
                if (MessageBox.Show(
                    $"Обнаружены проблемы с размещением отверстий:\n{error}\n\n" +
                    $"Продолжить без отверстий?",
                    "Предупреждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }

                // Удаляем все отверстия
                _part.HoleGroups.Clear();
            }

            // Генерируем финальную геометрию
            PartPreviewGenerator.EnsureDisplayGeometryWithHoles(_part);
            DialogResult = true;
            Close();
        }

        private void UpdatePreview()
        {
            if (_isUpdatingPreview) return;

            _isUpdatingPreview = true;

            try
            {
                // Обновляем превью
                PartPreviewGenerator.EnsureDisplayGeometryWithHoles(_part);

                // Обновляем статистику
                UpdateStatistics();

                // Проверяем валидность
                ValidateAndShowErrors();
            }
            finally
            {
                _isUpdatingPreview = false;
            }
        }

        private void UpdateStatistics()
        {
            if (HoleStatsText == null) return;

            int totalCount = _part.HoleGroups.Sum(g => g.Count);
            double totalArea = _part.HoleGroups.Sum(g => g.TotalArea);

            HoleStatsText.Text = $"Всего отверстий: {totalCount} шт\n" +
                                $"Суммарная площадь: {totalArea:F1} мм²";
        }

        private void ValidateAndShowErrors()
        {
            var (isValid, error) = PartPreviewGenerator.ValidateHolesPlacement(_part);

            if (!isValid)
            {
                ValidationErrorBorder.Visibility = Visibility.Visible;
                ValidationErrorMessage.Text = error;
            }
            else
            {
                ValidationErrorBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}