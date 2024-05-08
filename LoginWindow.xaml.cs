using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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
            //DialogResult = true;
            string login = LoginText.Text;
            string password = PasswordText.Password;

            using ManagerContext db = new(MainWindow.M.IsLocal ? MainWindow.M.connections[0] : MainWindow.M.connections[1]);
            db.Managers.Load();
            MainWindow.M.Managers = db.Managers.Local.ToObservableCollection();

            MainWindow.M.ManagerDrop.ItemsSource = MainWindow.M.Managers.Where(m => !m.IsEngineer);     //список ТОЛЬКО менеджеров (для выставления КП)
            MainWindow.M.UserDrop.ItemsSource = MainWindow.M.Managers;                                  //список ВСЕХ пользователей (для отчетов)

            db.Customers.Load();
            MainWindow.M.Customers = db.Customers.Local.ToObservableCollection();

            Manager? manager = MainWindow.M.Managers.FirstOrDefault(x => x.Name == login);
            if (manager != null)
            {
                if (manager.Password == password)
                {
                    //определяем текущего менеджера
                    MainWindow.M.CurrentManager = manager;

                    //устанавливаем менеджера по умолчанию
                    if (MainWindow.M.ManagerDrop.Items.Contains(manager)) MainWindow.M.ManagerDrop.SelectedItem = manager;


                    //if (manager.Name == "Серых Михаил")
                    //{
                    //    db.Offers.Load();
                    //    List<Offer> _offers = new();

                    //    foreach (Offer offer in db.Offers.Local.OrderByDescending(o => o.Id).ToList())
                    //    {
                    //        if (offer.Company != null)
                    //        {
                    //            Offer? _offer = _offers.FirstOrDefault(o => o.Company == offer.Company);
                    //            if (_offer == null)
                    //            {
                    //                _offers.Add(offer);

                    //                Customer customer = new()
                    //                {
                    //                    Name = offer.Company,
                    //                    Agent = offer.Agent,
                    //                    Manager = offer.Manager,
                    //                };

                    //                Customer? _customer = db.Customers.FirstOrDefault(x => x.Name == customer.Name); 
                    //                if (_customer is null) db.Customers.Add(customer);
                    //            }
                    //        }
                    //    }

                    //    db.SaveChanges();
                    //    MainWindow.M.StatusBegin($"{db.Customers.Local.Count}");
                    //}


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
