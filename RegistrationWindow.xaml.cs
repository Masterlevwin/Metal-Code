using Metal_Code.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для RegistrationWindow.xaml
    /// </summary>
    public partial class RegistrationWindow : Window
    {
        public RegistrationWindow() => InitializeComponent();

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginText.Text.Trim();
            string password = PasswordText.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введены не все данные. Проверьте логин и пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ⭐ Всегда используем локальную базу (PG контролируется администратором)
            using ManagerContext db = new(MainWindow.M.connections[0]);

            string currentMachine = Environment.MachineName;

            // Проверяем, нет ли уже такого пользователя локально
            bool existsByName = db.Managers.Any(m => m.Name == login);
            bool existsByMachine = db.Managers.Any(m => m.MachineName == currentMachine);

            if (existsByName)
            {
                MessageBox.Show("Пользователь с таким именем уже существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (existsByMachine)
            {
                MessageBox.Show("Этот компьютер уже зарегистрирован под другим пользователем.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ⭐ НОВОЕ: Создаём менеджера по умолчанию, если его нет
            EnsureDefaultManagerExists(db);

            // Создаем зарегистрированного пользователя
            Manager manager = new()
            {
                Name = login,
                Password = password,
                Contact = ContactText.Text.Trim(),
                MachineName = currentMachine,
                IsAdmin = false,
                IsEngineer = IsEngineer.IsChecked == true,
                IsLaser = IsLaser.IsChecked == true
            };

            db.Managers.Add(manager);
            db.SaveChanges();

            // ⭐ НЕ обновляем коллекции в главном окне — это сделает InitializeManagersAsync после закрытия окна
            // ⭐ НЕ добавляем в PG — список менеджеров контролируется администратором вручную

            DialogResult = true;

            MessageBox.Show(
                $"Добро пожаловать, {login}!\n\n" +
                $"Обязательно запомните свой пароль.\n" +
                $"Функция восстановления пароля не предусмотрена!\n\n" +
                $"⚠️ Ваш аккаунт создан локально. Для работы с сервером обратитесь к администратору.",
                "Успешная регистрация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Создаёт менеджера по умолчанию "Сергеев Юрий" (администратор), если его нет в базе.
        /// Это гарантирует, что в дропе менеджеров будет хотя бы один пользователь при первом запуске.
        /// Пароль "uri" — специальный маркер, защищающий от случайного удаления.
        /// </summary>
        private void EnsureDefaultManagerExists(ManagerContext db)
        {
            // ⭐ Проверяем, есть ли уже менеджер по умолчанию (по паролю-маркеру "0000")
            bool defaultManagerExists = db.Managers.Any(m => m.Password == "0000");

            if (!defaultManagerExists)
            {
                Manager defaultManager = new()
                {
                    Name = "Расчетный менеджер",
                    Password = "0000",
                    MachineName = null,
                    IsAdmin = false,
                    IsEngineer = false,
                    IsLaser = true
                };

                db.Managers.Add(defaultManager);
                db.SaveChanges();

                Trace.WriteLine($"✅ Создан менеджер по умолчанию: {defaultManager.Name} (Id={defaultManager.Id})");
            }
            else
            {
                Trace.WriteLine($"ℹ️ Менеджер по умолчанию уже существует");
            }
        }

        private void Exit(object sender, RoutedEventArgs e) => Environment.Exit(0);
    }
}