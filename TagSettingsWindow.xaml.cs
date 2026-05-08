using Metal_Code.Models;
using Metal_Code.Utils;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    public partial class TagSettingsWindow : Window
    {
        public ObservableCollection<CommentTag> Tags { get; }

        public TagSettingsWindow(ObservableCollection<CommentTag> tags)
        {
            Tags = tags;
            InitializeComponent();
            DataContext = this;
            TagsList.ItemsSource = Tags;
        }

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            Tags.Add(new CommentTag(
                name: $"тэг{Tags.Count + 1}",
                text: " Текст комментария"));

            // Прокрутка к новому элементу
            TagsList.ScrollIntoView(Tags[^1]);
            TagsList.UpdateLayout();
            var container = TagsList.ItemContainerGenerator.ContainerFromItem(Tags[^1]) as ListBoxItem;
            container?.Focus();
        }

        private void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CommentTag tag)
            {
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
                "Сбросить все тэги к значениям по умолчанию?\n" +
                "Все ваши настройки будут потеряны.",
                "Подтверждение сброса",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Tags.Clear();
                foreach (var tag in TagManager.GetDefaultTags())
                    Tags.Add(tag);
            }
        }

        private void DialogOk(object sender, RoutedEventArgs e)
        {
            // Валидация
            if (!ValidateTags()) return;

            // Сохранение
            TagManager.SaveTags(Tags);

            DialogResult = true;
            Close();
        }

        private bool ValidateTags()
        {
            for (int i = 0; i < Tags.Count; i++)
            {
                var tag = Tags[i];
                if (string.IsNullOrWhiteSpace(tag.Name))
                {
                    MessageBox.Show(
                        $"Тэг #{i + 1} не имеет названия!\n" +
                        "Заполните поле \"Название кнопки\".",
                        "Ошибка валидации",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    TagsList.ScrollIntoView(tag);
                    return false;
                }
            }
            return true;
        }
    }
}