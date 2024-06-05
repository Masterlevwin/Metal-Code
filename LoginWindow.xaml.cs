using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginText.Text;
            string password = PasswordText.Password;
            bool isRemember = IsRemember.IsChecked is not null;

            using ManagerContext db = new(MainWindow.M.IsLocal ? MainWindow.M.connections[0] : MainWindow.M.connections[1]);
            db.Managers.Load();
            MainWindow.M.Managers = db.Managers.Local.ToObservableCollection();

            MainWindow.M.ManagerDrop.ItemsSource = MainWindow.M.Managers.Where(m => !m.IsEngineer);     //список ТОЛЬКО менеджеров (для выставления КП)
            MainWindow.M.UserDrop.ItemsSource = MainWindow.M.Managers;                                  //список ВСЕХ пользователей (для отчетов)

            db.Customers.Load();
            MainWindow.M.Customers = db.Customers.Local.ToObservableCollection();

            //авторизация с нового устройства
            Manager? manager = MainWindow.M.Managers.FirstOrDefault(x => x.Name == login);
            if (manager != null)
            {
                if (manager.Password == password)
                {
                    //определяем текущего менеджера
                    MainWindow.M.CurrentManager = manager;
                    MainWindow.M.Login.Header = MainWindow.M.CurrentManager.Name;

                    //устанавливаем менеджера по умолчанию
                    if (MainWindow.M.ManagerDrop.Items.Contains(manager)) MainWindow.M.ManagerDrop.SelectedItem = manager;

                    //если установлен флажок "Запомнить меня"
                    if (isRemember)
                    {
                        Manager? _manager = db.Managers.FirstOrDefault(x => x.Name == login);
                        if (_manager is not null)
                        {
                            _manager.Contact = Environment.MachineName;
                            db.Entry(_manager).Property(o => o.Contact).IsModified = true;
                            db.SaveChanges();
                        }
                    }

                    DialogResult = true;
                }
                else MessageBox.Show("Неправильный пароль. Попробуйте еще раз.");
            }
            else MessageBox.Show("Пользователь с таким именем не найден.");
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
