using System.Globalization;
using System.Windows;

namespace Metal_Code
{
    public partial class AutoNestSettingsWindow : Window
    {
        public bool IsConfirmed { get; private set; }

        // Свойства для чтения результатов
        public bool UseAutoNesting { get; private set; } = true;
        public double SheetWidth { get; private set; }
        public double SheetHeight { get; private set; }
        public double Spacing { get; private set; }
        public double PipeLength { get; private set; }
        public double ClampZone { get; private set; }
        public double CutLoss { get; private set; }

        public AutoNestSettingsWindow(bool isSheetMode, double defaultWidth, double defaultHeight, double defaultSpacing, double defaultLength, double defaultClamp, double defaultLoss)
        {
            InitializeComponent();

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

                // 🔥 Устанавливаем активную радио-кнопку на основе переданного значения по умолчанию
                if (defaultClamp == 160)
                    Clamp160Radio.IsChecked = true;
                else if (defaultClamp == 0)
                    Clamp0Radio.IsChecked = true;
                else
                    Clamp340Radio.IsChecked = true; // 340 по умолчанию
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
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

                    // 🔥 Считываем значение зажима строго из радио-кнопок
                    ClampZone = Clamp340Radio.IsChecked == true ? 340 :
                                Clamp160Radio.IsChecked == true ? 160 : 0;
                }

                IsConfirmed = true;
                DialogResult = true;
                Close();
            }
            catch
            {
                MessageBox.Show("Пожалуйста, введите корректные числовые значения.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
}