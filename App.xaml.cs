using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static string? StartupFileToOpen { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 🔑 Фиксируем рабочую директорию = папка с EXE
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // ✅ Автоматическая регистрация ассоциации .mcm
            RegisterMcmFileAssociation();

            base.OnStartup(e);

            try
            {
                if (e?.Args != null && e.Args.Length > 0)
                {
                    string arg = e.Args[0];
                    if (!string.IsNullOrEmpty(arg) && File.Exists(arg))
                    {
                        string? ext = Path.GetExtension(arg)?.ToLowerInvariant();
                        if (ext == ".mcm")
                        {
                            StartupFileToOpen = arg;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void RegisterMcmFileAssociation()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string currentVersion = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
                const string progId = "MetalCode.mcmfile";
                const string registryKeyPath = @"Software\Metal-Code";
                const string versionValueName = "McmAssocVersion";

                // Читаем последнюю зарегистрированную версию
                object? lastVersion = Registry.GetValue(
                    $"HKEY_CURRENT_USER\\{registryKeyPath}",
                    versionValueName,
                    null
                );

                // Регистрируем, только если версия изменилась (или никогда не регистрировали)
                if (lastVersion as string != currentVersion)
                {
                    using (var classesKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
                    {
                        // Ассоциируем .mcm → ProgID
                        using (var extKey = classesKey.CreateSubKey(".mcm"))
                        {
                            extKey.SetValue("", progId);
                        }

                        // Описываем ProgID
                        using (var progKey = classesKey.CreateSubKey(progId))
                        {
                            progKey.SetValue("", "Metal-Code Calculation File");

                            // Иконка из EXE
                            using (var iconKey = progKey.CreateSubKey("DefaultIcon"))
                            {
                                iconKey.SetValue("", $"{exePath},0");
                            }

                            // Команда открытия с передачей пути к файлу
                            using (var cmdKey = progKey.CreateSubKey(@"shell\open\command"))
                            {
                                cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
                            }
                        }
                    }

                    // Сохраняем версию, чтобы не регистрировать каждый раз
                    Registry.SetValue(
                        $"HKEY_CURRENT_USER\\{registryKeyPath}",
                        versionValueName,
                        currentVersion
                    );

                    // Уведомляем Windows об изменении ассоциаций
                    SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем работу приложения
                Trace.WriteLine($"[App] Не удалось зарегистрировать .mcm ассоциацию: {ex}");
            }
        }
    }
}