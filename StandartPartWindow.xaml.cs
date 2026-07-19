using Metal_Code.Utils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    public partial class StandartPartWindow : Window
    {
        private readonly Part _currentPart;
        private readonly ObservableCollection<Part> _batchBuffer = new();

        // Определяет режим нестинга
        public bool UseAutoNesting { get; private set; } = true; // По умолчанию включено

        // Пользовательский отступ (0 означает "авто")
        public double CustomSpacing { get; private set; } = 0;

        public StandartPartWindow(Part templatePart)
        {
            InitializeComponent();
            _currentPart = templatePart;
            DataContext = _currentPart;
            BatchItemsControl.ItemsSource = _batchBuffer;

            if (_currentPart.PartType == PartType.Rectangle) RectangleRadio.IsChecked = true; // По умолчанию прямоугольник

            // Подписка на изменения отверстий
            if (_currentPart.HoleGroups is INotifyCollectionChanged incc)
                incc.CollectionChanged += (s, e) => UpdatePreview();

            UpdatePreview();
        }

        // Простой обработчик — только меняем тип и стандартные размеры
        private void OnShapeTypeChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string shapeType)
            {
                if (shapeType == "Round")
                {
                    _currentPart.PartType = PartType.Round;
                    _currentPart.Title = "Круг";
                }
                else // Rectangle
                {
                    _currentPart.PartType = PartType.Rectangle;
                    _currentPart.Title = "Прямоугольник";
                }

                _currentPart.OnPropertyChanged(nameof(Part.Title));
                _currentPart.HoleGroups.Clear();
                UpdatePreview();
            }
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
            // 1. Валидация имени (не пустое ли)
            if (string.IsNullOrWhiteSpace(_currentPart.Title))
            {
                MessageBox.Show("Укажите название детали", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 2. Проверка на дубликат имени в буфере
            if (_batchBuffer.Any(p => p.Title == _currentPart.Title))
            {
                MessageBox.Show(
                    $"Деталь с названием \"{_currentPart.Title}\" уже существует в списке.\n" +
                    "Удалите существующую деталь или измените название новой.",
                    "Ошибка добавления",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // 3. Валидация количества
            if (_currentPart.Count <= 0)
            {
                MessageBox.Show("Укажите количество деталей", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 4. Валидация отверстий
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
                // Если пользователь согласился продолжить, очищаем отверстия у текущей детали перед клонированием
                _currentPart.HoleGroups.Clear();
            }

            // 5. Клонируем и добавляем
            var clonedPart = ClonePart(_currentPart);
            if (clonedPart != null)
            {
                _batchBuffer.Add(clonedPart);

                _currentPart.HoleGroups.Clear();
                UpdatePreview();
            }
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

            // ✅ СОХРАНЯЕМ ЗНАЧЕНИЕ ЧЕКБОКСА ПЕРЕД ЗАКРЫТИЕМ
            UseAutoNesting = AutoNestingCheck.IsChecked ?? true;

            // 🔥 СЧИТЫВАЕМ И ВАЛИДИРУЕМ ОТСТУП
            // Используем InvariantCulture, чтобы корректно обрабатывать и точку, и запятую
            if (double.TryParse(SpacingInput.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double spacing) && spacing >= 0)
            {
                CustomSpacing = spacing;
            }
            else
            {
                CustomSpacing = 0; // Если поле пустое или введено некорректное значение, сбрасываем на "авто"
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

        private static Part? ClonePart(Part source)
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
                HoleGroups = new ObservableCollection<HoleGroup>(source.HoleGroups),
                DisplayGeometry = PartPreviewGenerator.CloneGeometry(source.DisplayGeometry),
                PropsDict = new Dictionary<int, List<string>>(source.PropsDict)
            };
        }

        private void UpdatePreview()
        {
            _currentPart.DisplayGeometry = null;

            if (_currentPart.HoleGroups.Count > 0)
                PartPreviewGenerator.EnsureDisplayGeometryWithHoles(_currentPart);
            else PartPreviewGenerator.EnsureDisplayGeometry(_currentPart);
        }
    }
}