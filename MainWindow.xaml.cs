using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
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
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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

        //временный словарь расчетов для синхронизации с основной базой (0 - новые, 1 - удаленные, 2 - измененные)
        private readonly Dictionary<byte, List<Offer>> TempOffersDict = new() { [0] = new(), [1] = new(), [2] = new() };
        private readonly Dictionary<string, float> TempWorksDict = new();                                       //временный словарь работ

        public readonly List<float> Destinies = new() { .5f, .7f, .8f, 1, 1.2f, 1.5f, 2, 2.5f, 3, 4, 5, 6, 8, 10, 12, 14, 16, 18, 20, 22, 25, 30 };
        public Dictionary<string, Dictionary<float, (float, float, float)>> MetalDict = new();                  //словарь материалов
        public Dictionary<double, float> WideDict = new();                                                      //словарь отверстий
        public Dictionary<Metal, float> MetalRatioDict = new();                                                 //словарь коэффициентов за материал

        //----------Свойства и их основные методы---------//
        #region
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
                HorisontalSplitter.Visibility = DetailsBox.Visibility = PartsBox.Visibility = isRequest ? Visibility.Collapsed : Visibility.Visible;
                
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

        private string version = "2.6.4";
        public string Version
        {
            get => version;
            set => version = value;
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
                if (IsLaser)
                {
                    LaserRadioButton.IsChecked = true;
                    ThemeChange("laserTheme");
                }
                else
                {
                    AppRadioButton.IsChecked = true;
                    ThemeChange("appTheme");
                }
                OnPropertyChanged(nameof(IsLaser));
            }
        }
        private void IsLaserChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                if (radioButton.Name == "AppRadioButton") IsLaser = false;
                else if (radioButton.Name == "LaserRadioButton") IsLaser = true;
            }
            TotalResult();
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

        private float ratio;
        public float Ratio
        {
            get => ratio;
            set
            {
                ratio = value;
                if (ratio <= 0) ratio = 1;
                OnPropertyChanged(nameof(Ratio));
            }
        }
        private void SetRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (float.TryParse(tBox.Text, out float r)) SetRatio(r);
        }
        public void SetRatio(float _ratio)
        {
            Ratio = _ratio;
            TotalResult();
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
                    Comment.Text = Comment.Text.Replace(" Предварительное КП","");
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
            }
        }

        private void SetExpress(object sender, RoutedEventArgs e)
        {
            if (!HasAssembly)
            {
                SetExpress(1);
                DateProduction.Text = "";
            }
            else
            {
                if (AssemblyRatio.Text == "") AssemblyRatio.Text = "2";
                SetExpress(Parser(AssemblyRatio.Text));
                DateProduction.Text = "3";
            }
        }
        private void SetExpress(object sender, TextChangedEventArgs e)
        {
            if (!HasAssembly || sender is not TextBox tBox) return;
            SetExpress(Parser(tBox.Text));
        }
        public void SetExpress(float express)
        {
            if (express <= 0) return;
            foreach (DetailControl det in DetailControls)
                foreach (TypeDetailControl type in det.TypeDetailControls)
                    foreach (WorkControl work in type.WorkControls)
                        work.Ratio = express;
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
            db.Managers.Load();
            Managers = db.Managers.Local.ToObservableCollection();

            db.Offers.Load();
            Offers = db.Offers.Local.ToObservableCollection();

            if (Managers.Count == 0) ShowWindow(new RegistrationWindow());  //если пользователей в базе нет, запускаем процесс регистрации
            //else if (!CheckMachine())                                       //проверяем защитный файл
            //{
            //    MessageBox.Show($"Данная копия программы защищена. Ее невозможно запустить на этом компьютере!");
            //    Environment.Exit(0);
            //}
            else if (!CheckMachineName()) ShowWindow(new LoginWindow());    //проверяем пользователя
            else NewProject();                                              //если все проверки пройдены, создаем новый проект
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
                UserDrop.ItemsSource = Managers;                                  //список ВСЕХ пользователей (для отчетов)

                db.Customers.Load();
                Customers = db.Customers.Local.ToObservableCollection();

                CurrentManager = manager;                                                       //определяем текущего менеджера
                Login.Header = CurrentManager.Name;
                if (ManagerDrop.Items.Contains(manager)) ManagerDrop.SelectedItem = manager;    //устанавливаем менеджера по умолчанию

                UserDrop.SelectedItem = CurrentManager;
                if (CurrentManager.IsEngineer)
                {
                    IsEnabled = false;
                    if (CurrentManager.IsEngineer)
                    {
                        SetManagerWindow setManagerWindow = new();
                        if (setManagerWindow.ShowDialog() == true) ManagerDrop.SelectedItem = setManagerWindow.SelectManager;
                    }
                    else ManagerDrop.SelectedItem = CurrentManager;
                    IsEnabled = true;
                }
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
                UserDrop.SelectedItem = CurrentManager;
                if (CurrentManager.IsEngineer)
                {
                    SetManagerWindow setManagerWindow = new();
                    if (setManagerWindow.ShowDialog() == true) ManagerDrop.SelectedItem = setManagerWindow.SelectManager;
                }
                else ManagerDrop.SelectedItem = CurrentManager;

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

            CurrentOffers = Offers.Where(m => m.ManagerId == TargetManager.Id).TakeLast(23).ToList();
            OffersGrid.ItemsSource = CurrentOffers;

            if (TargetManager == CurrentManager)
            {
                ReportDrop.ItemsSource = Months;
                ReportDrop.SelectedItem = Months[DateTime.Now.Month - 1];
                ReportChanged(Months[DateTime.Now.Month - 1]);
            }
            else ReportOffers.Clear();
        }

        readonly string[] Months = { "январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь" };

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

            ReportOffers = Offers.Where(o => o.Order != null && o.Order != ""
                            && o.CreatedDate >= start && o.CreatedDate < end).ToList();
            ReportGrid.ItemsSource = ReportOffers;
            ReportView();
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
            }
        }
        public void TotalResult()
        {
            Result = 0;

            foreach (DetailControl d in DetailControls) Result += d.Detail.Total;

            Result *= Count;

            Result += Delivery * DeliveryRatio;

            float result = Result * Ratio;

            Bonus = result * ((100 + BonusRatio) / 100) - result;

            Result = result + Bonus;

            if (Result > 0) Parts = PartsSource();
        }

        //-----------Обновление общей стоимости расчета-----------//
        private void UpdateResult(object sender, MouseEventArgs e) { UpdateResult(); }
        private void UpdateResult(object sender, TextChangedEventArgs e) { UpdateResult(); }
        private void UpdateResult(object sender, RoutedEventArgs e) { UpdateResult(); }
        public void UpdateResult()
        {
            Construct = ConstructResult();

            foreach (DetailControl d in DetailControls)
                foreach (TypeDetailControl t in d.TypeDetailControls) t.PriceChanged();

            var works = DetailControls.SelectMany(x => x.TypeDetailControls).SelectMany(x => x.WorkControls);

            var workSorted = works.GroupBy(x => x.workType?.GetType());

            DateProduction.Text = $"{workSorted.Count() * 5}";
        }

        //-------------Создание нового проекта-----------//
        public void NewProject()
        {
            ClearDetails();     // удаляем все детали
            ClearCalculate();   // очищаем расчет
            AddDetail();        // добавляем пустой блок детали
        }
        public void ClearDetails()         // метод удаления всех деталей и очищения текущего расчета
        {
            while (DetailControls.Count > 0) DetailControls[^1].Remove();
            AssemblyWindow.A.Assemblies.Clear();
            isAssemblyOffer = false;
        }
        public void ClearCalculate()
        {
            SetRatio(1);
            SetBonusRatio(0);
            SetCount(1);
            Construct = 0;
            HasDelivery = false;
            CheckConstruct.IsChecked = false;
            IsExpressOffer = false;
            HasAssembly = false;
            Order.Text = CustomerDrop.Text = DateProduction.Text = Adress.Text = Comment.Text = ConstructRatio.Text = AssemblyRatio.Text = "";
            ProductName.Text = $"Изделие";
            ActiveOffer = null;
            Log = null;
        }

        //-----------Добавление контрола детали----------//
        public List<DetailControl> DetailControls = new();
        private void AddDetail(object sender, RoutedEventArgs e)
        {
            AddDetail();
        }
        public void AddDetail()
        {
            DetailControl detail = new(new());

            DetailControls.Add(detail);
            detail.Counter.Text = $"{DetailControls.IndexOf(detail) + 1}";

            DetailsStack.Children.Add(detail);
            DetailsScroll.ScrollToEnd();

            detail.AddTypeDetail();   // при добавлении новой детали добавляем дроп комплектации
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
        #endregion


        //-----------Подключения к базе расчетов-----------------//
        #region
        private void AutoRemoveOffers()             //метод удаления старых расчетов из локальной базы
        {
            if (!IsLocal || DateTime.UtcNow.DayOfWeek is not DayOfWeek.Friday) return;  //удаление старых расчетов выполняем только по пятницам

            using ManagerContext db = new(connections[0]);                  //подключаемся к локальной базе данных
            try
            {
                db.Offers.Load();                                           //загружаем все расчеты

                //получаем коллекцию расчетов, которые созданы более 60 дней назад
                var offers = db.Offers.Where(o => o.CreatedDate < DateTime.UtcNow.AddDays(-60));

                db.Offers.RemoveRange(offers);                              //удаляем полученную коллекцию старых расчетов
                db.SaveChanges();

                StatusBegin($"Общее количество расчетов в базе - {db.Offers.ToList().Count}.");
            }
            catch (DbUpdateConcurrencyException ex) { StatusBegin(ex.Message); }
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

        //метод запуска процесса поиска расчетов по номеру, компании или диапазону дат
        private void Search_Offers(object sender, RoutedEventArgs e) { CreateWorker(Search_Offers, ActionState.search); }
        private string Search_Offers(string? message = null)
        {        
            //сначала получаем все расчеты менеджера
            List<Offer>? offers = Offers.Where(m => m.ManagerId == TargetManager.Id).ToList();

            //затем ищем все КП согласно введенному номеру расчета или названию компании
            if (SearchOffers != "") offers = offers?.Where(o => (o.N is not null && o.N.ToLower().Contains(SearchOffers.ToLower()))
                                            || (o.Company is not null && o.Company.ToLower().Contains(SearchOffers.ToLower()))
                                            || (o.Invoice is not null && o.Invoice.ToLower().Contains(SearchOffers.ToLower()))
                                            || (o.Order is not null && o.Order.ToLower().Contains(SearchOffers.ToLower()))).ToList();

            if (offers?.Count == 0) return $"Расчетов по выбранным параметрам не найдено";
            else CurrentOffers = offers;

            return $"Найдено {offers?.Count} расчетов";
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

        private void ResetDates(object sender, RoutedEventArgs e)           //метод сброса дат на начало текущего месяца до начала дня
        {
            SearchOffers = "";
            DateTime date = DateTime.UtcNow;
            StartDay = new DateTime(date.Year, date.Month, 1);
            EndDay = DateTime.UtcNow.AddDays(1);
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
                        Offer _offer = new(Order.Text, CustomerDrop.Text, Result, GetMetalPrice(), GetServices())
                        {
                            Agent = IsAgent,
                            EndDate = EndDate(),
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
                catch (DbUpdateConcurrencyException ex) { StatusBegin(ex.Message); }
            }
        }

        private void UpdateOffer(object sender, RoutedEventArgs e) { UpdateOffer(OffersGrid); }
        private void UpdateOffer(DataGrid dataGrid)          //метод сохранения изменений в расчете
        {
            //подключаемся к базе данных
            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);
            bool isAvalaible = db.Database.CanConnect();                    //проверяем, свободна ли база для подключения
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
                        StatusBegin($"Данные расчета {offer.N} изменены");

                        //создаем файлы комплектации и списка задач
                        if (ActiveOffer?.Data == _offer.Data) CreateComplect(connections[8], _offer);
                    }
                }
                catch (DbUpdateConcurrencyException ex) { StatusBegin(ex.Message); }
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
            byte[] bytes = Encoding.Unicode.GetBytes(json);         //преобразуем строку в массив байтов

            using MemoryStream stream = new(bytes);
            DataContractJsonSerializer serializer = new(typeof(Product));

            return (Product?)serializer.ReadObject(stream);         //возвращаем десериализованный объект
        }

        private void UpdateDatabases(object sender, RoutedEventArgs e)      //метод обновления локальных баз
        {
            if (!IsLocal) return;               //если запущена основная база, выходим из метода

            MessageBoxResult response = MessageBox.Show(
                "Для обновления локальных баз, потребуется перезагрузка.\nНажмите \"Нет\", если требуется сохранить текущий расчет",
                "Обновление локальных баз", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

            if (response == MessageBoxResult.No) return;

            CreateWorker(InsertDatabase, ActionState.restartBases);         //запускаем фоновый процесс с перезапуском программы
        }
        private void UpdateDatabases()                              //обновление баз заготовок, работ и материалов посредством замены файлов
        {
            if (!IsLocal || !Directory.Exists(connections[9])) return;     //если запущена основная база или нет директории, выходим из метода

            string path = connections[9];    //путь к основным базам данных

            if (File.Exists(path + "\\typedetails.db"))
            {
                FileInfo dbTypeFile = new(path + "\\typedetails.db");
                dbTypeFile.CopyTo(Directory.GetCurrentDirectory() + "\\typedetails.db", true);
            }

            if (File.Exists(path + "\\works.db"))
            {
                FileInfo dbWorkFile = new(path + "\\works.db");
                dbWorkFile.CopyTo(Directory.GetCurrentDirectory() + "\\works.db", true);
            }

            if (File.Exists(path + "\\metals.db"))
            {
                FileInfo dbMetalFile = new(path + "\\metals.db");
                dbMetalFile.CopyTo(Directory.GetCurrentDirectory() + "\\metals.db", true);
            }
        }

        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (((PropertyDescriptor)e.PropertyDescriptor).IsBrowsable == false) e.Cancel = true;   //скрываем свойства с атрибутом [IsBrowsable]

            if (e.PropertyName == "N") e.Column.Header = "№";
            if (e.PropertyName == "Company") e.Column.Header = "Компания";
            if (e.PropertyName == "Amount") e.Column.Header = "Итого";
            if (e.PropertyName == "Material") e.Column.Header = "Материал";
            if (e.PropertyName == "Agent") e.Column.Header = "Нал";
            if (e.PropertyName == "Invoice") e.Column.Header = "Счёт";
            if (e.PropertyName == "CreatedDate") e.Column.Header = "Создан";
            if (e.PropertyName == "Order") e.Column.Header = "Заказ";       
            if (e.PropertyName == "EndDate") e.Column.Header = "Дата отгрузки";
            if (e.PropertyName == "Autor")
            {
                e.Column.Header = "Автор";
                e.Column.IsReadOnly = true;     //запрещаем редактировать автора расчета
            }
        }

        private void DataGrid_AutoGeneratedColumn(object sender, EventArgs e)
        {
            DataGrid dg = (DataGrid)sender;
            ((DataGridTextColumn)dg.Columns[6]).Binding.StringFormat = "d.MM.y";
            ((DataGridTextColumn)dg.Columns[9]).Binding.StringFormat = "d.MM.y";
            dg.FrozenColumnCount = 2;
        }


        //метод загрузки строк в таблицу ВСЕХ расчетов
        private void OffersGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            Button btn = new()
            {
                Height = 20,
                Width = 20,
                BorderThickness = new Thickness(0),
            };

            if (e.Row.Item is Offer offer)
            {
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

            btn.MouseEnter += (sender, k) => { e.Row.IsSelected = true; };
            btn.MouseLeave += (sender, k) => { e.Row.IsSelected = false; };
            e.Row.Header = btn;
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
            Button btn = new()
            {
                Height = 20,
                Width = 20,
                BorderThickness = new Thickness(0),
            };

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
                    StatusBegin($"Подождите, идет конвертация файлов dwg в dxf...");
                    break;
                case ActionState.get:
                    StatusBegin($"Подождите, идет получение расчетов из основной базы...");
                    GetBtn.IsEnabled = false;
                    InsertBtn.IsEnabled = false;
                    UpdateBtn.IsEnabled = false;
                    break;
                case ActionState.insert:
                    StatusBegin($"Подождите, идет отправка расчетов в основную базу...");
                    InsertBtn.IsEnabled = false;
                    UpdateBtn.IsEnabled = false;
                    break;
                case ActionState.search:
                    StatusBegin($"Подождите, идет поиск расчетов по заданным параметрам...");
                    SearchBtn.Content = "Поиск...";
                    SearchBtn.IsEnabled = false;
                    UpdateBtn.IsEnabled = false;
                    break;
                case ActionState.update:
                    StatusBegin($"Подождите, идет обновление базы...");
                    IsEnabled = false;
                    break;
                case ActionState.restartBases:
                    StatusBegin($"Подождите, идет обновление локальных баз с последующей перезагрузкой...");
                    IsEnabled = false;
                    break;
                case ActionState.restartApp:
                    StatusBegin($"Подождите, идет обновление программы с последующей перезагрузкой...");
                    IsEnabled = false;
                    break;
                case ActionState.exit:
                    StatusBegin($"Подождите, идет отправка расчетов в основную базу с последующим выходом из программы...");
                    IsEnabled = false;
                    break;
                case ActionState.express:
                    StatusBegin($"Подождите, идет автоматическая раскладка и создание предварительного расчета...");
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
                    StatusBegin($"{e.Result}");
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
                case ActionState.search:
                    OffersGrid.ItemsSource = CurrentOffers;
                    StatusBegin($"{e.Result}");
                    SearchBtn.IsEnabled = true;
                    SearchBtn.Content = "Поиск";
                    UpdateBtn.IsEnabled = true;
                    InsertProgressBar.Visibility = Visibility.Collapsed;
                    break;
                case ActionState.update:
                    ManagerChanged();
                    StatusBegin($"{e.Result}");
                    message = null;
                    IsEnabled = true;
                    UpdateBtn.IsEnabled = true;
                    InsertProgressBar.Visibility = Visibility.Collapsed;
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
                    StatusBegin($"{e.Result}");
                    message = null;
                    IsEnabled = true;
                    InsertProgressBar.Visibility = Visibility.Collapsed;
                    break;
                default:
                    break;
            }
        }
        #endregion


        //---------Сохранение и загрузка контролов расчета-------//
        #region
        public Product SaveProduct()
        {
            Log = null;         //очищаем логи об анализе раскладок и прочие

            Product product = new()
            {
                Name = ProductName.Text,
                Order = Order.Text,
                Company = CustomerDrop.Text,
                Production = DateProduction.Text,
                Manager = Adress.Text,                  //поле "Manager" сохраняет ссылку на адрес доставки;
                                                        //не менял название поля, чтобы загружались старые сохранения
                Comment = Comment.Text,
                Ratio = Ratio,
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
            };

            product.Details = SaveDetails();
            product.Assemblies = AssemblyWindow.A.Assemblies;
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
                                //если список еще не содержит работу с таким именем, создаем такую запись, иначе просто добавляем стоимость
                                if (!TempWorksDict.ContainsKey(_work.Name)) TempWorksDict[_work.Name] = work.Result;
                                else TempWorksDict[_work.Name] += work.Result;
                            }

                            SaveWork _saveWork = new(_work.Name, work.Ratio, work.TechRatio);
                            _saveWork.ExtraResult = work.ExtraResult;

                            if (work.workType is ICut _cut && _cut.PartDetails?.Count > 0 && _cut.Items?.Count > 0)
                            {
                                foreach (Part p in _cut.PartDetails)
                                {
                                    p.Description = _cut is CutControl cut ? cut.HaveCut || cut.HaveNitro ? "Л" : "Б" : "Т";
                                    if (p.PropsDict.ContainsKey(100) && p.PropsDict[100].Count > 2)
                                        p.Accuracy = _cut is CutControl ?
                                            $"{p.PropsDict[100][0].Trim()}x{p.PropsDict[100][1].Trim()}"
                                            : $"{p.PropsDict[100][2].Trim()} мм";
                                    p.Price = 0;

                                    //пробегаемся по ключам от 50 до 70, которые зарезервированы под конкретные работы
                                    if (p.PropsDict.Count > 0)
                                        for (int key = 50; key < 70; key++) p.PropsDict.Remove(key);

                                    //добавляем конструкторские работы в цену детали, если их необходимо "размазать"
                                    if (CheckConstruct.IsChecked == true)
                                    {
                                        float _send = (float)Math.Round((double)Construct / DetailControls.Count / det.TypeDetailControls.Count / _cut.PartDetails.Sum(p => p.Count), 2);
                                        p.Price += _send;
                                        p.PropsDict[62] = new() { $"{_send}" };
                                    }

                                    //добавляем доставку в цену детали, если ее необходимо "размазать"
                                    if (HasDelivery is null)
                                    {
                                        float _send = (float)Math.Round((double)Delivery * DeliveryRatio / DetailControls.Where(d => d.Detail.IsComplect).Count() / det.TypeDetailControls.Count / _cut.PartDetails.Sum(p => p.Count), 2);
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

                                if (_cut.PartsControl?.Parts.Count > 0)
                                    foreach (PartControl part in _cut.PartsControl.Parts) part.PropertiesChanged?.Invoke(part, true);

                                _saveWork.Parts = _cut.PartDetails;
                                _saveWork.Items = _cut.Items;
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

            ProductName.Text = ProductModel.Product.Name;
            Order.Text = ProductModel.Product.Order;
            CustomerDrop.Text = ProductModel.Product.Company;
            DateProduction.Text = ProductModel.Product.Production;
            Adress.Text = ProductModel.Product.Manager;
            Comment.Text = ProductModel.Product.Comment;
            CheckConstruct.IsChecked = ProductModel.Product.HasConstruct;
            ConstructRatio.Text = ProductModel.Product.ConstructRatio;
            SetRatio(ProductModel.Product.Ratio);
            SetCount(ProductModel.Product.Count);
            SetDeliveryRatio(ProductModel.Product.DeliveryRatio);
            SetDelivery(ProductModel.Product.Delivery);
            IsAgent = ProductModel.Product.IsAgent;
            HasDelivery = ProductModel.Product.HasDelivery;
            HasAssembly = ProductModel.Product.HasAssembly;
            IsExpressOffer = ProductModel.Product.IsExpressOffer;
            SetBonusRatio(ProductModel.Product.BonusRatio);

            IsLoadData = true;
            LoadDetails(ProductModel.Product.Details);
            IsLoadData = false;

            if (ProductModel.Product.Assemblies?.Count > 0)
                AssemblyWindow.A = new() { Assemblies = ProductModel.Product.Assemblies };
            //UpdateResult();
        }
        public void LoadDetails(ObservableCollection<Detail> details)
        {
            ClearDetails();   // очищаем текущий расчет

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
                        WorkControl? work = _type.WorkControls.FirstOrDefault(w => w.WorkDrop.Text == item.NameWork && !item.NameWork.Contains("Доп"));

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

                        if (_work.workType is ICut _cut && item.Items?.Count > 0)
                        {
                            _cut.Items = item.Items;
                            _cut.PartDetails = item.Parts;

                            if (_cut is CutControl cut)
                            {
                                if (_cut.Items?.Count > 0) cut.SumProperties(_cut.Items);
                                cut.Parts = cut.PartList();
                                cut.PartsControl = new(cut, cut.Parts);
                                cut.AddPartsTab();
                            }
                            else if (_cut is PipeControl pipe)
                            {
                                pipe.Parts = pipe.PartList();
                                pipe.PartsControl = new(pipe, pipe.Parts);
                                pipe.AddPartsTab();
                                pipe.SetTotalProperties();
                            }

                            if (_cut.PartsControl?.Parts.Count > 0)
                            {
                                foreach (PartControl part in _cut.PartsControl.Parts)
                                {
                                    if (part.Part.PropsDict.Count > 0)      //ключи от "[50]" зарезервированы под кусочки цены за работы, габариты детали и прочее
                                        foreach (int key in part.Part.PropsDict.Keys) if (key < 50)
                                                part.AddControl((int)Parser(part.Part.PropsDict[key][0]));
                                    part.PropertiesChanged?.Invoke(part, false);
                                }
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

                            Part? part = Parts.FirstOrDefault(p => p.Title == particle.Title);
                            if (part is not null)
                            {
                                particle.Price = part.Price * particle.Count;
                                worksheet.Cells[row, 5].Value = particle.Title;
                                worksheet.Cells[row, 6].Value = particle.Count * assembly.Count;
                                row++;
                            }
                        }
                        assembly.Price = (float)Math.Ceiling(assembly.Particles.Sum(p => p.Price) * Ratio * ((100 + BonusRatio) / 100));
                        assembly.Total = assembly.Price * assembly.Count;

                        worksheet.Cells[rowAssembly, 5, rowAssembly, 8].Style.Font.Bold = true;
                        worksheet.Cells[rowAssembly, 5].Value = assembly.Title;
                        worksheet.Cells[rowAssembly, 6].Value = assembly.Count;
                        worksheet.Cells[rowAssembly, 7].Value = assembly.Price;
                        worksheet.Cells[rowAssembly, 8].Value = assembly.Total;
                    }

                AssemblyWindow.A.CheckAssemblies();

                if (Parts.Count > 0)
                    foreach (Part part in Parts)
                    {
                        Part? _part = AssemblyWindow.A.Assemblies.SelectMany(a => a.Particles).FirstOrDefault(x => x.Title == part.Title);
                        if (_part is null) LooseParts.Add(part);
                    }

                if (LooseParts.Count > 0)
                {
                    for (int i = 0; i < LooseParts.Count; i++)
                    {
                        LooseParts[i].Price = (float)Math.Ceiling(LooseParts[i].Price * Ratio * ((100 + BonusRatio) / 100));
                        LooseParts[i].Total = LooseParts[i].Count * LooseParts[i].Price;
                    }
                    DataTable loosePartsTable = ToDataTable(LooseParts);
                    worksheet.Cells[row, 1].LoadFromDataTable(loosePartsTable, false);
                    row += loosePartsTable.Rows.Count;
                }
            }
            //иначе если есть нарезанные детали, вычисляем их общую стоимость, и оформляем их в КП
            else if (Parts.Count > 0)
            {
                for (int i = 0; i < Parts.Count; i++)
                {
                    Parts[i].Price = (float)Math.Ceiling(Parts[i].Price * Ratio * ((100 + BonusRatio) / 100));
                    Parts[i].Total = Parts[i].Count * Parts[i].Price;
                }
                DataTable partTable = ToDataTable(Parts);
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

            //взависимости от исходящего контрагента добавляем к наименованию детали формулировку услуги или товара
            foreach (var cell in worksheet.Cells[8, 5, row + 8, 5])
                if (cell.Value != null)
                {
                    cell.Value = IsAgent ? $"{cell.Value}".Insert(0, "Изготовление детали ") : $"{cell.Value}".Insert(0, "Деталь ");

                    //и обрезаем наименование
                    foreach (Metal metal in Metals)
                        if (metal.Name != null && $"{cell.Value}".ToLower().Contains(metal.Name))
                        {
                            cell.Value = $"{cell.Value}".Replace(metal.Name, "");
                            break;
                        }
                    if ($"{cell.Value}".ToLower().Contains('s')) cell.Value = $"{cell.Value}"[..$"{cell.Value}".ToLower().LastIndexOf('s')];
                }

            //оформляем заголовки таблицы
            List<string> _headersD = new() { "Материал", "Толщина", "Работы", "Размеры, мм", "Наименование", "Кол-во, шт", "Цена за шт, руб", "Стоимость, руб" };
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
                worksheet.Cells[row, 6].Value = DeliveryRatio;
                worksheet.Cells[row, 7].Value = (float)Math.Ceiling(Delivery * Ratio * ((100 + BonusRatio) / 100));
                worksheet.Cells[row, 8].Value = (float)Math.Ceiling(DeliveryRatio * Delivery * Ratio * ((100 + BonusRatio) / 100));
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

            if (Parts.Count > 0)        //в случае с нарезанными деталями, оформляем расшифровку работ
            {
                worksheet.Cells[row + 6, 1].Value = "Расшифровка работ: ";
                worksheet.Cells[row + 6, 2].Value = "";
                foreach (ExcelRangeBase cell in worksheet.Cells[8, 3, row + 8, 3])
                {
                    if (cell.Value != null && $"{cell.Value}".Contains('Л') && !$"{worksheet.Cells[row + 6, 2].Value}".Contains('Л')) worksheet.Cells[row + 6, 2].Value += "Л - Лазер ";
                    if (cell.Value != null && $"{cell.Value}".Contains('Б') && !$"{worksheet.Cells[row + 6, 2].Value}".Contains('Б')) worksheet.Cells[row + 6, 2].Value += "Б - Без лазера ";
                    if (cell.Value != null && $"{cell.Value}".Contains('Т') && !$"{worksheet.Cells[row + 6, 2].Value}".Contains('Т')) worksheet.Cells[row + 6, 2].Value += "Т - Труборез ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Г ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("Г ")) worksheet.Cells[row + 6, 2].Value += "Г - Гибка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("В ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("В ")) worksheet.Cells[row + 6, 2].Value += "В - Вальцовка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Р ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("Р ")) worksheet.Cells[row + 6, 2].Value += "Р - Резьба ";
                    if (cell.Value != null && $"{cell.Value}".Contains("З ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("З ")) worksheet.Cells[row + 6, 2].Value += "З - Зенковка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Зк ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("Зк ")) worksheet.Cells[row + 6, 2].Value += "Зк - Заклепки ";
                    if (cell.Value != null && $"{cell.Value}".Contains("С ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("С ")) worksheet.Cells[row + 6, 2].Value += "С - Сверловка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Св ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("Св ")) worksheet.Cells[row + 6, 2].Value += "Св - Сварка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("О ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("О ")) worksheet.Cells[row + 6, 2].Value += "О - Окраска ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Ц ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("Ц ")) worksheet.Cells[row + 6, 2].Value += "Ц - Цинкование ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Ф ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("Ф ")) worksheet.Cells[row + 6, 2].Value += "Ф - Фрезеровка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("А ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("А ")) worksheet.Cells[row + 6, 2].Value += "А - Аквабластинг ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Доп ") && !$"{worksheet.Cells[row + 6, 2].Value}".Contains("Доп ")) worksheet.Cells[row + 6, 2].Value += "Доп - Дополнительные работы ";
                }
                worksheet.Cells[row + 6, 2, row + 6, 8].Merge = true;
                if (isAssemblyOffer) worksheet.Cells[row + 6, 2].Value += "  Внимание - в КП присутствуют сборочные единицы!";
            }

            worksheet.Cells[row + 7, 1].Value = "Примечание:";
            worksheet.Cells[row + 7, 2].Value = Comment.Text;
            worksheet.Cells[row + 7, 2].Style.Font.Bold = true;
            worksheet.Cells[row + 7, 2, row + 7, 8].Merge = true;

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
                worksheet.Cells["E4"].Value = ProductName.Text;
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
                int startRow = AssemblyWindow.A.Assemblies.Count + AssemblyWindow.A.Assemblies.Sum(p => p.Particles.Count);

                if (LooseParts.Count > 0)
                {
                    //сначала заполняем ячейки по каждой детали и работе
                    for (int i = 0; i < LooseParts.Count; i++)
                    {
                        byte[]? bytes = LooseParts[i].ImageBytes;           //получаем изображение детали, если оно есть
                        if (bytes is not null)
                        {
                            Stream? stream = new MemoryStream(bytes);
                            ExcelPicture pic = scoresheet.Drawings.AddPicture($"{LooseParts[i].Title}", stream);
                            scoresheet.Row(i + startRow + 2).Height = 32;   //увеличиваем высоту строки, чтобы вмещалось изображение
                            pic.SetSize(32, 32);
                            pic.SetPosition(i + startRow + 1, 5, 3, 5);     //для изображений индекс начинается от нуля (0), для ячеек - от единицы (1)
                        }

                        for (int j = 0; j < 20; j++)        //пробегаемся по ключам от 50 до 70, которые зарезервированы под конкретные работы
                            if (LooseParts[i].PropsDict.ContainsKey(j + 50) && float.TryParse(LooseParts[i].PropsDict[j + 50][0], out float value))
                            {
                                scoresheet.Cells[i + startRow + 2, j + 5].Value = Math.Round(value, 2);

                                //подробности окраски/аква/цинк для каждой детали
                                if (j == 4 || j == 16 || j == 17)
                                {
                                    if (float.TryParse(LooseParts[i].PropsDict[j + 50][1], out float square)) scoresheet.Cells[i + startRow + 2, 23].Value = Math.Round(square, 3);     //площадь покрытия
                                    if (LooseParts[i].PropsDict[j + 50].Count > 2) scoresheet.Cells[i + startRow + 2, 24].Value = LooseParts[i].PropsDict[j + 50][2];                   //цвет
                                }

                                //ставим галочку, если деталь добавлена в работы для Провэлда
                                if (scoresheet.Cells[i + startRow + 2, 25].Value == null && (j >= 3 && j <= 9 || (j >= 15 && j <= 17))) scoresheet.Cells[i + startRow + 2, 25].Value = "П";
                            }

                        if (scoresheet.Cells[i + startRow + 2, 25].Value != null && int.TryParse($"{scoresheet.Cells[i + startRow + 2, 2].Value}", out int _c)) countProweld += _c;
                    }
                }

                rowStat = startRow + LooseParts.Count;
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
                        ExcelPicture pic = scoresheet.Drawings.AddPicture($"{Parts[i].Title}", stream);
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

            scoresheet.Cells[rowStat + 2, 25].Value = countProweld;

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

            if (HasDelivery is true)
            {
                scoresheet.Cells[extable.Rows + 1, 18].Value = scoresheet.Cells[extable.Rows + 1, 2].Value;
                scoresheet.Cells[extable.Rows + 2, 18].Value = scoresheet.Cells[extable.Rows + 1, 3].Value;
            }

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

            int beginBitrix = 2;

            List<string> _headersBitrix = new()
            {
                "№ заказа", "Заказчик", "Менеджер", "Количество материала", "Лазерные работы", "Труборез",
                "Гибочные работы", "Время лазерных работ", "Производство", "Нанесение покрытий",
                "Логистика", "Комментарий", "Дата сдачи", "Время фрезерных работ"
            };
            for (int col = 0; col < _headersBitrix.Count; col++) statsheet.Cells[1, col + 7].Value = _headersBitrix[col];

            int countTypeDetails = DetailControls.Sum(t => t.TypeDetailControls.Count) + 1;

            ExcelRange registryBitrix = statsheet.Cells[1, 7, countTypeDetails, 20];
            registryBitrix.Style.Fill.PatternType = ExcelFillStyle.Solid;
            registryBitrix.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LavenderBlush);
            statsheet.Row(1).Style.Font.Bold = true;

            statsheet.Cells[1, 2].Value = "ПРОВЭЛД";
            statsheet.Cells[1, 2, 1, 3].Merge = true;
            statsheet.Cells[1, 4].Value = "ЛАЗЕРФЛЕКС";
            statsheet.Cells[1, 4, 1, 5].Merge = true;
            statsheet.Cells[2, 2].Value = statsheet.Cells[2, 4].Value = "Работы";
            statsheet.Cells[2, 3].Value = statsheet.Cells[2, 5].Value = "Стоимость";

            int las = 0, pr = 0;        //количество видов работ Лазерфлекс / Провэлд

            if (TempWorksDict.Count > 0) foreach (string key in TempWorksDict.Keys)
                {
                    if (key == "Лазерная резка" || key == "Гибка" || key == "Труборез" || key == "Фрезеровка" || key.Contains("(Л)"))
                    {
                        statsheet.Cells[3 + las, 4].Value = key;
                        statsheet.Cells[3 + las, 5].Value = Math.Round(TempWorksDict[key], 2);
                        las++;
                    }
                    else
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
            statsheet.Cells[5 + temp, 3].Value = Math.Round(GetMetalPrice(), 2);
            statsheet.Cells[6 + temp, 2].Value = "Доставка:";
            statsheet.Cells[6 + temp, 3].Value = Delivery * DeliveryRatio;
            statsheet.Cells[7 + temp, 2].Value = "Конструкторские работы:";
            statsheet.Cells[7 + temp, 3].Value = Construct;

            temp = 7 + temp >= countTypeDetails + 1 ? 7 + temp : countTypeDetails + 1;

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

                    statsheet.Cells[i + temp, 2].Value = statsheet.Cells[i + beginBitrix, 8].Value = CustomerDrop.Text; //"Заказчик"
                    statsheet.Cells[i + temp, 3].Value = statsheet.Cells[i + beginBitrix, 9].Value = ShortManager();    //"Менеджер"

                    if (HasDelivery != false)
                    {
                        statsheet.Cells[i + temp, 8].Value = statsheet.Cells[i + beginBitrix, 17].Value = "Доставка ";  //"Логистика"
                    }

                    if (type.CheckMetal.IsChecked == false) statsheet.Cells[i + temp, 10].Value = statsheet.Cells[i + beginBitrix, 18].Value = "Давальч. ";
                    if (type.Comment != null && type.Comment != "")                                                     //"Комментарий"
                    {
                        statsheet.Cells[i + temp, 10].Value += $"{type.Comment}";
                        statsheet.Cells[i + beginBitrix, 18].Value += $"{type.Comment}";
                    }

                    statsheet.Cells[i + temp, 11].Value = statsheet.Cells[i + beginBitrix, 19].Value = EndDate();       //"Дата сдачи"
                    statsheet.Cells[i + temp, 11].Style.Numberformat.Format = statsheet.Cells[i + beginBitrix, 19].Style.Numberformat.Format = "d MMM";

                    statsheet.Cells[i + temp, 15].Value = Order.Text;       //"Номер КП"

                    if (type.MetalDrop.SelectedItem is Metal met)           //"Количество материала и (его цена за 1 кг)"
                    {
                        double _mass = Math.Ceiling(det.Detail.IsComplect ? type.Mass : type.Mass * type.Count);

                        statsheet.Cells[i + temp, 14].Value = $"{_mass}" +
                            $" ({(type.CheckMetal.IsChecked == true ? (type.ExtraResult > 0 ? Math.Ceiling(type.ExtraResult / _mass) :
                            Math.Ceiling(type.S switch
                            {
                                < 14 => met.MassPrice,
                                < 18 => met.MassPrice * 1.05f,
                                _ => met.MassPrice * 1.15f,
                            })
                            ) : 0)}р)";

                        statsheet.Cells[i + beginBitrix, 10].Value = _mass; //"Количество материала"
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

                            statsheet.Cells[i + temp, 4].Value = statsheet.Cells[i + beginBitrix, 11].Value = description;  //"Лазерные работы"

                            //"Лазер (время работ)"                                 //"Время лазерных работ"
                            statsheet.Cells[i + temp, 12].Value = statsheet.Cells[i + beginBitrix, 14].Value = Math.Ceiling(w.Result * 0.012f / w.Ratio);

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
                        }
                        else if (w.workType is BendControl)
                        {
                            statsheet.Cells[i + temp, 6].Value = "гибка";
                            //"Гибка (время работ)"                                 //"Гибочные работы"
                            statsheet.Cells[i + temp, 13].Value = statsheet.Cells[i + beginBitrix, 13].Value = Math.Ceiling(w.Result * 0.018f / w.Ratio);

                            if (w.Ratio != 1) _bkk += w.Ratio;
                            if (w.TechRatio > 1) _bpk += w.TechRatio;
                            _bc++;
                        }
                        else if (w.workType is PipeControl pipe)
                        {
                            //"Толщина и марка металла"                             //"Труборез"
                            statsheet.Cells[i + temp, 4].Value = statsheet.Cells[i + beginBitrix, 12].Value = $"(ТР) {type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text}";
                            //"Лазер (время работ)"                                 //"Время лазерных работ"
                            statsheet.Cells[i + temp, 12].Value = statsheet.Cells[i + beginBitrix, 14].Value = Math.Ceiling(w.Result * 0.012f / w.Ratio);

                            if (type.CheckMetal.IsChecked is not null)     //если материал давальческий, добавляем его в накладную
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
                            statsheet.Cells[i + temp, 4].Value = statsheet.Cells[i + beginBitrix, 12].Value = $"(ЛП) {type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text}";
                            //"Лазер (время работ)"                                 //"Время лазерных работ"
                            statsheet.Cells[i + temp, 12].Value = statsheet.Cells[i + beginBitrix, 14].Value = Math.Ceiling(w.Result * 0.018f / w.Ratio);
                            
                            notesheet.Cells[tempNote, 2].Value = notesheet.Cells[tempNote, 7].Value = $"{type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text} ({type.L})";
                            notesheet.Cells[tempNote, 3].Value = notesheet.Cells[tempNote, 8].Value = type.Count;
                            tempNote++;
                        }
                        else if (w.workType is ExtraControl _extra)     //для доп работы её наименование добавляем к наименованию работы - особый случай
                        {
                            statsheet.Cells[i + temp, 8].Value += $"{_extra.NameExtra} ";
                            statsheet.Cells[i + beginBitrix, 15].Value += $"{_extra.NameExtra} ";       //"Производство"
                        }
                        else if (w.workType is MillingTotalControl _milling)
                        {
                            statsheet.Cells[i + beginBitrix, 20].Value = _milling.TotalTime;            //"Время фрезерных работ"
                        }
                        else if (w.WorkDrop.SelectedItem is Work work)
                        {
                            statsheet.Cells[i + temp, 8].Value += $"{work.Name} ";                      //"Доп работы"

                            if (w.workType is PaintControl _paint)                                      //"Нанесение покрытий"
                                statsheet.Cells[i + beginBitrix, 16].Value += $"{_paint.Ral} {_paint.TypeDrop.SelectedItem} ";
                            else statsheet.Cells[i + beginBitrix, 15].Value += $"{work.Name} ";         //"Производство"
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
                beginBitrix += det.TypeDetailControls.Count;
            }

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
                    if (key == "Лазерная резка" || key == "Гибка" || key == "Труборез" || key == "Фрезеровка" || key.Contains("(Л)")) continue;

                    if (key.Contains("Цинк"))
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
                statsheet.Cells[temp, 12].Value = Math.Ceiling(sum * 60/ 2000);                             //"Время работ, мин"
                statsheet.Cells[temp, 13].Value = EndDate();                                                //"Дата отгрузки"
                statsheet.Cells[temp, 13].Style.Numberformat.Format = "dd.mm.yy";

                //по умолчанию "Цвет/цинк" - "БП" (без покраски)
                if ($"{statsheet.Cells[temp, 17].Value}" == "") statsheet.Cells[temp, 17].Value = "БП";
                statsheet.Cells[temp, 17].Style.WrapText = true;                                            //"Цвет/цинк"

                //"Примечание"
                if (isAssemblyOffer)
                    statsheet.Cells[temp, 18].Value = LooseParts.Count > 0 ?
                        $"{AssemblyWindow.A.Assemblies.Sum(x => x.Count)} - изделий, " +
                        $"{scoresheet.Cells[AssemblyWindow.A.Assemblies.Count + LooseParts.Count + 2, 25].Value} - штучных деталей"
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

            workbook.SaveAs(Path.GetDirectoryName(_path) + "\\" + "Файл для счета " + Order.Text + "_" + CustomerDrop.Text + " на сумму " + $"{Result}" + ".xlsx");
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
                StatusBegin($"Данные расчета изменены. Но файл комплектации не создан, так как номер заказа введен некорректно.");
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
            List<string> _heads = new() { "№", "Вид", "Название детали", "Маршрут", "Кол-во", "Размеры детали", "Вес, кг", "Материал", "Толщина", "Факт" };
            for (int head = 0; head < _heads.Count; head++) complectsheet.Cells[2, head + 1].Value = _heads[head];

            //параллельно создаем лист с раскладками
            ExcelWorksheet itemsheet = workbook.Workbook.Worksheets.Add("Раскладки");

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
                                            ExcelPicture pic = complectsheet.Drawings.AddPicture($"{cut.PartDetails[i].Title}", stream);
                                            complectsheet.Row(temp + 2).Height = 32;    //увеличиваем высоту строки, чтобы вмещалось изображение
                                            pic.SetSize(32, 32);
                                            pic.SetPosition(temp + 1, 5, 1, 5);         //для изображений индекс начинается от нуля (0), для ячеек - от единицы (1)
                                        }

                                        complectsheet.Cells[temp + 2, 3].Value = cut.PartDetails[i].Title;           //наименование детали

                                        complectsheet.Cells[temp + 2, 4].Value = cut.PartDetails[i].Description;     //маршрут изготовления
                                        complectsheet.Cells[temp + 2, 4].Style.WrapText = true;

                                        complectsheet.Cells[temp + 2, 5].Value = cut.PartDetails[i].Count;           //количество деталей
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
                                        if (cut is PipeControl) complectsheet.Cells[temp + 2, 1, temp + 2, 10].Style.Fill.SetBackground(System.Drawing.Color.Linen);

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
                                            ExcelPicture pic = itemsheet.Drawings.AddPicture($"{namePic}", stream);
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
                    if (det.Detail.Description != null && det.Detail.Description.Contains("Труборез"))
                        complectsheet.Cells[temp + 2, 1, temp + 2, 10].Style.Fill.SetBackground(System.Drawing.Color.Linen);

                    temp++;
                }
            }

            //приводим float-значения типа 0,699999993 к формату 0,7
            foreach (var cell in complectsheet.Cells[3, 9, temp, 9])
                if (cell.Value != null && $"{cell.Value}".Contains(',')) cell.Style.Numberformat.Format = "0.0";


            complectsheet.Cells[temp + 2, 4].Value = "всего деталей:";
            complectsheet.Cells[temp + 2, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            complectsheet.Names.Add("totalCount", complectsheet.Cells[3, 5, temp + 1, 5]);
            complectsheet.Cells[temp + 2, 5].Formula = "=SUM(totalCount)";
            complectsheet.Cells[temp + 2, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            complectsheet.Cells[temp + 2, 6].Value = "общий вес:";
            complectsheet.Cells[temp + 2, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            complectsheet.Cells[temp + 2, 7].Value = Math.Ceiling(_totalMass);
            complectsheet.Cells[temp + 2, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            complectsheet.Names.Add("totalFact", complectsheet.Cells[3, 10, temp + 1, 10]);
            complectsheet.Cells[temp + 2, 10].Formula = "=SUM(totalFact)";
            complectsheet.Cells[temp + 2, 10].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            complectsheet.Row(temp + 2).Style.Font.Bold = true;     //выделяем жирным шрифтом подсчитанные кол-во и вес

            ExcelRange details = complectsheet.Cells[2, 1, temp + 1, 10];    //получаем таблицу деталей для оформления

            if (Parts.Count > 0)        //в случае с нарезанными деталями, оформляем расшифровку работ
            {
                complectsheet.Cells[temp + 3, 1].Value = "Зачистка деталей";
                complectsheet.Cells[temp + 3, 1, temp + 3, 2].Merge = true;
                complectsheet.Cells[temp + 3, 3].Value = "(по необходимости / требованию)";
                complectsheet.Cells[temp + 3, 3].Style.Font.Bold = true;
                complectsheet.Cells[temp + 3, 3, temp + 3, 9].Merge = true;

                complectsheet.Cells[temp + 4, 1].Value = "Расшифровка:";
                complectsheet.Cells[temp + 4, 1, temp + 4, 2].Merge = true;
                complectsheet.Cells[temp + 4, 3].Value = "";
                complectsheet.Cells[temp + 4, 3, temp + 4, 9].Merge = true;

                foreach (ExcelRangeBase cell in complectsheet.Cells[3, 4, temp + 2, 4])
                {
                    if (cell.Value != null && $"{cell.Value}".Contains('Л') && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains('Л')) complectsheet.Cells[temp + 4, 3].Value += "Л - Лазер ";
                    if (cell.Value != null && $"{cell.Value}".Contains('Б') && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains('Б')) complectsheet.Cells[temp + 4, 3].Value += "Б - Без лазера ";
                    if (cell.Value != null && $"{cell.Value}".Contains('Т') && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains('Т')) complectsheet.Cells[temp + 4, 3].Value += "Т - Труборез ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Г ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("Г ")) complectsheet.Cells[temp + 4, 3].Value += "Г - Гибка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("В ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("В ")) complectsheet.Cells[temp + 4, 3].Value += "В - Вальцовка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Р ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("Р ")) complectsheet.Cells[temp + 4, 3].Value += "Р - Резьба ";
                    if (cell.Value != null && $"{cell.Value}".Contains("З ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("З ")) complectsheet.Cells[temp + 4, 3].Value += "З - Зенковка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Зк ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("Зк ")) complectsheet.Cells[temp + 4, 3].Value += "Зк - Заклепки ";
                    if (cell.Value != null && $"{cell.Value}".Contains("С ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("С ")) complectsheet.Cells[temp + 4, 3].Value += "С - Сверловка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Св ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("Св ")) complectsheet.Cells[temp + 4, 3].Value += "Св - Сварка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("О ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("О ")) complectsheet.Cells[temp + 4, 3].Value += "О - Окраска ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Ц ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("Ц ")) complectsheet.Cells[temp + 4, 3].Value += "Ц - Цинкование ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Ф ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("Ф ")) complectsheet.Cells[temp + 4, 3].Value += "Ф - Фрезеровка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("А ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("А ")) complectsheet.Cells[temp + 4, 3].Value += "А - Аквабластинг ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Доп ") && !$"{complectsheet.Cells[temp + 4, 3].Value}".Contains("Доп ")) complectsheet.Cells[temp + 4, 3].Value += "Доп - Дополнительные работы ";
                }
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
            labelsheet.Names.Add("totalCount", complectsheet.Cells[3, 5, temp + 1, 5]);
            labelsheet.Cells[4, 2].Formula = "=SUM(totalCount)";
            labelsheet.Cells[4, 1, 4, 2].Style.Font.Size = 16;
            labelsheet.Cells[4, 1, 4, 2].Style.Font.Bold = true;
            labelsheet.Cells[5, 1].Value = IsLaser ? "тел : (812) 509 - 60 - 11" : "тел:(812) 603 - 45 - 33";
            labelsheet.Cells[5, 1, 5, 2].Merge = true;

            ExcelRange label = labelsheet.Cells[1, 1, 5, 2];                        //получаем этикетку для оформления

            //создаем реестр простых задач для Битрикса
            CreateSimpleRegistry(workbook);

            //обводка границ и авторастягивание столбцов
            details.Style.HorizontalAlignment = label.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            details.Style.VerticalAlignment = label.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            details.Style.Border.Right.Style = label.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            details.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            label.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            complectsheet.Cells.AutoFitColumns();
            if (complectsheet.Column(3).Width < 40) complectsheet.Column(3).Width = 40;

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

                            StatusBegin($"Изменения в базе сохранены. Кроме того созданы файлы комплектации и списка задач в папке {Path.GetDirectoryName(files[0])}");
                            break;
                        }
                        else StatusBegin($"Изменения в базе сохранены. Но файл комплектации не создан, так как в папке заказа нет файлов.");
                    }
                }
            }
            else workbook.SaveAs($"{Path.GetDirectoryName(_path)}\\{Order.Text} {CustomerDrop.Text} - комплектация.xlsx");
        }

        //-ПРОСТЫЕ ЗАДАЧИ
        private void CreateSimpleRegistry(ExcelPackage workbook)
        {
            ExcelWorksheet registrysheet = workbook.Workbook.Worksheets.Add("Реестр");

            int beginBitrix = 2;

            List<string> _headersBitrix = new()
            {
                "№ заказа", "Заказчик", "Менеджер", "Количество материала", "Лазерные работы", "Труборез",
                "Гибочные работы", "Время лазерных работ", "Производство", "Нанесение покрытий",
                "Логистика", "Комментарий", "Дата сдачи", "Время фрезерных работ"
            };
            for (int col = 0; col < _headersBitrix.Count; col++) registrysheet.Cells[1, col + 7].Value = _headersBitrix[col];

            int countTypeDetails = DetailControls.Sum(t => t.TypeDetailControls.Count) + 1;

            ExcelRange registryBitrix = registrysheet.Cells[1, 7, countTypeDetails, 20];
            registryBitrix.Style.Fill.SetBackground(System.Drawing.Color.LavenderBlush);
            registrysheet.Row(1).Style.Font.Bold = true;

            foreach (DetailControl det in DetailControls)
            {
                for (int i = 0; i < det.TypeDetailControls.Count; i++)
                {
                    TypeDetailControl type = det.TypeDetailControls[i];

                    registrysheet.Cells[i + beginBitrix, 8].Value = CustomerDrop.Text;  //"Заказчик"
                    registrysheet.Cells[i + beginBitrix, 9].Value = ShortManager();     //"Менеджер"

                    if (HasDelivery != false)
                        registrysheet.Cells[i + beginBitrix, 17].Value = "Доставка ";   //"Логистика"

                    if (type.CheckMetal.IsChecked == false)
                        registrysheet.Cells[i + beginBitrix, 18].Value = "Давальч. ";   //"Комментарий"

                    if (type.Comment != null && type.Comment != "")
                        registrysheet.Cells[i + beginBitrix, 18].Value += $"{type.Comment}";                                                     //"Комментарий"

                    registrysheet.Cells[i + beginBitrix, 19].Value = EndDate();         //"Дата сдачи"
                    registrysheet.Cells[i + beginBitrix, 19].Style.Numberformat.Format = "d MMM";

                    if (type.MetalDrop.SelectedItem is Metal met)
                    {
                        double _mass = Math.Ceiling(det.Detail.IsComplect ? type.Mass : type.Mass * type.Count);

                        registrysheet.Cells[i + beginBitrix, 10].Value = _mass;         //"Количество материала"
                    }

                    foreach (WorkControl w in type.WorkControls)    //анализируем работы каждой заготовки
                    {
                        if (w.Result == 0) continue;                //пропускаем добавление нулевых работ

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
                            
                            //"Лазерные работы"
                            registrysheet.Cells[i + beginBitrix, 11].Value = description;

                            //"Время лазерных работ"
                            registrysheet.Cells[i + beginBitrix, 14].Value = Math.Ceiling(w.Result * 0.012f / w.Ratio);
                        }
                        else if (w.workType is BendControl)
                        {
                            //"Гибочные работы"
                            registrysheet.Cells[i + beginBitrix, 13].Value = Math.Ceiling(w.Result * 0.018f / w.Ratio);
                        }
                        else if (w.workType is PipeControl pipe)
                        {
                            //"Труборез"
                            registrysheet.Cells[i + beginBitrix, 12].Value = $"(ТР) {type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text}";
                            
                            //"Время лазерных работ"
                            registrysheet.Cells[i + beginBitrix, 14].Value = Math.Ceiling(w.Result * 0.012f / w.Ratio);
                        }
                        else if (w.workType is SawControl _saw)
                        {
                            //"Труборез"
                            registrysheet.Cells[i + beginBitrix, 12].Value = $"(ЛП) {type.TypeDetailDrop.Text} {type.A}x{type.B}x{type.S} {type.MetalDrop.Text}";
                            //"Время лазерных работ"
                            registrysheet.Cells[i + beginBitrix, 14].Value = Math.Ceiling(w.Result * 0.018f / w.Ratio);
                        }
                        else if (w.workType is ExtraControl _extra)     //для доп работы её наименование добавляем к наименованию работы - особый случай
                        {
                            //"Производство"
                            registrysheet.Cells[i + beginBitrix, 15].Value += $"{_extra.NameExtra} ";
                        }
                        else if (w.workType is MillingTotalControl _milling)
                        {
                            //"Время фрезерных работ"
                            registrysheet.Cells[i + beginBitrix, 20].Value = _milling.TotalTime;     
                        }
                        else if (w.WorkDrop.SelectedItem is Work work)
                        {
                            //"Нанесение покрытий"
                            if (w.workType is PaintControl _paint)                                      
                                registrysheet.Cells[i + beginBitrix, 16].Value += $"{_paint.Ral} {_paint.TypeDrop.SelectedItem} ";
                            //"Производство"
                            else registrysheet.Cells[i + beginBitrix, 15].Value += $"{work.Name} ";
                        }
                    }
                }
                beginBitrix += det.TypeDetailControls.Count;
            }

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
                            tasksheet.Cells[temp, 1].Value += $"{description} Гибка";
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
        private void CreatePasport(object sender, RoutedEventArgs e)
        {
            if (ActiveOffer is not null) CreatePasport(ActiveOffer);
            else StatusBegin("Для создания паспорта необходимо загрузить расчет.");
        }
        private void CreatePasport(Offer offer)
        {
            //если путь к расчету не сохранен, или файла комплектации по этому пути нет, выходим из метода
            if (offer.Act is null || !File.Exists($"{Path.GetDirectoryName(offer.Act)}\\{Order.Text} {CustomerDrop.Text} - комплектация.xlsx"))
            {
                StatusBegin("Не удалось найти ФАЙЛ комплектации для создания паспорта. Попробуйте пересохранить расчет заново.");
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
                complectsheet.Cells[1, 1, 1, 9].Merge = true;
                complectsheet.Cells[1, 1].Value = "Паспорт качества";
                complectsheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                complectsheet.Row(1).Style.Font.Bold = true;

                //редактируем остальные строки
                complectsheet.Cells[2, 1].Value = "спец , упд ";
                complectsheet.Cells[2, 4].Value = "ООО Лазерфлекс";
                complectsheet.Row(2).Height = 16;
                complectsheet.Cells[1, 1, 2, 9].Style.Font.Size = 12;
                complectsheet.DeleteRow(Parts.Count + 5);
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
                StatusBegin($"Создан паспорт качества для текущего расчета: {Order.Text} {CustomerDrop.Text}");
            }
            else StatusBegin("Не удалось найти ЛИСТ комплектации для создания паспорта. Попробуйте пересохранить расчет заново.");
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
            else StatusBegin($"Не выбрано ни одного файла");
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
            StatusBegin($"Создана простая комплектация в папке {Path.GetDirectoryName(_paths[0])}");
        }

        //-МАРШРУТ ПРОИЗВОДСТВА
        private void ShowRouteWindow(object sender, RoutedEventArgs e)
        {
            if (ActiveOffer is not null) ShowRouteWindow();
            else StatusBegin("Для создания маршрута производства необходимо загрузить расчет.");
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
            StatusBegin($"Создан маршрут производства для расчета {ActiveOffer?.N}");
        }


        //-------------Отчеты-----------------//
        private void CreateReport(object sender, RoutedEventArgs e)
        {
            if (!CurrentManager.IsAdmin && UserDrop.SelectedItem is Manager user && user != CurrentManager)
            {
                StatusBegin("Нельзя получить отчет другого менеджера или инженера! Выберите себя.");
                return;
            }

            try
            {
                SaveFileDialog saveFileDialog = new() { FileName = $"Отчет за {ReportCalendar.SelectedDates[^1]:MMMM}" };

                if (saveFileDialog.ShowDialog() == true && saveFileDialog.FileName != null)
                {
                    if (ReportCalendar.SelectedDates.Count < 2)
                    {
                        StatusBegin("Выберите даты, по которым следует сформировать отчет");
                        return;
                    }

                    if (sender is Button btn)
                    {
                        bool _report = false;

                        switch (btn.Content)
                        {
                            case "по заказам":
                                _report = ManagerReport(saveFileDialog.FileName);
                                break;
                            case "по расчетам":
                                _report = EngineerReport(saveFileDialog.FileName);
                                break;
                        }
                        if (_report) StatusBegin($"Создан отчёт {btn.Content} за {ReportCalendar.SelectedDates[^1]:MMMM}");
                        else StatusBegin($"Нет расчетов за выбранный период");
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void CreateManagerReport(object sender, RoutedEventArgs e)
        {
            if (!CurrentManager.IsAdmin && UserDrop.SelectedItem is Manager user && user != CurrentManager)
            {
                StatusBegin("Нельзя получить отчет другого менеджера! Выберите себя.");
                return;
            }

            try
            {
                SaveFileDialog saveFileDialog = new() { FileName = $"Отчет за {ReportDrop.SelectedItem}" };

                if (saveFileDialog.ShowDialog() == true && saveFileDialog.FileName != null)
                {
                    bool _report = ManagerReport(saveFileDialog.FileName, ReportOffers);
                    if (_report) StatusBegin($"Создан отчёт менеджера за {ReportDrop.SelectedItem}");
                    else StatusBegin($"Нет расчетов за выбранный период");
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
        
        //метод создания отчета по заказам
        public bool ManagerReport(string path, List<Offer>? reportOffers = null)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet worksheet = workbook.Workbook.Worksheets.Add("Лист1");

            if (reportOffers is null)
            {
                //подключаемся к базе данных
                using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);

                bool isAvalaible = db.Database.CanConnect();                    //проверяем, свободна ли база для подключения
                if (isAvalaible && UserDrop.SelectedItem is Manager man)        //если база свободна, получаем выбранного менеджера
                {
                    try
                    {   //получаем список КП за выбранный период, которые выложены в работу, т.е. оплачены и имеют номер заказа
                        DateTime start = ReportCalendar.SelectedDates[0];
                        DateTime end = ReportCalendar.SelectedDates[^1].AddDays(1);

                        reportOffers = db.Offers.Where(o => o.ManagerId == man.Id && o.Order != null && o.Order != "" &&
                        o.CreatedDate >= start && o.CreatedDate <= end).ToList();

                        if (reportOffers.Count == 0) return false;
                    }
                    catch (DbUpdateConcurrencyException ex) { StatusBegin(ex.Message); }
                }
            }

            List<string> _headers = new() { "дата", "№счета", "проект", "№заказа", "работа", "металл", "Итого", "%", "бонус", "№КП" };
            List<Offer> _agentFalse = new();    //ООО
            List<Offer> _agentTrue = new();     //ИП и ПК

            if (reportOffers?.Count > 0)
            {
                _agentFalse = reportOffers.Where(o => o.Agent == false).ToList();
                _agentTrue = reportOffers.Where(o => o.Agent == true).ToList();
            }

            if (_agentFalse.Count > 0)
            {
                worksheet.Cells[1, 1].Value = "ООО";
                worksheet.Cells[1, 1].Style.Font.Bold = true;

                for (int col = 0; col < _headers.Count; col++) worksheet.Cells[2, col + 1].Value = _headers[col];
                worksheet.Cells[2, 1, 2, _headers.Count].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[2, 1, 2, _headers.Count].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

                for (int f = 0; f < _agentFalse.Count; f++)
                {
                    worksheet.Cells[f + 3, 1].Value = _agentFalse[f].CreatedDate;
                    worksheet.Cells[f + 3, 1].Style.Numberformat.Format = "d MMM";
                    worksheet.Cells[f + 3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    worksheet.Cells[f + 3, 2].Value = _agentFalse[f].Invoice;
                    worksheet.Cells[f + 3, 3].Value = _agentFalse[f].Company;
                    worksheet.Cells[f + 3, 4].Value = _agentFalse[f].Order;
                    
                    float _services = (float)Math.Ceiling(_agentFalse[f].Services);
                    worksheet.Cells[f + 3, 5].Value = _services;

                    float _material = (float)Math.Ceiling(_agentFalse[f].Material);
                    worksheet.Cells[f + 3, 6].Value = _material;

                    float _amount = (float)Math.Ceiling(_agentFalse[f].Amount);
                    worksheet.Cells[f + 3, 7].Value = _amount;

                    float _bonusRatio = Parser(ExtractBonus(_agentFalse[f]));

                    worksheet.Cells[f + 3, 8].Value = _bonusRatio;
                    worksheet.Cells[f + 3, 9].Value = Math.Ceiling(_amount * _bonusRatio / (100 + _bonusRatio));

                    float _servicesBonus = (float)Math.Ceiling(_services * _bonusRatio / (100 + _bonusRatio));
                    worksheet.Cells[f + 3, 11].Value = _servicesBonus;
                    worksheet.Cells[f + 3, 13].Value = _services - _servicesBonus;

                    float _materialBonus = (float)Math.Ceiling(_material * _bonusRatio / (100 + _bonusRatio));
                    worksheet.Cells[f + 3, 12].Value = _materialBonus;
                    worksheet.Cells[f + 3, 14].Value = _material - _materialBonus;

                    worksheet.Cells[f + 3, 10].Value = _agentFalse[f].N;
                }

                worksheet.Names.Add("totalS1", worksheet.Cells[3, 5, 2 + _agentFalse.Count, 5]);
                worksheet.Cells[3 + _agentFalse.Count, 5].Formula = "=SUM(totalS1)";
                worksheet.Cells[3 + _agentFalse.Count, 15].Formula = "=(SUM(totalS1)-SUM(totalS1)/1.3)/1.2";
                if (float.TryParse($"{worksheet.Cells[3 + _agentFalse.Count, 15].Value}", out float total) && total > 0)
                    worksheet.Cells[3 + _agentFalse.Count, 15].Value = Math.Ceiling(total);

                worksheet.Names.Add("totalM1", worksheet.Cells[3, 6, 2 + _agentFalse.Count, 6]);
                worksheet.Cells[3 + _agentFalse.Count, 6].Formula = "=SUM(totalM1)";
                worksheet.Cells[3 + _agentFalse.Count, 16].Formula = "=(SUM(totalM1)-SUM(totalM1)/1.15)/1.2";
                if (float.TryParse($"{worksheet.Cells[3 + _agentFalse.Count, 16].Value}", out float _total) && _total > 0)
                    worksheet.Cells[3 + _agentFalse.Count, 16].Value = Math.Ceiling(_total);

                worksheet.Names.Add("total1", worksheet.Cells[3, 7, 2 + _agentFalse.Count, 7]);
                worksheet.Cells[3 + _agentFalse.Count, 7].Formula = "=SUM(total1)";

                worksheet.Names.Add("bonus1", worksheet.Cells[3, 9, 2 + _agentFalse.Count, 9]);
                worksheet.Cells[3 + _agentFalse.Count, 9].Formula = "=SUM(bonus1)";

                worksheet.Names.Add("services1", worksheet.Cells[3, 13, 2 + _agentFalse.Count, 13]);
                worksheet.Cells[3 + _agentFalse.Count, 13].Formula = "=SUM(services1)";

                worksheet.Names.Add("material1", worksheet.Cells[3, 14, 2 + _agentFalse.Count, 14]);
                worksheet.Cells[3 + _agentFalse.Count, 14].Formula = "=SUM(material1)";

                worksheet.Cells[3 + _agentFalse.Count, 5, 3 + _agentFalse.Count, 10].Style.Font.Bold = true;
                worksheet.Cells[3, 1, 3 + _agentFalse.Count, 10].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                worksheet.Cells[3, 1, 3 + _agentFalse.Count, 10].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                worksheet.Cells[3, 1, 3 + _agentFalse.Count, 10].Style.Border.BorderAround(ExcelBorderStyle.Medium);

                worksheet.Cells[3 + _agentFalse.Count, 15, 3 + _agentFalse.Count, 16].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[3 + _agentFalse.Count, 15, 3 + _agentFalse.Count, 16].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
            }
            else
            {       //если вдруг за выбранный период не оказалось оплаченных счетов на ООО, устанавливаем общую сумму,
                    //сумму за услуги и сумму за материал, в ноль по умолчанию во избежании ошибок
                worksheet.Names.Add("totalS1", worksheet.Cells[1, 1, 1, 1]);
                worksheet.Names.Add("totalM1", worksheet.Cells[1, 1, 1, 1]);
                worksheet.Names.Add("total1", worksheet.Cells[1, 1, 1, 1]);
            }

            if (_agentTrue.Count > 0)
            {
                worksheet.Cells[4 + _agentFalse.Count, 1].Value = "ИП и ПК";
                worksheet.Cells[4 + _agentFalse.Count, 1].Style.Font.Bold = true;

                for (int col = 0; col < _headers.Count; col++) worksheet.Cells[5 + _agentFalse.Count, col + 1].Value = _headers[col];
                worksheet.Cells[5 + _agentFalse.Count, 1, 5 + _agentFalse.Count, _headers.Count].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[5 + _agentFalse.Count, 1, 5 + _agentFalse.Count, _headers.Count].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

                for (int t = 0; t < _agentTrue.Count; t++)
                {
                    worksheet.Cells[t + 6 + _agentFalse.Count, 1].Value = _agentTrue[t].CreatedDate;
                    worksheet.Cells[t + 6 + _agentFalse.Count, 1].Style.Numberformat.Format = "d MMM";
                    worksheet.Cells[t + 6 + _agentFalse.Count, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

                    string? invoice = _agentTrue[t].Invoice;
                    worksheet.Cells[t + 6 + _agentFalse.Count, 2].Value = invoice;

                    worksheet.Cells[t + 6 + _agentFalse.Count, 3].Value = _agentTrue[t].Company;
                    worksheet.Cells[t + 6 + _agentFalse.Count, 4].Value = _agentTrue[t].Order;

                    float _services = (float)Math.Ceiling(_agentTrue[t].Services);
                    worksheet.Cells[t + 6 + _agentFalse.Count, 5].Value = _services;

                    float _material = (float)Math.Ceiling(_agentTrue[t].Material);
                    worksheet.Cells[t + 6 + _agentFalse.Count, 6].Value = _material;

                    float _amount = (float)Math.Ceiling(_agentTrue[t].Amount);
                    worksheet.Cells[t + 6 + _agentFalse.Count, 7].Value = _amount;

                    if (invoice != null && invoice.Contains("без бонуса", StringComparison.OrdinalIgnoreCase))
                        worksheet.Cells[t + 6 + _agentFalse.Count, 17].Value = _amount;

                    float _bonusRatio = Parser(ExtractBonus(_agentTrue[t]));
                    worksheet.Cells[t + 6 + _agentFalse.Count, 8].Value = _bonusRatio;

                    worksheet.Cells[t + 6 + _agentFalse.Count, 9].Value = Math.Ceiling(_amount * _bonusRatio / (100 + _bonusRatio));

                    float _servicesBonus = (float)Math.Ceiling(_services * _bonusRatio / (100 + _bonusRatio));
                    worksheet.Cells[t + 6 + _agentFalse.Count, 11].Value = _servicesBonus;
                    worksheet.Cells[t + 6 + _agentFalse.Count, 13].Value = _services - _servicesBonus;

                    float _materialBonus = (float)Math.Ceiling(_material * _bonusRatio / (100 + _bonusRatio));
                    worksheet.Cells[t + 6 + _agentFalse.Count, 12].Value = _materialBonus;
                    worksheet.Cells[t + 6 + _agentFalse.Count, 14].Value = _material - _materialBonus;

                    worksheet.Cells[t + 6 + _agentFalse.Count, 10].Value = _agentTrue[t].N;
                }

                worksheet.Names.Add("totalS2", worksheet.Cells[6 + _agentFalse.Count, 5, 5 + _agentFalse.Count + _agentTrue.Count, 5]);
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 5].Formula = "=SUM(totalS2)";
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 15].Formula = "=(SUM(totalS2)-SUM(totalS2)/1.3)/1.2";
                if (float.TryParse($"{worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 15].Value}", out float total) && total > 0)
                    worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 15].Value = Math.Ceiling(total);

                worksheet.Names.Add("totalM2", worksheet.Cells[6 + _agentFalse.Count, 6, 5 + _agentFalse.Count + _agentTrue.Count, 6]);
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 6].Formula = "=SUM(totalM2)";
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 16].Formula = "=SUM(totalM2)-SUM(totalM2)/1.15";
                if (float.TryParse($"{worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 16].Value}", out float _total) && _total > 0)
                    worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 16].Value = Math.Ceiling(_total);

                worksheet.Names.Add("total2", worksheet.Cells[6 + _agentFalse.Count, 7, 5 + _agentFalse.Count + _agentTrue.Count, 7]);
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 7].Formula = "=SUM(total2)";
                
                worksheet.Names.Add("bonus2", worksheet.Cells[6 + _agentFalse.Count, 9, 5 + _agentFalse.Count + _agentTrue.Count, 9]);
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 9].Formula = "=SUM(bonus2)";

                worksheet.Names.Add("services2", worksheet.Cells[6 + _agentFalse.Count, 13, 5 + _agentFalse.Count + _agentTrue.Count, 13]);
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 13].Formula = "=SUM(services2)";

                worksheet.Names.Add("material2", worksheet.Cells[6 + _agentFalse.Count, 14, 5 + _agentFalse.Count + _agentTrue.Count, 14]);
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 14].Formula = "=SUM(material2)";

                worksheet.Names.Add("notbonus", worksheet.Cells[6 + _agentFalse.Count, 17, 5 + _agentFalse.Count + _agentTrue.Count, 17]);
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 17].Formula = "=SUM(notbonus)";
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 17].Calculate();

                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 5, 6 + _agentFalse.Count + _agentTrue.Count, 10].Style.Font.Bold = true;
                worksheet.Cells[6 + _agentFalse.Count, 1, 6 + _agentFalse.Count + _agentTrue.Count, 10].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                worksheet.Cells[6 + _agentFalse.Count, 1, 6 + _agentFalse.Count + _agentTrue.Count, 10].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                worksheet.Cells[6 + _agentFalse.Count, 1, 6 + _agentFalse.Count + _agentTrue.Count, 10].Style.Border.BorderAround(ExcelBorderStyle.Medium);

                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 15, 6 + _agentFalse.Count + _agentTrue.Count, 16].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 15, 6 + _agentFalse.Count + _agentTrue.Count, 16].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
            }
            else
            {       //если вдруг за выбранный период не оказалось оплаченных счетов на ИП и ПК, устанавливаем общую сумму,
                    //сумму за услуги и сумму за материал, в ноль по умолчанию во избежании ошибок
                worksheet.Names.Add("totalS2", worksheet.Cells[1, 1, 1, 1]);
                worksheet.Names.Add("totalM2", worksheet.Cells[1, 1, 1, 1]);
                worksheet.Names.Add("total2", worksheet.Cells[1, 1, 1, 1]);
            }

            int plan = 200000;                      //сумма плана для продаж менеджера

            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 4].Value = "ИТОГО:";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 5].Formula = "=SUM(totalS1)+SUM(totalS2)";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 6].Formula = "=SUM(totalM1)+SUM(totalM2)";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 7].Formula = "=SUM(total1)+SUM(total2)";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 9].Formula = "=SUM(bonus1)+SUM(bonus2)";

            //определяем прибыль месяца
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Прибыль месяца:";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 2].Style.Font.Bold = true;

            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 11].Value = "Чист:";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 12].Formula =
                "=(SUM(services1)-SUM(services1)/1.3)/1.2+(SUM(services2)-SUM(services2)/1.3)/1.2+(SUM(material1)-SUM(material1)/1.15)/1.2+SUM(material2)-SUM(material2)/1.15";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 12].Calculate();
            
            if (float.TryParse($"{worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 12].Value}", out float _total1) && _total1 > 0)
            {
                worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 12].Value = Math.Ceiling(_total1);
                worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 13].Value = "Устар:";
                worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 14].Formula = "=(SUM(totalS1)-SUM(totalS1)/1.3)/1.2+(SUM(totalS2)-SUM(totalS2)/1.3)/1.2+(SUM(totalM1)-SUM(totalM1)/1.15)/1.2+SUM(totalM2)-SUM(totalM2)/1.15";
                worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 14].Calculate();
                worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 14].Value = Math.Ceiling(Parser($"{worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 14].Value}"));
                worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 2].Formula = "=SUM(bonus1)+SUM(bonus2)+(SUM(services1)-SUM(services1)/1.3)/1.2+(SUM(services2)-SUM(services2)/1.3)/1.2+(SUM(material1)-SUM(material1)/1.15)/1.2+SUM(material2)-SUM(material2)/1.15";
            }
            else worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 2].Formula = "=(SUM(totalS1)-SUM(totalS1)/1.3)/1.2+(SUM(totalS2)-SUM(totalS2)/1.3)/1.2+(SUM(totalM1)-SUM(totalM1)/1.15)/1.2+SUM(totalM2)-SUM(totalM2)/1.15";

            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 2].Calculate();

            if (float.TryParse($"{worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 2].Value}", out float value))
            {
                worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 2].Value = Math.Ceiling(value);
                worksheet.Cells[13 + _agentFalse.Count + _agentTrue.Count, 2].Value = value >= plan ? 20000 : 0;
                worksheet.Cells[14 + _agentFalse.Count + _agentTrue.Count, 2].Value = value < plan ? "" : (value - plan) * 0.15f;
            }

            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 1, 8 + _agentFalse.Count + _agentTrue.Count, 14].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 1, 8 + _agentFalse.Count + _agentTrue.Count, 14].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightPink);

            worksheet.Cells[11 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Доп бонус за ИП и ПК:";

            if (Parser($"{worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 17].Value}") > 0)
                worksheet.Cells[11 + _agentFalse.Count + _agentTrue.Count, 2].Formula = "=(SUM(total2)-SUM(notbonus))/30";
            else worksheet.Cells[11 + _agentFalse.Count + _agentTrue.Count, 2].Formula = "=SUM(total2)/30";

            if (float.TryParse($"{worksheet.Cells[11 + _agentFalse.Count + _agentTrue.Count, 2].Value}", out float _bonus) && _bonus > 0)
                worksheet.Cells[11 + _agentFalse.Count + _agentTrue.Count, 2].Value = Math.Ceiling(_bonus);

            worksheet.Cells[12 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Оклад:";
            worksheet.Cells[12 + _agentFalse.Count + _agentTrue.Count, 2].Value = 30000;
            worksheet.Cells[13 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Премия за план:";

            worksheet.Cells[14 + _agentFalse.Count + _agentTrue.Count, 1].Value = "%:";
            worksheet.Cells[15 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Аванс:";
            worksheet.Cells[16 + _agentFalse.Count + _agentTrue.Count, 1].Value = "На карту:";

            worksheet.Names.Add("totalPlus", worksheet.Cells[11 + _agentFalse.Count + _agentTrue.Count, 2, 14 + _agentFalse.Count + _agentTrue.Count, 2]);
            worksheet.Names.Add("totalMinus", worksheet.Cells[15 + _agentFalse.Count + _agentTrue.Count, 2, 16 + _agentFalse.Count + _agentTrue.Count, 2]);

            worksheet.Cells[18 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Итоговая за месяц:";
            worksheet.Cells[18 + _agentFalse.Count + _agentTrue.Count, 2].Formula = "=SUM(totalPlus)";
            worksheet.Cells[19 + _agentFalse.Count + _agentTrue.Count, 1].Value = "К доплате:";
            worksheet.Cells[19 + _agentFalse.Count + _agentTrue.Count, 2].Formula = "=SUM(totalPlus)-SUM(totalMinus)";
            worksheet.Cells[19 + _agentFalse.Count + _agentTrue.Count, 2].Style.Font.Color.SetColor(System.Drawing.Color.Red);

            worksheet.Column(10).Hidden = true;
            worksheet.Column(11).Hidden = true;
            worksheet.Column(12).Hidden = true;
            worksheet.Column(13).Hidden = true;
            worksheet.Column(14).Hidden = true;

            ExcelRange tablePlus = worksheet.Cells[11 + _agentFalse.Count + _agentTrue.Count, 1, 14 + _agentFalse.Count + _agentTrue.Count, 2];
            tablePlus.Style.Fill.PatternType = ExcelFillStyle.Solid;
            tablePlus.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);

            ExcelRange tableMinus = worksheet.Cells[15 + _agentFalse.Count + _agentTrue.Count, 1, 16 + _agentFalse.Count + _agentTrue.Count, 2];
            tableMinus.Style.Fill.PatternType = ExcelFillStyle.Solid;
            tableMinus.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);

            ExcelRange bonus = worksheet.Cells[18 + _agentFalse.Count + _agentTrue.Count, 1, 19 + _agentFalse.Count + _agentTrue.Count, 2];
            bonus.Style.Fill.PatternType = ExcelFillStyle.Solid;
            bonus.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.GreenYellow);

            ExcelRange table = worksheet.Cells[11 + _agentFalse.Count + _agentTrue.Count, 1, 19 + _agentFalse.Count + _agentTrue.Count, 2];
            table.Style.Numberformat.Format = "0.00";
            table.Style.Border.Right.Style = table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            table.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            worksheet.Cells.AutoFitColumns();
            workbook.SaveAs(path + ".xlsx");      //сохраняем отчет .xlsx

            return true;
        }

        private void ReportView(object sender, RoutedEventArgs e)
        {
            ReportView();
        }
        private void ReportView()
        {
            List<Offer> _agentFalse = ReportOffers.Where(o => o.Agent == false).ToList();   //ООО
            List<Offer> _agentTrue = ReportOffers.Where(o => o.Agent == true).ToList();     //ИП и ПК

            float totalS1 = 0, totalM1 = 0, totalS2 = 0, totalM2 = 0, bonus1 = 0, bonus2 = 0, notbonus = 0;

            string pattern = "\"BonusRatio\"\\s*:\\s*([\\d.]+)";    //регулярное выражение, для поска бонусного процента

            foreach (Offer offer in _agentFalse)
            {
                if (offer.Data != null)
                {
                    int bonusRatio = 0;
                    Match match = Regex.Match(offer.Data, pattern); //поиск совпадений
                    if (match.Success) bonusRatio = (int)Parser(match.Groups[1].Value);  //извлечение найденного числа
                    totalS1 += offer.Services - (offer.Services * bonusRatio / (100 + bonusRatio));
                    totalM1 += offer.Material - (offer.Material * bonusRatio / (100 + bonusRatio));
                    bonus1 += offer.Amount * bonusRatio / (100 + bonusRatio);
                }
            }

            foreach (Offer offer in _agentTrue)
            {
                if (offer.Invoice != null && offer.Invoice.Contains("без бонуса", StringComparison.OrdinalIgnoreCase))
                    notbonus += offer.Amount;

                if (offer.Data != null)
                {
                    int bonusRatio = 0;
                    Match match = Regex.Match(offer.Data, pattern); //поиск совпадений
                    if (match.Success) bonusRatio = (int)Parser(match.Groups[1].Value);  //извлечение найденного числа
                    totalS2 += offer.Services - (offer.Services * bonusRatio / (100 + bonusRatio));
                    totalM2 += offer.Material - (offer.Material * bonusRatio / (100 + bonusRatio));
                    bonus2 += offer.Amount * bonusRatio / (100 + bonusRatio);
                }
            }

            double bonusOOO = 0, salary = 0, target = 200000;

            double plan = Math.Ceiling((totalS1 - totalS1 / 1.3f) / 1.2f + (totalS2 - totalS2 / 1.3f) / 1.2f + (totalM1 - totalM1 / 1.15f) / 1.2f + (totalM2 - totalM2 / 1.15f) + bonus1 + bonus2);
            Plan.Text = $"{plan}";
            if (plan >= target)
            {
                Plan.BorderBrush = Brushes.Green;
                bonusOOO = Math.Ceiling(((totalS1 - totalS1 / 1.3f) / 1.2f + (totalS2 - totalS2 / 1.3f) / 1.2f + (totalM1 - totalM1 / 1.15f) / 1.2f + (totalM2 - totalM2 / 1.15f) - target + bonus1 + bonus2) * 0.15f);
            }
            else Plan.BorderBrush = Brushes.Red;

            BonusOOO.Text = $"{bonusOOO}";

            double bonusIP = Math.Ceiling((totalS2 + totalM2 + bonus2 - notbonus) / 30);
            BonusIP.Text = $"{bonusIP}";

            salary = Math.Ceiling(bonusOOO > 0 ? bonusOOO + bonusIP + 50000 : bonusIP + 30000);
            Salary.Text = $"{salary}";
        }

        public static string ExtractBonus(Offer offer)
        {
            string bonus = "";

            if (offer.Data != null)
            {
                Product? product = OpenOfferData(offer.Data);
                if (product is not null && product.BonusRatio > 0) return $"{product.BonusRatio}";
            }

            return bonus;
        }

        public bool EngineerReport(string path)         //метод создания отчета по выполненным расчетам
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet worksheet = workbook.Workbook.Worksheets.Add("Лист1");

            List<Offer> _offers = new();

            //подключаемся к базе данных
            using ManagerContext db = new(IsLocal ? connections[0] : connections[1]);
            bool isAvalaible = db.Database.CanConnect();                    //проверяем, свободна ли база для подключения
            if (isAvalaible && UserDrop.SelectedItem is Manager man)        //если база свободна, получаем выбранного сотрудника
            {
                try
                {   //получаем список расчетов за выбранный период, автором которых является выбранный сотрудник
                    _offers = db.Offers.Where(o => o.Autor == man.Name &&
                    o.CreatedDate >= ReportCalendar.SelectedDates[0] && o.CreatedDate <= ReportCalendar.SelectedDates[ReportCalendar.SelectedDates.Count - 1]).ToList();

                    if (_offers.Count == 0) return false;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    StatusBegin(ex.Message);
                }
            }

            (int, int, int, int, int, int) _values;

            if (_offers.Count > 0)
                for (int i = 0; i < _offers.Count; i++)
                {
                    _values = TypesAndWorks(_offers[i]);

                    worksheet.Cells[i + 2, 1].Value = _offers[i].CreatedDate;
                    worksheet.Cells[i + 2, 1].Style.Numberformat.Format = "d MMM";
                    worksheet.Cells[i + 2, 2].Value = _offers[i].N;
                    worksheet.Cells[i + 2, 3].Value = _offers[i].Company;
                    worksheet.Cells[i + 2, 4].Value = Math.Round(_offers[i].Amount, 2);
                    worksheet.Cells[i + 2, 5].Value = _values.Item1;
                    worksheet.Cells[i + 2, 6].Value = _values.Item2;
                    worksheet.Cells[i + 2, 7].Value = _values.Item3;
                    worksheet.Cells[i + 2, 8].Value = _values.Item4;
                    worksheet.Cells[i + 2, 9].Value = _values.Item5;
                    worksheet.Cells[i + 2, 10].Value = _values.Item6;
                }

            List<string> _headers = new() { "дата", "№ расчета", "проект", "сумма, руб", "заготовок", "работ", "позиций", "блоков гибки", "моделей", "сборок" };
            for (int col = 0; col < _headers.Count; col++) worksheet.Cells[1, col + 1].Value = _headers[col];

            worksheet.Cells[1, 1, 1, _headers.Count].Style.Font.Bold = true;
            worksheet.Cells[1, 1, 1, _headers.Count].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[1, 1, 1, _headers.Count].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

            ExcelRange table = worksheet.Cells[1, 1, _offers.Count + 2, _headers.Count];
            table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            table.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            table.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            worksheet.Names.Add("totalT", worksheet.Cells[2, 5, _offers.Count + 1, 5]);
            worksheet.Cells[_offers.Count + 2, 5].Formula = "=SUM(totalT)";
            worksheet.Names.Add("totalW", worksheet.Cells[2, 6, _offers.Count + 1, 6]);
            worksheet.Cells[_offers.Count + 2, 6].Formula = "=SUM(totalW)";
            worksheet.Names.Add("totalP", worksheet.Cells[2, 7, _offers.Count + 1, 7]);
            worksheet.Cells[_offers.Count + 2, 7].Formula = "=SUM(totalP)";
            worksheet.Names.Add("totalB", worksheet.Cells[2, 8, _offers.Count + 1, 8]);
            worksheet.Cells[_offers.Count + 2, 8].Formula = "=SUM(totalB)";
            worksheet.Names.Add("totalM", worksheet.Cells[2, 9, _offers.Count + 1, 9]);
            worksheet.Cells[_offers.Count + 2, 9].Formula = "=SUM(totalM)";
            worksheet.Names.Add("totalA", worksheet.Cells[2, 10, _offers.Count + 1, 10]);
            worksheet.Cells[_offers.Count + 2, 10].Formula = "=SUM(totalA)";

            ExcelRange row = worksheet.Cells[_offers.Count + 2, 1, _offers.Count + 2, _headers.Count];
            row.Style.Font.Bold = true;
            row.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            worksheet.Cells.AutoFitColumns();
            workbook.SaveAs(path + ".xlsx");      //сохраняем отчет .xlsx

            return true;
        }

        private (int, int, int, int, int, int) TypesAndWorks(Offer offer)
        {
            if (offer.Data is null) return (0, 0, 0, 0, 0, 0);

            int types = 0; int works = 0; int count = 0; int bends = 0; int models = 0; int assemblies = 0;

            ProductModel.Product = OpenOfferData(offer.Data);

            if (ProductModel.Product != null && ProductModel.Product.HasAssembly) assemblies++;

            if (ProductModel.Product is not null && ProductModel.Product.Details.Count > 0)
            {
                foreach (Detail det in ProductModel.Product.Details)
                    foreach (SaveTypeDetail type in det.TypeDetails)
                    {
                        types++;
                        foreach (SaveWork work in type.Works)
                        {
                            if (work.NameWork == "Лазерная резка")
                            {
                                count += work.Parts.Count;

                                foreach (Part part in work.Parts)
                                    foreach (List<string> list in part.PropsDict.Values)
                                        if (list.Count > 2 && list[0] == "0" && part.Description != null && part.Description.Contains(" + Г ")) bends++;
                                break;
                            }
                            else if (work.NameWork == "Труборез")
                            {
                                count += work.Parts.Count;
                                break;
                            }
                        }
                        works += type.Works.Count;
                    }
            }

            return (types, works, count, bends, models, assemblies);
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
                StatusBegin($"Для добавления нового заказчика в базу обратитесь к администратору.");
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
                else StatusBegin($"Заказчик {_customer.Name} уже существует.");
            }
        }

        private void EditCustomer(object sender, RoutedEventArgs e)                     //метод редактирования свойств заказчика
        {
            if (CustomerDrop.SelectedItem is not Customer customer)
            {
                StatusBegin($"Такого заказчика нет в базе.");
                return;
            }

            if ((!IsLocal && !CurrentManager.IsAdmin) || customer.Name == "Частное лицо")
            {
                StatusBegin($"Для изменения данных заказчика в базе обратитесь к администратору.");
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
                StatusBegin($"Такого заказчика нет в базе.");
                return;
            }

            if ((!IsLocal && !CurrentManager.IsAdmin) || customer.Name == "Частное лицо")
            {
                StatusBegin($"Для удаления заказчика из базы обратитесь к администратору.");
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


        //-----------Вспомогательные методы----------------------//
        #region

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

        public void StatusBegin(string? notify = null)
        {
            if (notify != null) NotifyText.Text = notify;
            ColorAnimation animation = new()
            {
                From = Colors.White,
                To = Colors.Red,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
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
            if (OffersGrid.SelectedItem is not Offer offer) return;
            if (offer.Data != null)
            {
                Product? product = OpenOfferData(offer.Data);
                if (product is null)
                {
                    StatusBegin("Не удалось открыть расчет для чтения");
                    return;
                }

                ProductWindow clon = new(product) { Title = $"{offer.N}  {offer.Company}" };
                clon.Amount.Text = $"{offer.Amount} руб";
                clon.Show();
                StatusBegin($"Расчет {offer.N} открыт для чтения");
            }
        }

        //------------Создание заявки за клиента-----------------//
        private void CreateRequest(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "All files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames.Length > 0)
            {
                NewProject();
                RequestControl = new(openFileDialog.FileNames.ToList());
                WindowGrid.Children.Insert(0, RequestControl);
                Grid.SetRowSpan(RequestControl, 3);
                IsRequest = true;
            }
            else StatusBegin($"Не выбрано ни одного файла");
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
                if (entity is Line line)
                {
                    DrawLine(line, scale, offsetX, offsetY, geometries, drawingBounds);
                }
                else if (entity is Arc arc)
                {
                    DrawArc(arc, scale, offsetX, offsetY, geometries, drawingBounds);
                }
                else if (entity is Circle circle)
                {
                    DrawCircle(circle, scale, offsetX, offsetY, geometries, drawingBounds);
                }
                else if (entity is Insert insert)
                {
                    RenderBlock(insert.Block, scale, offsetX, offsetY, geometries, drawingBounds);
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
        
        public static void RenderBlock(BlockRecord block, double scale, double offsetX, double offsetY, ObservableCollection<IGeometryDescriptor> geometries, Rect drawingBounds)
        {
            if (block == null || block.Entities == null) return;

            foreach (var reference in block.Entities)
            {
                if (reference is Line line)
                {
                    DrawLine(line, scale, offsetX, offsetY, geometries, drawingBounds);
                }
                else if (reference is Arc arc)
                {
                    DrawArc(arc, scale, offsetX, offsetY, geometries, drawingBounds);
                }
                else if (reference is Circle circle)
                {
                    DrawCircle(circle, scale, offsetX, offsetY, geometries, drawingBounds);
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


        //------------Сортировка файлов по папкам----------------//
        private void CreateTech(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Excel-File (*.xlsx)|*.xlsx|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames.Length > 0)
            {
                Tech tech = new(openFileDialog.FileNames[0]);
                StatusBegin(tech.Run());
            }
            else StatusBegin($"Не выбрано ни одного файла");
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
                StatusBegin($"Нет деталей для добавления в сборку!");
                return;
            }
            AssemblyWindow.A.CurrentParts.Clear();
            foreach (Part part in Parts) AssemblyWindow.A.CurrentParts.Add(part);
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
            if (!Directory.Exists(connections[8])) return $"Не удалось запустить в производство!\n" +
                    $"Нет подключения к папке \"В работу\"";

            if (ActiveOffer is null || ActiveOffer.Data != offer.Data) return $"Не удалось запустить в производство!\n" +
                    $"Расчет {offer.N} не загружен (не является активным).";

            string? sourceDir = null;       //путь к сохраненному расчету на диске (КП)

            //проверяем путь к КП, или пытаемся обновить его по номеру расчета, если пути нет
            if (File.Exists(offer.Act)) sourceDir = Path.GetDirectoryName(Path.GetDirectoryName(offer.Act));
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
            
            if (sourceDir is null || sourceDir == "") return $"Не удалось запустить в производство!\n" +
                    $"Не найден путь к КП. Пересохраните расчет и повторите попытку.";

            string notify = $"Расчет {offer.N} запущен в производство с номером заказа ";

            string[] dirs = Directory.GetDirectories(connections[8]);   //получаем все подкаталоги в папке Y:\\Производство\\Laser rezka\\В работу"
            List<int> orders = new();                                   //список номеров заказов

            //получаем все номера заказов в виде чисел
            string orderPattern = @"^\d{4,5}";
            foreach (string s in dirs)
            {
                Match numOrder = Regex.Match(new DirectoryInfo(s).Name, orderPattern);
                if (numOrder.Success) orders.Add((int)Parser(numOrder.Value));
            }

            if (orders.Count == 0) return $"Не удалось запустить в производство!\n" +
                    $"В директории [{connections[8]}] не найдено папок.";

            int nextOrder = orders.Max() + 1;                   //получаем следующий по порядку номер заказа
            offer.Order = $"{nextOrder}";                       //присваиваем этот номер заказа текущему расчету

            //проверяем наличие трубореза среди работ
            bool hasPipe = false;

            foreach (DetailControl det in DetailControls)
                foreach (TypeDetailControl type in det.TypeDetailControls)
                    foreach (WorkControl work in type.WorkControls)
                        if (work.workType is PipeControl)
                        {
                            hasPipe = true;
                            break;
                        }

            //создаем папку нового заказа
            string destinationDir = $"{Directory.CreateDirectory(connections[8] + "\\"
                + $"{nextOrder}"
                + (hasPipe ? " (ТР) " : " ")
                + $"{offer.Company}({ShortManager()})"
                + (HasAssembly ? " ЭКСПРЕСС" : ""))}";

            if (sourceDir is not null && sourceDir != "")
            {
                //копируем папки с рабочими файлами в папку созданного заказа
                CopyDirectoryToWork(sourceDir, destinationDir, true, sourceDir);

                //ищем счет в корневой папке расчета
                DirectoryInfo dir = new(sourceDir);

                if (dir.GetFiles().Length == 0) offer.Invoice = "нал";
                else
                {
                    foreach (FileInfo file in dir.GetFiles())
                    {
                        string pattern = @"№\s*(\d+)";              //регулярное выражение, для определения № счета
                        Match match = Regex.Match(file.Name, pattern);
                        if (match.Success)
                        {
                            offer.Invoice = $"№ {match.Groups[1].Value}";  //извлечение найденного числа
                            break;
                        }
                    }
                } 
            }

            UpdateOffer(OffersGrid);                  //сохраняем изменения данных текущего расчета в базе

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
        public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
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
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
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

        private void Search_Details(object sender, RoutedEventArgs e)       //метод поиска нарезанной детали
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
            if (SearchDetails.Replace(" ", "") == "" || Parts.Count == 0)
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
                if (foundDetails[0].owner is ICut _cut && _cut.TabItem != null)
                {
                    _cut.TabItem.Focus();
                    _cut.PartsControl?.partsList.ScrollIntoView(foundDetails[0]);

                    if (foundDetails.Count > 1)
                        StatusBegin($"Деталей по запросу \"{SearchDetails}\" найдено {foundDetails.Count}. " +
                            $"Все они окрашены зеленым цветом и могут находиться в других вкладках.");
                }
            }
            else StatusBegin($"Деталей по запросу \"{SearchDetails}\" не найдено.");
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
            string _order = Order.Text;
            string _customer = CustomerDrop.Text;
            string _dateProduction = DateProduction.Text;

            Brush _orderB = Order.BorderBrush;
            Brush _customerB = CustomerDrop.BorderBrush;
            Brush _dateProductionB = DateProduction.BorderBrush;

            if (_order == "" || _customer == "" || _dateProduction == "")
            {
                Order.BorderBrush = CustomerDrop.BorderBrush = DateProduction.BorderBrush = Brushes.OrangeRed;
                Order.BorderThickness = CustomerDrop.BorderThickness = DateProduction.BorderThickness = new Thickness(2);

                MessageBoxResult response = MessageBox.Show(
                    "Некоторые поля не заполнены. Все равно сохранить расчет?",
                    "Сохранение расчета", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                if (response == MessageBoxResult.No)
                {
                    Order.BorderBrush = _orderB;
                    CustomerDrop.BorderBrush = _customerB;
                    DateProduction.BorderBrush = _dateProductionB;
                    Order.BorderThickness = CustomerDrop.BorderThickness = DateProduction.BorderThickness = new Thickness(1);
                    return false;
                }
                else
                {
                    Order.BorderBrush = _orderB;
                    CustomerDrop.BorderBrush = _customerB;
                    DateProduction.BorderBrush = _dateProductionB;
                    Order.BorderThickness = CustomerDrop.BorderThickness = DateProduction.BorderThickness = new Thickness(1);
                }
            }

            if (((!IsAgent && Result < 4000) || (IsAgent && Result < 2500)) && LimitCheck.IsChecked == false)
            {
                Brush _resultB = ResultTB.BorderBrush;
                Brush _limitB = LimitCheck.BorderBrush;

                ResultTB.BorderBrush = LimitCheck.BorderBrush = Brushes.OrangeRed;
                ResultTB.BorderThickness = new Thickness(2);

                ColorAnimation animation = new()
                {
                    From = Colors.White,
                    To = Colors.OrangeRed,
                    Duration = new Duration(TimeSpan.FromSeconds(1)),
                    AutoReverse = true
                };
                LimitCheck.Background = new SolidColorBrush(Colors.White);
                LimitCheck.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);

                MessageBox.Show("Проверьте стоимость КП:\n" +
                    "Стоимость КП с НДС не должна быть меньше 4000 руб!\n" +
                    "Стоимость КП без НДС не должна быть меньше 2500 руб!\n" +
                    "Снимите ограничение, если хотите все равно сохранить расчет.",
                    "Сохранение расчета", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                ResultTB.BorderBrush = _resultB;
                LimitCheck.BorderBrush = _limitB;
                ResultTB.BorderThickness = new Thickness(0);

                return false;
            }

            LimitCheck.IsChecked = false;
            return true;
        }

        public float GetMetalPrice()        //метод получения стоимости материала всего расчета
        {
            float metalprice = 0;
            foreach (DetailControl d in DetailControls)
                foreach (TypeDetailControl t in d.TypeDetailControls) metalprice += t.Result;
            return (float)Math.Round(metalprice, 2);
        }

        public float GetServices() => (float)Math.Round(Result - GetMetalPrice(), 2);

        public string ShortManager()       //метод, возвращающий сокращенное имя менеджера
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
                _ => ""
            };
        }

        private void CreateDelivery(object sender, RoutedEventArgs e) { StatusBegin($"{CreateDelivery()}"); }
        private string CreateDelivery()     //метод построения строки запроса в логистику
        {
            StringBuilder sb = new(EndDate()?.ToString("d MMM"));    //инициализируем строку датой отгрузки в формате "d MMM"

            //если есть номер заказа, добавляем его; иначе добавляем номер КП
            if (ActiveOffer != null && ActiveOffer.Order != null) sb.Append($", №{ActiveOffer.Order}");
            else sb.Append($", №{Order.Text}");

            sb.Append($", {CustomerDrop.Text}({ShortManager()})");   //добавляем заказчика и менеджера в сокращенном виде
            sb.Append($", примерно {GetTotalMass()} кг;");      //добавляем массу всех деталей
            sb.Append($" {Adress.Text}");                       //и, наконец, адрес доставки и контакт

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
            if (float.TryParse(data.Replace(',', '.'), System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.InvariantCulture, out float f)) return f;
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
        //метод анализа срока изготовления на основе старого реестра
        private void AnalyseDateProduction(string path = "Y:\\Производство\\Laser rezka\\В работу")
        {
            if (!File.Exists(path + "\\!Реестр ЗАКРЫВАЙТЕ.xlsx"))
            {
                StatusBegin($"Реестра не существует в папке, или он переименован.");
                return;
            }

            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage(path + "\\!Реестр ЗАКРЫВАЙТЕ.xlsx");
            ExcelWorksheet notesheet = workbook.Workbook.Worksheets[0];

            int endRow = notesheet.Cells.Where(c => c.Start.Column == 2 && !string.IsNullOrEmpty(c.Text)).Last().Start.Row;     //номер последней строки реестра
            int timeLaser = 0; int timeBend = 0;                //счетчики времени работ лазера и гибки

            string[] dirs = Directory.GetDirectories(path);     //получаем все подкаталоги с невырезанными заказами
            foreach (string s in dirs)
            {
                for (int i = endRow; i > 0; i--)
                {
                    if (notesheet.Cells[i, 2].Value != null && s.Contains($"{notesheet.Cells[i, 2].Value}"))
                    {
                        if (notesheet.Cells[i, 13].Value != null && int.TryParse($"{notesheet.Cells[i, 13].Value}", out int l) && notesheet.Cells[i, 19].Value != null && int.TryParse($"{notesheet.Cells[i, 19].Value}", out int rl)) timeLaser += l / rl;
                        else if (notesheet.Cells[i, 13].Value != null && int.TryParse($"{notesheet.Cells[i, 13].Value}", out int vl)) timeLaser += vl;
                        if (notesheet.Cells[i, 14].Value != null && int.TryParse($"{notesheet.Cells[i, 14].Value}", out int b) && notesheet.Cells[i, 19].Value != null && int.TryParse($"{notesheet.Cells[i, 19].Value}", out int rb)) timeBend += b / rb;
                        else if (notesheet.Cells[i, 14].Value != null && int.TryParse($"{notesheet.Cells[i, 14].Value}", out int vb)) timeBend += vb;
                    }
                }
            }

            StatusBegin($"Лазер: {Math.Round((decimal)timeLaser / 60 / 12)} дней; Гибка: {Math.Round((decimal)timeBend / 60 / 12)} дней; Кол-во папок в работе - {dirs.Length}");
        }

        //метод расчета количества рабочих дней
        //private void SetDate(object sender, SelectionChangedEventArgs e)
        //{
        //    //if (datePicker.SelectedDate is not null) DateProduction.Text = $"{GetBusinessDays(DateTime.Now, (DateTime)datePicker.SelectedDate)}";

        //    static int GetBusinessDays(DateTime startD, DateTime endD)
        //    {
        //        int calcBusinessDays =
        //            (int)(1 + ((endD - startD).TotalDays * 5 -
        //            (startD.DayOfWeek - endD.DayOfWeek) * 2) / 7);

        //        if (endD.DayOfWeek == DayOfWeek.Saturday) calcBusinessDays--;
        //        if (startD.DayOfWeek == DayOfWeek.Sunday) calcBusinessDays--;

        //        return calcBusinessDays;
        //    }
        //}
        #endregion
    }
}