using Metal_Code.Utils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    public partial class StandartPartWindow : Window
    {
        private readonly Part _currentPart;
        private readonly ObservableCollection<Part> _batchBuffer = new();
        private bool _isUpdatingPreview = false;

        public StandartPartWindow(Part templatePart)
        {
            InitializeComponent();
            _currentPart = templatePart;
            DataContext = _currentPart;
            BatchItemsControl.ItemsSource = _batchBuffer;

            // Подписка на изменения отверстий
            if (_currentPart.HoleGroups is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += (s, e) => UpdatePreview();
            }

            UpdatePreview();
        }

        // Свойство для получения результата после закрытия окна
        public List<Part> GetBatchedParts() => new(_batchBuffer);

        private void AddHoleGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(DiameterInput.Text, out double diameter) || diameter <= 0)
            {
                MessageBox.Show("Введите корректный диаметр отверстия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(CountInput.Text, out int count) || count <= 0)
            {
                MessageBox.Show("Введите корректное количество отверстий", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (diameter < 1 || diameter > 100)
            {
                MessageBox.Show("Диаметр должен быть от 1 до 100 мм", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (count > 50)
            {
                MessageBox.Show("Максимальное количество отверстий в группе - 50", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _currentPart.HoleGroups.Add(new HoleGroup(diameter, count));
            DiameterInput.Text = "10";
            CountInput.Text = "1";
            UpdatePreview();
        }

        private void RemoveHoleGroup_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is HoleGroup group)
            {
                _currentPart.HoleGroups.Remove(group);
                UpdatePreview();
            }
        }

        // Добавление текущей детали в буфер
        private void AddToBatch_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentPart.Title))
            {
                MessageBox.Show("Укажите название детали", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_currentPart.Count <= 0)
            {
                MessageBox.Show("Укажите количество деталей", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Валидация отверстий
            var (isValid, error) = PartPreviewGenerator.ValidateHolesPlacement(_currentPart);
            if (!isValid)
            {
                if (MessageBox.Show(
                    $"Проблемы с отверстиями:\n{error}\n\nПродолжить без отверстий?",
                    "Предупреждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
                _currentPart.HoleGroups.Clear();
            }

            // Клонируем деталь для буфера
            var clonedPart = ClonePart(_currentPart);
            if (clonedPart != null) _batchBuffer.Add(clonedPart);

            // Сбрасываем текущую деталь для новой
            _currentPart.HoleGroups.Clear();
            UpdatePreview();
        }

        // Завершение и расчёт
        private void FinishBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_batchBuffer.Count == 0)
            {
                MessageBox.Show("Список деталей пуст. Добавьте хотя бы одну деталь.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void RemoveFromBatch_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is Part part)
            {
                _batchBuffer.Remove(part);
                MessageBox.Show("Деталь удалена из списка", "Удалено", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private Part? ClonePart(Part source)
        {
            if (source.DisplayGeometry is null) return null;

            return new Part
            {
                Title = source.Title,
                Count = source.Count,
                Metal = source.Metal,
                Destiny = source.Destiny,
                Width = source.Width,
                Height = source.Height,
                Length = source.Length,
                PartType = source.PartType,
                HoleGroups = new List<HoleGroup>(source.HoleGroups),
                DisplayGeometry = PartPreviewGenerator.CloneGeometry(source.DisplayGeometry),
                PropsDict = new Dictionary<int, List<string>>(source.PropsDict)
            };
        }

        private void UpdatePreview()
        {
            if (_isUpdatingPreview) return;
            _isUpdatingPreview = true;
            try
            {
                PartPreviewGenerator.EnsureDisplayGeometryWithHoles(_currentPart);
                UpdateStatistics();
            }
            finally
            {
                _isUpdatingPreview = false;
            }
        }

        private void UpdateStatistics()
        {
            if (HoleStatsText == null) return;

            int totalCount = _currentPart.HoleGroups.Sum(g => g.Count);
            double totalArea = _currentPart.HoleGroups.Sum(g => g.TotalArea);

            HoleStatsText.Text = $"Всего отверстий: {totalCount} шт\n" +
                                $"Суммарная площадь: {totalArea:F1} мм²";
        }
    }
}