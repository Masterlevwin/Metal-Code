using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Metal_Code
{
    /// <summary>
    /// Краткая сводка предварительного раскроя (без сохранения самих листов/хлыстов).
    /// </summary>
    public class NestingPreviewResult
    {
        public int StocksCount { get; set; }
        public string Description { get; set; } = "";
        public int UnplacedCount { get; set; } = 0;
    }

    public partial class AutoNestSettingsWindow : Window
    {
        public bool IsConfirmed { get; private set; }

        // Параметры раскладки
        public bool UseAutoNesting { get; private set; } = true;
        public double SheetWidth { get; private set; }
        public double SheetHeight { get; private set; }
        public double Spacing { get; private set; }
        public double PipeLength { get; private set; }
        public double ClampZone { get; private set; }
        public double CutLoss { get; private set; }

        // Результат проверки
        public NestingPreviewResult? PreviewResult { get; private set; }
        /// <summary>true — применять автонестинг; false — оставить текущий (файловый) вариант.</summary>
        public bool UsePreviewResult { get; private set; }

        private readonly int _currentStocksCount;
        private readonly string _currentStocksDescription;
        private readonly Func<AutoNestSettingsWindow, Task<NestingPreviewResult?>> _previewHandler;

        public AutoNestSettingsWindow(
            bool isSheetMode,
            double defaultWidth, double defaultHeight, double defaultSpacing,
            double defaultLength, double defaultClamp, double defaultLoss,
            int currentStocksCount,
            string currentStocksDescription,
            Func<AutoNestSettingsWindow, Task<NestingPreviewResult?>> previewHandler)
        {
            InitializeComponent();

            _currentStocksCount = currentStocksCount;
            _currentStocksDescription = currentStocksDescription;
            _previewHandler = previewHandler;

            if (isSheetMode)
            {
                SheetPanel.Visibility = Visibility.Visible;
                TxtWidth.Text = defaultWidth.ToString("F0");
                TxtHeight.Text = defaultHeight.ToString("F0");
                TxtSpacing.Text = defaultSpacing.ToString("F0");
            }
            else
            {
                PipePanel.Visibility = Visibility.Visible;
                TxtLength.Text = defaultLength.ToString("F0");
                TxtLoss.Text = defaultLoss.ToString("F0");

                if (defaultClamp == 160) Clamp160Radio.IsChecked = true;
                else if (defaultClamp == 0) Clamp0Radio.IsChecked = true;
                else Clamp340Radio.IsChecked = true;
            }

            // Нет текущих элементов — нечего сравнивать, скрываем выбор
            if (_currentStocksCount <= 0)
                ApplyChoicePanel.Visibility = Visibility.Collapsed;
        }

        private bool TryReadSettings()
        {
            try
            {
                if (SheetPanel.Visibility == Visibility.Visible)
                {
                    UseAutoNesting = AutoNestingCheck.IsChecked ?? true;
                    SheetWidth = double.Parse(TxtWidth.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
                    SheetHeight = double.Parse(TxtHeight.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
                    Spacing = double.Parse(TxtSpacing.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
                }
                else
                {
                    PipeLength = double.Parse(TxtLength.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
                    CutLoss = double.Parse(TxtLoss.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
                    ClampZone = Clamp340Radio.IsChecked == true ? 340 :
                                 Clamp160Radio.IsChecked == true ? 160 : 0;
                }
                return true;
            }
            catch
            {
                MessageBox.Show("Пожалуйста, введите корректные числовые значения.",
                    "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private async void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadSettings()) return;

            BtnPreview.IsEnabled = false;
            BtnPreview.Content = "Расчёт...";
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                var outcome = await _previewHandler(this);
                if (outcome == null)
                {
                    MessageBox.Show("Не удалось выполнить раскладку. Проверьте исходные данные." +
                        "\n\nВозможно Вы пытаетесь разложить детали из отчета AJANCAM - эта функция пока не поддерживается!",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                PreviewResult = outcome;
                PreviewResultPanel.Visibility = Visibility.Visible;

                CurrentResultText.Visibility = _currentStocksCount > 0 ? Visibility.Visible : Visibility.Collapsed;
                CurrentResultText.Text = $"Отчёт (текущий): {_currentStocksDescription}";

                string comparison = "";
                if (_currentStocksCount > 0)
                {
                    // 🔥 ГЛАВНОЕ ПРАВИЛО: Если есть неразмещенные детали, это НЕ выгодный вариант
                    if (outcome.UnplacedCount > 0)
                    {
                        comparison = $"  ⚠ ЕСТЬ НЕРАЗМЕЩЕННЫЕ ДЕТАЛИ: {outcome.UnplacedCount} шт.";
                        // Принудительно выбираем "Оставить текущий", так как автонестинг неполный
                        KeepCurrentRadio.IsChecked = true;
                    }
                    else if (outcome.StocksCount < _currentStocksCount)
                    {
                        comparison = "  ✓ выгоднее";
                        UsePreviewRadio.IsChecked = true;
                    }
                    else if (outcome.StocksCount > _currentStocksCount)
                    {
                        comparison = "  ✗ дороже";
                        KeepCurrentRadio.IsChecked = true;
                    }
                    else
                    {
                        comparison = "  = одинаково";
                        KeepCurrentRadio.IsChecked = true; // или UsePreviewRadio, по вашему усмотрению
                    }
                }
                else
                {
                    // Если текущего варианта нет, но автонестинг не справился
                    if (outcome.UnplacedCount > 0)
                    {
                        comparison = $"  ⚠ ЕСТЬ НЕРАЗМЕЩЕННЫЕ ДЕТАЛИ: {outcome.UnplacedCount} шт.";
                    }
                    else
                    {
                        UsePreviewRadio.IsChecked = true;
                    }
                }

                PreviewResultText.Text = $"Автонестинг: {outcome.Description}{comparison}";

                // 🔥 Дополнительно: меняем цвет текста предупреждения на красный, если есть проблемы
                if (outcome.UnplacedCount > 0)
                {
                    PreviewResultText.Foreground = System.Windows.Media.Brushes.Red;
                    PreviewResultText.FontWeight = FontWeights.Bold;
                }
                else
                {
                    PreviewResultText.Foreground = System.Windows.Media.Brushes.Black;
                    PreviewResultText.FontWeight = FontWeights.Normal;
                }

                // Автовыбор лучшего
                if (_currentStocksCount <= 0 || outcome.StocksCount < _currentStocksCount)
                    UsePreviewRadio.IsChecked = true;
                else
                    KeepCurrentRadio.IsChecked = true;
            }
            finally
            {
                BtnPreview.IsEnabled = true;
                BtnPreview.Content = "Проверка";
                Mouse.OverrideCursor = null;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadSettings()) return;

            if (_currentStocksCount > 0)
            {
                if (PreviewResult == null)
                {
                    var answer = MessageBox.Show(
                        "Вы не выполнили проверку. Применить автонестинг без сравнения?\n\n" +
                        "Текущий вариант (из отчёта) будет заменён результатом автонестинга.",
                        "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (answer != MessageBoxResult.Yes) return;
                    UsePreviewResult = true;
                }
                else
                {
                    UsePreviewResult = UsePreviewRadio.IsChecked == true;
                }
            }
            else
            {
                UsePreviewResult = true;
            }

            IsConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }

        // Для русских склонений в описании
        internal static string Pluralize(int n, string one, string few, string many)
        {
            int m10 = n % 10, m100 = n % 100;
            if (m100 >= 11 && m100 <= 19) return many;
            if (m10 == 1) return one;
            if (m10 >= 2 && m10 <= 4) return few;
            return many;
        }
    }
}