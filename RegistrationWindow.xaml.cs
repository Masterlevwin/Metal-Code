using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для RegistrationWindow.xaml
    /// </summary>
    public partial class RegistrationWindow : Window
    {
        public RegistrationWindow()
        {
            InitializeComponent();
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginText.Text;
            string password = PasswordText.Password;

            if (login == "" || password == "")
            {
                MessageBox.Show("Введены не все данные. Проверьте логин и пароль.");
                return;
            }

            using ManagerContext db = new(MainWindow.M.IsLocal ? MainWindow.M.connections[0] : MainWindow.M.connections[1]);

            //добавляем менеджера по умолчанию
            Manager admin = new()
            {
                Name = "Сергеев Юрий",
                Password = "uri",
                IsAdmin = true,
                IsLaser = false,
                IsEngineer = false
            };

            //добавляем заказчика по умолчанию
            Customer customer = new() { Name = "Частное лицо", Agent = true };

            //создаем зарегистрированного пользователя
            Manager manager = new()
            {
                Name = login,
                Password = password,
                Contact = Environment.MachineName,
                IsAdmin = true
            };

            if (IsEngineer.IsChecked == true) manager.IsEngineer = true;
            else manager.IsEngineer = false;

            if (IsLaser.IsChecked == true) manager.IsLaser = true;
            else manager.IsLaser = false;

            if (manager.IsEngineer == true) admin.Customers.Add(customer);
            else manager.Customers.Add(customer);
            
            db.Managers.Add(admin);
            db.Managers.Add(manager);

            db.SaveChanges();

            db.Managers.Load();
            MainWindow.M.Managers = db.Managers.Local.ToObservableCollection();

            MainWindow.M.ManagerDrop.ItemsSource = MainWindow.M.Managers.Where(m => !m.IsEngineer);     //список ТОЛЬКО менеджеров (для выставления КП)
            MainWindow.M.UserDrop.ItemsSource = MainWindow.M.Managers;                                  //список ВСЕХ пользователей (для отчетов)

            db.Customers.Load();
            MainWindow.M.Customers = db.Customers.Local.ToObservableCollection();

            //определяем текущего менеджера
            MainWindow.M.CurrentManager = manager;
            MainWindow.M.Login.Header = MainWindow.M.CurrentManager.Name;

            //создаем защитный файл
            MainWindow.EncryptFile();

            //проверяем защитный файл
            if (!MainWindow.CheckMachine())
            {
                MessageBox.Show($"Данная копия программы защищена. Ее невозможно запустить на этом компьютере!");
                Environment.Exit(0);
            }

            DialogResult = true;

            MessageBox.Show($"Обязательно запомните или запишите свой пароль \"{password}\"\nФункция восстановления пароля не предусмотрена!");
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
