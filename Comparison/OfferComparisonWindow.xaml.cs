using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Metal_Code.Comparison
{
    public partial class OfferComparisonWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public ComparisonResult Result { get; }

        public string DeltaDisplay => Result.DeltaTotal == 0
            ? "0 ₽"
            : $"{Result.DeltaTotal:+#,##0;-#,##0;0} ₽ ({Result.DeltaPercent:+0.0;-0.0;0.0}%)";

        public Brush DeltaColor => Math.Abs(Result.DeltaPercent) > 5
            ? Brushes.DarkRed
            : (Math.Abs(Result.DeltaPercent) > 0_01 ? Brushes.DarkOrange : Brushes.SeaGreen);

        public Visibility DiagnosticsVisibility =>
            Result.Diagnostics.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public OfferComparisonWindow(ComparisonResult result)
        {
            InitializeComponent();
            Result = result;
            DataContext = this;
        }
    }
}