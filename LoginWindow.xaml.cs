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
            DialogResult = true;
            //string login = LoginText.Text;
            //string password = PasswordText.Password;

            //using ManagerContext db = new();
            //db.Database.EnsureCreated();
            //db.Managers.Load();
            //MainWindow.M.Managers = db.Managers.Local.ToObservableCollection();
            //MainWindow.M.ManagerDrop.ItemsSource = MainWindow.M.Managers.Where(m => !m.IsEngineer);

            //Manager? manager = MainWindow.M.Managers.FirstOrDefault(x => x.Name == login);
            //if (manager != null)
            //{
            //    if (manager.Password == password)
            //    {
            //        //определяем текущего менеджера
            //        MainWindow.M.CurrentManager = manager;

            //        //устанавливаем менеджера по умолчанию
            //        if (MainWindow.M.ManagerDrop.Items.Contains(manager)) MainWindow.M.ManagerDrop.SelectedItem = manager;

            //        DialogResult = true;
            //    }
            //    else MessageBox.Show("Неправильный пароль. Попробуйте еще раз.");
            //}
            //else MessageBox.Show("Пользователь с таким именем не найден.");
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
