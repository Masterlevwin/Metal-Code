using Microsoft.EntityFrameworkCore;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для ManagerWindow.xaml
    /// </summary>
    public partial class ManagerWindow : Window
    {
        ManagerContext db = new(MainWindow.M.isLocal ? MainWindow.M.connections[0] : MainWindow.M.connections[1]);
        public ManagerWindow()
        {
            InitializeComponent();
            Loaded += ManagerWindow_Loaded;
        }

        // при загрузке окна
        private void ManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // загружаем данные из БД
            db.Managers.Load();
            // и устанавливаем данные в качестве контекста
            DataContext = db.Managers.Local.ToObservableCollection();

            if (!MainWindow.M.CurrentManager.IsAdmin) foreach (UIElement element in ButtonsStack.Children)
                    if (element is Button) element.IsEnabled = false;
        }

        // добавление
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            ManagerSettings ManagerSettings = new(new());
            if (ManagerSettings.ShowDialog() == true)
            {
                Manager Manager = ManagerSettings.Manager;
                db.Managers.Add(Manager);
                db.SaveChanges();
            }
        }
        // редактирование
        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            // получаем выделенный объект
            Manager? manager = typesList.SelectedItem as Manager;
            // если ни одного объекта не выделено, выходим
            if (manager is null) return;

            ManagerSettings ManagerSettings = new(new()
            {
                Id = manager.Id,
                Name = manager.Name,
                Contact = manager.Contact,
                Password = manager.Password,
                IsAdmin = manager.IsAdmin,
                IsEngineer = manager.IsEngineer,
                IsLaser = manager.IsLaser
            });

            if (ManagerSettings.ShowDialog() == true)
            {
                // получаем измененный объект
                manager = db.Managers.Find(ManagerSettings.Manager.Id);
                if (manager != null)
                {
                    manager.Name = ManagerSettings.Manager.Name;
                    manager.Contact = ManagerSettings.Manager.Contact;
                    manager.Password = ManagerSettings.Manager.Password;
                    manager.IsAdmin = ManagerSettings.Manager.IsAdmin;
                    manager.IsEngineer = ManagerSettings.Manager.IsEngineer;
                    manager.IsLaser = ManagerSettings.Manager.IsLaser;
                    db.SaveChanges();
                    typesList.Items.Refresh();
                }
            }
        }
        // удаление
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            // получаем выделенный объект
            // если ни одного объекта не выделено, выходим
            if (typesList.SelectedItem is not Manager manager) return;
            db.Managers.Remove(manager);
            db.SaveChanges();
        }

        private void FocusMainWindow(object sender, EventArgs e)
        {
            MainWindow.M.IsEnabled = true;
        }
    }
}