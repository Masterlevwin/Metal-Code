using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Metal_Code
{
    public static class DeleteHighlight
    {
        public static readonly DependencyProperty TargetElementProperty =
            DependencyProperty.RegisterAttached(
                "TargetElement",
                typeof(FrameworkElement),
                typeof(DeleteHighlight),
                new PropertyMetadata(null, OnTargetElementChanged));

        public static void SetTargetElement(DependencyObject element, FrameworkElement value)
            => element.SetValue(TargetElementProperty, value);

        public static FrameworkElement GetTargetElement(DependencyObject element)
            => (FrameworkElement)element.GetValue(TargetElementProperty);

        private static void OnTargetElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement button)
            {
                if (e.OldValue is FrameworkElement oldTarget)
                {
                    button.MouseEnter -= OnMouseEnter;
                    button.MouseLeave -= OnMouseLeave;
                }

                if (e.NewValue is FrameworkElement newTarget)
                {
                    button.MouseEnter += OnMouseEnter;
                    button.MouseLeave += OnMouseLeave;
                }
            }
        }

        private static void OnMouseEnter(object sender, MouseEventArgs e)
        {
            var target = GetTargetElement((DependencyObject)sender);
            if (target != null)
            {
                // Сохраняем исходные значения (опционально)
                target.SetValue(BorderBrushPropertyKey, target.GetValue(Border.BorderBrushProperty));
                target.SetValue(BorderThicknessPropertyKey, target.GetValue(Border.BorderThicknessProperty));

                target.SetValue(Border.BorderBrushProperty, Brushes.OrangeRed);
            }
        }

        private static void OnMouseLeave(object sender, MouseEventArgs e)
        {
            var target = GetTargetElement((DependencyObject)sender);
            if (target != null)
            {
                // Восстанавливаем исходные значения
                var originalBrush = target.GetValue(BorderBrushPropertyKey);
                var originalThickness = target.GetValue(BorderThicknessPropertyKey);

                target.SetValue(Border.BorderBrushProperty, originalBrush ?? new SolidColorBrush(Color.FromArgb(0xFF, 0xD5, 0xDF, 0xE5)));
                target.SetValue(Border.BorderThicknessProperty, originalThickness ?? new Thickness(1));
            }
        }

        // Ключи для хранения оригинальных значений (чтобы не "забывать" стиль)
        private static readonly DependencyProperty BorderBrushPropertyKey =
            DependencyProperty.RegisterAttached("OriginalBorderBrush", typeof(Brush), typeof(DeleteHighlight), new PropertyMetadata(null));

        private static readonly DependencyProperty BorderThicknessPropertyKey =
            DependencyProperty.RegisterAttached("OriginalBorderThickness", typeof(Thickness), typeof(DeleteHighlight), new PropertyMetadata(null));
    }
}