using Metal_Code.Models;
using Metal_Code.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        /// <summary>
        /// Менеджер, успешно прошедший авторизацию (для передачи в MainWindow).
        /// </summary>
        public Manager? AuthenticatedManager { get; private set; }

        public LoginWindow() => InitializeComponent();

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginText.Text.Trim();
            string password = PasswordText.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите логин и пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dataService = MainWindow.M.DataService;
            if (dataService == null)
            {
                MessageBox.Show("Сервис данных не инициализирован.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Ищем менеджера по имени (с приоритетом PG)
                Manager? manager = await FindManagerByNameAsync(dataService, login);

                if (manager == null)
                {
                    MessageBox.Show("Пользователь с таким именем не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверяем пароль
                if (manager.Password != password)
                {
                    MessageBox.Show("Неправильный пароль. Попробуйте еще раз.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // ⭐ НЕ обновляем Contact/MachineName — это временная сессия!
                // Просто сохраняем найденного менеджера для передачи в MainWindow
                AuthenticatedManager = manager;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка авторизации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Ищет менеджера по имени с приоритетом PG -> SQLite.
        /// </summary>
        private async System.Threading.Tasks.Task<Manager?> FindManagerByNameAsync(HybridDataService dataService, string name)
        {
            // Сначала ищем в PG (если онлайн)
            if (dataService.IsOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(App.PostgresOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == name);
                    if (pgManager != null) return pgManager;
                }
                catch
                {
                    // Ошибка сети — переходим к локальной базе
                }
            }

            // Фоллбек на локальную базу
            using var localCtx = new ManagerContext(MainWindow.M.connections[0]);
            localCtx.Database.SetCommandTimeout(10);
            return await localCtx.Managers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Name == name);
        }

        private void Exit(object sender, RoutedEventArgs e) => Environment.Exit(0);
    }
}