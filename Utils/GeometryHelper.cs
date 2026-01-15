using System.Windows.Markup;
using System.Windows.Media;

namespace Metal_Code.Utils
{
    public static class GeometryHelper
    {
        public static string? ToXamlString(PathGeometry? geometry)
        {
            if (geometry == null) return null;
            return XamlWriter.Save(geometry);
        }

        public static PathGeometry? FromXamlString(string? xaml)
        {
            if (string.IsNullOrEmpty(xaml)) return null;
            try
            {
                return (PathGeometry)XamlReader.Parse(xaml);
            }
            catch
            {
                return null;
            }
        }
    }
}