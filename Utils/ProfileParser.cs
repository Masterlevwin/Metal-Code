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
        public static void ParseToTechItem(TechItem item)
        {
            var name = item.Profile.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(name)) return;

            // === ТРУБЫ ===
            if (name.Contains("ТРУБА"))
            {
                // Круглая: только маркер "(кр.)"
                if (name.Contains("(КР.)"))
                {
                    var match = Regex.Match(name,
                        @"ТРУБА.*?\(КР\.\).*?(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)",
                        RegexOptions.IgnoreCase);

                    if (match.Success &&
                        double.TryParse(match.Groups[1].Value.Replace(',', '.'), Culture, out double d) &&
                        double.TryParse(match.Groups[2].Value.Replace(',', '.'), Culture, out double thickness))
                    {
                        item.Width = item.Height = d;
                        item.Thickness = thickness;
                        item.Destiny = $"⌀{item.Width}x{item.Thickness}";
                        item.PartType = PartType.RoundTube;
                        return;
                    }
                }

                // Прямоугольная: три размера
                var matchRect = Regex.Match(name,
                    @"ТРУБА.*?(?:\(ПР\.\))?.*?(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)",
                    RegexOptions.IgnoreCase);

                if (matchRect.Success &&
                    double.TryParse(matchRect.Groups[1].Value.Replace(',', '.'), Culture, out double w) &&
                    double.TryParse(matchRect.Groups[2].Value.Replace(',', '.'), Culture, out double h) &&
                    double.TryParse(matchRect.Groups[3].Value.Replace(',', '.'), Culture, out double t))
                {
                    item.Width = w;
                    item.Height = h;
                    item.Thickness = t;
                    item.Destiny = $"{item.Width}x{item.Height}x{item.Thickness}";
                    item.PartType = PartType.RectangularTube;
                    return;
                }

                // Квадратная (эмпирически): два размера → дублируем первый
                var matchSquare = Regex.Match(name,
                    @"ТРУБА.*?(?:\(КВ\.\))?.*?(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)\s*(?:MM)?",
                    RegexOptions.IgnoreCase);

                if (matchSquare.Success &&
                    double.TryParse(matchSquare.Groups[1].Value.Replace(',', '.'), Culture, out double side) &&
                    double.TryParse(matchSquare.Groups[2].Value.Replace(',', '.'), Culture, out double wall))
                {
                    item.Width = item.Height = side;
                    item.Thickness = wall;
                    item.Destiny = $"{item.Width}x{item.Height}x{item.Thickness}";
                    item.PartType = PartType.RectangularTube;
                    return;
                }
            }

            // === ЛИСТ / ПОЛОСА ===
            // Проверяем, что это не профиль с ключевыми словами
            if (!name.Contains("ТРУБА") && !name.Contains("ДВУТАВР") && !name.Contains("УГОЛОК") &&
                !name.Contains("ШВЕЛЛЕР") && !name.Contains("КВАДРАТ"))
            {
                var clean = name.TrimStart('-', ' ');
                var match = Regex.Match(clean,
                    @"^(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)\s*(?:MM)?",
                    RegexOptions.IgnoreCase);

                if (match.Success &&
                    double.TryParse(match.Groups[1].Value.Replace(',', '.'), Culture, out double thick) &&
                    double.TryParse(match.Groups[2].Value.Replace(',', '.'), Culture, out double width))
                {
                    item.Width = width;
                    item.Thickness = thick;
                    item.Destiny = match.Groups[1].Value;
                    item.PartType = PartType.Rectangle;
                    return;
                }
            }

            // === ДВУТАВР ===
            if (name.Contains("ДВУТАВР"))
            {
                // Марка
                var matchMark = Regex.Match(name, @"ДВУТАВР.*?([А-Я0-9]+)",
                    RegexOptions.IgnoreCase);
                if (matchMark.Success)
                {
                    var numMatch = Regex.Match(matchMark.Groups[1].Value, @"^(\d+)");
                    if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out int mark))
                    {
                        item.Width = item.Height = mark * 10; // см → мм
                        item.Destiny = $"I{matchMark.Groups[1].Value}";
                        item.PartType = PartType.IBeam;
                    }
                    return;
                }

                // Размеры как у труб
                var matchDims = Regex.Match(name,
                    @"ДВУТАВР.*?(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)",
                    RegexOptions.IgnoreCase);
                if (matchDims.Success &&
                    double.TryParse(matchDims.Groups[1].Value.Replace(',', '.'), Culture, out double h) &&
                    double.TryParse(matchDims.Groups[2].Value.Replace(',', '.'), Culture, out double b) &&
                    double.TryParse(matchDims.Groups[3].Value.Replace(',', '.'), Culture, out double t))
                {
                    item.Width = h;
                    item.Height = b;
                    item.Thickness = t;
                    item.Destiny = $"I{item.Width}x{item.Height}x{item.Thickness}";
                    item.PartType = PartType.IBeam;
                    return;
                }
            }

            // === УГОЛОК ===
            if (name.Contains("УГОЛОК"))
            {
                // Неравнополочный: три размера
                var matchThree = Regex.Match(name,
                    @"УГОЛОК.*?(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)",
                    RegexOptions.IgnoreCase);

                if (matchThree.Success &&
                    double.TryParse(matchThree.Groups[1].Value.Replace(',', '.'), Culture, out double l1) &&
                    double.TryParse(matchThree.Groups[2].Value.Replace(',', '.'), Culture, out double l2) &&
                    double.TryParse(matchThree.Groups[3].Value.Replace(',', '.'), Culture, out double t))
                {
                    item.Width = l1;
                    item.Height = l2;
                    item.Thickness = t;
                    item.Destiny = $"L{item.Width}x{item.Height}x{item.Thickness}";
                    item.PartType = PartType.Angle;
                    return;
                }

                // Равнополочный: два размера
                var matchTwo = Regex.Match(name,
                    @"УГОЛОК.*?(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)",
                    RegexOptions.IgnoreCase);

                if (matchTwo.Success &&
                    double.TryParse(matchTwo.Groups[1].Value.Replace(',', '.'), Culture, out double leg) &&
                    double.TryParse(matchTwo.Groups[2].Value.Replace(',', '.'), Culture, out double wall))
                {
                    item.Width = item.Height = leg;
                    item.Thickness = wall;
                    item.Destiny = $"L{item.Width}x{item.Thickness}";
                    item.PartType = PartType.Angle;
                    return;
                }
            }

            // === ШВЕЛЛЕР ===
            if (name.Contains("ШВЕЛЛЕР"))
            {
                var matchMark = Regex.Match(name, @"ШВЕЛЛЕР.*?([А-Я0-9]+)",
                    RegexOptions.IgnoreCase);
                if (matchMark.Success)
                {
                    var numMatch = Regex.Match(matchMark.Groups[1].Value, @"^(\d+)");
                    if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out int mark))
                    {
                        item.Width = item.Height = mark * 10; // см → мм
                        item.Destiny = $"U{matchMark.Groups[1].Value}";
                        item.PartType = PartType.Channel;
                    }
                    return;
                }

                var matchDims = Regex.Match(name,
                    @"ШВЕЛЛЕР.*?(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)\s*[xхXХ]\s*(\d+(?:[.,]\d+)?)",
                    RegexOptions.IgnoreCase);
                if (matchDims.Success &&
                    double.TryParse(matchDims.Groups[1].Value.Replace(',', '.'), Culture, out double h) &&
                    double.TryParse(matchDims.Groups[2].Value.Replace(',', '.'), Culture, out double b) &&
                    double.TryParse(matchDims.Groups[3].Value.Replace(',', '.'), Culture, out double t))
                {
                    item.Width = h;
                    item.Height = b;
                    item.Thickness = t;
                    item.Destiny = $"U{item.Width}x{item.Height}x{item.Thickness}";
                    item.PartType = PartType.Channel;
                    return;
                }
            }

            // === КВАДРАТНЫЙ ПРУТОК ===
            if (name.Contains("КВАДРАТ") && !name.Contains("ТРУБА"))
            {
                var match = Regex.Match(name, @"КВАДРАТ.*?(\d+(?:[.,]\d+)?)",
                    RegexOptions.IgnoreCase);
                if (match.Success &&
                    double.TryParse(match.Groups[1].Value.Replace(',', '.'), Culture, out double size))
                {
                    item.Width = item.Height = size;
                    item.Thickness = 0;
                    item.Destiny = $"□{match.Groups[1].Value}";
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
            PartType.Channel when item.Destiny.Contains('П') => "Швеллер П",
            PartType.Channel when item.Destiny.Contains('У') => "Швеллер У",
            PartType.IBeam when item.Destiny.Contains('Б') => "Двутавр парал",
            PartType.IBeam when item.Destiny.Contains('Ш') => "Двутавр широк",
            PartType.IBeam when item.Destiny.Contains('К') => "Двутавр колон",
            PartType.IBeam => "Двутавр",
            PartType.SquareBar => "Квадрат",
            _ => "Лист металла"
        };
    }
}