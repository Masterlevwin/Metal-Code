using System.Globalization;
using System.Text.RegularExpressions;

namespace Metal_Code.Utils
{
    public static class ProfileParser
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>
        /// Парсит строку наименования профиля из заявки Excel
        /// </summary>
        public static ProfileInfo Parse(string profileName)
        {
            var result = new ProfileInfo
            {
                OriginalName = profileName?.Trim() ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(result.OriginalName))
                return result;

            var name = result.OriginalName.ToUpperInvariant();

            // === ТРУБЫ ===
            if (name.Contains("ТРУБА"))
            {
                // Круглая труба: только маркер "(кр.)"
                if (name.Contains("(КР.)"))
                {
                    var match = Regex.Match(name,
                        @"ТРУБА\s*\(КР\.\)\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)",
                        RegexOptions.IgnoreCase);

                    if (match.Success &&
                        double.TryParse(match.Groups[1].Value.Replace(',', '.'), Culture, out double diameter) &&
                        double.TryParse(match.Groups[2].Value.Replace(',', '.'), Culture, out double thickness))
                    {
                        result.Type = ProfileType.Pipe;
                        result.Width = diameter;
                        result.Height = diameter;  // Для круглой: Width = Height = диаметр
                        result.Thickness = thickness;
                        return result;
                    }
                }

                // Профильная труба (квадратная или прямоугольная)
                // Три размера: прямоугольная "60х40x4"
                var matchRect = Regex.Match(name,
                    @"ТРУБА\s*(?:\(ПР\.\))?\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)",
                    RegexOptions.IgnoreCase);

                if (matchRect.Success &&
                    double.TryParse(matchRect.Groups[1].Value.Replace(',', '.'), Culture, out double w) &&
                    double.TryParse(matchRect.Groups[2].Value.Replace(',', '.'), Culture, out double h) &&
                    double.TryParse(matchRect.Groups[3].Value.Replace(',', '.'), Culture, out double t))
                {
                    result.Type = ProfileType.Pipe;
                    result.Width = w;
                    result.Height = h;
                    result.Thickness = t;
                    return result;
                }

                // Два размера: квадратная "100x4" → 100×100×4
                var matchSquare = Regex.Match(name,
                    @"ТРУБА\s*(?:\(КВ\.\))?\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*(?:MM)?$",
                    RegexOptions.IgnoreCase);

                if (matchSquare.Success &&
                    double.TryParse(matchSquare.Groups[1].Value.Replace(',', '.'), Culture, out double side) &&
                    double.TryParse(matchSquare.Groups[2].Value.Replace(',', '.'), Culture, out double wallThickness))
                {
                    result.Type = ProfileType.Pipe;
                    result.Width = side;
                    result.Height = side;  // Квадратная: Width = Height
                    result.Thickness = wallThickness;
                    return result;
                }
            }

            // === ЛИСТ / ПОЛОСА ===
            // Формат: "- 10x140" или "10x140" (толщина × ширина)
            if (name.StartsWith("-") ||
                (!name.Contains("ТРУБА") && !name.Contains("ДВУТАВР") && !name.Contains("УГОЛОК") && !name.Contains("ШВЕЛЛЕР") && !name.Contains("КВАДРАТ")))
            {
                var cleanName = name.TrimStart('-', ' ');
                var match = Regex.Match(cleanName,
                    @"^(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*(?:MM)?$",
                    RegexOptions.IgnoreCase);

                if (match.Success &&
                    double.TryParse(match.Groups[1].Value.Replace(',', '.'), Culture, out double thickness) &&
                    double.TryParse(match.Groups[2].Value.Replace(',', '.'), Culture, out double width))
                {
                    result.Type = ProfileType.Sheet;
                    result.Thickness = thickness;
                    result.Width = width;
                    return result;
                }
            }

            // === ДВУТАВР ===
            if (name.Contains("ДВУТАВР"))
            {
                var match = Regex.Match(name,
                    @"ДВУТАВР\s+([А-Я0-9]+)",
                    RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    result.Type = ProfileType.IBeam;
                    result.StandardMark = match.Groups[1].Value.Trim();
                    return result;
                }
            }

            // === УГОЛОК ===
            if (name.Contains("УГОЛОК"))
            {
                // Три размера: неравнополочный "125x80x8"
                var matchThree = Regex.Match(name,
                    @"УГОЛОК\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)",
                    RegexOptions.IgnoreCase);

                if (matchThree.Success &&
                    double.TryParse(matchThree.Groups[1].Value.Replace(',', '.'), Culture, out double leg1) &&
                    double.TryParse(matchThree.Groups[2].Value.Replace(',', '.'), Culture, out double leg2) &&
                    double.TryParse(matchThree.Groups[3].Value.Replace(',', '.'), Culture, out double thickness))
                {
                    result.Type = ProfileType.Angle;
                    result.Width = leg1;
                    result.Height = leg2;
                    result.Thickness = thickness;
                    return result;
                }

                // Два размера: равнополочный "100x7" → 100×100×7
                var matchTwo = Regex.Match(name,
                    @"УГОЛОК\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*(?:MM)?$",
                    RegexOptions.IgnoreCase);

                if (matchTwo.Success &&
                    double.TryParse(matchTwo.Groups[1].Value.Replace(',', '.'), Culture, out double leg) &&
                    double.TryParse(matchTwo.Groups[2].Value.Replace(',', '.'), Culture, out double wallThickness))
                {
                    result.Type = ProfileType.Angle;
                    result.Width = leg;
                    result.Height = leg;  // Равнополочный: Width = Height
                    result.Thickness = wallThickness;
                    return result;
                }
            }

            // === ШВЕЛЛЕР ===
            if (name.Contains("ШВЕЛЛЕР"))
            {
                var match = Regex.Match(name,
                    @"ШВЕЛЛЕР\s+([А-Я0-9]+)",
                    RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    result.Type = ProfileType.Channel;
                    result.StandardMark = match.Groups[1].Value.Trim();
                    return result;
                }
            }

            // === КВАДРАТНЫЙ ПРУТОК ===
            if (name.Contains("КВАДРАТ") && !name.Contains("ТРУБА"))
            {
                var match = Regex.Match(name,
                    @"КВАДРАТ\s*(\d+(?:[.,]\d+)?)",
                    RegexOptions.IgnoreCase);

                if (match.Success &&
                    double.TryParse(match.Groups[1].Value.Replace(',', '.'), Culture, out double size))
                {
                    result.Type = ProfileType.SquareBar;
                    result.Width = size;
                    result.Height = size;
                    return result;
                }
            }

            return result;
        }

        /// <summary>
        /// Тип профиля на русском (для UI)
        /// </summary>
        public static string GetLocalizedTypeName(ProfileType type) => type switch
        {
            ProfileType.Pipe => "Труба профильная",
            ProfileType.Sheet => "Лист металла",
            ProfileType.IBeam => "Двутавр",
            ProfileType.Angle => "Уголок",
            ProfileType.Channel => "Швеллер",
            ProfileType.SquareBar => "Квадрат",
            _ => "Не определено"
        };
    }

    public enum ProfileType
    {
        Unknown,
        Pipe,           // Все трубы (квадратные, прямоугольные, круглые)
        Sheet,          // Лист/полоса
        IBeam,          // Двутавр
        Angle,          // Уголок
        Channel,        // Швеллер
        SquareBar       // Квадратный пруток
    }

    public class ProfileInfo
    {
        public ProfileType Type { get; set; } = ProfileType.Unknown;
        public string OriginalName { get; set; } = string.Empty;

        // Универсальные размеры (в мм)
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? Thickness { get; set; }

        // Марка для стандартных профилей (двутавр, швеллер)
        public string? StandardMark { get; set; }

        public bool IsValid => Type != ProfileType.Unknown;

        /// <summary>
        /// Тип профиля на русском (для UI)
        /// </summary>
        public string GetLocalizedTypeName() => Type switch
        {
            ProfileType.Pipe when Width == Height => "Труба квадратная",
            ProfileType.Pipe => "Труба профильная",
            ProfileType.Sheet => "Лист металла",
            ProfileType.IBeam => "Двутавр",
            ProfileType.Angle when Width == Height => "Уголок равнополочный",
            ProfileType.Angle => "Уголок неравнополочный",
            ProfileType.Channel => "Швеллер",
            ProfileType.SquareBar => "Квадрат",
            _ => "Не определено"
        };

        /// <summary>
        /// Единый формат отображения размеров: "Width×Height×Thickness мм"
        /// </summary>
        public string GetSizeDescription()
        {
            if (!IsValid) return string.Empty;

            // Для двутавра и швеллера возвращаем марку
            if (Type is ProfileType.IBeam or ProfileType.Channel && !string.IsNullOrEmpty(StandardMark))
                return StandardMark;

            // Для квадрата (без толщины)
            if (Type == ProfileType.SquareBar && Width.HasValue)
                return $"{Width}×{Width} мм";

            // Для листа (толщина × ширина)
            if (Type == ProfileType.Sheet && Thickness.HasValue && Width.HasValue)
                return $"{Thickness}×{Width} мм";

            // Для всех остальных: Width×Height×Thickness
            if (Width.HasValue && Height.HasValue && Thickness.HasValue)
                return $"{Width}×{Height}×{Thickness} мм";

            // Частичные данные
            if (Width.HasValue && Height.HasValue)
                return $"{Width}×{Height} мм";

            if (Width.HasValue && Thickness.HasValue)
                return $"{Width}×{Thickness} мм";

            return string.Empty;
        }
    }
}