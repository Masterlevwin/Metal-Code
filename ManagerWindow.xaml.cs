using Microsoft.EntityFrameworkCore;
using System;
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
        ManagerContext db = new(MainWindow.M.IsLocal ? MainWindow.M.connections[0] : MainWindow.M.connections[1]);
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

            LaserList.DataContext = db.Managers.Local.ToObservableCollection().Where(x => x.IsLaser);
            AppList.DataContext = db.Managers.Local.ToObservableCollection().Where(x => !x.IsLaser);

            if (MainWindow.M.Managers.Count > 0 && !MainWindow.M.CurrentManager.IsAdmin) foreach (UIElement element in ButtonsStack.Children)
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

                LaserList.DataContext = db.Managers.Local.ToObservableCollection().Where(x => x.IsLaser);
                AppList.DataContext = db.Managers.Local.ToObservableCollection().Where(x => !x.IsLaser);
            }
        }
        // редактирование
        private void Edit_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox list || !MainWindow.M.CurrentManager.IsAdmin) return;

            // получаем выделенный объект
            Manager? manager = list.SelectedItem as Manager;

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

                    LaserList.DataContext = db.Managers.Local.ToObservableCollection().Where(x => x.IsLaser);
                    AppList.DataContext = db.Managers.Local.ToObservableCollection().Where(x => !x.IsLaser);
                }
            }
        }

        // удаление
        private void DeleteLaser_Click(object sender, RoutedEventArgs e)
        {
            // если ни одного объекта не выделено, выходим
            if (LaserList.SelectedItem is not Manager manager) return;

            // предупреждаем о том, что будут потеряны данные
            MessageBoxResult response = MessageBox.Show("Уверены? Пользователь и все его заказчики с расчетами будут удалены из базы!",
                "Удаление пользователя", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (response == MessageBoxResult.No) return;

            // получаем выделенный объект
            Manager? _manager = db.Managers.Where(x => x.Id == manager.Id).Include(o => o.Customers).Include(o => o.Offers).FirstOrDefault();
            if (_manager != null)
            {
                db.Managers.Remove(_manager);
                db.SaveChanges();
                LaserList.DataContext = db.Managers.Local.ToObservableCollection().Where(x => x.IsLaser);
                HaveChanged = true;
            }
        }

        private void DeleteApp_Click(object sender, RoutedEventArgs e)
        {
            // если ни одного объекта не выделено, выходим
            if (AppList.SelectedItem is not Manager manager) return;

            // предупреждаем о том, что будут потеряны данные
            MessageBoxResult response = MessageBox.Show("Уверены? Пользователь и все его заказчики с расчетами будут удалены из базы!",
                "Удаление пользователя", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (response == MessageBoxResult.No) return;

            // получаем выделенный объект
            Manager? _manager = db.Managers.Where(x => x.Id == manager.Id).Include(o => o.Customers).Include(o => o.Offers).FirstOrDefault();
            if (_manager != null)
            {
                db.Managers.Remove(_manager);
                db.SaveChanges();
                AppList.DataContext = db.Managers.Local.ToObservableCollection().Where(x => !x.IsLaser);
                HaveChanged = true;
            }
        }

        private bool HaveChanged = false;           //было ли удаление менеджера
        private void FocusMainWindow(object sender, EventArgs e)
        {
            if (HaveChanged)                        //если произошло удаление менеджера, перезапускаем программу
            {
                System.Windows.Forms.Application.Restart();
                Environment.Exit(0);
            }
            else MainWindow.M.IsEnabled = true;     //иначе просто активируем главное окно
        }
    }
}