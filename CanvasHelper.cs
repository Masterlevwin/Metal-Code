using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    public static class CanvasHelper
    {
        public static readonly DependencyProperty GeometryDescriptorsProperty =
            DependencyProperty.RegisterAttached(
                "GeometryDescriptors",
                typeof(ObservableCollection<IGeometryDescriptor>),
                typeof(CanvasHelper),
                new PropertyMetadata(null, OnGeometryDescriptorsChanged));

        public static ObservableCollection<IGeometryDescriptor> GetGeometryDescriptors(DependencyObject obj)
        {
            return (ObservableCollection<IGeometryDescriptor>)obj.GetValue(GeometryDescriptorsProperty);
        }

        public static void SetGeometryDescriptors(DependencyObject obj, ObservableCollection<IGeometryDescriptor> value)
        {
            obj.SetValue(GeometryDescriptorsProperty, value);
        }

        private static void OnGeometryDescriptorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Canvas canvas) return;

            canvas.Children.Clear();

            if (e.NewValue is ObservableCollection<IGeometryDescriptor> descriptors)
            {
                foreach (var descriptor in descriptors)
                {
                    descriptor.Draw(canvas);
                }

                // Обновление при изменении коллекции
                descriptors.CollectionChanged += (sender, args) =>
                {
                    canvas.Children.Clear();
                    foreach (var descriptor in descriptors)
                    {
                        descriptor.Draw(canvas);
                    }
                };
            }
        }

    }

    public interface IGeometryDescriptor
    {
        void Draw(Canvas canvas);
    }
}