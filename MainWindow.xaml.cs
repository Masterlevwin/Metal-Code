using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using HandyControl.Data;
using Metal_Code.Converters;
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
using System.Management;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Border = System.Windows.Controls.Border;
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
        {   //дом
            "Data Source=managers.db",
            //$"Data Source = C:\\ProgramData\\Metal-Code\\managers.db",
            $"Data Source = C:\\Users\\Михаил\\Desktop\\Тест\\Базы\\managers.db",
            "Data Source=typedetails.db",
            $"Data Source = C:\\ProgramData\\Metal-Code\\typedetails.db",
            "Data Source=works.db",
            $"Data Source = C:\\ProgramData\\Metal-Code\\works.db",
            "Data Source=metals.db",
            $"Data Source = C:\\ProgramData\\Metal-Code\\metals.db",
            //$"C:\\Users\\Михаил\\Desktop\\Тесты\\Производство",
            $"Y:\\Производство\\Laser rezka\\В работу",
            $"M:\\Metal-Code",
            $"C:\\ProgramData",
            $"Host=srv-fs-laser;Port=5432;Database=metalcodedb;Username=postgres;Password=lazerpro",
            "Data Source=templates.db",

            //прод
            //"Data Source=managers.db",
            //$"Data Source = Y:\\Конструкторский отдел\\Расчет Заказов ЛФ Сервер\\Metal-Code\\managers.db",
            //"Data Source=typedetails.db",
            //$"Data Source = Y:\\Конструкторский отдел\\Расчет Заказов ЛФ Сервер\\Metal-Code\\typedetails.db",
            //"Data Source=works.db",
            //$"Data Source = Y:\\Конструкторский отдел\\Расчет Заказов ЛФ Сервер\\Metal-Code\\works.db",
            //"Data Source=metals.db",
            //$"Data Source = Y:\\Конструкторский отдел\\Расчет Заказов ЛФ Сервер\\Metal-Code\\metals.db",
            //$"Y:\\Производство\\Laser rezka\\В работу",
            //$"M:\\Metal-Code",
            //$"Y:\\Конструкторский отдел\\Расчет Заказов ЛФ Сервер\\Metal-Code",
            //$"Host=srv-fs-laser;Port=5432;Database=metalcodedb;Username=postgres;Password=lazerpro",
            //"Data Source=templates.db",
        };

        public ProductViewModel ProductModel { get; set; } = new(new DefaultDialogService(), new JsonFileService(), new Product());
        public RequestControl? RequestControl;

        public Manager CurrentManager = new();      //текущий авторизованный менеджер
        public Manager TargetManager = new();       //выбранный менеджер из списка
        public Customer TargetCustomer = new();     //выбранный заказчик из списка

        public List<TechItem> TechItems = new();    //список полученных объектов из строк заявки
        public ObservableCollection<Manager> Managers { get; set; } = new();
        public ObservableCollection<Offer> Offers { get; set; } = new();
        public List<Offer> CurrentOffers { get; set; } = new();
        public List<Offer> ReportOffers { get; set; } = new();
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

        //временный словарь расчетов для синхронизации с основной базой (0 - новые, 1 - удаленные, 2 - измененные)
        private readonly Dictionary<byte, List<Offer>> TempOffersDict = new() { [0] = new(), [1] = new(), [2] = new() };
        private readonly Dictionary<string, float> TempWorksDict = new();                                       //временный словарь работ

        public readonly List<float> Destinies = new() { .5f, .7f, .8f, 1, 1.2f, 1.5f, 2, 2.5f, 3, 4, 5, 6, 8, 10, 12, 14, 16, 18, 20, 22, 25, 30 };
        public Dictionary<string, Dictionary<float, (float, float, float)>> MetalDict = new();                  //словарь материалов
        public Dictionary<double, float> WideDict = new();                                                      //словарь отверстий
        public Dictionary<Metal, float> MetalRatioDict = new();                                                 //словарь коэффициентов за материал

        //----------Свойства и их основные методы---------//
        #region
        private string version = "2.7.0";
        public string Version
        {
            get => version;
            set => version = value;
        }

        private bool isLocal = true;    //запуск локальной версии
        //private bool isLocal = false;   //запуск основной версии
        public bool IsLocal
        {
            get => isLocal;
            set
            {
                isLocal = value;
                OnPropertyChanged(nameof(IsLocal));
            }
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

        private string searchOffers = "";
        public string SearchOffers
        {
            get => searchOffers;
            set
            {
                searchOffers = value;
                OnPropertyChanged(nameof(SearchOffers));
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

        private DateTime startDay = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        public DateTime StartDay
        {
            get => startDay;
            set
            {
                startDay = value;
                OnPropertyChanged(nameof(StartDay));
            }
        }

        private DateTime endDay = DateTime.UtcNow;
        public DateTime EndDay
        {
            get => endDay;
            set
            {
                endDay = value;
                OnPropertyChanged(nameof(EndDay));
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
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            M = this;
            Title = $"Metal-Code {Version}";

            //if (!CheckVersion(out string _version)) Restart();
            //else UpdateDatabases();
            //AutoRemoveOffers();

            DataContext = ProductModel;
            Loaded += LoadDataBases;
        }

        //-------------Основные методы-----------//
        #region
        private void LoadDataBases(object sender, RoutedEventArgs e)    // при загрузке окна
        {
            using TypeDetailContext dbT = new(IsLocal ? connections[2] : connections[3]);
            dbT.TypeDetails.Load();
            TypeDetails = dbT.TypeDetails.Local.ToObservableCollection();

            using WorkContext dbW = new(IsLocal ? connections[4] : connections[5]);
            dbW.Works.Load();
            Works = dbW.Works.Local.ToObservableCollection();

            using MetalContext dbM = new(IsLocal ? connections[6] : connections[7]);
            dbM.Metals.Load();
            Metals = dbM.Metals.Local.ToObservableCollection();

            InitializeDict();

            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);

            AddSpecTemplateColumnsIfMissing(db);

            db.Managers.Load();
            Managers = db.Managers.Local.ToObservableCollection();

            db.Offers.Load();
            Offers = db.Offers.Local.ToObservableCollection();
            InitializeOffersView();

            if (Managers.Count == 0) ShowWindow(new RegistrationWindow());  //если пользователей в базе нет, запускаем процесс регистрации
            //else if (!CheckMachine())                                       //проверяем защитный файл
            //{
            //    MessageBox.Show($"Данная копия программы защищена. Ее невозможно запустить на этом компьютере!");
            //    Environment.Exit(0);
            //}
            else if (!CheckMachineName()) ShowWindow(new LoginWindow());    //проверяем пользователя
            else NewProject();                                              //если все проверки пройдены, создаем новый проект

            OfferToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            // Проверяем, нужно ли открыть файл
            var filePath = App.StartupFileToOpen;
            if (!string.IsNullOrEmpty(filePath))
            {
                OpenFileOnStartup(filePath);
            }

            ShowUpdateWindow();
        }

        public void ShowUpdateWindow()          // метод добавления и загрузки обновлений
        {
            // Создаём контекст
            using var ctx = new RequestContext(connections[12]);
            ctx.EnsureUpdateTableExists();

            // Гарантируем, что история есть
            ctx.EnsureUpdateHistoryInitialized();

            // Добавить новое обновление (если его ещё нет)
            ctx.AddNewUpdateIfNotExists(
                version: "v2.6.9.6",
                releaseDate: new DateTime(2026, 03, 30),
                description: "Изменилась форма ПКИ.\nДобавлен столбец \"Профиль\" в шаблон заявки.",
                screenshotPath: "/Updates/v2.6.9.6_2026-03-30.png"
            );

            // Получаем новые обновления
            var newUpdates = ctx.GetNewStartupUpdates();

            if (newUpdates.Any())
            {
                var updateWindow = new UpdateWindow(newUpdates); // передаём список в окно
                updateWindow.ShowDialog();

                // После закрытия — помечаем как просмотренные
                using var freshCtx = new RequestContext(connections[12]); // или переиспользуйте, если в том же потоке
                freshCtx.MarkStartupUpdatesAsSeen();
            }
        }

        private void OnUpdatesMenuItemClick(object sender, RoutedEventArgs e)
        {
            using var ctx = new RequestContext(connections[12]);

            // Показываем ВСЮ историю (без фильтра IsShownAtStartup)
            var allUpdates = ctx.UpdateItems
                .OrderByDescending(u => u.ReleaseDate)
                .ToList();

            var updateWindow = new UpdateWindow(allUpdates);
            updateWindow.ShowDialog();
        }

        public void OpenFileOnStartup(string filePath)
        {
            try
            {
                var product = ProductModel.fileService.Open(filePath);
                if (product != null)
                {
                    ProductModel.Product = product;
                    LoadProduct();
                    SaveOrRemoveOffer(true, filePath);
                }
                else
                {
                    MessageBox.Show("Не удалось загрузить файл расчёта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public static bool CheckMachine()   // проверка серийного номера жесткого диска
        {
            ManagementObjectSearcher searcher = new("SELECT * FROM Win32_PhysicalMedia");

            foreach (ManagementObject hdd in searcher.Get().Cast<ManagementObject>())
                if (DecryptFile(out string s) && s == $"{hdd["SerialNumber"]}") return true;

            return false;
        }

        public static void EncryptFile()    // создание защитного файла
        {
            if (File.Exists(Directory.GetCurrentDirectory() + "\\encrypt.dat")) return;

            ManagementObjectSearcher searcher = new("SELECT * FROM Win32_PhysicalMedia");

            foreach (ManagementObject hdd in searcher.Get().Cast<ManagementObject>())
            {
                using FileStream fs = File.Create(Directory.GetCurrentDirectory() + "\\encrypt.dat");
                byte[] info = new UTF8Encoding(true).GetBytes($"{hdd["SerialNumber"]}");
                fs.Write(info, 0, info.Length);
                break;
            }
        }

        public static bool DecryptFile(out string s)    // проверка защитного файла
        {
            s = "";
            if (!File.Exists(Directory.GetCurrentDirectory() + "\\encrypt.dat")) return false;

            using StreamReader sr = File.OpenText(Directory.GetCurrentDirectory() + "\\encrypt.dat");
            s = sr.ReadLine();
            return true;
        }

        private bool CheckMachineName()     // авторизация на основе имени компьютера
        {
            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);

            db.Managers.Load();
            Managers = db.Managers.Local.ToObservableCollection();

            Manager? manager = Managers.FirstOrDefault(c => c.Contact == Environment.MachineName);

            if (manager != null)
            {
                ManagerDrop.ItemsSource = Managers.Where(m => !m.IsEngineer);     //список ТОЛЬКО менеджеров (для выставления КП)

                db.Customers.Load();
                Customers = db.Customers.Local.ToObservableCollection();

                CurrentManager = manager;                                                       //определяем текущего менеджера
                Login.Header = CurrentManager.Name;
                if (ManagerDrop.Items.Contains(manager)) ManagerDrop.SelectedItem = manager;    //устанавливаем менеджера по умолчанию

                if (CurrentManager.IsEngineer)
                {
                    IsEnabled = false;

                    SetManagerWindow setManagerWindow = new();
                    if (setManagerWindow.ShowDialog() == true) ManagerDrop.SelectedItem = setManagerWindow.SelectManager;

                    IsEnabled = true;
                }

                ReportTab.Visibility = BonusStack.Visibility = CurrentManager.IsEngineer ? Visibility.Collapsed : Visibility.Visible;
                LimitCheck.Content = CurrentManager.IsEngineer ? "Минималка" : "Снять ограничения";

                return true;
            }
            return false;
        }

        private void ShowLoginWindow(object sender, RoutedEventArgs e)  // обработчик пункта меню "Сменить пользователя"
        {
            MessageBoxResult response = MessageBox.Show("Сменить текущего пользователя?\nЕсли \"Да\", потребуется авторизация, и текущий расчет будет очищен!", "Сменить пользователя",
                               MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (response == MessageBoxResult.No) return;
            else ShowWindow(new LoginWindow());
        }

        private void ShowWindow(Window window)          // установка текущего и выбранного менеджеров
        {
            IsEnabled = false;

            if (window.ShowDialog() == true)
            {
                if (CurrentManager.IsEngineer)
                {
                    SetManagerWindow setManagerWindow = new();
                    if (setManagerWindow.ShowDialog() == true) ManagerDrop.SelectedItem = setManagerWindow.SelectManager;
                }
                else ManagerDrop.SelectedItem = CurrentManager;

                ReportTab.Visibility = BonusStack.Visibility = CurrentManager.IsEngineer ? Visibility.Collapsed : Visibility.Visible;
                LimitCheck.Content = CurrentManager.IsEngineer ? "Минималка" : "Снять ограничения";

                IsEnabled = true;
                NewProject();
            }
        }

        private void ManagerChanged(object sender, SelectionChangedEventArgs e)     //при смене менеджера
        {
            if (ManagerDrop.SelectedItem is Manager man)
            {
                TargetManager = man;
                IsLaser = TargetManager.IsLaser;
                ManagerChanged();
            }
        }
        private void ManagerChanged()
        {
            CurrentCustomers = Customers.Where(m => m.ManagerId == TargetManager.Id).OrderBy(s => s.Name).ToList();
            CustomerDrop.ItemsSource = CurrentCustomers;

            // Сбрасываем режимы
            _searchQuery = string.Empty;
            _isProductionMode = false;
            if (InProductionFilterToggle.IsChecked == true)
                InProductionFilterToggle.IsChecked = false;

            // Перестраиваем отображение
            ApplyCurrentMode();

            SummaryInfoTextBlock.Text = $"Всего расчётов: {Offers.Count} шт.";
            if (InProductionFilterToggle.IsChecked == true) InProductionFilterToggle.IsChecked = false;

            if (TargetManager == CurrentManager)
            {
                ReportDrop.ItemsSource = Months;
                ReportDrop.SelectedItem = Months[DateTime.Now.Month - 1];
                ReportChanged(Months[DateTime.Now.Month - 1]);
            }
            else ReportOffers.Clear();
        }

        private void InitializeOffersView()
        {
            // Привязываемся к CurrentOffers, а не к Offers
            OffersView = CollectionViewSource.GetDefaultView(CurrentOffers);

            // Группировка и сортировка настраиваются ТОЛЬКО ЗДЕСЬ
            OffersView.GroupDescriptions.Clear();
            OffersView.SortDescriptions.Clear();

            OffersView.SortDescriptions.Add(
                new SortDescription(nameof(Offer.CreatedDate), ListSortDirection.Ascending));
            OffersView.GroupDescriptions.Add(
                new PropertyGroupDescription(nameof(Offer.ParentQuoteNumber)));

            OffersGrid.ItemsSource = OffersView;
        }

        private void ApplyCurrentMode()
        {
            IEnumerable<Offer> dataToDisplay;

            if (_isProductionMode)
            {
                // Режим "В производстве"
                dataToDisplay = Offers.Where(o => !string.IsNullOrEmpty(o.Order) && o.EndDate == null);
                var totalAmount = Math.Ceiling(dataToDisplay.Sum(o => o.Amount));
                SummaryInfoTextBlock.Text = $"В производстве: {dataToDisplay.Count()} шт на сумму {totalAmount:N0} руб.";
            }
            else if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                // Режим поиска (по ВСЕЙ коллекции Offers)
                string q = _searchQuery.Trim();
                dataToDisplay = Offers.Where(o =>
                    o.N?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    o.Company?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    o.Invoice?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    o.Order?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                SummaryInfoTextBlock.Text = $"Найдено: {dataToDisplay.Count()} из {Offers.Count} шт.";
            }
            else
            {
                // Режим по умолчанию: 30 последних расчётов текущего менеджера
                dataToDisplay = Offers
                    .Where(o => TargetManager != null && o.ManagerId == TargetManager.Id)
                    .OrderByDescending(o => o.CreatedDate)
                    .Take(30);
                SummaryInfoTextBlock.Text = $"Показано: {dataToDisplay.Count()} последних";
            }

            // Безопасно заменяем содержимое CurrentOffers
            CurrentOffers.Clear();
            foreach (var item in dataToDisplay.ToList()) CurrentOffers.Add(item);

            // Очищаем кэш конвертера и перерисовываем группы
            if (TryFindResource("CompanyNamesConverter") is GroupCompanyNamesConverter conv)
                conv.ClearCache();
            OffersView.Refresh();
        }

        private void InProductionFilterToggle_Click(object sender, RoutedEventArgs e)
        {
            _isProductionMode = InProductionFilterToggle.IsChecked == true;
            if (_isProductionMode) _searchQuery = string.Empty;
            ApplyCurrentMode();
        }

        //-------------Настройка блока отчетов-----------//
        readonly string[] Months = { "январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь" };

        // Норма рабочих часов по месяцам
        private readonly int[] WorkingHours = { 136, 152, 168, 168, 144, 152, 168, 168, 168, 168, 160, 168 };

        private bool _isReportByCreated = false; // false = EndDate (по умолчанию)
        public bool IsReportByCreated
        {
            get => _isReportByCreated;
            set
            {
                if (_isReportByCreated != value)
                {
                    _isReportByCreated = value;
                    OnPropertyChanged(nameof(IsReportByCreated));
                    RebuildCurrentReport();
                }
            }
        }
        private void RebuildCurrentReport()
        {
            if (ReportDrop.SelectedItem is string monthStr)
            {
                int monthIndex = Array.IndexOf(Months, monthStr);
                if (monthIndex == -1) return;

                var now = DateTime.Now;
                int selectedMonth = monthIndex + 1;

                int year = now.Year;
                // Если сейчас начало года (янв–март), а выбран конец года (окт–дек) → прошлый год
                if (now.Month <= 3 && selectedMonth >= 10)
                    year--;

                ReportChanged(new DateTime(year, selectedMonth, 1));
            }
        }

        private void ReportChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetManager != CurrentManager) return;
            if (ReportDrop.SelectedItem is string name) ReportChanged(name);
        }
        private void ReportChanged(string name)
        {
            foreach (string month in Months)
                if (month == name)
                {
                    DateTime now = DateTime.Now;
                    if (now.Month >= Array.IndexOf(Months, month) + 1)
                        ReportChanged(new DateTime(now.Year, Array.IndexOf(Months, month) + 1, 1));
                    else ReportChanged(new DateTime(now.Year - 1, Array.IndexOf(Months, month) + 1, 1));
                }
        }
        private void ReportChanged(DateTime target)
        {
            DateTime start = new(target.Year, target.Month, 1);
            DateTime end = start.AddMonths(1);

            ReportOffers = Offers.Where(o =>
                !string.IsNullOrEmpty(o.Order) && // есть номер заказа
                (
                    (IsReportByCreated &&
                     o.CreatedDate.HasValue &&
                     o.CreatedDate.Value >= start &&
                     o.CreatedDate.Value < end)
                    ||
                    (!IsReportByCreated &&
                     o.EndDate.HasValue &&
                     o.EndDate.Value >= start &&
                     o.EndDate.Value < end)
                )
            ).ToList();

            ReportGrid.ItemsSource = ReportOffers;
            ReportView();
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
                                    parts.Add(p.Part);
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
                        if (_part is null) LooseParts.Add(part);
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
        private void AutoRemoveOffers()             //метод удаления старых расчетов из локальной базы
        {
            if (!IsLocal || DateTime.UtcNow.DayOfWeek is not DayOfWeek.Friday) return;  //удаление старых расчетов выполняем только по пятницам

            using ManagerContext db = new(connections[0]);                  //подключаемся к локальной базе данных
            try
            {
                db.Offers.Load();                                           //загружаем все расчеты

                //получаем коллекцию отгруженных расчетов, которые созданы более 60 дней назад
                var offers = db.Offers.Where(o => o.EndDate != null && o.CreatedDate < DateTime.UtcNow.AddDays(-60));

                db.Offers.RemoveRange(offers);                              //удаляем полученную коллекцию старых расчетов
                db.SaveChanges();

                StatusBegin($"Общее количество расчетов в базе - {db.Offers.ToList().Count}.");
            }
            catch (DbUpdateConcurrencyException ex) { StatusBegin(ex.Message, StatusMessageType.Error); }
        }

        //метод запуска процесса обновления расчетов
        private void UpdateOffersCollection(object sender, RoutedEventArgs e) { CreateWorker(UpdateOffersCollection, ActionState.update); }
        private string UpdateOffersCollection(string? message = null)
        {
            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);
            db.Offers.Load();
            Offers = db.Offers.Local.ToObservableCollection();

            if (message != null && message != "") return message;
            return $"Список расчетов обновлен. Расчетов в базе - {Offers.Count}.";
        }


        private Border? _highlightedHeaderBorder;

        public void ScrollToGroupAndHighlight(string parentQuoteNumber)
        {
            if (string.IsNullOrEmpty(parentQuoteNumber) || OffersGrid.ItemsSource is not ICollectionView view)
                return;

            var targetGroup = view.Groups.Cast<CollectionViewGroup>()
                .FirstOrDefault(g => g.Name?.ToString() == parentQuoteNumber);

            if (targetGroup == null) return;

            if (OffersGrid.ItemContainerGenerator.ContainerFromItem(targetGroup) is not GroupItem groupItem)
            {
                Dispatcher.BeginInvoke(new Action(() => ScrollToGroupAndHighlight(parentQuoteNumber)),
                    DispatcherPriority.Background);
                return;
            }

            // Раскрываем только если есть Expander (группа с 2+ элементами)
            var expander = FindVisualChild<Expander>(groupItem);
            if (expander != null && !expander.IsExpanded)
                expander.IsExpanded = true;

            // Прокручиваем
            groupItem.BringIntoView();

            // ⬇️ Поиск Border: либо внутри Expander, либо напрямую в GroupItem ⬇️
            Border? headerBorder = null;
            if (expander != null)
                headerBorder = FindVisualChild<Border>(expander);

            headerBorder ??= FindVisualChild<Border>(groupItem);

            if (headerBorder == null) return;

            ClearGroupHighlight();
            _highlightedHeaderBorder = headerBorder;
            headerBorder.Background = new SolidColorBrush(Color.FromArgb(90, 70, 155, 80));
        }

        // Сброс подсветки (универсальный)
        private void ClearGroupHighlight()
        {
            _highlightedHeaderBorder?.ClearValue(Border.BackgroundProperty);
            _highlightedHeaderBorder = null;
        }

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

        private void OffersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0) ClearGroupHighlight();
        }

        //метод запуска процесса обновления заказчиков
        private void UpdateCustomersCollection(object sender, RoutedEventArgs e) { CreateWorker(UpdateCustomersCollection, ActionState.update); }
        private string UpdateCustomersCollection(string? message = null)
        {
            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);
            db.Customers.Load();
            Customers = db.Customers.Local.ToObservableCollection();

            if (message != null && message != "") return message;
            return $"Список заказчиков обновлен. Заказчиков в базе - {Customers.Count}.";
        }

        private void Show_SearchWindow(object sender, RoutedEventArgs e)
        {
            SearchWindow window = new SearchWindow();
            window.Show();
        }

        //метод запуска процесса загрузки расчетов из основной базы в локальную
        private void GetOffers_WithoutMainBase(object sender, RoutedEventArgs e) { CreateWorker(GetOffers_WithoutMainBase, ActionState.get); }
        private string GetOffers_WithoutMainBase(string? message = null)
        {
            if (!IsLocal) return "Загружена основная база расчетов. Обновление не требуется.";

            int count = 0;

            using ManagerContext db = new(connections[1]);      //подключаемся к основной базе данных
            bool isAvalaible = db.Database.CanConnect();        //проверяем, свободна ли база для подключения
            if (isAvalaible)
            {
                try
                {
                    //подключаемся к локальной базе данных
                    using ManagerContext dbLocal = new(connections[0]);

                    //ищем менеджера в основной базе по имени соответствующего выбранному, при этом загружаем его расчеты
                    Manager? _man = db.Managers.Where(m => m.Name == TargetManager.Name).Include(c => c.Offers).FirstOrDefault();

                    //ищем менеджера в локальной базе по имени соответствующего локальному, при этом загружаем его расчеты
                    Manager? _manLocal = dbLocal.Managers.Where(m => m.Name == TargetManager.Name).Include(c => c.Offers).FirstOrDefault();

                    if (_man?.Offers.Count > 0)
                        foreach (Offer offer in _man.Offers)
                        {
                            //проверяем наличие идентичного КП в локальной базе, и если такое уже есть, пропускаем копирование
                            Offer? tempOffer = _manLocal?.Offers.Where(o => o.N == offer.N
                                                                && o.Company == offer.Company
                                                                && o.Amount == offer.Amount).FirstOrDefault();
                            if (tempOffer != null) continue;

                            //копируем итеративное КП в новое с целью автоматического присваивания Id при вставке в базу
                            Offer _offer = new(offer.N, offer.Company, offer.Amount, offer.Material, offer.Services)
                            {
                                Agent = offer.Agent,
                                Invoice = offer.Invoice,
                                Order = offer.Order,
                                Act = offer.Act,
                                CreatedDate = offer.CreatedDate,
                                EndDate = offer.EndDate,
                                Autor = offer.Autor,
                                Manager = _manLocal,        //указываем соответствующего менеджера  
                                Data = offer.Data
                            };

                            _manLocal?.Offers.Add(_offer);  //переносим расчет в базу этого менеджера
                            count++;
                        }
                    dbLocal.SaveChanges();                  //сохраняем изменения в локальной базе данных
                }
                catch (DbUpdateConcurrencyException ex) { return ex.Message; }
            }
            return $"Локальная база обновлена. Добавлено {count} расчетов.";
        }

        //метод запуска процесса синхронизации расчетов с основной базой
        private void InsertDatabase(object sender, RoutedEventArgs e) { CreateWorker(InsertDatabase, ActionState.insert); }
        private string InsertDatabase(string? message = null)
        {
            if (!IsLocal || (TempOffersDict[0].Count == 0 && TempOffersDict[1].Count == 0 && TempOffersDict[2].Count == 0))
                return "Нет изменений для отправки в основную базу";

            int countChange = 0; int countRemove = 0; int countAdd = 0;

            using ManagerContext db = new(connections[1]);      //подключаемся к основной базе данных
            bool isAvalaible = db.Database.CanConnect();        //проверяем, свободна ли база для подключения
            if (isAvalaible)
            {
                try
                {
                    //перебираем список расчетов на синхронизацию изменений
                    if (TempOffersDict.TryGetValue(2, out List<Offer>? changeList) && changeList.Count > 0)
                        foreach (Offer offer in changeList)
                        {
                            Offer? tempOffer = db.Offers.Where(o => o.N == offer.N
                                                                && o.Company == offer.Company
                                                                && o.Amount == offer.Amount).FirstOrDefault();
                            if (tempOffer != null)
                            {
                                tempOffer.CreatedDate = offer.CreatedDate;
                                db.Entry(tempOffer).Property(o => o.CreatedDate).IsModified = true;
                                tempOffer.Agent = offer.Agent;
                                db.Entry(tempOffer).Property(o => o.Agent).IsModified = true;
                                tempOffer.Invoice = offer.Invoice;
                                db.Entry(tempOffer).Property(o => o.Invoice).IsModified = true;
                                tempOffer.Order = offer.Order;
                                db.Entry(tempOffer).Property(o => o.Order).IsModified = true;
                                countChange++;
                            }
                        }

                    //перебираем список расчетов на удаление
                    if (TempOffersDict.TryGetValue(1, out List<Offer>? removeList) && removeList.Count > 0)
                        foreach (Offer offer in removeList)
                        {
                            Offer? tempOffer = db.Offers.Where(o => o.N == offer.N
                                                                && o.Company == offer.Company
                                                                && o.Amount == offer.Amount).FirstOrDefault();
                            if (tempOffer != null)
                            {
                                db.Offers.Remove(tempOffer);
                                countRemove++;
                            }
                        }

                    //перебираем список расчетов на добавление
                    if (TempOffersDict.TryGetValue(0, out List<Offer>? addList) && addList.Count > 0)
                        foreach (Offer offer in addList)
                            if (offer.Manager is not null)
                            {
                                //ищем менеджера по имени соответствующего менеджера расчета
                                Manager? _man = db.Managers.FirstOrDefault(m => m.Name == offer.Manager.Name);

                                //копируем итеративное КП в новое с целью автоматического присваивания Id при вставке в базу
                                Offer _offer = new(offer.N, offer.Company, offer.Amount, offer.Material, offer.Services)
                                {
                                    Agent = offer.Agent,
                                    Invoice = offer.Invoice,
                                    Order = offer.Order,
                                    Act = offer.Act,
                                    CreatedDate = offer.CreatedDate,
                                    EndDate = offer.EndDate,
                                    Autor = offer.Autor,
                                    Manager = _man,             //указываем соответствующего менеджера
                                    Data = offer.Data
                                };

                                _man?.Offers.Add(_offer);       //переносим расчет в базу этого менеджера
                                countAdd++;
                            }
                    db.SaveChanges();                       //сохраняем изменения в основной базе данных

                    //очищаем списки во временном словаре
                    TempOffersDict[0].Clear();
                    TempOffersDict[1].Clear();
                    TempOffersDict[2].Clear();
                }
                catch (DbUpdateConcurrencyException ex) { return ex.Message; }
            }
            return $"Основная база обновлена. Добавлено {countAdd} расчетов. Удалено {countRemove} расчетов. Изменено {countChange} расчетов.";
        }

        public void SaveOrRemoveOffer(bool isSave, string? path = null)     //метод сохранения и удаления расчета
        {
            //подключаемся к базе данных
            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);
            bool isAvalaible = db.Database.CanConnect();                    //проверяем, свободна ли база для подключения
            if (isAvalaible)                                                //если база свободна, получаем выбранного менеджера
            {
                try
                {
                    //ищем менеджера в базе по имени соответствующего выбранному
                    Manager? _man = db.Managers.FirstOrDefault(m => m.Id == TargetManager.Id);

                    if (isSave)     //если метод запущен с параметром true, то есть в режиме сохранения
                    {
                        //сначала создаем новое КП
                        Offer _offer = new(Order.Text, CustomerDrop.Text, Result, GetMaterial(), GetServices())
                        {
                            Agent = IsAgent,
                            Manager = _man,
                            Data = SaveOfferData(),     //сериализуем расчет в виде строки json
                            Act = path                  //запоминаем путь к расчету
                        };

                        //при необходимости перезаписываем имя автора расчета
                        if (ActiveOffer?.Autor == CurrentManager.Name || ActiveOffer is null) _offer.Autor = CurrentManager.Name;
                        else _offer.Autor = $"{ActiveOffer?.Autor}\n{CurrentManager.Name} ({_offer.CreatedDate})";

                        if (IsLocal) TempOffersDict[0].Add(_offer);     //добавляем расчет во временный список для отправки в основную базу

                        _man?.Offers.Add(_offer);       //добавляем созданный расчет в базу этого менеджера
                        ActiveOffer = _offer;
                        LimitCheck.IsChecked = false;
                        message = $"Расчет {_offer.N} {_offer.Company} сохранен.";
                    }
                    else            //если метод запущен с параметром false, то есть в режиме удаления
                    {
                        if (OffersGrid.SelectedItem is Offer offer)                             //получаем выбранный расчет
                        {
                            Offer? _offer = db.Offers.FirstOrDefault(o => o.Id == offer.Id);    //ищем этот расчет по Id
                            if (_offer != null)
                            {
                                //добавляем расчет во временный список для удаления из основной базы, если текущий менеджер - владелец расчета
                                if (IsLocal && CurrentManager == TargetManager)
                                {
                                    if (TempOffersDict.TryGetValue(0, out List<Offer>? addList))
                                    {
                                        Offer? off = addList.FirstOrDefault(o => o.Data == offer.Data);
                                        if (off != null) addList.Remove(off);
                                    }
                                    TempOffersDict[1].Add(_offer);
                                }

                                _man?.Offers.Remove(_offer);            //если находим, то удаляем его из базы
                                StatusBegin($"Расчет {_offer.N} {_offer.Company} удален.");

                                DataGridRow row = (DataGridRow)OffersGrid.ItemContainerGenerator.ContainerFromIndex(OffersGrid.SelectedIndex);
                                SolidColorBrush _deleteBrush = new(Colors.Gray);
                                row.Background = _deleteBrush;

                                if (ReportOffers.Contains(offer)) ReportOffers.Remove(offer);
                            }
                        }
                    }

                    db.SaveChanges();               //сохраняем изменения в базе данных

                    if (isSave)
                    {
                        CreateWorker(UpdateOffersCollection, ActionState.update);   //и обновляем списки, если появился новый расчет

                        Customer? _customer = db.Customers.FirstOrDefault(x => x.Name == CustomerDrop.Text);
                        if (_customer is null) Log += $"\nЗаказчик {CustomerDrop.Text} не сохранен в базе. Добавьте его данные в базу, чтобы использовать их повторно.\n";

                        if (!CheckVersion(out string _version) && (Log is null || !Log.Contains("Текущая версия не актуальна. Рекомендуется обновить программу.")))
                            Log += $"\nТекущая версия не актуальна. Рекомендуется обновить программу.\n";

                        if (Log is not null && Log != "") MessageBox.Show(Log, "Обратите внимание!", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Log = null;
                    }
                }
                catch (DbUpdateConcurrencyException ex) { StatusBegin(ex.Message, StatusMessageType.Error); }
            }
        }

        private void UpdateOffer(object sender, RoutedEventArgs e) { UpdateOffer(OffersGrid); }
        private void UpdateOffer(DataGrid dataGrid)          //метод сохранения изменений в расчете
        {
            //подключаемся к базе данных
            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);
            bool isAvalaible = db.Database.CanConnect();                  //проверяем, свободна ли база для подключения
            if (isAvalaible && dataGrid.SelectedItem is Offer offer)      //если база свободна, получаем выбранный расчет
            {
                try
                {
                    Offer? _offer = db.Offers.FirstOrDefault(o => o.Id == offer.Id);      //ищем этот расчет по N
                    if (_offer != null)
                    {                   //менять можно только агента, номер счета, дату создания и номер заказа
                        if (_offer.Agent != offer.Agent)
                        {
                            _offer.Agent = offer.Agent;
                            db.Entry(_offer).Property(o => o.Agent).IsModified = true;
                        }
                        if (_offer.Invoice != offer.Invoice)
                        {
                            _offer.Invoice = offer.Invoice;
                            db.Entry(_offer).Property(o => o.Invoice).IsModified = true;
                        }
                        if (_offer.CreatedDate != offer.CreatedDate)
                        {
                            _offer.CreatedDate = offer.CreatedDate;
                            db.Entry(_offer).Property(o => o.CreatedDate).IsModified = true;
                        }
                        if (_offer.Order != offer.Order)
                        {
                            _offer.Order = offer.Order;
                            db.Entry(_offer).Property(o => o.Order).IsModified = true;
                            _offer.CreatedDate = DateTime.UtcNow;
                            db.Entry(_offer).Property(o => o.CreatedDate).IsModified = true;
                        }
                        if (_offer.Act != offer.Act)
                        {
                            _offer.Act = offer.Act;
                            db.Entry(_offer).Property(a => a.Act).IsModified = true;
                        }

                        //дата отгрузки меняется программно по кнопке добавления в отчет
                        if (_offer.EndDate != offer.EndDate)
                        {
                            _offer.EndDate = offer.EndDate;
                            db.Entry(_offer).Property(o => o.EndDate).IsModified = true;
                        }

                        //добавляем расчет во временный список для синхронизации с основной базой
                        if (IsLocal && ManagerDrop.SelectedItem is Manager man && CurrentManager == man)
                        {
                            if (TempOffersDict.TryGetValue(0, out List<Offer>? addList))
                            {
                                Offer? off = addList.FirstOrDefault(o => o.CreatedDate == offer.CreatedDate);
                                if (off != null)
                                {
                                    off.Agent = offer.Agent;
                                    off.Invoice = offer.Invoice;
                                    off.Order = offer.Order;
                                }
                            }
                            TempOffersDict[2].Add(_offer);
                        }

                        db.SaveChanges();
                        StatusBegin($"Данные расчета {offer.N} изменены", StatusMessageType.Success);

                        //создаем файлы комплектации и списка задач
                        if (ActiveOffer?.Data == _offer.Data) CreateComplect(connections[8], _offer);
                    }
                }
                catch (DbUpdateConcurrencyException ex) { StatusBegin(ex.Message, StatusMessageType.Error); }
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

        private void UpdateDatabases(object sender, RoutedEventArgs e)      //метод обновления локальных баз
        {
            UpdateDatabases();
            //if (!IsLocal) return;               //если запущена основная база, выходим из метода

            //MessageBoxResult response = MessageBox.Show(
            //    "Для обновления локальных баз, потребуется перезагрузка.\nНажмите \"Нет\", если требуется сохранить текущий расчет",
            //    "Обновление локальных баз", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

            //if (response == MessageBoxResult.No) return;

            //CreateWorker(InsertDatabase, ActionState.restartBases);         //запускаем фоновый процесс с перезапуском программы
        }
        private async void UpdateDatabases()                              //обновление баз заготовок, работ и материалов посредством замены файлов
        {
            try
            {
                // 1. Парсим весь прайс
                var rawItems = await LoadPriceListFromDialogAsync();

                // 2. Агрегируем в средние цены
                var averages = PriceAggregator.AggregateToAverages(rawItems);

                // 3. Создаём окно и загружаем данные
                var priceMetalWindow = new PriceMetalWindow();
                priceMetalWindow.LoadData(rawItems, averages);

                // 4. Показываем окно
                priceMetalWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка импорта", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // items готова для биндинга, фильтрации или сохранения в БД
            //if (!IsLocal || !Directory.Exists(connections[9])) return;     //если запущена основная база или нет директории, выходим из метода

            //string path = connections[9];    //путь к основным базам данных

            //if (File.Exists(path + "\\typedetails.db"))
            //{
            //    FileInfo dbTypeFile = new(path + "\\typedetails.db");
            //    dbTypeFile.CopyTo(Directory.GetCurrentDirectory() + "\\typedetails.db", true);
            //}

            //if (File.Exists(path + "\\works.db"))
            //{
            //    FileInfo dbWorkFile = new(path + "\\works.db");
            //    dbWorkFile.CopyTo(Directory.GetCurrentDirectory() + "\\works.db", true);
            //}

            //if (File.Exists(path + "\\metals.db"))
            //{
            //    FileInfo dbMetalFile = new(path + "\\metals.db");
            //    dbMetalFile.CopyTo(Directory.GetCurrentDirectory() + "\\metals.db", true);
            //}
        }

        public async Task<List<PriceListItem>> LoadPriceListFromDialogAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите прайс-лист(ы) металла",
                Filter = "Excel файлы|*.xls;*.xlsx|Все файлы|*.*",
                DefaultExt = ".xlsx",
                Multiselect = true, // 🔥 Ключевое изменение: разрешаем выбор нескольких файлов
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            bool? result = dialog.ShowDialog();
            if (result == true && dialog.FileNames.Length > 0)
            {
                var allItems = new List<PriceListItem>();
                var errors = new List<string>();

                // Парсим каждый выбранный файл
                foreach (var filePath in dialog.FileNames)
                {
                    try
                    {
                        var parser = new PriceListParserService();
                        var items = await parser.ParseAsync(filePath);

                        allItems.AddRange(items);
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибку, но продолжаем обработку остальных файлов
                        errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                    }
                }

                // Если все файлы не распарсились — выбрасываем исключение
                if (allItems.Count == 0 && errors.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Не удалось обработать ни один файл.\nОшибки:\n{string.Join("\n", errors)}");
                }

                return allItems;
            }

            // Пользователь нажал "Отмена"
            return new List<PriceListItem>();
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

        private void OnShipmentToggleClick(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn &&
                btn.DataContext is Offer offer &&
                !string.IsNullOrEmpty(offer.Order))
            {
                offer.EndDate = btn.IsChecked == true ? DateTime.UtcNow : null;
                UpdateOffer(OffersGrid);

                btn.ToolTip = offer.EndDate.HasValue
                    ? "Удалить из отчета"
                    : "Добавить в отчет";
            }
        }

        private void AddOfferToReport(object sender, RoutedEventArgs e)
        {
            if (OffersGrid.SelectedItem is Offer offer)
            {
                offer.Order = offer.N;
                UpdateOffer(OffersGrid);

                if (sender is Button btn)
                {
                    btn.Content = new Image() { Source = new BitmapImage(new Uri($"Images/delete.png", UriKind.Relative)) };
                    btn.ToolTip = "Удалить из отчета";
                    btn.Click -= AddOfferToReport;
                    btn.Click += RemoveOfferFromReport;
                }
            }
        }

        private void RemoveOfferFromReport(object sender, RoutedEventArgs e)
        {
            if (OffersGrid.SelectedItem is Offer offer)
            {
                offer.Order = "";
                UpdateOffer(OffersGrid);

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

        private void AddOfferToBonus(object sender, RoutedEventArgs e)
        {
            if (ReportGrid.SelectedItem is Offer offer)
            {
                offer.Invoice += " (без бонуса)";
                UpdateOffer(ReportGrid);

                if (sender is Button btn)
                {
                    btn.Content = new Image() { Source = new BitmapImage(new Uri($"Images/notbonus.png", UriKind.Relative)) };
                    btn.ToolTip = "Добавить бонус";
                    btn.Click -= AddOfferToBonus;
                    btn.Click += RemoveOfferFromBonus;
                }
            }
        }

        private void RemoveOfferFromBonus(object sender, RoutedEventArgs e)
        {
            if (ReportGrid.SelectedItem is Offer offer && offer.Invoice is not null && offer.Invoice.Contains(" (без бонуса)"))
            {
                offer.Invoice = offer.Invoice.Replace(" (без бонуса)", "");
                UpdateOffer(ReportGrid);

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


        //-------------Фоновые процессы----------//
        #region
        public enum ActionState         //условия окончания работы фонового процесса
        {
            none,                       //по умолчанию
            convert,                    //конвертация файлов dwg в dxf
            get,                        //получение расчетов из основной базы
            insert,                     //отправка расчетов в основную базу
            search,                     //поиск расчетов
            update,                     //обновление списков расчетов и заказчиков
            restartBases,               //перезапуск программы при обновлении баз
            restartApp,                 //перезапуск программы при обновлении программы
            exit,                       //выход из программы
            express                     //предварительный расчет
        }

        public ActionState State = ActionState.none;
        private string? message;

        public void CreateWorker(Func<string, string> func, ActionState state)        //метод создания фонового процесса
        {
            State = state;
            InsertProgressBar.Visibility = Visibility.Visible;

            switch (State)
            {
                case ActionState.convert:
                    StatusBegin($"Подождите, идет конвертация файлов dwg в dxf...", StatusMessageType.Warning);
                    break;
                case ActionState.get:
                    StatusBegin($"Подождите, идет получение расчетов из основной базы...", StatusMessageType.Warning);
                    GetBtn.IsEnabled = false;
                    InsertBtn.IsEnabled = false;
                    UpdateBtn.IsEnabled = false;
                    break;
                case ActionState.insert:
                    StatusBegin($"Подождите, идет отправка расчетов в основную базу...", StatusMessageType.Warning);
                    InsertBtn.IsEnabled = false;
                    UpdateBtn.IsEnabled = false;
                    break;
                case ActionState.update:
                    StatusBegin($"Подождите, идет обновление базы...", StatusMessageType.Warning);
                    IsEnabled = false;
                    break;
                case ActionState.restartBases:
                    StatusBegin($"Подождите, идет обновление локальных баз с последующей перезагрузкой...", StatusMessageType.Warning);
                    IsEnabled = false;
                    break;
                case ActionState.restartApp:
                    StatusBegin($"Подождите, идет обновление программы с последующей перезагрузкой...", StatusMessageType.Warning);
                    IsEnabled = false;
                    break;
                case ActionState.exit:
                    StatusBegin($"Подождите, идет отправка расчетов в основную базу с последующим выходом из программы...", StatusMessageType.Warning);
                    IsEnabled = false;
                    break;
                case ActionState.express:
                    StatusBegin($"Подождите, идет автоматическая раскладка и создание предварительного расчета...", StatusMessageType.Warning);
                    IsEnabled = false;
                    break;
                default:
                    break;
            }

            BackgroundWorker worker = new()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync(func);
        }

        void Worker_DoWork(object? sender, DoWorkEventArgs e)                            //обработчик события запуска фонового процесса
        {
            if (e.Argument is Func<string, string> func) e.Result = func($"{message}");
        }

        void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)          //обработчик промежуточных результатов фонового процесса
        {

        }

        void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)    //обработчик события завершения фонового процесса
        {
            switch (State)
            {
                case ActionState.convert:
                    StatusBegin($"{e.Result}", StatusMessageType.Success);
                    InsertProgressBar.Visibility = Visibility.Collapsed;
                    break;
                case ActionState.get:
                    GetBtn.IsEnabled = true;
                    InsertBtn.IsEnabled = true;
                    InsertProgressBar.Visibility = Visibility.Collapsed;
                    message = $"{e.Result}";
                    CreateWorker(UpdateOffersCollection, ActionState.update);
                    break;
                case ActionState.insert:
                    InsertBtn.IsEnabled = true;
                    InsertProgressBar.Visibility = Visibility.Collapsed;
                    message = $"{e.Result}";
                    CreateWorker(UpdateOffersCollection, ActionState.update);
                    break;
                case ActionState.update:
                    ManagerChanged();
                    StatusBegin($"{e.Result}", StatusMessageType.Success);
                    message = null;
                    IsEnabled = true;
                    UpdateBtn.IsEnabled = true;
                    InsertProgressBar.Visibility = Visibility.Collapsed;

                    if (!string.IsNullOrEmpty(ActiveOffer?.ParentQuoteNumber))
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                            ScrollToGroupAndHighlight(ActiveOffer.ParentQuoteNumber)),
                            DispatcherPriority.ApplicationIdle);
                    }
                    break;
                case ActionState.restartBases:
                    System.Windows.Forms.Application.Restart();
                    Environment.Exit(0);
                    break;
                case ActionState.restartApp:
                    Restart();
                    break;
                case ActionState.exit:
                    Environment.Exit(0);
                    break;
                case ActionState.express:
                    RequestControl?.Show_ExpressOffer();
                    StatusBegin($"{e.Result}", StatusMessageType.Success);
                    message = null;
                    IsEnabled = true;
                    InsertProgressBar.Visibility = Visibility.Collapsed;
                    break;
                default:
                    break;
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
                                    if (_cut is CutControl && _cut.HaveCut) p.Description = "Л";
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
                                if (_cut.Items?.Count > 0) cut.SumProperties(_cut.Items);
                                cut.Parts = cut.PartList();
                                cut.PartsControl = new(cut, cut.Parts);
                                cut.AddPartsControl();
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

            worksheet.Cells[row + 1, 1].Value = "Материал:";
            worksheet.Cells[row + 1, 1, row + 1, 4].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[row + 1, 2].Value = DetailControls[0].TypeDetailControls[0].HasMetal ? "Исполнителя" : "Заказчика";
            worksheet.Cells[row + 1, 2].Style.Font.Bold = true;
            worksheet.Cells[row + 1, 2, row + 1, 3].Merge = true;

            if (!DetailControls[0].TypeDetailControls[0].HasMetal)
            {
                worksheet.Cells[row + 1, 4].Value = "Внимание: остатки давальческого материала забираются вместе с заказом, иначе эти остатки утилизируются!";
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
            worksheet.Cells[row + 5, 2].Value = "H14/h14 +-IT 14/2";

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

            //      50      51      52      53      54      55        56      57        58      59      60          61          62         63         64       65      66      67         68           69     70
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
            for (int col = 0; col < _headersBitrix.Count; col++) statsheet.Cells[1, col + 7].Value = _headersBitrix[col];

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
            statsheet.Cells[7 + temp, 2].Value = "Конструкторские работы:";
            statsheet.Cells[7 + temp, 3].Value = Construct;

            int countTypeDetails = DetailControls.Sum(t => t.TypeDetailControls.Count) + AssemblyWindow.A.Assemblies.Count;
            temp = 7 + temp >= countTypeDetails ? 7 + temp : countTypeDetails;


            // ----- реестр Лазерфлекс (Лист2 - "Реестр") -----
            int beginL = temp += 3;

            List<string> _headersL = new()
            {
                "№ заказа", "Заказчик", "Менеджер", "Толщина и марка металла", "V",
                "Гибка", "V", "Доп работы", "V", "Комментарий", "Дата сдачи", "Лазер (время работ)",
                "Гибка (время работ)", "Количество материала", "Номер КП", "Статус", "Комментарий менеджера", "КК", "ПК"
            };
            for (int col = 0; col < _headersL.Count; col++) statsheet.Cells[temp, col + 1].Value = _headersL[col];

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
                "Дата", "№ п/п", "№ Проекта / Лазера", "Наименование изделия\n/вид работы", "Кол-во",
                "ед изм.", "Подразделение", "Компания", "Мастер", "Менеджер", "Инженер", "Время работ, мин",
                "Дата отгрузки", "Готово к отгрузке", "Отгружено", "Готово \"V\"", "Цвет/цинк",
                "Примечание", "Ход проекта", "ОТГРУЗКИ _ дата и количество", "Стоимость работ"
            };
            for (int col = 0; col < _headersP.Count; col++) statsheet.Cells[temp, col + 1].Value = _headersP[col];

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
                        notesheet.Cells[tempNote, 3].Value = notesheet.Cells[tempNote, 8].Value = $"{Math.Ceiling(_square * 0.14f)} кг";
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
                if (float.TryParse($"{scoresheet.Cells[i + 2, 3].Value}", out float p)) scoresheet.Cells[i + 2, 4].Value = Math.Round(p / 1.2f, 2);
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

        private void CreateOfferDelivery(object sender, RoutedEventArgs e)
        {
            if (!WarningSave()) return;
            SaveProduct();
            SaveOrRemoveOffer(true);
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
                                            Stream? stream = new MemoryStream(bytes);
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

                                //раскладки
                                if (cut.Items?.Count > 0)
                                {
                                    foreach (LaserItem item in cut.Items)
                                    {
                                        byte[]? bytes = item.imageBytes;                //получаем изображение раскладки, если оно есть
                                        if (bytes is not null)
                                        {
                                            Stream? stream = new MemoryStream(bytes);
                                            string uniqueName = $"Image_{Guid.NewGuid().ToString("N")[..8]}"; // короткий уникальный ID
                                            ExcelPicture pic = itemsheet.Drawings.AddPicture(uniqueName, stream);
                                            itemsheet.Cells[namePic + 1, 1].Value = $"s{type.S} {type.MetalDrop.Text}";
                                            itemsheet.Cells[namePic + 1, 1].Style.TextRotation = 90;
                                            itemsheet.Cells[namePic + 1, 1].Style.Font.Bold = true;
                                            itemsheet.Cells[namePic + 1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                                            itemsheet.Cells[namePic + 1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                                            itemsheet.Row(namePic + 1).Height = 400;    //увеличиваем высоту строки, чтобы вмещалось изображение
                                            pic.SetPosition(namePic, 10, 1, 10);        //для изображений индекс начинается от нуля (0), для ячеек - от единицы (1)
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
            //если путь к расчету не сохранен, или файла комплектации по этому пути нет, выходим из метода
            if (offer.Act is null || !File.Exists($"{Path.GetDirectoryName(offer.Act)}\\{Order.Text} {CustomerDrop.Text} - комплектация.xlsx"))
            {
                StatusBegin("Не удалось найти ФАЙЛ комплектации для создания паспорта. Попробуйте пересохранить расчет заново.", StatusMessageType.Error);
                return;
            }

            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            //получаем файл комплектации, созданный ранее
            using var complectbook = new ExcelPackage(new FileInfo($"{Path.GetDirectoryName(offer.Act)}\\{Order.Text} {CustomerDrop.Text} - комплектация.xlsx"));

            //получаем лист комплектации
            ExcelWorksheet? complectsheet = complectbook.Workbook.Worksheets.FirstOrDefault(x => x.Name == "Комплектация");

            if (complectsheet is not null)      //если лист комплектации найден
            {
                //добавляем этот лист в книгу как новый с именем "Паспорт"
                complectsheet = complectbook.Workbook.Worksheets.Add("Паспорт", complectsheet);

                //и удаляем все остальные листы
                while (complectbook.Workbook.Worksheets.Count > 1) complectbook.Workbook.Worksheets.Delete(complectbook.Workbook.Worksheets[0]);

                //добавляем и настраиваем первую строку с заголовком
                complectsheet.InsertRow(1, 1);
                complectsheet.Cells[1, 1, 1, 10].Merge = true;
                complectsheet.Cells[1, 1].Value = "Паспорт качества";
                complectsheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                complectsheet.Row(1).Style.Font.Bold = true;

                //редактируем остальные строки
                complectsheet.Cells[2, 1].Value = "спец , упд ";
                complectsheet.Cells[2, 4].Value = "ООО Лазерфлекс";
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

                //добавляем подпись и печать
                ExcelPicture signature = complectsheet.Drawings.AddPicture("signature2", Application.GetResourceStream(new Uri("Images/signature2.jpg", UriKind.Relative)).Stream);
                signature.SetPosition(Parts.Count + 6, -5, 4, -5);
                ExcelPicture print = complectsheet.Drawings.AddPicture("print2", Application.GetResourceStream(new Uri("Images/print2.png", UriKind.Relative)).Stream);
                print.SetPosition(Parts.Count + 9, 0, 3, 0);

                //выравниваем содержимое и сохраняем книгу в файл
                complectsheet.Cells.AutoFitColumns();
                complectbook.SaveAs($"{Path.GetDirectoryName(offer.Act)}\\{Order.Text} {CustomerDrop.Text} - паспорт.xlsx");
                StatusBegin($"Создан паспорт качества для текущего расчета: {Order.Text} {CustomerDrop.Text}", StatusMessageType.Success);
            }
            else StatusBegin("Не удалось найти ЛИСТ комплектации для создания паспорта. Попробуйте пересохранить расчет заново.", StatusMessageType.Warning);
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


        private void ReportView(object sender, RoutedEventArgs e) { ReportView(); }
        private void ReportView()
        {
            if (ReportOffers == null || ReportOffers.Count == 0) return;

            _currentReport = BuildReport(ReportOffers);
            UpdateReportUi((_currentReport.Plan, _currentReport.BonusOoo, _currentReport.BonusIp, _currentReport.TotalSalary));
        }

        private void UpdateReportUi((decimal Plan, decimal BonusOoo, decimal BonusIp, decimal TotalSalary) result)
        {
            // Форматирование без дробной части и с разделителями (если нужно — можно убрать)
            Plan.Text = result.Plan.ToString("N0");
            Plan.BorderBrush = result.Plan >= BonusOooThreshold ? Brushes.Green : Brushes.Red;

            BonusOOO.Text = result.BonusOoo.ToString("N0");
            BonusIP.Text = result.BonusIp.ToString("N0");
            Salary.Text = result.TotalSalary.ToString("N0");

            StatusBegin($"Отчет перестроен {(IsReportByCreated ? "по дате создания" : "по дате отгрузки")}");
        }

        private ReportResult BuildReport(List<Offer> offers)
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

            int row = 1;
            var _headers = new List<string> { $"дата {(IsReportByCreated ? "создания" : "отгрузки")}", "№счета", "проект", "№заказа", "работа", "металл", "Итого", "%", "бонус", "№КП" };

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
                    worksheet.Cells[row, 1].Value = IsReportByCreated ? item.CreatedDate : item.EndDate;
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
                    worksheet.Cells[row, 1].Value = IsReportByCreated ? item.CreatedDate : item.EndDate;
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
            //worksheet.Cells[row, 2].Value = report.Plan; // ← это cleanProfit + бонусы
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
            //worksheet.Cells[row, 12].Value = Math.Ceiling(report.CleanProfit);

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
            //worksheet.Cells[salaryRow, 2].Value = report.BonusIp;
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightBlue);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "Оклад:";
            worksheet.Cells[salaryRow, 2].Formula = $"=ROUND(MAX(0, 30000 - B{adjustedBaseRow}), 0)";
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightBlue);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "Премия за план:";
            worksheet.Cells[salaryRow, 2].Formula = $"=IF(B{totalRow}>={BonusOooThresholdStr}, 20000, 0)";
            //worksheet.Cells[salaryRow, 2].Value = report.Plan >= BonusOooThreshold ? 20000 : 0; // 20 000, если план выполнен
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightBlue);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "%:";
            worksheet.Cells[salaryRow, 2].Formula = $"=IF(B{totalRow}>={BonusOooThresholdStr}, ROUND((B{totalRow}-{BonusOooThresholdStr})*{BonusOooRateStr}, 0), 0)";
            //worksheet.Cells[salaryRow, 2].Value = report.BonusOoo; // это (Plan - 200000) * 0.15, если Plan >= 200000
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightBlue);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "Аванс:";
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightYellow);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "На карту:";
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.LightYellow);
            salaryRow += 2;

            // Итоговая сумма
            //decimal totalSalary = report.BonusIp + 30000 +
            //                     (report.Plan >= BonusOooThreshold ? 20000 : 0) +
            //                     report.BonusOoo;

            worksheet.Cells[salaryRow, 1].Value = "Итоговая за месяц:";
            worksheet.Cells[salaryRow, 2].Formula = $"=ROUND(SUM(B{row}:B{row + 3}), 0)";
            //worksheet.Cells[salaryRow, 2].Value = totalSalary;
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.GreenYellow);
            salaryRow++;

            worksheet.Cells[salaryRow, 1].Value = "К доплате:";
            worksheet.Cells[salaryRow, 2].Formula = $"=ROUND(SUM(B{row}:B{row + 3})-SUM(B{row + 4}:B{row + 6}), 0)";
            //worksheet.Cells[salaryRow, 2].Value = totalSalary; // или можно сделать ссылку на ячейку
            worksheet.Cells[salaryRow, 2].Style.Font.Color.SetColor(System.Drawing.Color.Red);
            worksheet.Cells[salaryRow, 1, salaryRow, 2].Style.Fill.SetBackground(System.Drawing.Color.GreenYellow);

            var table = worksheet.Cells[row, 1, salaryRow, 2];
            table.Style.Border.Right.Style = table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            table.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            // Скрыть вспомогательные столбцы
            for (int col = 10; col <= 14; col++)
                worksheet.Column(col).Hidden = true;

            worksheet.Cells.AutoFitColumns();
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
        #endregion


        //-------------Заказчики----------------//
        #region
        private void CustomerChanged(object sender, SelectionChangedEventArgs e)        //метод смены заказчика
        {
            if (CustomerDrop.SelectedItem is Customer customer)
            {
                IsAgent = customer.Agent;
                if (HasDelivery != false) HasDelivery = false;
                TargetCustomer = customer;
            }
        }

        private void AddCustomer(object sender, RoutedEventArgs e)                      //метод добавления нового заказчика в базу
        {
            if (CustomerDrop.Text is null || CustomerDrop.Text == "") return;

            if (!IsLocal && !CurrentManager.IsAdmin)
            {
                StatusBegin($"Для добавления нового заказчика в базу обратитесь к администратору.", StatusMessageType.Warning);
                return;
            }

            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);   //подключаемся к базе данных
            bool isAvalaible = db.Database.CanConnect();                                //проверяем, свободна ли база для подключения
            if (isAvalaible)
            {               //если база свободна, проверяем введенное имя заказчика на совпадение с именами в базе
                Customer? _customer = db.Customers.FirstOrDefault(x => x.Name == CustomerDrop.Text);

                if (_customer is null)
                {           //если заказчика с таким именем еще нет в базе, ищем менеджера согласно выбранному
                    Manager? _man = db.Managers.FirstOrDefault(m => m.Id == TargetManager.Id);

                    if (_man is not null)                                               //и добавляем нового заказчика ему в базу
                    {
                        _customer = new()
                        {
                            Name = CustomerDrop.Text,
                            Address = Adress.Text,
                            Agent = IsAgent,
                            DeliveryPrice = Delivery
                        };

                        _man.Customers.Add(_customer);
                        db.SaveChanges();
                        message = $"Заказчик {_customer.Name} добавлен в базу {_man.Name}.";

                        CreateWorker(UpdateCustomersCollection, ActionState.update);
                    }
                }
                else StatusBegin($"Заказчик {_customer.Name} уже существует.", StatusMessageType.Error);
            }
        }

        private void EditCustomer(object sender, RoutedEventArgs e)                     //метод редактирования свойств заказчика
        {
            if (CustomerDrop.SelectedItem is not Customer customer)
            {
                StatusBegin($"Такого заказчика нет в базе.", StatusMessageType.Warning);
                return;
            }

            if ((!IsLocal && !CurrentManager.IsAdmin) || customer.Name == "Частное лицо")
            {
                StatusBegin($"Для изменения данных заказчика в базе обратитесь к администратору.", StatusMessageType.Warning);
                return;
            }
            else
            {
                MessageBoxResult response = MessageBox.Show("Уверены? Данные заказчика будут изменены!", "Редактирование данных заказчика",
                    MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (response == MessageBoxResult.No) return;
            }

            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);   //подключаемся к базе данных
            bool isAvalaible = db.Database.CanConnect();                                //проверяем, свободна ли база для подключения
            if (isAvalaible)
            {               //если база свободна, находим выбранного заказчика в базе
                Customer? _customer = db.Customers.FirstOrDefault(x => x.Id == customer.Id);
                if (_customer is not null)                                              //и изменяем его свойства
                {
                    _customer.Name = CustomerDrop.Text;
                    db.Entry(_customer).Property(o => o.Name).IsModified = true;
                    _customer.Address = Adress.Text;
                    db.Entry(_customer).Property(o => o.Address).IsModified = true;
                    _customer.Agent = IsAgent;
                    db.Entry(_customer).Property(o => o.Agent).IsModified = true;
                    if (int.TryParse(DeliveryPrice.Text, out int delivery)) _customer.DeliveryPrice = delivery;
                    db.Entry(_customer).Property(o => o.DeliveryPrice).IsModified = true;

                    db.SaveChanges();
                    message = $"Данные заказчика {customer.Name} изменены.";

                    CreateWorker(UpdateCustomersCollection, ActionState.update);
                }
            }
        }

        private void DeleteCustomer(object sender, RoutedEventArgs e)                   //метод удаления заказчика из базы
        {
            if (CustomerDrop.SelectedItem is not Customer customer)
            {
                StatusBegin($"Такого заказчика нет в базе.", StatusMessageType.Warning);
                return;
            }

            if ((!IsLocal && !CurrentManager.IsAdmin) || customer.Name == "Частное лицо")
            {
                StatusBegin($"Для удаления заказчика из базы обратитесь к администратору.", StatusMessageType.Warning);
                return;
            }
            else
            {
                MessageBoxResult response = MessageBox.Show("Уверены? Заказчик будет удален из базы!", "Удаление заказчика",
                    MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (response == MessageBoxResult.No) return;
            }

            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);   //подключаемся к базе данных
            bool isAvalaible = db.Database.CanConnect();                                //проверяем, свободна ли база для подключения
            if (isAvalaible)
            {
                Customer? _customer = db.Customers.FirstOrDefault(x => x.Id == customer.Id);
                if (_customer is not null)
                {
                    db.Customers.Remove(_customer);
                    db.SaveChanges();
                    message = $"Заказчик {customer.Name} удален из базы.";

                    CreateWorker(UpdateCustomersCollection, ActionState.update);
                }
            }
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

        //-----------Поиск расчетов по номеру КП, компании, номеру счета или заказа-----------//
        private void Search_Offers(object sender, FunctionEventArgs<string> e)
        {
            _searchQuery = e.Info ?? string.Empty;
            _isProductionMode = false;
            if (InProductionFilterToggle.IsChecked == true)
                InProductionFilterToggle.IsChecked = false;
            ApplyCurrentMode();
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
                CreateWorker(Convert_dwg_to_dxf, ActionState.convert);
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
        private void OpenOffer(object sender, RoutedEventArgs e)
        {
            Offer? offer = null;
            try
            {
                // 1. Проверка входных данных
                if (OffersGrid.SelectedItem is not Offer selectedOffer) return;
                offer = selectedOffer;  // ← Сохраняем для использования в catch

                if (offer.Data == null)
                {
                    StatusBegin("Данные расчета отсутствуют", StatusMessageType.Warning);
                    return;
                }

                // 2. Десериализация данных
                Product? product = OpenOfferData(offer.Data);
                if (product is null)
                {
                    StatusBegin("Не удалось открыть расчет для чтения: данные повреждены", StatusMessageType.Error);
                    return;
                }

                // 3. Создание и показ окна
                ProductWindow clon = new(product)
                {
                    Title = $"{offer.N}  {offer.Company}",
                    Amount = offer.Amount
                };

                clon.Show();

                StatusBegin($"Расчет {offer.N} открыт для чтения", StatusMessageType.Success);
            }
            catch (System.Runtime.Serialization.SerializationException ex)
            {
                // Ошибка формата данных — offer теперь виден!
                string offerId = offer?.N ?? "неизвестно";
                StatusBegin($"Ошибка формата данных расчета #{offerId}", StatusMessageType.Error);
                LogException(ex, $"OpenOffer.Serialization: Offer #{offerId}");
            }
            catch (IOException ex)
            {
                string offerId = offer?.N ?? "неизвестно";
                StatusBegin($"Не удалось прочитать данные расчета #{offerId}", StatusMessageType.Error);
                LogException(ex, $"OpenOffer.IO: Offer #{offerId}");
            }
            catch (InvalidOperationException ex)
            {
                string offerId = offer?.N ?? "неизвестно";
                StatusBegin($"Ошибка интерфейса при открытии расчета #{offerId}", StatusMessageType.Error);
                LogException(ex, $"OpenOffer.InvalidOp: Offer #{offerId}");
            }
            catch (NullReferenceException ex)
            {
                string offerId = offer?.N ?? "неизвестно";
                StatusBegin("Внутренняя ошибка приложения. Обратитесь к разработчику.", StatusMessageType.Error);
                LogException(ex, $"OpenOffer.NullRef: Offer #{offerId}", true);
            }
            catch (Exception ex)
            {
                string offerId = offer?.N ?? "неизвестно";
                StatusBegin($"Непредвиденная ошибка при открытии расчета #{offerId}", StatusMessageType.Error);
                LogException(ex, $"OpenOffer.General: Offer #{offerId}");
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
            if (isCritical)
            {
                // MessageBox.Show("Критическая ошибка. Приложение будет закрыто.", "Ошибка", 
                //     MessageBoxButton.OK, MessageBoxImage.Error);
                // Application.Current.Shutdown(1);
            }
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
        private void LaunchToWork(object sender, RoutedEventArgs e)
        {
            if (OffersGrid.SelectedItem is Offer offer)
            {
                try { MessageBox.Show(LaunchToWork(offer)); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }
        private string LaunchToWork(Offer offer)
        {
            if (!Directory.Exists(connections[8]))
                return $"Не удалось запустить в производство!\n" +
                       $"Нет подключения к папке \"В работу\"";

            if (ActiveOffer is null || ActiveOffer.Data != offer.Data)
                return $"Не удалось запустить в производство!\n" +
                       $"Расчет {offer.N} не загружен (не является активным).";

            string? sourceDir = null; // путь к сохраненному расчету на диске (КП)

            // Проверяем путь к КП или пытаемся обновить его по номеру расчета, если пути нет
            if (File.Exists(offer.Act))
                sourceDir = Path.GetDirectoryName(Path.GetDirectoryName(offer.Act));
            else if (offer.Act is not null && !File.Exists(offer.Act))
            {
                string? dirOffers = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(offer.Act)));

                if (dirOffers is not null)
                {
                    DirectoryInfo dir = new(dirOffers);

                    foreach (DirectoryInfo name in dir.GetDirectories())
                    {
                        if (name.Name.Length > 5 && offer.Act.Contains(name.Name.Remove(5)))
                        {
                            sourceDir = name.FullName;
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(sourceDir))
                return $"Не удалось запустить в производство!\n" +
                       $"Не найден путь к КП. Пересохраните расчет и повторите попытку.";

            string notify = $"Расчет {offer.N} запущен в производство с номером заказа ";

            const int MIN_ORDER = 1000;
            const int MAX_ORDER = 9999;
            const int WINDOW_SIZE = 50; // Максимальное количество "ручных" папок вперёд от последнего номера

            int nextOrder = MIN_ORDER;
            string workingDir = connections[8];
            string logFilePath = Path.Combine(workingDir, "issued_orders.txt");

            // Создаём файл, если он не существует, и делаем его скрытым
            if (!File.Exists(logFilePath))
            {
                using (File.Create(logFilePath)) { }
                File.SetAttributes(logFilePath, FileAttributes.Hidden);
            }

            // Открываем файл с эксклюзивной блокировкой
            using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // --- 1. Читаем последнюю строку из файла (последний выданный номер программой) ---
                stream.Position = 0;
                string? lastLine = null;
                using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        // Игнорируем пустые строки и комментарии (начинающиеся с #)
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
                HashSet<int> candidateNumbers = new() { lastIssued }; // всегда включаем последний из файла

                string orderPattern = @"^\d{4}(?=\D|$)"; // ровно 4 цифры в начале имени папки
                foreach (string dirPath in Directory.GetDirectories(workingDir))
                {
                    string dirName = Path.GetFileName(dirPath);
                    Match match = Regex.Match(dirName, orderPattern);
                    if (match.Success && int.TryParse(match.Value, out int orderNum))
                    {
                        // Учитываем только номера в пределах окна и допустимого диапазона
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
                    throw new InvalidOperationException($"Достигнут максимальный номер заказа ({MAX_ORDER}). Невозможно назначить новый.");
                }

                // --- 4. Записываем новый номер в конец файла ---
                stream.Seek(0, SeekOrigin.End);

                // Добавляем перевод строки, если файл не пуст и не заканчивается им
                if (stream.Length > 0)
                {
                    stream.Seek(-1, SeekOrigin.End);
                    int lastByte = stream.ReadByte();
                    if (lastByte != '\n' && lastByte != '\r')
                    {
                        stream.Seek(0, SeekOrigin.End);
                        stream.WriteByte((byte)'\n');
                    }
                    else
                    {
                        stream.Seek(0, SeekOrigin.End);
                    }
                }

                using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1, leaveOpen: true))
                {
                    writer.WriteLine(nextOrder.ToString());
                    writer.Flush();
                }
            }

            // Присваиваем номер заказа
            offer.Order = nextOrder.ToString();

            // Убеждаемся, что файл остаётся скрытым
            try
            {
                var attrs = File.GetAttributes(logFilePath);
                if (!attrs.HasFlag(FileAttributes.Hidden))
                {
                    File.SetAttributes(logFilePath, attrs | FileAttributes.Hidden);
                }
            }
            catch
            {
                /* Не критично — продолжаем работу */
            }

            // Проверяем наличие трубореза среди работ
            bool hasPipe = false;
            foreach (DetailControl det in DetailControls)
                foreach (TypeDetailControl type in det.TypeDetailControls)
                    foreach (WorkControl work in type.WorkControls)
                        if (work.workType is PipeControl)
                        {
                            hasPipe = true;
                            break;
                        }

            // Создаём папку нового заказа
            string destinationDir = Directory.CreateDirectory(
                Path.Combine(
                    workingDir,
                    $"{nextOrder}{(hasPipe ? " (ТР) " : " ")}{offer.Company}({ShortManager()}){(HasAssembly ? " ЭКСПРЕСС" : "")}"
                )
            ).FullName;

            if (!string.IsNullOrEmpty(sourceDir))
            {
                // Копируем папки с рабочими файлами в папку созданного заказа
                CopyDirectoryToWork(sourceDir, destinationDir, true, sourceDir);

                // Ищем счет в корневой папке расчета
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
                        Match match = Regex.Match(file.Name, pattern, RegexOptions.IgnoreCase);
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

                    // Формируем суффикс в зависимости от типа расчёта
                    string invoiceNumber = "без_счёта";
                    if (!string.IsNullOrEmpty(offer.Invoice))
                    {
                        // Извлекаем только цифры из offer.Invoice (например, "№ 20" → "20")
                        var match = Regex.Match(offer.Invoice, @"\d+");
                        if (match.Success)
                            invoiceNumber = match.Value;
                    }
                    string invoiceSuffix = offer.Agent ? $"нал№{invoiceNumber}" : $"сч№{invoiceNumber}";

                    string orderPart = !string.IsNullOrEmpty(offer.Order) ? offer.Order.Trim() : "без_заказа";

                    // Удаляем недопустимые символы из динамических частей (но не из originalName — он уже существует)
                    string SanitizePart(string input)
                    {
                        var invalid = Path.GetInvalidFileNameChars();
                        return string.Join("_", input.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim('_');
                    }

                    invoiceSuffix = SanitizePart(invoiceSuffix);
                    orderPart = SanitizePart(orderPart);

                    // === Условное выравнивание ===
                    const int ALIGN_WIDTH = 30;
                    string baseNamePart = originalName.Length <= ALIGN_WIDTH
                        ? originalName.PadRight(ALIGN_WIDTH)
                        : originalName;

                    string newKpFolderName = $"{baseNamePart} {invoiceSuffix} {orderPart}".TrimEnd();

                    // Избегаем повторного переименования и конфликтов
                    if (originalName != newKpFolderName)
                    {
                        string parentDir = Path.GetDirectoryName(sourceDir)!;
                        string newKpPath = Path.Combine(parentDir, newKpFolderName);

                        if (!Directory.Exists(newKpPath))
                        {
                            Directory.Move(sourceDir, newKpPath);

                            // Обновляем offer.Act, если он существует
                            if (!string.IsNullOrEmpty(offer.Act))
                            {
                                string relativePath = Path.GetRelativePath(sourceDir, offer.Act);
                                offer.Act = Path.Combine(newKpPath, relativePath);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusBegin($"Ошибка переименования КП: {ex.Message}", StatusMessageType.Error);
            }

            UpdateOffer(OffersGrid); // Сохраняем изменения данных текущего расчета в базе
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

        public float CorrectDestiny(float _destiny)     //метод определения расчетной толщины
        {
            if (_destiny < 0.5f || _destiny > 30) return 0;

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
                "Андросова Светлана" => "са",
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

            Clipboard.SetText($"{sb} - запрос скопирован в буфер");
            return $"{sb} - запрос скопирован в буфер";
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


        //-------------Выход и перезагрузка----------------------//
        #region
        public void Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
        private void Exit(object sender, CancelEventArgs e)
        {
            MessageBoxResult response = MessageBox.Show("Выйти без сохранения?", "Выход из программы",
                                           MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (response == MessageBoxResult.Yes) CreateWorker(InsertDatabase, ActionState.exit);       //запускаем фоновый процесс с выходом из программмы
            e.Cancel = true;
        }

        private void Restart(object sender, RoutedEventArgs e)
        {
            if (CheckVersion(out string _version))
            {
                MessageBoxResult response = MessageBox.Show(
                    $"Metal-Code не требует обновления.\nТекущая версия - {_version}.\nНажмите \"Да\", если требуется обновить принудительно.",
                    "Обновление программы", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (response == MessageBoxResult.No) return;

                CreateWorker(InsertDatabase, ActionState.restartApp);
            }
            else
            {
                MessageBoxResult response = MessageBox.Show(
                    "Для обновления программы, потребуется перезагрузка.\nНажмите \"Нет\", если требуется сохранить текущий расчет.",
                    "Обновление программы", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                if (response == MessageBoxResult.No) return;

                CreateWorker(InsertDatabase, ActionState.restartApp);
            }
        }
        private void Restart()
        {
            Process.Start(Directory.GetCurrentDirectory() + "\\Metal-Code.Updater.exe");
            Environment.Exit(0);
        }

        public bool CheckVersion(out string _version)                   //метод проверки версии приложения
        {
            if (!IsLocal || !File.Exists(connections[9] + "\\version.txt"))
            {
                _version = $"{Version}, без подключения к серверу.";
                return true;
            }

            FileInfo serverVersionFile = new(connections[9] + "\\version.txt");
            FileInfo localVersionFile = new(Directory.GetCurrentDirectory() + "\\version.txt");

            _version = File.ReadAllText(connections[9] + "\\version.txt");

            return serverVersionFile.Exists && localVersionFile.Exists
                && File.ReadAllText(connections[9] + "\\version.txt") == File.ReadAllText(Directory.GetCurrentDirectory() + "\\version.txt");
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