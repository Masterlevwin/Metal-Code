using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Metal_Code
{
    public static class NumericInputHelper
    {
        // Числовые типы, для которых нужна замена точки на запятую
        private static readonly Type[] NumericTypes =
        {
        typeof(int), typeof(long), typeof(float), typeof(double),
        typeof(decimal), typeof(short), typeof(byte),
        typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte)
    };

        // Метод для глобальной регистрации
        public static void Register()
        {
            EventManager.RegisterClassHandler(
                typeof(TextBox),
                TextBox.PreviewTextInputEvent,
                new TextCompositionEventHandler(TextBox_PreviewTextInput),
                true);

            EventManager.RegisterClassHandler(
                typeof(TextBox),
                DataObject.PastingEvent,
                new DataObjectPastingEventHandler(TextBox_Pasting),
                true);
        }

        // Метод для точечного подключения
        public static void AttachNumericInput(TextBox textBox)
        {
            textBox.PreviewTextInput += TextBox_PreviewTextInput;
            DataObject.AddPastingHandler(textBox, TextBox_Pasting);
        }

        private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == ".")
            {
                var textBox = (TextBox)sender;

                // Проверяем, привязан ли TextBox к числовому свойству
                if (IsBoundToNumericProperty(textBox))
                {
                    e.Handled = true;
                    int cursor = textBox.CaretIndex;
                    textBox.Text = textBox.Text.Insert(cursor, ",");
                    textBox.CaretIndex = cursor + 1;
                }
                // Если свойство строковое — точка вводится как есть
            }
        }

        private static void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var textBox = (TextBox)sender;

                // Применяем замену только для числовых полей
                if (IsBoundToNumericProperty(textBox))
                {
                    string? text = e.DataObject.GetData(DataFormats.Text) as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        text = text.Replace('.', ',');
                        e.DataObject = new DataObject(DataFormats.Text, text);
                    }
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        /// <summary>
        /// Проверяет, привязан ли TextBox к свойству числового типа
        /// </summary>
        private static bool IsBoundToNumericProperty(TextBox textBox)
        {
            try
            {
                // Получаем привязку для свойства Text
                BindingExpression binding = textBox.GetBindingExpression(TextBox.TextProperty);
                if (binding == null)
                    return false;

                // Получаем информацию о свойстве
                PropertyDescriptor? property = binding.ResolvedSourcePropertyName != null
                    ? TypeDescriptor.GetProperties(binding.ResolvedSource)
                        .Find(binding.ResolvedSourcePropertyName, true)
                    : null;

                if (property != null)
                {
                    Type propertyType = property.PropertyType;

                    // Проверяем, является ли тип числовым
                    return Array.Exists(NumericTypes, t => t == propertyType) ||
                           propertyType == typeof(float?) ||  // Nullable типы
                           propertyType == typeof(double?) ||
                           propertyType == typeof(decimal?) ||
                           propertyType == typeof(int?) ||
                           propertyType == typeof(long?);
                }

                return false;
            }
            catch
            {
                // В случае ошибки считаем, что поле не числовое (безопасное поведение)
                return false;
            }
        }
    }
}