using Metal_Code.Models;
using Metal_Code.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Metal_Code
{
    public partial class AssistantWindow : Window
    {
        private readonly AssistantService _service;
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

        public event Action<string>? ActionRequested;

        public AssistantWindow(AssistantService service)
        {
            InitializeComponent();
            _service = service;
            ChatHistory.ItemsSource = ChatMessages;

            AddMessage("МИША",
                "Здравствуйте! Я помогу вам:\n\n" +
                "• Найти нужную кнопку или раздел\n" +
                "• Узнать цены на резку материалов (из базы)\n" +
                "• Проверить расчёт на ошибки\n\n" +
                "Просто напишите вопрос!",
                isUser: false);
        }

        private void AddMessage(string sender, string text, bool isUser,
                                 List<AssistantAction>? actions = null,
                                 string? iconPath = null)
        {
            var message = ChatMessage.Create(sender, text, isUser, actions, iconPath);
            ChatMessages.Add(message);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ChatScroll.ScrollToEnd();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnAskButtonClick(object sender, RoutedEventArgs e)
        {
            var query = QueryTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            AddMessage("Вы", query, isUser: true);
            QueryTextBox.Clear();

            var response = _service.ProcessQuery(query);

            AddMessage("МИША", response.Text, isUser: false,
                       actions: response.Actions,
                       iconPath: response.IconPath);
        }

        private void OnValidateButtonClick(object sender, RoutedEventArgs e)
        {
            if (Owner is not MainWindow mainWindow)
            {
                AddMessage("МИША", "⚠️ Не удалось получить доступ к текущему расчёту.", isUser: false);
                return;
            }

            // Собираем расширенный снимок из MainWindow
            var snapshot = new CalculationSnapshot
            {
                Result = mainWindow.Result,
                Quantity = mainWindow.Count,
                CustomerName = mainWindow.CustomerDrop?.Text,
                OrderNumber = mainWindow.Order?.Text,
                IsAgent = mainWindow.IsAgent,
                CuttingLengthMeters = GetCuttingLengthFromMainWindow(mainWindow),
                SelectedMaterial = GetSelectedMaterial(mainWindow),
                SelectedThickness = GetSelectedThickness(mainWindow),
                SelectedWorks = GetSelectedWorks(mainWindow)
            };

            var result = _service.ValidateCurrentCalculation(snapshot);

            AddMessage("Вы", "✓ Проверить текущий расчёт", isUser: true);

            if (result.IsValid && !result.Issues.Any())
            {
                AddMessage("МИША",
                    "✅ Расчёт выглядит корректным!\n\n" +
                    "Все обязательные поля заполнены, нулей не обнаружено.\n" +
                    "Материал и толщина найдены в базе.",
                    isUser: false);
            }
            else
            {
                var text = $"⚠️ Найдено проблем: {result.Issues.Count}\n\n";
                foreach (var issue in result.Issues)
                {
                    string icon = issue.Severity switch
                    {
                        ValidationSeverity.Error => "❌",
                        ValidationSeverity.Warning => "⚠️",
                        _ => "ℹ️"
                    };
                    text += $"{icon} {issue.Message}\n   → {issue.Recommendation}\n\n";
                }
                AddMessage("МИША", text, isUser: false);
            }
        }

        private float GetCuttingLengthFromMainWindow(MainWindow mainWindow)
        {
            float total = 0;
            try
            {
                foreach (var detail in mainWindow.DetailControls)
                {
                    foreach (var typeDetail in detail.TypeDetailControls)
                    {
                        foreach (var work in typeDetail.WorkControls)
                        {
                            if (work.workType is ICut cut)
                            {
                                total += work.Result / 1000f;
                            }
                        }
                    }
                }
            }
            catch { }
            return total;
        }

        /// <summary>
        /// Извлекает название металла из первой заготовки (для валидации).
        /// </summary>
        private string? GetSelectedMaterial(MainWindow mainWindow)
        {
            try
            {
                var firstType = mainWindow.DetailControls
                    .FirstOrDefault()?.TypeDetailControls.FirstOrDefault();
                if (firstType?.MetalDrop?.SelectedItem is Metal metal)
                    return metal.Name;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Извлекает толщину из первой заготовки (для валидации).
        /// </summary>
        private float GetSelectedThickness(MainWindow mainWindow)
        {
            try
            {
                var firstType = mainWindow.DetailControls
                    .FirstOrDefault()?.TypeDetailControls.FirstOrDefault();
                return firstType?.S ?? 0;
            }
            catch { }
            return 0;
        }

        private List<string> GetSelectedWorks(MainWindow mainWindow)
        {
            var works = new List<string>();
            try
            {
                foreach (var detail in mainWindow.DetailControls)
                {
                    foreach (var typeDetail in detail.TypeDetailControls)
                    {
                        foreach (var work in typeDetail.WorkControls)
                        {
                            if (work.WorkDrop?.SelectedItem is Metal_Code.Models.Work selectedWork
                                && !string.IsNullOrEmpty(selectedWork.Name))
                            {
                                if (!works.Contains(selectedWork.Name))
                                    works.Add(selectedWork.Name);
                            }
                        }
                    }
                }
            }
            catch { }
            return works;
        }

        private void OnActionButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string commandId)
            {
                ActionRequested?.Invoke(commandId);
            }
        }

        private void QueryTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnAskButtonClick(sender, e);
                e.Handled = true;
            }
        }
    }

    public class ChatMessage : INotifyPropertyChanged
    {
        public string Sender { get; set; } = default!;
        public string Text { get; set; } = default!;
        public List<AssistantAction> Actions { get; set; } = new();

        // ⭐ Визуальные свойства
        public SolidColorBrush Background { get; set; } = default!;
        public SolidColorBrush AccentBrush { get; set; } = default!;   // полоса слева
        public SolidColorBrush SenderBrush { get; set; } = default!;    // цвет имени
        public SolidColorBrush TextBrush { get; set; } = default!;      // цвет текста

        /// <summary>
        /// Фабрика: создаёт сообщение с разным оформлением для пользователя и ассистента.
        /// Ответы ассистента — яркие, запросы пользователя — нейтральные.
        /// </summary>
        public static ChatMessage Create(string sender, string text, bool isUser,
                                          List<AssistantAction>? actions = null,
                                          string? iconPath = null)
        {
            var msg = new ChatMessage
            {
                Sender = isUser ? "👤 Вы" : $"🤖 {sender}",
                Text = text,
                Actions = actions ?? new(),
                IconPath = iconPath
            };

            if (isUser)
            {
                // Запросы пользователя — нейтральные, приглушённые
                msg.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF4)); // светло-серый
                msg.AccentBrush = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)); // серая полоса
                msg.SenderBrush = new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C));
                msg.TextBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x5A, 0x5E));
            }
            else
            {
                // ⭐ Ответы ассистента — яркие, акцентные
                msg.Background = new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD)); // насыщенный голубой
                msg.AccentBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x86, 0xDE)); // синяя полоса
                msg.SenderBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x6F, 0xC0));
                msg.TextBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x33)); // тёмный, контрастный
            }

            return msg;
        }

        private string? _iconPath;
        public string? IconPath
        {
            get => _iconPath;
            set
            {
                _iconPath = value;
                OnPropertyChanged(nameof(IconPath));
                OnPropertyChanged(nameof(HasIcon));
                OnPropertyChanged(nameof(IconSource));
            }
        }

        // ⭐ Кэш, чтобы не пересоздавать BitmapImage при каждом скролле
        private BitmapImage? _cachedIcon;
        private bool _iconResolved;

        public bool HasIcon => IconSource != null;

        public BitmapImage? IconSource
        {
            get
            {
                if (_iconResolved) return _cachedIcon;
                _iconResolved = true;
                _cachedIcon = ResolveIcon(_iconPath);
                return _cachedIcon;
            }
        }

        private static BitmapImage? ResolveIcon(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // Вариант 1: Pack URI (файлы как Resource)
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri("pack://application:,,,/" + path, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { /* пробуем дальше */ }

            // Вариант 2: Файл рядом с EXE
            try
            {
                var fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                if (System.IO.File.Exists(fullPath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { /* игнорируем */ }

            return null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}