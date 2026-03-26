using System.Globalization;
using System.Text.RegularExpressions;

namespace Metal_Code.Utils
{
    public static class ProfileParser
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>
        /// Парсит профиль и записывает результаты прямо в свойства TechItem
        /// </summary>
        public static void ParseToTechItem(TechItem item, string profileName)
        {
            if (item == null || string.IsNullOrWhiteSpace(profileName)) return;

            var name = profileName.Trim().ToUpperInvariant();

            // Сбрасываем старые значения
            item.Width = item.Height = item.Thickness = 0;

            // === ТРУБЫ ===
            if (name.Contains("ТРУБА"))
            {
                // Круглая: только маркер "(кр.)"
                if (name.Contains("(КР.)"))
                {
                    var match = Regex.Match(name,
                        @"ТРУБА\s*\(КР\.\)\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)");

                    if (match.Success &&
                        double.TryParse(match.Groups[1].Value.Replace(',', '.'), Culture, out double d) &&
                        double.TryParse(match.Groups[2].Value.Replace(',', '.'), Culture, out double thickness))
                    {
                        item.Width = d;
                        item.Height = d;  // круглая: диаметр в оба поля
                        item.Thickness = thickness;
                        item.PartType = PartType.RoundTube;
                        return;
                    }
                }

                // Прямоугольная: три размера
                var matchRect = Regex.Match(name,
                    @"ТРУБА\s*(?:\(ПР\.\))?\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)");

                if (matchRect.Success &&
                    double.TryParse(matchRect.Groups[1].Value.Replace(',', '.'), Culture, out double w) &&
                    double.TryParse(matchRect.Groups[2].Value.Replace(',', '.'), Culture, out double h) &&
                    double.TryParse(matchRect.Groups[3].Value.Replace(',', '.'), Culture, out double t))
                {
                    item.Width = w;
                    item.Height = h;
                    item.Thickness = t;
                    item.PartType = PartType.RectangularTube;
                    return;
                }

                // Квадратная (эмпирически): два размера → дублируем первый
                var matchSquare = Regex.Match(name,
                    @"ТРУБА\s*(?:\(КВ\.\))?\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*(?:MM)?$",
                    RegexOptions.IgnoreCase);

                if (matchSquare.Success &&
                    double.TryParse(matchSquare.Groups[1].Value.Replace(',', '.'), Culture, out double side) &&
                    double.TryParse(matchSquare.Groups[2].Value.Replace(',', '.'), Culture, out double wall))
                {
                    item.Width = side;
                    item.Height = side;  // квадратная: одинаковые стороны
                    item.Thickness = wall;
                    item.PartType = PartType.RectangularTube;
                    return;
                }
            }

            // === ЛИСТ / ПОЛОСА ===
            if (name.StartsWith("-") ||
                (!name.Contains("ТРУБА") && !name.Contains("ДВУТАВР") && !name.Contains("УГОЛОК") &&
                 !name.Contains("ШВЕЛЛЕР") && !name.Contains("КВАДРАТ")))
            {
                var clean = name.TrimStart('-', ' ');
                var match = Regex.Match(clean,
                    @"^(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*(?:MM)?$",
                    RegexOptions.IgnoreCase);

                if (match.Success &&
                    double.TryParse(match.Groups[1].Value.Replace(',', '.'), Culture, out double thick) &&
                    double.TryParse(match.Groups[2].Value.Replace(',', '.'), Culture, out double width))
                {
                    item.Thickness = thick;  // для листа толщина — первое число
                    item.Width = width;      // ширина листа
                    item.Height = width;     // для единообразия
                    item.PartType = PartType.Rectangle;
                    return;
                }
            }

            // === ДВУТАВР ===
            if (name.Contains("ДВУТАВР"))
            {
                // Марка: извлекаем высоту в мм
                var matchMark = Regex.Match(name, @"ДВУТАВР\s+([А-Я0-9]+)");
                if (matchMark.Success)
                {
                    var numMatch = Regex.Match(matchMark.Groups[1].Value, @"^(\d+)");
                    if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out int hCm))
                    {
                        item.Width = hCm * 10;
                        item.Height = item.Width;
                        item.PartType = PartType.IBeam;
                    }
                    return;
                }

                // Размеры как у труб
                var matchDims = Regex.Match(name,
                    @"ДВУТАВР\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)");
                if (matchDims.Success &&
                    double.TryParse(matchDims.Groups[1].Value.Replace(',', '.'), Culture, out double h) &&
                    double.TryParse(matchDims.Groups[2].Value.Replace(',', '.'), Culture, out double b) &&
                    double.TryParse(matchDims.Groups[3].Value.Replace(',', '.'), Culture, out double t))
                {
                    item.Width = h;
                    item.Height = b;
                    item.Thickness = t;
                    item.PartType = PartType.IBeam;
                    return;
                }
            }

            // === УГОЛОК ===
            if (name.Contains("УГОЛОК"))
            {
                // Неравнополочный: три размера
                var matchThree = Regex.Match(name,
                    @"УГОЛОК\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)");
                if (matchThree.Success &&
                    double.TryParse(matchThree.Groups[1].Value.Replace(',', '.'), Culture, out double l1) &&
                    double.TryParse(matchThree.Groups[2].Value.Replace(',', '.'), Culture, out double l2) &&
                    double.TryParse(matchThree.Groups[3].Value.Replace(',', '.'), Culture, out double t))
                {
                    item.Width = l1;
                    item.Height = l2;
                    item.Thickness = t;
                    item.PartType = PartType.Angle;
                    return;
                }

                // Равнополочный: два размера → дублируем полку
                var matchTwo = Regex.Match(name,
                    @"УГОЛОК\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*(?:MM)?$",
                    RegexOptions.IgnoreCase);
                if (matchTwo.Success &&
                    double.TryParse(matchTwo.Groups[1].Value.Replace(',', '.'), Culture, out double leg) &&
                    double.TryParse(matchTwo.Groups[2].Value.Replace(',', '.'), Culture, out double wall))
                {
                    item.Width = leg;
                    item.Height = leg;  // равнополочный
                    item.Thickness = wall;
                    item.PartType = PartType.Angle;
                    return;
                }
            }

            // === ШВЕЛЛЕР ===
            if (name.Contains("ШВЕЛЛЕР"))
            {
                var matchMark = Regex.Match(name, @"ШВЕЛЛЕР\s+([А-Я0-9]+)");
                if (matchMark.Success)
                {
                    var numMatch = Regex.Match(matchMark.Groups[1].Value, @"^(\d+)");
                    if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out int hCm))
                    {
                        item.Width = hCm * 10;
                        item.Height = item.Width;
                        item.PartType = PartType.Channel;
                    }
                    return;
                }

                var matchDims = Regex.Match(name,
                    @"ШВЕЛЛЕР\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)\s*[xх]\s*(\d+(?:[.,]\d+)?)");
                if (matchDims.Success &&
                    double.TryParse(matchDims.Groups[1].Value.Replace(',', '.'), Culture, out double h) &&
                    double.TryParse(matchDims.Groups[2].Value.Replace(',', '.'), Culture, out double b) &&
                    double.TryParse(matchDims.Groups[3].Value.Replace(',', '.'), Culture, out double t))
                {
                    item.Width = h;
                    item.Height = b;
                    item.Thickness = t;
                    item.PartType = PartType.Channel;
                    return;
                }
            }

            // === КВАДРАТНЫЙ ПРУТОК ===
            if (name.Contains("КВАДРАТ") && !name.Contains("ТРУБА"))
            {
                var match = Regex.Match(name, @"КВАДРАТ\s*(\d+(?:[.,]\d+)?)");
                if (match.Success &&
                    double.TryParse(match.Groups[1].Value.Replace(',', '.'), Culture, out double size))
                {
                    item.Width = size;
                    item.Height = size;
                    // Thickness не задаём — у прутка нет стенки
                    item.PartType = PartType.SquareBar;
                    return;
                }
            }
        }


        /// <summary>
        /// Тип профиля на русском (для UI)
        /// </summary>
        public static string GetLocalizedTypeName(TechItem item) => item.PartType switch
        {
            PartType.RectangularTube => "Труба профильная",
            PartType.RoundTube => "Труба круглая",
            PartType.Angle when item.Width == item.Height => "Уголок равнополочный",
            PartType.Angle => "Уголок неравнополочный",
            PartType.Channel when item.Profile.Contains('П') => "Швеллер П",
            PartType.Channel when item.Profile.Contains('У') => "Швеллер У",
            PartType.IBeam when item.Profile.Contains('Б') => "Двутавр парал",
            PartType.IBeam when item.Profile.Contains('Ш') => "Двутавр широк",
            PartType.IBeam when item.Profile.Contains('К') => "Двутавр колон",
            PartType.IBeam => "Двутавр",
            PartType.SquareBar => "Квадрат",
            _ => "Лист металла"
        };
    }
}