using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для SearchWindow.xaml
    /// </summary>
    public partial class SearchWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string search = "";
        public string Search
        {
            get => search;
            set
            {
                if (search != value)
                {
                    search = value;
                    OnPropertyChanged(nameof(Search));
                }
            }
        }

        public SearchWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Get_Offers(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Search))
                MainWindow.M.CreateWorker(GetOffers_WithoutMainBase, MainWindow.ActionState.get);
            else
                MainWindow.M.CreateWorker(Get_Offers, MainWindow.ActionState.get);

            Close();
        }

        /// <summary>
        /// Загрузка отфильтрованных расчётов из основной базы в локальную
        /// </summary>
        private string Get_Offers(string? message = null)
        {
            int count = 0;
            string searchTerm = Search?.Trim() ?? "";

            using var db = new ManagerContext(MainWindow.M.connections[1]);      // Основная база
            using var dbLocal = new ManagerContext(MainWindow.M.connections[0]);  // Локальная база

            try
            {
                // Проверяем подключение
                if (!db.Database.CanConnect())
                    return "Ошибка: Не удалось подключиться к основной базе данных.";

                // Получаем имя целевого менеджера
                var targetManagerName = MainWindow.M.TargetManager?.Name;
                if (string.IsNullOrEmpty(targetManagerName))
                    return "Ошибка: Не выбран менеджер.";

                var manLocal = dbLocal.Managers
                    .Include(m => m.Offers)
                    .FirstOrDefault(m => m.Name == targetManagerName);

                if (manLocal == null)
                    return $"Ошибка: Менеджер \"{targetManagerName}\" не найден в локальной базе.";

                var offers = db.Offers
                    .Include(o => o.Manager)
                    .Where(o => o.Manager != null
                             && o.Manager.Name == targetManagerName
                             && (
                                 (!string.IsNullOrEmpty(o.N) && o.N.Contains(searchTerm))
                                 ||
                                 (!string.IsNullOrEmpty(o.Company) && o.Company.Contains(searchTerm))
                             ))
                    .ToList();

                if (offers.Count == 0)
                    return $"Расчётов по фильтру \"{searchTerm}\" не найдено. Проверьте регистр и правильность ввода.";

                // Копируем расчёты в локальную базу
                foreach (var offer in offers)
                {
                    // Проверяем дубликаты по ключевым полям
                    bool exists = manLocal.Offers.Any(o =>
                        o.N == offer.N &&
                        o.Company == offer.Company &&
                        o.Amount == offer.Amount);

                    if (exists)
                        continue;

                    // Создаём новую сущность для локальной БД (detach от основного контекста)
                    var newOffer = new Offer(offer.N, offer.Company, offer.Amount, offer.Material, offer.Services)
                    {
                        Agent = offer.Agent,
                        Invoice = offer.Invoice,
                        Order = offer.Order,
                        Act = offer.Act,
                        CreatedDate = offer.CreatedDate,
                        EndDate = offer.EndDate,
                        Autor = offer.Autor,
                        Manager = manLocal,
                        Data = offer.Data
                    };

                    manLocal.Offers.Add(newOffer);
                    count++;
                }

                dbLocal.SaveChanges();
                return $"Локальная база обновлена. Добавлено расчётов: {count}.";
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return $"Ошибка параллельного доступа: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Ошибка при загрузке расчётов: {ex.Message}";
            }
        }

        /// <summary>
        /// Загрузка ВСЕХ расчётов из основной базы в локальную
        /// </summary>
        private string GetOffers_WithoutMainBase(string? message = null)
        {
            if (!MainWindow.M.IsLocal)
                return "Загружена основная база расчётов. Обновление не требуется.";

            int count = 0;

            using var db = new ManagerContext(MainWindow.M.connections[1]);      // Основная база
            using var dbLocal = new ManagerContext(MainWindow.M.connections[0]);  // Локальная база

            try
            {
                if (!db.Database.CanConnect())
                    return "Ошибка: Не удалось подключиться к основной базе данных.";

                var targetManagerName = MainWindow.M.TargetManager?.Name;
                if (string.IsNullOrEmpty(targetManagerName))
                    return "Ошибка: Не выбран менеджер.";

                // Получаем менеджера из основной базы
                var manMain = db.Managers
                    .Include(m => m.Offers)
                    .FirstOrDefault(m => m.Name == targetManagerName);

                // Получаем менеджера из локальной базы
                var manLocal = dbLocal.Managers
                    .Include(m => m.Offers)
                    .FirstOrDefault(m => m.Name == targetManagerName);

                if (manLocal == null)
                    return $"Ошибка: Менеджер \"{targetManagerName}\" не найден в локальной базе.";

                if (manMain?.Offers == null || manMain.Offers.Count == 0)
                    return $"У менеджера \"{targetManagerName}\" нет расчётов в основной базе.";

                foreach (var offer in manMain.Offers)
                {
                    // Проверяем дубликаты
                    bool exists = manLocal.Offers.Any(o =>
                        o.N == offer.N &&
                        o.Company == offer.Company &&
                        o.Amount == offer.Amount);

                    if (exists)
                        continue;

                    var newOffer = new Offer(offer.N, offer.Company, offer.Amount, offer.Material, offer.Services)
                    {
                        Agent = offer.Agent,
                        Invoice = offer.Invoice,
                        Order = offer.Order,
                        Act = offer.Act,
                        CreatedDate = offer.CreatedDate,
                        EndDate = offer.EndDate,
                        Autor = offer.Autor,
                        Manager = manLocal,
                        Data = offer.Data
                    };

                    manLocal.Offers.Add(newOffer);
                    count++;
                }

                dbLocal.SaveChanges();
                return $"Локальная база обновлена. Добавлено расчётов: {count}.";
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return $"Ошибка параллельного доступа: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Ошибка при загрузке расчётов: {ex.Message}";
            }
        }
    }
}