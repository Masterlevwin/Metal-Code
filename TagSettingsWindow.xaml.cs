using Metal_Code.Models;
using Metal_Code.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Metal_Code
{
    public partial class TagSettingsWindow : Window
    {
        public ObservableCollection<CommentTag> Tags { get; }

        // ⭐ НОВОЕ: отфильтрованное представление для UI
        private ICollectionView? _userTagsView;

        public TagSettingsWindow(ObservableCollection<CommentTag> tags)
        {
            Tags = tags;
            InitializeComponent();
            DataContext = this;

            // ⭐ Создаём представление с фильтром: показываем только незащищённые теги
            _userTagsView = new ListCollectionView(Tags)
            {
                Filter = tag => tag is CommentTag ct && !ct.IsProtected
            };

            TagsList.ItemsSource = _userTagsView;
        }

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            Tags.Add(new CommentTag("Новый тэг", " Текст комментария", createsFolder: false));

            TagsList.ScrollIntoView(Tags[^1]);
            TagsList.UpdateLayout();
            var container = TagsList.ItemContainerGenerator.ContainerFromItem(Tags[^1]) as ListBoxItem;
            container?.Focus();
        }

        private void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CommentTag tag)
            {
                // ⭐ Проверка IsProtected больше не нужна — защищённые теги не показываются в UI
                var result = MessageBox.Show(
                    $"Удалить тэг \"{tag.Name}\"?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    Tags.Remove(tag);
            }
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Сбросить пользовательские тэги к значениям по умолчанию?\n" +
                "Все ваши добавленные тэги будут удалены.",
                "Подтверждение сброса",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Tags.Clear();
                foreach (var tag in TagManager.GetDefaultTags())
                    Tags.Add(tag);
                // ⭐ Фильтр автоматически скроет защищённые теги
            }
        }

        private void DialogOk(object sender, RoutedEventArgs e)
        {
            // ⭐ Валидация только пользовательских тегов (незащищённых)
            if (!ValidateTags()) return;

            // ⭐ Сохраняем ВСЕ теги (включая защищённые)
            TagManager.SaveTags(Tags);

            DialogResult = true;
            Close();
        }

        private bool ValidateTags()
        {
            int index = 0;
            foreach (var tag in Tags)
            {
                if (tag.IsProtected) continue;
                index++;

                if (string.IsNullOrWhiteSpace(tag.Name))
                {
                    MessageBox.Show($"Тэг #{index} не имеет названия!\nЗаполните поле \"Название кнопки\".",
                        "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TagsList.ScrollIntoView(tag);
                    return false;
                }
            }
            return true;
        }
    }
}