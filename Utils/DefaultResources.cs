using System;
using System.IO;
using System.Windows;

namespace Metal_Code.Utils
{
    public static class DefaultResources
    {
        // Ленивая загрузка: массив создастся только при первом обращении
        private static readonly Lazy<byte[]> _defaultPartImage = new(LoadDefaultPartImage);

        public static byte[] DefaultPartImage => _defaultPartImage.Value;

        private static byte[] LoadDefaultPartImage()
        {
            // Получаем поток ресурса напрямую (без декодирования в BitmapSource)
            var uri = new Uri("pack://application:,,,/Images/basket2.png");
            var resource = Application.GetResourceStream(uri) ?? throw new FileNotFoundException($"Ресурс не найден: {uri}");
            using var stream = resource.Stream;
            using var memoryStream = new MemoryStream();
            // Просто копируем байты файла, без перекодирования PNG
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
