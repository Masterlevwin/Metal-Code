using Metal_Code.Models;
using System;
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

            // Создаем зарегистрированного пользователя
            Manager manager = new()
            {
                Name = login,
                Password = password,
                Contact = ContactText.Text.Trim(), // ⭐ НОВОЕ: сохраняем контактные данные
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

        private void Exit(object sender, RoutedEventArgs e) => Environment.Exit(0);
    }
}