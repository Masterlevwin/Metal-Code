using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Metal_Code
{
    public static class NumericInputHelper
    {
        // Метод для глобальной регистрации (вызывать один раз при старте)
        public static void Register()
        {
            // Перехват ввода текста (для всех TextBox)
            EventManager.RegisterClassHandler(
                typeof(TextBox),
                TextBox.PreviewTextInputEvent,
                new TextCompositionEventHandler(TextBox_PreviewTextInput),
                true);

            // Перехват вставки из буфера (используем DataObject.PastingEvent)
            EventManager.RegisterClassHandler(
                typeof(TextBox),
                DataObject.PastingEvent,
                new DataObjectPastingEventHandler(TextBox_Pasting),
                true);
        }

        // Метод для точечного подключения к конкретному TextBox
        public static void AttachNumericInput(TextBox textBox)
        {
            textBox.PreviewTextInput += TextBox_PreviewTextInput;
            DataObject.AddPastingHandler(textBox, TextBox_Pasting);
        }

        private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Если ввели точку — заменяем на запятую
            if (e.Text == ".")
            {
                var textBox = (TextBox)sender;
                e.Handled = true; // Блокируем стандартный ввод точки

                int cursor = textBox.CaretIndex;
                // Вставляем запятую вместо точки
                textBox.Text = textBox.Text.Insert(cursor, ",");
                textBox.CaretIndex = cursor + 1;
            }
            // Ввод запятой разрешаем (она валидна для ru-RU)
            // Цифры разрешены по умолчанию
        }

        private static void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string? text = e.DataObject.GetData(DataFormats.Text) as string;
                if (!string.IsNullOrEmpty(text))
                {
                    // При вставке заменяем точки на запятые
                    text = text.Replace('.', ',');

                    // Создаем новый объект данных с исправленным текстом
                    e.DataObject = new DataObject(DataFormats.Text, text);
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
}