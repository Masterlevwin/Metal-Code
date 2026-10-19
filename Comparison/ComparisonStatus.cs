using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Metal_Code.Comparison
{
    public enum ComparisonStatus
    {
        Equal,          // Полное совпадение
        Different,      // Отличается (информационно)
        OnlyInA,        // Есть в A, отсутствует в B
        OnlyInB,        // Есть в B, отсутствует в A
        Warning,        // Значимое расхождение
        Critical        // Критичное расхождение
    }

    public class ComparisonItem
    {
        public string Category { get; set; } = "";
        public string? Property { get; set; }
        public string? ValueA { get; set; }
        public string? ValueB { get; set; }
        public string? Difference { get; set; }
        public string? Note { get; set; }
        public ComparisonStatus Status { get; set; } = ComparisonStatus.Equal;
        public List<ComparisonItem> Children { get; set; } = new();
        public bool IsExpanded { get; set; } = true;

        public string DisplayName => string.IsNullOrEmpty(Property) ? Category : Property!;

        public FontWeight NameWeight => Children.Count == 0 ? FontWeights.Normal : FontWeights.SemiBold;

        public Brush BackgroundBrush => Status switch
        {
            ComparisonStatus.Equal => Brushes.Transparent,
            ComparisonStatus.Different => new SolidColorBrush(Color.FromRgb(255, 250, 205)), // LemonChiffon
            ComparisonStatus.OnlyInA => new SolidColorBrush(Color.FromRgb(220, 235, 255)), // Light blue
            ComparisonStatus.OnlyInB => new SolidColorBrush(Color.FromRgb(225, 245, 225)), // Light green
            ComparisonStatus.Warning => new SolidColorBrush(Color.FromRgb(255, 231, 186)), // Orange-ish
            ComparisonStatus.Critical => new SolidColorBrush(Color.FromRgb(255, 200, 200)), // Light red
            _ => Brushes.Transparent
        };

        public Brush DifferenceBrush => Status switch
        {
            ComparisonStatus.Critical => Brushes.DarkRed,
            ComparisonStatus.Warning => Brushes.DarkOrange,
            ComparisonStatus.OnlyInA => Brushes.SteelBlue,
            ComparisonStatus.OnlyInB => Brushes.SeaGreen,
            ComparisonStatus.Different => Brushes.DarkSlateGray,
            _ => Brushes.Gray
        };

        public bool HasDifference => Status != ComparisonStatus.Equal;
    }

    public class DiagnosticMessage
    {
        public string Text { get; set; } = "";
        public ComparisonStatus Severity { get; set; }
        public Brush SeverityBrush => Severity switch
        {
            ComparisonStatus.Critical => Brushes.DarkRed,
            ComparisonStatus.Warning => Brushes.DarkOrange,
            ComparisonStatus.Different => Brushes.SteelBlue,
            _ => Brushes.Gray
        };
        public string Icon => Severity switch
        {
            ComparisonStatus.Critical => "🔴",
            ComparisonStatus.Warning => "🟠",
            ComparisonStatus.Different => "🔵",
            _ => "⚪"
        };
    }

    public class ComparisonResult
    {
        public string TitleA { get; set; } = "A";
        public string TitleB { get; set; } = "B";
        public decimal AmountA { get; set; }
        public decimal AmountB { get; set; }
        public decimal DeltaTotal => AmountB - AmountA;
        public decimal DeltaPercent => AmountA > 0 ? DeltaTotal / AmountA * 100 : 0;

        public List<ComparisonItem> Items { get; set; } = new();
        public List<DiagnosticMessage> Diagnostics { get; set; } = new();

        public int CriticalCount { get; set; }
        public int WarningCount { get; set; }
        public int DifferenceCount { get; set; }
        public bool HasAnyDifferences => DifferenceCount > 0;

        public string SummaryLine => HasAnyDifferences
            ? $"Найдено расхождений: {DifferenceCount} (критичных: {CriticalCount}, предупреждений: {WarningCount})"
            : "Расчёты идентичны";
    }
}