using Metal_Code.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для ManagerWindow.xaml
    /// </summary>
    public partial class ManagerWindow : Window
    {
        ManagerContext db = new(MainWindow.M.connections[0]);

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

            // устанавливаем данные в качестве контекста
            var managers = db.Managers.Local.ToObservableCollection();

            LaserList.ItemsSource = managers.Where(x => x.IsLaser).ToList();
            AppList.ItemsSource = managers.Where(x => !x.IsLaser).ToList();
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

                RefreshLists();
            }
        }

        // редактирование
        private void Edit_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox list) return;

            Manager? manager = list.SelectedItem as Manager;

            if (manager is null || manager.Password == "uri") return;

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

                    RefreshLists();
                }
            }
        }

        // удаление из списка лазерных операторов
        private void DeleteLaser_Click(object sender, RoutedEventArgs e)
        {
            if (LaserList.SelectedItem is not Manager manager) return;

            if (manager.Id == MainWindow.M.CurrentManager?.Id)
            {
                MessageBox.Show("Нельзя удалить текущего пользователя!\nСначала войдите под другим пользователем.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var response = MessageBox.Show(
                $"Уверены?\nПользователь \"{manager.Name}\" и все его заказчики с расчётами будут удалены из локальной базы!",
                "Удаление пользователя", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (response != MessageBoxResult.Yes) return;

            try
            {
                DeleteManagerCompletely(manager.Id);
                HaveChanged = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // удаление из списка менеджеров
        private void DeleteApp_Click(object sender, RoutedEventArgs e)
        {
            if (AppList.SelectedItem is not Manager manager) return;

            if (manager.Id == MainWindow.M.CurrentManager?.Id)
            {
                MessageBox.Show("Нельзя удалить текущего пользователя!\nСначала войдите под другим пользователем.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var response = MessageBox.Show(
                $"Уверены?\nПользователь \"{manager.Name}\" и все его заказчики с расчётами будут удалены из локальной базы!",
                "Удаление пользователя", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (response != MessageBoxResult.Yes) return;

            try
            {
                DeleteManagerCompletely(manager.Id);
                HaveChanged = true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Ошибка удаления: {ex.Message}");
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Полностью удаляет менеджера и все связанные данные (заказчики, расчёты) через прямой SQL.
        /// </summary>
        private void DeleteManagerCompletely(int managerId)
        {
            // 1. Удаляем все расчёты менеджера
            db.Database.ExecuteSqlRaw(
                "DELETE FROM Offers WHERE ManagerId = {0}", managerId);

            // 2. Удаляем всех заказчиков менеджера
            db.Database.ExecuteSqlRaw(
                "DELETE FROM Customers WHERE ManagerId = {0}", managerId);

            // 3. Удаляем самого менеджера
            db.Database.ExecuteSqlRaw(
                "DELETE FROM Managers WHERE Id = {0}", managerId);

            // 4. Обновляем UI
            RefreshLists();
        }

        /// <summary>
        /// Обновляет списки менеджеров в UI.
        /// </summary>
        private void RefreshLists()
        {
            // Очищаем ChangeTracker от всех отслеживаемых сущностей
            db.ChangeTracker.Clear();

            // Перечитываем коллекцию из базы
            db.Managers.Load();

            // Обновляем ObservableCollection
            var managers = db.Managers.Local.ToObservableCollection();

            LaserList.ItemsSource = managers.Where(x => x.IsLaser).ToList();
            AppList.ItemsSource = managers.Where(x => !x.IsLaser).ToList();
        }

        private bool HaveChanged = false;

        private void FocusMainWindow(object sender, EventArgs e)
        {
            if (HaveChanged)
            {
                System.Windows.Forms.Application.Restart();
                Environment.Exit(0);
            }
            else MainWindow.M.IsEnabled = true;
        }
    }
}