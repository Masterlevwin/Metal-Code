using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using HandyControl.Data;
using HtmlAgilityPack;
using Metal_Code.Models;
using Metal_Code.Services;
using Metal_Code.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Drawing;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using Path = System.IO.Path;
using Point = System.Windows.Point;

namespace Metal_Code
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public static MainWindow M = new();

        public readonly string[] connections =
        {
            // [0] managers.db (локальная SQLite — для работы оффлайн)
            "Data Source=managers.db",
            // [1] typedetails.db (локальная SQLite)
            "Data Source=typedetails.db",
            // [2] works.db (локальная SQLite)
            "Data Source=works.db",
            // [3] metals.db (локальная SQLite)
            "Data Source=metals.db",
            // [4] templates.db (локальная SQLite)
            "Data Source=templates.db",
            // [5] Рабочая папка "В работу" (сетевая — для запуска в производство)
            @"Y:\Производство\Laser rezka\В работу",
            // [6] Metal-Code (сетевая — общая папка для проверки обновления программы)
            @"M:\Metal-Code",
        };

        public HybridDataService DataService { get; set; } = null!;
        public ProductViewModel ProductModel { get; set; } = new(new DefaultDialogService(), new JsonFileService(), new Product());
        public RequestControl? RequestControl;

        private Manager _currentManager = new();    //текущий авторизованный менеджер
        public Manager CurrentManager
        {
            get => _currentManager;
            set
            {
                if (_currentManager == value) return;
                _currentManager = value;
                OnPropertyChanged(nameof(CurrentManager));
            }
        }

        public Manager TargetManager = new();       //выбранный менеджер из списка
        public Customer TargetCustomer = new();     //выбранный заказчик из списка

        public List<TechItem> TechItems = new();    //список полученных объектов из строк заявки
        public ObservableCollection<Manager> Managers { get; set; } = new();
        public List<Offer> CurrentOffers { get; set; } = new();
        public ObservableCollection<Offer> ReportOffers { get; set; } = new();
        public ObservableCollection<Customer> Customers { get; set; } = new();
        public List<Customer> CurrentCustomers { get; set; } = new();
        public ObservableCollection<TypeDetail> TypeDetails { get; set; } = new();
        public ObservableCollection<Work> Works { get; set; } = new();
        public ObservableCollection<Metal> Metals { get; set; } = new();

        // Представление для группировки/сортировки
        private ICollectionView OffersView { get; set; } = null!;

        // Состояние режимов
        private string _searchQuery = string.Empty;
        private bool _isProductionMode = false;

        private readonly Dictionary<string, float> TempWorksDict = new();                                       //временный словарь работ

        public readonly List<float> Destinies = new() { .5f, .7f, .8f, 1, 1.2f, 1.5f, 2, 2.5f, 3, 4, 5, 6, 8, 10, 12, 14, 16, 18, 20, 22, 25, 30 };
        public Dictionary<string, Dictionary<float, (float, float, float)>> MetalDict = new();                  //словарь материалов
        public Dictionary<double, float> WideDict = new();                                                      //словарь отверстий
        public Dictionary<Metal, float> MetalRatioDict = new();                                                 //словарь коэффициентов за материал

        //----------Свойства и их основные методы---------//
        #region
        private string version = "2.7.2";
        public string Version
        {
            get => version;
            set => version = value;
        }

        private bool isRequest = false;
        public bool IsRequest
        {
            get => isRequest;
            set
            {
                isRequest = value;
                DetailsBox.Visibility = isRequest ? Visibility.Collapsed : Visibility.Visible;

                OnPropertyChanged(nameof(IsRequest));
            }
        }

        private bool isLoadData = false;
        public bool IsLoadData
        {
            get => isLoadData;
            set
            {
                isLoadData = value;
                OnPropertyChanged(nameof(IsLoadData));
            }
        }

        private Offer? activeOffer;
        public Offer? ActiveOffer
        {
            get => activeOffer;
            set
            {
                activeOffer = value;
                OnPropertyChanged(nameof(ActiveOffer));
            }
        }

        private bool isLaser;
        public bool IsLaser
        {
            get => isLaser;
            set
            {
                isLaser = value;
                OnPropertyChanged(nameof(IsLaser));
            }
        }

        private bool isAgent;
        public bool IsAgent
        {
            get => isAgent;
            set
            {
                isAgent = value;
                if (IsAgent) IPRadioButton.IsChecked = true;
                else OOORadioButton.IsChecked = true;
                OnPropertyChanged(nameof(IsAgent));
            }
        }
        private void IsAgentChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                if (radioButton.Name == "IPRadioButton") IsAgent = true;
                else if (radioButton.Name == "OOORadioButton") IsAgent = false;
            }
        }

        private double ratio;
        public double Ratio
        {
            get => ratio;
            set
            {
                if (ratio != value)
                {
                    ratio = value;
                    if (ratio <= 0) ratio = 1;
                    TotalResult();
                    OnPropertyChanged(nameof(Ratio));
                }
            }
        }
        public void SetRatio(double _ratio)
        {
            Ratio = _ratio;
            TotalResult();
        }

        private double materialFactor = 1.0;
        public double MaterialFactor
        {
            get => materialFactor;
            set
            {
                if (materialFactor != value)
                {
                    materialFactor = value;
                    if (materialFactor <= 0) materialFactor = 1;
                    OnPropertyChanged(nameof(MaterialFactor));
                }
            }
        }

        private double serviceFactor = 1.0;
        public double ServiceFactor
        {
            get => serviceFactor;
            set
            {
                if (serviceFactor != value)
                {
                    serviceFactor = value;
                    if (serviceFactor <= 0) serviceFactor = 1;

                    if (!IsLoadData)
                        SetServiceFactor(serviceFactor);

                    OnPropertyChanged(nameof(ServiceFactor));
                }
            }
        }
        public void SetServiceFactor(double ratio)
        {
            if (ratio <= 0) return;
            foreach (DetailControl det in DetailControls)
                foreach (TypeDetailControl type in det.TypeDetailControls)
                    foreach (WorkControl work in type.WorkControls)
                        work.Ratio = (float)ratio;
        }

        private int count;
        public int Count
        {
            get => count;
            set
            {
                count = value;
                if (count <= 0) count = 1;
                OnPropertyChanged(nameof(Count));
            }
        }
        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int c)) SetCount(c);
        }
        public void SetCount(int _count)
        {
            Count = _count;
            TotalResult();
        }

        private bool? hasDelivery;
        public bool? HasDelivery
        {
            get => hasDelivery;
            set
            {
                hasDelivery = value;
                if (hasDelivery == false)
                {
                    SetDelivery(0);
                    SetDeliveryRatio(1);
                }
                OnPropertyChanged(nameof(HasDelivery));
            }
        }
        private void HasDeliveryChanged(object sender, RoutedEventArgs e)
        {
            if (HasDelivery != false && CustomerDrop.SelectedItem is Customer customer)
            {
                Adress.Text = customer.Address;
                SetDelivery(customer.DeliveryPrice);
            }
        }

        private int delivery;
        public int Delivery
        {
            get => delivery;
            set
            {
                if (value != delivery)
                {
                    delivery = value;
                    OnPropertyChanged(nameof(Delivery));
                }
            }
        }
        private void SetDelivery(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox && int.TryParse(tBox.Text, out int delivery)) SetDelivery(delivery);
        }
        public void SetDelivery(int _delivery)
        {
            Delivery = _delivery;
            TotalResult();
        }

        private int deliveryRatio;
        public int DeliveryRatio
        {
            get => deliveryRatio;
            set
            {
                if (value != deliveryRatio)
                {
                    deliveryRatio = value;
                    OnPropertyChanged(nameof(DeliveryRatio));
                }
            }
        }
        private void SetDeliveryRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox && int.TryParse(tBox.Text, out int ratio)) SetDeliveryRatio(ratio);
        }
        public void SetDeliveryRatio(int _ratio)
        {
            DeliveryRatio = _ratio;
            TotalResult();
        }

        private float construct;
        public float Construct
        {
            get => construct;
            set
            {
                if (value != construct)
                {
                    construct = value;
                    OnPropertyChanged(nameof(Construct));
                }
            }
        }
        public float ConstructResult()
        {
            float result = 0;
            if (CheckConstruct.IsChecked != false)
                // проверяем наличие работы и добавляем её минималку к расчету
                foreach (Work w in Works) if (w.Name == "Конструкторские работы")
                {
                    result += w.Price;
                    break;
                }
            if (float.TryParse(ConstructRatio.Text, out float c)) result *= c;
            return result;
        }

        private bool isExpressOffer;    //свойство, определяющее предварительный расчет
        public bool IsExpressOffer
        {
            get => isExpressOffer;
            set
            {
                isExpressOffer = value;
                OnPropertyChanged(nameof(IsExpressOffer));
                SetExpressOffer();
            }
        }
        public void SetExpressOffer()
        {
            if (IsExpressOffer)
            {
                if (!Comment.Text.Contains("Предварительное КП"))
                    Comment.Text = Comment.Text.Insert(Comment.Text.Length, " Предварительное КП");
                MenuMain.Background = Brushes.Moccasin;
            }
            else
            {
                if (Comment.Text.Contains(" Предварительное КП"))
                    Comment.Text = Comment.Text.Replace(" Предварительное КП", "");
                MenuMain.Background = Brushes.White;
            }
        }

        private bool hasAssembly;       //свойство, определяющее экспресс-изготовление
        public bool HasAssembly
        {
            get => hasAssembly;
            set
            {
                hasAssembly = value;
                OnPropertyChanged(nameof(HasAssembly));

                if (!HasAssembly)
                {
                    ServiceFactor = 1;
                    DateProduction.Text = "";
                }
                else
                {
                    if (ServiceFactor < 2) ServiceFactor = 2;
                    DateProduction.Text = "3";
                }
            }
        }

        private string searchDetails = "";
        public string SearchDetails
        {
            get => searchDetails;
            set
            {
                if (searchDetails == value) return;
                searchDetails = value;
                OnPropertyChanged(nameof(SearchDetails));
            }
        }

        private string? log;
        public string? Log
        {
            get => log;
            set
            {
                log = value;
                OnPropertyChanged(nameof(Log));
            }
        }

        private float bonus = 0;
        public float Bonus
        {
            get => bonus;
            set
            {
                if (value != bonus)
                {
                    bonus = value;
                    OnPropertyChanged(nameof(Bonus));
                }
            }
        }

        private float bonusRatio = 0;
        public float BonusRatio
        {
            get => bonusRatio;
            set
            {
                if (value != bonusRatio)
                {
                    bonusRatio = value;
                    OnPropertyChanged(nameof(BonusRatio));
                }
            }
        }
        private void SetBonusRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox && int.TryParse(tBox.Text, out int ratio)) SetBonusRatio(ratio);
        }
        public void SetBonusRatio(float _ratio)
        {
            BonusRatio = _ratio;
            TotalResult();
        }

        private bool _isOffersGridReadOnly = true;
        public bool IsOffersGridReadOnly
        {
            get => _isOffersGridReadOnly;
            private set
            {
                if (_isOffersGridReadOnly == value) return;
                _isOffersGridReadOnly = value;
                OnPropertyChanged(nameof(IsOffersGridReadOnly));
            }
        }
        private void UpdateOffersGridReadOnlyState()
        {
            if (CurrentManager == null)
            {
                IsOffersGridReadOnly = true;
                return;
            }

            if (CurrentManager.IsEngineer)
            {
                // Инженер не редактирует существующие расчёты
                IsOffersGridReadOnly = true;
            }
            else if (CurrentManager.IsAdmin)
            {
                // Админ может редактировать всё
                IsOffersGridReadOnly = false;
            }
            else
            {
                // Обычный менеджер — только свои расчёты
                IsOffersGridReadOnly = TargetManager?.Id != CurrentManager.Id;
            }

            Trace.WriteLine($"🔒 IsOffersGridReadOnly = {IsOffersGridReadOnly} " +
                            $"(роль: {(CurrentManager.IsEngineer ? "инженер" : CurrentManager.IsAdmin ? "админ" : "менеджер")}, " +
                            $"TargetManager: {TargetManager?.Name})");
        }
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            M = this;

            if (!CheckVersion(out string versionInfo))
                Restart();

            // ⭐ Показываем версию + статус подключения
            Title = $"Metal-Code {versionInfo}";

            DataContext = ProductModel;

            ReportDrop.ItemsSource = Months;
            ReportDrop.SelectedItem = Months[DateTime.Now.Month - 1];
            ReportGrid.ItemsSource = ReportOffers;

            Loaded += LoadDataBases;
        }


        //-------------Основные методы-----------//
        #region
        private async void LoadDataBases(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            StatusBegin("Инициализация приложения...", StatusMessageType.Info);

            try
            {
                DataService = new HybridDataService(App.PostgresOptions, connections);

                bool isOnline = await DataService.InitializeAsync();
                UpdateOnlineStatus();

                // ⭐ Очищаем локальную базу от старых расчетов (всегда, даже оффлайн)
                // Теперь этот метод корректно пропускает расчетного менеджера
                await DataService.CleanupLocalOffersAsync();

                if (!isOnline)
                {
                    StatusBegin("Работа в локальном режиме.", StatusMessageType.Warning);
                }

                // Миграция данных (если есть сеть)
                if (isOnline)
                {
                    StatusBegin("Проверка и синхронизация данных с сервером. Пожалуйста, подождите...", StatusMessageType.Info);
                    try
                    {
                        // 1. СНАЧАЛА синхронизация отложенных расчетов
                        // (теперь пропускает расчетного менеджера)
                        await DataService.SyncPendingOffersAsync();

                        // 2. ПОТОМ миграция
                        await DataService.MigrateUserDataToPgAsync();

                        // 3. Очистка осиротевших локальных расчётов
                        // (уже пропускает расчетного менеджера)
                        await DataService.CleanupOrphanedLocalOffersAsync();

                        StatusBegin("Синхронизация с сервером успешно завершена.", StatusMessageType.Success);
                    }
                    catch (Exception ex)
                    {
                        string fullError = ex.InnerException != null ? $"{ex.Message} | Inner: {ex.InnerException.Message}" : ex.Message;
                        Trace.WriteLine($"Ошибка миграции: {fullError}");
                        StatusBegin("Работа в локальном режиме (ошибка синхронизации).", StatusMessageType.Warning);
                        isOnline = false;
                    }
                }

                // Поиск текущего пользователя в локальной базе
                Manager? currentManager = null;
                using (var tempCtx = new ManagerContext(connections[0]))
                {
                    currentManager = await tempCtx.Managers
                        .FirstOrDefaultAsync(m => m.MachineName == Environment.MachineName || m.Contact == Environment.MachineName);
                }

                // Если пользователя нет — открываем регистрацию
                if (currentManager == null)
                {
                    IsEnabled = true;
                    ShowWindow(new RegistrationWindow());
                    return;
                }

                // Синхронизация менеджеров (если есть сеть)
                if (isOnline)
                {
                    try
                    {
                        bool isEngineerOrAdmin = currentManager.IsEngineer || currentManager.IsAdmin;
                        await DataService.SyncManagersAsync(isEngineerOrAdmin);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Ошибка синхронизации менеджеров: {ex.Message}");
                    }
                }

                // ⭐ ЕДИНЫЙ ВЫЗОВ инициализации менеджеров
                if (!await InitializeManagersAsync())
                {
                    IsEnabled = true;
                    ShowWindow(new RegistrationWindow());
                    return;
                }

                // Загрузка справочников
                Metals = new ObservableCollection<Metal>(await DataService.GetLocalMetalsAsync());
                TypeDetails = new ObservableCollection<TypeDetail>(await DataService.GetLocalTypeDetailsAsync());
                Works = new ObservableCollection<Work>(await DataService.GetLocalWorksAsync());
                InitializeDict();

                // Финальная инициализация
                NewProject();
                OfferToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                var filePath = App.StartupFileToOpen;
                if (!string.IsNullOrEmpty(filePath)) await OpenFileOnStartupAsync(filePath);

                ShowUpdateWindow();
            }
            catch (Exception ex)
            {
                string fullError = ex.InnerException != null ? $"{ex.Message} | Inner: {ex.InnerException.Message}" : ex.Message;
                Trace.WriteLine($"Критическая ошибка запуска: {fullError}");
                MessageBox.Show($"Ошибка инициализации:\n{fullError}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void MainWindow_Activated(object sender, EventArgs e)
        {
            if (CurrentManager == null || CurrentManager.IsEngineer) return;
            if (DataService == null || !DataService.IsOnline) return;
            if (TargetManager == null || TargetManager.Name is null) return;

            // ⭐ Если количество ещё не инициализировано — загружаем из БД
            if (_lastKnownOffersCount < 0)
            {
                await UpdateOffersCountCacheAsync();
                return;
            }

            // Ограничение частоты запросов
            if ((DateTime.UtcNow - _lastOffersCheck).TotalSeconds < OFFERS_CHECK_INTERVAL_SECONDS)
                return;

            _lastOffersCheck = DateTime.UtcNow;

            try
            {
                int currentCount = await DataService.GetTotalOffersCountAsync(
                    TargetManager.Id, TargetManager.Name);

                if (currentCount > _lastKnownOffersCount)
                {
                    int newCount = currentCount - _lastKnownOffersCount;
                    Trace.WriteLine($"🔔 Новые расчёты: было {_lastKnownOffersCount}, стало {currentCount} (+{newCount})");
                    ShowNewOffersNotification(newCount);
                }
                else if (currentCount < _lastKnownOffersCount)
                {
                    Trace.WriteLine($"ℹ️ Расчёты были удалены: было {_lastKnownOffersCount}, стало {currentCount}");
                }

                _lastKnownOffersCount = currentCount;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"⚠️ Ошибка проверки новых расчётов: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновляет счётчик расчётов из базы данных.
        /// Вызывать после LoadManagerDataAsync, когда TargetManager уже установлен.
        /// </summary>
        public async System.Threading.Tasks.Task UpdateOffersCountCacheAsync()
        {
            if (TargetManager == null || TargetManager.Name == null) return;
            if (DataService == null || !DataService.IsOnline) return;

            try
            {
                // ⭐ Получаем РЕАЛЬНОЕ количество из БД, а не из UI-коллекции
                int count = await DataService.GetTotalOffersCountAsync(
                    TargetManager.Id, TargetManager.Name);

                _lastKnownOffersCount = count;
                Trace.WriteLine($"📊 Счётчик расчётов обновлён из БД: {_lastKnownOffersCount}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"⚠️ Ошибка обновления счётчика: {ex.Message}");
            }
        }

        private void ShowNewOffersNotification(int count)
        {
            string message = count == 1
                ? "Появился 1 новый расчёт."
                : $"Появилось {count} новых расчётов.";

            StatusBegin($"{message} Нажмите «Обновить» для просмотра.", StatusMessageType.Info);

            if (UpdateBtn != null)
            {
                UpdateBtn.Background = new SolidColorBrush(Color.FromRgb(255, 200, 100));
            }
        }

        public void ShowUpdateWindow()          // метод добавления и загрузки обновлений
        {
            // Создаём контекст
            using var ctx = new RequestContext(connections[4]);
            ctx.EnsureUpdateTableExists();

            // Гарантируем, что история есть
            ctx.EnsureUpdateHistoryInitialized();

            // Добавить новое обновление (если его ещё нет)
            ctx.AddNewUpdateIfNotExists(
                version: "v2.7.1.5",
                releaseDate: new DateTime(2026, 07, 20),
                description: "Добавлена интерактивность раскладки. Изменен дизайн для этого.",
                screenshotPath: "/Updates/v2.7.1.5_2026-07-20.png"
            );

            // Получаем новые обновления
            var newUpdates = ctx.GetNewStartupUpdates();

            if (newUpdates.Any())
            {
                var updateWindow = new UpdateWindow(newUpdates); // передаём список в окно
                updateWindow.ShowDialog();

                // После закрытия — помечаем как просмотренные
                using var freshCtx = new RequestContext(connections[4]); // или переиспользуйте, если в том же потоке
                freshCtx.MarkStartupUpdatesAsSeen();
            }
        }

        private void OnUpdatesMenuItemClick(object sender, RoutedEventArgs e)
        {
            using var ctx = new RequestContext(connections[4]);

            // Показываем ВСЮ историю (без фильтра IsShownAtStartup)
            var allUpdates = ctx.UpdateItems
                .OrderByDescending(u => u.ReleaseDate)
                .ToList();

            var updateWindow = new UpdateWindow(allUpdates);
            updateWindow.ShowDialog();
        }

        /// <summary>
        /// Открывает файл расчёта по ассоциации.
        /// Если расчёт уже есть в БД — загружает его, иначе создаёт новый.
        /// </summary>
        public async System.Threading.Tasks.Task OpenFileOnStartupAsync(string filePath)
        {
            try
            {
                var product = ProductModel.fileService.Open(filePath);
                if (product == null)
                {
                    MessageBox.Show("Не удалось загрузить файл расчёта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ProductModel.Product = product;
                LoadProduct();

                if (DataService == null || TargetManager == null || TargetManager.Name is null)
                {
                    MessageBox.Show("Сервис данных не инициализирован.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ⭐ Ищем расчёт в БД по пути к файлу (чтобы не создавать дубликаты)
                int? existingOfferId = await DataService.FindOfferIdByActAsync(filePath, TargetManager.Id, TargetManager.Name);

                if (existingOfferId.HasValue)
                {
                    // Расчёт уже есть в БД — загружаем его полные данные
                    StatusBegin("Загрузка расчёта из базы...", StatusMessageType.Info);

                    var fullOffer = await DataService.LoadOfferDataAsync(existingOfferId.Value);
                    if (fullOffer != null)
                    {
                        ActiveOffer = fullOffer;
                        StatusBegin($"Расчёт {fullOffer.N} загружен.", StatusMessageType.Success);
                    }
                }
                else
                {
                    // Расчёта нет в БД — создаём новый
                    StatusBegin("Сохранение расчёта в базу...", StatusMessageType.Info);

                    // Определяем автора
                    string autor;
                    if (ActiveOffer?.Autor == CurrentManager?.Name || ActiveOffer == null)
                        autor = CurrentManager?.Name ?? "";
                    else
                        autor = $"{ActiveOffer?.Autor}\n{CurrentManager?.Name} ({DateTime.Now:dd.MM.yyyy})";

                    // Сериализуем данные расчёта
                    string? dataJson = SaveOfferData();

                    // Сохраняем через сервис
                    var savedOffer = await DataService.SaveOfferAsync(
                        orderNumber: Order.Text,
                        companyName: CustomerDrop.Text,
                        amount: Result,
                        material: GetMaterial(),
                        services: GetServices(),
                        isAgent: IsAgent,
                        autor: autor,
                        actPath: filePath,
                        dataJson: dataJson,
                        managerId: TargetManager.Id
                    );

                    ActiveOffer = savedOffer;
                    LimitCheck.IsChecked = false;

                    // ⭐ Обновляем представление таблицы расчётов
                    await LoadManagerDataAsync(TargetManager);

                    StatusBegin($"Расчёт {savedOffer.N} сохранён.", StatusMessageType.Success);

                    // Проверка заказчика в базе
                    using var checkCtx = new ManagerContext(connections[0]);
                    var customer = await checkCtx.Customers.FirstOrDefaultAsync(x => x.Name == CustomerDrop.Text);
                    if (customer is null)
                    {
                        Log += $"\nЗаказчик {CustomerDrop.Text} не сохранён в базе. Добавьте его данные, чтобы использовать повторно.\n";
                    }

                    if (!string.IsNullOrEmpty(Log))
                    {
                        MessageBox.Show(Log, "Обратите внимание!", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Log = null;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии файла:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddSpecTemplateColumnsIfMissing(ManagerContext db)
        {
            try
            {
                db.Database.ExecuteSqlRaw(@"
            ALTER TABLE Customers ADD COLUMN ""SpecTemplate_Header"" TEXT 
            DEFAULT 'Приложение № 1 к договору поставки №'");
            }
            catch { } // Игнорируем, если колонка уже есть

            try
            {
                db.Database.ExecuteSqlRaw(@"ALTER TABLE Customers ADD COLUMN ""SpecTemplate_Number"" INTEGER DEFAULT 1");
            }
            catch { }

            try
            {
                db.Database.ExecuteSqlRaw(@"ALTER TABLE Customers ADD COLUMN ""SpecTemplate_Terms"" TEXT DEFAULT '100% предоплата.'");
            }
            catch { }

            try
            {
                db.Database.ExecuteSqlRaw(@"ALTER TABLE Customers ADD COLUMN ""SpecTemplate_Provider"" TEXT DEFAULT 'ООО ЛАЗЕРФЛЕКС'");
            }
            catch { }

            try
            {
                db.Database.ExecuteSqlRaw(@"ALTER TABLE Customers ADD COLUMN ""SpecTemplate_Buyer"" TEXT DEFAULT ''");
            }
            catch { }

            // Заполним старые записи
            try
            {
                db.Database.ExecuteSqlRaw(@"
            UPDATE Customers 
            SET 
                SpecTemplate_Header = 'Приложение № 1 к договору поставки №',
                SpecTemplate_Number = 1,
                SpecTemplate_Terms = '100% предоплата.',
                SpecTemplate_Provider = 'ООО ЛАЗЕРФЛЕКС',
                SpecTemplate_Buyer = ''
            WHERE SpecTemplate_Buyer IS NULL");
            }
            catch { }
        }

        private void InitializeDict()       //метод заполнения словарей значениями
        {
            foreach (Metal metal in Metals)
            {
                if (metal.Name != null && metal.WayPrice != null && metal.PinholePrice != null && metal.MoldPrice != null)
                {
                    Dictionary<float, (float, float, float)> prices = new();

                    string[] _ways = metal.WayPrice.Split('/');
                    string[] _pinholes = metal.PinholePrice.Split('/');
                    string[] _molds = metal.MoldPrice.Split('/');

                    for (int i = 0; i < _ways.Length; i++) prices[Destinies[i]] = (Parser(_ways[i]), Parser(_pinholes[i]), Parser(_molds[i]));

                    MetalDict[metal.Name] = prices;
                }
            }

            for (int i = 0; i < Destinies.Count; i++)
            {
                if (Destinies[i] <= 4) WideDict[Destinies[i]] = 1;
                else WideDict[Destinies[i]] = (float)Math.Round(1 + 0.05f * (Destinies[i] - 1.5f), 1);
            }

            foreach (Metal met in Metals)
            {
                if (met == null || met.Name == null) continue;

                if (met.Name == "09г2с" || met.Name.Contains("амг") || met.Name.Contains("д16")) MetalRatioDict[met] = 1.5f;
                else if (met.Name.Contains("aisi")) MetalRatioDict[met] = 2;
                else MetalRatioDict[met] = 1;
            }
        }

        private void OpenSettings(object sender, RoutedEventArgs e)     // пункт меню настройки баз
        {
            IsEnabled = false;
            if (sender == Settings.Items[0])
            {
                TypeDetailWindow typeDetailWindow = new();
                typeDetailWindow.Show();
            }
            else if (sender == Settings.Items[1])
            {
                WorkWindow workWindow = new();
                workWindow.Show();
            }
            else if (sender == Settings.Items[2])
            {
                ManagerWindow managerWindow = new();
                managerWindow.Show();
            }
            else if (sender == Settings.Items[3])
            {
                MetalWindow metalWindow = new();
                metalWindow.Show();
            }
        }

        /// <summary>
        /// Инициализирует CurrentManager и TargetManager.
        /// Если передан forcedManager — использует его (для временной сессии через LoginWindow).
        /// Иначе ищет пользователя по MachineName.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> InitializeManagersAsync(Manager? forcedManager = null)
        {
            // 1. Определяем текущего пользователя
            Manager? currentManager = forcedManager;

            if (currentManager == null)
            {
                using var tempCtx = new ManagerContext(connections[0]);
                currentManager = await tempCtx.Managers
                    .FirstOrDefaultAsync(m => m.MachineName == Environment.MachineName || m.Contact == Environment.MachineName);
            }

            if (currentManager == null) return false;

            CurrentManager = currentManager;
            Login.Content = CurrentManager.Name;

            // 2. Загружаем список менеджеров
            if (DataService != null)
            {
                var managersList = await DataService.GetLocalManagersAsync();
                Managers = new ObservableCollection<Manager>(managersList);
            }

            // ⭐ ФИЛЬТРАЦИЯ ПО РОЛИ
            List<Manager> managersForDrop;
            if (CurrentManager.IsEngineer || CurrentManager.IsAdmin)
            {
                // Инженер и админ видят всех менеджеров (не инженеров)
                managersForDrop = Managers.Where(m => !m.IsEngineer).ToList();
            }
            else
            {
                // ⭐ Простой менеджер видит СЕБЯ и "Расчетного менеджера"
                managersForDrop = new List<Manager> { CurrentManager };

                var draftManager = Managers.FirstOrDefault(m => m.Name == "Расчетный менеджер");
                if (draftManager != null)
                {
                    managersForDrop.Add(draftManager);
                }
            }

            ManagerDrop.ItemsSource = managersForDrop;

            // ⭐ Если список из одного элемента — блокируем дроп
            ManagerDrop.IsEnabled = managersForDrop.Count > 1;

            // 3. Логика выбора TargetManager
            if (CurrentManager.IsEngineer)
            {
                // Инженер выбирает менеджера через диалог для просмотра его расчетов
                SetManagerWindow setManagerWindow = new();
                if (setManagerWindow.ShowDialog() == true && setManagerWindow.SelectManager != null)
                {
                    var selectedManager = managersForDrop.FirstOrDefault(m => m.Id == setManagerWindow.SelectManager.Id);
                    TargetManager = selectedManager ?? managersForDrop.FirstOrDefault() ?? CurrentManager;
                }
                else
                {
                    TargetManager = managersForDrop.FirstOrDefault() ?? CurrentManager;
                }
            }
            else
            {
                // Обычный менеджер И админ — TargetManager = CurrentManager
                var managerInDrop = managersForDrop.FirstOrDefault(m => m.Id == CurrentManager.Id);
                TargetManager = managerInDrop ?? CurrentManager;
            }

            // 4. Устанавливаем SelectedItem (с отпиской от события)
            ManagerDrop.SelectionChanged -= ManagerChanged;
            ManagerDrop.SelectedItem = TargetManager;
            ManagerDrop.SelectionChanged += ManagerChanged;

            // 5. Настройка UI под роль
            ReportTab.Visibility = BonusStack.Visibility = CurrentManager.IsEngineer ? Visibility.Collapsed : Visibility.Visible;
            LimitCheck.Content = CurrentManager.IsEngineer ? "Минималка" : "Снять ограничения";

            // 6. Загрузка данных выбранного менеджера
            await LoadManagerDataAsync(TargetManager);

            // 7. Обновляем состояние доступа таблицы расчетов
            UpdateOffersGridReadOnlyState();

            return true;
        }

        private async void ShowLoginWindow(object sender, RoutedEventArgs e)
        {
            var response = MessageBox.Show(
                "Сменить текущего пользователя?\n" +
                "Если \"Да\", потребуется авторизация, и текущий расчет будет очищен!\n\n" +
                "⚠️ Это временная сессия. При следующем запуске приложения будет выполнен вход под владельцем этого ПК.",
                "Сменить пользователя",
                MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

            if (response != MessageBoxResult.Yes) return;

            IsEnabled = false;

            var loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() == true && loginWindow.AuthenticatedManager != null)
            {
                // ⭐ Инициализируем сессию с принудительным менеджером (без обновления MachineName)
                await InitializeManagersAsync(loginWindow.AuthenticatedManager);

                IsEnabled = true;
                NewProject();
            }
            else
            {
                IsEnabled = true;
            }
        }

        private async void ShowWindow(Window window)
        {
            IsEnabled = false;

            if (window.ShowDialog() == true)
            {
                // ⭐ ЕДИНЫЙ ВЫЗОВ инициализации менеджеров
                await InitializeManagersAsync();

                IsEnabled = true;
                NewProject();
            }
            else
            {
                IsEnabled = true;
            }
        }

        /// <summary>
        /// Обновляет визуальный индикатор статуса подключения.
        /// </summary>
        private void UpdateOnlineStatus()
        {
            bool isOnline = DataService.IsOnline == true;

            if (OnlineIndicator != null)
            {
                // Зеленый для онлайн, серый для офлайн
                OnlineIndicator.Background = isOnline
                    ? new SolidColorBrush(Colors.LimeGreen)
                    : new SolidColorBrush(Colors.Gray);

                OnlineIndicator.ToolTip = isOnline
                    ? "Подключено к серверу"
                    : "Работа в локальном режиме";
            }

            // Дополнительно: можно изменить цвет текста Login
            if (Login != null)
            {
                Login.Foreground = isOnline
                    ? new SolidColorBrush(Colors.Black)
                    : new SolidColorBrush(Colors.Gray);
            }
        }

        // ⭐ Флаг для предотвращения рекурсии
        private bool _isSyncingManagerOrder = false;

        /// <summary>
        /// При потере фокуса полем Order — определяем менеджера по номеру.
        /// </summary>
        private void Order_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isSyncingManagerOrder) return;

            // ⭐ Работает только для инженеров и админов
            if (CurrentManager == null || (!CurrentManager.IsEngineer && !CurrentManager.IsAdmin))
                return;

            string orderText = Order.Text.Trim();
            if (string.IsNullOrEmpty(orderText)) return;

            _isSyncingManagerOrder = true;
            try
            {
                // ⭐ Извлекаем имя менеджера из номера заказа
                string? managerName = ManagerCodes.ExtractManagerNameFromOrder(orderText);
                if (managerName == null)
                {
                    Trace.WriteLine($"⚠️ Не удалось определить менеджера по номеру: {orderText}");
                    return;
                }

                // ⭐ Получаем список менеджеров из дропа
                var managers = ManagerDrop.ItemsSource as IEnumerable<Manager>;
                if (managers == null) return;

                // ⭐ Ищем менеджера по имени
                var manager = ManagerCodes.FindByName(managerName, managers);

                if (manager != null && ManagerDrop.SelectedItem != manager)
                {
                    ManagerDrop.SelectedItem = manager;
                    Trace.WriteLine($"🔄 Номер {orderText} → менеджер {manager.Name}");
                    StatusBegin($"Автоматически выбран менеджер: {manager.Name}", StatusMessageType.Info);
                }
                else if (manager == null)
                {
                    Trace.WriteLine($"⚠️ Менеджер '{managerName}' не найден в списке");
                    StatusBegin($"Менеджер '{managerName}' не найден в списке", StatusMessageType.Warning);
                }
            }
            finally
            {
                _isSyncingManagerOrder = false;
            }
        }

        /// <summary>
        /// Обработчик выбора менеджера из выпадающего списка.
        /// </summary>
        private async void ManagerChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ManagerDrop.SelectedItem is Manager man && man.Name != null)
            {
                TargetManager = man;

                StatusBegin($"Загрузка расчётов для {man.Name}...", StatusMessageType.Info);

                if (_isLoadingManagerData) return;

                // ⭐ НОВОЕ: Для инженеров и админов — записываем префикс менеджера в Order
                if (CurrentManager != null && (CurrentManager.IsEngineer || CurrentManager.IsAdmin))
                {
                    if (!_isSyncingManagerOrder)
                    {
                        _isSyncingManagerOrder = true;
                        try
                        {
                            string currentOrder = Order.Text.Trim();
                            string prefix = ManagerCodes.GetCode(man);

                            // ⭐ Записываем префикс, если:
                            // - Order пустой
                            // - Или текущий номер не соответствует выбранному менеджеру
                            if (string.IsNullOrEmpty(currentOrder))
                            {
                                Order.Text = prefix;
                                Trace.WriteLine($"🔄 Менеджер {man.Name} → префикс {prefix}");
                            }
                            else
                            {
                                string? currentManagerName = ManagerCodes.ExtractManagerNameFromOrder(currentOrder);
                                if (!string.Equals(currentManagerName, man.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    Order.Text = prefix;
                                    Trace.WriteLine($"🔄 Менеджер {man.Name} → префикс {prefix} (был {currentManagerName ?? "не определён"})");
                                }
                            }
                        }
                        finally
                        {
                            _isSyncingManagerOrder = false;
                        }
                    }
                }

                // Для инженера: синхронизируем заказчиков выбранного менеджера
                if (CurrentManager?.IsEngineer == true)
                {
                    await DataService.SyncCustomersToLocalAsync(man.Name);
                }

                await LoadManagerDataAsync(man);

                // Обновляем состояние доступа таблицы расчетов
                UpdateOffersGridReadOnlyState();

                ManagerChanged();
            }
        }

        /// <summary>
        /// Асинхронно загружает заказчиков и расчеты указанного менеджера из сервиса.
        /// </summary>
        public async System.Threading.Tasks.Task LoadManagerDataAsync(Manager man)
        {
            if (_isLoadingManagerData || man.Name is null) return;
            _isLoadingManagerData = true;

            try
            {
                IsLaser = man.IsLaser;

                // 1. Заказчики
                var customers = await DataService.GetCustomersAsync(man.Id, man.Name);
                var selectedCustomerName = (CustomerDrop.SelectedItem as Customer)?.Name;
                Customers.Clear();
                foreach (var c in customers) Customers.Add(c);
                CurrentCustomers = Customers.ToList();
                CustomerDrop.ItemsSource = CurrentCustomers;
                if (!string.IsNullOrEmpty(selectedCustomerName))
                {
                    var restored = Customers.FirstOrDefault(c => c.Name == selectedCustomerName);
                    if (restored != null) CustomerDrop.SelectedItem = restored;
                }

                // 2. ⭐ Загружаем только ПОСЛЕДНИЕ 50 расчётов
                var offers = await DataService.GetOffersAsync(man.Name, 50);

                CurrentOffers.Clear();
                foreach (var offer in offers) CurrentOffers.Add(offer);

                // 3. Получаем общее количество для статистики
                int totalCount = await DataService.GetTotalOffersCountAsync(man.Id, man.Name);

                // 4. Перестраиваем представление
                InitializeOffersView();

                // ⭐ ВСЕГДА синхронизируем заказчиков из PG в локалку (не только для инженеров)
                await DataService.SyncCustomersToLocalAsync(man.Name);
                var refreshedCustomers = await DataService.GetCustomersAsync(man.Id, man.Name);
                selectedCustomerName = (CustomerDrop.SelectedItem as Customer)?.Name;
                Customers.Clear();
                foreach (var c in refreshedCustomers) Customers.Add(c);
                CurrentCustomers = Customers.ToList();
                CustomerDrop.ItemsSource = CurrentCustomers;
                if (!string.IsNullOrEmpty(selectedCustomerName))
                {
                    var restored = Customers.FirstOrDefault(c => c.Name == selectedCustomerName);
                    if (restored != null) CustomerDrop.SelectedItem = restored;
                }

                await UpdateOffersCountCacheAsync();

                SummaryInfoTextBlock.Text = $"Показано: {CurrentOffers.Count} из {totalCount} расчётов";
                StatusBegin($"📅 Загружено {CurrentOffers.Count} из {totalCount} расчётов для '{man.Name}'");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Ошибка загрузки данных для {man.Name}: {ex.Message}");
            }
            finally
            {
                _isLoadingManagerData = false;
            }
        }

        /// <summary>
        /// Обновляет UI после смены менеджера.
        /// </summary>
        private void ManagerChanged()
        {
            if (TargetManager == null || CurrentManager == null) return;

            _searchQuery = string.Empty;
            _isProductionMode = false;
            if (InProductionFilterToggle?.IsChecked == true)
                InProductionFilterToggle.IsChecked = false;

            ApplyCurrentMode();

            if (SummaryInfoTextBlock != null)
                SummaryInfoTextBlock.Text = $"Всего расчётов: {CurrentOffers?.Count ?? 0} шт.";

            // ⭐ ItemsSource уже установлен в конструкторе, только выбираем месяц
            if (TargetManager.Name == CurrentManager.Name)
            {
                var currentMonth = Months[DateTime.Now.Month - 1];

                // ⭐ Временно отключаем обработчик, чтобы избежать рекурсии
                ReportDrop.SelectionChanged -= ReportChanged;
                ReportDrop.SelectedItem = currentMonth;
                ReportDrop.SelectionChanged += ReportChanged;

                // Вызываем ReportChanged вручную
                ReportChanged(currentMonth);
            }
            else
            {
                ReportOffers?.Clear();
                ReportGrid.ItemsSource = null;
            }
        }

        // 🔹 Поле для режима расчета (по умолчанию: false = "Только текущие")
        private bool _forecastMixedMode = false;

        /// <summary>
        /// Срабатывает при включении/выключении тоггла "В производстве"
        /// </summary>
        private void ProductionToggle_StateChanged(object sender, RoutedEventArgs e)
        {
            _isProductionMode = InProductionFilterToggle.IsChecked == true;

            // 🔹 Показываем/скрываем панель прогноза
            // ⭐ Инженер не видит панель прогноза — только менеджер/админ
            bool showForecastPanel = _isProductionMode && CurrentManager != null && !CurrentManager.IsEngineer;
            ForecastPanel.Visibility = showForecastPanel ? Visibility.Visible : Visibility.Collapsed;

            if (_isProductionMode) _searchQuery = string.Empty;
            ApplyCurrentMode();
        }

        private async void ApplyCurrentMode()
        {
            try
            {
                List<Offer>? dataToDisplay = null;

                if (_isProductionMode && TargetManager.Name != null)
                {
                    // ⭐ Режим "В производстве" — загружаем через сервис
                    StatusBegin("Загрузка расчётов в производстве...", StatusMessageType.Info);
                    var productionOffers = await DataService.GetOffersInProductionAsync(
                        TargetManager.Id, TargetManager.Name);

                    // ⭐ Для смешанного режима нужны ещё отгруженные за текущий месяц
                    List<Offer>? shippedThisMonth = null;
                    if (_forecastMixedMode)
                    {
                        var now = DateTime.Now;
                        shippedThisMonth = await DataService.GetShippedOffersAsync(
                            TargetManager.Id, TargetManager.Name, now.Month, now.Year);
                    }

                    dataToDisplay = productionOffers;
                    UpdateProductionSummary(productionOffers);
                    UpdateForecastPanel(productionOffers, shippedThisMonth);

                    StatusBegin($"В производстве: {productionOffers.Count} расчётов", StatusMessageType.Success);
                }
                else if (!string.IsNullOrWhiteSpace(_searchQuery))
                {
                    // Режим поиска — данные уже загружены в Search_Offers
                    dataToDisplay = CurrentOffers.ToList();
                }
                else if (TargetManager.Name != null)
                {
                    // 2. ⭐ Загружаем только ПОСЛЕДНИЕ 50 расчётов
                    var offers = await DataService.GetOffersAsync(TargetManager.Name, 50);

                    CurrentOffers.Clear();
                    foreach (var offer in offers) CurrentOffers.Add(offer);
                }

                // Обновляем CurrentOffers (если режим "в производстве")
                if (_isProductionMode && dataToDisplay != null)
                {
                    CurrentOffers.Clear();
                    foreach (var item in dataToDisplay) CurrentOffers.Add(item);
                }

                Trace.WriteLine($"🔍 ApplyCurrentMode: CurrentOffers содержит {CurrentOffers.Count} элементов");
                OffersView.Refresh();
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка фильтрации: {ex.Message}", StatusMessageType.Error);
                Trace.WriteLine($"❌ Ошибка ApplyCurrentMode: {ex.Message}");
            }
        }

        private bool _isLoadingManagerData = false;

        /// <summary>
        /// Срабатывает при клике на правый тоггл "Текущие / Смешанный"
        /// </summary>
        private void ForecastModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _forecastMixedMode = ForecastModeToggle.IsChecked == true;
            ForecastModeText.Text = _forecastMixedMode ? "🔄 Все" : "📦 Текущие";

            // 🔹 Если панель видна — сразу пересчитываем
            if (InProductionFilterToggle.IsChecked == true)
                ApplyCurrentMode();
        }

        /// <summary>
        /// Обновляет текст сводки (количество и сумма) на основе переданных данных.
        /// Данные должны быть загружены через DataService.GetOffersInProductionAsync.
        /// </summary>
        private IEnumerable<Offer>? UpdateProductionSummary(List<Offer> productionOffers)
        {
            try
            {
                int count = productionOffers.Count;
                decimal totalAmount = (decimal)Math.Ceiling(productionOffers.Sum(o => o.Amount));

                SummaryInfoTextBlock.Text = count > 0
                    ? $"{count} шт на сумму {totalAmount:N0} ₽"
                    : "Нет заказов";

                SummaryInfoTextBlock.Foreground = count > 0 ? Brushes.Black : Brushes.Gray;

                return productionOffers;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Ошибка UpdateProductionSummary: {ex.Message}");
                SummaryInfoTextBlock.Text = "Ошибка расчета";
                SummaryInfoTextBlock.Foreground = Brushes.Gray;
                return null;
            }
        }

        /// <summary>
        /// Пересчитывает и выводит 4 показателя в TextBox на основе переданных данных.
        /// </summary>
        /// <param name="productionOffers">Расчёты "в производстве" (без EndDate, с Order)</param>
        /// <param name="shippedThisMonth">Отгруженные расчёты за текущий месяц (нужны для смешанного режима)</param>
        private void UpdateForecastPanel(List<Offer> productionOffers, List<Offer>? shippedThisMonth = null)
        {
            if (ForecastPanel.Visibility != Visibility.Visible) return;

            try
            {
                if (!productionOffers.Any() && (shippedThisMonth == null || !shippedThisMonth.Any()) && !_forecastMixedMode)
                {
                    ForecastPlan.Text = ForecastBonusOoo.Text = ForecastBonusIp.Text = ForecastSalary.Text = "0";
                    return;
                }

                // 🔹 Выбираем данные в зависимости от режима
                List<Offer> offersForCalc;
                if (_forecastMixedMode)
                {
                    // Смешанный режим: "в производстве" + отгруженные за текущий месяц
                    var shipped = shippedThisMonth ?? new List<Offer>();
                    offersForCalc = productionOffers.Concat(shipped).ToList();
                }
                else
                {
                    // Обычный режим: только "в производстве"
                    offersForCalc = productionOffers;
                }

                ReportOffers.Clear();
                foreach (var item in offersForCalc) ReportOffers.Add(item);

                var result = BuildReport(ReportOffers);

                // 🔹 Выводим значения
                ForecastPlan.Text = result.Plan.ToString("N0");
                ForecastBonusOoo.Text = result.BonusOoo.ToString("N0");
                ForecastBonusIp.Text = result.BonusIp.ToString("N0");
                ForecastSalary.Text = result.TotalSalary.ToString("N0");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Ошибка UpdateForecastPanel: {ex.Message}");
                ForecastPlan.Text = ForecastBonusOoo.Text = ForecastBonusIp.Text = ForecastSalary.Text = "—";
            }
        }

        //-------------Настройка блока отчетов-----------//
        readonly string[] Months = { "январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь" };

        // Норма рабочих часов по месяцам
        private readonly int[] WorkingHours = { 136, 152, 168, 168, 144, 152, 168, 168, 168, 168, 160, 168 };

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ⭐ Проверяем, что активной стала именно вкладка отчётов
            if (e.AddedItems.Contains(ReportTab) && ReportTab.IsLoaded)
            {
                RefreshReportIfNeeded();
            }
        }
        private void ReportChanged(object sender, SelectionChangedEventArgs e) => RefreshReportIfNeeded();

        /// <summary>
        /// Пересчитывает отчёт на основе выбранного месяца.
        /// Вызывается из обработчиков событий GotFocus и SelectionChanged.
        /// </summary>
        private void RefreshReportIfNeeded()
        {
            if (TargetManager?.Name != CurrentManager?.Name) return;
            if (ReportDrop?.SelectedItem is not string name) return;

            ReportChanged(name);
        }
        private void ReportChanged(string name)
        {
            int monthIndex = Array.IndexOf(Months, name) + 1;
            if (monthIndex == 0) return; // Месяц не найден

            DateTime now = DateTime.Now;
            int year = now.Month >= monthIndex ? now.Year : now.Year - 1;

            ReportChanged(new DateTime(year, monthIndex, 1));
        }
        private async void ReportChanged(DateTime target)
        {
            try
            {
                if (TargetManager.Name != null)
                {
                    StatusBegin($"Загрузка отчёта за {target:MMMM yyyy}...", StatusMessageType.Info);

                    var shippedOffers = await DataService.GetShippedOffersAsync(
                        TargetManager.Id, TargetManager.Name, target.Month, target.Year);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        ReportOffers.Clear();
                        foreach (var offer in shippedOffers)
                        {
                            ReportOffers.Add(offer);
                        }
                    });
                    await ReportView();

                    StatusBegin($"Отгружено за {target:MMMM yyyy}: {ReportOffers.Count} расчётов", StatusMessageType.Success);
                }
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка загрузки отчёта: {ex.Message}", StatusMessageType.Error);
            }
        }


        //-------------Создание нового проекта-----------//
        public void NewProject()
        {
            ClearDetails();     // удаляем все детали
            ClearCalculate();   // очищаем расчет
            AddDetail();        // добавляем пустой блок детали
        }
        public void ClearDetails()      //удаление всех деталей и очищение текущего расчета
        {
            while (DetailControls.Count > 0) DetailControls[^1].Remove();
            while (BasketControls.Count > 0) BasketControls[^1].Remove();
            AssemblyWindow.A.Assemblies.Clear();
            isAssemblyOffer = false;
            Parts.Clear();
            InvalidatePartsData();
            InitializeFolderPresets();
            OffersTab.Focus();
        }
        public void ClearCalculate()    //сброс расчета к значениям по умолчанию
        {
            SetRatio(1);
            SetBonusRatio(0);
            SetCount(1);
            MaterialFactor = ServiceFactor = 1;
            Construct = 0;
            HasDelivery = false;
            CheckConstruct.IsChecked = false;
            IsExpressOffer = false;
            HasAssembly = false;
            IsLoadData = false;
            Order.Text = CustomerDrop.Text = DateProduction.Text = Adress.Text = Comment.Text = ConstructRatio.Text = TotalCount.Text = TotalPrice.Text = "";
            ActiveOffer = null;
            Log = null;
        }

        //-----------Добавление контрола детали----------//
        public List<DetailControl> DetailControls = new();
        private void AddDetail(object sender, RoutedEventArgs e) { AddDetail(); }
        public void AddDetail()
        {
            DetailControl detail = new(new());

            DetailControls.Add(detail);
            detail.Counter.Text = $"{DetailControls.IndexOf(detail) + 1}";

            DetailsStack.Children.Add(detail);
            DetailsScroll.ScrollToEnd();

            detail.AddTypeDetail();   // при добавлении новой детали добавляем дроп комплектации
        }

        //-----------Добавление контрола покупного изделя----------//
        public List<BasketControl> BasketControls = new();
        private void AddBasket(object sender, RoutedEventArgs e) { AddBasket(new()); }
        private void AddBasket(Part basket)
        {
            BasketControl bc = new(basket);
            BasketControls.Add(bc);

            DetailsStack.Children.Add(bc);
            DetailsScroll.ScrollToEnd();
        }

        //---------Общий результат расчета и его обновление-------//
        private float result;
        public float Result
        {
            get => result;
            set
            {
                result = value;
                OnPropertyChanged(nameof(Result));

                MaterialTotal.Text = $"{Math.Ceiling(GetMetalPrice()):N0} руб.";
                ServicesTotal.Text = $"{Math.Ceiling(GetServicesPrice()):N0} руб.";
                WeldTotal.Text = $"{Math.Ceiling(GetWeldAssembly()):N0} руб.";
                PaintTotal.Text = $"{Math.Ceiling(GetPaintAssembly()):N0} руб.";
                BasketTotal.Text = $"{Math.Ceiling(GetBasketPrice()):N0} руб.";
            }
        }
        public void TotalResult()
        {
            Result = 0;

            foreach (DetailControl d in DetailControls) Result += d.Detail.Total;
            foreach (BasketControl b in BasketControls) Result += b.Basket.Total;

            if (AssemblyWindow.A.Assemblies.Count > 0)
                foreach (var assembly in AssemblyWindow.A.Assemblies)
                    Result += (float)(assembly.WeldPrice + assembly.PaintPrice);

            Result *= Count;

            float result = (float)(Result * Ratio);

            Bonus = result * ((100 + BonusRatio) / 100) - result;

            Result = result + Bonus;

            Result += Delivery * DeliveryRatio;

            if (Result > 0) Parts = PartsSource();
        }

        private void Copy_Result(object sender, RoutedEventArgs e) { Clipboard.SetText($"{(float)Math.Ceiling(Result)}"); }

        //-----------Обновление общей стоимости расчета-----------//
        private void UpdateResult(object sender, MouseEventArgs e) { UpdateResult(); }
        private void UpdateResult(object sender, TextChangedEventArgs e) { UpdateResult(); }
        private void UpdateResult(object sender, RoutedEventArgs e) { UpdateResult(); }
        public void UpdateResult()
        {
            Construct = ConstructResult();

            foreach (DetailControl d in DetailControls)
                foreach (TypeDetailControl t in d.TypeDetailControls) t.PriceChanged();

            if (!HasAssembly)
            {
                var works = DetailControls.SelectMany(x => x.TypeDetailControls).SelectMany(x => x.WorkControls);
                var workSorted = works.GroupBy(x => x.workType?.GetType());
                DateProduction.Text = $"{workSorted.Count() * 5}";
            }
        }

        //-----------Формирование списка нарезанных деталей-------//
        public ObservableCollection<Part> Parts = new();
        public ObservableCollection<Part> LooseParts = new();
        private ObservableCollection<Part> PartsSource()
        {
            ObservableCollection<Part> parts = new();

            for (int i = 0; i < DetailControls.Count; i++)
                for (int j = 0; j < DetailControls[i].TypeDetailControls.Count; j++)
                    for (int k = 0; k < DetailControls[i].TypeDetailControls[j].WorkControls.Count; k++)
                        if (DetailControls[i].TypeDetailControls[j].WorkControls[k].workType is ICut _cut)
                            if (_cut.PartsControl != null && _cut.PartsControl.Parts.Count > 0)
                                foreach (PartControl p in _cut.PartsControl.Parts)
                                {
                                    // Записываем геометрию детали в свойство Part.ImageBytes
                                    WpfImageHelper.GetOrCreatePartImage(p.Part);
                                    parts.Add(p.Part);
                                }
            return parts;
        }

        //-----------Предпросмотр КП-----------------------------//
        private void LoadPartsData_Click(object sender, RoutedEventArgs e)
        {
            PartsGrid.ItemsSource = PartsViewCollection();
            PartsGrid.Visibility = Visibility.Visible;
            PlaceholderPanel.Visibility = Visibility.Collapsed;
        }

        private void InvalidatePartsData()
        {
            if (PartsGrid.Visibility == Visibility.Visible)
            {
                PartsGrid.Visibility = Visibility.Hidden;
                PlaceholderPanel.Visibility = Visibility.Visible;
                TotalCount.Text = TotalPrice.Text = "";
            }
        }

        private void PartsView(object sender, RoutedEventArgs e) { PartsGrid.ItemsSource = PartsViewCollection(); }

        private List<dynamic> PartsViewCollection()
        {
            var items = new List<dynamic>();

            UpdatePricePart();

            if (AssemblyWindow.A.Assemblies.Count > 0)
            {
                foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
                {
                    dynamic item = new ExpandoObject();

                    item.Title = assembly.Title;
                    item.Count = assembly.Count;
                    item.Price = assembly.Price;
                    item.Total = assembly.Total;
                    item.Description = assembly.Description;
                    item.Metal = item.Destiny = item.Accuracy = "";

                    items.Add(item);
                }

                if (LooseParts.Count > 0)
                    foreach (Part part in LooseParts)
                    {
                        dynamic item = new ExpandoObject();

                        item.Title = part.Title;
                        item.Count = part.Count;
                        item.Price = part.Price;
                        item.Total = part.Total;
                        item.Metal = part.Metal;
                        item.Destiny = part.Destiny;
                        item.Description = part.Description;
                        item.Accuracy = part.Accuracy;

                        items.Add(item);
                    }
            }
            else if (Parts.Count > 0)
                foreach (Part part in Parts)
                {
                    dynamic item = new ExpandoObject();

                    item.Title = part.Title;
                    item.Count = part.Count;
                    item.Price = part.Price;
                    item.Total = part.Total;
                    item.Metal = part.Metal;
                    item.Destiny = part.Destiny;
                    item.Description = part.Description;
                    item.Accuracy = part.Accuracy;
                    item.IsHiddenInOffer = part.IsHiddenInOffer;

                    items.Add(item);
                }

            List<Detail> details = DetailControls.Where(d => !d.Detail.IsComplect).Select(d => d.Detail).ToList();
            if (details.Count > 0)
                foreach (Detail detail in details)
                {
                    dynamic item = new ExpandoObject();

                    item.Title = detail.Title;
                    item.Count = detail.Count;
                    item.Price = (float)Math.Ceiling(detail.Price * Ratio * ((100 + BonusRatio) / 100));
                    item.Total = item.Price * detail.Count;
                    item.Metal = detail.Metal;
                    item.Destiny = detail.Destiny;
                    item.Description = detail.Description;
                    item.Accuracy = detail.Accuracy;

                    items.Add(item);
                }

            if (BasketControls.Count > 0)
                foreach (BasketControl basket in BasketControls)
                {
                    var particles = AssemblyWindow.A.Assemblies.SelectMany(p => p.Particles);
                    var collect = particles.Union(LooseParts);

                    var particle = collect.FirstOrDefault(t => t.Title == basket.Basket.Title);
                    if (particle != null) continue;

                    dynamic item = new ExpandoObject();

                    item.Title = basket.Basket.Title;
                    item.Count = basket.Basket.Count;
                    item.Price = basket.Basket.Price;
                    item.Total = item.Price * basket.Basket.Count;
                    item.Metal = basket.Basket.Metal;
                    item.Destiny = basket.Basket.Destiny;
                    item.Description = basket.Basket.Description;
                    item.Accuracy = "";

                    items.Add(item);
                }

            if (CheckConstruct.IsChecked == null)
            {
                dynamic item = new ExpandoObject();

                item.Title = "Конструкторские работы";
                item.Count = Parser(ConstructRatio.Text) > 1 ? (int)Parser(ConstructRatio.Text) : 1;
                item.Price = (float)Math.Ceiling(Construct * Ratio * ((100 + BonusRatio) / 100) / item.Count);
                item.Total = item.Price * item.Count;
                item.Metal = item.Destiny = item.Description = item.Accuracy = "";

                items.Add(item);
            }

            if (HasDelivery is true)
            {
                dynamic item = new ExpandoObject();

                item.Title = "Доставка";
                item.Count = DeliveryRatio;
                item.Price = (float)(Delivery * Ratio);
                item.Total = (float)(DeliveryRatio * Delivery * Ratio);
                item.Metal = item.Destiny = item.Description = item.Accuracy = "";

                items.Add(item);
            }

            TotalCount.Text = $"{items.Sum(t => t.Count)}";
            TotalPrice.Text = $"{items.Sum(t => (float)t.Total)}";

            return items;
        }

        public void UpdatePricePart()   //формирование предварительной цены детали
        {
            var baskets = BasketControls.Select(b => b.Basket);

            var parts = Parts.Union(baskets);

            var works = DetailControls.Where(d => d.Detail.IsComplect)
                .SelectMany(d => d.TypeDetailControls)
                .SelectMany(t => t.WorkControls)
                .Where(w => w.workType is ICut);

            if (works.Any())
            {
                foreach (var work in works)
                {
                    if (work.workType is ICut _cut && _cut.Parts?.Count > 0)
                    {
                        foreach (PartControl part in _cut.Parts)
                        {
                            if (_cut is CutControl && _cut.HaveCut) part.Part.Description = "Л";
                            else if (_cut is PipeControl && _cut.HaveCut) part.Part.Description = "Т";
                            else if (_cut is SawControl) part.Part.Description = "ЛП";
                            else part.Part.Description = "Б";

                            if (part.Part.PropsDict.ContainsKey(100) && part.Part.PropsDict[100].Count > 2)
                                part.Part.Accuracy = _cut is CutControl ?
                                    $"{part.Part.PropsDict[100][0].Trim()}x{part.Part.PropsDict[100][1].Trim()}"
                                    : $"{part.Part.PropsDict[100][2].Trim()} мм";

                            part.Part.Price = 0;

                            //пробегаемся по ключам от 50 до 70, которые зарезервированы под конкретные работы
                            if (part.Part.PropsDict.Count > 0)
                                for (int key = 50; key < 70; key++) part.Part.PropsDict.Remove(key);

                            part.Part.WorksDict ??= new();
                            part.Part.WorksDict.Clear();

                            //добавляем конструкторские работы в цену детали, если их необходимо "размазать"
                            if (CheckConstruct.IsChecked == true) part.Part.Price += (float)Math.Round(Construct / Parts.Count / part.Part.Count, 2);

                            //добавляем доставку в цену детали, если ее необходимо "размазать"
                            if (HasDelivery is null) part.Part.Price += (float)Math.Round((double)Delivery * DeliveryRatio / Parts.Count / part.Part.Count, 2);

                            var extra = work.type.WorkControls.Where(e => e.workType is ExtraControl);
                            if (extra.Any()) part.Part.Price += extra.Sum(e => e.Result) / _cut.Parts.Count / part.Part.Count;

                            part.PropertiesChanged?.Invoke(part, true);
                        }
                        work.PropertiesChanged?.Invoke(work, true);
                    }
                }

                foreach (Part p in parts)
                {
                    if (baskets.Contains(p))
                    {
                        if (!p.IsFixed)
                        {
                            p.FixedPrice = p.Price;
                            p.IsFixed = true;
                        }
                        p.Price = (float)Math.Ceiling(p.FixedPrice * Ratio * ((100 + BonusRatio) / 100));
                        continue;
                    }

                    p.Price *= (float)(Ratio * ((100 + BonusRatio) / 100));
                    p.Price = p.Price < p.FixedPrice ? p.FixedPrice : p.Price;
                    p.Price = (float)Math.Ceiling(p.Price);
                }
            }

            if (AssemblyWindow.A.Assemblies.Count > 0)
            {
                AssemblyWindow.A.CheckAssemblies();

                foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
                {
                    if (assembly.Particles.Count == 0)
                    {
                        assembly.Price = assembly.Total = 0;
                        continue;
                    }

                    foreach (Particle particle in assembly.Particles)
                    {
                        Part? part = parts.FirstOrDefault(p => p.Title == particle.Title);
                        if (part is not null)
                            particle.Price = (float)(part.Price +
                                (assembly.WeldPrice + assembly.PaintPrice)
                                * Ratio * ((100 + BonusRatio) / 100)
                                / assembly.Count / assembly.Particles.Sum(p => p.Count));
                    }
                    assembly.Price = (float)Math.Ceiling(assembly.Particles.Sum(p => p.Price * p.Count));
                    assembly.Total = assembly.Price * assembly.Count;
                }

                if (parts.Any())
                    foreach (Part part in parts)
                    {
                        Part? _part = AssemblyWindow.A.Assemblies.SelectMany(a => a.Particles).FirstOrDefault(x => x.Title == part.Title);

                        if (_part is null && !LooseParts.Contains(part)) LooseParts.Add(part);
                    }
            }
        }

        private void ToggleRowDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var row = FindVisualParent<DataGridRow>(element);
                if (row != null)
                {
                    row.DetailsVisibility = row.DetailsVisibility == Visibility.Visible
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
            }
        }

        public static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null && child is not T)
                child = VisualTreeHelper.GetParent(child);
            return child as T;
        }
        #endregion


        //-------------Подключения к базе расчетов-----------------//
        #region
        private int _lastKnownOffersCount = -1;                 // -1 означает "не инициализировано"
        private DateTime _lastOffersCheck = DateTime.MinValue;  // для ограничения частоты запросов
        private const int OFFERS_CHECK_INTERVAL_SECONDS = 30;

        /// <summary>
        /// Сбрасывает все фильтры: "В производстве", поиск и т.д.
        /// </summary>
        private void ResetFilters()
        {
            // ⭐ Сбрасываем тоггл "В производстве"
            if (InProductionFilterToggle.IsChecked == true)
            {
                // ⭐ Временно отписываемся от события, чтобы избежать двойного вызова ApplyCurrentMode
                InProductionFilterToggle.Checked -= ProductionToggle_StateChanged;
                InProductionFilterToggle.Unchecked -= ProductionToggle_StateChanged;

                InProductionFilterToggle.IsChecked = false;
                _isProductionMode = false;

                // ⭐ Скрываем панель прогноза
                ForecastPanel.Visibility = Visibility.Collapsed;

                // ⭐ Подписываемся обратно
                InProductionFilterToggle.Checked += ProductionToggle_StateChanged;
                InProductionFilterToggle.Unchecked += ProductionToggle_StateChanged;
            }

            // ⭐ Сбрасываем поисковый запрос
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                _searchQuery = string.Empty;
            }
        }

        //метод обновления коллекции расчетов
        private async void UpdateOffersCollection(object sender, RoutedEventArgs e)
        {
            if (TargetManager == null) return;

            try
            {
                UpdateBtn.IsEnabled = false;
                StatusBegin("Обновление списка расчётов...", StatusMessageType.Info);

                bool wasSearchActive = !string.IsNullOrWhiteSpace(_searchQuery);

                ResetFilters();

                int lastOfferIdBefore = CurrentOffers?.Select(o => o.Id).DefaultIfEmpty(0).Max() ?? 0;

                await LoadManagerDataAsync(TargetManager);

                // Сбрасываем флаг поиска после обновления
                _searchQuery = string.Empty;

                if (wasSearchActive)
                {
                    // После поиска не считаем «новые» — просто сообщаем об обновлении
                    StatusBegin("Список расчётов обновлён", StatusMessageType.Success);
                }
                else
                {
                    var newOffers = CurrentOffers?
                        .Where(o => o.Id > lastOfferIdBefore)
                        .OrderByDescending(o => o.Id)
                        .ToList() ?? new List<Offer>();

                    int newCount = newOffers.Count;

                    if (newCount == 0)
                    {
                        StatusBegin("Список расчётов обновлён (новых нет)", StatusMessageType.Success);
                    }
                    else if (newCount == 1)
                    {
                        ScrollToOfferAndHighlight(newOffers[0]);
                        StatusBegin($"Обновлено: добавлен расчёт {newOffers[0].N}", StatusMessageType.Success);
                    }
                    else
                    {
                        ScrollToOfferAndHighlight(newOffers[0]);
                        StatusBegin($"Обновлено: добавлено {newCount} расчётов", StatusMessageType.Success);
                    }
                }

                UpdateBtn?.ClearValue(BackgroundProperty);
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка обновления: {ex.Message}", StatusMessageType.Error);
            }
            finally
            {
                UpdateBtn.IsEnabled = true;
            }
        }

        private void OffersGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // ⭐ Быстрая синхронная проверка: если выбран расчетный менеджер — скрываем пункт
            bool isDraftManager = TargetManager?.Name == "Расчетный менеджер";
            LaunchToWorkMenuItem.Visibility = isDraftManager ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OffersGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // ⭐ Игнорируем отмену редактирования (Esc)
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row.Item is not Offer offer) return;

            // ⭐ Проверяем, действительно ли данные изменились
            if (!HasOfferDataChanged(e.EditingElement, offer))
            {
                Trace.WriteLine($"ℹ️ Изменений в расчёте {offer.N} не обнаружено, пропуск");
                return;
            }

            // ⭐ Проверка прав доступа
            if (CurrentManager?.IsEngineer == true)
            {
                StatusBegin("Инженеры не могут редактировать расчёты", StatusMessageType.Warning);
                e.Cancel = true;
                return;
            }

            if (CurrentManager != null && !CurrentManager.IsAdmin && offer.ManagerId != CurrentManager.Id)
            {
                StatusBegin("Вы не можете редактировать расчёты другого менеджера", StatusMessageType.Warning);
                e.Cancel = true;
                return;
            }

            // ⭐ ГЛАВНОЕ: откладываем обновление через Dispatcher
            // Это даёт WPF время завершить режим редактирования ДО вызова Refresh
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await UpdateOfferAsync(offer);
                }
                catch (Exception ex)
                {
                    StatusBegin($"Ошибка сохранения: {ex.Message}", StatusMessageType.Error);
                    Trace.WriteLine($"❌ Ошибка отложенного UpdateOfferAsync: {ex.Message}");
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// Проверяет, действительно ли изменились данные в редактируемой ячейке.
        /// </summary>
        private bool HasOfferDataChanged(FrameworkElement editingElement, Offer offer)
        {
            // Получаем привязку ячейки
            if (editingElement is TextBox textBox)
            {
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                if (binding?.ParentBinding?.Path?.Path == null) return true;

                string propertyName = binding.ParentBinding.Path.Path;

                // Получаем текущее и исходное значение
                var currentValue = textBox.Text;
                var originalValue = offer.GetType().GetProperty(propertyName)?.GetValue(offer)?.ToString() ?? "";

                return currentValue != originalValue;
            }

            if (editingElement is CheckBox checkBox)
            {
                var binding = checkBox.GetBindingExpression(CheckBox.IsCheckedProperty);
                if (binding?.ParentBinding?.Path?.Path == null) return true;

                string propertyName = binding.ParentBinding.Path.Path;
                var currentValue = checkBox.IsChecked ?? false;
                var originalValue = offer.GetType().GetProperty(propertyName)?.GetValue(offer) as bool? ?? false;

                return currentValue != originalValue;
            }

            return true; // По умолчанию считаем, что изменения есть
        }
        private string GetBindingPropertyName(FrameworkElement element)
        {
            var binding = element switch
            {
                TextBox textBox => textBox.GetBindingExpression(TextBox.TextProperty)?.ParentBinding,
                CheckBox checkBox => checkBox.GetBindingExpression(CheckBox.IsCheckedProperty)?.ParentBinding,
                _ => null
            };

            return binding?.Path.Path ?? string.Empty;
        }

        /// <summary>
        /// Сохраняет изменения расчёта в БД и обновляет представление.
        /// </summary>
        private async System.Threading.Tasks.Task UpdateOfferAsync(Offer offer)
        {
            if (offer == null) return;

            try
            {
                StatusBegin($"Сохранение изменений расчёта {offer.N}...", StatusMessageType.Info);

                // ⭐ Сохраняем состояние групп ДО обновления
                var expandedGroups = GetExpandedGroupNames();

                // ⭐ Сохраняем в БД
                bool success = await DataService.UpdateOfferAsync(offer);

                if (success)
                {
                    StatusBegin($"Данные расчёта {offer.N} изменены.", StatusMessageType.Success);

                    // ⭐ ВАЖНО: Refresh через DispatcherPriority.Loaded
                    // К этому моменту WPF уже завершил режим редактирования
                    await Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            OffersView?.Refresh();
                        }
                        catch (InvalidOperationException ex)
                        {
                            Trace.WriteLine($"⚠️ Refresh отложен: {ex.Message}");
                            // Повторная попытка на следующем цикле отрисовки
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try { OffersView?.Refresh(); }
                                catch { /* игнорируем */ }
                            }), DispatcherPriority.Loaded);
                        }
                    }, DispatcherPriority.Loaded);

                    // ⭐ Ждём пересоздания контейнеров
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    RestoreExpandedGroups(expandedGroups);

                    // ⭐ Подсветка строки
                    HighlightOfferRow(offer);

                    if (ActiveOffer?.Id == offer.Id)
                    {
                        CreateComplect(connections[5], offer);
                    }
                }
                else
                {
                    StatusBegin($"Не удалось сохранить изменения расчёта {offer.N}.", StatusMessageType.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка обновления: {ex.Message}", StatusMessageType.Error);
                Trace.WriteLine($"❌ Ошибка UpdateOfferAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Собирает имена всех развёрнутых групп.
        /// </summary>
        private HashSet<string> GetExpandedGroupNames()
        {
            var expanded = new HashSet<string>();
            if (OffersGrid?.Items.Groups == null) return expanded;

            foreach (var group in OffersGrid.Items.Groups)
            {
                if (group is CollectionViewGroup cvg && cvg.Name != null)
                {
                    var groupItem = OffersGrid.ItemContainerGenerator.ContainerFromItem(group) as GroupItem;
                    if (groupItem != null)
                    {
                        var expander = FindVisualChild<Expander>(groupItem);
                        if (expander?.IsExpanded == true)
                        {
                            expanded.Add(cvg.Name.ToString()!);
                        }
                    }
                }
            }
            return expanded;
        }

        /// <summary>
        /// Восстанавливает состояние развёрнутости групп.
        /// </summary>
        private void RestoreExpandedGroups(HashSet<string> expandedGroups)
        {
            if (OffersGrid?.Items.Groups == null) return;

            foreach (var group in OffersGrid.Items.Groups)
            {
                if (group is CollectionViewGroup cvg && cvg.Name != null)
                {
                    if (expandedGroups.Contains(cvg.Name.ToString()!))
                    {
                        var groupItem = OffersGrid.ItemContainerGenerator.ContainerFromItem(group) as GroupItem;
                        if (groupItem != null)
                        {
                            var expander = FindVisualChild<Expander>(groupItem);
                            if (expander != null && !expander.IsExpanded)
                            {
                                expander.IsExpanded = true;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Разворачивает группу по ParentQuoteNumber.
        /// </summary>
        private void ExpandGroupByParentQuoteNumber(string parentQuoteNumber)
        {
            if (string.IsNullOrEmpty(parentQuoteNumber) || OffersView == null) return;

            // ⭐ Работаем с OffersView, а не с OffersGrid.Items.Groups
            var groups = OffersView.Groups;
            if (groups == null) return;

            foreach (var group in groups)
            {
                if (group is CollectionViewGroup cvg && cvg.Name?.ToString() == parentQuoteNumber)
                {
                    // ⭐ Находим GroupItem через ItemContainerGenerator
                    var groupItem = OffersGrid.ItemContainerGenerator.ContainerFromItem(group) as GroupItem;
                    if (groupItem != null)
                    {
                        var expander = FindVisualChild<Expander>(groupItem);
                        if (expander != null && !expander.IsExpanded)
                        {
                            expander.IsExpanded = true;
                            Trace.WriteLine($"✅ Группа '{parentQuoteNumber}' развёрнута");
                        }
                    }
                    else
                    {
                        Trace.WriteLine($"⚠️ GroupItem не найден для группы '{parentQuoteNumber}'");
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Прокручивает OffersGrid к указанному расчёту, разворачивает его группу и подсвечивает строку.
        /// Используется после добавления нового расчёта.
        /// </summary>
        public void ScrollToOfferAndHighlight(Offer offer)
        {
            if (offer == null || OffersGrid == null) return;

            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    // Находим актуальный объект в коллекции по Id
                    var actualOffer = CurrentOffers.FirstOrDefault(o => o.Id == offer.Id);
                    if (actualOffer == null)
                    {
                        Trace.WriteLine($"⚠️ Расчёт Id={offer.Id} не найден в коллекции CurrentOffers");
                        return;
                    }

                    Trace.WriteLine($"🔍 Прокрутка к расчёту: Id={actualOffer.Id}, N={actualOffer.N}, ParentQuoteNumber={actualOffer.ParentQuoteNumber}");

                    // ⭐ Ждём завершения рендеринга
                    await System.Threading.Tasks.Task.Delay(150);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

                    // 1. Разворачиваем группу
                    ExpandGroupByParentQuoteNumber(actualOffer.ParentQuoteNumber);

                    // ⭐ Ждём, пока группа раскроется
                    await System.Threading.Tasks.Task.Delay(250);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

                    // 2. Прокручиваем к строке
                    OffersGrid.ScrollIntoView(actualOffer);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        OffersGrid.UpdateLayout();
                    }, DispatcherPriority.Render);

                    // 3. Подсвечиваем строку
                    HighlightOfferRow(actualOffer);

                    Trace.WriteLine($"✅ Прокрутка и подсветка выполнены для Id={actualOffer.Id}");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Ошибка прокрутки к расчёту: {ex.Message}");
                }
            }), DispatcherPriority.Input); // ⭐ Input вместо Loaded
        }

        /// <summary>
        /// Подсвечивает строку расчёта в OffersGrid с плавным затуханием.
        /// </summary>
        public void HighlightOfferRow(Offer offer)
        {
            if (offer == null || OffersGrid == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // Находим актуальный объект в коллекции
                    var actualOffer = CurrentOffers.FirstOrDefault(o => o.Id == offer.Id);
                    if (actualOffer == null) return;

                    // Прокручиваем к строке
                    OffersGrid.ScrollIntoView(actualOffer);
                    OffersGrid.UpdateLayout();

                    var row = OffersGrid.ItemContainerGenerator.ContainerFromItem(actualOffer) as DataGridRow;

                    // Если строка не найдена (виртуализация), пробуем ещё раз
                    if (row == null)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var retryRow = OffersGrid.ItemContainerGenerator.ContainerFromItem(actualOffer) as DataGridRow;
                            if (retryRow != null) ApplyHighlight(retryRow);
                            else Trace.WriteLine($"⚠️ Строка не найдена после повторной попытки для Id={actualOffer.Id}");
                        }), DispatcherPriority.Input); // ⭐ Input вместо Background
                        return;
                    }

                    ApplyHighlight(row);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Ошибка подсветки строки: {ex.Message}");
                }
            }), DispatcherPriority.Input); // ⭐ Input вместо Loaded
        }

        private void ApplyHighlight(DataGridRow row)
        {
            var originalBrush = row.Background;
            var originalForeground = row.Foreground;

            // ⭐ Используем SetValue с приоритетом Local, чтобы переопределить стили
            row.SetValue(BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 230, 150)));
            row.SetValue(ForegroundProperty, new SolidColorBrush(Colors.Black));

            var fadeAnimation = new ColorAnimation
            {
                From = Color.FromRgb(255, 230, 150),
                To = originalBrush is SolidColorBrush solid ? solid.Color : Colors.Transparent,
                Duration = TimeSpan.FromMilliseconds(2000),
                FillBehavior = FillBehavior.Stop
            };

            fadeAnimation.Completed += (s, e) =>
            {
                row.SetValue(BackgroundProperty, originalBrush);
                row.SetValue(ForegroundProperty, originalForeground);
            };

            var highlightBrush = (SolidColorBrush)row.Background;
            highlightBrush.BeginAnimation(SolidColorBrush.ColorProperty, fadeAnimation);
        }

        // Поля для отслеживания текущего состояния сортировки
        private ListSortDirection _currentSortDirection = ListSortDirection.Ascending;
        private string _currentSortColumn = nameof(Offer.ParentQuoteNumber);

        /// <summary>
        /// Инициализация представления таблицы расчетов с группировкой по умолчанию.
        /// </summary>
        public void InitializeOffersView()
        {
            // ⭐ КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Создаем представление ТОЛЬКО если его еще нет.
            // Повторное создание CollectionViewSource при каждом поиске/обновлении 
            // разрывает внутренние связи DataGrid и является частой причиной крашей в WPF.
            if (OffersView == null)
            {
                var viewSource = new CollectionViewSource { Source = CurrentOffers };
                OffersView = viewSource.View;
                OffersGrid.ItemsSource = OffersView;
            }

            // Всегда применяем актуальную сортировку и группировку к существующему представлению
            string sortProp = string.IsNullOrEmpty(_currentSortColumn) ? nameof(Offer.ParentQuoteNumber) : _currentSortColumn;
            ApplySortingAndGrouping(sortProp, _currentSortDirection);

            // ⭐ Безопасное восстановление развернутых групп (только если группы вообще существуют)
            if (OffersView is ICollectionView view && view.Groups != null && view.Groups.Count > 0)
            {
                var expandedGroups = GetExpandedGroupNames();
                if (expandedGroups.Any())
                {
                    Dispatcher.BeginInvoke(new Action(() => RestoreExpandedGroups(expandedGroups)), DispatcherPriority.Background);
                }
            }
        }

        /// <summary>
        /// Обработчик клика по заголовку столбца.
        /// </summary>
        private void OffersGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Отменяем стандартную сортировку WPF, чтобы управлять ею вручную
            e.Handled = true;

            var column = e.Column;
            string propertyName = column.SortMemberPath;

            if (string.IsNullOrEmpty(propertyName)) return;

            // Если кликнули по тому же столбцу, меняем направление. Иначе - сбрасываем на Ascending.
            if (_currentSortColumn == propertyName)
            {
                _currentSortDirection = _currentSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                _currentSortColumn = propertyName;
                _currentSortDirection = ListSortDirection.Ascending;
            }

            // Применяем новую логику сортировки и группировки
            ApplySortingAndGrouping(propertyName, _currentSortDirection);
        }

        /// <summary>
        /// Централизованное применение сортировки и (опционально) группировки.
        /// </summary>
        private void ApplySortingAndGrouping(string sortProperty, ListSortDirection direction)
        {
            if (OffersView is not ICollectionView view) return;

            // 1. Очищаем старые правила
            view.SortDescriptions.Clear();
            view.GroupDescriptions.Clear();

            // 2. Логика: группировка применяется ТОЛЬКО если сортируем по номеру расчета.
            // При сортировке по любому другому полю (Компания, Дата и т.д.) группировка снимается 
            // для отображения глобального отсортированного списка.
            bool shouldGroup = sortProperty == nameof(Offer.ParentQuoteNumber);

            if (shouldGroup)
            {
                view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Offer.ParentQuoteNumber)));
            }

            // 3. Добавляем новое правило сортировки
            view.SortDescriptions.Add(new SortDescription(sortProperty, direction));

            // 4. Обновляем визуальные индикаторы (стрелочки) в заголовках столбцов
            UpdateColumnSortIndicators(sortProperty, direction);

            // 5. Если мы вернулись к режиму группировки, восстанавливаем развернутые группы
            if (shouldGroup)
            {
                Dispatcher.BeginInvoke(new Action(() => RestoreExpandedGroups(GetExpandedGroupNames())), DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// Визуальное отображение направления сортировки в заголовках столбцов.
        /// </summary>
        private void UpdateColumnSortIndicators(string sortProperty, ListSortDirection direction)
        {
            foreach (var column in OffersGrid.Columns)
            {
                if (column.SortMemberPath == sortProperty)
                {
                    column.SortDirection = direction;
                }
                else
                {
                    column.SortDirection = null; // Сбрасываем индикатор у остальных колонок
                }
            }
        }

        /// <summary>
        /// Рекурсивный поиск визуального дочернего элемента заданного типа.
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found) return found;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        //-----------Поиск расчетов по номеру КП, компании, номеру счета или заказа-----------//
        private async void Search_Offers(object sender, FunctionEventArgs<string> e)
        {
            _searchQuery = e.Info ?? string.Empty;
            _isProductionMode = false;
            if (InProductionFilterToggle.IsChecked == true)
                InProductionFilterToggle.IsChecked = false;

            if (string.IsNullOrWhiteSpace(_searchQuery) || TargetManager.Name is null)
            {
                // Пустой поиск — возвращаем последние 50
                await LoadManagerDataAsync(TargetManager);
                return;
            }

            try
            {
                StatusBegin($"Поиск '{_searchQuery}' в базе...", StatusMessageType.Info);

                var results = await DataService.SearchOffersAsync(
                    TargetManager.Id, TargetManager.Name, _searchQuery.Trim());

                CurrentOffers.Clear();
                foreach (var offer in results) CurrentOffers.Add(offer);

                InitializeOffersView();

                // ⭐ Показываем РЕЗУЛЬТАТ, а не процесс
                StatusBegin($"Найдено расчётов: {results.Count}", StatusMessageType.Success);
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка поиска: {ex.Message}", StatusMessageType.Error);
            }
        }

        public string SaveOfferData()                               //метод сериализации расчета
        {
            using MemoryStream stream = new();
            DataContractJsonSerializer serializer = new(typeof(Product));
            serializer.WriteObject(stream, ProductModel.Product);   //сериализуем объект

            return Encoding.UTF8.GetString(stream.ToArray());       //возвращаем строку преобразованного объекта в массив байтов
        }

        public static Product? OpenOfferData(string json)           //метод десериализации расчета
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);            //преобразуем строку в массив байтов

            using MemoryStream stream = new(bytes);
            DataContractJsonSerializer serializer = new(typeof(Product));

            return (Product?)serializer.ReadObject(stream);         //возвращаем десериализованный объект
        }

        public static Product? OpenOfferDataSafe(string json, out string? error)
        {
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return null;
                return OpenOfferData(json);
            }
            catch (System.Runtime.Serialization.SerializationException ex)
            {
                error = $"Serialization: {ex.Message}";
                return null;
            }
            catch (JsonException ex)
            {
                error = $"JSON: {ex.Message}";
                return null;
            }
            catch (Exception ex)
            {
                error = $"Unexpected: {ex.GetBaseException().Message}";
                return null;
            }
        }


        //метод загрузки строк в таблицу ВСЕХ расчетов
        private void OffersGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            Button btn = new() { BorderThickness = new Thickness(0), Width = 20, Padding = new(2) };

            if (e.Row.Item is Offer offer)
            {
                // добавить в январе проверку даты отгрузки '&& offer.EndDate is not null'
                if (offer.Order is not null && offer.Order != "")
                {
                    btn.Content = new Image() { Source = new BitmapImage(new Uri($"Images/delete.png", UriKind.Relative)) };
                    btn.ToolTip = "Удалить из отчета";
                    btn.Click += RemoveOfferFromReport;
                }
                else
                {
                    btn.Content = new Image() { Source = new BitmapImage(new Uri($"Images/ruble.png", UriKind.Relative)) };
                    btn.ToolTip = "Добавить в отчет";
                    btn.Click += AddOfferToReport;
                }
            }

            e.Row.Header = btn;
        }

        private async void OnShipmentToggleClick(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn &&
                btn.DataContext is Offer offer &&
                !string.IsNullOrEmpty(offer.Order))
            {
                offer.EndDate = btn.IsChecked == true
                    ? DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                    : null;
                await UpdateOfferAsync(offer);
            }
        }

        private async void AddOfferToReport(object sender, RoutedEventArgs e)
        {
            if (OffersGrid.SelectedItem is Offer offer)
            {
                offer.Order = offer.N;
                await UpdateOfferAsync(offer);

                if (sender is Button btn)
                {
                    btn.Content = new Image() { Source = new BitmapImage(new Uri($"Images/delete.png", UriKind.Relative)) };
                    btn.ToolTip = "Удалить из отчета";
                    btn.Click -= AddOfferToReport;
                    btn.Click += RemoveOfferFromReport;
                }
            }
        }

        private async void RemoveOfferFromReport(object sender, RoutedEventArgs e)
        {
            if (OffersGrid.SelectedItem is Offer offer)
            {
                offer.Order = "";
                await UpdateOfferAsync(offer);

                if (sender is Button btn)
                {
                    btn.Content = new Image() { Source = new BitmapImage(new Uri($"Images/ruble.png", UriKind.Relative)) };
                    btn.ToolTip = "Добавить в отчет";
                    btn.Click -= RemoveOfferFromReport;
                    btn.Click += AddOfferToReport;
                }
            }
        }

        //метод загрузки строк в таблицу ОТЧЕТНЫХ расчетов
        private void ReportGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            Button btn = new() { BorderThickness = new Thickness(0), Width = 20, Padding = new(2) };

            if (e.Row.Item is Offer offer)
            {
                if (offer.Invoice is not null && offer.Invoice.Contains(" (без бонуса)"))
                {
                    btn.Content = new Image() { Source = new BitmapImage(new Uri($"Images/notbonus.png", UriKind.Relative)) };
                    btn.ToolTip = "Добавить бонус";
                    btn.Click += RemoveOfferFromBonus;
                }
                else
                {
                    btn.Content = new Image() { Source = new BitmapImage(new Uri($"Images/ruble.png", UriKind.Relative)) };
                    btn.ToolTip = "Убрать бонус";
                    btn.Click += AddOfferToBonus;
                }
            }

            btn.MouseEnter += (sender, k) => { e.Row.IsSelected = true; };
            btn.MouseLeave += (sender, k) => { e.Row.IsSelected = false; };
            e.Row.Header = btn;
        }

        private async void AddOfferToBonus(object sender, RoutedEventArgs e)
        {
            if (ReportGrid.SelectedItem is Offer offer)
            {
                offer.Invoice = (offer.Invoice ?? "") + " (без бонуса)";
                await UpdateOfferAsync(offer);

                if (sender is Button btn)
                {
                    btn.Content = new Image() { Source = new BitmapImage(new Uri($"Images/notbonus.png", UriKind.Relative)) };
                    btn.ToolTip = "Добавить бонус";
                    btn.Click -= AddOfferToBonus;
                    btn.Click += RemoveOfferFromBonus;
                }
            }
        }

        private async void RemoveOfferFromBonus(object sender, RoutedEventArgs e)
        {
            if (ReportGrid.SelectedItem is Offer offer && offer.Invoice != null && offer.Invoice.Contains(" (без бонуса)"))
            {
                offer.Invoice = offer.Invoice.Replace(" (без бонуса)", "");
                await UpdateOfferAsync(offer);

                if (sender is Button btn)
                {
                    btn.Content = new Image() { Source = new BitmapImage(new Uri($"Images/ruble.png", UriKind.Relative)) };
                    btn.ToolTip = "Убрать бонус";
                    btn.Click -= RemoveOfferFromBonus;
                    btn.Click += AddOfferToBonus;
                }
            }
        }
        #endregion


        //-------------Сохранение и загрузка контролов расчета-------//
        #region
        public Product SaveProduct()
        {
            Log = null;         //очищаем логи об анализе раскладок и прочие

            Product product = new()
            {
                Order = Order.Text,
                Company = CustomerDrop.Text,
                Production = DateProduction.Text,
                Manager = Adress.Text,                  //поле "Manager" сохраняет ссылку на адрес доставки;
                                                        //не менял название поля, чтобы загружались старые сохранения
                Ratio = Ratio,
                MaterialFactor = MaterialFactor,
                ServiceFactor = ServiceFactor,
                Count = Count,
                ConstructRatio = ConstructRatio.Text,
                Delivery = Delivery,
                DeliveryRatio = DeliveryRatio,
                IsAgent = IsAgent,
                HasDelivery = HasDelivery,
                HasConstruct = CheckConstruct.IsChecked,
                HasAssembly = HasAssembly,
                IsExpressOffer = IsExpressOffer,
                BonusRatio = BonusRatio,

                Baskets = BasketControls.Select(b => b.Basket).ToList(),
                Details = SaveDetails(),
                Assemblies = AssemblyWindow.A.Assemblies,
                Comment = Comment.Text,
            };

            ProductModel.Product = product;

            return product;
        }
        public ObservableCollection<Detail> SaveDetails()
        {
            if (TempWorksDict.Count > 0) TempWorksDict.Clear();

            ObservableCollection<Detail> details = new();
            for (int i = 0; i < DetailControls.Count; i++)
            {
                DetailControl det = DetailControls[i];
                Detail _detail = det.Detail;
                _detail.Metal = _detail.Destiny = _detail.Description = "";     //очищаем описания свойств детали

                if (_detail.TypeDetails.Count > 0) _detail.TypeDetails.Clear(); //как будто решаем проблему дублирования при пересохранении

                for (int j = 0; j < det.TypeDetailControls.Count; j++)
                {
                    TypeDetailControl type = det.TypeDetailControls[j];
                    SaveTypeDetail _typeDetail = new(type.TypeDetailDrop.SelectedIndex, type.Count, type.MetalDrop.SelectedIndex, type.HasMetal,
                        (type.SortDrop.SelectedIndex, type.A, type.B, type.S, type.L), type.ExtraResult, type.Comment);

                    if (!Destinies.Contains(type.S) && (Log is null || !Log.Contains("Проверьте толщину заготовок во всех деталях!"))) Log += "\nПроверьте толщину заготовок во всех деталях!\n";

                    //с помощью повторения символа переноса строки визуализируем дерево деталей и работ
                    if (type.MetalDrop.SelectedItem is Metal _metal)
                        _detail.Metal += $"{_metal.Name}" + string.Join("", Enumerable.Repeat('\n', type.WorkControls.Count));
                    _detail.Destiny += $"{type.S}" + string.Join("", Enumerable.Repeat('\n', type.WorkControls.Count));

                    //удаляем крайний перенос строки
                    if (j == det.TypeDetailControls.Count - 1)
                    {
                        _detail.Metal = _detail.Metal.TrimEnd('\n');
                        _detail.Destiny = _detail.Destiny.TrimEnd('\n');
                    }

                    for (int k = 0; k < type.WorkControls.Count; k++)
                    {
                        if (type.WorkControls[k].Result == 0) continue;     //пропускаем сохранение нулевых работ

                        WorkControl work = type.WorkControls[k];

                        if (work.WorkDrop.SelectedItem is Work _work)
                        {
                            if (work.workType is ExtraControl extra && extra.NameExtra != null)         //проверяем наличие доп работ
                            {
                                //если комментарий еще не содержит доп работу с таким именем, создаем такую запись
                                if (!Comment.Text.Contains($"{extra.NameExtra}"))
                                {
                                    if (!Comment.Text.Contains("Доп работы -")) Comment.Text += " Доп работы -";
                                    Comment.Text += $" {extra.NameExtra}";
                                }

                                //если список еще не содержит доп работу с таким именем, создаем такую запись, иначе просто добавляем стоимость
                                if (_work.Name == "Доп работа П")
                                {
                                    if (!TempWorksDict.ContainsKey($"{extra.NameExtra} (П)")) TempWorksDict[$"{extra.NameExtra} (П)"] = work.Result;
                                    else TempWorksDict[$"{extra.NameExtra} (П)"] += work.Result;
                                }

                                if (_work.Name == "Доп работа Л")
                                {
                                    if (!TempWorksDict.ContainsKey($"{extra.NameExtra} (Л)")) TempWorksDict[$"{extra.NameExtra} (Л)"] = work.Result;
                                    else TempWorksDict[$"{extra.NameExtra} (Л)"] += work.Result;
                                }
                            }
                            else if (work.workType is PaintControl paint && paint.Ral != null)          //проверяем наличие окраски
                            {
                                //если список еще не содержит окраску в этот цвет, создаем такую запись, иначе просто добавляем стоимость
                                if (!TempWorksDict.ContainsKey($"Окраска в {paint.Ral} {paint.TypeDrop.SelectedItem}"))
                                    TempWorksDict[$"Окраска в {paint.Ral} {paint.TypeDrop.SelectedItem}"] = work.Result;
                                else TempWorksDict[$"Окраска в {paint.Ral} {paint.TypeDrop.SelectedItem}"] += work.Result;
                            }
                            else if (_work.Name != null)                                                //проверяем все остальные работы
                            {
                                if (_work.Name == "Фрезеровка")
                                {
                                    //если комментарий еще не содержит предупреждение о заготовках, создаем такую запись
                                    if (!Comment.Text.Contains("Для фрезеровки габариты некоторых деталей увеличены!"))
                                        Comment.Text += " Для фрезеровки габариты некоторых деталей увеличены!";
                                }

                                //если список еще не содержит работу с таким именем, создаем такую запись, иначе просто добавляем стоимость
                                if (!TempWorksDict.ContainsKey(_work.Name)) TempWorksDict[_work.Name] = work.Result;
                                else TempWorksDict[_work.Name] += work.Result;
                            }

                            SaveWork _saveWork = new(_work.Name, work.Ratio, work.TechRatio) { ExtraResult = work.ExtraResult };

                            if (work.workType is ICut _cut && _cut.PartDetails?.Count > 0)
                            {
                                foreach (Part p in _cut.PartDetails)
                                {
                                    if (_cut is CutControl cut && cut.HaveCut)
                                    {
                                        _saveWork.IsGrooved = cut.IsGrooved;
                                        p.Description = "Л";
                                        if (cut.HaveNitro && (Log is null || !Log.Contains("Проверьте общую стоимость резки АЗОТОМ!")))
                                            Log += "\nПроверьте общую стоимость резки АЗОТОМ!\nМинимальная стоимость - 25 000 руб.\n";
                                    }
                                    else if (_cut is PipeControl && _cut.HaveCut) p.Description = "Т";
                                    else if (_cut is SawControl) p.Description = "ЛП";
                                    else p.Description = "Б";

                                    if (p.PropsDict.ContainsKey(100) && p.PropsDict[100].Count > 2)
                                        p.Accuracy = _cut is CutControl ?
                                            $"{p.PropsDict[100][0].Trim()}x{p.PropsDict[100][1].Trim()}"
                                            : $"{p.PropsDict[100][2].Trim()} мм";

                                    p.Price = 0;

                                    //пробегаемся по ключам от 50 до 70, которые зарезервированы под конкретные работы
                                    if (p.PropsDict.Count > 0)
                                        for (int key = 50; key < 70; key++) p.PropsDict.Remove(key);

                                    p.WorksDict ??= new();
                                    p.WorksDict.Clear();

                                    //добавляем конструкторские работы в цену детали, если их необходимо "размазать"
                                    if (CheckConstruct.IsChecked == true)
                                    {
                                        float _send = (float)Math.Round(Construct / DetailControls.Count / det.TypeDetailControls.Count / _cut.PartDetails.Sum(p => p.Count), 2);
                                        p.Price += _send;
                                        p.PropsDict[62] = new() { $"{_send}" };
                                    }

                                    //добавляем доставку в цену детали, если ее необходимо "размазать"
                                    if (HasDelivery is null)
                                    {
                                        float _send = (float)Math.Round((double)Delivery * DeliveryRatio / Parts.Count / p.Count, 2);
                                        p.Price += _send;
                                        p.PropsDict[63] = new() { $"{_send}" };
                                    }

                                    //"размазываем" доп работы
                                    foreach (WorkControl w in work.type.WorkControls)
                                        if (w.workType is ExtraControl)
                                        {
                                            float _send = w.Result / _cut.PartDetails.Sum(p => p.Count);
                                            p.Price += _send;
                                            p.Description += " + Доп ";
                                            if (w.WorkDrop.SelectedItem is Work _extra && _extra.Name == "Доп работа П") p.PropsDict[59] = new() { $"{_send}" };
                                            else p.PropsDict[60] = new() { $"{_send}" };
                                        }
                                }

                                if (_cut.Parts?.Count > 0)
                                    foreach (PartControl part in _cut.Parts)
                                        part.PropertiesChanged?.Invoke(part, true);

                                _saveWork.Parts = _cut.PartDetails;
                                if (_cut.Items?.Count > 0) _saveWork.Items = _cut.Items;
                            }

                            work.PropertiesChanged?.Invoke(work, true);
                            _saveWork.PropsList = work.propsList;

                            //для окраски уточняем цвет в описании работы
                            if (work.workType is PaintControl _paint) _detail.Description += $"{_work.Name}(цвет - {_paint.Ral} {_paint.TypeDrop.SelectedItem})\n";
                            //для доп работы её наименование добавляем к наименованию работы - особый случай
                            else if (work.workType is ExtraControl _extra) _detail.Description += $"{_extra.NameExtra}\n";
                            //в остальных случаях добавляем наименование работы
                            else _detail.Description += $"{_work.Name}\n";

                            //удаляем крайний перенос строки
                            if (j == det.TypeDetailControls.Count - 1 && k == type.WorkControls.Count - 1)
                                _detail.Description = _detail.Description.TrimEnd('\n');

                            _typeDetail.Works.Add(_saveWork);
                        }
                    }
                    _detail.TypeDetails.Add(_typeDetail);
                }
                details.Add(_detail);
            }
            return details;
        }
        public void CreateFolderTagsForCalculation(string calculationFolderPath)
        {
            try
            {
                // Собираем все комментарии из всех заготовок
                var allComments = new List<string>();

                foreach (var detail in DetailControls)
                {
                    foreach (var typeDetail in detail.TypeDetailControls)
                    {
                        if (!string.IsNullOrWhiteSpace(typeDetail.Comment))
                            allComments.Add(typeDetail.Comment);
                    }
                }

                if (!allComments.Any())
                    return;

                // Объединяем все комментарии
                string fullComment = string.Join(" ", allComments);

                // Получаем все тэги
                var allTags = TagManager.LoadTags();

                // Извлекаем тэги-папки
                var folderTagNames = FolderTagService.GetFolderTagsFromComment(fullComment, allTags);

                if (folderTagNames.Any())
                {
                    // Создаем папки
                    var created = FolderTagService.CreateFolderTags(calculationFolderPath, folderTagNames);

                    if (created.Any())
                    {
                        StatusBegin($"Созданы папки: {string.Join(", ", created)}");
                    }
                }
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка при создании папок тэгов: {ex.Message}");
            }
        }

        public void LoadProduct()
        {
            if (ProductModel.Product == null) return;

            //выйти из режима заявки
            if (RequestControl != null) CloseRequestControl();

            Order.Text = ProductModel.Product.Order;
            CustomerDrop.Text = ProductModel.Product.Company;
            Adress.Text = ProductModel.Product.Manager;
            Comment.Text = ProductModel.Product.Comment;

            CheckConstruct.IsChecked = ProductModel.Product.HasConstruct;
            ConstructRatio.Text = ProductModel.Product.ConstructRatio != null
                        && Parser(ProductModel.Product.ConstructRatio) > 1
                        ? ProductModel.Product.ConstructRatio : "1";

            SetRatio(ProductModel.Product.Ratio);
            MaterialFactor = ProductModel.Product.MaterialFactor;
            SetCount(ProductModel.Product.Count);
            SetDeliveryRatio(ProductModel.Product.DeliveryRatio);
            SetDelivery(ProductModel.Product.Delivery);
            IsAgent = ProductModel.Product.IsAgent;
            HasDelivery = ProductModel.Product.HasDelivery;
            HasAssembly = ProductModel.Product.HasAssembly;
            IsExpressOffer = ProductModel.Product.IsExpressOffer;
            SetBonusRatio(ProductModel.Product.BonusRatio);

            IsLoadData = true;
            ClearDetails();     // очищаем текущий расчет
            LoadDetails(ProductModel.Product.Details);
            ServiceFactor = ProductModel.Product.ServiceFactor;
            if (ProductModel.Product.Baskets?.Count > 0) LoadBaskets(ProductModel.Product.Baskets);
            DateProduction.Text = ProductModel.Product.Production;
            IsLoadData = false;

            if (ProductModel.Product.Assemblies?.Count > 0)
                AssemblyWindow.A = new() { Assemblies = ProductModel.Product.Assemblies };
        }
        public void LoadDetails(ObservableCollection<Detail> details)
        {
            for (int i = 0; i < details.Count; i++)
            {
                AddDetail();
                DetailControl _det = DetailControls[i];
                _det.Detail.Title = details[i].Title;

                //если деталь является Комплектом, запускаем ограничения
                if (_det.Detail.Title != null && _det.Detail.Title.Contains("Комплект")) _det.IsComplectChanged();

                _det.Detail.Count = details[i].Count;
                _det.Detail.MillingHoles = details[i].MillingHoles;
                _det.Detail.MillingGrooves = details[i].MillingGrooves;

                for (int j = 0; j < details[i].TypeDetails.Count; j++)
                {
                    TypeDetailControl _type = DetailControls[i].TypeDetailControls[j];
                    _type.TypeDetailDrop.SelectedIndex = details[i].TypeDetails[j].Index;
                    _type.Count = details[i].TypeDetails[j].Count;
                    _type.MetalDrop.SelectedIndex = details[i].TypeDetails[j].Metal;
                    _type.SortDrop.SelectedIndex = details[i].TypeDetails[j].Tuple.Item1;
                    _type.A = details[i].TypeDetails[j].Tuple.Item2;
                    _type.B = details[i].TypeDetails[j].Tuple.Item3;
                    _type.S = details[i].TypeDetails[j].Tuple.Item4;
                    _type.L = details[i].TypeDetails[j].Tuple.Item5;
                    _type.HasMetal = details[i].TypeDetails[j].HasMetal;
                    _type.ExtraResult = details[i].TypeDetails[j].ExtraResult;
                    _type.SetComment(details[i].TypeDetails[j].Comment);

                    foreach (SaveWork item in details[i].TypeDetails[j].Works)  //проверяем каждую сохраненную работу
                    {
                        //получаем последний созданный контрол
                        WorkControl _work = DetailControls[i].TypeDetailControls[j].WorkControls[^1];

                        //получаем работу, совпадающую по имени с сохраненной, на случай, если она уже добавлена
                        WorkControl? work = _type.WorkControls.FirstOrDefault(w =>
                            item.NameWork != null && !item.NameWork.Contains("Доп") &&
                            (
                                (w.WorkDrop?.SelectedItem is Work selectedWork && selectedWork.Name == item.NameWork) ||  // ← Основной поиск по объекту
                                w.WorkDrop?.Text == item.NameWork  // ← Фолбэк на случай, если binding уже обновился
                            ));

                        if (work is not null)
                        {
                            work.Ratio = item.Ratio;
                            work.TechRatio = item.TechRatio;
                            work.ExtraResult = item.ExtraResult;
                            continue;
                        }
                        else
                            foreach (Work w in _work.WorkDrop.Items)        // чтобы не подвязываться на сохраненный индекс работы, ориентируемся на ее имя                                          
                                if (w.Name == item.NameWork)                // таким образом избегаем ошибки, когда админ изменит порядок работ в базе данных
                                {
                                    _work.WorkDrop.SelectedIndex = _work.WorkDrop.Items.IndexOf(w);
                                    break;
                                }

                        if (_work.workType is ICut _cut)
                        {
                            if (item.Items?.Count > 0) _cut.Items = item.Items;
                            if (item.Parts.Count > 0) _cut.PartDetails = item.Parts;

                            if (_cut is CutControl cut)
                            {
                                cut.IsGrooved = item.IsGrooved;
                                if (_cut.Items?.Count > 0) cut.SumProperties(_cut.Items);
                                cut.Parts = cut.PartList();
                                cut.PartsControl = new(cut, cut.Parts);
                                cut.AddPartsControl();

                                if (cut.PartDetails?.Count > 0 && cut.Items?.Count > 0)
                                {
                                    var partByTitle = cut.PartDetails
                                        .Where(p => !string.IsNullOrEmpty(p.Title))
                                        .GroupBy(p => p.Title!)
                                        .ToDictionary(g => g.Key, g => g.First());

                                    foreach (var laserItem in cut.Items)
                                    {
                                        if (laserItem.NestingSheet?.Parts == null) continue;
                                        foreach (var placement in laserItem.NestingSheet.Parts)
                                        {
                                            if (placement.Part?.Title != null &&
                                                partByTitle.TryGetValue(placement.Part.Title, out var canonicalPart))
                                            {
                                                placement.Part = canonicalPart;
                                            }
                                        }
                                    }
                                }
                            }
                            else if (_cut is PipeControl pipe)
                            {
                                pipe.Parts = pipe.PartList();
                                pipe.PartsControl = new(pipe, pipe.Parts);
                                pipe.AddPartsControl();
                                pipe.SetTotalProperties();
                            }
                            else if (_cut is SawControl saw)
                            {
                                saw.Parts = saw.PartList();
                                saw.PartsControl = new(saw, saw.Parts);
                                saw.AddPartsControl();
                                saw.SetTotalProperties();
                            }

                            if (_cut.Parts?.Count > 0)
                                foreach (PartControl part in _cut.Parts)
                                {
                                    if (part.Part.WorksDict?.Count > 0)
                                        foreach (var guid in part.Part.WorksDict.Keys)
                                            part.AddControl((int)Parser(part.Part.WorksDict[guid][0]), guid);

                                    else if (part.Part.PropsDict.Count > 0)      //ключи от "[50]" зарезервированы под кусочки цены за работы, габариты детали и прочее
                                        foreach (int key in part.Part.PropsDict.Keys) if (key < 50)
                                            part.AddControl((int)Parser(part.Part.PropsDict[key][0]));

                                    part.PropertiesChanged?.Invoke(part, false);
                                }
                        }

                        _work.propsList = item.PropsList;
                        _work.PropertiesChanged?.Invoke(_work, false);
                        _work.Ratio = item.Ratio;
                        _work.TechRatio = item.TechRatio;
                        _work.ExtraResult = item.ExtraResult;

                        if (_type.WorkControls.Count < details[i].TypeDetails[j].Works.Count) _type.AddWork();
                    }

                    if (_det.TypeDetailControls.Count < details[i].TypeDetails.Count) _det.AddTypeDetail();
                }
            }
        }
        public void LoadBaskets(List<Part> baskets)
        {
            foreach (Part basket in baskets) AddBasket(basket);
        }
        #endregion


        //-------------Выходные файлы-----------//
        #region

        public bool isAssemblyOffer = false;
        //-КП
        public void ExportToExcel(string path)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet worksheet = workbook.Workbook.Worksheets.Add("КП");

            worksheet.Drawings.AddPicture("A1", IsLaser ? "laser_logo.jpg" : "app_logo.jpg");  //файлы должны быть в директории bin/Debug...

            int row = 8;        //счетчик строк деталей, начинаем с восьмой строки документа

            //если выбран формат сборочного КП
            if (isAssemblyOffer)
            {
                AssemblyWindow.A.CheckAssemblies();

                var baskets = BasketControls.Select(b => b.Basket);
                var parts = Parts.Union(baskets);

                if (AssemblyWindow.A.Assemblies.Count > 0)
                    foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
                    {
                        int rowAssembly = row;
                        row++;
                        for (int p = 0; p < assembly.Particles.Count; p++)
                        {
                            Particle particle = assembly.Particles[p];
                            if (particle.Count <= 0)
                            {
                                assembly.Particles.Remove(particle);
                                continue;
                            }

                            Part? part = parts.FirstOrDefault(p => p.Title == particle.Title);
                            if (part is not null)
                            {
                                particle.Price = (float)(part.Price + (assembly.WeldPrice + assembly.PaintPrice) / assembly.Count / assembly.Particles.Sum(p => p.Count));
                                worksheet.Cells[row, 1].Value = part.Metal;
                                worksheet.Cells[row, 2].Value = part.Destiny > 0 ? part.Destiny : "";
                                worksheet.Cells[row, 3].Value = part.Accuracy;
                                worksheet.Cells[row, 4].Value = part.Description;
                                worksheet.Cells[row, 5].Value = particle.Title;
                                worksheet.Cells[row, 6].Value = particle.Count;
                                row++;
                            }
                        }
                        assembly.Price = (float)Math.Ceiling(assembly.Particles.Sum(p => p.Price * p.Count) * Ratio * ((100 + BonusRatio) / 100));
                        assembly.Total = assembly.Price * assembly.Count;

                        worksheet.Cells[rowAssembly, 4].Value = assembly.Description;
                        worksheet.Cells[rowAssembly, 5].Value = assembly.Title;
                        worksheet.Cells[rowAssembly, 6].Value = assembly.Count;
                        worksheet.Cells[rowAssembly, 7].Value = assembly.Price;
                        worksheet.Cells[rowAssembly, 8].Value = assembly.Total;
                        worksheet.Row(rowAssembly).Style.Font.Bold = true;
                    }

                if (parts.Any())
                    foreach (Part part in parts)
                    {
                        Part? _part = AssemblyWindow.A.Assemblies.SelectMany(a => a.Particles).FirstOrDefault(x => x.Title == part.Title);
                        if (_part is null) LooseParts.Add(part);
                    }

                if (LooseParts.Count > 0)
                {
                    worksheet.Cells[row, 5].Value = "Дополнительные детали:";
                    worksheet.Cells[row, 5].Style.Font.Bold = true;
                    row++;

                    for (int i = 0; i < LooseParts.Count; i++)
                    {
                        LooseParts[i].Price = (float)Math.Ceiling(LooseParts[i].Price * Ratio * ((100 + BonusRatio) / 100));
                        LooseParts[i].Price = LooseParts[i].Price < LooseParts[i].FixedPrice ? LooseParts[i].FixedPrice : LooseParts[i].Price;
                    }
                    DataTable loosePartsTable = ToDataTable(LooseParts);
                    worksheet.Cells[row, 1].LoadFromDataTable(loosePartsTable, false);
                    row += loosePartsTable.Rows.Count;
                }
            }
            //иначе если есть нарезанные детали, вычисляем их общую стоимость, и оформляем их в КП
            else if (Parts.Count > 0)
            {
                var visiblePartsForExport = OfferCalculator.PrepareVisiblePartsForOffer(Parts, (float)Ratio, BonusRatio);

                DataTable partTable = ToDataTable(new ObservableCollection<Part>(visiblePartsForExport));
                worksheet.Cells[row, 1].LoadFromDataTable(partTable, false);
                row += partTable.Rows.Count;
            }

            //далее оформляем остальные детали и работы
            if (CheckConstruct.IsChecked == null)       //проверяем, включена ли опция добавления конструкторских работ отдельной строкой
                foreach (DetailControl d in DetailControls.Where(d => !d.Detail.IsComplect))
                {
                    //в этом случае, пересчитываем цену и стоимость деталей, которые НЕ "Комплект деталей" (их цена и стоимость пересчитываются в блоке SaveDetails())
                    d.Detail.Total -= Construct / DetailControls.Count;
                    d.Detail.Price = (float)Math.Ceiling(d.Detail.Total * Ratio * ((100 + BonusRatio) / 100) / d.Detail.Count);
                }

            ObservableCollection<Detail> _details = new(ProductModel.Product.Details.Where(d => !d.IsComplect));
            if (_details.Count > 0)
                foreach (Detail det in _details)
                {
                    det.Price = (float)Math.Ceiling(det.Price * Ratio * ((100 + BonusRatio) / 100));
                    det.Total = det.Price * det.Count;
                }
            DataTable detailTable = ToDataTable(_details);
            worksheet.Cells[row, 1].LoadFromDataTable(detailTable, false);
            row += detailTable.Rows.Count;

            //добавляем покупные издели
            if (!isAssemblyOffer && ProductModel.Product.Baskets?.Count > 0)
            {
                var basketsWithWork = ProductModel.Product.Baskets.Where(b => !string.IsNullOrEmpty(b.Description));
                if (basketsWithWork != null)
                    foreach (Part basketWithWork in basketsWithWork)
                    {
                        worksheet.Cells[row, 1].Value = basketWithWork.Metal;
                        worksheet.Cells[row, 2].Value = basketWithWork.Destiny > 0 ? basketWithWork.Destiny : "";
                        worksheet.Cells[row, 4].Value = basketWithWork.Description;
                        worksheet.Cells[row, 5].Value = basketWithWork.Title;
                        worksheet.Cells[row, 6].Value = basketWithWork.Count;
                        worksheet.Cells[row, 7].Value = (float)Math.Ceiling(basketWithWork.Price * Ratio * ((100 + BonusRatio) / 100));
                        worksheet.Cells[row, 8].Value = basketWithWork.Count * (float)Math.Ceiling(basketWithWork.Price * Ratio * ((100 + BonusRatio) / 100));
                        row++;
                    }

                var basketsExtra = basketsWithWork?.Count() > 0 ? ProductModel.Product.Baskets.Except(basketsWithWork) : ProductModel.Product.Baskets;

                worksheet.Cells[row, 5].Value = "Покупные изделия:";
                worksheet.Cells[row, 5].Style.Font.Bold = true;
                row++;

                foreach (Part basket in basketsExtra)
                {
                    worksheet.Cells[row, 5].Value = basket.Title;
                    worksheet.Cells[row, 6].Value = basket.Count;
                    worksheet.Cells[row, 7].Value = (float)Math.Ceiling(basket.Price * Ratio * ((100 + BonusRatio) / 100));
                    worksheet.Cells[row, 8].Value = basket.Count * (float)Math.Ceiling(basket.Price * Ratio * ((100 + BonusRatio) / 100));
                    row++;
                }
            }

            // Префикс в зависимости от типа контрагента
            string prefix = IsAgent ? "Изготовление детали " : "Деталь ";

            foreach (var cell in worksheet.Cells[8, 5, row + 8, 5])
            {

                if (cell.Value == null || $"{cell.Value}" == "Дополнительные детали:") continue;
                else if ($"{cell.Value}" == "Покупные изделия:") break;

                string? value = cell.Value.ToString();

                // 1. Добавляем префикс
                value = prefix + value;

                // 2. Удаляем название металла (первое совпадение)
                foreach (Metal metal in Metals)
                {
                    if (string.IsNullOrEmpty(metal.Name)) continue;

                    int index = value.IndexOf(metal.Name, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        value = value.Remove(index, metal.Name.Length);
                        break;
                    }
                }

                // 3. Обрезаем
                value = Regex.Replace(value, @"\.[a-z]{2,5}$", "", RegexOptions.IgnoreCase);
                value = Regex.Replace(value, @"\s*\([^)]+\)\s*$", "", RegexOptions.IgnoreCase);
                value = Regex.Replace(value, @"\s+[sn]\d+(?:\.\d+)?\b", "", RegexOptions.IgnoreCase);

                // 4. Убираем лишние пробелы
                cell.Value = value.Trim();
            }

            //оформляем заголовки таблицы
            List<string> _headersD = new() { "Материал", "Толщина", "Размеры, мм", "Работы", "Наименование", "Кол-во, шт", "Цена за шт, руб", "Стоимость, руб" };
            for (int col = 0; col < _headersD.Count; col++)
            {
                worksheet.Cells[6, col + 1].Value = _headersD[col];
                worksheet.Cells[6, col + 1].Style.WrapText = true;
                worksheet.Cells[6, col + 1, 7, col + 1].Merge = true;
                worksheet.Cells[6, col + 1, 7, col + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[6, col + 1, 7, col + 1].Style.Fill.BackgroundColor.SetColor(0, IsLaser ? 120 : 255, IsLaser ? 180 : 170, IsLaser ? 255 : 0);
            }
            worksheet.Column(9).Hidden = true;
            worksheet.Column(10).Hidden = true;
            worksheet.Column(11).Hidden = true;
            worksheet.Column(12).Hidden = true;
            worksheet.Column(13).Hidden = true;
            worksheet.Column(14).Hidden = true;
            worksheet.Column(15).Hidden = true;
            worksheet.Column(16).Hidden = true;
            worksheet.Column(17).Hidden = true;
            worksheet.Column(18).Hidden = true;

            if (CheckConstruct.IsChecked == null)       //если требуется указать конструкторские работы отдельной строкой
            {
                worksheet.Cells[row, 5].Value = "Конструкторские работы";
                if (float.TryParse(ConstructRatio.Text, out float c) && c > 1)
                {
                    worksheet.Cells[row, 6].Value = c;
                    worksheet.Cells[row, 7].Value = (float)Math.Ceiling(Construct * Ratio * ((100 + BonusRatio) / 100) / c);
                }
                else
                {
                    worksheet.Cells[row, 6].Value = 1;
                    worksheet.Cells[row, 7].Value = (float)Math.Ceiling(Construct * Ratio * ((100 + BonusRatio) / 100));
                }
                worksheet.Cells[row, 8].Value = (float)Math.Ceiling(Construct * Ratio * ((100 + BonusRatio) / 100));
                row++;
            }

            if (HasDelivery is true)                //если требуется указать доставку отдельной строкой
            {
                worksheet.Cells[row, 5].Value = "Доставка";
                worksheet.Cells[row, 5].Style.Font.Bold = true;
                worksheet.Cells[row, 6].Value = DeliveryRatio;
                worksheet.Cells[row, 7].Value = Delivery * Ratio;
                worksheet.Cells[row, 8].Value = DeliveryRatio * Delivery * Ratio;
                row++;
                worksheet.Cells[row + 4, 2].Value = $"Доставка силами Исполнителя по адресу: {Adress.Text}.";

            }
            else
            {
                worksheet.Cells[row + 4, 2].Value = "Самовывоз со склада Исполнителя по адресу: Ленинградская область, Всеволожский район, " +
                    "Колтуши, деревня Мяглово, ул. Дорожная, уч. 4Б.";
            }
            worksheet.Cells[row + 4, 2].Style.WrapText = true;

            //приводим float-значения типа 0,699999993 к формату 0,7
            foreach (var cell in worksheet.Cells[8, 2, row, 2])
                if (cell.Value != null && $"{cell.Value}".Contains("0,7") || $"{cell.Value}".Contains("0,8") || $"{cell.Value}".Contains("1,2") || $"{cell.Value}".Contains("3,2"))
                    cell.Style.Numberformat.Format = "0.0";

            //вычисляем итоговую сумму КП и оформляем соответствующим образом
            worksheet.Cells[row, 7].Value = IsAgent ? "ИТОГО:" : "ИТОГО с НДС:";
            worksheet.Cells[row, 7].Style.Font.Bold = true;
            worksheet.Cells[row, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Names.Add("totalOrder", worksheet.Cells[8, 8, row - 1, 8]);
            worksheet.Cells[row, 8].Formula = "=SUM(totalOrder)";       //вводим формулу в ячейку общей стоимости
            worksheet.Cells[row, 8].Calculate();                        //считаем значение по введенной формуле 
            Result = Parser($"{worksheet.Cells[row, 8].Value}");        //показываем полученный результат пользователю
            worksheet.Cells[row, 8].Style.Font.Bold = true;
            worksheet.Cells[row, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[row, 1, row, 8].Style.Border.BorderAround(ExcelBorderStyle.Medium);
            worksheet.Cells[8, 7, row, 8].Style.Numberformat.Format = "#,##0.00";

            // Собираем информацию о материале со всех заготовок
            var allTypeDetails = DetailControls
                .SelectMany(dc => dc.TypeDetailControls)
                .ToList();

            bool allFromExecutor = allTypeDetails.All(t => t.HasMetal);
            bool allFromCustomer = allTypeDetails.All(t => !t.HasMetal);
            bool mixedSituation = !allFromExecutor && !allFromCustomer;

            // Проверяем наличие алюминиевых листов от исполнителя
            bool hasAluminumSheets = allTypeDetails.Any(t =>
                t.TypeDetailDrop?.Text == "Лист металла" &&
                t.HasMetal &&
                t.MetalDrop?.Text != null &&
                (t.MetalDrop.Text.Contains("амг2", StringComparison.OrdinalIgnoreCase) ||
                 t.MetalDrop.Text.Contains("амг5", StringComparison.OrdinalIgnoreCase) ||
                 t.MetalDrop.Text.Contains("амг6", StringComparison.OrdinalIgnoreCase) ||
                 t.MetalDrop.Text.Contains("д16АТ", StringComparison.OrdinalIgnoreCase) ||
                 t.MetalDrop.Text.Contains("д16АМ", StringComparison.OrdinalIgnoreCase)));

            string aluminumWarning = hasAluminumSheets ? " (возможны неглубокие царапины от обрезков)" : "";

            worksheet.Cells[row + 1, 1].Value = "Материал:";
            worksheet.Cells[row + 1, 1, row + 1, 4].Style.VerticalAlignment = ExcelVerticalAlignment.Top;

            if (allFromExecutor)
            {
                worksheet.Cells[row + 1, 2].Value = "Исполнителя" + aluminumWarning;
            }
            else if (allFromCustomer)
            {
                worksheet.Cells[row + 1, 2].Value = "Заказчика";
            }
            else // mixedSituation
            {
                worksheet.Cells[row + 1, 2].Value = "Частично заказчика, частично исполнителя" + aluminumWarning;
            }

            worksheet.Cells[row + 1, 2].Style.Font.Bold = true;
            worksheet.Cells[row + 1, 2, row + 1, 3].Merge = true;
            worksheet.Cells[row + 1, 2].Style.WrapText = true;
            worksheet.Row(row + 1).Height = 30;

            // Предупреждение показываем, если есть хоть один давальческий материал
            if (!allFromExecutor)
            {
                string warningText = mixedSituation
                    ? "Внимание: часть материала давальческий. Остатки давальческого материала забираются вместе с заказом, иначе эти остатки утилизируются!"
                    : "Внимание: остатки давальческого материала забираются вместе с заказом, иначе эти остатки утилизируются!";

                worksheet.Cells[row + 1, 4].Value = warningText;
                worksheet.Cells[row + 1, 4, row + 1, 8].Merge = true;
                worksheet.Cells[row + 1, 4].Style.WrapText = true;
            }

            worksheet.Cells[row + 2, 1].Value = "Срок изготовления:";
            worksheet.Cells[row + 2, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[row + 2, 2].Value = DateProduction.Text + " раб/дней.";
            worksheet.Cells[row + 2, 2].Style.Font.Bold = true;
            worksheet.Cells[row + 2, 2, row + 2, 3].Merge = true;

            if (HasAssembly)
            {
                worksheet.Cells[row + 2, 4].Value = "ЭКСПРЕСС";
                worksheet.Cells[row + 2, 4].Style.Font.Bold = true;
            }

            worksheet.Cells[row + 3, 1].Value = "Условия оплаты:";
            worksheet.Cells[row + 3, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[row + 3, 2].Value = "Предоплата 100% по счету Исполнителя.";
            worksheet.Cells[row + 3, 2, row + 3, 5].Merge = true;

            worksheet.Cells[row + 4, 1].Value = "Порядок отгрузки:";
            worksheet.Cells[row + 4, 1, row + 4, 2].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[row + 4, 2, row + 4, 8].Merge = true;

            worksheet.Cells[row + 5, 1].Value = "Точность:";
            worksheet.Cells[row + 5, 2].Value = "H14/h14 +-IT 14/2 (резка осуществляется воздухом).";
            worksheet.Cells[row + 5, 2, row + 5, 5].Merge = true;

            //в случае с нарезанными деталями, оформляем расшифровку работ
            if (Parts.Count > 0)
            {
                worksheet.Cells[row + 6, 1].Value = "Расшифровка работ: ";
                var descriptionCell = worksheet.Cells[row + 6, 2];
                var foundOperations = new HashSet<string>(); // Уникальные операции

                // Словарь: ключ (символ или строка) -> расшифровка
                var operations = new Dictionary<string, string>
                {
                    { "Л", "Л - Лазер " },
                    { "Б", "Б - Без лазера " },
                    { "Т", "Т - Труборез " },
                    { "ЛП", "ЛП - Лентопил " },
                    { "Г ", "Г - Гибка " },
                    { "В ", "В - Вальцовка " },
                    { "Р ", "Р - Резьба " },
                    { "З ", "З - Зенковка " },
                    { "Зк ", "Зк - Заклепки " },
                    { "С ", "С - Сверловка " },
                    { "Св ", "Св - Сварка " },
                    { "О ", "О - Окраска " },
                    { "Ц ", "Ц - Цинкование " },
                    { "Ф ", "Ф - Фрезеровка " },
                    { "А ", "А - Аквабластинг " },
                    { "Доп ", "Доп - Дополнительные работы " }
                };

                // Проходим по ячейкам в столбце 4 (столбец D)
                for (int r = 8; r <= row + 8; r++)
                {
                    var cellValue = worksheet.Cells[r, 4].Value?.ToString();
                    if (string.IsNullOrEmpty(cellValue)) continue;

                    foreach (var op in operations)
                        if (cellValue.Contains(op.Key) && !foundOperations.Contains(op.Key))
                            foundOperations.Add(op.Key);
                }

                // Формируем итоговую строку
                descriptionCell.Value = string.Join("", foundOperations.Select(k => operations[k]));

                // Дополнительное уведомление
                if (isAssemblyOffer) descriptionCell.Value += "  Внимание - в КП присутствуют сборочные единицы!";

                // Объединение ячеек
                worksheet.Cells[row + 6, 2, row + 6, 8].Merge = true;
            }

            //получаем строку расшифровки работ, если она есть, для передачи в pdf-файл
            string? descriptionWorks = string.Empty;
            if (worksheet.Cells[row + 6, 2].Value != null) descriptionWorks = worksheet.Cells[row + 6, 2].Value.ToString();

            // === Обновлённое примечание с юридической оговоркой ===
            string disclaimer = "Изделия изготавливаются строго по предоставленным Заказчиком чертежам. " +
                               "Исполнитель не несёт ответственности за корректность конструкторской документации.";

            var userComment = Comment.Text?.Trim();
            string finalNote = string.IsNullOrEmpty(userComment)
                ? disclaimer
                : $"{disclaimer}\n{userComment}";

            worksheet.Cells[row + 7, 1].Value = "Примечание:";
            worksheet.Cells[row + 7, 1].Style.Font.Bold = true;
            worksheet.Cells[row + 7, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[row + 7, 2].Value = finalNote;
            worksheet.Row(row + 7).Height = 45;
            worksheet.Cells[row + 7, 2, row + 7, 8].Merge = true;
            worksheet.Cells[row + 7, 2].Style.WrapText = true;
            worksheet.Cells[row + 7, 2].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            // ======================================================

            worksheet.Cells[row + 8, 1].Value = "Ваш менеджер:";
            worksheet.Cells[row + 8, 2].Value = ManagerDrop.Text;
            worksheet.Cells[row + 8, 2, row + 8, 4].Merge = true;

            worksheet.Cells[row + 8, 8].Value = "версия: " + version;
            worksheet.Cells[row + 8, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            //добавляем столбец с порядковыми номерами
            worksheet.InsertColumn(5, 1);
            int num = 1;
            for (int i = 1; i < row - 7; i++)
            {
                if (worksheet.Cells[i + 7, 8].Value != null && $"{worksheet.Cells[i + 7, 8].Value}" != "")
                {
                    worksheet.Cells[i + 7, 5].Value = num;
                    num++;
                }
            }

            worksheet.Cells[row, 5].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
            worksheet.Cells[6, 5].Value = "№";
            worksheet.Cells[6, 5, 7, 5].Merge = true;
            worksheet.Cells[6, 5, 7, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[6, 5, 7, 5].Style.Fill.BackgroundColor.SetColor(0, IsLaser ? 120 : 255, IsLaser ? 180 : 170, IsLaser ? 255 : 0);

            //оформляем стиль созданной таблицы
            ExcelRange table = worksheet.Cells[6, 1, row - 1, 9];
            table.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            table.Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            table.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            table.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            //оформляем первую строку КП, где указываем название нашей компании и ее телефон
            worksheet.Cells["A1"].Value = IsLaser ? "ЛАЗЕРФЛЕКС тел : (812)509 - 60 - 11" : "ПРОВЭЛД тел:(812)603-45-33";
            worksheet.Cells[1, 1, 1, 9].Merge = true;
            worksheet.Rows[1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            worksheet.Cells[1, 1, 1, 9].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
            worksheet.Cells[1, 1, 1, 9].Style.Border.Bottom.Color.SetColor(0, IsLaser ? 120 : 255, IsLaser ? 180 : 170, IsLaser ? 255 : 0);

            //оформляем вторую строку, где указываем номер КП, контрагента и дату создания
            worksheet.Rows[2].Style.Font.Size = 16;
            worksheet.Rows[2].Style.Font.Bold = true;
            worksheet.Rows[2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells["C2"].Value = "КП № " + Order.Text + " для " + CustomerDrop.Text + " от " + DateTime.Now.ToString("d");
            worksheet.Cells[2, 3, 2, 9].Merge = true;
            worksheet.Cells[2, 3, 2, 9].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
            worksheet.Cells[2, 3, 2, 9].Style.Border.Bottom.Color.SetColor(0, IsLaser ? 120 : 255, IsLaser ? 180 : 170, IsLaser ? 255 : 0);

            //оформляем третью строку с предупреждением
            worksheet.Cells["A3"].Value = "Данный расчет действителен в течении 2-х банковских дней";
            worksheet.Cells[3, 1, 3, 9].Merge = true;
            worksheet.Rows[3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

            if (!IsLaser)       //для Провэлда оформляем две уточняющие строки
            {
                worksheet.Cells["C4"].Value = $"Для изготовления изделия";
                worksheet.Cells["C5"].Value = "понадобятся следующие детали и работы:";
                worksheet.Cells[4, 3, 4, 4].Merge = true;
                worksheet.Cells[5, 3, 5, 6].Merge = true;
                //worksheet.Cells["E4"].Value = ProductName.Text;
                worksheet.Cells["E4"].Style.Font.Bold = true;
            }

            //выравниваем содержимое документа и оформляем нюансы
            worksheet.Cells.AutoFitColumns();

            if (worksheet.Rows[row + 4].Height < 35) worksheet.Rows[row + 4].Height = 35;       //оформляем строку, где указан порядок отгрузки
            if (!DetailControls[0].TypeDetailControls[0].HasMetal
                && worksheet.Rows[row + 1].Height < 35) worksheet.Rows[row + 1].Height = 35;    //оформляем строку, где указано предупреждение об остатках материала
            worksheet.Columns[5].Width = 8;                                                     //оформляем столбец, где указано номер позиции
            if (worksheet.Columns[6].Width < 15) worksheet.Columns[6].Width = 15;               //оформляем столбец, где указано наименование детали
            worksheet.Cells[8, 1, row, 3].Style.WrapText = true;                                //переносим текст при необходимости
            if (IsLaser) worksheet.DeleteRow(4, 2);                                             //удаляем 4 и 5 строки, необходимые только для Провэлда

            //устанавливаем настройки для печати, чтобы сохранение в формате .pdf выводило весь документ по ширине страницы
            worksheet.PrinterSettings.FitToPage = true;
            worksheet.PrinterSettings.FitToWidth = 1;
            worksheet.PrinterSettings.FitToHeight = 0;
            worksheet.PrinterSettings.HorizontalCentered = true;


            //заранее добавляем второй лист "Реестр" для удобства просмотра, но сначала заполняем третий лист "Статистика"
            ExcelWorksheet statsheet = workbook.Workbook.Worksheets.Add("Реестр");


            // ----- таблица статистики по нарезанным деталям (Лист3 - "Статистика") -----

            ExcelRange extable = worksheet.Cells[IsLaser ? 6 : 8, 6, IsLaser ? row - 3 : row - 1, 8];

            ExcelWorksheet scoresheet = workbook.Workbook.Worksheets.Add("Статистика");

            extable.Copy(scoresheet.Cells["A2"]);       //копируем список деталей из листа "КП" в лист "Статистика"

            scoresheet.Cells["A1"].Value = "Наименование";
            scoresheet.Cells["B1"].Value = "Кол-во";
            scoresheet.Cells["C1"].Value = "Цена";
            scoresheet.Cells["D1"].Value = "Вид детали";

            float total = 0;
            for (int i = 0; i < extable.Rows; i++)
            {
                if (float.TryParse($"{scoresheet.Cells[i + 2, 3].Value}", out float p)
                    && float.TryParse($"{scoresheet.Cells[i + 2, 2].Value}", out float c)) total += p * c;
            }
            scoresheet.Cells[extable.Rows + 3, 3].Value = "итого:";
            scoresheet.Cells[extable.Rows + 3, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            scoresheet.Cells[extable.Rows + 3, 3].Style.Font.Bold = scoresheet.Cells[extable.Rows + 3, 4].Style.Font.Bold = true;
            scoresheet.Cells[extable.Rows + 3, 4].Value = Math.Ceiling(total);
            scoresheet.Cells[extable.Rows + 3, 4].Style.Fill.PatternType = ExcelFillStyle.Solid;
            scoresheet.Cells[extable.Rows + 3, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Yellow);

            ExcelRange details = scoresheet.Cells[1, 1, extable.Rows + 1, 3];


            // ----- таблица разбивки цены детали по работам (Лист3 - "Статистика") -----

            //                                  50      51      52      53      54      55        56      57        58      59      60          61          62         63         64       65      66      67         68           69     70
            List<string> _heads = new() { "Материал", "Лазер", "Гиб", "Свар", "Окр", "Резьба", "Зенк", "Сверл", "Вальц", "Допы П", "Допы Л", "Труборез", "Констр", "Доставка", "Фрезер", "Закл", "Аква", "Цинк", "S покр / вес", "цвет", "П" };

            int rowStat = 0;        //счетчик строк всех деталей
            int countProweld = 0;   //счетчик кол-ва деталей для реестра Провэлда

            if (isAssemblyOffer && AssemblyWindow.A.Assemblies.Count > 0)
            {
                int rowAssembly = 2;

                foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
                {
                    scoresheet.Cells[rowAssembly, 8].Value = Math.Round(assembly.WeldPrice / assembly.Count, 2);    //стоимость сварки
                    scoresheet.Cells[rowAssembly, 9].Value = Math.Round(assembly.PaintPrice / assembly.Count, 2);   //стоимость окраски
                    scoresheet.Cells[rowAssembly, 23].Value = Math.Round(assembly.Square, 2);                       //площадь покрытия
                    scoresheet.Cells[rowAssembly, 24].Value = $"{assembly.Ral} {assembly.Structure}";               //цвет
                    scoresheet.Cells[rowAssembly, 25].Value = "П";
                    rowAssembly += assembly.Particles.Count + 1;

                    //если список работ еще не содержит сварку, создаем такую запись, иначе просто добавляем стоимость
                    if (!TempWorksDict.ContainsKey("Сварка")) TempWorksDict["Сварка"] = assembly.WeldPrice;
                    else TempWorksDict["Сварка"] += assembly.WeldPrice;

                    //если список работ еще не содержит окраску в этот цвет, создаем такую запись, иначе просто добавляем стоимость
                    if (!TempWorksDict.ContainsKey($"Окраска в {assembly.Ral} {assembly.Structure}"))
                        TempWorksDict[$"Окраска в {assembly.Ral} {assembly.Structure}"] = assembly.PaintPrice;
                    else TempWorksDict[$"Окраска в {assembly.Ral} {assembly.Structure}"] += assembly.PaintPrice;
                }

                if (LooseParts.Count > 0)
                {
                    rowAssembly++;

                    //сначала заполняем ячейки по каждой детали и работе
                    for (int i = 0; i < LooseParts.Count; i++)
                    {
                        byte[]? bytes = LooseParts[i].ImageBytes;           //получаем изображение детали, если оно есть
                        if (bytes is not null)
                        {
                            Stream? stream = new MemoryStream(bytes);
                            string uniqueName = $"Image_{Guid.NewGuid().ToString("N")[..8]}"; // короткий уникальный ID
                            ExcelPicture pic = scoresheet.Drawings.AddPicture(uniqueName, stream);
                            scoresheet.Row(i + rowAssembly).Height = 32;   //увеличиваем высоту строки, чтобы вмещалось изображение
                            pic.SetSize(32, 32);
                            pic.SetPosition(i + rowAssembly - 1, 5, 3, 5);     //для изображений индекс начинается от нуля (0), для ячеек - от единицы (1)
                        }

                        for (int j = 0; j < 20; j++)        //пробегаемся по ключам от 50 до 70, которые зарезервированы под конкретные работы
                            if (LooseParts[i].PropsDict.ContainsKey(j + 50) && float.TryParse(LooseParts[i].PropsDict[j + 50][0], out float value))
                            {
                                scoresheet.Cells[i + rowAssembly, j + 5].Value = Math.Round(value, 2);

                                //подробности окраски/аква/цинк для каждой детали
                                if (j == 4 || j == 16 || j == 17)
                                {
                                    if (float.TryParse(LooseParts[i].PropsDict[j + 50][1], out float square)) scoresheet.Cells[i + rowAssembly, 23].Value = Math.Round(square, 3);     //площадь покрытия
                                    if (LooseParts[i].PropsDict[j + 50].Count > 2) scoresheet.Cells[i + rowAssembly, 24].Value = LooseParts[i].PropsDict[j + 50][2];                   //цвет
                                }

                                //ставим галочку, если деталь добавлена в работы для Провэлда
                                if (scoresheet.Cells[i + rowAssembly, 25].Value == null && (j >= 3 && j <= 9 || (j >= 15 && j <= 17))) scoresheet.Cells[i + rowAssembly, 25].Value = "П";
                            }

                        if (scoresheet.Cells[i + rowAssembly, 25].Value != null && int.TryParse($"{scoresheet.Cells[i + rowAssembly, 2].Value}", out int _c)) countProweld += _c;
                    }
                }

                rowStat = rowAssembly + LooseParts.Count - 2;
            }
            else if (Parts.Count > 0)
            {
                //сначала заполняем ячейки по каждой нарезанной детали и работе
                for (int i = 0; i < Parts.Count; i++)
                {
                    byte[]? bytes = Parts[i].ImageBytes;            //получаем изображение детали, если оно есть
                    if (bytes is not null)
                    {
                        Stream? stream = new MemoryStream(bytes);
                        string uniqueName = $"Image_{Guid.NewGuid().ToString("N")[..8]}"; // короткий уникальный ID
                        ExcelPicture pic = scoresheet.Drawings.AddPicture(uniqueName, stream);
                        scoresheet.Row(i + 2).Height = 32;          //увеличиваем высоту строки, чтобы вмещалось изображение
                        pic.SetSize(32, 32);
                        pic.SetPosition(i + 1, 5, 3, 5);            //для изображений индекс начинается от нуля (0), для ячеек - от единицы (1)
                    }

                    for (int j = 0; j < 20; j++)        //пробегаемся по ключам от 50 до 70, которые зарезервированы под конкретные работы
                        if (Parts[i].PropsDict.ContainsKey(j + 50) && float.TryParse(Parts[i].PropsDict[j + 50][0], out float value))
                        {
                            scoresheet.Cells[i + 2, j + 5].Value = Math.Round(value, 2);

                            //подробности окраски/аква/цинк для каждой детали
                            if (j == 4 || j == 16 || j == 17)
                            {
                                //площадь покрытия
                                if (float.TryParse(Parts[i].PropsDict[j + 50][1], out float square)) scoresheet.Cells[i + 2, 23].Value = Math.Round(square, 3);
                                //цвет и структура                     
                                if (Parts[i].PropsDict[j + 50].Count > 2) scoresheet.Cells[i + 2, 24].Value = Parts[i].PropsDict[j + 50][2];
                            }

                            //ставим галочку, если деталь добавлена в работы для Провэлда
                            if (scoresheet.Cells[i + 2, 25].Value == null && (j >= 3 && j <= 9 || (j >= 15 && j <= 17))) scoresheet.Cells[i + 2, 25].Value = "П";
                        }

                    if (scoresheet.Cells[i + 2, 25].Value != null && int.TryParse($"{scoresheet.Cells[i + 2, 2].Value}", out int _c)) countProweld += _c;
                }

                rowStat = Parts.Count;
            }

            var looseDetails = DetailControls.Where(d => !d.Detail.IsComplect).ToList();
            if (looseDetails.Count > 0)
            {
                foreach (DetailControl det in looseDetails)
                {
                    if (det.Detail.Count <= 0) continue;

                    //считаем материал всех заготовок детали
                    scoresheet.Cells[rowStat + 2, 5].Value = Math.Round(det.TypeDetailControls.Sum(t => t.Result) / det.Detail.Count, 2);

                    //затем считаем стоимость однотипных работ
                    var works = det.TypeDetailControls.SelectMany(t => t.WorkControls).GroupBy(w => w.workType);

                    foreach (var work in works)
                    {
                        double result = Math.Round(work.Sum(w => w.Result) / det.Detail.Count, 2);

                        switch (work.Key)
                        {
                            case CutControl:
                                scoresheet.Cells[rowStat + 2, 6].Value = result;
                                break;
                            case BendControl:
                                scoresheet.Cells[rowStat + 2, 7].Value = result;
                                break;
                            case WeldControl:
                                scoresheet.Cells[rowStat + 2, 8].Value = result;
                                break;
                            case PaintControl:
                                scoresheet.Cells[rowStat + 2, 9].Value = result;
                                break;
                            case ThreadControl thread:
                                if (thread.CharName == "Р") scoresheet.Cells[rowStat + 2, 10].Value = result;
                                else if (thread.CharName == "З") scoresheet.Cells[rowStat + 2, 11].Value = result;
                                else if (thread.CharName == "С") scoresheet.Cells[rowStat + 2, 12].Value = result;
                                else if (thread.CharName == "Зк") scoresheet.Cells[rowStat + 2, 20].Value = result;
                                break;
                            case RollingControl:
                                scoresheet.Cells[rowStat + 2, 13].Value = result;
                                break;
                            case ExtraControl extra:
                                if (extra.work.WorkDrop.Text == "Доп работа П") scoresheet.Cells[rowStat + 2, 14].Value = result;
                                else if (extra.work.WorkDrop.Text == "Доп работа Л") scoresheet.Cells[rowStat + 2, 15].Value = result;
                                break;
                            case PipeControl:
                                scoresheet.Cells[rowStat + 2, 16].Value = result;
                                break;
                            case MillingTotalControl:
                                scoresheet.Cells[rowStat + 2, 19].Value = result;
                                break;
                            case AquaControl:
                                scoresheet.Cells[rowStat + 2, 21].Value = result;
                                break;
                            case ZincControl:
                                scoresheet.Cells[rowStat + 2, 22].Value = result;
                                break;
                        }
                    }

                    rowStat++;
                }
            }

            //учитываем покупные изделия
            if (ProductModel.Product.Baskets?.Count > 0) rowStat += ProductModel.Product.Baskets.Count + 1;

            if (HasDelivery is true)
            {
                rowStat++;
                scoresheet.Cells[rowStat + 1, 18].Value = scoresheet.Cells[rowStat + 1, 3].Value;
            }

            //оформляем заголовки таблицы и подсчитываем общую стоимость и количество деталей для каждой работы   
            float workTotal = 0, workCount = 0;

            for (int col = 0; col < _heads.Count; col++)
            {
                scoresheet.Cells[1, col + 5].Value = _heads[col];       //заполняем заголовки из списка
                if (col + 5 > 22) continue;

                for (int i = 0; i < rowStat; i++)       //пробегаем по каждой детали и получаем стоимость работы с учетом количества деталей
                {
                    if (float.TryParse($"{scoresheet.Cells[i + 2, col + 5].Value}", out float w)    //кусочек цены работы за 1 шт
                        && float.TryParse($"{scoresheet.Cells[i + 2, 2].Value}", out float c))      //количество деталей
                    {
                        workTotal += w * c;
                        workCount += c;
                    }
                }
                scoresheet.Cells[rowStat + 2, col + 5].Value = workCount;                   //получаем общее количество деталей, участвующих в работе
                scoresheet.Cells[rowStat + 3, col + 5].Value = Math.Ceiling(workTotal);     //получаем общую стоимость работы
                workTotal = workCount = 0;                      //обнуляем переменные для следующей работы
            }

            scoresheet.Cells[rowStat + 2, 25].Value = countProweld;

            scoresheet.Cells[extable.Rows + 2, 1].Value = "общее кол-во:";
            scoresheet.Cells[extable.Rows + 2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            scoresheet.Names.Add("totalDetails", scoresheet.Cells[2, 2, extable.Rows + 1, 2]);
            scoresheet.Cells[extable.Rows + 2, 2].Formula = "=SUM(totalDetails)";
            scoresheet.Cells[extable.Rows + 2, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            scoresheet.Cells[extable.Rows + 2, 1].Style.Font.Bold = scoresheet.Cells[extable.Rows + 2, 2].Style.Font.Bold = true;

            ExcelRange totals = scoresheet.Cells[rowStat + 2, 5, rowStat + 3, 25];
            totals.Style.Fill.SetBackground(System.Drawing.Color.PowderBlue);

            ExcelRange sends = scoresheet.Cells[1, 5, rowStat + 3, 25];
            sends.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ExcelRange materials = scoresheet.Cells[1, 5, extable.Rows + 1, 5];

            // ----- таблица общих сумм работ, выполняемых подразделениями (Лист2 - "Реестр") -----

            int rowTask = 2;

            List<string> _headersBitrix = new()
            {
                "№ заказа", "Заказчик", "Менеджер", "Количество материала", "Лазерные работы", "Труборез",
                "Гибочные работы", "Время лазерных работ", "Производство", "Нанесение покрытий",
                "Логистика", "Комментарий", "Дата сдачи", "Время фрезерных работ"
            };
            for (int col = 0; col < _headersBitrix.Count; col++)
            {
                statsheet.Cells[1, col + 7].Value = _headersBitrix[col];
                statsheet.Cells[1, col + 7].Style.WrapText = true;
            }

            statsheet.Cells[1, 2].Value = "ПРОВЭЛД";
            statsheet.Cells[1, 2, 1, 3].Merge = true;
            statsheet.Cells[1, 4].Value = "ЛАЗЕРФЛЕКС";
            statsheet.Cells[1, 4, 1, 5].Merge = true;
            statsheet.Cells[2, 2].Value = statsheet.Cells[2, 4].Value = "Работы";
            statsheet.Cells[2, 3].Value = statsheet.Cells[2, 5].Value = "Стоимость";

            int las = 0, pr = 0;        //количество видов работ Лазерфлекс / Провэлд

            if (TempWorksDict.Count > 0)
                foreach (string key in TempWorksDict.Keys)
                {
                    if (TempWorksDict[key] > 0 && (key == "Лазерная резка" || key == "Гибка" || key == "Труборез" || key == "Фрезеровка" || key.Contains("(Л)")))
                    {
                        statsheet.Cells[3 + las, 4].Value = key;
                        statsheet.Cells[3 + las, 5].Value = Math.Round(TempWorksDict[key], 2);
                        las++;
                    }
                    else if (TempWorksDict[key] > 0)
                    {
                        statsheet.Cells[3 + pr, 2].Value = key;
                        statsheet.Cells[3 + pr, 3].Value = Math.Round(TempWorksDict[key], 2);
                        pr++;
                    }
                }

            int temp = pr >= las ? pr : las;     //ограничиваем таблицу тем количеством строк, которых получилось больше

            ExcelRange restable = statsheet.Cells[1, 2, 3 + temp, 5];
            restable.Style.Fill.PatternType = ExcelFillStyle.Solid;
            statsheet.Cells[1, 2, 3 + temp, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGoldenrodYellow);
            statsheet.Cells[1, 4, 3 + temp, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCyan);
            statsheet.Names.Add("totalProweld", statsheet.Cells[3, 3, 2 + temp, 3]);
            statsheet.Cells[3 + temp, 3].Formula = "=SUM(totalProweld)";
            statsheet.Names.Add("totalLaserflex", statsheet.Cells[3, 5, 2 + temp, 5]);
            statsheet.Cells[3 + temp, 5].Formula = "=SUM(totalLaserflex)";
            statsheet.Cells[3 + temp, 3].Style.Font.Bold = statsheet.Cells[3 + temp, 5].Style.Font.Bold = true;


            // ----- таблица стоимости материала, доставки и конструкторских работ (Лист2 - "Реестр") -----

            ExcelRange material = statsheet.Cells[5 + temp, 2, 7 + temp, 3];
            material.Style.Fill.PatternType = ExcelFillStyle.Solid;
            material.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Lavender);
            statsheet.Cells[5 + temp, 2].Value = "Материал:";
            statsheet.Cells[5 + temp, 3].Value = Math.Round(GetMaterial(), 2);
            statsheet.Cells[6 + temp, 2].Value = "Доставка:";
            statsheet.Cells[6 + temp, 3].Value = Delivery * DeliveryRatio;
            statsheet.Cells[7 + temp, 2].Value = "Конструктор:";
            statsheet.Cells[7 + temp, 3].Value = Construct;

            int countTypeDetails = DetailControls.Sum(t => t.TypeDetailControls.Count) + AssemblyWindow.A.Assemblies.Count;
            temp = 7 + temp >= countTypeDetails ? 7 + temp : countTypeDetails;


            // ----- реестр Лазерфлекс (Лист2 - "Реестр") -----
            int beginL = temp += 3;

            List<string> _headersL = new()
            {
                "№ заказа", "Заказчик", "Менеджер", "Материал", "V",
                "Гибка", "V", "Доп работы", "V", "Комментарий", "Дата сдачи", "Лазер (время работ)",
                "Гибка (время работ)", "Количество материала", "Номер КП", "Статус", "Комментарий менеджера", "КК", "ПК"
            };
            for (int col = 0; col < _headersL.Count; col++)
            {
                statsheet.Cells[temp, col + 1].Value = _headersL[col];
                statsheet.Cells[temp, col + 1].Style.WrapText = true;
            }
            temp++;

            float _lkk, _lpk, _bkk, _bpk;          //счетчики коэффициентов лазера и гибки
            _lkk = _lpk = _bkk = _bpk = 1;

            float _lc, _bc;                        //счетчики количества работ лазера и гибки
            _lc = _bc = 0;


            // ----- параллельно с реестром заполняем давальческую накладную (Лист4 - "Накладная") -----
            using var templatebook = new ExcelPackage(new FileInfo("template.xlsx"));
            ExcelWorksheet notesheet = workbook.Workbook.Worksheets.Add("Накладная", templatebook.Workbook.Worksheets[0]);
            notesheet.Cells[3, 3].Value = notesheet.Cells[3, 8].Value = Order.Text;
            notesheet.Cells[5, 2].Value = notesheet.Cells[5, 7].Value = IsLaser ? "ЛАЗЕРФЛЕКС" : "ПРОВЭЛД";
            notesheet.Cells[6, 2].Value = notesheet.Cells[6, 7].Value = CustomerDrop.Text;
            int tempNote = 9;       //строка, с которой начинаем заполнение

            foreach (DetailControl det in DetailControls)
            {
                for (int i = 0; i < det.TypeDetailControls.Count; i++)
                {
                    TypeDetailControl type = det.TypeDetailControls[i];

                    statsheet.Cells[i + temp, 2].Value = statsheet.Cells[i + rowTask, 8].Value = CustomerDrop.Text; //"Заказчик"
                    statsheet.Cells[i + temp, 3].Value = statsheet.Cells[i + rowTask, 9].Value = ShortManager();    //"Менеджер"

                    if (HasDelivery != false)
                    {
                        statsheet.Cells[i + temp, 8].Value = statsheet.Cells[i + rowTask, 17].Value = "Доставка ";  //"Логистика"
                    }

                    if (type.CheckMetal.IsChecked == false) statsheet.Cells[i + temp, 10].Value = statsheet.Cells[i + rowTask, 18].Value = "Давальч. ";
                    if (type.Comment != null && type.Comment != "")                                                 //"Комментарий"
                    {
                        statsheet.Cells[i + temp, 10].Value += $"{type.Comment}";
                        statsheet.Cells[i + rowTask, 18].Value += $"{type.Comment}";
                    }

                    statsheet.Cells[i + temp, 11].Value = statsheet.Cells[i + rowTask, 19].Value = EndDate();       //"Дата сдачи"
                    statsheet.Cells[i + temp, 11].Style.Numberformat.Format = statsheet.Cells[i + rowTask, 19].Style.Numberformat.Format = "d MMM";

                    statsheet.Cells[i + temp, 15].Value = Order.Text;       //"Номер КП"

                    if (type.MetalDrop.SelectedItem is Metal met)           //"Количество материала и (его цена за 1 кг)"
                    {
                        double _mass = Math.Ceiling(det.Detail.IsComplect ? type.Mass : type.Mass * type.Count);

                        statsheet.Cells[i + temp, 14].Value = $"{_mass}" +
                            $" ({(type.CheckMetal.IsChecked == true ? (type.ExtraResult > 0 ? Math.Ceiling(type.ExtraResult / _mass) :
                            Math.Ceiling(type.Price)) : 0)}р)";

                        statsheet.Cells[i + rowTask, 10].Value = _mass; //"Количество материала"
                    }

                    foreach (WorkControl w in type.WorkControls)            //анализируем работы каждой типовой детали
                    {
                        if (w.Result == 0) continue;                        //пропускаем добавление нулевых работ

                        if (w.workType is CutControl cut)
                        {
                            //"Толщина и марка металла"
                            string description = "";
                            if ((type.MetalDrop.Text.Contains("ст") && type.S >= 3) || (type.MetalDrop.Text.Contains("хк") && type.S < 3)) description = $"s{type.S}";
                            else if (type.MetalDrop.Text.Contains("амг2")) description = $"al{type.S}";
                            else if (type.MetalDrop.Text.Contains("амг") || type.MetalDrop.Text.Contains("д16")) description = $"al{type.S} {type.MetalDrop.Text}";
                            else if (type.MetalDrop.Text.Contains("латунь")) description = $"br{type.S}";
                            else if (type.MetalDrop.Text.Contains("медь")) description = $"cu{type.S}";
                            else description = $"s{type.S} {type.MetalDrop.Text}";

                            //добавляем тэг рифленки при необходимости
                            if (cut.IsGrooved) description += " рифл";

                            //"Лазер (время работ)"                                 //"Время лазерных работ"
                            statsheet.Cells[i + temp, 12].Value = statsheet.Cells[i + rowTask, 14].Value = Math.Ceiling(w.Result * 0.012f / w.Ratio);

                            if (w.Ratio != 1) _lkk += w.Ratio;
                            if (w.TechRatio > 1) _lpk += w.TechRatio;
                            _lc++;

                            if (type.CheckMetal.IsChecked is not null)     //если материал давальческий, добавляем его в накладную
                            {
                                if (cut.Items?.Count > 0)
                                {
                                    var _items = cut.Items?.GroupBy(c => c.sheetSize);      //группируем все листы по размеру
                                    if (_items is not null)
                                        foreach (var item in _items)    //каждую группу листов одного размера и их количество записываем в одну строку
                                        {
                                            notesheet.Cells[tempNote, 2].Value = notesheet.Cells[tempNote, 7].Value = $"Лист {description} ({item.Key})";
                                            notesheet.Cells[tempNote, 3].Value = notesheet.Cells[tempNote, 8].Value = item.Sum(s => s.sheets);
                                            tempNote++;
                                        }
                                }
                                else
                                {
                                    notesheet.Cells[tempNote, 2].Value = notesheet.Cells[tempNote, 7].Value = $"Лист {description} ({type.A}x{type.B})";
                                    notesheet.Cells[tempNote, 3].Value = notesheet.Cells[tempNote, 8].Value = type.Count;
                                    tempNote++;
                                }
                            }

                            //добавляем тег срочности и коментария
                            if (HasAssembly) description += " (ЭКСПРЕСС)";
                            if (type.Comment != null && type.Comment != "") description += " (комментарий)";

                            statsheet.Cells[i + temp, 4].Value = statsheet.Cells[i + rowTask, 11].Value = description;  //"Лазерные работы"
                        }
                        else if (w.workType is BendControl)
                        {
                            statsheet.Cells[i + temp, 6].Value = "гибка";
                            //"Гибка (время работ)"                                 //"Гибочные работы"
                            statsheet.Cells[i + temp, 13].Value = statsheet.Cells[i + rowTask, 13].Value = Math.Ceiling(w.Result * 0.018f / w.Ratio);

                            if (w.Ratio != 1) _bkk += w.Ratio;
                            if (w.TechRatio > 1) _bpk += w.TechRatio;
                            _bc++;
                        }
                        else if (w.workType is PipeControl pipe)
                        {
                            //"Толщина и марка металла"                             //"Труборез"
                            statsheet.Cells[i + temp, 4].Value = statsheet.Cells[i + rowTask, 12].Value = $"(ТР) {type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text}";
                            if (HasAssembly) statsheet.Cells[i + rowTask, 12].Value += " (ЭКСПРЕСС)";
                            if (type.Comment != null && type.Comment != "") statsheet.Cells[i + rowTask, 12].Value += " (комментарий)";

                            //"Лазер (время работ)"                                 //"Время лазерных работ"
                            statsheet.Cells[i + temp, 12].Value = statsheet.Cells[i + rowTask, 14].Value = Math.Ceiling(w.Result * 0.012f / w.Ratio);

                            if (type.CheckMetal.IsChecked is not null)
                            {
                                if (pipe.Items?.Count > 0)
                                {
                                    var _items = pipe.Items?.GroupBy(c => c.sheetSize);      //группируем все трубы по размеру
                                    if (_items is not null)
                                        foreach (var item in _items)    //каждую группу труб одного размера и их количество записываем в одну строку
                                        {
                                            notesheet.Cells[tempNote, 2].Value = notesheet.Cells[tempNote, 7].Value = $"{type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text} ({item.Key})";
                                            notesheet.Cells[tempNote, 3].Value = notesheet.Cells[tempNote, 8].Value = item.Sum(s => s.sheets);
                                            tempNote++;
                                        }
                                }
                                else
                                {
                                    notesheet.Cells[tempNote, 2].Value = notesheet.Cells[tempNote, 7].Value = $"{type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text} ({type.L})";
                                    notesheet.Cells[tempNote, 3].Value = notesheet.Cells[tempNote, 8].Value = type.Count;
                                    tempNote++;
                                }
                            }
                        }
                        else if (w.workType is SawControl _saw)         //для лентопила указываем вид заготовки по аналогии с труборезом
                        {
                            //"Толщина и марка металла"                             //"Труборез"
                            statsheet.Cells[i + temp, 4].Value = statsheet.Cells[i + rowTask, 12].Value = $"(ЛП) {type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text}";
                            //"Лазер (время работ)"                                 //"Время лазерных работ"
                            statsheet.Cells[i + temp, 12].Value = statsheet.Cells[i + rowTask, 14].Value = Math.Ceiling(w.Result * 0.018f / w.Ratio);

                            notesheet.Cells[tempNote, 2].Value = notesheet.Cells[tempNote, 7].Value = $"{type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text} ({type.L})";
                            notesheet.Cells[tempNote, 3].Value = notesheet.Cells[tempNote, 8].Value = type.Count;
                            tempNote++;
                        }
                        else if (w.workType is ExtraControl _extra)     //для доп работы её наименование добавляем к наименованию работы - особый случай
                        {
                            statsheet.Cells[i + temp, 8].Value += $"{_extra.NameExtra} ";
                            statsheet.Cells[i + rowTask, 15].Value += $"{_extra.NameExtra} ";       //"Производство"
                        }
                        else if (w.workType is MillingTotalControl _milling)
                        {
                            statsheet.Cells[i + rowTask, 20].Value = _milling.TotalTime;            //"Время фрезерных работ"
                        }
                        else if (w.WorkDrop.SelectedItem is Work work)
                        {
                            statsheet.Cells[i + temp, 8].Value += $"{work.Name} ";                  //"Доп работы"

                            if (w.workType is PaintControl _paint)                                  //"Нанесение покрытий"
                                statsheet.Cells[i + rowTask, 16].Value += $"{_paint.Ral} {_paint.TypeDrop.SelectedItem} ";
                            else statsheet.Cells[i + rowTask, 15].Value += $"{work.Name} ";         //"Производство"
                        }

                        //проверяем наличие коэффициентов
                        if (w.Ratio != 1)
                            if (float.TryParse($"{statsheet.Cells[i + temp, 16].Value}", out float r))
                                statsheet.Cells[i + temp, 18].Value = Math.Round(r * w.Ratio, 2);
                            else statsheet.Cells[i + temp, 18].Value = Math.Round(w.Ratio, 2);
                        if (w.TechRatio > 1)
                            if (float.TryParse($"{statsheet.Cells[i + temp, 17].Value}", out float r))
                                statsheet.Cells[i + temp, 19].Value = Math.Round(r * w.TechRatio, 2);
                            else statsheet.Cells[i + temp, 19].Value = Math.Round(w.TechRatio, 2);
                    }
                }
                temp += det.TypeDetailControls.Count;
                rowTask += det.TypeDetailControls.Count;
            }

            if (isAssemblyOffer && AssemblyWindow.A.Assemblies.Count > 0)
            {
                foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
                {
                    if (assembly.WeldPrice == 0 && assembly.PaintPrice == 0) continue;

                    statsheet.Cells[rowTask, 8].Value = CustomerDrop.Text;  //"Заказчик"
                    statsheet.Cells[rowTask, 9].Value = ShortManager();     //"Менеджер"
                    statsheet.Cells[rowTask, 10].Value = assembly.Count;    //"Кол-во"
                    statsheet.Cells[rowTask, 19].Value = EndDate();         //"Дата сдачи"
                    statsheet.Cells[rowTask, 19].Style.Numberformat.Format = "d MMM";

                    if (assembly.WeldPrice > 0)         //"Производство"
                    {
                        statsheet.Cells[rowTask, 15].Value = "Сварка (сборка)";
                    }

                    if (assembly.PaintPrice > 0)        //"Нанесение покрытий" и "Комментарий"
                    {
                        statsheet.Cells[rowTask, 16].Value = $"{assembly.Ral} {assembly.Structure}";
                        statsheet.Cells[rowTask, 18].Value +=
                            $"Окраска в {assembly.Ral} {assembly.Structure} " +
                            $"({Math.Round(assembly.Square, 2)} кв м - {assembly.Count} шт) ";
                    }
                    rowTask++;
                }
            }

            ExcelRange registryBitrix = statsheet.Cells[1, 7, rowTask - 1, 20];
            registryBitrix.Style.Fill.PatternType = ExcelFillStyle.Solid;
            registryBitrix.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LavenderBlush);
            statsheet.Row(1).Style.Font.Bold = true;

            for (int n = 1; n <= tempNote - 9; n++) notesheet.Cells[n + 8, 1].Value = notesheet.Cells[n + 8, 6].Value = n;
            worksheet.Select();

            ExcelRange registryL = statsheet.Cells[beginL, 1, temp - 1, 19];
            statsheet.Cells[beginL, 4, temp - 1, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;      //"Толщина и марка металла"
            statsheet.Cells[beginL, 11, temp - 1, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;    //"Дата сдачи"
            statsheet.Cells[beginL, 15, temp - 1, 19].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;     //"Номер КП"

            if (_lkk != 1) statsheet.Cells[temp, 12].Value = $"КК-{_lkk / _lc}";
            if (_lpk > 1) statsheet.Cells[temp + 1, 12].Value = $"ПК-{_lpk / _lc}";
            if (_bkk != 1) statsheet.Cells[temp, 13].Value = $"КК-{_bkk / _bc}";
            if (_bpk > 1) statsheet.Cells[temp + 1, 13].Value = $"ПК-{_bpk / _bc}";
            if (Ratio != 1) statsheet.Cells[temp, 15].Value = $"ОК-{Ratio}";
            if (Bonus > 0) statsheet.Cells[temp, 15].Value += $" Бонус-{Math.Ceiling(Bonus)} р";


            // ----- реестр Провэлд (Лист2 - "Реестр") -----

            int beginP = temp += 3;

            List<string> _headersP = new()
            {
                "Дата", "№ п/п", "№ Проекта / Лазера", "Наименование изделия / вид работы", "Кол-во",
                "ед изм.", "Подразделение", "Компания", "Мастер", "Менеджер", "Инженер", "Время работ, мин",
                "Дата отгрузки", "Готово к отгрузке", "Отгружено", "Готово \"V\"", "Цвет/цинк",
                "Примечание", "Ход проекта", "ОТГРУЗКИ _ дата и количество", "Стоимость работ"
            };
            for (int col = 0; col < _headersP.Count; col++)
            {
                statsheet.Cells[temp, col + 1].Value = _headersP[col];
                statsheet.Cells[temp, col + 1].Style.WrapText = true;
            }
            temp++;

            if (TempWorksDict.Count > 0)
            {
                float sum = 0;        //счетчик общей стоимости работ Провэлда

                foreach (string key in TempWorksDict.Keys)
                {
                    if (TempWorksDict[key] == 0 || key == "Лазерная резка" || key == "Гибка" || key == "Труборез" || key == "Фрезеровка" || key.Contains("(Л)")) continue;

                    if (key.Contains("Цинк") && !key.Contains("Окраска"))   //уточнение "...&& !key.Contains("Окраска")" нужно, чтобы исключить цвет "цинко-грунт"
                    {
                        statsheet.Cells[temp, 4].Value += $"{key} ";

                        int _count = 0;
                        float _square = 0;

                        for (int i = 0; i < Parts.Count; i++)
                        {
                            //если в столбце "Цинк" не пусто, считаем кол-во и вес таких деталей
                            if ($"{scoresheet.Cells[i + 2, 22].Value}" != "")
                            {
                                _count += (int)Parser($"{scoresheet.Cells[i + 2, 2].Value}");
                                if (float.TryParse($"{scoresheet.Cells[i + 2, 23].Value}", out float s))
                                    _square += s * Parser($"{scoresheet.Cells[i + 2, 2].Value}");
                            }
                        }

                        statsheet.Cells[temp, 17].Value += $"Цинк ({Math.Ceiling(_square)} кг - {_count} шт) ";
                        notesheet.Cells[tempNote, 2].Value = notesheet.Cells[tempNote, 7].Value = $"Цинк ({Math.Ceiling(_square)} кг - {_count} шт) ";
                        notesheet.Cells[tempNote, 3].Value = notesheet.Cells[tempNote, 8].Value = $"{Math.Ceiling(_square)} кг";
                        tempNote++;

                    }
                    else if (key.Contains("Окраска"))
                    {
                        if (!$"{statsheet.Cells[temp, 4].Value}".Contains("Окраска")) statsheet.Cells[temp, 4].Value += "Окраска ";

                        int _count = 0;
                        float _square = 0;

                        if (isAssemblyOffer && AssemblyWindow.A.Assemblies.Count > 0)
                        {
                            foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
                            {
                                //если в столбце "цвет" не пусто, и данная окраска соответствует по RAL, считаем кол-во и площадь таких деталей
                                if ($"{key}".Trim() == $"Окраска в {assembly.Ral} {assembly.Structure}".Trim())
                                {
                                    _count += assembly.Count;
                                    _square += assembly.Square;
                                }
                            }

                            if (LooseParts.Count > 0)
                                for (int i = 0; i < LooseParts.Count; i++)
                                {
                                    //если в столбце "цвет" не пусто, и данная окраска соответствует по RAL, считаем кол-во и площадь таких деталей
                                    if ($"{scoresheet.Cells[i + rowStat - AssemblyWindow.A.Assemblies.Count, 24].Value}" != "" && $"{key[10..]}".Trim() == $"{scoresheet.Cells[i + rowStat - AssemblyWindow.A.Assemblies.Count, 24].Value}".Trim())
                                    {
                                        _count += (int)Parser($"{scoresheet.Cells[i + rowStat - AssemblyWindow.A.Assemblies.Count, 2].Value}");
                                        if (float.TryParse($"{scoresheet.Cells[i + rowStat - AssemblyWindow.A.Assemblies.Count, 23].Value}", out float s))
                                            _square += s * Parser($"{scoresheet.Cells[i + rowStat - AssemblyWindow.A.Assemblies.Count, 2].Value}");
                                    }
                                }
                        }
                        else if (Parts.Count > 0)
                            for (int i = 0; i < Parts.Count; i++)
                            {
                                //если в столбце "цвет" не пусто, и данная окраска соответствует по RAL, считаем кол-во и площадь таких деталей
                                if ($"{scoresheet.Cells[i + 2, 24].Value}" != "" && $"{key[10..]}".Trim() == $"{scoresheet.Cells[i + 2, 24].Value}".Trim())
                                {
                                    _count += (int)Parser($"{scoresheet.Cells[i + 2, 2].Value}");
                                    if (float.TryParse($"{scoresheet.Cells[i + 2, 23].Value}", out float s))
                                        _square += s * Parser($"{scoresheet.Cells[i + 2, 2].Value}");
                                }
                            }

                        statsheet.Cells[temp, 17].Value += $"{key[10..]} ({Math.Round(_square, 3)} кв м - {_count} шт) ";
                        notesheet.Cells[tempNote, 2].Value = notesheet.Cells[tempNote, 7].Value = $"{key[10..]} ({Math.Round(_square, 3)} кв м - {_count} шт) ";
                        notesheet.Cells[tempNote, 3].Value = notesheet.Cells[tempNote, 8].Value = $"{Math.Ceiling(_square * 0.3f)} кг";
                        tempNote++;
                    }
                    else statsheet.Cells[temp, 4].Value += $"{key} ";                                       //"Наименование изделия / вид работы"

                    sum += TempWorksDict[key];
                }

                //дополняем накладную покупными изделиями
                if (ProductModel.Product.Baskets?.Count > 0)
                    foreach (Part basket in ProductModel.Product.Baskets)
                    {
                        notesheet.Cells[tempNote, 2].Value = notesheet.Cells[tempNote, 7].Value = basket.Title;
                        notesheet.Cells[tempNote, 3].Value = notesheet.Cells[tempNote, 8].Value = basket.Count;
                        tempNote++;
                    }

                statsheet.Cells[temp, 3].Value = Order.Text;                                                //"№ Проекта / Лазера"
                statsheet.Cells[temp, 4].Style.WrapText = true;
                statsheet.Cells[temp, 5].Value = isAssemblyOffer ?
                    AssemblyWindow.A.Assemblies.Sum(x => x.Count)
                    : scoresheet.Cells[Parts.Count + 2, 25].Value;                                          //"Кол-во" (изделий/сборок)
                statsheet.Cells[temp, 6].Value = "шт";                                                      //"ед изм."
                statsheet.Cells[temp, 7].Value = IsLaser ? "ЛАЗЕРФЛЕКС" : "ПРОВЭЛД";                        //"Подразделение"
                statsheet.Cells[temp, 8].Value = CustomerDrop.Text;                                         //"Компания"
                statsheet.Cells[temp, 10].Value = ManagerDrop.Text;                                         //"Менеджер"
                statsheet.Cells[temp, 11].Value = CurrentManager.Name;                                      //"Инженер"
                statsheet.Cells[temp, 12].Value = Math.Ceiling(sum * 60 / 2000);                            //"Время работ, мин"
                statsheet.Cells[temp, 13].Value = EndDate();                                                //"Дата отгрузки"
                statsheet.Cells[temp, 13].Style.Numberformat.Format = "dd.mm.yy";

                //по умолчанию "Цвет/цинк" - "БП" (без покраски)
                if ($"{statsheet.Cells[temp, 17].Value}" == "") statsheet.Cells[temp, 17].Value = "БП";
                statsheet.Cells[temp, 17].Style.WrapText = true;                                            //"Цвет/цинк"

                //"Примечание"
                if (isAssemblyOffer)
                    statsheet.Cells[temp, 18].Value = LooseParts.Count > 0 ?
                        $"{AssemblyWindow.A.Assemblies.Sum(x => x.Count)} - изделий, " +
                        $"{scoresheet.Cells[AssemblyWindow.A.Assemblies.Count + AssemblyWindow.A.Assemblies.Sum(p => p.Particles.Count) + LooseParts.Count + 2, 25].Value} - штучных деталей"
                        : $"{AssemblyWindow.A.Assemblies.Sum(x => x.Count)} - изделий";

                statsheet.Cells[temp, 21].Value = Math.Ceiling(sum);                                        //"Стоимость работ"
                statsheet.Cells[temp, 21].Style.Numberformat.Format = "#,##0.00";
            }

            ExcelRange registryP = statsheet.Cells[beginP, 1, temp, 21];
            registryP.Style.Font.Name = "Arial";
            registryP.Style.Font.Size = 10;
            statsheet.Cells[temp, 13].Style.Font.Size = 11;
            statsheet.Cells[temp, 3].Style.Font.Size = statsheet.Cells[temp, 21].Style.Font.Size = 14;
            statsheet.Cells[temp, 1].Style.Font.Bold = statsheet.Cells[temp, 3].Style.Font.Bold = statsheet.Cells[temp, 13].Style.Font.Bold = true;

            // ----- обводка границ и авторастягивание столбцов -----

            details.Style.Border.Bottom.Style = sends.Style.Border.Bottom.Style = restable.Style.Border.Bottom.Style = material.Style.Border.Bottom.Style = registryL.Style.Border.Bottom.Style = registryP.Style.Border.Bottom.Style = registryBitrix.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            details.Style.Border.Right.Style = sends.Style.Border.Right.Style = restable.Style.Border.Right.Style = material.Style.Border.Right.Style = registryL.Style.Border.Right.Style = registryP.Style.Border.Right.Style = registryBitrix.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            details.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            sends.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            restable.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            material.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            registryL.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            registryP.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            registryBitrix.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            registryBitrix.Style.HorizontalAlignment = registryP.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            registryBitrix.Style.VerticalAlignment = registryP.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            scoresheet.Cells.AutoFitColumns();
            statsheet.Cells.AutoFitColumns();


            // ----- сохраняем книгу в файл Excel -----
            workbook.SaveAs(path.Remove(path.LastIndexOf(".")) + ".xlsx");      //сохраняем файл .xlsx

            CreateScore(worksheet, row - 8, path, materials);                   //создаем файл для счета на основе полученного КП
            CreateComplect(path);                                               //создаем файл комплектации    
            OfferPdf offerPdf = new(path[..path.LastIndexOf(".")] + ".pdf", descriptionWorks);
        }

        //-СЧЕТ
        private void CreateScore(ExcelWorksheet worksheet, int row, string _path, ExcelRange materials)
        {
            // ----- основная таблица деталей для экспорта в 1С (Лист1 - "Счет") -----

            ExcelRange extable = worksheet.Cells[IsLaser ? 6 : 8, 6, IsLaser ? row + 5 : row + 7, 8];

            using var workbook = new ExcelPackage();
            ExcelWorksheet scoresheet = workbook.Workbook.Worksheets.Add("Счет");

            extable.Copy(scoresheet.Cells["A2"]);       //копируем список деталей из КП в файл для счета

            scoresheet.Cells["A1"].Value = "Наименование";
            scoresheet.Cells["B1"].Value = "Количество";
            scoresheet.Cells["C1"].Value = "Цена";
            scoresheet.Cells["D1"].Value = "Цена";
            scoresheet.Cells["E1"].Value = "Ед. изм.";

            materials.Copy(scoresheet.Cells["G1"]);     //копируем стоимость материала из КП в файл для счета

            for (int i = 0; i < extable.Rows; i++)
            {
                if (float.TryParse($"{scoresheet.Cells[i + 2, 7].Value}", out float m)        //кусочек цены материала за 1 шт
                   && float.TryParse($"{scoresheet.Cells[i + 2, 2].Value}", out float c))     //количество деталей
                    scoresheet.Cells[i + 2, 6].Value = Math.Round(m * c, 2);
                if (float.TryParse($"{scoresheet.Cells[i + 2, 6].Value}", out float _m) && _m == 0)
                    scoresheet.Cells[i + 2, 6].Value = 1;
            }
            scoresheet.Column(7).Hidden = true;

            for (int i = 0; i < row; i++)
            {
                if (float.TryParse($"{scoresheet.Cells[i + 2, 3].Value}", out float p)) scoresheet.Cells[i + 2, 4].Value = Math.Round(p / 1.17f, 2);
                scoresheet.Cells[i + 2, 5].Value = "шт";
            }

            ExcelRange details = scoresheet.Cells[1, 1, row + 1, 5];

            for (int i = details.Rows; i > 0; i--)      //удаляем строки без цен - детали сборок
            {
                if (scoresheet.Cells[i, 3].Value is null || $"{scoresheet.Cells[i, 3].Value}" == "")
                {
                    scoresheet.DeleteRow(i);
                    row--;
                }
            }
            details = scoresheet.Cells[1, 1, row + 1, 5];

            // ----- обводка границ и авторастягивание столбцов -----

            details.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            details.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            details.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            scoresheet.Cells.AutoFitColumns();

            // ----- сохраняем книгу в файл Excel -----

            string? directory = Path.GetDirectoryName(_path),
                    template = "\\" + "Файл для счета " + Order.Text + "_" + CustomerDrop.Text + " на сумму ";

            if (directory != null)
                foreach (var file in Directory.GetFiles(directory))
                    if (file.Contains(Path.GetDirectoryName(_path) + template)) File.Delete(file);

            workbook.SaveAs(Path.GetDirectoryName(_path) + template + $"{Result}" + ".xlsx");
        }

        private async void CreateOfferDelivery(object sender, RoutedEventArgs e)
        {
            if (!WarningSave()) return;
            SaveProduct();

            // Сохраняем через сервис
            var savedOffer = await DataService.SaveOfferAsync(
                orderNumber: Order.Text,
                companyName: CustomerDrop.Text,
                amount: Result,
                material: GetMaterial(),
                services: GetServices(),
                isAgent: IsAgent,
                autor: CurrentManager.Name,
                null,
                null,
                managerId: TargetManager.Id
            );

            // Обновляем список расчетов в UI
            await LoadManagerDataAsync(TargetManager);

            string statusMessage = DataService.IsOnline
                ? $"Расчет {savedOffer.N} {savedOffer.Company} сохранен на сервере."
                : $"Расчет {savedOffer.N} {savedOffer.Company} сохранен локально (ожидает синхронизации).";

            StatusBegin(statusMessage, StatusMessageType.Success);
        }

        //-КОМПЛЕКТАЦИЯ
        private void CreateComplect(string _path, Offer? offer = null)
        {
            if (offer != null && (offer.Order is null || offer?.Order?.Length != 4))
            {
                StatusBegin($"Данные расчета изменены. Но файл комплектации не создан, так как номер заказа введен некорректно.", StatusMessageType.Warning);
                return;
            }

            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet complectsheet = workbook.Workbook.Worksheets.Add("Комплектация");

            complectsheet.Cells[1, 1, 1, 3].Merge = true;
            complectsheet.Cells[1, 1].Value = offer != null ? offer.Order : Order.Text;     //Номер КП
            complectsheet.Cells[1, 1].Style.Font.Size = 60;
            complectsheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

            complectsheet.Cells[1, 4, 1, 9].Merge = true;
            complectsheet.Cells[1, 4].Value = CustomerDrop.Text;                            //Компания
            complectsheet.Cells[1, 4].Style.Font.Size = 36;
            complectsheet.Cells[1, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            //оформляем первую строку
            complectsheet.Row(1).Style.Font.Bold = true;
            complectsheet.Row(1).Height = 60;

            //устанавливаем заголовки таблицы
            List<string> _heads = new() { "№", "Вид", "Название детали", "Маршрут", "Кол-во", "Размеры детали", "Вес, кг", "Металл", "Толщина", "Факт" };
            for (int head = 0; head < _heads.Count; head++) complectsheet.Cells[2, head + 1].Value = _heads[head];
            complectsheet.Row(2).Style.Font.Bold = true;
            complectsheet.Cells[2, 1, 2, _heads.Count].Style.Fill.SetBackground(System.Drawing.Color.LightGray);

            //параллельно создаем лист с раскладками
            ExcelWorksheet itemsheet = workbook.Workbook.Worksheets.Add("Раскладки");

            // и лист с дополнительными работами
            ExcelWorksheet additionalSheet = workbook.Workbook.Worksheets.Add("Допы");
            additionalSheet.Cells[1, 1].Value = "Работа";
            additionalSheet.Cells[1, 2].Value = "Вид";
            additionalSheet.Cells[1, 3].Value = "Наименование детали";
            additionalSheet.Cells[1, 4].Value = "Кол-во";
            additionalSheet.Cells[1, 5].Value = "Размеры";
            additionalSheet.Row(1).Style.Font.Bold = true;
            additionalSheet.Cells[1, 1, 1, 5].Style.Fill.SetBackground(System.Drawing.Color.LightGray);
            int additionalRow = 2; // начальная строка данных для листа доп работ

            // Словарь: ключ — символ/подстрока в ячейке, значение — расшифровка
            var operationsMap = new Dictionary<string, string>
    {
        { "Л", "Л - Лазер " },
        { "Б", "Б - Без лазера " },
        { "Т", "Т - Труборез " },
        { "ЛП", "ЛП - Лентопил " },
        { "Г", "Г - Гибка " },
        { "В", "В - Вальцовка " },
        { "Р", "Р - Резьба " },
        { "З", "З - Зенковка " },
        { "Зк", "Зк - Заклепки " },
        { "С", "С - Сверловка " },
        { "Св", "Св - Сварка " },
        { "О", "О - Окраска " },
        { "Ц", "Ц - Цинкование " },
        { "Ф", "Ф - Фрезеровка " },
        { "А", "А - Аквабластинг " },
        { "Доп", "Доп - Дополнительные работы " }
    };

            // Цвета для чередования (очень светлые)
            var color1 = System.Drawing.Color.White;
            var color2 = System.Drawing.Color.LightBlue;

            var excludedOps = new HashSet<string> { "Л", "Б", "Т", "ЛП", "Лазерная резка", "Труборез", "Лентопил" };

            // Словарь: операция → список деталей
            var workGroups = new Dictionary<string, List<(string? Name, object Count, byte[]? Bytes, string Dimensions)>>();

            // Общая коллекция деталей, приведенная к анонимному типу для группировки по работам
            var combined = DetailControls.Where(d => !d.Detail.IsComplect)
                .Select(d => new
                {
                    d.Detail.Title,
                    d.Detail.Description,
                    d.Detail.Count,
                    ImageBytes = (byte[]?)null,
                    Dimensions = "" // Detail не имеет размеров
                })
                .Concat(
                    Parts.Select(p => new
                    {
                        p.Title,
                        p.Description,
                        p.Count,
                        p.ImageBytes,
                        Dimensions = GetDimensionsString(p.PropsDict)
                    })
                );
            foreach (var item in combined)
            {
                if (string.IsNullOrWhiteSpace(item.Description))
                    continue;

                var opCodes = item.Description
                    .Split('+')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s) && !excludedOps.Contains(s))
                    .ToList();

                foreach (var opCode in opCodes)
                {
                    string opName = operationsMap.TryGetValue(opCode, out var name) ? name : opCode;

                    if (!workGroups.ContainsKey(opName))
                        workGroups[opName] = new List<(string?, object, byte[]?, string)>();

                    workGroups[opName].Add((item.Title, item.Count, item.ImageBytes, item.Dimensions));
                }
            }

            bool useColor1 = true;

            // Проходим по каждой операции
            foreach (var group in workGroups)
            {
                string operationName = group.Key;
                var parts = group.Value;

                for (int i = 0; i < parts.Count; i++)
                {
                    var (name, count, bytes, dimensions) = parts[i];

                    // Колонка A: название операции — только в первой строке группы
                    if (i == 0)
                        additionalSheet.Cells[additionalRow, 1].Value = operationName;

                    // Колонка C: наименование
                    additionalSheet.Cells[additionalRow, 3].Value = name;

                    // Колонка D: количество
                    additionalSheet.Cells[additionalRow, 4].Value = count;

                    // Колонка E: размеры
                    additionalSheet.Cells[additionalRow, 5].Value = dimensions;

                    // Колонка B: изображение
                    if (bytes != null)
                    {
                        Stream? stream = new MemoryStream(bytes);
                        string uniqueName = $"Image_{Guid.NewGuid().ToString("N")[..8]}";   //короткий уникальный ID
                        ExcelPicture pic_work = additionalSheet.Drawings.AddPicture(uniqueName, stream);
                        pic_work.SetPosition(additionalRow - 1, 5, 1, 5); // строка row (0-based), колонка B (индекс 1)
                        pic_work.SetSize(32, 32);
                        additionalSheet.Row(additionalRow).Height = 32;
                    }

                    // === Стиль фона для всей строки ===
                    additionalSheet.Cells[additionalRow, 1, additionalRow, 5].Style.Fill.SetBackground(useColor1 ? color1 : color2);

                    additionalRow++;
                }

                useColor1 = !useColor1;

                // === Жирная нижняя граница под последней строкой группы ===
                additionalSheet.Cells[additionalRow - 1, 1, additionalRow - 1, 5].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
            }

            if (Delivery > 0)
            {
                additionalSheet.Cells[additionalRow, 1].Value = "Доставка";
                additionalSheet.Cells[additionalRow, 1].Style.Font.Bold = true;
            }

            int temp = 1;               //номер текущей строки
            float _totalMass = 0;       //счетчик общего веса деталей
            int namePic = 0;            //порядковое имя картинки

            foreach (DetailControl det in DetailControls)
            {
                if (det.Detail.IsComplect)
                {
                    foreach (TypeDetailControl type in det.TypeDetailControls)
                        foreach (WorkControl work in type.WorkControls)
                            if (work.workType is ICut cut)
                            {
                                //нарезанные детали
                                if (cut.PartDetails?.Count > 0)
                                    for (int i = 0; i < cut.PartDetails.Count; i++)
                                    {
                                        complectsheet.Cells[temp + 2, 1].Value = temp;  //номер детали по порядку

                                        byte[]? bytes = cut.PartDetails[i].ImageBytes;  //получаем изображение детали, если оно есть
                                        if (bytes is not null)
                                        {
                                            using var stream = new MemoryStream(bytes);
                                            string uniqueName = $"Image_{Guid.NewGuid().ToString("N")[..8]}";   //короткий уникальный ID
                                            ExcelPicture pic = complectsheet.Drawings.AddPicture(uniqueName, stream);

                                            //увеличиваем высоту строки, чтобы вмещалось изображение
                                            complectsheet.Row(temp + 2).Height = 32;

                                            pic.SetSize(32, 32);
                                            pic.SetPosition(temp + 1, 5, 1, 5);     //для изображений индекс начинается от нуля (0), для ячеек - от единицы (1)
                                        }

                                        complectsheet.Cells[temp + 2, 3].Value = cut.PartDetails[i].Title;          //наименование детали
                                        complectsheet.Cells[temp + 2, 4].Value = cut.PartDetails[i].Description;    //маршрут изготовления
                                        complectsheet.Cells[temp + 2, 4].Style.WrapText = true;

                                        complectsheet.Cells[temp + 2, 5].Value = cut.PartDetails[i].Count;          //количество деталей
                                        complectsheet.Cells[temp + 2, 5].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                                        complectsheet.Cells[temp + 2, 5].Style.Font.Bold = true;

                                        if (cut.PartDetails[i].PropsDict.ContainsKey(100) && cut.PartDetails[i].PropsDict[100].Count > 2)   //габаритные размеры детали без отверстий
                                        {
                                            complectsheet.Cells[temp + 2, 6].Value =
                                                cut is CutControl laser ?
                                                $"{cut.PartDetails[i].PropsDict[100][0].Trim()}x{cut.PartDetails[i].PropsDict[100][1].Trim()}"
                                                : $"{cut.PartDetails[i].PropsDict[100][2].Trim()} мм";
                                        }

                                        complectsheet.Cells[temp + 2, 7].Value = Math.Round(cut.PartDetails[i].Mass, 1);     //масса детали
                                        _totalMass += cut.PartDetails[i].Mass * cut.PartDetails[i].Count;                    //дополнительно считаем общий вес

                                        complectsheet.Cells[temp + 2, 8].Value = cut.PartDetails[i].Metal;                   //материал

                                        complectsheet.Cells[temp + 2, 9].Value = cut.PartDetails[i].Destiny;                 //толщина
                                        if ($"{complectsheet.Cells[temp + 1, 9].Value}" != $"{complectsheet.Cells[temp + 2, 9].Value}" || $"{complectsheet.Cells[temp + 1, 8].Value}" != $"{complectsheet.Cells[temp + 2, 8].Value}")
                                            complectsheet.Cells[temp + 1, 1, temp + 1, 10].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                                        else complectsheet.Cells[temp + 1, 1, temp + 1, 10].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

                                        complectsheet.Cells[temp + 2, 10].Style.Font.Color.SetColor(System.Drawing.Color.Red);  //факт
                                        complectsheet.Cells[temp + 2, 10].Style.Font.Bold = true;

                                        //строки с трубами выделяем бледно-розовым цветом
                                        if (cut is PipeControl or SawControl) complectsheet.Cells[temp + 2, 1, temp + 2, 10].Style.Fill.SetBackground(System.Drawing.Color.Linen);

                                        temp++;
                                    }

                                // Раскладки
                                if (cut.Items?.Count > 0)
                                {
                                    foreach (LaserItem item in cut.Items)
                                    {
                                        if (item.NestingSheet != null)
                                        {
                                            // 1. Создаём контрол
                                            var preview = new NestingPreviewControl { Margin = new Thickness(0) };

                                            // 2. Показываем лист (контрол сам пересчитает внутренние размеры Canvas)
                                            preview.ShowSheet(item.NestingSheet);

                                            // 3. ВАЖНО: Получаем реальные размеры всего контрола вместе с подписями
                                            double totalWidth = item.NestingSheet.StockWidth + 50;  // LabelMarginLeft = 50
                                            double totalHeight = item.NestingSheet.StockHeight + 40; // LabelMarginBottom = 40

                                            // 4. Принудительно измеряем и располагаем контрол в этих полных размерах
                                            preview.Measure(new Size(totalWidth, totalHeight));
                                            preview.Arrange(new Rect(0, 0, totalWidth, totalHeight));
                                            preview.UpdateLayout();

                                            // 5. Задаём целевой размер картинки в пикселях (масштабируем под Excel)
                                            int targetPixelWidth = 800;
                                            // Сохраняем пропорции полного размера (с подписями)
                                            int targetPixelHeight = (int)(totalHeight * targetPixelWidth / totalWidth);

                                            // 6. Рендерим в PNG
                                            byte[] pngBytes = WpfImageHelper.RenderVisualToPng(preview, targetPixelWidth, targetPixelHeight);

                                            // 7. Вставляем в Excel
                                            string uniqueName = $"Nesting_{item.NestingSheet.Id.ToString("N")[..8]}";
                                            using var stream = new MemoryStream(pngBytes);
                                            ExcelPicture pic = itemsheet.Drawings.AddPicture(uniqueName, stream);

                                            // Позиционирование
                                            pic.SetPosition(namePic, 10, 1, 10);

                                            // Подпись
                                            itemsheet.Cells[namePic + 1, 1].Value = $"s{type.S} {type.MetalDrop.Text}";
                                            itemsheet.Cells[namePic + 1, 1].Style.TextRotation = 90;
                                            itemsheet.Cells[namePic + 1, 1].Style.Font.Bold = true;
                                            itemsheet.Cells[namePic + 1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                                            itemsheet.Cells[namePic + 1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                                            // Высота строки под картинку
                                            itemsheet.Row(namePic + 1).Height = targetPixelHeight / 1.33 + 10;

                                            namePic++;
                                        }
                                        else if (item.imageBytes is not null)
                                        {
                                            // Старая логика для byte[] изображений
                                            using var stream = new MemoryStream(item.imageBytes);
                                            string uniqueName = $"Image_{Guid.NewGuid().ToString("N")[..8]}";
                                            ExcelPicture pic = itemsheet.Drawings.AddPicture(uniqueName, stream);

                                            itemsheet.Cells[namePic + 1, 1].Value = $"s{type.S} {type.MetalDrop.Text}";
                                            itemsheet.Cells[namePic + 1, 1].Style.TextRotation = 90;
                                            itemsheet.Cells[namePic + 1, 1].Style.Font.Bold = true;
                                            itemsheet.Cells[namePic + 1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                                            itemsheet.Cells[namePic + 1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                                            itemsheet.Row(namePic + 1).Height = 400;
                                            pic.SetPosition(namePic, 10, 1, 10);
                                            namePic++;
                                        }
                                    }
                                }
                                break;
                            }
                }
                else
                {
                    complectsheet.Cells[temp + 2, 1].Value = temp;                      //номер детали по порядку
                    complectsheet.Cells[temp + 2, 3].Value = det.Detail.Title;          //наименование детали

                    complectsheet.Cells[temp + 2, 4].Value = det.Detail.Description;    //маршрут изготовления
                    complectsheet.Cells[temp + 2, 4].Style.WrapText = true;

                    complectsheet.Cells[temp + 2, 5].Value = det.Detail.Count;          //количество деталей
                    complectsheet.Cells[temp + 2, 5].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    complectsheet.Cells[temp + 2, 5].Style.Font.Bold = true;

                    complectsheet.Cells[temp + 2, 7].Value = Math.Round(det.Detail.Mass, 1);    //масса детали
                    _totalMass += det.Detail.Mass * det.Detail.Count;                   //дополнительно считаем общий вес

                    complectsheet.Cells[temp + 2, 8].Value = det.Detail.Metal;          //материал

                    complectsheet.Cells[temp + 2, 9].Value = det.Detail.Destiny;        //толщина
                    if (complectsheet.Cells[temp + 1, 9].Value != null && $"{complectsheet.Cells[temp + 1, 9].Value}" != $"{complectsheet.Cells[temp + 2, 9].Value}")
                        complectsheet.Cells[temp + 1, 1, temp + 1, 10].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                    else complectsheet.Cells[temp + 1, 1, temp + 1, 10].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

                    complectsheet.Cells[temp + 2, 10].Style.Font.Color.SetColor(System.Drawing.Color.Red);  //факт
                    complectsheet.Cells[temp + 2, 10].Style.Font.Bold = true;

                    //детали с трубами выделяем бледно-розовым цветом
                    if (det.Detail.Description != null && (det.Detail.Description.Contains("Труборез") || det.Detail.Description.Contains("Лентопил")))
                        complectsheet.Cells[temp + 2, 1, temp + 2, 10].Style.Fill.SetBackground(System.Drawing.Color.Linen);

                    temp++;
                }
            }

            //добавляем покупные изделия
            if (ProductModel.Product.Baskets?.Count > 0)
            {
                complectsheet.Cells[temp + 2, 3].Value = "Покупные изделия:";
                complectsheet.Cells[temp + 2, 3].Style.Font.Bold = true;
                complectsheet.Cells[temp + 1, 1, temp + 1, 10].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;

                foreach (Part basket in ProductModel.Product.Baskets)
                {
                    complectsheet.Cells[temp + 3, 1].Value = temp;              //номер по порядку

                    byte[]? bytes = basket.ImageBytes;  //получаем изображение детали, если оно есть
                    if (bytes is not null)
                    {
                        Stream? stream = new MemoryStream(bytes);
                        string uniqueName = $"Image_{Guid.NewGuid().ToString("N")[..8]}";   //короткий уникальный ID
                        ExcelPicture pic = complectsheet.Drawings.AddPicture(uniqueName, stream);

                        //увеличиваем высоту строки, чтобы вмещалось изображение
                        complectsheet.Row(temp + 3).Height = 32;

                        pic.SetSize(32, 32);
                        pic.SetPosition(temp + 2, 5, 1, 5);     //для изображений индекс начинается от нуля (0), для ячеек - от единицы (1)
                    }

                    complectsheet.Cells[temp + 3, 3].Value = basket.Title;      //наименование изделия

                    complectsheet.Cells[temp + 3, 5].Value = basket.Count;      //количество изделий
                    complectsheet.Cells[temp + 3, 5].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    complectsheet.Cells[temp + 3, 5].Style.Font.Bold = true;

                    complectsheet.Cells[temp + 2, 1, temp + 2, 10].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    complectsheet.Cells[temp + 3, 1, temp + 3, 10].Style.Fill.SetBackground(System.Drawing.Color.Linen);
                    temp++;
                }
                temp++;     //добавляем к счетчику строку "Покупные изделия:"
            }

            //приводим float-значения типа 0,699999993 к формату 0,7
            foreach (var cell in complectsheet.Cells[3, 9, temp, 9])
                if (cell.Value != null && $"{cell.Value}".Contains(',')) cell.Style.Numberformat.Format = "0.0";

            complectsheet.Cells[temp + 2, 4].Value = "всего деталей:";
            complectsheet.Cells[temp + 2, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            complectsheet.Names.Add("totalCount", complectsheet.Cells[3, 5, temp + 1, 5]);
            complectsheet.Cells[temp + 2, 5].Formula = "=SUM(totalCount)";
            complectsheet.Cells[temp + 2, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            complectsheet.Cells[temp + 2, 5].Calculate();
            var totalCount = complectsheet.Cells[temp + 2, 5].Value;

            complectsheet.Cells[temp + 2, 6].Value = "общий вес:";
            complectsheet.Cells[temp + 2, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            complectsheet.Cells[temp + 2, 7].Value = Math.Ceiling(_totalMass);
            complectsheet.Cells[temp + 2, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            complectsheet.Names.Add("totalFact", complectsheet.Cells[3, 10, temp + 1, 10]);
            complectsheet.Cells[temp + 2, 10].Formula = "=SUM(totalFact)";
            complectsheet.Cells[temp + 2, 10].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            complectsheet.Row(temp + 2).Style.Font.Bold = true;     //выделяем жирным шрифтом подсчитанные кол-во и вес

            ExcelRange details = complectsheet.Cells[2, 1, temp + 1, 10];    //получаем таблицу деталей для оформления
            ExcelRange works = additionalSheet.Cells[1, 1, additionalRow - 1, 5];

            //в случае с нарезанными деталями, оформляем расшифровку работ
            if (Parts.Count > 0)
            {
                // Зачистка деталей
                complectsheet.Cells[temp + 3, 1].Value = "Зачистка деталей";
                complectsheet.Cells[temp + 3, 1, temp + 3, 2].Merge = true;

                complectsheet.Cells[temp + 3, 3].Value = "(по необходимости / требованию)";
                complectsheet.Cells[temp + 3, 3].Style.Font.Bold = true;
                complectsheet.Cells[temp + 3, 3, temp + 3, 9].Merge = true;

                // Расшифровка
                complectsheet.Cells[temp + 4, 1].Value = "Расшифровка:";
                complectsheet.Cells[temp + 4, 1, temp + 4, 2].Merge = true;
                complectsheet.Cells[temp + 4, 3, temp + 4, 9].Merge = true;

                var descriptionCell = complectsheet.Cells[temp + 4, 3];
                descriptionCell.Value = string.Empty;

                var addedKeys = new HashSet<string>(); // Чтобы избежать дублирования

                // Перебираем все ячейки в диапазоне [3,4] до [temp+2,4]
                for (int row = 3; row <= temp + 2; row++)
                {
                    var cell = complectsheet.Cells[row, 4];
                    if (cell?.Value == null) continue;

                    string cellValue = $"{cell.Value}";

                    foreach (var kvp in operationsMap)
                    {
                        string key = kvp.Key;
                        string value = kvp.Value;

                        if (cellValue.Contains(key) && !addedKeys.Contains(key))
                        {
                            descriptionCell.Value += value;
                            addedKeys.Add(key);
                        }
                    }
                }
            }

            // Заголовок легенды
            complectsheet.Cells[temp + 6, 1].Value = "Легенда цветов:";

            // Определяем цвета и соответствующие статусы
            var legend = new[]
            {
                (System.Drawing.Color.Green, "Всё хорошо"),
                (System.Drawing.Color.Yellow, "Не хватает"),
                (System.Drawing.Color.Orange, "Доп работы"),
                (System.Drawing.Color.Aqua, "Отгружено")
            };

            foreach (var (color, status) in legend)
            {
                // Ячейка с заливкой цвета
                complectsheet.Cells[temp + 6, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                complectsheet.Cells[temp + 6, 2].Style.Fill.BackgroundColor.SetColor(color);
                complectsheet.Cells[temp + 6, 2].Style.Border.BorderAround(ExcelBorderStyle.Thin);

                // Текст статуса рядом
                complectsheet.Cells[temp + 6, 3].Value = status;

                temp++;
            }

            //создаем этикетку
            ExcelWorksheet labelsheet = workbook.Workbook.Worksheets.Add("Этикетка");
            var logo = labelsheet.Drawings.AddPicture("A1", IsLaser ? "laser_logo.jpg" : "app_logo.jpg");  //файлы должны быть в директории bin/Debug...
            logo.SetPosition(0, 5, 0, 20);

            labelsheet.Cells[1, 1, 1, 2].Merge = true;
            labelsheet.Cells[2, 1].Value = offer != null ? offer.Order : Order.Text;
            labelsheet.Cells[2, 1].Style.Font.Size = 48;
            labelsheet.Cells[2, 1].Style.Font.Bold = true;
            labelsheet.Cells[2, 1, 2, 2].Merge = true;
            labelsheet.Cells[3, 1].Value = CustomerDrop.Text;
            labelsheet.Cells[3, 1].Style.Font.Size = 16;
            labelsheet.Cells[3, 1].Style.Font.Bold = true;
            labelsheet.Cells[3, 1, 3, 2].Merge = true;
            labelsheet.Cells[4, 1].Value = $"общее кол-во деталей:";
            labelsheet.Cells[4, 2].Value = totalCount;
            labelsheet.Cells[4, 1, 4, 2].Style.Font.Size = 16;
            labelsheet.Cells[4, 1, 4, 2].Style.Font.Bold = true;
            labelsheet.Cells[5, 1].Value = IsLaser ? "тел : (812) 509 - 60 - 11" : "тел:(812) 603 - 45 - 33";
            labelsheet.Cells[5, 1, 5, 2].Merge = true;

            ExcelRange label = labelsheet.Cells[1, 1, 5, 2];        //получаем этикетку для оформления

            //создаем реестр простых задач для Битрикса
            CreateSimpleRegistry(workbook);

            //создаем лист со сборками, если они есть
            if (AssemblyWindow.A.Assemblies.Count > 0)
            {
                ExcelWorksheet assemblysheet = workbook.Workbook.Worksheets.Add("Сборки");

                assemblysheet.Cells[1, 1, 1, 3].Merge = true;
                assemblysheet.Cells[1, 1].Value = offer != null ? offer.Order : Order.Text;     //Номер КП
                assemblysheet.Cells[1, 1].Style.Font.Size = 60;
                assemblysheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

                assemblysheet.Cells[1, 4, 1, 9].Merge = true;
                assemblysheet.Cells[1, 4].Value = CustomerDrop.Text;                            //Компания
                assemblysheet.Cells[1, 4].Style.Font.Size = 36;
                assemblysheet.Cells[1, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                //оформляем первую строку
                assemblysheet.Row(1).Style.Font.Bold = true;
                assemblysheet.Row(1).Height = 60;

                for (int head = 0; head < _heads.Count - 1; head++)
                {
                    assemblysheet.Cells[2, head + 1].Value = _heads[head];
                    assemblysheet.Cells[2, head + 1, 3, head + 1].Merge = true;
                    assemblysheet.Cells[2, head + 1, 3, head + 1].Style.WrapText = true;
                }

                int number = 1, row = 3;
                ExcelRange assemblyRange;
                var collect = Parts.Union(BasketControls.Select(p => p.Basket));

                foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
                {
                    row++;

                    assemblysheet.Cells[row, 1].Value = number;
                    assemblysheet.Cells[row, 3].Value = assembly.Title;
                    assemblysheet.Cells[row, 4].Value = assembly.Description;
                    assemblysheet.Cells[row, 5].Value = assembly.Count;
                    assemblysheet.Row(row).Style.Font.Bold = true;

                    number++; row++;

                    for (int p = 0; p < assembly.Particles.Count; p++)
                    {
                        Particle particle = assembly.Particles[p];
                        Part? part = collect.FirstOrDefault(p => p.Title == particle.Title);

                        if (part is not null)
                        {
                            byte[]? bytes = part.ImageBytes;  //получаем изображение детали, если оно есть
                            if (bytes is not null)
                            {
                                Stream? stream = new MemoryStream(bytes);
                                string uniqueName = $"Image_{Guid.NewGuid().ToString("N")[..8]}"; // короткий уникальный ID
                                ExcelPicture pic = assemblysheet.Drawings.AddPicture(uniqueName, stream);
                                assemblysheet.Row(row).Height = 32;    //увеличиваем высоту строки, чтобы вмещалось изображение
                                pic.SetSize(32, 32);
                                pic.SetPosition(row - 1, 5, 1, 5);     //для изображений индекс начинается от нуля (0), для ячеек - от единицы (1)
                            }
                            assemblysheet.Cells[row, 3].Value = particle.Title;
                            assemblysheet.Cells[row, 4].Value = part.Description;

                            //строки с трубами выделяем бледно-розовым цветом
                            if (part.Description != null && (part.Description.Contains('Т') || part.Description.Contains("ЛП"))) assemblysheet.Cells[row, 1, row, 9].Style.Fill.SetBackground(System.Drawing.Color.Linen);

                            assemblysheet.Cells[row, 5].Value = particle.Count;
                            assemblysheet.Cells[row, 5].Style.Font.Bold = true;
                            assemblysheet.Cells[row, 5].Style.Font.Color.SetColor(System.Drawing.Color.Red);

                            assemblysheet.Cells[row, 6].Value = part.Accuracy;
                            assemblysheet.Cells[row, 7].Value = Math.Ceiling(part.Mass);
                            assemblysheet.Cells[row, 8].Value = part.Metal;
                            assemblysheet.Cells[row, 9].Value = part.Destiny;
                        }

                        row++;
                    }

                    assemblysheet.Cells[row, 1, row, 9].Merge = true;
                    assemblyRange = assemblysheet.Cells[row - assembly.Particles.Count - 1, 1, row, 9];
                    assemblyRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                }

                assemblysheet.Cells[row + 2, 1].Value = "Зачистка деталей";
                assemblysheet.Cells[row + 2, 1, row + 2, 2].Merge = true;
                assemblysheet.Cells[row + 2, 3].Value = "(по необходимости / требованию)";
                assemblysheet.Cells[row + 2, 3].Style.Font.Bold = true;
                assemblysheet.Cells[row + 2, 3, row + 2, 9].Merge = true;

                assemblysheet.Cells[row + 3, 1].Value = "Расшифровка: ";
                assemblysheet.Cells[row + 3, 1, row + 3, 2].Merge = true;
                var descriptionCell = assemblysheet.Cells[row + 3, 3];
                var foundOperations = new HashSet<string>(); // Уникальные операции

                // Проходим по ячейкам в столбце 4 (столбец D)
                for (int r = 4; r <= row + 3; r++)
                {
                    var cellValue = assemblysheet.Cells[r, 4].Value?.ToString();
                    if (string.IsNullOrEmpty(cellValue)) continue;

                    foreach (var op in operationsMap)
                        if (cellValue.Contains(op.Key) && !foundOperations.Contains(op.Key))
                            foundOperations.Add(op.Key);
                }

                // Формируем итоговую строку
                descriptionCell.Value = string.Join("", foundOperations.Select(k => operationsMap[k]));

                // Объединение ячеек
                assemblysheet.Cells[row + 3, 3, row + 3, 9].Merge = true;

                ExcelRange assemblies = assemblysheet.Cells[2, 1, row, 9];
                assemblies.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                assemblies.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                assemblies.Style.Border.Right.Style = assemblies.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                assemblies.Style.Border.BorderAround(ExcelBorderStyle.Medium);
                assemblysheet.Cells.AutoFitColumns();
            }

            //обводка границ и авторастягивание столбцов
            details.Style.HorizontalAlignment = label.Style.HorizontalAlignment = works.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            details.Style.VerticalAlignment = label.Style.VerticalAlignment = works.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            details.Style.Border.Right.Style = label.Style.Border.Bottom.Style = works.Style.Border.Right.Style = works.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            details.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            label.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            works.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            complectsheet.Cells.AutoFitColumns();
            if (complectsheet.Column(3).Width < 40) complectsheet.Column(3).Width = 40;

            additionalSheet.Column(1).Style.WrapText = true;
            additionalSheet.Cells.AutoFitColumns();

            labelsheet.DefaultRowHeight = 40;
            labelsheet.Cells.AutoFitColumns();

            //устанавливаем настройки для печати, чтобы сохранение в формате .pdf выводило весь документ по ширине страницы
            complectsheet.PrinterSettings.FitToPage = true;
            complectsheet.PrinterSettings.FitToWidth = 1;
            complectsheet.PrinterSettings.FitToHeight = 0;
            complectsheet.PrinterSettings.HorizontalCentered = true;

            //устанавливаем колонтитул (в данном случае будет подчеркнутое название файла)            
            complectsheet.HeaderFooter.OddFooter.RightAlignedText = $"&24&U&\"Arial Rounded MT Bold\" {Path.GetFileNameWithoutExtension(ExcelHeaderFooter.FileName)}";

            //сохраняем книгу в файл Excel
            if (offer is not null && offer.Order is not null && Directory.Exists(_path))    //если в параметре передан расчет, подразумевается, что заказ создан
            {                                                                               //и файл комплектации нужно сохранить в папке заказа
                UpdateOffer(offer);                                 //добавляем номер заказа в ячейки реестров
                string[] dirs = Directory.GetDirectories(_path);    //получаем все подкаталоги в папке Y:\\Производство\\Laser rezka\\В работу"
                foreach (string s in dirs)
                {
                    if (s.Contains(offer.Order.Remove(4)))                    //ищем подкаталог с номером заказа
                    {
                        string[] files = Directory.GetFileSystemEntries(s);   //получаем все файлы в папке заказа, чтобы сохранить файл комплектации в директории этих файлов
                        if (files.Length > 0)
                        {
                            workbook.SaveAs($"{Path.GetDirectoryName(files[0])}\\{offer.Order} {CustomerDrop.Text} - комплектация.xlsx");
                            CreateRegistry(files[0], offer.Order);

                            StatusBegin($"Изменения в базе сохранены. Кроме того созданы файлы комплектации и списка задач в папке {Path.GetDirectoryName(files[0])}", StatusMessageType.Success);
                            break;
                        }
                        else StatusBegin($"Изменения в базе сохранены. Но файл комплектации не создан, так как в папке заказа нет файлов.", StatusMessageType.Warning);
                    }
                }
            }
            else workbook.SaveAs($"{Path.GetDirectoryName(_path)}\\{Order.Text} {CustomerDrop.Text} - комплектация.xlsx");
        }

        private static string GetDimensionsString(Dictionary<int, List<string>> propsDict)
        {
            if (propsDict == null || !propsDict.TryGetValue(100, out var list) || list == null)
                return "";

            // Листовая деталь: ширина x высота
            if (list.Count >= 2 && !string.IsNullOrEmpty(list[0]) && !string.IsNullOrEmpty(list[1]))
            {
                return $"{list[0]}×{list[1]}"; // используем × (U+00D7), а не x
            }

            // Труба: длина
            if (list.Count >= 3 && !string.IsNullOrEmpty(list[2]))
            {
                return list[2];
            }

            // Если есть только ширина или только высота — можно вернуть как есть, но, скорее всего, это ошибка
            if (list.Count >= 1 && !string.IsNullOrEmpty(list[0]))
            {
                return list[0]; // на всякий случай
            }

            return "";
        }

        //-ПРОСТЫЕ ЗАДАЧИ
        private void CreateSimpleRegistry(ExcelPackage workbook)
        {
            ExcelWorksheet registrysheet = workbook.Workbook.Worksheets.Add("Реестр");

            int rowTask = 2;

            List<string> _headersBitrix = new()
            {
                "№ заказа", "Заказчик", "Менеджер", "Количество материала", "Лазерные работы", "Труборез",
                "Гибочные работы", "Время лазерных работ", "Производство", "Нанесение покрытий",
                "Логистика", "Комментарий", "Дата сдачи", "Время фрезерных работ"
            };
            for (int col = 0; col < _headersBitrix.Count; col++) registrysheet.Cells[1, col + 7].Value = _headersBitrix[col];

            foreach (DetailControl det in DetailControls)
            {
                for (int i = 0; i < det.TypeDetailControls.Count; i++)
                {
                    TypeDetailControl type = det.TypeDetailControls[i];

                    registrysheet.Cells[i + rowTask, 8].Value = CustomerDrop.Text;  //"Заказчик"
                    registrysheet.Cells[i + rowTask, 9].Value = ShortManager();     //"Менеджер"

                    if (HasDelivery != false)
                        registrysheet.Cells[i + rowTask, 17].Value = "Доставка ";   //"Логистика"

                    if (type.CheckMetal.IsChecked == false)
                        registrysheet.Cells[i + rowTask, 18].Value = "Давальч. ";   //"Комментарий"

                    if (type.Comment != null && type.Comment != "")
                        registrysheet.Cells[i + rowTask, 18].Value += $"{type.Comment}";

                    registrysheet.Cells[i + rowTask, 19].Value = EndDate();         //"Дата сдачи"
                    registrysheet.Cells[i + rowTask, 19].Style.Numberformat.Format = "d MMM";

                    if (type.MetalDrop.SelectedItem is Metal met)
                    {
                        double _mass = Math.Ceiling(det.Detail.IsComplect ? type.Mass : type.Mass * type.Count);

                        registrysheet.Cells[i + rowTask, 10].Value = _mass;         //"Количество материала"
                    }

                    foreach (WorkControl w in type.WorkControls)    //анализируем работы каждой заготовки
                    {
                        if (w.Result == 0) continue;                //пропускаем добавление нулевых работ

                        if (w.workType is CutControl cut)
                        {
                            //"Толщина и марка металла"
                            string description = "";
                            if ((type.MetalDrop.Text.Contains("ст") && type.S >= 3) || (type.MetalDrop.Text.Contains("хк") && type.S < 3)) description = $"s{type.S}";
                            else if (type.MetalDrop.Text.Contains("ст") && type.S < 3) description = $"s{type.S} гк";
                            else if (type.MetalDrop.Text.Contains("амг2")) description = $"al{type.S}";
                            else if (type.MetalDrop.Text.Contains("амг") || type.MetalDrop.Text.Contains("д16")) description = $"al{type.S} {type.MetalDrop.Text}";
                            else if (type.MetalDrop.Text.Contains("латунь")) description = $"br{type.S}";
                            else if (type.MetalDrop.Text.Contains("медь")) description = $"cu{type.S}";
                            else description = $"s{type.S} {type.MetalDrop.Text}";

                            //добавляем тэг рифленки при необходимости
                            if (cut.IsGrooved) description += " рифл";

                            //добавляем тег срочности и коментария
                            if (HasAssembly) description += " (ЭКСПРЕСС)";
                            if (type.Comment != null && type.Comment != "") description += " (комментарий)";

                            //"Лазерные работы"
                            registrysheet.Cells[i + rowTask, 11].Value = description;

                            //"Время лазерных работ"
                            registrysheet.Cells[i + rowTask, 14].Value = Math.Ceiling(w.Result * 0.012f / w.Ratio);
                        }
                        else if (w.workType is BendControl)
                        {
                            //"Гибочные работы"
                            registrysheet.Cells[i + rowTask, 13].Value = Math.Ceiling(w.Result * 0.018f / w.Ratio);
                        }
                        else if (w.workType is PipeControl pipe)
                        {
                            //"Труборез"
                            registrysheet.Cells[i + rowTask, 12].Value = $"(ТР) {type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text}";

                            //добавляем тег срочности и коментария
                            if (HasAssembly) registrysheet.Cells[i + rowTask, 12].Value += " (ЭКСПРЕСС)";
                            if (type.Comment != null && type.Comment != "") registrysheet.Cells[i + rowTask, 12].Value += " (комментарий)";

                            //"Время лазерных работ"
                            registrysheet.Cells[i + rowTask, 14].Value = Math.Ceiling(w.Result * 0.012f / w.Ratio);
                        }
                        else if (w.workType is SawControl _saw)
                        {
                            //"Труборез"
                            registrysheet.Cells[i + rowTask, 12].Value = $"(ЛП) {type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text}";

                            //добавляем тег срочности и коментария
                            if (HasAssembly) registrysheet.Cells[i + rowTask, 12].Value += " (ЭКСПРЕСС)";
                            if (type.Comment != null && type.Comment != "") registrysheet.Cells[i + rowTask, 12].Value += " (комментарий)";

                            //"Время лазерных работ"
                            registrysheet.Cells[i + rowTask, 14].Value = Math.Ceiling(w.Result * 0.018f / w.Ratio);
                        }
                        else if (w.workType is ExtraControl _extra)     //для доп работы её наименование добавляем к наименованию работы - особый случай
                        {
                            //"Производство"
                            registrysheet.Cells[i + rowTask, 15].Value += $"{_extra.NameExtra} ";
                        }
                        else if (w.workType is MillingTotalControl _milling)
                        {
                            //"Время фрезерных работ"
                            registrysheet.Cells[i + rowTask, 20].Value = _milling.TotalTime;
                        }
                        else if (w.WorkDrop.SelectedItem is Work work)
                        {
                            //"Нанесение покрытий"
                            if (w.workType is PaintControl _paint)
                                registrysheet.Cells[i + rowTask, 16].Value += $"{_paint.Ral} {_paint.TypeDrop.SelectedItem} ";
                            //"Производство"
                            else registrysheet.Cells[i + rowTask, 15].Value += $"{work.Name} ";
                        }
                    }
                }
                rowTask += det.TypeDetailControls.Count;
            }

            if (AssemblyWindow.A.Assemblies.Count > 0)
            {
                foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
                {
                    if (assembly.WeldPrice == 0 && assembly.PaintPrice == 0) continue;

                    registrysheet.Cells[rowTask, 8].Value = CustomerDrop.Text;  //"Заказчик"
                    registrysheet.Cells[rowTask, 9].Value = ShortManager();     //"Менеджер"
                    registrysheet.Cells[rowTask, 10].Value = assembly.Count;    //"Кол-во"
                    registrysheet.Cells[rowTask, 19].Value = EndDate();         //"Дата сдачи"
                    registrysheet.Cells[rowTask, 19].Style.Numberformat.Format = "d MMM";

                    if (assembly.WeldPrice > 0)         //"Производство"
                    {
                        registrysheet.Cells[rowTask, 15].Value = "Сварка (сборка)";
                    }

                    if (assembly.PaintPrice > 0)        //"Нанесение покрытий" и "Комментарий"
                    {
                        registrysheet.Cells[rowTask, 16].Value = $"{assembly.Ral} {assembly.Structure}";
                        registrysheet.Cells[rowTask, 18].Value +=
                            $"Окраска в {assembly.Ral} {assembly.Structure} " +
                            $"({Math.Round(assembly.Square, 2)} кв м - {assembly.Count} шт) ";
                    }
                    rowTask++;
                }
            }

            ExcelRange registryBitrix = registrysheet.Cells[1, 7, rowTask - 1, 20];
            registryBitrix.Style.Fill.SetBackground(System.Drawing.Color.LavenderBlush);
            registrysheet.Row(1).Style.Font.Bold = true;

            //обводка границ и авторастягивание столбцов
            registryBitrix.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            registryBitrix.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            registryBitrix.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            registryBitrix.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            registryBitrix.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            registrysheet.Cells.AutoFitColumns();
        }

        //-ЗАДАЧИ
        private void CreateRegistry(string _path, string _order)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var CSVbook = new ExcelPackage();
            ExcelWorksheet tasksheet = CSVbook.Workbook.Worksheets.Add("Задачи");

            List<string> headers = new()
            {
                "Название", "Описание", "Крайний срок", "Исполнитель", "Проект",
                "Время на выполнение задачи в секундах", "Разрешить ответственному менять сроки задачи",
                "Проконтролировать задачу после завершения", "Автоматически завершать задачу при завершении подзадач"
            };
            for (int col = 0; col < headers.Count; col++) tasksheet.Cells[1, col + 1].Value = headers[col];

            string material = "";

            int temp = 2;
            foreach (DetailControl det in DetailControls)
            {
                bool isProd = false;        //создана ли задача на "Производство"
                int rowProd = 0;            //номер задачи на "Производство"

                foreach (TypeDetailControl type in det.TypeDetailControls)
                {
                    string description = "";        //Описание заготовки
                    double _mass = Math.Ceiling(det.Detail.IsComplect ? type.Mass : type.Mass * type.Count);    //Масса

                    foreach (WorkControl w in type.WorkControls)            //анализируем работы каждой типовой детали
                    {
                        if (w.Result == 0) continue;                        //пропускаем добавление нулевых работ

                        //"Название"
                        tasksheet.Cells[temp, 1].Value = $"{_order} ";

                        if (w.workType is CutControl cut)
                        {
                            if ((type.MetalDrop.Text.Contains("ст") && type.S >= 3) || (type.MetalDrop.Text.Contains("хк") && type.S < 3)) description = $"s{type.S}";
                            else if (type.MetalDrop.Text.Contains("ст") && type.S < 3) description = $"s{type.S} гк";
                            else if (type.MetalDrop.Text.Contains("амг2")) description = $"al{type.S}";
                            else if (type.MetalDrop.Text.Contains("амг") || type.MetalDrop.Text.Contains("д16")) description = $"al{type.S} {type.MetalDrop.Text}";
                            else if (type.MetalDrop.Text.Contains("латунь")) description = $"br{type.S}";
                            else if (type.MetalDrop.Text.Contains("медь")) description = $"cu{type.S}";
                            else description = $"s{type.S} {type.MetalDrop.Text}";

                            //добавляем тэг рифленки при необходимости
                            if (cut.IsGrooved) description += " рифл";

                            //добавляем тэг срочности и коментария
                            if (HasAssembly) description += " (ЭКСПРЕСС)";
                            if (type.Comment != null && type.Comment != "") description += " (комментарий)";

                            //"Название"
                            tasksheet.Cells[temp, 1].Value += $"{description}";
                            //"Описание"
                            tasksheet.Cells[temp, 2].Value = $"Заказчик: {CustomerDrop.Text}, Количество материала: {_mass}, Комментарий: ";
                            if (type.CheckMetal.IsChecked == false) tasksheet.Cells[temp, 2].Value += "Давальч. ";
                            else
                            {   //если требуется закупить материал, заполняем строку для задачи в снабжение
                                if (cut.Items?.Count > 0)
                                {
                                    var _items = cut.Items?.GroupBy(c => c.sheetSize);      //группируем все листы по размеру
                                    if (_items is not null)
                                        foreach (var item in _items)    //каждую группу листов одного размера и их количество записываем в одну строку
                                            material += $"Лист {description} ({item.Key}) - {item.Sum(s => s.sheets)} шт, ";
                                }
                                else material += $"Лист {description} ({type.A}x{type.B}) - {type.Count} шт, ";
                            }
                            if (type.Comment != null && type.Comment != "") tasksheet.Cells[temp, 2].Value += $"{type.Comment}";
                            //"Крайний срок"
                            tasksheet.Cells[temp, 3].Value = DateTime.UtcNow.AddDays(3).ToString("g");
                            //"Исполнитель"
                            tasksheet.Cells[temp, 4].Value = $"Павел Березкин";
                            //"Проект"
                            tasksheet.Cells[temp, 5].Value = "Лазерные работы";
                            //"Время на выполнение задачи в секундах"
                            tasksheet.Cells[temp, 6].Value = Math.Ceiling(w.Result * 0.012f * 60) / w.Ratio;

                            temp++;
                        }
                        else if (w.workType is BendControl)
                        {
                            //"Название"
                            tasksheet.Cells[temp, 1].Value += $"Гибка {description}";
                            //"Описание"
                            tasksheet.Cells[temp, 2].Value = $"Заказчик: {CustomerDrop.Text}";
                            //"Крайний срок"
                            tasksheet.Cells[temp, 3].Value = DateTime.UtcNow.AddDays(5).ToString("g");
                            //"Исполнитель"
                            tasksheet.Cells[temp, 4].Value = $"Павел Березкин";
                            //"Проект"
                            tasksheet.Cells[temp, 5].Value = "Гибочные работы";
                            //"Время на выполнение задачи в секундах"
                            tasksheet.Cells[temp, 6].Value = Math.Ceiling(w.Result * 0.018f * 60) / w.Ratio;

                            temp++;
                        }
                        else if (w.workType is PipeControl pipe)
                        {
                            description = $"{type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text}";

                            //добавляем тег срочности и коментария
                            if (HasAssembly) description += " (ЭКСПРЕСС)";
                            if (type.Comment != null && type.Comment != "") description += " (комментарий)";

                            //"Название"
                            tasksheet.Cells[temp, 1].Value += $"{description}";
                            //"Описание"
                            tasksheet.Cells[temp, 2].Value = $"Заказчик: {CustomerDrop.Text}, Количество материала: {_mass}, Комментарий: ";
                            if (type.CheckMetal.IsChecked == false) tasksheet.Cells[temp, 2].Value += "Давальч. ";
                            else
                            {   //если требуется закупить материал, заполняем строку для задачи в снабжение
                                if (pipe.Items?.Count > 0)
                                {
                                    var _items = pipe.Items?.GroupBy(c => c.sheetSize);      //группируем все листы по размеру
                                    if (_items is not null)
                                        foreach (var item in _items)    //каждую группу листов одного размера и их количество записываем в одну строку
                                            material += $"{type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text} ({item.Key}) - {item.Sum(s => s.sheets)} шт, ";
                                }
                                else material += $"{type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text} ({type.L}) - {type.Count} шт, ";
                            }
                            if (type.Comment != null && type.Comment != "") tasksheet.Cells[temp, 2].Value += $"{type.Comment}";
                            //"Крайний срок"
                            tasksheet.Cells[temp, 3].Value = DateTime.UtcNow.AddDays(3).ToString("g");
                            //"Исполнитель"
                            tasksheet.Cells[temp, 4].Value = $"Руслан Ломакин";
                            //"Проект"
                            tasksheet.Cells[temp, 5].Value = "Труборез";
                            //"Время на выполнение задачи в секундах"
                            tasksheet.Cells[temp, 6].Value = Math.Ceiling(w.Result * 0.012f * 60) / w.Ratio;

                            temp++;
                        }
                        else if (w.WorkDrop.SelectedItem is Work work)
                        {
                            if (!isProd)
                            {
                                //"Название"
                                tasksheet.Cells[temp, 1].Value += $"{CustomerDrop.Text}";
                                //"Описание"
                                if (w.workType is PaintControl _paint)
                                {
                                    tasksheet.Cells[temp, 2].Value = $"Окраска в {_paint.Ral}";
                                    material += $"Заказ краски - {_paint.Ral}, ";
                                }
                                else if (w.workType is ExtraControl _extra) tasksheet.Cells[temp, 2].Value = $"{_extra.NameExtra}";
                                else tasksheet.Cells[temp, 2].Value = $"{work.Name}";
                                //"Крайний срок"
                                tasksheet.Cells[temp, 3].Value = DateTime.UtcNow.AddDays(10).ToString("g");
                                //"Исполнитель"
                                tasksheet.Cells[temp, 4].Value = $"Леонид Шишлин";
                                //"Проект"
                                tasksheet.Cells[temp, 5].Value = "Производство";
                                //"Время на выполнение задачи в секундах"
                                tasksheet.Cells[temp, 6].Value = 0;

                                isProd = true;
                                rowProd = temp;
                                temp++;
                            }
                            else if (w.workType is PaintControl paint && !$"{tasksheet.Cells[rowProd, 2].Value}".Contains($"Окраска в {paint.Ral}"))
                            {
                                tasksheet.Cells[rowProd, 2].Value += $", Окраска в {paint.Ral}";
                                material += $"Заказ краски - {paint.Ral}, ";
                            }
                            else if (w.workType is ExtraControl extra && !$"{tasksheet.Cells[rowProd, 2].Value}".Contains($"{extra.NameExtra}"))
                                tasksheet.Cells[rowProd, 2].Value += $", {extra.NameExtra}";
                            else if (!$"{tasksheet.Cells[rowProd, 2].Value}".Contains($"{work.Name}"))
                                tasksheet.Cells[rowProd, 2].Value += $", {work.Name}";
                        }
                    }
                }
            }

            if (material != "")
            {
                //"Название"
                tasksheet.Cells[temp, 1].Value = $"Закупка материала под заказ: {_order} {CustomerDrop.Text}";
                //"Описание"
                tasksheet.Cells[temp, 2].Value = material;
                //"Крайний срок"
                tasksheet.Cells[temp, 3].Value = DateTime.UtcNow.AddDays(2).ToString("g");
                //"Исполнитель"
                tasksheet.Cells[temp, 4].Value = $"Антон Сухоруков";
                //"Время на выполнение задачи в секундах"
                tasksheet.Cells[temp, 6].Value = 0;

                temp++;
            }

            for (int row = 2; row < temp; row++)
            {
                tasksheet.Cells[row, 7].Value = 1;
                tasksheet.Cells[row, 8].Value = 0;
                tasksheet.Cells[row, 9].Value = 1;
            }
            ExcelRange taskRange = tasksheet.Cells[1, 1, temp - 1, 9];

            var file = new FileInfo($"{Path.GetDirectoryName(_path)}\\{_order} {CustomerDrop.Text} - список задач.csv");
            var format = new ExcelOutputTextFormat
            {
                Delimiter = ';',
                Encoding = new UTF8Encoding(),
            };
            taskRange.SaveToText(file, format);
        }

        //-РЕЕСТР
        private void UpdateOffer(Offer offer)   // метод добавления номера заказа в ячейки реестров
        {
            if (offer.Act is null || !File.Exists(offer.Act)) return;

            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            try
            {
                using var offerbook = new ExcelPackage(new FileInfo(offer.Act));
                ExcelWorksheet? registrysheet = offerbook.Workbook.Worksheets.FirstOrDefault(x => x.Name == "Реестр");

                if (registrysheet is not null)
                {
                    foreach (var cell in registrysheet.Cells)
                    {
                        if (cell.Value is not null && $"{cell.Value}" == "№ заказа")
                        {
                            int countTypeDetails = DetailControls.Sum(t => t.TypeDetailControls.Count);

                            for (int i = 1; i <= countTypeDetails; i++)
                            {
                                registrysheet.Cells[cell.Start.Row + i, cell.Start.Column].Value = offer.Order;
                            }
                        }

                        if (cell.Value is not null && $"{cell.Value}" == "№ Проекта / Лазера")
                        {
                            registrysheet.Cells[cell.Start.Row + 1, cell.Start.Column].Value = offer.Order;
                        }
                    }
                }

                ExcelWorksheet? notesheet = offerbook.Workbook.Worksheets.FirstOrDefault(x => x.Name == "Накладная");

                if (notesheet is not null)
                    foreach (var cell in notesheet.Cells)
                        if (cell.Value is not null && $"{cell.Value}" == "НАКЛАДНАЯ №")
                            notesheet.Cells[cell.Start.Row, cell.Start.Column + 1].Value = offer.Order;

                // ----- сохраняем книгу в файл Excel -----
                offerbook.SaveAs(offer.Act);      //сохраняем файл .xlsx
            }
            catch (Exception ex) { MessageBox.Show($"{ex.Message}\nВозможно файл КП открыт и не удается внести изменения реестра, закройте его и поворите попытку."); }
        }

        //-ПАСПОРТ
        private void CreatePassport(object sender, RoutedEventArgs e)
        {
            if (ActiveOffer is not null) CreatePassport(ActiveOffer);
            else StatusBegin("Для создания паспорта необходимо загрузить расчет.", StatusMessageType.Error);
        }
        private void CreatePassport(Offer offer)
        {
            // Если путь к расчету не сохранен, или файла комплектации по этому пути нет, выходим из метода
            if (offer.Act is null || !File.Exists($"{Path.GetDirectoryName(offer.Act)}\\{Order.Text} {CustomerDrop.Text} - комплектация.xlsx"))
            {
                StatusBegin("Не удалось найти ФАЙЛ комплектации для создания паспорта. Попробуйте пересохранить расчет заново.", StatusMessageType.Error);
                return;
            }

            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            // Получаем файл комплектации, созданный ранее
            using var complectbook = new ExcelPackage(new FileInfo($"{Path.GetDirectoryName(offer.Act)}\\{Order.Text} {CustomerDrop.Text} - комплектация.xlsx"));

            // Получаем лист комплектации
            ExcelWorksheet? complectsheet = complectbook.Workbook.Worksheets.FirstOrDefault(x => x.Name == "Комплектация");

            if (complectsheet is not null)
            {
                // Добавляем этот лист в книгу как новый с именем "Паспорт"
                complectsheet = complectbook.Workbook.Worksheets.Add("Паспорт", complectsheet);

                // И удаляем все остальные листы
                while (complectbook.Workbook.Worksheets.Count > 1)
                    complectbook.Workbook.Worksheets.Delete(complectbook.Workbook.Worksheets[0]);

                // Добавляем и настраиваем первую строку с заголовком
                complectsheet.InsertRow(1, 1);
                complectsheet.Cells[1, 1, 1, 10].Merge = true;
                complectsheet.Cells[1, 1].Value = "Паспорт качества";
                complectsheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                complectsheet.Row(1).Style.Font.Bold = true;

                // === ОПРЕДЕЛЯЕМ ПРОВАЙДЕРА ПО ФЛАГАМ ===
                var provider = ProviderRegistry.GetByFlags(IsLaser, IsAgent)
                            ?? ProviderRegistry.Providers.FirstOrDefault();

                // Редактируем остальные строки
                complectsheet.Cells[2, 1].Value = "спец , упд ";
                complectsheet.Cells[2, 4].Value = provider?.Name;
                complectsheet.Row(2).Height = 16;
                complectsheet.Cells[1, 1, 2, 10].Style.Font.Size = 12;

                complectsheet.DeleteRow(Parts.Count + 5);
                complectsheet.DeleteRow(Parts.Count + 7);
                complectsheet.DeleteRow(Parts.Count + 7);
                complectsheet.DeleteRow(Parts.Count + 7);
                complectsheet.DeleteRow(Parts.Count + 7);

                complectsheet.Cells[Parts.Count + 7, 3].Value = "Детали соответствуют конструкторской документации Заказчика";
                complectsheet.Cells[Parts.Count + 9, 3].Value = "Начальник производства";
                complectsheet.Cells[Parts.Count + 9, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                complectsheet.Cells[Parts.Count + 9, 6].Value = "Березкин П.";
                complectsheet.Cells[Parts.Count + 9, 4, Parts.Count + 9, 5].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

                // === ДОБАВЛЯЕМ ПОДПИСЬ И ПЕЧАТЬ НА ОСНОВЕ ПРОВАЙДЕРА ===
                if (provider != null)
                {
                    // Подпись
                    using var sigStream = WpfImageHelper.GetStream(provider.SignatureResource);
                    if (sigStream != null)
                    {
                        ExcelPicture signature = complectsheet.Drawings.AddPicture("signature", sigStream);
                        signature.SetPosition(Parts.Count + 6, 5, 3, 50);
                        signature.SetSize(120, 50);
                    }

                    // Печать
                    using var printStream = WpfImageHelper.GetStream(provider.PrintResource);
                    if (printStream != null)
                    {
                        ExcelPicture print = complectsheet.Drawings.AddPicture("print", printStream);
                        print.SetPosition(Parts.Count + 9, 5, 3, 0);
                        print.SetSize(120, 120);
                    }
                }

                // Выравниваем содержимое и сохраняем книгу в файл
                complectsheet.Cells.AutoFitColumns();
                complectbook.SaveAs($"{Path.GetDirectoryName(offer.Act)}\\{Order.Text} {CustomerDrop.Text} - паспорт.xlsx");
                StatusBegin($"Создан паспорт качества для текущего расчета: {Order.Text} {CustomerDrop.Text}", StatusMessageType.Success);
            }
            else
            {
                StatusBegin("Не удалось найти ЛИСТ комплектации для создания паспорта. Попробуйте пересохранить расчет заново.", StatusMessageType.Warning);
            }
        }

        //-ПРОСТАЯ КОМПЛЕКТАЦИЯ
        private void CreateComplect(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "All files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames.Length > 0) CreateComplect(openFileDialog.FileNames);
            else StatusBegin($"Не выбрано ни одного файла", StatusMessageType.Warning);
        }
        private void CreateComplect(string[] _paths)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet complectsheet = workbook.Workbook.Worksheets.Add("Комплектация");

            complectsheet.Cells[1, 1, 1, 3].Merge = true;
            complectsheet.Cells[1, 1].Value = Order.Text;                       //Номер КП
            complectsheet.Cells[1, 1].Style.Font.Size = 60;
            complectsheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

            complectsheet.Cells[1, 4, 1, 9].Merge = true;
            complectsheet.Cells[1, 4].Value = CustomerDrop.Text;                //Компания
            complectsheet.Cells[1, 4].Style.Font.Size = 36;
            complectsheet.Cells[1, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            //оформляем первую строку
            complectsheet.Row(1).Style.Font.Bold = true;
            complectsheet.Row(1).Height = 60;

            //устанавливаем заголовки таблицы
            List<string> _heads = new() { "№", "Название детали", "Кол-во" };
            for (int head = 0; head < _heads.Count; head++) complectsheet.Cells[2, head + 1].Value = _heads[head];

            for (int i = 0; i < _paths.Length; i++)
            {
                string path = Path.GetFileNameWithoutExtension(_paths[i]);

                complectsheet.Cells[i + 3, 1].Value = i + 1;                                                    //номер по порядку

                if (path.ToLower().Contains('n'))                                                               //наименование детали
                    complectsheet.Cells[i + 3, 2].Value = path.Remove(path.ToLower().IndexOf('n'));
                else complectsheet.Cells[i + 3, 2].Value = path;

                Regex count = new(@"[+-]?((\d+\.?\d*)|(\.\d+))");

                List<Match> matches = count.Matches(path).ToList();

                if (matches.Count > 0) complectsheet.Cells[i + 3, 3].Value = (int)Parser($"{matches[^1]}");     //количество деталей
                else complectsheet.Cells[i + 3, 3].Value = 0;
                complectsheet.Cells[i + 3, 3].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                complectsheet.Cells[i + 3, 3].Style.Font.Bold = true;
            }

            complectsheet.Cells[_paths.Length + 3, 2].Value = "всего деталей:";
            complectsheet.Cells[_paths.Length + 3, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            complectsheet.Names.Add("totalCount", complectsheet.Cells[3, 3, _paths.Length + 2, 3]);
            complectsheet.Cells[_paths.Length + 3, 3].Formula = "=SUM(totalCount)";
            complectsheet.Cells[_paths.Length + 3, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            complectsheet.Row(_paths.Length + 3).Style.Font.Bold = true;

            complectsheet.Cells[_paths.Length + 4, 1].Value = "Зачистка деталей";
            complectsheet.Cells[_paths.Length + 4, 2].Value = "(по необходимости / требованию)";
            complectsheet.Cells[_paths.Length + 4, 2].Style.Font.Bold = true;

            ExcelRange details = complectsheet.Cells[2, 1, _paths.Length + 2, 3];     //получаем таблицу деталей для оформления


            //создаем этикетку
            ExcelWorksheet labelsheet = workbook.Workbook.Worksheets.Add("Этикетка");
            var logo = labelsheet.Drawings.AddPicture("A1", IsLaser ? "laser_logo.jpg" : "app_logo.jpg");  //файлы должны быть в директории bin/Debug...
            logo.SetPosition(0, 5, 0, 20);

            labelsheet.Cells[1, 1, 1, 2].Merge = true;
            labelsheet.Cells[2, 1].Value = Order.Text;
            labelsheet.Cells[2, 1].Style.Font.Size = 48;
            labelsheet.Cells[2, 1].Style.Font.Bold = true;
            labelsheet.Cells[2, 1, 2, 2].Merge = true;
            labelsheet.Cells[3, 1].Value = CustomerDrop.Text;
            labelsheet.Cells[3, 1].Style.Font.Size = 16;
            labelsheet.Cells[3, 1].Style.Font.Bold = true;
            labelsheet.Cells[3, 1, 3, 2].Merge = true;
            labelsheet.Cells[4, 1].Value = $"общее кол-во деталей:";
            labelsheet.Names.Add("totalCount", complectsheet.Cells[3, 3, _paths.Length + 2, 3]);
            labelsheet.Cells[4, 2].Formula = "=SUM(totalCount)";
            labelsheet.Cells[4, 1, 4, 2].Style.Font.Size = 16;
            labelsheet.Cells[4, 1, 4, 2].Style.Font.Bold = true;
            labelsheet.Cells[5, 1].Value = IsLaser ? "тел : (812)509 - 60 - 11" : "тел:(812)603 - 45 - 33";
            labelsheet.Cells[5, 1, 5, 2].Merge = true;

            ExcelRange label = labelsheet.Cells[1, 1, 5, 2];                        //получаем этикетку для оформления

            //обводка границ и авторастягивание столбцов
            details.Style.HorizontalAlignment = label.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            details.Style.VerticalAlignment = label.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            details.Style.Border.Right.Style = details.Style.Border.Bottom.Style = label.Style.Border.Right.Style = label.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            details.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            label.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            complectsheet.Cells.AutoFitColumns();
            labelsheet.DefaultRowHeight = 40;
            labelsheet.Cells.AutoFitColumns();

            //устанавливаем настройки для печати, чтобы сохранение в формате .pdf выводило весь документ по ширине страницы
            complectsheet.PrinterSettings.FitToPage = true;
            complectsheet.PrinterSettings.FitToWidth = 1;
            complectsheet.PrinterSettings.FitToHeight = 0;
            complectsheet.PrinterSettings.HorizontalCentered = true;

            //сохраняем книгу в файл Excel
            if (Order.Text != "" && CustomerDrop.Text != "") workbook.SaveAs($"{Path.GetDirectoryName(_paths[0])}\\{Order.Text} {CustomerDrop.Text} - комплектация.xlsx");
            else workbook.SaveAs($"{Path.GetDirectoryName(_paths[0])}\\Простая комплектация.xlsx");
            StatusBegin($"Создана простая комплектация в папке {Path.GetDirectoryName(_paths[0])}", StatusMessageType.Success);
        }

        //-МАРШРУТ ПРОИЗВОДСТВА
        private void ShowRouteWindow(object sender, RoutedEventArgs e)
        {
            if (ActiveOffer is not null) ShowRouteWindow();
            else StatusBegin("Для создания маршрута производства необходимо загрузить расчет.", StatusMessageType.Error);
        }
        private void ShowRouteWindow()
        {
            RouteWindow routeWindow = new();
            if (Parts.Count > 0) foreach (Part part in Parts)
            {
                TextBox _part = new()
                {
                    Width = 125,
                    Height = 70,
                    Margin = new Thickness(5),
                    Text = part.Title,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };
                routeWindow.DetailStack.Children.Add(_part);
                int ndx = routeWindow.DetailStack.Children.IndexOf(_part);

                if (part.PropsDict.Count > 0) foreach (var key in part.PropsDict.Keys)
                {
                    switch (key)
                    {
                        case 51:
                            while (ndx > routeWindow.CutStack.Children.Count) routeWindow.CutStack.Children.Add(new PlugControl());
                            routeWindow.CutStack.Children.Insert(ndx, new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        case 52:
                            while (ndx > routeWindow.BendStack.Children.Count) routeWindow.BendStack.Children.Add(new PlugControl());
                            routeWindow.BendStack.Children.Insert(ndx, new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        case 53:
                            while (ndx > routeWindow.WeldStack.Children.Count) routeWindow.WeldStack.Children.Add(new PlugControl());
                            routeWindow.WeldStack.Children.Add(new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        case 54:
                            while (ndx > routeWindow.PaintStack.Children.Count) routeWindow.PaintStack.Children.Add(new PlugControl());
                            routeWindow.PaintStack.Children.Add(new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        case 55:
                            while (ndx > routeWindow.ThreadStack.Children.Count) routeWindow.ThreadStack.Children.Add(new PlugControl());
                            routeWindow.ThreadStack.Children.Add(new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        case 56:
                            while (ndx > routeWindow.CountersinkStack.Children.Count) routeWindow.CountersinkStack.Children.Add(new PlugControl());
                            routeWindow.CountersinkStack.Children.Add(new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        case 57:
                            while (ndx > routeWindow.DrillingStack.Children.Count) routeWindow.DrillingStack.Children.Add(new PlugControl());
                            routeWindow.DrillingStack.Children.Add(new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        case 58:
                            while (ndx > routeWindow.RollStack.Children.Count) routeWindow.RollStack.Children.Add(new PlugControl());
                            routeWindow.RollStack.Children.Add(new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        case 59:
                            while (ndx > routeWindow.ExtraPStack.Children.Count) routeWindow.ExtraPStack.Children.Add(new PlugControl());
                            routeWindow.ExtraPStack.Children.Add(new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        case 60:
                            while (ndx > routeWindow.ExtraLStack.Children.Count) routeWindow.ExtraLStack.Children.Add(new PlugControl());
                            routeWindow.ExtraLStack.Children.Add(new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        case 61:
                            while (ndx > routeWindow.PipeStack.Children.Count) routeWindow.PipeStack.Children.Add(new PlugControl());
                            routeWindow.PipeStack.Children.Add(new PartViewControl(part) { Margin = new Thickness(5) });
                            break;
                        default:
                            break;

                    }
                }

                TextBox _comment = new()
                {
                    Width = 100,
                    Height = 70,
                    Margin = new Thickness(5),
                    Text = $"s{part.Destiny} {part.Metal}\n{part.Mass} кг",
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };
                routeWindow.CommentStack.Children.Add(_comment);
            }

            foreach (var item in routeWindow.WorkStack.Children)
                if (item is StackPanel stack)
                    while (stack.Children.Count < routeWindow.DetailStack.Children.Count)
                        stack.Children.Add(new PlugControl());

            routeWindow.Show();
            StatusBegin($"Создан маршрут производства для расчета {ActiveOffer?.N}", StatusMessageType.Success);
        }

        //-СПЕЦИФИКАЦИЯ
        private void CreateSpec(object sender, RoutedEventArgs e)
        {
            if (ActiveOffer is null)
            {
                StatusBegin("Для создания спецификации необходимо загрузить расчет.", StatusMessageType.Error);
                return;
            }
            else if (CustomerDrop.SelectedItem is not Customer)
            {
                StatusBegin("Не удалось определить заказчика для создания спецификации. Добавьте заказчика в базу.", StatusMessageType.Error);
                return;
            }
            else if (ActiveOffer.Act is null || !File.Exists(ActiveOffer.Act))
            {
                StatusBegin("Не удалось найти файл КП для создания спецификации. Попробуйте пересохранить расчет заново.", StatusMessageType.Error);
                return;
            }

            UpdatePricePart();

            SpecWindow specWindow = new(ActiveOffer.Act) { Title = $"Спецификация на КП № {ActiveOffer.N}" };
            specWindow.Show();
        }
        #endregion


        //-------------Отчет по продажам-----------------//
        #region
        private void CreateManagerReport(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedMonth = ReportDrop.SelectedItem as string;
                if (string.IsNullOrEmpty(selectedMonth))
                {
                    MessageBox.Show("Выберите месяц");
                    return;
                }

                // Находим индекс месяца (0 = январь, 11 = декабрь)
                int monthIndex = Array.IndexOf(Months, selectedMonth.ToLower());
                if (monthIndex == -1)
                {
                    MessageBox.Show($"Неизвестный месяц: {selectedMonth}");
                    return;
                }

                // Получаем норму часов
                int normHours = WorkingHours[monthIndex];

                SaveFileDialog saveFileDialog = new() { FileName = $"Отчет за {selectedMonth}" };

                if (saveFileDialog.ShowDialog() == true && !string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    // Передаём normHours в ManagerReport
                    bool _report = ManagerReport(saveFileDialog.FileName, normHours);
                    if (_report)
                        StatusBegin($"Создан отчёт менеджера за {selectedMonth}");
                    else
                        StatusBegin($"Нет расчётов за выбранный период");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private const string BonusRatioPattern = @"""BonusRatio""\s*:\s*([\d.]+)";
        private const string NoBonusMarker = "без бонуса";

        private const decimal VatRateServices = 1.30m;      // Маржа 30% для услуг
        private const decimal VatRateMaterial = 1.15m;      // Маржа 15% для материалов
        private const decimal ProfitMargin = 1.22m;         // НДС 22% для ООО
        private const decimal ProfitMarginForIp = 1.17m;    // НДС 17% для ИП

        private const decimal BonusOooThreshold = 200_000m;
        private const decimal BonusOooRate = 0.15m;
        private const decimal BonusIpFactor = 5m * ProfitMarginForIp / (ProfitMarginForIp - 1m);

        private ReportResult? _currentReport;

        public decimal NormWorkingHours { get; set; } = 180m;
        public decimal ActualWorkingHours { get; set; } = 180m;

        private async System.Threading.Tasks.Task ReportView()
        {
            if (ReportOffers == null || ReportOffers.Count == 0)
            {
                Plan.Text = BonusOOO.Text = BonusIP.Text = Salary.Text = "—";
                return;
            }

            StatusBegin("Формирование отчета...", StatusMessageType.Info);

            _currentReport = BuildReport(ReportOffers);
            UpdateReportUi((_currentReport.Plan, _currentReport.BonusOoo, _currentReport.BonusIp, _currentReport.TotalSalary));

            // ⭐ Принудительное обновление таблицы (чтобы не пришлось прокручивать)
            await Dispatcher.InvokeAsync(() =>
            {
                var currentSource = ReportGrid.ItemsSource;
                ReportGrid.ItemsSource = null;
                ReportGrid.ItemsSource = currentSource;
            });
        }

        private void UpdateReportUi((decimal Plan, decimal BonusOoo, decimal BonusIp, decimal TotalSalary) result)
        {
            // Форматирование без дробной части и с разделителями (если нужно — можно убрать)
            Plan.Text = result.Plan.ToString("N0");
            Plan.BorderBrush = result.Plan >= BonusOooThreshold ? Brushes.Green : Brushes.Red;

            BonusOOO.Text = result.BonusOoo.ToString("N0");
            BonusIP.Text = result.BonusIp.ToString("N0");
            Salary.Text = result.TotalSalary.ToString("N0");
        }

        private ReportResult BuildReport(ObservableCollection<Offer> offers)
        {
            var result = new ReportResult();
            var regex = new Regex(BonusRatioPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            foreach (var offer in offers)
            {
                if (offer == null) continue;

                bool isAgent = offer.Agent == true;
                string? invoice = offer.Invoice;

                // Определяем, есть ли пометка "без бонуса"
                bool isNoBonus = isAgent && !string.IsNullOrEmpty(invoice) &&
                                 invoice.Contains(NoBonusMarker, StringComparison.OrdinalIgnoreCase);

                // Округляем исходные суммы вверх (как в Excel)
                decimal servicesGross = (decimal)Math.Ceiling(offer.Services);
                decimal materialGross = (decimal)Math.Ceiling(offer.Material);
                decimal amountGross = (decimal)Math.Ceiling(offer.Amount);

                // Извлекаем бонусный процент
                decimal bonusRatio = 0;
                if (!string.IsNullOrEmpty(offer.Data))
                {
                    var match = regex.Match(offer.Data);
                    if (match.Success)
                    {
                        // Нормализуем: заменяем запятую на точку для парсинга
                        string ratioStr = match.Groups[1].Value.Replace(",", ".");
                        if (decimal.TryParse(ratioStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRatio))
                        {
                            bonusRatio = parsedRatio;
                        }
                    }
                }

                // Общая сумма для расчёта пропорции (не может быть 0, если есть бонус)
                decimal totalGross = servicesGross + materialGross;

                // Рассчитываем бонусную часть от услуг и материала
                decimal servicesBonus = 0;
                decimal materialBonus = 0;

                if (bonusRatio > 0 && totalGross > 0)
                {
                    // Пропорциональное распределение бонуса
                    servicesBonus = Math.Ceiling(servicesGross * bonusRatio / (100 + bonusRatio));
                    materialBonus = Math.Ceiling(materialGross * bonusRatio / (100 + bonusRatio));
                }

                // Чистые суммы (остаются в компании)
                decimal servicesNet = servicesGross - servicesBonus;
                decimal materialNet = materialGross - materialBonus;
                decimal bonusAmount = Math.Ceiling(amountGross - amountGross / (1 + bonusRatio / 100));

                // Создаём элемент для Excel
                var item = new ReportOfferItem
                {
                    CreatedDate = offer.CreatedDate,
                    EndDate = offer.EndDate,
                    Invoice = invoice,
                    Company = offer.Company,
                    Order = offer.Order,
                    N = offer.N,
                    IsAgent = isAgent,
                    Services = servicesGross,
                    Material = materialGross,
                    Amount = amountGross,
                    ServicesNet = servicesNet,
                    MaterialNet = materialNet,
                    BonusRatio = bonusRatio,
                    BonusAmount = bonusAmount,
                    IsNoBonus = isNoBonus
                };

                if (isAgent)
                {
                    result.IpItems.Add(item);
                    result.TotalServicesIp += servicesNet;
                    result.TotalMaterialIp += materialNet;
                    result.TotalBonusIp += bonusAmount;
                    if (isNoBonus)
                        result.NoBonusAmount += amountGross; // ← именно amountGross, как в Excel
                    result.TotalAmountIp += amountGross;
                }
                else
                {
                    result.OooItems.Add(item);
                    result.TotalServicesOoo += servicesNet;
                    result.TotalMaterialOoo += materialNet;
                    result.TotalBonusOoo += bonusAmount;
                    result.TotalAmountOoo += amountGross;
                }
            }

            // === Расчёт ЧИСТОЙ прибыли (только от чистых сумм, без бонусов) ===
            decimal profitServicesOoo = (result.TotalServicesOoo - result.TotalServicesOoo / VatRateServices) / ProfitMargin;
            decimal profitMaterialOoo = (result.TotalMaterialOoo - result.TotalMaterialOoo / VatRateMaterial) / ProfitMargin;
            decimal profitServicesIp = (result.TotalServicesIp - result.TotalServicesIp / VatRateServices) / ProfitMargin;
            decimal profitMaterialIp = (result.TotalMaterialIp - result.TotalMaterialIp / VatRateMaterial) / ProfitMargin;

            result.CleanProfit = profitServicesOoo + profitMaterialOoo + profitServicesIp + profitMaterialIp;

            // === План = Чистая прибыль + все бонусы (как отдельная надбавка) ===
            result.Plan = Math.Ceiling(result.CleanProfit + result.TotalBonusOoo + result.TotalBonusIp);

            // === Бонус за ООО (сверхплановый) ===
            result.BonusOoo = result.Plan >= BonusOooThreshold
                ? Math.Ceiling((result.Plan - BonusOooThreshold) * BonusOooRate)
                : 0;

            // === Бонус ИП ===
            // Используем: (общая сумма расчетов ИП - "без бонуса") / расчетное значение от НДС
            decimal ipBaseForBonus = result.TotalAmountIp - result.NoBonusAmount;
            result.BonusIp = Math.Ceiling(ipBaseForBonus / BonusIpFactor);

            // === Итоговая зарплата ===
            decimal baseSalary = 30000m;
            decimal planBonus = result.Plan >= BonusOooThreshold ? 20000m : 0;
            result.TotalSalary = baseSalary + planBonus + result.BonusOoo + result.BonusIp;

            return result;
        }

        public bool ManagerReport(string path, int normHours)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            // Если вызван из UI — используем уже посчитанный отчёт
            ReportResult report;
            if (_currentReport != null)
            {
                report = _currentReport;
            }
            else
            {
                report = BuildReport(ReportOffers);
            }

            string VatRateServicesStr = VatRateServices.ToString(CultureInfo.InvariantCulture);
            string VatRateMaterialStr = VatRateMaterial.ToString(CultureInfo.InvariantCulture);
            string ProfitMarginStr = ProfitMargin.ToString(CultureInfo.InvariantCulture);
            string ProfitMarginForIpStr = ProfitMarginForIp.ToString(CultureInfo.InvariantCulture);
            string BonusOooThresholdStr = BonusOooThreshold.ToString(CultureInfo.InvariantCulture);
            string BonusOooRateStr = BonusOooRate.ToString(CultureInfo.InvariantCulture);

            // --- Создание Excel ---
            using var workbook = new ExcelPackage();
            var worksheet = workbook.Workbook.Worksheets.Add("Лист1");

            // === Заглушка для пустых секций (используется, если OooItems или IpItems пусты) ===
            int stubRow = 1000;
            worksheet.Cells[stubRow, 5].Value = 0;    // totalS / services bonus
            worksheet.Cells[stubRow, 6].Value = 0;    // totalM / material bonus
            worksheet.Cells[stubRow, 7].Value = 0;    // total
            worksheet.Cells[stubRow, 9].Value = 0;    // bonus
            worksheet.Cells[stubRow, 13].Value = 0;   // services net
            worksheet.Cells[stubRow, 14].Value = 0;   // material net
            worksheet.Cells[stubRow, 17].Value = 0;   // notbonus

            int row = 1;
            var _headers = new List<string> { $"дата отгрузки", "№счета", "проект", "№заказа", "работа", "металл", "Итого", "%", "бонус", "№КП" };

            // === ООО ===
            if (report.OooItems.Count > 0)
            {
                worksheet.Cells[row, 1].Value = "ООО";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                row++;

                WriteHeaders(worksheet, row, _headers);
                row++;

                foreach (var item in report.OooItems)
                {
                    worksheet.Cells[row, 1].Value = item.EndDate;
                    worksheet.Cells[row, 1].Style.Numberformat.Format = "d MMM";
                    worksheet.Cells[row, 2].Value = item.Invoice;
                    worksheet.Cells[row, 3].Value = item.Company;
                    worksheet.Cells[row, 4].Value = item.Order;
                    worksheet.Cells[row, 5].Value = item.Services;     // исходные
                    worksheet.Cells[row, 6].Value = item.Material;    // исходные
                    worksheet.Cells[row, 7].Value = item.Amount;      // исходные
                    worksheet.Cells[row, 8].Value = item.BonusRatio;  // из C#
                    worksheet.Cells[row, 10].Value = item.N;

                    // Формулы
                    if (item.BonusRatio > 0)
                    {
                        worksheet.Cells[row, 9].Formula = $"=ROUND(G{row} * H{row} / (100 + H{row}), 0)";
                        worksheet.Cells[row, 11].Formula = $"=ROUND(E{row} * H{row} / (100 + H{row}), 0)";
                        worksheet.Cells[row, 12].Formula = $"=ROUND(F{row} * H{row} / (100 + H{row}), 0)";
                    }
                    else
                    {
                        worksheet.Cells[row, 9].Value = 0;
                        worksheet.Cells[row, 11].Value = 0;
                        worksheet.Cells[row, 12].Value = 0;
                    }

                    worksheet.Cells[row, 13].Formula = $"=E{row} - K{row}"; // чистые услуги
                    worksheet.Cells[row, 14].Formula = $"=F{row} - L{row}"; // чистый материал

                    row++;
                }

                // Итоги ООО
                int oooStart = row - report.OooItems.Count;
                int oooEnd = row - 1;

                DefineName(worksheet, "totalS1", oooStart, oooEnd, 5);
                DefineName(worksheet, "totalM1", oooStart, oooEnd, 6);
                DefineName(worksheet, "total1", oooStart, oooEnd, 7);
                DefineName(worksheet, "bonus1", oooStart, oooEnd, 9);
                DefineName(worksheet, "services1", oooStart, oooEnd, 13);
                DefineName(worksheet, "material1", oooStart, oooEnd, 14);

                worksheet.Cells[row, 5].Formula = "=SUM(totalS1)";
                worksheet.Cells[row, 6].Formula = "=SUM(totalM1)";
                worksheet.Cells[row, 7].Formula = "=SUM(total1)";
                worksheet.Cells[row, 9].Formula = "=SUM(bonus1)";
                worksheet.Cells[row, 13].Formula = "=SUM(services1)";
                worksheet.Cells[row, 14].Formula = "=SUM(material1)";

                // Прибыль по формуле
                worksheet.Cells[row, 15].Formula = $"=(SUM(totalS1)-SUM(totalS1)/{VatRateServicesStr})/{ProfitMarginStr}";
                worksheet.Cells[row, 16].Formula = $"=(SUM(totalM1)-SUM(totalM1)/{VatRateMaterialStr})/{ProfitMarginStr}";

                // Стили
                worksheet.Cells[oooStart, 1, row, 10].Style.Border.BorderAround(ExcelBorderStyle.Medium);
                worksheet.Cells[row, 5, row, 10].Style.Font.Bold = true;
                worksheet.Cells[row, 15, row, 16].Style.Fill.SetBackground(System.Drawing.Color.LightGreen);

                row++;
            }
            else
            {
                // ⭐ Секция пуста — определяем имена, ссылающиеся на нулевую заглушку
                DefineName(worksheet, "totalS1", stubRow, stubRow, 5);
                DefineName(worksheet, "totalM1", stubRow, stubRow, 6);
                DefineName(worksheet, "total1", stubRow, stubRow, 7);
                DefineName(worksheet, "bonus1", stubRow, stubRow, 9);
                DefineName(worksheet, "services1", stubRow, stubRow, 13);
                DefineName(worksheet, "material1", stubRow, stubRow, 14);
            }

            // === ИП и ПК ===
            if (report.IpItems.Count > 0)
            {
                worksheet.Cells[row, 1].Value = "ИП и ПК";
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                row++;

                WriteHeaders(worksheet, row, _headers);
                row++;

                foreach (var item in report.IpItems)
                {
                    worksheet.Cells[row, 1].Value = item.EndDate;
                    worksheet.Cells[row, 1].Style.Numberformat.Format = "d MMM";
                    worksheet.Cells[row, 2].Value = item.Invoice;
                    worksheet.Cells[row, 3].Value = item.Company;
                    worksheet.Cells[row, 4].Value = item.Order;
                    worksheet.Cells[row, 5].Value = item.Services;
                    worksheet.Cells[row, 6].Value = item.Material;
                    worksheet.Cells[row, 7].Value = item.Amount;
                    worksheet.Cells[row, 8].Value = item.BonusRatio;
                    worksheet.Cells[row, 9].Value = item.BonusAmount;
                    worksheet.Cells[row, 10].Value = item.N;

                    // Детальные расчёты
                    decimal servicesBonus = item.Services * item.BonusRatio / (100 + item.BonusRatio);
                    decimal materialBonus = item.Material * item.BonusRatio / (100 + item.BonusRatio);
                    worksheet.Cells[row, 11].Value = Math.Ceiling(servicesBonus);
                    worksheet.Cells[row, 12].Value = Math.Ceiling(materialBonus);
                    worksheet.Cells[row, 13].Value = item.ServicesNet; // ← чистые (для расчёта прибыли)
                    worksheet.Cells[row, 14].Value = item.MaterialNet;

                    // Только если "без бонуса" — фиксируем для вычета
                    if (item.IsNoBonus)
                        worksheet.Cells[row, 17].Value = item.Amount;

                    row++;
                }

                int ipStart = row - report.IpItems.Count;
                int ipEnd = row - 1;

                DefineName(worksheet, "totalS2", ipStart, ipEnd, 5);
                DefineName(worksheet, "totalM2", ipStart, ipEnd, 6);
                DefineName(worksheet, "total2", ipStart, ipEnd, 7);
                DefineName(worksheet, "bonus2", ipStart, ipEnd, 9);
                DefineName(worksheet, "services2", ipStart, ipEnd, 13);
                DefineName(worksheet, "material2", ipStart, ipEnd, 14);
                DefineName(worksheet, "notbonus", ipStart, ipEnd, 17);

                worksheet.Cells[row, 5].Formula = "=SUM(totalS2)";
                worksheet.Cells[row, 6].Formula = "=SUM(totalM2)";
                worksheet.Cells[row, 7].Formula = "=SUM(total2)";
                worksheet.Cells[row, 9].Formula = "=SUM(bonus2)";
                worksheet.Cells[row, 13].Formula = "=SUM(services2)";
                worksheet.Cells[row, 14].Formula = "=SUM(material2)";
                worksheet.Cells[row, 17].Formula = "=SUM(notbonus)";

                worksheet.Cells[row, 15].Formula = $"=(SUM(totalS2)-SUM(totalS2)/{VatRateServicesStr})/{ProfitMarginStr}";
                worksheet.Cells[row, 16].Formula = $"=(SUM(totalM2)-SUM(totalM2)/{VatRateMaterialStr})/{ProfitMarginStr}";

                worksheet.Cells[ipStart, 1, row, 10].Style.Border.BorderAround(ExcelBorderStyle.Medium);
                worksheet.Cells[row, 5, row, 10].Style.Font.Bold = true;
                worksheet.Cells[row, 15, row, 16].Style.Fill.SetBackground(System.Drawing.Color.LightGreen);

                row++;
            }
            else
            {
                // ⭐ Секция пуста — определяем имена, ссылающиеся на нулевую заглушку
                DefineName(worksheet, "totalS2", stubRow, stubRow, 5);
                DefineName(worksheet, "totalM2", stubRow, stubRow, 6);
                DefineName(worksheet, "total2", stubRow, stubRow, 7);
                DefineName(worksheet, "bonus2", stubRow, stubRow, 9);
                DefineName(worksheet, "services2", stubRow, stubRow, 13);
                DefineName(worksheet, "material2", stubRow, stubRow, 14);
                DefineName(worksheet, "notbonus", stubRow, stubRow, 17); // Не забываем про notbonus!
            }

            // === Прибыль месяца (с разделением на "Чист" и "Устар") ===
            row++;
            worksheet.Cells[row, 1].Value = "Прибыль месяца:";
            worksheet.Cells[row, 2].Formula =
                $"=ROUND(" +
                $"(SUM(services1)-SUM(services1)/{VatRateServicesStr})/{ProfitMarginStr}" +
                $"+(SUM(material1)-SUM(material1)/{VatRateMaterialStr})/{ProfitMarginStr}" +
                $"+(SUM(services2)-SUM(services2)/{VatRateServicesStr})/{ProfitMarginStr}" +
                $"+(SUM(material2)-SUM(material2)/{VatRateMaterialStr})/{ProfitMarginStr}" +
                $"+SUM(bonus1)+SUM(bonus2)" +
                $", 0)";
            worksheet.Cells[row, 2].Style.Font.Bold = true;

            // === Общие итоги ===
            worksheet.Cells[row, 4].Value = "ИТОГО:";
            worksheet.Cells[row, 5].Formula = "=SUM(totalS1)+SUM(totalS2)";
            worksheet.Cells[row, 6].Formula = "=SUM(totalM1)+SUM(totalM2)";
            worksheet.Cells[row, 7].Formula = "=SUM(total1)+SUM(total2)";
            worksheet.Cells[row, 9].Formula = "=SUM(bonus1)+SUM(bonus2)";

            // Чистая прибыль (без бонусов в базе) — ОСНОВНАЯ
            worksheet.Cells[row, 11].Value = "Чист:";
            worksheet.Cells[row, 12].Formula =
                $"=ROUND(" +
                    $"(SUM(services1)-SUM(services1)/{VatRateServicesStr})/{ProfitMarginStr}" +
                    $"+(SUM(material1)-SUM(material1)/{VatRateMaterialStr})/{ProfitMarginStr}" +
                    $"+(SUM(services2)-SUM(services2)/{VatRateServicesStr})/{ProfitMarginStr}" +
                    $"+(SUM(material2)-SUM(material2)/{VatRateMaterialStr})/{ProfitMarginStr}" +
                ", 0)";

            // Устаревший расчёт (для сравнения/проверки)
            worksheet.Cells[row, 13].Value = "Устар:";
            worksheet.Cells[row, 14].Formula =
                $"=ROUND(" +
                    $"(SUM(totalS1)-SUM(totalS1)/{VatRateServicesStr})/{ProfitMarginStr}" +
                    $"+(SUM(totalS2)-SUM(totalS2)/{VatRateServicesStr})/{ProfitMarginStr}" +
                    $"+(SUM(totalM1)-SUM(totalM1)/{VatRateMaterialStr})/{ProfitMarginStr}" +
                    $"+(SUM(totalM2)-SUM(totalM2)/{VatRateMaterialStr})/{ProfitMarginStr}" +
                ", 0)";

            worksheet.Cells[row, 1, row, 14].Style.Fill.SetBackground(System.Drawing.Color.LightPink);
            int totalRow = row;
            row += 3;


            // === Учёт рабочего времени ===
            worksheet.Cells[row, 1].Value = "Норма часов:";
            worksheet.Cells[row, 2].Value = "Факт (ч):";
            worksheet.Cells[row, 1, row, 2].Style.Fill.SetBackground(System.Drawing.Color.LightGray);
            row++;

            worksheet.Cells[row, 1].Value = worksheet.Cells[row, 2].Value = normHours;
            int normRow = row;
            row++;

            worksheet.Cells[row, 1].Value = "Стоимость часа:";
            worksheet.Cells[row, 2].Value = "Вычет из оклада:";
            worksheet.Cells[row, 1, row, 2].Style.Fill.SetBackground(System.Drawing.Color.LightGray);
            row++;

            worksheet.Cells[row, 1].Formula = $"=IF(A{normRow}>0, 50000 / A{normRow}, 0)";
            worksheet.Cells[row, 2].Formula = $"=MAX(0, A{normRow} - B{normRow}) * A{normRow + 2}";
            int adjustedBaseRow = row;

            worksheet.Cells[normRow - 1, 1, row, 2].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            worksheet.Cells[normRow - 1, 1, row, 2].Style.Numberformat.Format = "0";
            row += 2;


            // === Расчёт зарплаты ===
            int salaryRow = row;

            worksheet.Cells[salaryRow, 1].Value = "Доп бонус за ИП и ПК:";
            worksheet.Cells[salaryRow, 2].Formula =
                $"=(SUM(total2)-SUM(notbonus)) / (5 * {ProfitMarginForIpStr} / ({ProfitMarginForIpStr} - 1))";
            worksheet.Cells[salaryRow, 2].Style.Numberformat.Format = "0";
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightBlue);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "Оклад:";
            worksheet.Cells[salaryRow, 2].Formula = $"=ROUND(MAX(0, 30000 - B{adjustedBaseRow}), 0)";
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightBlue);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "Премия за план:";
            worksheet.Cells[salaryRow, 2].Formula = $"=IF(B{totalRow}>={BonusOooThresholdStr}, 20000, 0)";
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightBlue);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "%:";
            worksheet.Cells[salaryRow, 2].Formula = $"=IF(B{totalRow}>={BonusOooThresholdStr}, ROUND((B{totalRow}-{BonusOooThresholdStr})*{BonusOooRateStr}, 0), 0)";
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightBlue);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "Аванс:";
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightYellow);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "На карту:";
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightYellow);
            salaryRow += 2;

            worksheet.Cells[salaryRow, 1].Value = "Итоговая за месяц:";
            worksheet.Cells[salaryRow, 2].Formula = $"=ROUND(SUM(B{row}:B{row + 3}), 0)";
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.GreenYellow);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "К доплате:";
            worksheet.Cells[salaryRow, 2].Formula = $"=ROUND(SUM(B{row}:B{row + 3})-SUM(B{row + 4}:B{row + 6}), 0)";
            worksheet.Cells[salaryRow, 2].Style.Font.Color.SetColor(System.Drawing.Color.Red);
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.GreenYellow);

            var table = worksheet.Cells[row, 1, salaryRow, 2];
            table.Style.Border.Right.Style = table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            table.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            // Скрыть вспомогательные столбцы
            for (int col = 11; col <= 14; col++)
                worksheet.Column(col).Hidden = true;

            worksheet.Cells.AutoFitColumns();

            // Скрываем строку-заглушку, чтобы её не было видно в файле
            worksheet.Row(stubRow).Hidden = true;

            workbook.SaveAs(path + ".xlsx");
            return true;
        }

        private void WriteHeaders(ExcelWorksheet ws, int row, List<string> headers)
        {
            for (int i = 0; i < headers.Count; i++)
                ws.Cells[row, i + 1].Value = headers[i];
            ws.Cells[row, 1, row, headers.Count].Style.Fill.SetBackground(System.Drawing.Color.LightGray);
        }
        private void DefineName(ExcelWorksheet ws, string name, int startRow, int endRow, int col)
        {
            if (startRow <= endRow)
                ws.Names.Add(name, ws.Cells[startRow, col, endRow, col]);
            else
                ws.Names.Add(name, ws.Cells[1, 1]); // пустой диапазон
        }


        // ⭐ Обработчик кнопки синхронизации
        private async void SyncWithCrm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Выберите отчет из CRM",
                    Filter = "Excel файлы|*.xls;*.xlsx|Все файлы|*.*",
                    DefaultExt = ".xls"
                };

                bool? result = openFileDialog.ShowDialog();
                if (result != true) return;

                string crmFilePath = openFileDialog.FileName;
                StatusBegin("Синхронизация с CRM отчетом...", StatusMessageType.Info);

                // ⭐ СНАЧАЛА: читаем HTML и собираем номера заказов
                var crmOrderNumbers = await System.Threading.Tasks.Task.Run(() => ReadCrmOrderNumbers(crmFilePath));

                if (crmOrderNumbers.Count == 0)
                {
                    StatusBegin("В CRM отчете не найдено номеров заказов", StatusMessageType.Warning);
                    return;
                }

                // ⭐ ПОТОМ: создаём .xlsx с выделением отсутствующих строк
                await System.Threading.Tasks.Task.Run(() => HighlightCrmReport(crmFilePath, crmOrderNumbers));

                // ⭐ И В КОНЦЕ: автоматическая отгрузка расчётов, которые есть в CRM, но не отгружены
                await AutoShipOffersFromCrmAsync(crmOrderNumbers);

                StatusBegin("Синхронизация завершена", StatusMessageType.Success);
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка синхронизации: {ex.Message}", StatusMessageType.Error);
                Trace.WriteLine($"❌ Ошибка SyncWithCrm_Click: {ex.Message}");
            }
        }

        /// <summary>
        /// Читает HTML-отчет из CRM и возвращает HashSet номеров заказов.
        /// </summary>
        private HashSet<string> ReadCrmOrderNumbers(string filePath)
        {
            var sourceTable = ReadHtmlTable(filePath);

            // ⭐ Находим столбец "№заказа"
            int orderColumnIndex = -1;
            for (int col = 0; col < sourceTable.Columns.Count; col++)
            {
                string colName = sourceTable.Columns[col].ColumnName?.Trim() ?? "";
                if (colName.Equals("№заказа", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("№ заказа", StringComparison.OrdinalIgnoreCase))
                {
                    orderColumnIndex = col;
                    break;
                }
            }

            if (orderColumnIndex < 0)
                throw new Exception("Не найден столбец '№заказа' в файле");

            // ⭐ Собираем все номера заказов из CRM (строгое сравнение)
            var crmOrders = new HashSet<string>(StringComparer.Ordinal);
            foreach (DataRow row in sourceTable.Rows)
            {
                var orderNumber = row[orderColumnIndex]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(orderNumber)) continue;
                crmOrders.Add(orderNumber);
            }

            Trace.WriteLine($"📊 В CRM отчете найдено {crmOrders.Count} уникальных номеров заказов");
            return crmOrders;
        }

        /// <summary>
        /// Создаёт .xlsx файл с выделением отсутствующих строк.
        /// </summary>
        private void HighlightCrmReport(string crmFilePath, HashSet<string> crmOrderNumbers)
        {
            // ⭐ Собираем номера заказов из отчета приложения
            var appOrderNumbers = new HashSet<string>(StringComparer.Ordinal);
            if (_currentReport != null)
            {
                foreach (var item in _currentReport.OooItems)
                    if (!string.IsNullOrEmpty(item.Order))
                        appOrderNumbers.Add(item.Order.Trim());
                foreach (var item in _currentReport.IpItems)
                    if (!string.IsNullOrEmpty(item.Order))
                        appOrderNumbers.Add(item.Order.Trim());
            }

            // ⭐ Читаем HTML-таблицу
            var sourceTable = ReadHtmlTable(crmFilePath);

            int orderColumnIndex = -1;
            for (int col = 0; col < sourceTable.Columns.Count; col++)
            {
                string colName = sourceTable.Columns[col].ColumnName?.Trim() ?? "";
                if (colName.Equals("№заказа", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("№ заказа", StringComparison.OrdinalIgnoreCase))
                {
                    orderColumnIndex = col;
                    break;
                }
            }

            if (orderColumnIndex < 0)
                throw new Exception("Не найден столбец '№заказа' в файле");

            // ⭐ Создаём .xlsx через EPPlus
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            string newFilePath = Path.ChangeExtension(crmFilePath, ".xlsx");
            if (File.Exists(newFilePath)) File.Delete(newFilePath);

            int highlightedCount = 0;
            int totalRows = 0;

            using (var package = new OfficeOpenXml.ExcelPackage(new FileInfo(newFilePath)))
            {
                var worksheet = package.Workbook.Worksheets.Add("CRM Report");

                // ⭐ Копируем все данные
                for (int row = 0; row < sourceTable.Rows.Count; row++)
                {
                    for (int col = 0; col < sourceTable.Columns.Count; col++)
                    {
                        var cellValue = sourceTable.Rows[row][col]?.ToString() ?? "";
                        worksheet.Cells[row + 1, col + 1].Value = cellValue;
                    }
                }

                // ⭐ Выделяем отсутствующие строки желтым
                for (int row = 0; row < sourceTable.Rows.Count; row++)
                {
                    var orderNumber = sourceTable.Rows[row][orderColumnIndex]?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(orderNumber)) continue;

                    // Пропускаем заголовки секций
                    if (orderNumber.Equals("№заказа", StringComparison.OrdinalIgnoreCase) ||
                        orderNumber.Equals("№ заказа", StringComparison.OrdinalIgnoreCase))
                        continue;

                    totalRows++;

                    if (!appOrderNumbers.Contains(orderNumber))
                    {
                        var rowRange = worksheet.Cells[row + 1, 1, row + 1, sourceTable.Columns.Count];
                        rowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Yellow);

                        highlightedCount++;
                        Trace.WriteLine($"⚠️ Заказ {orderNumber} не найден (строка {row + 1})");
                    }
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                package.Save();
            }

            Trace.WriteLine($"✅ Создан файл: {newFilePath}");
            Trace.WriteLine($"✅ Выделено {highlightedCount} из {totalRows} строк");

            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"Синхронизация завершена!\n\n" +
                    $"Найдено в приложении: {appOrderNumbers.Count} заказов\n" +
                    $"Проверено в CRM: {totalRows} строк\n" +
                    $"Выделено: {highlightedCount} строк\n\n" +
                    $"Новый файл сохранён:\n{newFilePath}",
                    "Синхронизация с CRM",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        }

        /// <summary>
        /// Автоматически отгружает расчёты, которые есть в CRM, но ещё не отгружены в приложении.
        /// </summary>
        private async System.Threading.Tasks.Task AutoShipOffersFromCrmAsync(HashSet<string> crmOrderNumbers)
        {
            if (TargetManager.Name is null) return;

            var allProductionOffers = await DataService.GetOffersInProductionAsync(
                        TargetManager.Id, TargetManager.Name);

            // ⭐ Находим расчёты, которые:
            // 1. Имеют заполненный Order (уже отфильтровано в GetOffersInProductionAsync, но оставим для надёжности)
            // 2. Ещё не отгружены (EndDate == null)
            // 3. Их Order есть в CRM-отчете (строгое сравнение)
            var offersToShip = allProductionOffers
                .Where(o => !string.IsNullOrEmpty(o.Order)
                         && o.EndDate == null
                         && crmOrderNumbers.Contains(o.Order.Trim()))
                .ToList();

            if (offersToShip.Count == 0)
            {
                Trace.WriteLine("ℹ️ Нет расчётов для автоматической отгрузки");
                return;
            }

            // ⭐ Показываем список пользователю для подтверждения
            string preview = string.Join("\n", offersToShip.Take(20).Select(o => $"• №{o.N} (заказ {o.Order}) — {o.Company}"));
            if (offersToShip.Count > 20)
                preview += $"\n... и ещё {offersToShip.Count - 20} расчётов";

            var response = MessageBox.Show(
                $"Обнаружено {offersToShip.Count} расчётов, которые есть в CRM, но ещё не отгружены.\n\n" +
                $"{preview}\n\n" +
                $"Отметить их как отгруженные (установить дату отгрузки = сегодня)?",
                "Автоматическая отгрузка",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (response != MessageBoxResult.Yes) return;

            StatusBegin($"Отгрузка {offersToShip.Count} расчётов...", StatusMessageType.Info);

            int successCount = 0;
            int failedCount = 0;
            var shippedOffers = new List<Offer>();

            foreach (var offer in offersToShip)
            {
                try
                {
                    // ⭐ Устанавливаем дату отгрузки = сегодня (UTC)
                    offer.EndDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                    // ⭐ Сохраняем в БД
                    bool success = await DataService.UpdateOfferAsync(offer);

                    if (success)
                    {
                        successCount++;
                        shippedOffers.Add(offer);
                        Trace.WriteLine($"✅ Отгружен расчёт {offer.N} (заказ {offer.Order})");
                    }
                    else
                    {
                        failedCount++;
                        offer.EndDate = null; // ⭐ Откатываем изменение при неудаче
                        Trace.WriteLine($"⚠️ Не удалось отгрузить расчёт {offer.N}");
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    offer.EndDate = null; // ⭐ Откатываем изменение при ошибке
                    Trace.WriteLine($"❌ Ошибка отгрузки {offer.N}: {ex.Message}");
                }
            }

            // ⭐ Обновляем представление таблицы расчётов
            if (shippedOffers.Any())
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        OffersView?.Refresh();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"⚠️ Refresh отложен: {ex.Message}");
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { OffersView?.Refresh(); }
                            catch { /* игнорируем */ }
                        }), DispatcherPriority.Loaded);
                    }
                }, DispatcherPriority.Loaded);

                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            }

            // ⭐ Автоматически перестраиваем отчёт, чтобы новые отгрузки попали в него
            if (shippedOffers.Any())
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    foreach (var offer in shippedOffers)
                    {
                        if (!ReportOffers.Any(o => o.Id == offer.Id))
                        {
                            ReportOffers.Add(offer);
                        }
                    }
                    await ReportView();
                });
            }

            // ⭐ Финальное сообщение
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"Автоматическая отгрузка завершена!\n\n" +
                    $"Отгружено: {successCount} расчётов\n" +
                    (failedCount > 0 ? $"Ошибок: {failedCount}\n\n" : "") +
                    $"Отчёт пересчитан с учётом новых отгрузок.",
                    "Автоматическая отгрузка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });

            Trace.WriteLine($"✅ Автоотгрузка: успешно={successCount}, ошибок={failedCount}");
        }

        /// <summary>
        /// Читает HTML-таблицу из файла CRM и возвращает DataTable.
        /// </summary>
        private DataTable ReadHtmlTable(string filePath)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.Load(filePath, System.Text.Encoding.UTF8);

            var tables = doc.DocumentNode.SelectNodes("//table");
            if (tables == null || tables.Count == 0)
                throw new Exception("В HTML-файле не найдены таблицы");

            Trace.WriteLine($"📄 Найдено таблиц в HTML: {tables.Count}");

            HtmlNode? targetTable = null;
            foreach (var table in tables)
            {
                var headerText = table.InnerText;
                if (headerText.Contains("№заказа") || headerText.Contains("№ заказа"))
                {
                    targetTable = table;
                    break;
                }
            }

            if (targetTable == null)
                throw new Exception("Не найдена таблица с заголовком '№заказа'");

            var dataTable = new DataTable("CrmReport");
            var rows = targetTable.SelectNodes(".//tr");
            if (rows == null || rows.Count == 0)
                throw new Exception("Таблица пуста");

            bool headerAdded = false;
            foreach (var row in rows)
            {
                var cells = row.SelectNodes(".//th|.//td");
                if (cells == null) continue;

                var cellValues = cells.Select(c => System.Net.WebUtility.HtmlDecode(c.InnerText).Trim()).ToArray();

                if (cellValues.All(string.IsNullOrEmpty)) continue;

                if (cellValues.Length == 1 &&
                    (cellValues[0] == "ООО" || cellValues[0] == "ИП и ПК"))
                    continue;

                if (!headerAdded)
                {
                    for (int i = 0; i < cellValues.Length; i++)
                    {
                        string colName = string.IsNullOrEmpty(cellValues[i]) ? $"Col{i}" : cellValues[i];
                        dataTable.Columns.Add(colName);
                    }
                    headerAdded = true;
                }
                else
                {
                    var dataRow = dataTable.NewRow();
                    for (int i = 0; i < Math.Min(cellValues.Length, dataTable.Columns.Count); i++)
                    {
                        dataRow[i] = cellValues[i];
                    }
                    dataTable.Rows.Add(dataRow);
                }
            }

            Trace.WriteLine($"✅ Прочитано строк из HTML: {dataTable.Rows.Count}, столбцов: {dataTable.Columns.Count}");
            return dataTable;
        }
        #endregion


        //-------------Отчеты ---------------------------//
        #region
        private void Report_On_Shipped_Orders(object sender, RoutedEventArgs e) => ShowReport(ReportType.Sales);
        private void Report_On_Production_Orders(object sender, RoutedEventArgs e) => ShowReport(ReportType.Production);
        private void ShowReport(ReportType reportType)
        {
            if (CurrentManager?.Name is null) return;

            // ⭐ 1. Показываем диалог выбора периода (модальный — без него нельзя)
            var periodDialog = new ReportPeriodDialog(CurrentManager.IsAdmin, CurrentManager.Name, reportType)
            {
                Owner = this
            };

            if (periodDialog.ShowDialog() != true)
                return; // Пользователь нажал "Отмена"

            // ⭐ 2. Открываем окно отчета НЕМодально — можно открыть несколько для сравнения
            var previewWindow = new ReportPreviewWindow(
                periodDialog.SelectedFrom,
                periodDialog.SelectedTo,
                reportType)
            {
                Owner = this  // ⭐ Окно будет закрываться вместе с главным
            };

            previewWindow.Show();  // ⭐ Show() вместо ShowDialog()
        }
        #endregion


        //-------------Заказчики----------------//
        #region
        private void CustomerChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomerDrop.SelectedItem is Customer customer)
            {
                IsAgent = customer.Agent;
                if (HasDelivery != false) HasDelivery = false;
                TargetCustomer = customer;
            }
        }

        private async void AddCustomer(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CustomerDrop.Text)) return;

            try
            {
                string customerName = CustomerDrop.Text.Trim();
                StatusBegin($"Проверка и добавление заказчика '{customerName}'...", StatusMessageType.Info);

                int.TryParse(DeliveryPrice.Text, out int delivery);
                bool isEngineer = CurrentManager?.IsEngineer == true;

                var newCustomer = await DataService.AddCustomerAsync(
                    customerName,
                    Adress.Text,
                    IsAgent,
                    delivery,
                    TargetManager.Id,
                    isEngineer);

                if (newCustomer != null)
                {
                    string scope = isEngineer ? "в вашу локальную базу" : "в общую базу";
                    StatusBegin($"Заказчик {newCustomer.Name} успешно добавлен {scope}.", StatusMessageType.Success);
                    await LoadManagerDataAsync(TargetManager);
                }
                else
                {
                    // ⭐ СПЕЦИАЛЬНОЕ СООБЩЕНИЕ О ГЛОБАЛЬНОМ ДУБЛИКАТЕ
                    var (ownerName, actualCustomerName) = await DataService.FindCustomerOwnerInPgAsync(customerName);

                    string message;
                    if (!string.IsNullOrEmpty(ownerName) && !string.IsNullOrEmpty(actualCustomerName))
                    {
                        if (actualCustomerName.Equals(customerName, StringComparison.OrdinalIgnoreCase))
                        {
                            message = $"Заказчик '{customerName}' уже существует в общей базе у менеджера «{ownerName}».\n\n" +
                                      $"Пожалуйста, переименуйте его (например, добавьте город или ИНН) и попробуйте снова.";
                        }
                        else
                        {
                            message = $"Заказчик с похожим именем «{actualCustomerName}» уже существует в общей базе у менеджера «{ownerName}».\n\n" +
                                      $"Вы ввели: '{customerName}'\n" +
                                      $"В базе найдено: '{actualCustomerName}'\n\n" +
                                      $"Пожалуйста, используйте точное имя или переименуйте вашего заказчика.";
                        }
                    }
                    else
                    {
                        message = $"Заказчик '{customerName}' уже существует в общей базе у другого менеджера.\n\n" +
                                  $"Пожалуйста, переименуйте его (например, добавьте город или ИНН) и попробуйте снова.";
                    }

                    StatusBegin(message, StatusMessageType.Warning);

                    CustomerDrop.Focus();
                    CustomerDrop.IsDropDownOpen = false;
                }
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка добавления заказчика: {ex.Message}", StatusMessageType.Error);
            }
        }

        private async void EditCustomer(object sender, RoutedEventArgs e)
        {
            if (CustomerDrop.SelectedItem is not Customer customer)
            {
                StatusBegin("Такого заказчика нет в базе.", StatusMessageType.Warning);
                return;
            }

            // ⭐ Определяем, является ли текущий пользователь владельцем заказчика
            bool isOwner = CurrentManager.Name == TargetManager.Name;

            // ⭐ Если не владелец и не админ — разрешаем только локальное редактирование
            if (!isOwner && !CurrentManager.IsAdmin)
            {
                var response = MessageBox.Show(
                    $"У вас нет прав на изменение этого заказчика в общей базе.\n" +
                    $"Изменения будут сохранены только в вашей локальной базе.\n\nПродолжить?",
                    "Ограничение прав",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (response != MessageBoxResult.Yes) return;
            }
            else
            {
                var response = MessageBox.Show(
                    "Уверены? Данные заказчика будут изменены!",
                    "Редактирование данных заказчика",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (response != MessageBoxResult.Yes) return;
            }

            try
            {
                StatusBegin($"Обновление данных заказчика {customer.Name}...", StatusMessageType.Info);

                var updatedCustomer = new Customer
                {
                    Id = customer.Id,
                    Name = customer.Name,
                    Address = Adress.Text,
                    Agent = IsAgent,
                    DeliveryPrice = int.TryParse(DeliveryPrice.Text, out int delivery) ? delivery : customer.DeliveryPrice,
                    ManagerId = customer.ManagerId
                };

                // ⭐ Передаём isOwner — если не владелец, обновит только локально
                bool success = await DataService.UpdateCustomerAsync(updatedCustomer, isOwner);

                if (success)
                {
                    string scope = isOwner ? "в базе" : "локально";
                    StatusBegin($"Данные заказчика {customer.Name} изменены {scope}.", StatusMessageType.Success);
                    await LoadManagerDataAsync(TargetManager);
                }
                else
                {
                    StatusBegin($"Не удалось изменить данные заказчика {customer.Name}.", StatusMessageType.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка обновления заказчика: {ex.Message}", StatusMessageType.Error);
            }
        }

        private async void DeleteCustomer(object sender, RoutedEventArgs e)
        {
            if (CustomerDrop.SelectedItem is not Customer customer)
            {
                StatusBegin("Такого заказчика нет в базе.", StatusMessageType.Warning);
                return;
            }

            // ⭐ Определяем, является ли текущий пользователь владельцем
            bool isOwner = CurrentManager.Name == TargetManager.Name;

            if (!isOwner && !CurrentManager.IsAdmin)
            {
                var response = MessageBox.Show(
                    $"У вас нет прав на удаление этого заказчика из общей базы.\n" +
                    $"Заказчик будет удалён только из вашей локальной базы.\n\nПродолжить?",
                    "Ограничение прав",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (response != MessageBoxResult.Yes) return;
            }
            else
            {
                var response = MessageBox.Show(
                    $"Уверены? Заказчик \"{customer.Name}\" будет безвозвратно удален!",
                    "Удаление заказчика",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (response != MessageBoxResult.Yes) return;
            }

            try
            {
                StatusBegin($"Удаление заказчика {customer.Name}...", StatusMessageType.Info);

                // ⭐ Передаём isOwner — если не владелец, удалит только локально
                bool success = await DataService.RemoveCustomerAsync(customer, isOwner);

                if (success)
                {
                    string scope = isOwner ? "из базы" : "локально";
                    StatusBegin($"Заказчик {customer.Name} удалён {scope}.", StatusMessageType.Success);

                    await LoadManagerDataAsync(TargetManager);
                    CustomerDrop.SelectedItem = null;
                }
                else
                {
                    StatusBegin($"Не удалось удалить заказчика {customer.Name}.", StatusMessageType.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка удаления заказчика: {ex.Message}", StatusMessageType.Error);
            }
        }

        /// <summary>
        /// Проверяет компанию загруженного расчёта напрямую в БД. 
        /// Если её нет, подсвечивает дроп и предлагает добавить.
        /// </summary>
        public async void CheckAndPromptForUnknownCustomer(Offer offer)
        {
            if (offer == null || string.IsNullOrWhiteSpace(offer.Company)) return;
            if (TargetManager == null || TargetManager.Name is null) return;

            string normalizedCompany = HybridDataService.NormalizeCustomerName(offer.Company);

            // ⭐ ПРЯМАЯ ПРОВЕРКА В БАЗЕ ДАННЫХ, а не в кэше UI. Это гарантирует отсутствие дубликатов.
            bool exists = await DataService.CustomerExistsAsync(TargetManager.Name, normalizedCompany);

            if (!exists)
            {
                HighlightCustomerDropTemporarily();

                var result = MessageBox.Show(
                    $"Заказчик \"{offer.Company}\" не найден в вашей базе данных.\n\n" +
                    $"Добавить его в базу сейчас?",
                    "Новый заказчик в расчёте",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await AddUnknownCustomerAsync(offer.Company);
                }
            }
            else
            {
                // Если заказчик уже есть в БД, гарантированно снимаем подсветку
                CustomerDrop?.ClearValue(BackgroundProperty);
            }
        }

        /// <summary>
        /// Добавляет неизвестного заказчика и гарантированно обновляет UI.
        /// </summary>
        private async System.Threading.Tasks.Task AddUnknownCustomerAsync(string companyName)
        {
            if (string.IsNullOrWhiteSpace(companyName) || TargetManager == null || CurrentManager == null) return;

            try
            {
                string normalizedName = HybridDataService.NormalizeCustomerName(companyName);
                var existingData = await DataService.FindCustomerByNameAsync(normalizedName);

                string address = existingData?.Address ?? "";
                bool isAgent = existingData?.Agent ?? false;
                int deliveryPrice = existingData?.DeliveryPrice ?? 0;

                StatusBegin($"Добавление заказчика '{companyName}'...", StatusMessageType.Info);

                var newCustomer = await DataService.AddCustomerAsync(
                    name: companyName.Trim(),
                    address: address,
                    isAgent: isAgent,
                    deliveryPrice: deliveryPrice,
                    localManagerId: TargetManager.Id,
                    isEngineer: CurrentManager.IsEngineer
                );

                if (newCustomer != null && TargetManager.Name != null)
                {
                    string scope = CurrentManager.IsEngineer ? "в вашу локальную базу" : "в общую базу";
                    StatusBegin($"Заказчик '{newCustomer.Name}' успешно добавлен {scope}.", StatusMessageType.Success);

                    var updatedCustomers = await DataService.GetCustomersAsync(TargetManager.Id, TargetManager.Name);
                    Customers.Clear();
                    foreach (var c in updatedCustomers) Customers.Add(c);

                    var addedCustomer = Customers.FirstOrDefault(c =>
                        HybridDataService.NormalizeCustomerName(c.Name) == normalizedName);

                    if (addedCustomer != null)
                    {
                        CustomerDrop.SelectedItem = addedCustomer;
                    }
                }
                else
                {
                    // ⭐ СПЕЦИАЛЬНОЕ СООБЩЕНИЕ ДЛЯ ЗАГРУЖЕННОГО РАСЧЕТА
                    StatusBegin($"Не удалось добавить: заказчик '{companyName}' уже существует в общей базе под другим именем/менеджером. Пожалуйста, измените имя заказчика в самом расчете или добавьте его вручную с уточнением.", StatusMessageType.Warning);

                    // Снимаем подсветку, так как автоматическое добавление не удалось
                    CustomerDrop?.ClearValue(BackgroundProperty);
                }
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка добавления заказчика: {ex.Message}", StatusMessageType.Error);
            }
        }

        /// <summary>
        /// Подсвечивает дроп заказчиков оранжевым цветом на 5 секунд.
        /// </summary>
        private void HighlightCustomerDropTemporarily()
        {
            if (CustomerDrop == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var highlightBrush = new SolidColorBrush(
                        Color.FromRgb(255, 200, 100)); // Мягкий оранжевый

                    CustomerDrop.Background = highlightBrush;

                    // Через 5 секунд возвращаем стандартный фон
                    System.Threading.Tasks.Task.Delay(5000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            CustomerDrop.ClearValue(BackgroundProperty);
                        });
                    });
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Ошибка подсветки дропа: {ex.Message}");
                }
            }), DispatcherPriority.Loaded);
        }
        #endregion


        //-------------Вспомогательные методы----------------------//
        #region

        //-----------Поиск нарезанной детали-----------//
        private void Search_Details(object sender, FunctionEventArgs<string> e)
        {
            //получаем все работы из комплектов деталей
            var works = DetailControls.Where(d => d.Detail.IsComplect)
                                .SelectMany(t => t.TypeDetailControls)
                                .SelectMany(w => w.WorkControls);

            //получаем контроллы всех нарезанных деталей
            List<PartControl> parts = new();

            foreach (WorkControl work in works)
                if (work.workType is ICut cut && cut.Parts?.Count > 0) parts.AddRange(cut.Parts);

            //если поле поиска пустое или нарезанных деталей нет, выходим
            if (SearchDetails.Replace(" ", "") == "")
            {
                foreach (PartControl part in parts) part.Background = Brushes.White;
                return;
            }

            //ищем детали, совпадающие по имени с введенным тестом пользователя
            List<PartControl> foundDetails = parts.Where(x => x.Part.Title is not null &&
                            x.Part.Title.Contains(SearchDetails, StringComparison.OrdinalIgnoreCase)).ToList();

            if (foundDetails.Count > 0)
            {
                //окрашиваем зеленым найденные детали
                foreach (PartControl part in parts)
                    part.Background = foundDetails.Contains(part) ? Brushes.LightGreen : Brushes.White;

                //и фокусируем пользователя на первой найденной детали
                var types = DetailControls.Where(d => d.Detail.IsComplect).SelectMany(t => t.TypeDetailControls);

                foreach (var type in types)
                {
                    if (IsVisualChild(type.PartsStack, foundDetails[0]))
                    {
                        // 1. Обновляем заголовок (вы уже это делаете)
                        if (type.FindName("PartsToggle") is ToggleButton toggle)
                        {
                            // 2. Визуальное выделение (на 2 секунды)
                            var originalBorder = toggle.BorderBrush;
                            toggle.BorderBrush = Brushes.OrangeRed;
                            toggle.BorderThickness = new Thickness(2);

                            // Вернуть обратно через 2 сек
                            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                            timer.Tick += (s, e) =>
                            {
                                toggle.BorderBrush = originalBorder;
                                toggle.BorderThickness = new Thickness(1);
                                timer.Stop();
                            };
                            timer.Start();

                            // 3. Прокручиваем к ToggleButton
                            toggle.BringIntoView();

                            // 4. Устанавливаем фокус на кнопку
                            toggle.Focus();
                        }

                        break;
                    }
                }
                if (foundDetails.Count > 1)
                    StatusBegin($"Деталей по запросу \"{SearchDetails}\" найдено {foundDetails.Count}. " +
                        $"Все они окрашены зеленым цветом и могут находиться в других заготовках.", StatusMessageType.Warning);
            }
            else StatusBegin($"Деталей по запросу \"{SearchDetails}\" не найдено.", StatusMessageType.Warning);
        }
        public static bool IsVisualChild(DependencyObject parent, DependencyObject child)
        {
            if (child == null || parent == null) return false;
            if (parent == child) return true;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var descendant = VisualTreeHelper.GetChild(parent, i);
                if (IsVisualChild(descendant, child))
                    return true;
            }
            return false;
        }

        //-------------Даты-----------//
        public DateTime? EndDate()
        {
            if (int.TryParse(DateProduction.Text, out int days))
            {
                DateTime _date;
                int workDays = days;

                for (int i = 0; i <= days; i++)
                {
                    _date = DateTime.Now.AddDays(i);
                    if (_date.DayOfWeek == DayOfWeek.Saturday || _date.DayOfWeek == DayOfWeek.Sunday) workDays++;
                }

                if (DateTime.Now.AddDays(workDays).DayOfWeek == DayOfWeek.Saturday) workDays++;
                if (DateTime.Now.AddDays(workDays).DayOfWeek == DayOfWeek.Sunday) workDays++;

                return DateTime.Now.AddDays(workDays);
            }
            else return null;
        }

        public enum StatusMessageType
        {
            Info,
            Success,
            Warning,
            Error
        }

        public void StatusBegin(string? notify = null, StatusMessageType type = StatusMessageType.Info)
        {
            if (notify != null)
                NotifyText.Text = notify;

            Color targetColor = type switch
            {
                StatusMessageType.Success => (Color)ColorConverter.ConvertFromString("#90EE90"), // мягкий зеленый
                StatusMessageType.Warning => (Color)ColorConverter.ConvertFromString("#FFD580"), // мягкий оранжевый
                StatusMessageType.Error => Colors.Red,
                _ => Colors.White
            };

            var animation = new ColorAnimation
            {
                From = Colors.White,
                To = targetColor,
                Duration = TimeSpan.FromSeconds(2),
                AutoReverse = true
            };

            Status.Background = new SolidColorBrush(Colors.White);
            Status.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        //------------Краткое руководство-------------------------//
        private void OpenExample(object sender, RoutedEventArgs e)
        {
            ExampleWindow exampleWindow = new();
            exampleWindow.Show();
        }

        //------------Таблица гибов-------------------------------//
        private void ShowTableOfBends(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                string header = $"{item.Header}";
                ExtraWindow extraWindow = new(header);
                Image image = new() { Source = new BitmapImage(new Uri("Images/tableofbends.jpg", UriKind.Relative)), MaxWidth = 800 };
                extraWindow.ContentStack.Children.Add(image);
                TextBlock tb = new()
                {
                    Text = $"Максимальная длина гиба – 2550 мм.\r\n" +
                    $"Коэффициент для развертки – 0,4 мм.\r\n" +
                    $"Проверить усилие станка по таблице гибов:\r\n" +
                    $"1. Определяем № матрицы.\r\n" +
                    $"2. На пересечении матрицы и толщины металла\r\n" +
                    $"получаем количество тонн на метр гиба.\r\n" +
                    $"3. Умножаем это значение на фактический размер гиба.\r\n" +
                    $"4. Если результат меньше 100 тонн, значит согнём!\r\n" +
                    $"Проверить размер полки выбранной матрицы:\r\n" +
                    $"1. Берем половину от № матрицы.\r\n" +
                    $"2. Добавляем толщину металла и 1 мм на зацеп.\r\n" +
                    $"3. Полученный результат – минимальная полка гиба.\r\n" +
                    $"Проверить внутренний размер между гибами:\r\n" +
                    $"если этот размер меньше полок, второй гиб может не получиться из-за того,\r\n" +
                    $"что полка первого гиба будет упираться в станок.\r\n" +
                    $"Этот момент уточняется экспериментальным путем у специалиста - гибщика!",
                    Margin = new Thickness(20),
                };
                extraWindow.ContentStack.Children.Add(tb);
                extraWindow.Show();
            }
        }

        //------------Смена темы----------------------------------//
        public static void ThemeChange(string style)
        {
            // определяем путь к файлу ресурсов
            Uri? uri = new("Themes/" + style + ".xaml", UriKind.Relative);
            // загружаем словарь ресурсов
            ResourceDictionary? resourceDict = Application.LoadComponent(uri) as ResourceDictionary;
            // очищаем коллекцию ресурсов приложения
            Application.Current.Resources.Clear();
            // добавляем загруженный словарь ресурсов
            Application.Current.Resources.MergedDictionaries.Add(resourceDict);
        }

        //------------Конвертер файлов dwg в dxf------------------//
        public string[]? fileNames;
        private void Convert_dwg_to_dxf(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Multiselect = true,
                Filter = "DWG-File (*.dwg)|*.dwg|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames != null)
            {
                fileNames = openFileDialog.FileNames;
                Convert_dwg_to_dxf();
            }
        }
        public string Convert_dwg_to_dxf(string? message = null)
        {
            try
            {
                if (fileNames?.Length > 0)
                    foreach (string _name in fileNames)
                    {
                        CadDocument doc;

                        using DwgReader reader = new(_name);
                        doc = reader.Read();

                        using DxfWriter writer = new(Path.GetDirectoryName(_name) + "\\" + Path.GetFileNameWithoutExtension(_name) + ".dxf", doc, false);
                        writer.Write();
                    }
            }
            catch (Exception ex) { return $"Произошла ошибка конвертации: {ex.Message}"; }

            return $"Файлы в количестве {fileNames?.Length} шт успешно конвертированы";
        }

        //------------Загрузка расчета в режиме чтения-----------//
        private async void OpenOffer(object sender, RoutedEventArgs e)
        {
            if (OffersGrid.SelectedItem is not Offer selectedOffer) return;

            try
            {
                StatusBegin($"Загрузка данных расчета {selectedOffer.N}...", StatusMessageType.Info);

                // ⭐ 1. Если Data уже есть в памяти — используем его
                // ⭐ 2. Если Data == null — загружаем из базы через сервис
                string? dataJson = selectedOffer.Data;

                if (string.IsNullOrEmpty(dataJson))
                {
                    dataJson = await DataService.GetOfferDataAsync(selectedOffer.Id);

                    if (string.IsNullOrEmpty(selectedOffer.Data))
                    {
                        selectedOffer.Data = dataJson; // ⭐ Кэшируем в объекте
                    }
                }

                if (string.IsNullOrEmpty(dataJson))
                {
                    StatusBegin($"Данные расчета {selectedOffer.N} отсутствуют в базе", StatusMessageType.Warning);
                    return;
                }

                // 3. Десериализация данных
                Product? product = OpenOfferData(dataJson);
                if (product is null)
                {
                    StatusBegin($"Не удалось открыть расчет {selectedOffer.N}: данные повреждены", StatusMessageType.Error);
                    return;
                }

                // 4. Создание и показ окна
                ProductWindow clon = new(product)
                {
                    Title = $"{selectedOffer.N}  {selectedOffer.Company}",
                    Amount = selectedOffer.Amount
                };

                clon.Show();
                StatusBegin($"Расчет {selectedOffer.N} открыт для чтения", StatusMessageType.Success);
            }
            catch (System.Runtime.Serialization.SerializationException ex)
            {
                StatusBegin($"Ошибка формата данных расчета #{selectedOffer.N}", StatusMessageType.Error);
                LogException(ex, $"OpenOffer.Serialization: Offer #{selectedOffer.N}");
            }
            catch (IOException ex)
            {
                StatusBegin($"Не удалось прочитать данные расчета #{selectedOffer.N}", StatusMessageType.Error);
                LogException(ex, $"OpenOffer.IO: Offer #{selectedOffer.N}");
            }
            catch (InvalidOperationException ex)
            {
                StatusBegin($"Ошибка интерфейса при открытии расчета #{selectedOffer.N}", StatusMessageType.Error);
                LogException(ex, $"OpenOffer.InvalidOp: Offer #{selectedOffer.N}");
            }
            catch (NullReferenceException ex)
            {
                StatusBegin("Внутренняя ошибка приложения. Обратитесь к разработчику.", StatusMessageType.Error);
                LogException(ex, $"OpenOffer.NullRef: Offer #{selectedOffer.N}", true);
            }
            catch (Exception ex)
            {
                StatusBegin($"Непредвиденная ошибка при открытии расчета #{selectedOffer.N}", StatusMessageType.Error);
                LogException(ex, $"OpenOffer.General: Offer #{selectedOffer.N}");
            }
        }

        /// <summary>
        /// Возвращает безопасное сообщение для пользователя (без путей, стека вызовов и технических деталей)
        /// </summary>
        private string GetUserFriendlyMessage(Exception ex)
        {
            // Скрываем технические детали, но показываем суть
            return ex.Message switch
            {
                var m when m.Contains("access to the path") => "Нет доступа к файлу",
                var m when m.Contains("is not a valid") => "Неверный формат данных",
                var m when m.Contains("cannot be found") => "Файл не найден",
                var m when m.Contains("sequence contains no elements") => "Данные пусты",
                _ => "Произошла ошибка при загрузке"
            };
        }

        /// <summary>
        /// Логирует исключение (в файл, EventLog, или консоль отладки)
        /// </summary>
        private void LogException(Exception ex, string context, bool isCritical = false)
        {
            // Вариант 1: Простой вывод в Debug (для разработки)
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss}] {context}\n{ex}");

            // Вариант 2: Запись в файл (для продакшена)
            // try 
            // {
            //     File.AppendAllText("error.log", $"{DateTime.Now}: {context}\n{ex}\n\n");
            // }
            // catch { /* Игнорируем ошибки логирования, чтобы не зациклить */ }

            // Если ошибка критическая — можно показать диалог или завершить работу
            //if (isCritical)
            //{
            // MessageBox.Show("Критическая ошибка. Приложение будет закрыто.", "Ошибка", 
            //     MessageBoxButton.OK, MessageBoxImage.Error);
            // Application.Current.Shutdown(1);
            //}
        }

        //------------Создание папки проекта-----------------//
        private void CreateProjectFolder(object sender, RoutedEventArgs e)
        {
            // 1. Создаём диалог выбора папки
            var dialog = new OpenFileDialog
            {
                Title = "Укажите расположение для проекта",
                CheckFileExists = false,       // Разрешает выбор несуществующих файлов/папок
                ValidateNames = false,         // Отключает проверку имени файла
                FileName = $"{Order.Text} {CustomerDrop.Text}", // Подсказка в поле ввода
                InitialDirectory = ProductModel.dialogService.LastUsedDirectory
            };

            // 2. Проверяем результат выбора
            if (dialog.ShowDialog() == true)
            {
                string? projectPath = ProductModel.dialogService.LastUsedDirectory = dialog.FileName;

                // Если пользователь случайно выбрал файл, берём его родительскую директорию
                if (Path.HasExtension(projectPath) && File.Exists(projectPath))
                    projectPath = Path.GetDirectoryName(projectPath);

                if (projectPath is null) return;

                try
                {
                    // 3. Создаём вложенную структуру
                    Directory.CreateDirectory(Path.Combine(projectPath, "ТЗ", "Исходник"));
                    Directory.CreateDirectory(Path.Combine(projectPath, "ТЗ", "Редакция"));

                    // 4. Уведомляем об успехе
                    MessageBox.Show(
                        $"Структура проекта успешно создана:\n{projectPath}",
                        "Готово",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Process.Start("explorer.exe", projectPath);
                }
                catch (Exception ex)
                {
                    // 5. Обработка ошибок (нет прав, занятый файл, недопустимые символы и т.д.)
                    MessageBox.Show(
                        $"Не удалось создать папки:\n{ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        //------------Создание заявки за клиента-----------------//
        public string lastInputDirectory = string.Empty;            //директория для ТЗ

        private void CreateRequest(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "All files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames.Length > 0)
            {
                lastInputDirectory = Path.GetDirectoryName(openFileDialog.FileNames[0]);
                if (lastInputDirectory != null && !lastInputDirectory.Contains("ТЗ", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBoxResult response = MessageBox.Show(
                        "Папка с моделями, в которой будет создана заявка, должна называться \"ТЗ\"!",
                        "Создание заявки", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (response == MessageBoxResult.No) return;
                }

                NewProject();
                RequestControl = new(openFileDialog.FileNames.ToList());
                WindowGrid.Children.Insert(0, RequestControl);

                IsRequest = true;
            }
            else StatusBegin($"Не выбрано ни одного файла", StatusMessageType.Error);
        }

        public void CloseRequestControl()
        {
            WindowGrid.Children.RemoveAt(0);
            RequestControl = null;
            IsRequest = false;
        }


        //------------Получение габаритов детали из dxf----------//
        public static (Rect, float, int) GetDrawingBounds(CadDocument dxf)
        {
            var bounds = new List<Point>();     //крайние точки сущностей
            float way = 0;                      //путь резки
            int pinholes = 0;                   //проколы

            foreach (var entity in dxf.Entities)
            {
                if (entity is Line line)
                {
                    bounds.Add(new Point(line.StartPoint.X, line.StartPoint.Y));
                    bounds.Add(new Point(line.EndPoint.X, line.EndPoint.Y));

                    way += (float)Math.Abs(Math.Sqrt(Math.Pow(line.EndPoint.X - line.StartPoint.X, 2) + Math.Pow(line.EndPoint.Y - line.StartPoint.Y, 2)));
                    if (pinholes == 0) pinholes++;
                }
                else if (entity is LwPolyline polyline)
                {
                    foreach (var vertex in polyline.Vertices)
                        bounds.Add(new Point(vertex.Location.X, vertex.Location.Y));

                    way += (float)GetPolylineLength(polyline);
                    if (polyline.IsClosed) pinholes++;
                }
                else if (entity is Spline spline)
                {
                    if (spline.FitPoints != null && spline.FitPoints.Count > 0)
                    {
                        foreach (var fp in spline.FitPoints)
                        {
                            bounds.Add(new Point(fp.X, fp.Y));
                        }
                    }
                    else if (spline.ControlPoints != null)
                    {
                        // fallback, если FitPoints нет
                        foreach (var cp in spline.ControlPoints)
                        {
                            bounds.Add(new Point(cp.X, cp.Y));
                        }
                    }

                    way += (float)GetSplineLength(spline);
                    if (spline.IsClosed) pinholes++;
                }
                else if (entity is Arc arc)
                {
                    // Добавляем центр дуги
                    bounds.Add(new Point(arc.Center.X, arc.Center.Y));

                    // Добавляем начальную и конечную точки дуги
                    double startAngle = arc.StartAngle.ToRadians();
                    double endAngle = arc.EndAngle.ToRadians();

                    double xStart = arc.Center.X + arc.Radius * Math.Cos(startAngle);
                    double yStart = arc.Center.Y + arc.Radius * Math.Sin(startAngle);
                    bounds.Add(new Point(xStart, yStart));

                    double xEnd = arc.Center.X + arc.Radius * Math.Cos(endAngle);
                    double yEnd = arc.Center.Y + arc.Radius * Math.Sin(endAngle);
                    bounds.Add(new Point(xEnd, yEnd));

                    way += (float)GetArcLength(arc);
                    pinholes++;
                }
                else if (entity is Circle circle)
                {
                    bounds.Add(new Point(circle.Center.X, circle.Center.Y));                 // center
                    bounds.Add(new Point(circle.Center.X + circle.Radius, circle.Center.Y)); // right
                    bounds.Add(new Point(circle.Center.X - circle.Radius, circle.Center.Y)); // left
                    bounds.Add(new Point(circle.Center.X, circle.Center.Y + circle.Radius)); // top
                    bounds.Add(new Point(circle.Center.X, circle.Center.Y - circle.Radius)); // bottom

                    way += (float)(Math.PI * circle.Radius * 2);
                    pinholes++;
                }
                else if (entity is Insert insert)
                {
                    if (dxf.BlockRecords.TryGetValue(insert.Block.Name, out BlockRecord blockRecord))
                        foreach (var blockEntity in blockRecord.Entities)
                        {
                            if (blockEntity is Line _line)
                            {
                                bounds.Add(new Point(_line.StartPoint.X, _line.StartPoint.Y));
                                bounds.Add(new Point(_line.EndPoint.X, _line.EndPoint.Y));

                                way += (float)Math.Abs(Math.Sqrt(Math.Pow(_line.EndPoint.X - _line.StartPoint.X, 2)
                                                            + Math.Pow(_line.EndPoint.Y - _line.StartPoint.Y, 2)));
                                if (pinholes == 0) pinholes++;
                            }
                            else if (blockEntity is LwPolyline _polyline)
                            {
                                foreach (var vertex in _polyline.Vertices)
                                    bounds.Add(new Point(vertex.Location.X, vertex.Location.Y));

                                way += (float)GetPolylineLength(_polyline);
                                if (_polyline.IsClosed) pinholes++;
                            }
                            else if (blockEntity is Spline _spline)
                            {
                                if (_spline.FitPoints != null && _spline.FitPoints.Count > 0)
                                {
                                    foreach (var fp in _spline.FitPoints)
                                    {
                                        bounds.Add(new Point(fp.X, fp.Y));
                                    }
                                }
                                else if (_spline.ControlPoints != null)
                                {
                                    // fallback, если FitPoints нет
                                    foreach (var cp in _spline.ControlPoints)
                                    {
                                        bounds.Add(new Point(cp.X, cp.Y));
                                    }
                                }

                                way += (float)GetSplineLength(_spline);
                                if (_spline.IsClosed) pinholes++;
                            }
                            else if (blockEntity is Arc _arc)
                            {
                                // Добавляем центр дуги
                                bounds.Add(new Point(_arc.Center.X, _arc.Center.Y));

                                // Добавляем начальную и конечную точки дуги
                                double startAngle = _arc.StartAngle.ToRadians();
                                double endAngle = _arc.EndAngle.ToRadians();

                                double xStart = _arc.Center.X + _arc.Radius * Math.Cos(startAngle);
                                double yStart = _arc.Center.Y + _arc.Radius * Math.Sin(startAngle);
                                bounds.Add(new Point(xStart, yStart));

                                double xEnd = _arc.Center.X + _arc.Radius * Math.Cos(endAngle);
                                double yEnd = _arc.Center.Y + _arc.Radius * Math.Sin(endAngle);
                                bounds.Add(new Point(xEnd, yEnd));

                                way += (float)GetArcLength(_arc);
                                pinholes++;
                            }
                            else if (blockEntity is Circle _circle)
                            {
                                bounds.Add(new Point(_circle.Center.X, _circle.Center.Y));                  // center
                                bounds.Add(new Point(_circle.Center.X + _circle.Radius, _circle.Center.Y)); // right
                                bounds.Add(new Point(_circle.Center.X - _circle.Radius, _circle.Center.Y)); // left
                                bounds.Add(new Point(_circle.Center.X, _circle.Center.Y + _circle.Radius)); // top
                                bounds.Add(new Point(_circle.Center.X, _circle.Center.Y - _circle.Radius)); // bottom

                                way += (float)(Math.PI * _circle.Radius * 2);
                                pinholes++;
                            }
                        }
                }
            }

            if (bounds.Count == 0) return (Rect.Empty, way, pinholes);

            double minX = bounds.Min(p => p.X);
            double maxX = bounds.Max(p => p.X);
            double minY = bounds.Min(p => p.Y);
            double maxY = bounds.Max(p => p.Y);

            return (new Rect(minX, minY, maxX - minX, maxY - minY), way, pinholes);
        }

        //------------Получение геометрии детали из dxf----------//
        public static ObservableCollection<IGeometryDescriptor> GetGeometries(CadDocument dxf, Rect drawingBounds, double targetWidth, double targetHeight)
        {
            ObservableCollection<IGeometryDescriptor> geometries = new();

            double scaleX = targetWidth / drawingBounds.Width;
            double scaleY = targetHeight / drawingBounds.Height;
            double scale = Math.Min(scaleX, scaleY);

            double offsetX = (targetWidth - drawingBounds.Width * scale) / 2;
            double offsetY = (targetHeight - drawingBounds.Height * scale) / 2;

            foreach (var entity in dxf.Entities)
            {
                switch (entity)
                {
                    case Line line:
                        DrawLine(line, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case Arc arc:
                        DrawArc(arc, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case Circle circle:
                        DrawCircle(circle, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case Ellipse ellipse:
                        DrawEllipse(ellipse, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case LwPolyline lwPoly:
                        DrawLwPolyline(lwPoly, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case Spline spline:
                        DrawSpline(spline, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case Insert insert:
                        RenderBlock(insert.Block, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                }
            }

            return geometries;
        }

        public static void DrawLine(Line line, double scale, double offsetX, double offsetY, ObservableCollection<IGeometryDescriptor> geometries, Rect drawingBounds)
        {
            Point start = Transform(line.StartPoint, scale, offsetX, offsetY, drawingBounds);
            Point end = Transform(line.EndPoint, scale, offsetX, offsetY, drawingBounds);

            geometries.Add(new LineDescriptor
            {
                Start = start,
                End = end
            });
        }

        public static void DrawArc(Arc arc, double scale, double offsetX, double offsetY, ObservableCollection<IGeometryDescriptor> geometries, Rect drawingBounds)
        {
            Point center = Transform(arc.Center, scale, offsetX, offsetY, drawingBounds);
            double scaledRadius = arc.Radius * scale;

            double startAngle = arc.StartAngle.ToRadians();
            double endAngle = arc.EndAngle.ToRadians();
            double sweepAngle = endAngle - startAngle;

            Point startPoint = new Point(
                center.X + scaledRadius * Math.Cos(startAngle),
                center.Y - scaledRadius * Math.Sin(startAngle)
            );

            Point endPoint = new Point(
                center.X + scaledRadius * Math.Cos(endAngle),
                center.Y - scaledRadius * Math.Sin(endAngle)
            );

            bool isLargeArc = Math.Abs(sweepAngle) > Math.PI;
            SweepDirection sweepDirection = sweepAngle >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

            geometries.Add(new ArcDescriptor
            {
                StartPoint = startPoint,
                EndPoint = endPoint,
                Size = new Size(scaledRadius, scaledRadius),
                IsLargeArc = isLargeArc,
                SweepDirection = sweepDirection
            });
        }

        public static void DrawCircle(Circle circle, double scale, double offsetX, double offsetY, ObservableCollection<IGeometryDescriptor> geometries, Rect drawingBounds)
        {
            Point center = Transform(circle.Center, scale, offsetX, offsetY, drawingBounds);
            double radius = circle.Radius * scale;

            geometries.Add(new CircleDescriptor
            {
                Center = center,
                Radius = radius
            });
        }

        public static void DrawEllipse(Ellipse ellipse, double scale, double offsetX, double offsetY, ObservableCollection<IGeometryDescriptor> geometries, Rect drawingBounds)
        {
            if (ellipse == null) return;

            double radiusX = ellipse.MajorAxis * scale;

            double radiusY = ellipse.MinorAxis * scale;

            // Преобразуем центр эллипса
            var center = Transform(ellipse.Center, scale, offsetX, offsetY, drawingBounds);

            geometries.Add(new EllipseDescriptor
            {
                Center = center,
                RadiusX = radiusX,
                RadiusY = radiusY,
                Stroke = Brushes.Red,
                StrokeThickness = 0.5
            });
        }

        public static void DrawLwPolyline(LwPolyline lwPolyline, double scale, double offsetX, double offsetY, ObservableCollection<IGeometryDescriptor> geometries, Rect drawingBounds)
        {
            if (lwPolyline?.Vertices == null || lwPolyline.Vertices.Count < 2)
                return;

            var points = new List<Point>();
            foreach (var vertex in lwPolyline.Vertices)
            {
                var pt = Transform(new(vertex.Location.X, vertex.Location.Y, 0), scale, offsetX, offsetY, drawingBounds);
                points.Add(pt);
            }

            // Если полилиния замкнута — добавим замыкающий сегмент (опционально для предпросмотра)
            if (lwPolyline.IsClosed && points.Count > 2)
            {
                points.Add(points[0]);
            }

            // Создаём последовательность линий
            for (int i = 1; i < points.Count; i++)
            {
                geometries.Add(new LineDescriptor
                {
                    Start = points[i - 1],
                    End = points[i]
                });
            }
        }

        public static void DrawSpline(Spline spline, double scale, double offsetX, double offsetY, ObservableCollection<IGeometryDescriptor> geometries, Rect drawingBounds)
        {
            if (spline == null) return;

            List<XYZ> sourcePoints;

            if (spline.FitPoints != null && spline.FitPoints.Count >= 2)
            {
                sourcePoints = spline.FitPoints;
            }
            else if (spline.ControlPoints != null && spline.ControlPoints.Count >= 2)
            {
                sourcePoints = spline.ControlPoints;
            }
            else
            {
                return;
            }

            var points = new List<Point>();
            foreach (var pt in sourcePoints)
            {
                var wpfPt = Transform(pt, scale, offsetX, offsetY, drawingBounds);
                points.Add(wpfPt);
            }

            geometries.Add(new SplineDescriptor
            {
                Points = points,
                IsClosed = spline.IsClosed,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5
            });
        }

        public static void RenderBlock(BlockRecord block, double scale, double offsetX, double offsetY, ObservableCollection<IGeometryDescriptor> geometries, Rect drawingBounds)
        {
            if (block?.Entities == null) return;

            foreach (var entity in block.Entities)
            {
                switch (entity)
                {
                    case Line line:
                        DrawLine(line, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case Arc arc:
                        DrawArc(arc, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case Circle circle:
                        DrawCircle(circle, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case Ellipse ellipse:
                        DrawEllipse(ellipse, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case LwPolyline lwPolyline:
                        DrawLwPolyline(lwPolyline, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                    case Spline spline:
                        DrawSpline(spline, scale, offsetX, offsetY, geometries, drawingBounds);
                        break;
                }
            }
        }

        public static Point Transform(XYZ point, double scale, double offsetX, double offsetY, Rect drawingBounds)
        {
            double x = (point.X - drawingBounds.X) * scale + offsetX;

            // Нормализуем Y относительно нижнего края, затем инвертируем
            double normalizedY = point.Y - drawingBounds.Y;
            double flippedY = drawingBounds.Height - normalizedY;
            double y = flippedY * scale + offsetY;

            return new Point(x, y);
        }

        public static double GetSplineLength(Spline spline)
        {
            if (spline?.FitPoints != null && spline.FitPoints.Count > 1)
            {
                double length = 0.0;
                for (int i = 1; i < spline.FitPoints.Count; i++)
                {
                    var p0 = spline.FitPoints[i - 1];
                    var p1 = spline.FitPoints[i];
                    length += Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
                }

                // Если сплайн замкнут — добавляем сегмент от последней к первой точке
                if (spline.IsClosed && spline.FitPoints.Count > 2)
                {
                    var first = spline.FitPoints[0];
                    var last = spline.FitPoints[^1];
                    length += Math.Sqrt((first.X - last.X) * (first.X - last.X) + (first.Y - last.Y) * (first.Y - last.Y));
                }

                return length;
            }

            // Если FitPoints нет — используем ControlPoints как fallback (с пониманием неточности)
            if (spline?.ControlPoints != null && spline.ControlPoints.Count > 1)
            {
                double length = 0.0;
                for (int i = 1; i < spline.ControlPoints.Count; i++)
                {
                    var p0 = spline.ControlPoints[i - 1];
                    var p1 = spline.ControlPoints[i];
                    length += Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
                }
                return length; // без коэффициента — лучше перестраховаться в большую сторону для резки
            }

            return 0.0;
        }

        private static double GetPolylineLength(LwPolyline polyline)
        {
            double totalLength = 0.0;
            var vertices = polyline.Vertices;

            if (vertices.Count < 2) return 0.0;

            for (int i = 0; i < vertices.Count - 1; i++)
            {
                var start = vertices[i].Location;
                var end = vertices[i + 1].Location;

                double dx = end.X - start.X;
                double dy = end.Y - start.Y;
                double chordLength = Math.Sqrt(dx * dx + dy * dy);

                if (Math.Abs(vertices[i + 1].Bulge) > 1e-6) // Дуга
                {
                    double theta = 4 * Math.Atan(Math.Abs(vertices[i + 1].Bulge));
                    double radius = chordLength / (2 * Math.Sin(theta / 2));
                    totalLength += radius * theta;
                }
                else // Прямая
                {
                    totalLength += chordLength;
                }
            }

            // Если замкнута — добавить последний сегмент
            if (polyline.IsClosed && vertices.Count > 2)
            {
                var last = vertices[^1].Location;
                var first = vertices[0].Location;
                double dx = first.X - last.X;
                double dy = first.Y - last.Y;
                double closeLength = Math.Sqrt(dx * dx + dy * dy);

                if (Math.Abs(vertices[0].Bulge) > 1e-6)
                {
                    double theta = 4 * Math.Atan(Math.Abs(vertices[0].Bulge));
                    double radius = closeLength / (2 * Math.Sin(theta / 2));
                    totalLength += radius * theta;
                }
                else
                {
                    totalLength += closeLength;
                }
            }

            return totalLength;
        }

        public static double GetArcLength(Arc arc)
        {
            if (arc.Radius <= 0) return 0;

            // Нормализуем углы в диапазон [0, 360]
            double start = NormalizeAngle(arc.StartAngle);
            double end = NormalizeAngle(arc.EndAngle);

            // Определяем направление: в DXF дуга всегда против часовой стрелки (CCW)
            // Но StartAngle и EndAngle могут быть в любом порядке
            double sweepAngle = end - start;

            if (sweepAngle < 0) sweepAngle += 360; // пересекает 0°

            // Переводим в радианы и вычисляем длину
            double angleRad = sweepAngle * Math.PI / 180;
            return arc.Radius * angleRad;
        }

        public static double NormalizeAngle(double angle)
        {
            angle = angle % 360;
            return angle < 0 ? angle + 360 : angle;
        }


        //------------Перенос чертежей при загрузке сохранения---//
        public void PdfMigrate(string path)
        {
            char c = '\\';

            if (Parts.Count > 0)
                foreach (Part part in Parts)
                {
                    if (part.PathToScan != null && part.PathToScan.Contains(c))
                    {
                        string oldPath = part.PathToScan[(part.PathToScan.LastIndexOf(c) + 1)..];
                        string newPath = Path.GetDirectoryName(Path.GetDirectoryName(path)) + "\\ТЗ\\" + oldPath;
                        part.PathToScan = newPath;
                    }
                }
        }

        //------------Управление сборками------------------------//
        private void ShowAssemblyWindow(object sender, RoutedEventArgs e)
        {
            if (Parts.Count == 0)
            {
                StatusBegin($"Нет деталей для добавления в сборку!", StatusMessageType.Error);
                return;
            }
            AssemblyWindow.A.CurrentParts.Clear();
            foreach (Part part in Parts.OrderBy(p => p.Title)) AssemblyWindow.A.CurrentParts.Add(part);

            AssemblyWindow.A.CurrentBaskets.Clear();
            var baskets = BasketControls.Select(b => b.Basket);
            if (baskets.Any()) foreach (var basket in baskets) AssemblyWindow.A.CurrentBaskets.Add(basket);

            AssemblyWindow.A.Show();
        }

        //------------Запуск в производство----------------------//
        private async void LaunchToWork(object sender, RoutedEventArgs e)
        {
            if (OffersGrid.SelectedItem is Offer offer)
            {
                string result = await LaunchToWork(offer);

                if (result.StartsWith("Не удалось"))
                    MessageBox.Show(result, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    StatusBegin(result, StatusMessageType.Success);
            }
        }
        private async System.Threading.Tasks.Task<string> LaunchToWork(Offer offer)
        {
            // ⭐ Защита от запуска тестового расчёта в производство
            if (await DataService.IsDraftOfferAsync(offer.Id))
                return $"Не удалось запустить в производство!\n" +
                       $"Расчёт {offer.N} является тестовым (расчетный менеджер) и не может быть передан в производство.";

            if (!Directory.Exists(connections[5]))
                return $"Не удалось запустить в производство!\n" +
                       $"Нет подключения к папке \"В работу\"";

            if (ActiveOffer is null || ActiveOffer.Data != offer.Data)
                return $"Не удалось запустить в производство!\n" +
                       $"Расчет {offer.N} не загружен (не является активным).";

            string? sourceDir = null; // путь к сохраненному расчету на диске (КП)

            // 1. Проверяем, существует ли файл по сохраненному пути (папка не переименовывалась)
            if (!string.IsNullOrEmpty(offer.Act) && File.Exists(offer.Act))
            {
                sourceDir = Path.GetDirectoryName(Path.GetDirectoryName(offer.Act));
            }

            // 2. Если файл не найден, пытаемся найти папку, поднимаясь по дереву до существующей директории
            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
            {
                if (!string.IsNullOrEmpty(offer.Act))
                {
                    // Поднимаемся по дереву каталогов, пока не найдем тот, который реально существует
                    string? currentPath = Path.GetDirectoryName(offer.Act);
                    string? validSearchRoot = null;

                    while (!string.IsNullOrEmpty(currentPath))
                    {
                        if (Directory.Exists(currentPath))
                        {
                            validSearchRoot = currentPath;
                            break;
                        }
                        currentPath = Path.GetDirectoryName(currentPath);
                    }

                    if (!string.IsNullOrEmpty(validSearchRoot))
                    {
                        string calcNumber = (offer.N ?? "").Trim();

                        // Асинхронное получение списка директорий для поиска
                        var directories = await System.Threading.Tasks.Task.Run(() => Directory.GetDirectories(validSearchRoot));

                        foreach (string dirPath in directories)
                        {
                            string dirName = Path.GetFileName(dirPath);

                            // Проверяем наличие номера расчета как отдельного "слова"
                            bool hasNumber = false;
                            if (!string.IsNullOrEmpty(calcNumber))
                            {
                                // Ищем номер в начале строки, или после пробела/подчеркивания/дефиса
                                string pattern = $@"(?:^|[\s_\-]){Regex.Escape(calcNumber)}(?:[\s_\-]|$)";
                                hasNumber = Regex.IsMatch(dirName, pattern, RegexOptions.IgnoreCase);

                                // Дополнительная страховка: если имя папки просто начинается с номера
                                if (!hasNumber && dirName.StartsWith(calcNumber, StringComparison.OrdinalIgnoreCase))
                                {
                                    hasNumber = true;
                                }
                            }

                            if (hasNumber)
                            {
                                sourceDir = dirPath;
                                break; // Останавливаем поиск после первого успешного совпадения
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                return $"Не удалось запустить в производство!\n" +
                       $"Не найден путь к КП. Проверьте, что папка с расчетом (№ {offer.N}) существует, или пересохраните расчет.";

            string notify = $"Расчет {offer.N} запущен в производство с номером заказа ";

            const int MIN_ORDER = 1000;
            const int MAX_ORDER = 9999;
            const int WINDOW_SIZE = 50; // Максимальное количество "ручных" папок вперёд от последнего номера

            int nextOrder = MIN_ORDER;
            string workingDir = connections[5];
            string logFilePath = Path.Combine(workingDir, "issued_orders.txt");

            // Создаём файл, если он не существует, и делаем его скрытым
            if (!File.Exists(logFilePath))
            {
                using (File.Create(logFilePath)) { }
                File.SetAttributes(logFilePath, FileAttributes.Hidden);
            }

            // Открываем файл с эксклюзивной блокировкой
            using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, useAsync: true))
            {
                // --- 1. Читаем последнюю строку из файла (последний выданный номер программой) ---
                stream.Position = 0;
                string? lastLine = null;
                using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        line = line.Trim();
                        if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                        {
                            lastLine = line;
                        }
                    }
                }

                int lastIssued = MIN_ORDER - 1;
                if (lastLine != null && int.TryParse(lastLine, out int parsed) &&
                    parsed >= MIN_ORDER && parsed <= MAX_ORDER)
                {
                    lastIssued = parsed;
                }

                // --- 2. Собираем номера из папок в окне [lastIssued, lastIssued + WINDOW_SIZE] ---
                int windowEnd = Math.Min(MAX_ORDER, lastIssued + WINDOW_SIZE);
                HashSet<int> candidateNumbers = new() { lastIssued };

                string orderPattern = @"^\d{4}(?=\D|$)";

                var directoriesWork = await System.Threading.Tasks.Task.Run(() => Directory.GetDirectories(workingDir));

                foreach (string dirPath in directoriesWork)
                {
                    string dirName = Path.GetFileName(dirPath);
                    var match = Regex.Match(dirName, orderPattern);
                    if (match.Success && int.TryParse(match.Value, out int orderNum))
                    {
                        if (orderNum >= lastIssued && orderNum <= windowEnd)
                        {
                            candidateNumbers.Add(orderNum);
                        }
                    }
                }

                // --- 3. Определяем следующий номер как максимум + 1 ---
                nextOrder = candidateNumbers.Max() + 1;

                if (nextOrder > MAX_ORDER)
                {
                    return $"Не удалось запустить в производство!\n" +
                           $"Достигнут максимальный номер заказа ({MAX_ORDER}). Невозможно назначить новый.";
                }

                // --- 4. Записываем новый номер в конец файла ---
                stream.Seek(0, SeekOrigin.End);

                if (stream.Length > 0)
                {
                    stream.Seek(-1, SeekOrigin.End);
                    int lastByte = stream.ReadByte();
                    if (lastByte != '\n' && lastByte != '\r')
                    {
                        stream.Seek(0, SeekOrigin.End);
                        await stream.WriteAsync(new byte[] { (byte)'\n' }, 0, 1);
                    }
                    else
                    {
                        stream.Seek(0, SeekOrigin.End);
                    }
                }

                using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1, leaveOpen: true);
                await writer.WriteLineAsync(nextOrder.ToString());
                await writer.FlushAsync();
            }

            offer.Order = nextOrder.ToString();

            try
            {
                var attrs = File.GetAttributes(logFilePath);
                if (!attrs.HasFlag(FileAttributes.Hidden))
                {
                    File.SetAttributes(logFilePath, attrs | FileAttributes.Hidden);
                }
            }
            catch { /* Не критично — продолжаем работу */ }

            bool hasPipe = false;
            foreach (DetailControl det in DetailControls)
                foreach (TypeDetailControl type in det.TypeDetailControls)
                    foreach (WorkControl work in type.WorkControls)
                        if (work.workType is PipeControl)
                        {
                            hasPipe = true;
                            break;
                        }

            string destinationDir = Directory.CreateDirectory(
                Path.Combine(
                    workingDir,
                    $"{nextOrder}{(hasPipe ? " (ТР) " : " ")}{offer.Company}({ShortManager()}){(HasAssembly ? " ЭКСПРЕСС" : "")}"
                )
            ).FullName;

            if (!string.IsNullOrEmpty(sourceDir))
            {
                await System.Threading.Tasks.Task.Run(() => CopyDirectoryToWork(sourceDir, destinationDir, true, sourceDir));

                DirectoryInfo dir = new(sourceDir);
                if (dir.GetFiles().Length == 0)
                {
                    offer.Invoice = "нал";
                }
                else
                {
                    foreach (FileInfo file in dir.GetFiles())
                    {
                        string pattern = @"счет[ё]?(?:\s+\S+)*\s+№\s*(\d+)";
                        var match = Regex.Match(file.Name, pattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            offer.Invoice = $"№ {match.Groups[1].Value}";
                            break;
                        }
                    }
                }
            }

            // === Дополнение имени папки КП (сохраняем исходное имя + добавляем счёт и заказ) ===
            try
            {
                if (!string.IsNullOrEmpty(sourceDir) && Directory.Exists(sourceDir))
                {
                    string originalName = Path.GetFileName(sourceDir);

                    string invoiceNumber = "без_счёта";
                    if (!string.IsNullOrEmpty(offer.Invoice))
                    {
                        var match = Regex.Match(offer.Invoice, @"\d+");
                        if (match.Success)
                            invoiceNumber = match.Value;
                    }
                    string invoiceSuffix = offer.Agent ? $"нал№{invoiceNumber}" : $"сч№{invoiceNumber}";
                    string orderPart = !string.IsNullOrEmpty(offer.Order) ? offer.Order.Trim() : "без_заказа";

                    string SanitizePart(string input)
                    {
                        var invalid = Path.GetInvalidFileNameChars();
                        return string.Join("_", input.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim('_');
                    }

                    invoiceSuffix = SanitizePart(invoiceSuffix);
                    orderPart = SanitizePart(orderPart);

                    const int ALIGN_WIDTH = 30;
                    string baseNamePart = originalName.Length <= ALIGN_WIDTH
                        ? originalName.PadRight(ALIGN_WIDTH)
                        : originalName;

                    string newKpFolderName = $"{baseNamePart} {invoiceSuffix} {orderPart}".TrimEnd();

                    if (originalName != newKpFolderName)
                    {
                        string parentDir = Path.GetDirectoryName(sourceDir)!;
                        string newKpPath = Path.Combine(parentDir, newKpFolderName);

                        if (!Directory.Exists(newKpPath))
                        {
                            Directory.Move(sourceDir, newKpPath);

                            // === Безопасное обновление offer.Act ===
                            if (!string.IsNullOrEmpty(offer.Act))
                            {
                                string fileName = Path.GetFileName(offer.Act);

                                // Если старый путь невалиден (папку уже переименовали), 
                                // мы просто собираем новый путь из новой папки и имени файла.
                                if (!File.Exists(offer.Act))
                                {
                                    offer.Act = Path.Combine(newKpPath, fileName);
                                }
                                else
                                {
                                    // Если файл всё ещё существует по старому пути (на случай, если это была подпапка)
                                    string relativePath = Path.GetRelativePath(sourceDir, offer.Act);
                                    offer.Act = Path.Combine(newKpPath, relativePath);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка переименования КП: {ex.Message}", StatusMessageType.Error);
            }

            await UpdateOfferAsync(offer);

            Process.Start("explorer.exe", destinationDir);

            return notify + nextOrder;
        }

        private void CopyDirectoryToWork(string sourceDir, string destinationDir, bool recursive, string mainDir)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                if (sourceDir == mainDir) continue;
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    if (subDir.Name.ToLower().Contains("кп") || subDir.Name.ToLower().Contains("тз") || subDir.Name.ToLower().Contains("архив")) continue;
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectoryToWork(subDir.FullName, newDestinationDir, true, mainDir);
                }
            }
        }

        //-----Получение DataTable-коллекции из ObservableCollection-----//
        public static DataTable ToDataTable<T>(ObservableCollection<T> items)
        {
            var tb = new DataTable(typeof(T).Name);

            PropertyInfo[] props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in props)
            {
                Type? t = GetCoreType(prop.PropertyType);
                if (t is not null) tb.Columns.Add(prop.Name, t);
            }


            foreach (T item in items)
            {
                var values = new object[props.Length];

                for (int i = 0; i < props.Length; i++)
                {
                    values[i] = props[i].GetValue(item, null);
                }

                tb.Rows.Add(values);
            }
            return tb;
        }

        public static bool IsNullable(Type t)
        {
            return !t.IsValueType || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        public static Type? GetCoreType(Type t)
        {
            if (t != null && IsNullable(t))
            {
                if (!t.IsValueType)
                {
                    return t;
                }
                else
                {
                    return Nullable.GetUnderlyingType(t);
                }
            }
            else
            {
                return t;
            }
        }

        //------------Остальные методы---------------------------//
        private void CreateRegistryWindow(object sender, RoutedEventArgs e) //открытие окна формирования списка задач
        {
            RegistryWindow registryWindow = new();
            registryWindow.Show();
        }

        public float CorrectDestiny(float _destiny, bool isGrooved = false) //метод определения расчетной толщины
        {
            if (_destiny < 0.5f || _destiny > 30) return 0;

            //если заготовкой является рифленый лист, увеличиваем толщину на 1
            if (isGrooved) _destiny++;

            //если введенная толщина заготовки соответствует возможной толщине, возвращаем толщину как есть
            if (Destinies.Contains(_destiny)) return _destiny;

            //если введенная толщина заготовки,округленная до большего целого, соответствует возможной толщине, возвращаем округленную толщину
            if (Destinies.Contains((float)Math.Ceiling(_destiny))) return (float)Math.Ceiling(_destiny);

            return CorrectDestiny(++_destiny);  //увеличиваем толщину на единицу и запускаем метод заново (рекурсия)
        }

        public bool WarningSave()           //предупреждение о незаполненных полях и недостаточной суммы КП
        {
            var fieldsToCheck = new Control[] { Order, CustomerDrop, DateProduction };
            var emptyFields = new List<Control>();

            // Подсветка пустых полей
            foreach (var control in fieldsToCheck)
            {
                string text = "";
                if (control is TextBox tb) text = tb.Text;
                else if (control is ComboBox cb) text = cb.Text;

                if (string.IsNullOrWhiteSpace(text))
                {
                    control.BorderBrush = Brushes.OrangeRed;
                    emptyFields.Add(control);
                }
            }

            if (emptyFields.Count > 0)
            {
                var result = MessageBox.Show(
                    "Некоторые поля не заполнены. Все равно сохранить расчет?",
                    "Сохранение расчета",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                // Восстанавливаем стиль контрола в исходное состояние
                foreach (var control in emptyFields) control.ClearValue(BorderBrushProperty);

                if (result == MessageBoxResult.No)
                {
                    emptyFields[0].Focus();
                    return false;
                }
            }


            bool isBelowLimit = (!IsAgent && Result < 4200) || (IsAgent && Result < 2700);
            if (isBelowLimit)
            {
                if (CurrentManager.IsEngineer)
                {
                    MessageBox.Show(
                        "Стоимость КП ниже минимальной.\n" +
                        "Установите корректный коэффициент или используйте галочку «Минималка» для автоматической подстройки.",
                        "Сохранение расчета",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    // Подсветка
                    DetailsToggle.BorderBrush = LimitCheckBorder.BorderBrush = Brushes.OrangeRed;
                    LimitCheckBorder.BorderThickness = new Thickness(1);

                    // Сброс подсветки через мгновение или при изменении — по желанию
                    return false;
                }
                else
                {
                    // Менеджер: галочка = "снять ограничения"
                    if (LimitCheck.IsChecked == false)
                    {
                        // Подсветка и сообщение (как у вас было)
                        DetailsToggle.BorderBrush = LimitCheckBorder.BorderBrush = Brushes.OrangeRed;
                        LimitCheckBorder.BorderThickness = new Thickness(1);

                        MessageBox.Show(
                            "Проверьте стоимость КП:\n" +
                            "• С НДС — не менее 4200 руб.\n" +
                            "• Без НДС — не менее 2700 руб.\n\n" +
                            "Установите галочку «Снять ограничения», если всё равно хотите сохранить.",
                            "Сохранение расчета",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation);

                        DetailsToggle.BorderBrush = LimitCheckBorder.BorderBrush = null;
                        DetailsToggle.BorderThickness = new Thickness(1);
                        return false;
                    }
                    // Иначе — менеджер с галочкой → разрешаем сохранение
                }
            }

            return true;
        }

        private void LimitCheck_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentManager.IsEngineer)
                return; // Для менеджера — поведение не меняется (галочка просто игнорируется или используется по-другому)

            if (LimitCheck.IsChecked == true)
            {
                // Включено: поднимаем коэффициент до минимального
                double minLimit = IsAgent ? 2700 : 4200;

                if (Result <= 0)
                {
                    MessageBox.Show("Невозможно рассчитать коэффициент: текущая стоимость недоступна или нулевая.");
                    LimitCheck.IsChecked = false; // отменяем действие
                    return;
                }

                SetRatio(Math.Ceiling(minLimit / Result * 100) / 100);
            }
            else
            {
                // Выключено: возвращаем коэффициент к 1 (или к исходному значению)
                SetRatio(1);
            }

            // Снимаем визуальную подсветку
            DetailsToggle.BorderBrush = null;
            LimitCheckBorder.BorderBrush = null;
            LimitCheckBorder.BorderThickness = new Thickness(0);
        }

        public float GetServices()          //метод получения стоимости ВСЕХ услуг
        {
            float servicesPrice = 0;

            servicesPrice += GetServicesPrice();            //получаем основные работы
            servicesPrice += GetWeldAssembly();             //добавляем сварку из сборок
            servicesPrice += GetPaintAssembly();            //добавляем окраску из сборок
            servicesPrice += Delivery * DeliveryRatio;      //добавляем доставку

            return (float)Math.Ceiling(servicesPrice);
        }

        public float GetMaterial()          //метод получения стоимости ВСЕГО материала
        {
            float metalPrice = 0;

            metalPrice += GetMetalPrice();          //получаем основной материал
            metalPrice += GetBasketPrice();         //добавляем покупные изделия

            return (float)Math.Ceiling(metalPrice);
        }

        public float GetMetalPrice()        //метод получения стоимости металла
        {
            float metalPrice = 0;
            foreach (DetailControl d in DetailControls)
                foreach (TypeDetailControl t in d.TypeDetailControls) metalPrice += t.Result;
            return (float)Math.Ceiling(metalPrice);
        }

        public float GetServicesPrice()     //метод получения стоимости основных работ
        {
            float servicesPrice = 0;
            foreach (DetailControl det in DetailControls)
                foreach (TypeDetailControl type in det.TypeDetailControls)
                    foreach (WorkControl work in type.WorkControls) servicesPrice += work.Result;
            return (float)Math.Ceiling(servicesPrice);
        }

        public float GetWeldAssembly()      //метод получения стоимости сварки из сборок
        {
            float weldAssembly = 0;
            if (AssemblyWindow.A.Assemblies.Count > 0)
                foreach (var assembly in AssemblyWindow.A.Assemblies) weldAssembly += assembly.WeldPrice;
            return (float)Math.Ceiling(weldAssembly);
        }

        public float GetPaintAssembly()     //метод получения стоимости окраски из сборок
        {
            float paintAssembly = 0;
            if (AssemblyWindow.A.Assemblies.Count > 0)
                foreach (var assembly in AssemblyWindow.A.Assemblies) paintAssembly += assembly.PaintPrice;
            return (float)Math.Ceiling(paintAssembly);
        }

        public float GetBasketPrice()       //метод получения стоимости всех покупных изделий
        {
            float basketPrice = 0;
            foreach (BasketControl b in BasketControls) basketPrice += b.Basket.Total;
            return (float)Math.Ceiling(basketPrice);
        }

        public string ShortManager()        //метод, возвращающий сокращенное имя менеджера
        {
            return ManagerDrop.Text switch
            {
                "Спильная Марина" => "мр",
                "Гамолина Светлана" => "сг",
                "Андрейченко Алексей" => "аа",
                "Сергеев Юрий" => "ю",
                "Сергеев Алексей" => "ас",
                "Серых Михаил" => "мс",
                "Мешеронова Мария" => "м",
                "Барабанов Дмитрий" => "дб",
                "Абрамова Анна" => "ан",
                "Еремин Андрей" => "еа",
                _ => ""
            };
        }

        private void CreateDelivery(object sender, RoutedEventArgs e) { StatusBegin($"{CreateDelivery()}", StatusMessageType.Success); }
        private string CreateDelivery()     //метод построения строки запроса в логистику
        {
            StringBuilder sb = new(EndDate()?.ToString("d MMM"));   //инициализируем строку датой отгрузки в формате "d MMM"

            //если есть номер заказа, добавляем его; иначе добавляем номер КП
            if (ActiveOffer != null && ActiveOffer.Order != null) sb.Append($", №{ActiveOffer.Order}");
            else sb.Append($", №{Order.Text}");

            sb.Append($", {CustomerDrop.Text}({ShortManager()})");  //добавляем заказчика и менеджера в сокращенном виде
            sb.Append($", примерно {GetTotalMass()} кг;");          //добавляем массу всех деталей
            sb.Append($" {Adress.Text}");                           //и, наконец, адрес доставки и контакт

            Clipboard.SetText($"{sb}");
            return $"{sb}";
        }

        private float GetTotalMass()        //метод расчета общей массы ВСЕХ деталей
        {
            float total = 0;

            //если есть нарезанные детали, подсчитываем их массу
            if (Parts.Count > 0) foreach (Part part in Parts) total += part.Mass * part.Count;
            //подсчитываем массу всех деталей, если они не "Комплект деталей!
            foreach (DetailControl det in DetailControls.Where(d => !d.Detail.IsComplect)) total += det.Detail.Mass;

            return (float)Math.Ceiling(total);      //округляем до целого в большую сторону
        }

        public static float MassRatio(float _mass)      //метод получения коэффициента за вес
        {
            float _massRatio = _mass switch
            {
                <= 5 => 1,
                <= 10 => 1.2f,
                <= 20 => 1.4f,
                <= 50 => 1.6f,
                <= 100 => 2,
                _ => 3,
            };
            return _massRatio;
        }

        public static float RatioSale(int _count)       //метод расчета скидки от объема
        {
            return _count switch
            {
                <= 50 => 1,
                <= 100 => 0.9f,
                <= 500 => 0.8f,
                <= 1000 => 0.7f,
                <= 2000 => 0.6f,
                _ => 0.5f
            };
        }

        public static float Parser(string data)                         //обёртка для парсинга float-значений
        {
            //если число одновременно содержит и запятую, и точку - удаляем запятую
            if (data.Contains(',') && data.Contains('.')) data = data.Replace(",", "");

            //если есть запятая в строке, заменяем ее на точку, и парсим строку в число
            if (float.TryParse(data.Replace(',', '.'), NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out float f)) return f;
            else return 0;
        }

        public static BitmapImage CreateBitmap(byte[] imageBytes)       //метод преобразования массива байтов в изображение BitmapImage
        {
            BitmapImage? image = new();
            image.BeginInit();
            image.StreamSource = new MemoryStream(imageBytes);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }

        public static void CreateBitmapFromVisual(Visual target, string fileName)   //метод создания снимка окна
        {
            var bounds = VisualTreeHelper.GetDescendantBounds(target);
            var renderTarget = new RenderTargetBitmap(
                (int)bounds.Width,
                (int)bounds.Height,
                96,
                96,
                PixelFormats.Pbgra32);

            var visual = new DrawingVisual();

            using (var context = visual.RenderOpen())
            {
                var visualBrush = new VisualBrush(target);
                context.DrawRectangle(visualBrush, null, new Rect(new System.Windows.Point(), bounds.Size));
            }

            renderTarget.Render(visual);
            var bitmapEncoder = new BmpBitmapEncoder();
            bitmapEncoder.Frames.Add(BitmapFrame.Create(renderTarget));
            using var stm = File.Create(fileName);
            bitmapEncoder.Save(stm);
        }

        private double? _partsStackExpandedHeight = null;
        private void OnOfferToggleClick(object sender, RoutedEventArgs e)
        {
            if (OfferToggle.IsChecked == true)
            {
                // Кэшируем высоту при первом раскрытии
                if (!_partsStackExpandedHeight.HasValue)
                {
                    // Сохраняем текущую высоту, чтобы не мигало
                    var originalHeight = PartsStack.Height;
                    PartsStack.Height = double.NaN; // сбрасываем ограничение

                    // Измеряем с шириной, равной ActualWidth родителя (OrderGrid)
                    double width = OrderGrid.ActualWidth - PartsStack.Margin.Left - PartsStack.Margin.Right;
                    if (width <= 0) width = 600; // fallback

                    PartsStack.Measure(new Size(width, double.PositiveInfinity));
                    _partsStackExpandedHeight = PartsStack.DesiredSize.Height;

                    // Возвращаем исходную высоту (0), чтобы не отобразилось до анимации
                    PartsStack.Height = originalHeight;
                }

                var expand = (Storyboard)FindResource("ExpandParts");
                var anim = (DoubleAnimation)expand.Children[0];
                anim.To = _partsStackExpandedHeight.Value;
                expand.Begin(this);
            }
            else
            {
                var collapse = (Storyboard)FindResource("CollapseParts");
                collapse.Begin(this);
            }
        }

        private void OnDetailsToggleClick(object sender, RoutedEventArgs e)
        {
            DetailsPanel.Visibility = DetailsToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenMaps(object sender, RoutedEventArgs e)         //метод открытия карт зонирования стоимости доставки
        {
            string url = "https://yandex.ru/maps/2/saint-petersburg/?ll=30.293848%2C59.914700&mode=usermaps&source=constructorLink&um=constructor%3Aee68688ecf05643f44c063ca6d885e8262ae116ac6d5f7921742ebb42a75a78d&z=12.11/";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        #endregion


        //-------------Шаблоны и настройки-----------------//
        #region
        // Глобальные шаблоны (загружаются один раз)
        private readonly ObservableCollection<FolderPreset> _globalPresets = FolderPresetManager.LoadPresets();

        // Локальные шаблоны для ТЕКУЩЕГО расчета (клонирование изолирует выбор)
        public ObservableCollection<FolderPreset> CurrentCalculationFolders { get; } = new();

        // Вызывать при создании нового расчета или загрузке существующего
        public void InitializeFolderPresets()
        {
            CurrentCalculationFolders.Clear();
            foreach (var preset in _globalPresets)
            {
                // Сбрасываем IsEnabled при новом расчете (или сохраняем, если нужно)
                var clone = preset.Clone();
                clone.IsEnabled = false;
                CurrentCalculationFolders.Add(clone);
            }
        }

        // Открытие окна настроек
        private void OpenFolderPresetSettings(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new FolderPresetSettingsWindow(_globalPresets);
            if (settingsWindow.ShowDialog() == true)
            {
                // Пересоздаем локальный список с учетом изменений в глобальном
                InitializeFolderPresets();
            }
        }

        public void CreateSelectedFolders(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) return;

            foreach (var folder in CurrentCalculationFolders.Where(f => f.IsEnabled))
            {
                try
                {
                    string safeName = SanitizeFolderName(folder.Name);
                    if (string.IsNullOrWhiteSpace(safeName)) continue;

                    string fullPath = Path.Combine(rootPath, safeName);
                    if (!Directory.Exists(fullPath))
                        Directory.CreateDirectory(fullPath);
                }
                catch (Exception ex)
                {
                    // Логируем, но не прерываем сохранение
                    StatusBegin($"⚠️ Не удалось создать папку '{folder.Name}': {ex.Message}");
                }
            }
        }

        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Без названия";

            // Убираем только реально недопустимые символы Windows
            char[] invalid = Path.GetInvalidFileNameChars();
            string cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray());

            // Windows сама обрезает конечные пробелы и точки, но для чистоты можно сделать Trim()
            return cleaned.Trim();
        }

        private void ShowFolderPresetsDialog(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderPresetsDialog(CurrentCalculationFolders);

            if (dialog.ShowDialog() == true)
            {
                StatusBegin($"Выбрано папок: {CurrentCalculationFolders.Count(p => p.IsEnabled)}");
            }
        }
        #endregion


        //-------------Выход и перезагрузка----------------------//
        #region
        public void Exit(object sender, RoutedEventArgs e) => Environment.Exit(0);
        private void Exit(object sender, CancelEventArgs e)
        {
            MessageBoxResult response = MessageBox.Show("Выйти без сохранения?", "Выход из программы",
                                           MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (response == MessageBoxResult.Yes) Environment.Exit(0);
            e.Cancel = true;
        }

        private void Restart(object sender, RoutedEventArgs e)
        {
            bool isUpToDate = CheckVersion(out string currentVersion);

            // ⭐ Формируем сообщение в зависимости от состояния версии
            string message = isUpToDate
                ? $"Metal-Code не требует обновления.\nТекущая версия: {currentVersion}\n\nНажмите \"Да\" для принудительного обновления."
                : $"Доступна новая версия программы.\nТекущая версия: {currentVersion}\n\nНажмите \"Да\" для обновления.\nНажмите \"Нет\" для отмены (не забудьте сохранить текущий расчёт).";

            MessageBoxImage icon = isUpToDate ? MessageBoxImage.Question : MessageBoxImage.Exclamation;

            MessageBoxResult response = MessageBox.Show(
                message,
                "Обновление программы",
                MessageBoxButton.YesNo,
                icon);

            if (response == MessageBoxResult.No) return;

            Restart();
        }
        private void Restart()
        {
            Process.Start(Directory.GetCurrentDirectory() + "\\Metal-Code.Updater.exe");
            Environment.Exit(0);
        }

        public bool CheckVersion(out string _version)
        {
            string networkFolder = connections[6]; // M:\Metal-Code

            // ⭐ Проверяем доступность сетевой папки напрямую
            if (!Directory.Exists(networkFolder))
            {
                _version = $"{Version}, без подключения к серверу.";
                return true; // Считаем версию актуальной (не можем проверить)
            }

            string serverVersionPath = Path.Combine(networkFolder, "version.txt");
            string localVersionPath = Path.Combine(Directory.GetCurrentDirectory(), "version.txt");

            if (!File.Exists(serverVersionPath) || !File.Exists(localVersionPath))
            {
                _version = $"{Version}, файл версии не найден.";
                return true;
            }

            try
            {
                _version = File.ReadAllText(serverVersionPath).Trim();
                string localVersion = File.ReadAllText(localVersionPath).Trim();
                return _version == localVersion;
            }
            catch (Exception ex)
            {
                _version = $"{Version}, ошибка чтения: {ex.Message}";
                return true; // При ошибке считаем версию актуальной
            }
        }
        #endregion


        //-------------Экспериметы и тесты-----------------------//
        #region               
        private void SortFilesByMonth(object sender, RoutedEventArgs e)
        {
            // Создаём диалог выбора папки
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Выберите папку с неотсортированными файлами";
            dialog.UseDescriptionForTitle = true; // Для Windows Vista и выше
                                                  // Показываем диалог и проверяем результат
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedPath = dialog.SelectedPath;

                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    MessageBox.Show("Путь к папке пустой.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    // Вызываем метод сортировки
                    FileSorter.SortFilesByMonth(selectedPath);
                    MessageBox.Show($"Файлы успешно отсортированы в папке:\n{selectedPath}", "Готово",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Произошла ошибка при сортировке файлов:\n{ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion
    }
}