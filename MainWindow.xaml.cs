using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;
using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OfficeOpenXml.Style;
using OfficeOpenXml;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Runtime.Serialization.Json;
using System.Text;

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
        readonly string version = "2.1.0";

        public bool isLocal = true;     //запуск локальной версии
        //public bool isLocal = false;    //запуск стандартной версии
        public readonly string[] connections =
        {
            "Data Source=managers.db",
            $"Data Source = Y:\\Конструкторский отдел\\Расчет Заказов ЛФ Сервер\\Metal-Code\\managers.db",
            "Data Source=typedetails.db",
            $"Data Source = Y:\\Конструкторский отдел\\Расчет Заказов ЛФ Сервер\\Metal-Code\\typedetails.db",
            "Data Source=works.db",
            $"Data Source = Y:\\Конструкторский отдел\\Расчет Заказов ЛФ Сервер\\Metal-Code\\works.db",
            "Data Source=metals.db",
            $"Data Source = Y:\\Конструкторский отдел\\Расчет Заказов ЛФ Сервер\\Metal-Code\\metals.db"
        };

        public readonly ProductViewModel ProductModel = new(new DefaultDialogService(), new JsonFileService(), new Product());

        public Manager CurrentManager = new();
        public ObservableCollection<Manager> Managers { get; set; } = new();
        public List<Offer> Offers { get; set; } = new();
        public ObservableCollection<TypeDetail> TypeDetails { get; set; } = new();
        public ObservableCollection<Work> Works { get; set; } = new();
        public ObservableCollection<Metal> Metals { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            M = this;
            Version.Text = version;
            DataContext = ProductModel;
            Loaded += LoadDataBases;
        }

        private void LoadDataBases(object sender, RoutedEventArgs e)  // при загрузке окна
        {
            using TypeDetailContext dbT = new(isLocal ? connections[2] : connections[3]);
            dbT.TypeDetails.Load();
            TypeDetails = dbT.TypeDetails.Local.ToObservableCollection();

            using WorkContext dbW = new(isLocal ? connections[4] : connections[5]);
            dbW.Works.Load();
            Works = dbW.Works.Local.ToObservableCollection();

            using MetalContext dbM = new(isLocal ? connections[6] : connections[7]);
            dbM.Metals.Load();
            Metals = dbM.Metals.Local.ToObservableCollection();
            InitializeDict();

            InsertBtn.IsEnabled = isLocal;      //если запущена локальная версия, делаем кнопку активной для использования
            ViewLoginWindow();
        }

        private void ViewLoginWindow()
        {
            LoginWindow loginWindow = new();
            IsEnabled = false;
            if (loginWindow.ShowDialog() == true)
            {
                if (CurrentManager.IsEngineer)
                {
                    SetManagerWindow setManagerWindow = new();
                    if (setManagerWindow.ShowDialog() == true)
                    {
                        if (ManagerDrop.Items.Contains(setManagerWindow.SelectManager)) ManagerDrop.SelectedItem = setManagerWindow.SelectManager;
                        else ManagerDrop.SelectedIndex = 0;
                    }
                }
                IsEnabled = true;
                AddDetail();
            }
        }

        private readonly Dictionary<string, float> TempWorks = new();

        public readonly List<double> Destinies = new() { .5f, .7f, .8f, 1, 1.2f, 1.5f, 2, 2.5f, 3, 4, 5, 6, 8, 10, 12, 14, 16, 18, 20, 25 };
        public Dictionary<string, Dictionary<double, (float, float, float)>> MetalDict = new();
        public Dictionary<double, float> WideDict = new();
        public Dictionary<Metal, float> MetalRatioDict = new();

        private void InitializeDict()
        {
            foreach (Metal metal in Metals)
            {
                if (metal.Name != null && metal.WayPrice != null && metal.PinholePrice != null && metal.MoldPrice != null)
                {
                    Dictionary<double, (float, float, float)> prices = new();

                    string[] _ways = metal.WayPrice.Split('/');
                    string[] _pinholes = metal.PinholePrice.Split('/');
                    string[] _molds = metal.MoldPrice.Split('/');

                    for (int i = 0; i < _ways.Length; i++) prices[Destinies[i]] = (Parser(_ways[i]), Parser(_pinholes[i]), Parser(_molds[i]));

                    MetalDict[metal.Name] = prices;
                }
            }

            for (int i = 0; i < Destinies.Count; i++)
            {
                if (Destinies[i] <= 3) WideDict[Destinies[i]] = 0;
                else WideDict[Destinies[i]] = (float)Math.Round(0.1f * (i + 2) - 1, 1);
            }

            for (int j = 0; j < Metals.Count; j++)
            {
                if (j < 3) MetalRatioDict[Metals[j]] = 0;
                else if (j < 5) MetalRatioDict[Metals[j]] = .5f;
                else MetalRatioDict[Metals[j]] = 2;
            }
        }

        private void OpenSettings(object sender, RoutedEventArgs e)
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

        private Offer? activeOffer;
        public Offer? ActiveOffer
        {
            get => activeOffer;
            set
            {
                activeOffer = value;
                OnPropertyChanged(nameof(ActiveOffer));
                //OffersGrid.Items.Refresh();
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
                    Boss.Text = $"ООО ЛАЗЕРФЛЕКС  ";
                    Phone.Text = "тел:(812)509-60-11";
                    LaserRadioButton.IsChecked = true;
                    ThemeChange("laserTheme");
                }
                else
                {
                    Boss.Text = $"ООО ПРОВЭЛД  ";
                    Phone.Text = "тел:(812)603-45-33";
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

        private bool hasDelivery;
        public bool HasDelivery
        {
            get => hasDelivery;
            set
            {
                hasDelivery = value;
                if (HasDelivery) DeliveryRadioButton.IsChecked = true;
                else PickupRadioButton.IsChecked = true;
                OnPropertyChanged(nameof(IsLaser));
            }
        }
        private void HasDeliveryChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                if (radioButton.Name == "DeliveryRadioButton") HasDelivery = true;
                else if (radioButton.Name == "PickupRadioButton")
                {
                    SetDelivery(0);
                    SetDeliveryRatio(1);
                    HasDelivery = false;
                } 
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
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int d)) SetDelivery(d);
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
                    if (deliveryRatio <= 0) deliveryRatio = 1;
                    OnPropertyChanged(nameof(DeliveryRatio));
                }
            }
        }
        private void SetDeliveryRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int r)) SetDeliveryRatio(r);
        }
        public void SetDeliveryRatio(int _ratio)
        {
            DeliveryRatio = _ratio;
            TotalResult();
        }

        private float paint;
        public float Paint
        {
            get => paint;
            set
            {
                if (value != paint)
                {
                    paint = value;
                    OnPropertyChanged(nameof(Paint));
                }
            }
        }
        public float PaintResult()
        {
            float result = 0;
            if (CheckPaint.IsChecked != false)
            {
                foreach (DetailControl d in DetailControls) if (!d.Detail.IsComplect)
                        foreach (TypeDetailControl t in d.TypeDetailControls)
                            result += 87 * t.L * t.Count / 1000;     // простая формула окраски через пог м типовой детали

                // проверяем наличие работы и добавляем её минималку к расчету
                foreach (Work w in Works) if (w.Name == "Окраска")
                    {
                        result += w.Price;
                        break;
                    }
            }
            if (float.TryParse(PaintRatio.Text, out float p)) result *= p;
            return result;
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

            Result = (float)Math.Round(Result * Ratio, 2);

            if (Result > 0) ViewDetailsGrid();
        }

        private void ViewDetailsGrid()
        {
            Parts = PartsSource();

            if (IsLaser) DetailsGrid.ItemsSource = Parts;
            else DetailsGrid.ItemsSource = DetailsSource();

            DetailsGrid.Columns[0].Header = "Материал";
            DetailsGrid.Columns[1].Header = "Толщина";
            DetailsGrid.Columns[2].Header = "Работы";
            DetailsGrid.Columns[3].Header = "Точность";
            DetailsGrid.Columns[4].Header = "Наименование";
            DetailsGrid.Columns[5].Header = "Кол-во, шт";
            DetailsGrid.Columns[6].Header = "Цена за шт, руб";
            DetailsGrid.Columns[7].Header = "Стоимость, руб";
            DetailsGrid.FrozenColumnCount = 1;
        }

        private void ManagerChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ManagerDrop.SelectedItem is Manager man) ViewOffersGrid(man);
        }
        private void RefreshOffers(object sender, RoutedEventArgs e)
        {
            if (ManagerDrop.SelectedItem is Manager man) ViewOffersGrid(man);
        }
        private void ViewOffersGrid(Manager man, bool allOffers = false, int count = 13)
        {
                //подключаемся к базе данных
            using ManagerContext db = new(isLocal ? connections[0] : connections[1]);
            bool isAvalaible = db.Database.CanConnect();        //проверяем, свободна ли база для подключения
            if (isAvalaible)
            {
                    //при смене менеджера загружаем ТОЛЬКО ЕГО и ЕГО коллекцию расчетов
                Manager? _man = db.Managers.Where(m => m.Id == man.Id).Include(c => c.Offers).FirstOrDefault();
                if (_man != null)
                {
                    if (!allOffers) Offers = _man.Offers.TakeLast(count).ToList();  //показываем последние "numberOffer" расчетов
                    else Offers = _man.Offers.ToList();                             //если пользователь хочет увидеть все расчеты

                    OffersGrid.ItemsSource = Offers;

                    OffersGrid.Columns[0].Header = "N";
                    OffersGrid.Columns[1].Header = "Компания";
                    OffersGrid.Columns[2].Header = "Итого, руб.";
                    OffersGrid.Columns[3].Header = "Материал, руб.";
                    OffersGrid.Columns[4].Header = "Счёт";
                    OffersGrid.Columns[5].Header = "Заказ";
                    OffersGrid.Columns[6].Header = "УПД";
                    OffersGrid.Columns[7].Header = "Автор";
                    OffersGrid.Columns[8].Header = "Дата создания";
                    (OffersGrid.Columns[8] as DataGridTextColumn).Binding.StringFormat = "d.MM.y";
                    OffersGrid.Columns[9].Header = "Дата отгрузки";
                    (OffersGrid.Columns[9] as DataGridTextColumn).Binding.StringFormat = "d.MM.y";

                    OffersGrid.FrozenColumnCount = 2;

                    //если список КП принадлежит текущему менеджеру, разрешаем ему удалять эти КП
                    //foreach (MenuItem item in OffersGrid.ContextMenu.Items)
                    //    if (item.Name == "DeleteOffer") item.IsEnabled = CurrentManager == man;
                }
            }
        }
        private void ViewAllOffers(object sender, RoutedEventArgs e)        //метод, в котором мы показываем все расчеты выбранного менеджера
        {
            if (sender is RadioButton rBtn && rBtn.IsChecked == true && ManagerDrop.SelectedItem is Manager man) ViewOffersGrid(man, true);
        }

        private void ViewOffers(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) ViewOfferWithDataBase(tBox.Text);
        }
        private void ViewOfferWithDataBase(string? numberOffer) //метод, в котором мы добавляем расчет из основной базы в локальную
                                                                //по введенному пользователем номеру расчета
        {
            if (!isLocal) return;                                //если запущена основная база, выходим из метода

            //подключаемся к основной базе данных
            using ManagerContext db = new(connections[1]);
            bool isAvalaible = db.Database.CanConnect();        //проверяем, свободна ли база для подключения
            if (isAvalaible && ManagerDrop.SelectedItem is Manager man)
            {
                try
                {
                    //ищем менеджера в основной базе по имени соответствующего локальному, при этом загружаем его расчеты
                    Manager? _man = db.Managers.Where(m => m.Name == man.Name).Include(c => c.Offers).FirstOrDefault();

                    List<Offer>? offers = _man?.Offers.Where(o => o.N == numberOffer).ToList();    //ищем все КП согласно введенному номеру

                    if (offers == null || offers.Count == 0)
                    {
                        StatusBegin($"Расчетов с таким номером у выбранного менеджера не найдено");
                        return;
                    }
                    else
                    {
                        //подключаемся к локальной базе данных
                        using ManagerContext dbLocal = new(connections[0]);

                        //ищем менеджера в локальной базе по индентификатору выбранного менеджера
                        Manager? _manLocal = dbLocal.Managers.Where(m => m.Id == man.Id).Include(c => c.Offers).FirstOrDefault();

                        if (_manLocal != null)
                        {
                            foreach (Offer offer in offers)
                            {
                                //проверяем наличие идентичного КП в локальной базе, если такое уже есть, пропускаем копирование
                                Offer? tempOffer = _manLocal?.Offers.FirstOrDefault(o => o.Data == offer.Data);
                                if (tempOffer != null)
                                {
                                    StatusBegin($"Один или несколько расчетов с таким номером уже есть");
                                    continue;
                                }
                                else
                                {
                                    //копируем найденное КП в новое с целью автоматического присваивания Id при вставке в локальную базу
                                    Offer _offer = new(offer.N, offer.Company, offer.Amount, offer.Material, offer.Services)
                                    {
                                        Invoice = offer.Invoice,
                                        Order = offer.Order,
                                        Act = offer.Act,
                                        CreatedDate = offer.CreatedDate,
                                        EndDate = offer.EndDate,
                                        Autor = offer.Autor,
                                        Manager = _manLocal,        //указываем локального менеджера  
                                        Data = offer.Data
                                    };
                                    dbLocal.Offers.Add(_offer);     //переносим расчет в локальную базу
                                }
                            }

                            dbLocal.SaveChanges();
                            StatusBegin($"Локальная база обновлена");

                            ViewOffersGrid(_manLocal);      //обновляем представление расчетов выбранного менеджера
                        }
                    }
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    StatusBegin(ex.Message);
                }
            }
        }

        private void InsertDatabase(object sender, RoutedEventArgs e)
        {
            InsertDatabase();
        }
        private void InsertDatabase()                           //метод отправки новых созданных расчетов в основную базу
        {
            if (!isLocal) return;                               //если запущена основная база, выходим из метода

            //подключаемся к основной базе данных
            using ManagerContext db = new(connections[1]);
            bool isAvalaible = db.Database.CanConnect();        //проверяем, свободна ли база для подключения
            if (isAvalaible)
            {
                try
                {
                    //подключаемся к локальной базе данных
                    using ManagerContext dbLocal = new(connections[0]);

                    foreach (Manager manLocal in Managers)
                    {
                        //ищем менеджера в основной базе по имени соответствующего локальному, при этом загружаем его расчеты
                        Manager? _man = db.Managers.Where(m => m.Name == manLocal.Name).Include(c => c.Offers).FirstOrDefault();

                        //ищем менеджера в локальной базе по имени соответствующего локальному, при этом загружаем его расчеты
                        Manager? _manLocal = dbLocal.Managers.Where(m => m.Name == manLocal.Name).Include(c => c.Offers).FirstOrDefault();

                        if (_manLocal?.Offers.Count > 0 && _man?.Name == _manLocal.Name)
                        {
                            foreach (Offer offer in _manLocal.Offers)
                            {
                                //проверяем наличие идентичного КП в основной базе, если такое уже есть,
                                //изменяем номера счета, заказа и акта, но пропускаем копирование
                                Offer? tempOffer = _man?.Offers.FirstOrDefault(o => o.Data == offer.Data);
                                if (tempOffer != null)
                                {
                                    tempOffer.Invoice = offer.Invoice;
                                    tempOffer.Order = offer.Order;
                                    tempOffer.Act = offer.Act;
                                    continue;
                                }

                                //копируем итеративное КП в новое с целью автоматического присваивания Id при вставке в базу
                                Offer _offer = new(offer.N, offer.Company, offer.Amount, offer.Material, offer.Services)
                                {
                                    Invoice = offer.Invoice,
                                    Order = offer.Order,
                                    Act = offer.Act,
                                    CreatedDate = offer.CreatedDate,
                                    EndDate = offer.EndDate,
                                    Autor = offer.Autor,
                                    Manager = _man,         //указываем соответствующего менеджера  
                                    Data = offer.Data
                                };

                                db.Offers.Add(_offer);     //переносим расчет в базу этого менеджера
                            }
                        }
                    }

                    db.SaveChanges();                      //сохраняем изменения в основной базе данных
                    StatusBegin($"Основная база обновлена");
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    StatusBegin(ex.Message);
                }
            }
        }

        private void UpdateDatabases(object sender, RoutedEventArgs e)      //метод обновления локальных баз
        {
            if (!isLocal) return;                               //если запущена основная база, выходим из метода

            //подключаемся к основной базе типовых деталей
            using TypeDetailContext dbType = new(connections[3]);

            if (dbType.Database.CanConnect())   //проверяем, свободна ли база для подключения
            {
                try
                {
                    //подключаемся к локальной базе данных
                    using TypeDetailContext dbLocalType = new(connections[2]);

                    foreach (TypeDetail typeDetail in dbType.TypeDetails)
                    {
                        // получаем типовую деталь из локальной базы, совпадающую с типовой деталью из основной базы по имени
                        TypeDetail? type = dbLocalType.TypeDetails.FirstOrDefault(t => t.Name == typeDetail.Name);

                        if (type != null)       //если такая типовая деталь существует, меняем её второстепенные свойства
                        {
                            type.Price = typeDetail.Price;
                            type.Sort = typeDetail.Sort;
                        }
                        else                    //иначе создаем новую типовую деталь и добавляем ее в локальную базу
                        {
                            TypeDetail _type = new()
                            {
                                Name = typeDetail.Name,
                                Price = typeDetail.Price,
                                Sort = typeDetail.Sort
                            };
                            dbLocalType.TypeDetails.Add(_type);
                        }
                    }
                    dbLocalType.SaveChanges();  //сохраняем изменения в локальной базе типовых деталей
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    StatusBegin(ex.Message);
                }
            }

            //подключаемся к основной базе работ
            using WorkContext dbWork = new(connections[5]);

            if (dbWork.Database.CanConnect())   //проверяем, свободна ли база для подключения
            {
                try
                {
                    //подключаемся к локальной базе данных
                    using WorkContext dbLocalWork = new(connections[4]);

                    foreach (Work w in dbWork.Works)
                    {
                        // получаем работу из локальной базы, совпадающую с работой из основной базы по имени
                        Work? work = dbLocalWork.Works.FirstOrDefault(r => r.Name == w.Name);

                        if (work != null)       //если такая работа существует, меняем её второстепенные свойства
                        {
                            work.Price = w.Price;
                            work.Time = w.Time;
                        }
                        else                    //иначе создаем новую работу и добавляем ее в локальную базу
                        {
                            Work _work = new()
                            {
                                Name = w.Name,
                                Price = w.Price,
                                Time = w.Time
                            };
                            dbLocalWork.Works.Add(_work);
                        }
                    }
                    dbLocalWork.SaveChanges();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    StatusBegin(ex.Message);
                }
            }

            //подключаемся к основной базе металлов
            using MetalContext dbMetal = new(connections[7]);

            if (dbMetal.Database.CanConnect())   //проверяем, свободна ли база для подключения
            {
                try
                {
                    //подключаемся к локальной базе данных
                    using MetalContext dbLocalMetal = new(connections[6]);

                    foreach (Metal met in dbMetal.Metals)
                    {
                        // получаем работу из локальной базы, совпадающую с работой из основной базы по имени
                        Metal? metal = dbLocalMetal.Metals.FirstOrDefault(m => m.Name == met.Name);

                        if (metal != null)       //если такая работа существует, меняем её второстепенные свойства
                        {
                            metal.Density = met.Density;
                            metal.MassPrice = met.MassPrice;
                            metal.WayPrice = met.WayPrice;
                            metal.PinholePrice = met.PinholePrice;
                            metal.MoldPrice = met.MoldPrice;
                        }
                        else                    //иначе создаем новую работу и добавляем ее в локальную базу
                        {
                            Metal _metal = new()
                            {
                                Name = met.Name,
                                Density = met.Density,
                                MassPrice = met.MassPrice,
                                WayPrice = met.WayPrice,
                                PinholePrice = met.PinholePrice,
                                MoldPrice = met.MoldPrice
                            };
                            dbLocalMetal.Metals.Add(_metal);
                        }
                    }
                    dbLocalMetal.SaveChanges();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    StatusBegin(ex.Message);
                }
            }

            MessageBox.Show("Локальные базы заготовок, работ и металлов обновлены.\nЧтобы применить изменения, необходимо перезапустить программу.",
                "Обновление локальных баз", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        private void OffersFormatting(object sender, DataGridRowEventArgs e)    // при загрузке строк (OffersGrid.LoadingRow="OffersFormatting")
        {
            if (e.Row.DataContext is not Offer offer) return;

            SolidColorBrush _endDateBrush = new(Colors.Gold);               //цвет расчета, у которого наступила дата отгрузки
            SolidColorBrush _activeOfferBrush = new(Colors.PaleGreen);      //цвет загруженного расчета

            // если наступила дата отгрузки, а закрывающий документ еще не записан,
            // предполагаем, что отгрузка еще не произведена, и окрашиваем такой расчет в золотой цвет
            if (offer.EndDate <= DateTime.Now && offer.Order != null && offer.Order != "" && (offer.Act == null || offer.Act == ""))
                e.Row.Background = _endDateBrush;
            if (offer != null && offer == ActiveOffer)                      // если расчет загружен, окрашиваем его в зеленый цвет
                e.Row.Background = _activeOfferBrush;
        }

        void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (((PropertyDescriptor)e.PropertyDescriptor).IsBrowsable == false) e.Cancel = true;   //скрываем свойства с атрибутом [IsBrowsable]
            if (e.PropertyName == "Autor") e.Column.IsReadOnly = true;                              //запрещаем редактировать автора расчета
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

        private void SetAllMetal(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cBox)
                foreach (DetailControl d in DetailControls)
                    foreach (TypeDetailControl t in d.TypeDetailControls) t.CheckMetal.IsChecked = cBox.IsChecked;
            UpdateResult(sender, e);
        }

        public float GetMetalPrice()
        {
            float metalprice = 0;
            foreach (DetailControl d in DetailControls)
                foreach (TypeDetailControl t in d.TypeDetailControls) metalprice += t.Result;

            return (float)Math.Round(metalprice, 2);
        }

        public float GetServices() => (float)Math.Round(Result - GetMetalPrice(), 2);

        public ObservableCollection<Part> Parts = new();
        private ObservableCollection<Part> PartsSource()
        {
            ObservableCollection<Part> parts = new();

            for (int i = 0; i < DetailControls.Count; i++)
                for (int j = 0; j < DetailControls[i].TypeDetailControls.Count; j++)
                    for (int k = 0; k < DetailControls[i].TypeDetailControls[j].WorkControls.Count; k++)
                        if (DetailControls[i].TypeDetailControls[j].WorkControls[k].workType is CutControl _cut)
                            if (_cut.PartsControl != null && _cut.PartsControl.Parts.Count > 0)
                                foreach (PartControl p in _cut.PartsControl.Parts)
                                    parts.Add(p.Part);
            return parts;
        }
        private ObservableCollection<Detail> DetailsSource()
        {
            ObservableCollection<Detail> details = new();
            for (int i = 0; i < DetailControls.Count; i++)
            {
                Detail _detail = DetailControls[i].Detail;
                _detail.Metal = _detail.Destiny = _detail.Description = "";     //очищаем описания свойств детали

                for (int j = 0; j < DetailControls[i].TypeDetailControls.Count; j++)
                {
                    //с помощью повторения символа переноса строки визуализируем дерево деталей и работ
                    if (DetailControls[i].TypeDetailControls[j].TypeDetailDrop.SelectedItem is TypeDetail _type)
                        _detail.Metal += $"{_type.Name}" + string.Join("", Enumerable.Repeat('\n', DetailControls[i].TypeDetailControls[j].WorkControls.Count));

                    _detail.Destiny += $"{DetailControls[i].TypeDetailControls[j].S}"
                        + string.Join("", Enumerable.Repeat('\n', DetailControls[i].TypeDetailControls[j].WorkControls.Count));

                    _detail.Accuracy = $"H12/h12 +-IT 12/2"
                        + string.Join("", Enumerable.Repeat('\n', DetailControls[i].TypeDetailControls[j].WorkControls.Count));

                    for (int k = 0; k < DetailControls[i].TypeDetailControls[j].WorkControls.Count; k++)
                    {
                        if (DetailControls[i].TypeDetailControls[j].WorkControls[k].WorkDrop.SelectedItem is Work work)
                        {
                            //для окраски уточняем цвет в описании работы
                            if (DetailControls[i].TypeDetailControls[j].WorkControls[k].workType is PaintControl _paint)
                                _detail.Description += $"{work.Name} (цвет - {_paint.Ral})\n";
                            //для доп работы её наименование добавляем к наименованию работы - особый случай
                            else if (DetailControls[i].TypeDetailControls[j].WorkControls[k].workType is ExtraControl _extra) _detail.Description += $"{_extra.NameExtra}\n";
                            //в остальных случаях добавляем наименование работы
                            else _detail.Description += $"{work.Name}\n";
                        }
                    }
                }
                details.Add(_detail);
            }
            return details;
        }

        private void UpdateResult(object sender, TextChangedEventArgs e)
        {
            UpdateResult();
        }
        private void UpdateResult(object sender, RoutedEventArgs e)
        {
            UpdateResult();
        }
        public void UpdateResult()         // метод принудительного обновления стоимости
        {
            Paint = PaintResult();
            Construct = ConstructResult();

            foreach (DetailControl d in DetailControls)
                foreach (TypeDetailControl t in d.TypeDetailControls) t.PriceChanged();
        }

        public List<DetailControl> DetailControls = new();
        private void AddDetail(object sender, RoutedEventArgs e)
        {
            AddDetail();
        }
        public void AddDetail()
        {
            DetailControl detail = new(new());

            if (DetailControls.Count > 0)
                detail.Margin = new Thickness(0,    //добавил +25, так как увеличился DetailControl
                    DetailControls[^1].Margin.Top + 25 + 25 * DetailControls[^1].TypeDetailControls.Sum(t => t.WorkControls.Count), 0, 0);

            DetailControls.Add(detail);
            ProductGrid.Children.Add(detail);

            detail.AddTypeDetail();   // при добавлении новой детали добавляем дроп комплектации
        }

        public void NewProject()
        {
            ClearDetails();     // удаляем все детали
            ClearCalculate();   // очищаем расчет
            AddDetail();        // добавляем пустой блок детали
        }
        private void ClearDetails(object sender, RoutedEventArgs e)     // метод очищения текущего расчета
        {
            NewProject();
        }
        private void ClearDetails()         // метод удаления всех деталей и очищения текущего расчета
        {
            while (DetailControls.Count > 0) DetailControls[^1].Remove();
        }
        private void ClearCalculate()
        {
            SetRatio(1);
            SetCount(1);
            HasDelivery = false;
            CheckPaint.IsChecked = false;
            CheckConstruct.IsChecked = false;
            Order.Text = Company.Text = DateProduction.Text = Adress.Text = "";
            ProductName.Text = $"Изделие";
            ManagerDrop.SelectedItem = CurrentManager;
        }

        private void SetDate(object sender, SelectionChangedEventArgs e)
        {
            DateProduction.Text = $"{GetBusinessDays(DateTime.Now, (DateTime)datePicker.SelectedDate)}";

            static int GetBusinessDays(DateTime startD, DateTime endD)
            {
                int calcBusinessDays =
                    (int)(1 + ((endD - startD).TotalDays * 5 -
                    (startD.DayOfWeek - endD.DayOfWeek) * 2) / 7);

                if (endD.DayOfWeek == DayOfWeek.Saturday) calcBusinessDays--;
                if (startD.DayOfWeek == DayOfWeek.Sunday) calcBusinessDays--;

                return calcBusinessDays;
            }
        }

        public string? DateFormat(DateTime? date)
        {
            return date?.ToString("d MMM");
        }

        private DateTime? EndDate()
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

        public Product SaveProduct()
        {
            Product product = new()
            {
                Name = ProductName.Text,
                Order = Order.Text,
                Company = Company.Text,
                Production = DateProduction.Text,
                Manager = Adress.Text,                  //поле "Manager" сохраняет ссылку на адрес доставки;
                                                        //не менял название поля, чтобы загружались старые сохранения
                Ratio = Ratio,                
                Count = Count,
                PaintRatio = PaintRatio.Text,
                ConstructRatio = ConstructRatio.Text,
                Delivery = Delivery,
                DeliveryRatio = DeliveryRatio,
                IsLaser = IsLaser,
                IsAgent = IsAgent,
                HasDelivery = HasDelivery,
                HasConstruct = CheckConstruct.IsChecked,
                HasPaint = CheckPaint.IsChecked
            };

            ProductModel.Product.Details = product.Details = SaveDetails();
            ProductModel.Product = product;

            return product;
        }
        public ObservableCollection<Detail> SaveDetails()
        {
            if (TempWorks.Count > 0) TempWorks.Clear();

            ObservableCollection<Detail> details = new();
            for (int i = 0; i < DetailControls.Count; i++)
            {
                DetailControl det = DetailControls[i];
                Detail _detail = det.Detail;
                _detail.Metal = _detail.Destiny = _detail.Description = "";     //очищаем описания свойств детали

                if (_detail.TypeDetails.Count > 0) _detail.TypeDetails.Clear();     //как будто решаем проблему дублирования при пересохранении

                for (int j = 0; j < det.TypeDetailControls.Count; j++)
                {
                    TypeDetailControl type = det.TypeDetailControls[j];
                    SaveTypeDetail _typeDetail = new(type.TypeDetailDrop.SelectedIndex, type.Count, type.MetalDrop.SelectedIndex, type.HasMetal,
                        (type.SortDrop.SelectedIndex, type.A, type.B, type.S, type.L), type.ExtraResult);

                    //с помощью повторения символа переноса строки визуализируем дерево деталей и работ
                    if (type.MetalDrop.SelectedItem is Metal _metal)
                        _detail.Metal += $"{_metal.Name}" + string.Join("", Enumerable.Repeat('\n', type.WorkControls.Count));
                    _detail.Destiny += $"{type.S}" + string.Join("", Enumerable.Repeat('\n', type.WorkControls.Count));
                    _detail.Accuracy = $"H12/h12 +-IT 12/2";

                    //удаляем крайний перенос строки
                    if (j == det.TypeDetailControls.Count - 1)
                    {
                        _detail.Metal = _detail.Metal.TrimEnd('\n');
                        _detail.Destiny = _detail.Destiny.TrimEnd('\n');
                    }

                    for (int k = 0; k < type.WorkControls.Count; k++)
                    {
                        WorkControl work = type.WorkControls[k];

                        if (work.WorkDrop.SelectedItem is Work _work)
                        {
                            if (work.workType is ExtraControl extra && extra.NameExtra != null)       //проверяем наличие доп работ
                            {
                                //если список еще не содержит доп работу с таким именем, создаем такую запись, иначе просто добавляем стоимость
                                if (!TempWorks.ContainsKey(extra.NameExtra)) TempWorks[extra.NameExtra] = work.Result;
                                else TempWorks[extra.NameExtra] += work.Result;
                            }
                            else if (_work.Name != null)                                                //проверяем все остальные работы
                            {
                                //если список еще не содержит работу с таким именем, создаем такую запись, иначе просто добавляем стоимость
                                if (!TempWorks.ContainsKey(_work.Name)) TempWorks[_work.Name] = work.Result;
                                else TempWorks[_work.Name] += work.Result;
                            }

                            SaveWork _saveWork = new(_work.Name, work.Ratio, work.TechRatio);

                            if (Parts.Count > 0 && work.workType is CutControl _cut)
                            {
                                foreach (Part p in _cut.PartDetails)
                                {
                                    p.Description = "Л";
                                    p.Accuracy = $"H14/h14 +-IT 14/2";
                                    p.Price = 0;

                                    (string, string) tuple = ("0", "0");
                                    if (p.PropsDict.ContainsKey(100)) tuple = (p.PropsDict[100][0], p.PropsDict[100][1]);   //получаем габариты детали
                                    p.PropsDict.Clear();                                                                    //очищаем словарь свойств
                                    p.PropsDict[100] = new() { tuple.Item1, tuple.Item2 };              //записываем габариты обратно в словарь свойств

                                    //добавляем конструкторские работы в цену детали, если их необходимо "размазать"
                                    if (CheckConstruct.IsChecked == true)
                                    {
                                        p.Price += Construct / DetailControls.Count /
                                        DetailControls.SingleOrDefault(d => d.Detail.IsComplect).TypeDetailControls.Count /
                                        _cut.PartDetails.Sum(p => p.Count);
                                        p.Accuracy += $" + {(float)Math.Round(p.Price, 2)}(к)";
                                    }
                                    //"размазываем" доп работы
                                    foreach (WorkControl w in work.type.WorkControls)
                                        if (w.workType is ExtraControl)
                                        {
                                            p.Price += w.Result / _cut.PartDetails.Sum(p => p.Count);
                                            p.Accuracy += $" + {(float)Math.Round(w.Result / _cut.PartDetails.Sum(p => p.Count), 2)}(д)";
                                        }
                                }

                                if (_cut.PartsControl != null && _cut.PartsControl.Parts.Count > 0)
                                    foreach (PartControl part in _cut.PartsControl.Parts) part.PropertiesChanged?.Invoke(part, true);

                                _saveWork.Parts = _cut.PartDetails;
                                _saveWork.Items = _cut.items;
                            }

                            work.PropertiesChanged?.Invoke(work, true);
                            _saveWork.PropsList = work.propsList;

                            //для окраски уточняем цвет в описании работы
                            if (work.workType is PaintControl _paint) _detail.Description += $"{_work.Name}(цвет - {_paint.Ral})\n";
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

            if (TempWorks.Count > 0 && Paint > 0)
            {
                if (TempWorks.ContainsKey("Окраска")) TempWorks["Окраска"] += Paint;
                else TempWorks["Окраска"] = Paint;
            }

            return details;
        }

        public void SaveOrRemoveOffer(bool isSave)
        {
                //подключаемся к базе данных
            using ManagerContext db = new(isLocal ? connections[0] : connections[1]);
            bool isAvalaible = db.Database.CanConnect();                            //проверяем, свободна ли база для подключения
            if (isAvalaible && ManagerDrop.SelectedItem is Manager man)             //если база свободна, получаем выбранного менеджера
            {
                try
                {
                    Manager? _man = db.Managers.FirstOrDefault(m => m.Id == man.Id);            //ищем его в базе по Id
                    if (isSave)     //если метод запущен с параметром true, то есть в режиме сохранения
                    {
                        //сначала создаем новое КП
                        Offer _offer = new(Order.Text, Company.Text, Result, GetMetalPrice(), GetServices())
                        {
                            EndDate = EndDate(),
                            Autor = CurrentManager.Name,
                            Manager = _man,
                            Data = SaveOfferData()      //сериализуем расчет в виде строки json
                        };

                        _man?.Offers.Add(_offer);       //добавляем созданный расчет в базу этого менеджера
                    }
                    else            //если метод запущен с параметром false, то есть в режиме удаления
                    {
                        if (OffersGrid.SelectedItem is Offer offer)                             //получаем выбранный расчет
                        {
                            Offer? _offer = db.Offers.FirstOrDefault(o => o.Id == offer.Id);    //ищем этот расчет по Id
                            if (_offer != null) _man?.Offers.Remove(_offer);                    //если находим, то удаляем его из базы
                        }
                    }

                    db.SaveChanges();           //сохраняем изменения в базе данных
                    ViewOffersGrid(man);        //и обновляем datagrid
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    StatusBegin(ex.Message);
                }
            }
        }

        //public void UpdateOffer(Manager man, Offer offer)
        //{
        //    // Загрузить только идентификатор менеджера
        //    int id = dbManagers.Managers
        //        .Where(c => c.Id == man.Id)
        //        .Select(c => c.Id)
        //        .FirstOrDefault();

        //    Manager customer = new() { Id = id };

        //    // Загрузить идентификатор и значение внешнего ключа
        //    // для заказа
        //    var tempOrder = dbManagers.Offers
        //        .Where(o => o.Id == offer.Id)
        //        .Select(o => new {
        //            id = o.Id,
        //            o.ManagerId
        //        })
        //        .FirstOrDefault();

        //    Offer order = new()
        //    {
        //        Id = tempOrder.id,
        //        ManagerId = tempOrder.ManagerId
        //    };

        //    // Обновить внешний ключ
        //    order.ManagerId = customer.Id;

        //    // Прикрепить сущность к контексту и указать, что
        //    // изменился только внешний ключ
        //    dbManagers.Offers.Attach(order);
        //    dbManagers.Entry(order).Property(o => o.ManagerId).IsModified = true;

        //    dbManagers.SaveChanges();
        //}

        public string SaveOfferData()                               //метод сохранения расчета в базе данных
        {
            using MemoryStream stream = new();
            DataContractJsonSerializer serializer = new(typeof(Product));
            serializer.WriteObject(stream, ProductModel.Product);   //сериализуем объект

            return Encoding.UTF8.GetString(stream.ToArray());       //возвращаем строку преобразованного объекта в массив байтов
        }

        public static Product? OpenOfferData(string json)           //метод загрузки расчета из базы данных
        {
            byte[] bytes = Encoding.Unicode.GetBytes(json);         //преобразуем строку в массив байтов

            using MemoryStream stream = new(bytes);
            DataContractJsonSerializer serializer = new(typeof(Product));

            return (Product?)serializer.ReadObject(stream);         //возвращаем десериализованный объект
        }

        //private void EditOffers(object sender, System.Windows.Input.MouseEventArgs e)       //когда мышь покидает OffersGrid
        //{
        //    bool saved = false;
        //    while (!saved)
        //    {
        //        try
        //        {
        //            // попытаемся сохранить изменения в базе данных
        //            dbManagers.SaveChanges();       
        //            saved = true;
        //        }
        //        catch (DbUpdateConcurrencyException ex)
        //        {
        //            StatusBegin($"Конфликт изменения данных.\nПопробуйте еще раз.");

        //            foreach (var entry in ex.Entries)
        //            {
        //                if (entry.Entity is Offer)
        //                {
        //                    var proposedValues = entry.CurrentValues;
        //                    var databaseValues = entry.GetDatabaseValues();

        //                    foreach (var property in proposedValues.Properties)
        //                    {
        //                        var proposedValue = proposedValues[property];
        //                        var databaseValue = databaseValues[property];

        //                        // TODO: decide which value should be written to database
        //                        // proposedValues[property] = <value to be saved>;
        //                    }

        //                    // Refresh original values to bypass next concurrency check
        //                    entry.OriginalValues.SetValues(databaseValues);
        //                }
        //                else
        //                {
        //                    throw new NotSupportedException(
        //                        "Don't know how to handle concurrency conflicts for "
        //                        + entry.Metadata.Name);
        //                }
        //            }
        //        }
        //    }
        //}

        public void LoadProduct()
        {
            if (ProductModel.Product == null) return;

            ProductName.Text = ProductModel.Product.Name;
            Order.Text = ProductModel.Product.Order;
            Company.Text = ProductModel.Product.Company;
            DateProduction.Text = ProductModel.Product.Production;
            Adress.Text = ProductModel.Product.Manager;
            CheckPaint.IsChecked = ProductModel.Product.HasPaint;
            PaintRatio.Text = ProductModel.Product.PaintRatio;
            CheckConstruct.IsChecked = ProductModel.Product.HasConstruct;
            ConstructRatio.Text = ProductModel.Product.ConstructRatio;
            SetRatio(ProductModel.Product.Ratio);
            SetCount(ProductModel.Product.Count);
            SetDeliveryRatio(ProductModel.Product.DeliveryRatio);
            SetDelivery(ProductModel.Product.Delivery);
            IsLaser = ProductModel.Product.IsLaser;
            IsAgent = ProductModel.Product.IsAgent;
            HasDelivery = ProductModel.Product.HasDelivery;

            LoadDetails(ProductModel.Product.Details);
            UpdateResult();
        }
        public void LoadDetails(ObservableCollection<Detail> details)
        {
            ClearDetails();   // очищаем текущий расчет

            for (int i = 0; i < details.Count; i++)
            {
                AddDetail();
                DetailControl _det = DetailControls[i];
                _det.Detail.Title = details[i].Title;
                if (_det.Detail.Title == "Комплект деталей") _det.IsComplectChanged();       //если деталь является Комплектом деталей, запускаем ограничения
                _det.Detail.Count = details[i].Count;

                for (int j = 0; j < details[i].TypeDetails.Count; j++)
                {
                    TypeDetailControl _type = DetailControls[i].TypeDetailControls[j];
                    _type.TypeDetailDrop.SelectedIndex = details[i].TypeDetails[j].Index;
                    _type.Count = details[i].TypeDetails[j].Count;
                    _type.MetalDrop.SelectedIndex = details[i].TypeDetails[j].Metal;
                    _type.HasMetal = details[i].TypeDetails[j].HasMetal;
                    _type.SortDrop.SelectedIndex = details[i].TypeDetails[j].Tuple.Item1;
                    _type.A = details[i].TypeDetails[j].Tuple.Item2;
                    _type.B = details[i].TypeDetails[j].Tuple.Item3;
                    _type.S = details[i].TypeDetails[j].Tuple.Item4;
                    _type.L = details[i].TypeDetails[j].Tuple.Item5;
                    _type.ExtraResult = details[i].TypeDetails[j].ExtraResult;

                    for (int k = 0; k < details[i].TypeDetails[j].Works.Count; k++)
                    {
                        WorkControl _work = DetailControls[i].TypeDetailControls[j].WorkControls[k];

                        foreach (Work w in _work.WorkDrop.Items)        // чтобы не подвязываться на сохраненный индекс работы, ориентируемся на ее имя
                                                                        // таким образом избегаем ошибки, когда админ изменит порядок работ в базе данных
                            if (w.Name == details[i].TypeDetails[j].Works[k].NameWork)
                            {
                                _work.WorkDrop.SelectedIndex = _work.WorkDrop.Items.IndexOf(w);
                                break;
                            }

                        if (_work.workType is CutControl _cut && details[i].TypeDetails[j].Works[k].Parts.Count > 0)
                        {                            
                            _cut.items = details[i].TypeDetails[j].Works[k].Items;
                            _cut.SumProperties(_cut.items);

                            _cut.PartDetails = details[i].TypeDetails[j].Works[k].Parts;
                            _cut.Parts = _cut.PartList();
                            _cut.PartsControl = new(_cut, _cut.Parts);
                            _cut.AddPartsTab();

                            if (_cut.PartsControl != null && _cut.PartsControl.Parts.Count > 0)
                                foreach (PartControl part in _cut.PartsControl.Parts)
                                {
                                    if (part.Part.PropsDict.Count > 0)
                                        foreach (int key in  part.Part.PropsDict.Keys) if (key != 100)   //ключ "[100]" зарезервирован на габариты детали 
                                            part.AddControl((int)Parser(part.Part.PropsDict[key][0]));
                                            
                                    part.PropertiesChanged?.Invoke(part, false);
                                }
                        }

                        _work.propsList = details[i].TypeDetails[j].Works[k].PropsList;
                        _work.PropertiesChanged?.Invoke(_work, false);
                        _work.Ratio = details[i].TypeDetails[j].Works[k].Ratio;
                        _work.TechRatio = details[i].TypeDetails[j].Works[k].TechRatio;

                        if (_type.WorkControls.Count < details[i].TypeDetails[j].Works.Count) _type.AddWork();
                    }
                    if (_det.TypeDetailControls.Count < details[i].TypeDetails.Count) _det.AddTypeDetail();
                }
            }
        }

        public void ExportToExcel(string path)      // метод оформления КП в формате excel
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet worksheet = workbook.Workbook.Worksheets.Add("Лист1");

            worksheet.Drawings.AddPicture("A1", IsLaser ? "laser_logo.jpg" : "app_logo.jpg");  //файлы должны быть в директории bin/Debug...

                //оформляем первую строку КП, где указываем название нашей компании и ее телефон
            worksheet.Cells["A1"].Value = Boss.Text + Phone.Text;
            worksheet.Cells[1, 1, 1, 8].Merge = true;
            worksheet.Rows[1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            worksheet.Cells[1, 1, 1, 8].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
            worksheet.Cells[1, 1, 1, 8].Style.Border.Bottom.Color.SetColor(0, IsLaser ? 120 : 255, IsLaser ? 180 : 170, IsLaser ? 255 : 0);

                //оформляем вторую строку, где указываем номер КП, контрагента и дату создания
            worksheet.Rows[2].Style.Font.Size = 16;
            worksheet.Rows[2].Style.Font.Bold = true;
            worksheet.Rows[2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells["C2"].Value = "КП № " + Order.Text + " для " + Company.Text + " от " + DateTime.Now.ToString("d");
            worksheet.Cells[2, 3, 2, 8].Merge = true;
            worksheet.Cells[2, 3, 2, 8].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
            worksheet.Cells[2, 3, 2, 8].Style.Border.Bottom.Color.SetColor(0, IsLaser ? 120 : 255, IsLaser ? 180 : 170, IsLaser ? 255 : 0);

                //оформляем третью строку с предупреждением
            worksheet.Cells["A3"].Value = "Данный расчет действителен в течении 2-х банковских дней";
            worksheet.Cells[3, 1, 3, 8].Merge = true;
            worksheet.Rows[3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

            if (!IsLaser)       //для Провэлда оформляем две уточняющие строки
            {
                worksheet.Cells["C4"].Value = $"Для изготовления изделия";
                worksheet.Cells["C5"].Value = "понадобятся следующие детали и работы:";
                worksheet.Cells[4, 3, 4, 4].Merge = true;
                worksheet.Cells[5, 3, 5, 5].Merge = true;
                worksheet.Cells["E4"].Value = ProductName.Text;
                worksheet.Cells["E4"].Style.Font.Bold = true;
            }

            int row = 8;        //счетчик строк деталей, начинаем с восьмой строки документа

            Dictionary<int, string> TitleAccuracy = new();              //вспомогательный словарь составляющих цены детали

                //если есть нарезанные детали, вычисляем их общую стоимость, и оформляем их в КП
            if (Parts.Count > 0)
            {
                for (int i = 0; i < Parts.Count; i++)
                {
                    Parts[i].Total = Parts[i].Count * Parts[i].Price;
                    TitleAccuracy[i] = Parts[i].Accuracy?[18..];        //заполняем вспомогательный словарь составляющих цены
                    Parts[i].Accuracy = Parts[i].Accuracy?[..17];       //обрезаем строку "Точность" до 17 символа
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
                    d.Detail.Price -= Construct / DetailControls.Count;
                    d.Detail.Total = d.Detail.Price * d.Detail.Count;
                }     
            DataTable detailTable = ToDataTable(ProductModel.Product.Details);
            worksheet.Cells[row, 1].LoadFromDataTable(detailTable, false);
            row += detailTable.Rows.Count;

                //в деталях нам не нужно повторно учитывать "Комплект деталей"
            for (int i = 8; i < row; i++)
                if (Parts.Count > 0 && worksheet.Cells[i, 5].Value != null && $"{worksheet.Cells[i, 5].Value}".Contains("Комплект деталей"))
                {
                    worksheet.DeleteRow(i);
                    row--;
                }

            //взависимости от исходящего контрагента добавляем к наименованию детали формулировку услуги или товара
            foreach (var cell in worksheet.Cells[8, 5, row + 8, 5])
                if (cell.Value != null)
                {
                    cell.Value = IsAgent ? $"{cell.Value}".Insert(0, "Изготовление детали ") : $"{cell.Value}".Insert(0, "Деталь ");
                    if ($"{cell.Value}".ToLower().Contains('s')) cell.Value = $"{cell.Value}"[..$"{cell.Value}".ToLower().LastIndexOf('s')];     //и обрезаем наименование
                }

            //оформляем заголовки таблицы
            for (int col = 0; col < DetailsGrid.Columns.Count; col++)
            {
                worksheet.Cells[6, col + 1].Value = DetailsGrid.Columns[col].Header;
                worksheet.Cells[6, col + 1, 7, col + 1].Merge = true;
                worksheet.Cells[6, col + 1, 7, col + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[6, col + 1, 7, col + 1].Style.Fill.BackgroundColor.SetColor(0, IsLaser ? 120 : 255, IsLaser ? 180 : 170, IsLaser ? 255 : 0);
                worksheet.Cells[6, col + 1].Style.WrapText = true;
            }
            worksheet.Columns[9].Hidden = true;
            worksheet.Columns[10].Hidden = true;

            if (CheckPaint.IsChecked == null)           //если требуется указать окраску отдельной строкой
            {
                worksheet.Cells[row, 5].Value = "Окраска";
                worksheet.Cells[row, 6].Value = 1;
                worksheet.Cells[row, 7].Value = worksheet.Cells[row, 8].Value = Paint;
                row++;
            }

            if (CheckConstruct.IsChecked == null)       //если требуется указать конструкторские работы отдельной строкой
            {
                worksheet.Cells[row, 5].Value = "Конструкторские работы";
                if (float.TryParse(ConstructRatio.Text, out float c) && c > 1)
                {
                    worksheet.Cells[row, 6].Value = c;
                    worksheet.Cells[row, 7].Value = Construct / c;
                }
                else
                {
                    worksheet.Cells[row, 6].Value = 1;
                    worksheet.Cells[row, 7].Value = Construct;
                }
                worksheet.Cells[row, 8].Value = Construct;
                row++;
            }

            if (HasDelivery)                            //если требуется доставка
            {
                worksheet.Cells[row, 5].Value = "Доставка";
                worksheet.Cells[row, 6].Value = DeliveryRatio;
                worksheet.Cells[row, 7].Value = Delivery;
                worksheet.Cells[row, 8].Value = DeliveryRatio * Delivery;
                row++;
                worksheet.Cells[row + 4, 2].Value = "Доставка силами Исполнителя по адресу Заказчика.";
            }
            else
            {
                worksheet.Cells[row + 4, 2].Value = "Самовывоз со склада Исполнителя по адресу: Ленинградская область, Всеволожский район, " +
                    "Колтушское сельское поселение, деревня Мяглово, ул. Дорожная, уч. 4Б.";
                worksheet.Cells[row + 4, 2].Style.WrapText = true;
            }

                //оформляем стиль созданной таблицы
            ExcelRange table = worksheet.Cells[6, 1, row - 1, 8];
            table.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            table.Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            table.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            table.Style.Border.BorderAround(ExcelBorderStyle.Medium);

                //приводим float-значения типа 0,699999993 к формату 0,7
            foreach (var cell in worksheet.Cells[8, 2, row, 2])
                if (cell.Value != null && $"{cell.Value}".Contains("0,7") || $"{cell.Value}".Contains("0,8") || $"{cell.Value}".Contains("1,2"))
                    cell.Style.Numberformat.Format = "0.0";

                //учитываем общий коэффициент в КП
            for (int i = 8; i < row; i++)
            {
                if (worksheet.Cells[i, 6].Value != null && float.TryParse($"{worksheet.Cells[i, 6].Value}", out float c)
                    && worksheet.Cells[i, 7].Value != null && float.TryParse($"{worksheet.Cells[i, 7].Value}", out float p))
                {
                    worksheet.Cells[i, 7].Value = p * Ratio;
                    worksheet.Cells[i, 8].Value = c * p * Ratio;
                }
            }

                //вычисляем итоговую сумму КП и оформляем соответствующим образом
            worksheet.Cells[row, 7].Value = IsAgent ? "ИТОГО:" : "ИТОГО с НДС:";
            worksheet.Cells[row, 7].Style.Font.Bold = true;
            worksheet.Cells[row, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Names.Add("totalOrder", worksheet.Cells[8, 8, row - 1, 8]);
            worksheet.Cells[row, 8].Formula = "=SUM(totalOrder)";
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

            worksheet.Cells[row + 3, 1].Value = "Условия оплаты:";
            worksheet.Cells[row + 3, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[row + 3, 2].Value = "Предоплата 100% по счету Исполнителя.";
            worksheet.Cells[row + 3, 2, row + 3, 5].Merge = true;

            worksheet.Cells[row + 4, 1].Value = "Порядок отгрузки:";
            worksheet.Cells[row + 4, 1, row + 4, 2].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[row + 4, 2, row + 4, 8].Merge = true;

            if (Parts.Count > 0)        //в случае с нарезанными деталями, оформляем расшифровку работ
            {
                worksheet.Cells[row + 5, 1].Value = "Расшифровка работ: ";
                worksheet.Cells[row + 5, 2].Value = "";
                foreach (ExcelRangeBase cell in worksheet.Cells[8, 3, row + 8, 3])
                {
                    if (cell.Value != null && $"{cell.Value}".Contains('Л') && !$"{worksheet.Cells[row + 5, 2].Value}".Contains('Л')) worksheet.Cells[row + 5, 2].Value += "Л - Лазер ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Г ") && !$"{worksheet.Cells[row + 5, 2].Value}".Contains("Г ")) worksheet.Cells[row + 5, 2].Value += "Г - Гибка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("С ") && !$"{worksheet.Cells[row + 5, 2].Value}".Contains("С ")) worksheet.Cells[row + 5, 2].Value += "С - Сварка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("О ") && !$"{worksheet.Cells[row + 5, 2].Value}".Contains("О ")) worksheet.Cells[row + 5, 2].Value += "О - Окраска ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Р ") && !$"{worksheet.Cells[row + 5, 2].Value}".Contains("Р ")) worksheet.Cells[row + 5, 2].Value += "Р - Резьба ";
                    if (cell.Value != null && $"{cell.Value}".Contains("З ") && !$"{worksheet.Cells[row + 5, 2].Value}".Contains("З ")) worksheet.Cells[row + 5, 2].Value += "З - Зенковка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("В ") && !$"{worksheet.Cells[row + 5, 2].Value}".Contains("В ")) worksheet.Cells[row + 5, 2].Value += "В - Вальцовка ";
                }
                worksheet.Cells[row + 5, 2, row + 5, 5].Merge = true;
            }

            worksheet.Cells[row + 6, 1].Value = "Ваш менеджер:";
            worksheet.Cells[row + 6, 2].Value = ManagerDrop.Text;
            worksheet.Cells[row + 6, 2].Style.Font.Bold = true;
            worksheet.Cells[row + 6, 2, row + 6, 4].Merge = true;

            worksheet.Cells[row + 6, 8].Value = "версия: " + version;
            worksheet.Cells[row + 6, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                //выравниваем содержимое документа и оформляем нюансы
            worksheet.Cells.AutoFitColumns();

            if (worksheet.Rows[row + 4].Height < 35) worksheet.Rows[row + 4].Height = 35;       //оформляем строку, где указан порядок отгрузки
            if (!DetailControls[0].TypeDetailControls[0].HasMetal
                && worksheet.Rows[row + 1].Height < 35) worksheet.Rows[row + 1].Height = 35;    //оформляем строку, где указано предупреждение об остатках материала
            if (worksheet.Columns[5].Width < 15) worksheet.Columns[5].Width = 15;               //оформляем столбец, где указано наименование детали
            worksheet.Cells[8, 1, row, 3].Style.WrapText = true;                                //переносим текст при необходимости
            if (IsLaser) worksheet.DeleteRow(4, 2);                                             //удаляем 4 и 5 строки, необходимые только для Провэлда
            if (Parts.Count == 0) worksheet.DeleteRow(row + 5, 1);       //удаляем строку, где указана расшифровка работ, если нет нарезанных деталей
                
            //устанавливаем настройки для печати, чтобы сохранение в формате .pdf выводило весь документ по ширине страницы
            worksheet.PrinterSettings.FitToPage = true;
            worksheet.PrinterSettings.FitToWidth = 1;
            worksheet.PrinterSettings.FitToHeight = 0;
            worksheet.PrinterSettings.HorizontalCentered = true;

            workbook.SaveAs(path.Remove(path.LastIndexOf(".")) + ".xlsx");      //сохраняем файл .xlsx

            CreateScore(worksheet, row - 8, path, TitleAccuracy);      //создаем файл для счета на основе полученного КП
        }

        private void CreateScore(ExcelWorksheet worksheet, int row, string _path, Dictionary<int, string> titleAccuracy)       // метод создания файла для счета
        {
            ExcelRange extable = worksheet.Cells[IsLaser ? 6 : 8, 5, IsLaser ? row + 5 : row + 7, 7];

            using var workbook = new ExcelPackage();
            ExcelWorksheet scoresheet = workbook.Workbook.Worksheets.Add("Лист1");

            extable.Copy(scoresheet.Cells["A2"]);

            scoresheet.Cells["A1"].Value = "Наименование";
            scoresheet.Cells["B1"].Value = "Кол-во";
            scoresheet.Cells["C1"].Value = "Цена";
            scoresheet.Cells["D1"].Value = "Цена";
            scoresheet.Cells["E1"].Value = "Ед. изм.";

            float total = 0;
            for (int i = 0; i < row; i++)
            {
                if (float.TryParse($"{scoresheet.Cells[i + 2, 3].Value}", out float p)) scoresheet.Cells[i + 2, 4].Value = Math.Round(p / 1.2f, 2);
                scoresheet.Cells[i + 2, 5].Value = "шт";
                if (titleAccuracy.ContainsKey(i)) scoresheet.Cells[i + 2, 6].Value = titleAccuracy[i];

                if (float.TryParse($"{scoresheet.Cells[i + 2, 2].Value}", out float f)
                    && float.TryParse($"{scoresheet.Cells[i + 2, 3].Value}", out float c)) total += f * c;
            }
            scoresheet.Cells[row + 2, 6].Value = Math.Round(total, 2);
            scoresheet.Cells[row + 2, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
            scoresheet.Cells[row + 2, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Yellow);

            ExcelRange details = scoresheet.Cells[1, 1, row + 1, 5];

            scoresheet.Cells[2, 8].Value = "ПРОВЭЛД";
            scoresheet.Cells[2, 8, 2, 9].Merge =true;
            scoresheet.Cells[2, 10].Value = "ЛАЗЕРФЛЕКС";
            scoresheet.Cells[2, 10, 2, 11].Merge = true;
            scoresheet.Cells[3, 8].Value = scoresheet.Cells[3, 10].Value = "Работы";
            scoresheet.Cells[3, 9].Value = scoresheet.Cells[3, 11].Value = "Стоимость";

            int las = 0, pr = 0;
            if (TempWorks.Count > 0) foreach (string key in TempWorks.Keys)
                {
                    if (key == "Лазерная резка" || key == "Гибка" || key == "Труборез")
                    {
                        scoresheet.Cells[4 + las, 10].Value = key;
                        scoresheet.Cells[4 + las, 11].Value = Math.Round(TempWorks[key], 2);
                        las++;
                    }
                    else
                    {
                        scoresheet.Cells[4 + pr, 8].Value = key;
                        scoresheet.Cells[4 + pr, 9].Value = Math.Round(TempWorks[key], 2);
                        pr++;
                    }
                }
            int tot = pr >= las ? pr : las;
            ExcelRange table = scoresheet.Cells[2, 8, 4 + tot, 11];
            table.Style.Fill.PatternType = ExcelFillStyle.Solid;
            scoresheet.Cells[2, 8, 4 + tot, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGoldenrodYellow);
            scoresheet.Cells[2, 10, 4 + tot, 11].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCyan);
            scoresheet.Names.Add("totalProweld", scoresheet.Cells[4, 9, 3 + tot, 9]);
            scoresheet.Cells[4 + tot, 9].Formula = "=SUM(totalProweld)";
            scoresheet.Names.Add("totalLaserflex", scoresheet.Cells[4, 11, 3 + tot, 11]);
            scoresheet.Cells[4 + tot, 11].Formula = "=SUM(totalLaserflex)";
            scoresheet.Cells[4 + tot, 9].Style.Font.Bold = scoresheet.Cells[4 + tot, 11].Style.Font.Bold = true;

            ExcelRange material = scoresheet.Cells[6 + tot, 8, 8 + tot, 9];
            material.Style.Fill.PatternType = ExcelFillStyle.Solid;
            material.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Lavender);
            scoresheet.Cells[6 + tot, 8].Value = "Материал:";
            scoresheet.Cells[6 + tot, 9].Value = Math.Round(GetMetalPrice(), 2);
            scoresheet.Cells[7 + tot, 8].Value = "Доставка:";
            scoresheet.Cells[7 + tot, 9].Value = Delivery * DeliveryRatio;
            scoresheet.Cells[8 + tot, 8].Value = "Конструкторские работы:";
            scoresheet.Cells[8 + tot, 9].Value = Construct;

            List<string> _headers = new()
            {
                "Заказ", "Заказчик", "Менеджер", "Толщина и марка металла", "V",
                "Гибка", "V", "Доп работы", "V", "Комментарий", "Дата сдачи", "Лазер (время работ)",
                "Гибка (время работ)", "Количество материала", "Номер КП"
            };
            for (int col = 0; col < _headers.Count; col++) scoresheet.Cells[10 + tot, col + 8].Value = _headers[col];

            DetailControl? complect = DetailControls.SingleOrDefault(d => d.Detail.IsComplect);
            if (complect != null)
                for (int i = 0; i < complect.TypeDetailControls.Count; i++)
                {
                    //"Толщина и марка металла"
                    scoresheet.Cells[i + 11 + tot, 11].Value = $"s{complect.TypeDetailControls[i].S} {complect.TypeDetailControls[i].MetalDrop.Text}";
                    //"Количество материала и (его цена за 1 кг)"
                    if (complect.TypeDetailControls[i].MetalDrop.SelectedItem is Metal met)
                        scoresheet.Cells[i + 11 + tot, 21].Value = $"{complect.TypeDetailControls[i].Mass}" +
                            $" ({(complect.TypeDetailControls[i].CheckMetal.IsChecked == true ? met.MassPrice : 0)}р)";

                    scoresheet.Cells[i + 11 + tot, 9].Value = Company.Text;       //"Заказчик"

                    scoresheet.Cells[i + 11 + tot, 10].Value = ShortManager();    //"Менеджер"

                    if (HasDelivery) scoresheet.Cells[i + 11 + tot, 15].Value = "Доставка ";

                    if (complect.TypeDetailControls[i].CheckMetal.IsChecked == false)
                        scoresheet.Cells[i + 11 + tot, 17].Value = "Давальч ";

                    scoresheet.Cells[i + 11 + tot, 18].Value = EndDate();        //"Дата сдачи"
                    scoresheet.Cells[i + 11 + tot, 18].Style.Numberformat.Format = "d MMM";

                    scoresheet.Cells[i + 11 + tot, 22].Value = Order.Text;       //"Номер КП"

                    foreach (WorkControl w in complect.TypeDetailControls[i].WorkControls)          //анализируем работы каждой типовой детали
                    {
                        if (w.workType is CutControl)
                        {
                            scoresheet.Cells[i + 11 + tot, 19].Value = Math.Ceiling(w.Result * 0.012f);     //"Лазер (время работ)"
                            if (w.TechRatio > 1) scoresheet.Cells[i + 11 + tot, 19].Value += $" (x{w.TechRatio})";
                        } 
                        else if (w.workType is BendControl)
                        {
                            scoresheet.Cells[i + 11 + tot, 13].Value = "гибка";
                            scoresheet.Cells[i + 11 + tot, 20].Value = Math.Ceiling(w.Result * 0.0085f);    //"Гибка (время работ)"
                            if (w.TechRatio > 1) scoresheet.Cells[i + 11 + tot, 20].Value += $" (x{w.TechRatio})";
                        }
                        //для доп работы её наименование добавляем к наименованию работы - особый случай
                        else if (w.workType is ExtraControl _extra) scoresheet.Cells[i + 11 + tot, 15].Value += $"{_extra.NameExtra} ";
                        else if (w.WorkDrop.SelectedItem is Work work) scoresheet.Cells[i + 11 + tot, 15].Value += $"{work.Name} ";     //"Доп работы"
                    }
                }

            ExcelRange registry = scoresheet.Cells[10 + tot, 8, 10 + tot, 22];
            if (complect != null)
            {
                registry = scoresheet.Cells[10 + tot, 8, complect.TypeDetailControls.Count + 10 + tot, 22];
                scoresheet.Cells[10 + tot, 11, complect.TypeDetailControls.Count + 10 + tot, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;    //"Толщина и марка металла"
                scoresheet.Cells[10 + tot, 18, complect.TypeDetailControls.Count + 10 + tot, 18].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;    //"Дата сдачи"
                scoresheet.Cells[10 + tot, 22, complect.TypeDetailControls.Count + 10 + tot, 22].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;     //"Номер КП"
            }

            details.Style.Border.Bottom.Style = table.Style.Border.Bottom.Style = material.Style.Border.Bottom.Style = registry.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            details.Style.Border.Right.Style = table.Style.Border.Right.Style = material.Style.Border.Right.Style = registry.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            details.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            table.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            material.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            registry.Style.Border.BorderAround(ExcelBorderStyle.Thin);

            scoresheet.Cells.AutoFitColumns();

            workbook.SaveAs(Path.GetDirectoryName(_path) + "\\" + "Файл для счета " + Order.Text + " на сумму " + $"{Result}" + ".xlsx");
        }

        public static DataTable ToDataTable<T>(ObservableCollection<T> items)
        {
            var tb = new DataTable(typeof(T).Name);

            PropertyInfo[] props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in props)
            {
                Type t = GetCoreType(prop.PropertyType);
                tb.Columns.Add(prop.Name, t);
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

        public static Type GetCoreType(Type t)
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

        public static float Parser(string data)        //обёртка для парсинга float-значений
        {
            if (float.TryParse(data, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.InvariantCulture, out float f)) return f;
            else return 0;
        }

        public static float SizeDetail(string str)
        {
            //выходим из метода, если строки нет, или она не содержит информацию о размере детали
            if (str == null || !str.Contains('X')) return 0;

            float size = 1;

            //создаем и сразу инициализируем массив строк по следующему принципу:
            //если у детали есть отверстия('Ø'), то сначала обрезаем строку до знака диаметра,
            //а затем разделяем получившуюся строку на два числовых значения (размеры детали),
            //иначе сразу разделяем строку на размеры
            string[] sizes = str.Contains('Ø') ? str[..str.IndexOf('Ø')].Split('X') : str.Split('X');

            //перемножаем размеры детали, получая её площадь (это нужно для расчета окраски)
            foreach (string s in sizes) size *= Parser(s);

            return size;
        }

        private void Exit(object sender, CancelEventArgs e)
        {
            MessageBoxResult response = MessageBox.Show("Выйти без сохранения?", "Выход из программы",
                                           MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (response == MessageBoxResult.No) e.Cancel = true;
            else Environment.Exit(0);
        }
        public void Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private static void ThemeChange(string style)
        {
            // определяем путь к файлу ресурсов
            Uri? uri = new(style + ".xaml", UriKind.Relative);
            // загружаем словарь ресурсов
            ResourceDictionary? resourceDict = Application.LoadComponent(uri) as ResourceDictionary;
            // очищаем коллекцию ресурсов приложения
            Application.Current.Resources.Clear();
            // добавляем загруженный словарь ресурсов
            Application.Current.Resources.MergedDictionaries.Add(resourceDict);
        }

        public void CreateReport(string path)           //метод создания отчета...
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet worksheet = workbook.Workbook.Worksheets.Add("Лист1");
                            
                //создаем новый список КП, которые выложены в работу, т.е. оплачены и имеют номер заказа
            List<Offer> _offers = new(Offers.Where(o => o.Order is not null and not ""
                                                && o.CreatedDate >= ReportCalendar.SelectedDates[0] && o.CreatedDate <= ReportCalendar.SelectedDates[^1]));

            List<string> _headers = new() { "дата", "№счета", "проект", "№заказа", "работа", "металл", "Итого" };
            List<Offer> _agentFalse = new();    //ООО
            List<Offer> _agentTrue = new();     //ИП и ПК

            foreach (Offer offer in _offers)
            {
                if (ExtractAgent(offer) == "f") _agentFalse.Add(offer);
                else if (ExtractAgent(offer) == "t") _agentTrue.Add(offer);
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
                    worksheet.Cells[f + 3, 5].Value = _agentFalse[f].Services;
                    worksheet.Cells[f + 3, 6].Value = _agentFalse[f].Material;
                    worksheet.Cells[f + 3, 7].Value = _agentFalse[f].Amount;
                }

                worksheet.Names.Add("totalS1", worksheet.Cells[3, 5, 2 + _agentFalse.Count, 5]);
                worksheet.Cells[3 + _agentFalse.Count, 5].Formula = "=SUM(totalS1)";
                worksheet.Cells[3 + _agentFalse.Count, 9].Formula = "=(SUM(totalS1)-SUM(totalS1)/1.3)/1.2";

                worksheet.Names.Add("totalM1", worksheet.Cells[3, 6, 2 + _agentFalse.Count, 6]);
                worksheet.Cells[3 + _agentFalse.Count, 6].Formula = "=SUM(totalM1)";
                worksheet.Cells[3 + _agentFalse.Count, 10].Formula = "=(SUM(totalM1)-SUM(totalM1)/1.15)/1.2";

                worksheet.Names.Add("total1", worksheet.Cells[3, 7, 2 + _agentFalse.Count, 7]);
                worksheet.Cells[3 + _agentFalse.Count, 7].Formula = "=SUM(total1)";

                worksheet.Cells[3 + _agentFalse.Count, 5, 3 + _agentFalse.Count, 7].Style.Font.Bold = true;
                worksheet.Cells[3, 1, 3 + _agentFalse.Count, 7].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                worksheet.Cells[3, 1, 3 + _agentFalse.Count, 7].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                worksheet.Cells[3, 1, 3 + _agentFalse.Count, 7].Style.Border.BorderAround(ExcelBorderStyle.Medium);

                worksheet.Cells[3 + _agentFalse.Count, 9, 3 + _agentFalse.Count, 10].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[3 + _agentFalse.Count, 9, 3 + _agentFalse.Count, 10].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
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
                    worksheet.Cells[t + 6 + _agentFalse.Count, 2].Value = _agentTrue[t].Invoice;
                    worksheet.Cells[t + 6 + _agentFalse.Count, 3].Value = _agentTrue[t].Company;
                    worksheet.Cells[t + 6 + _agentFalse.Count, 4].Value = _agentTrue[t].Order;
                    worksheet.Cells[t + 6 + _agentFalse.Count, 5].Value = _agentTrue[t].Services;
                    worksheet.Cells[t + 6 + _agentFalse.Count, 6].Value = _agentTrue[t].Material;
                    worksheet.Cells[t + 6 + _agentFalse.Count, 7].Value = _agentTrue[t].Amount;
                }

                worksheet.Names.Add("totalS2", worksheet.Cells[6 + _agentFalse.Count, 5, 5 + _agentFalse.Count + _agentTrue.Count, 5]);
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 5].Formula = "=SUM(totalS2)";
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 9].Formula = "=(SUM(totalS2)-SUM(totalS2)/1.3)/1.2";

                worksheet.Names.Add("totalM2", worksheet.Cells[6 + _agentFalse.Count, 6, 5 + _agentFalse.Count + _agentTrue.Count, 6]);
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 6].Formula = "=SUM(totalM2)";
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 10].Formula = "=SUM(totalM2)-SUM(totalM2)/1.15";

                worksheet.Names.Add("total2", worksheet.Cells[6 + _agentFalse.Count, 7, 5 + _agentFalse.Count + _agentTrue.Count, 7]);
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 7].Formula = "=SUM(total2)";

                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 5, 6 + _agentFalse.Count + _agentTrue.Count, 7].Style.Font.Bold = true;
                worksheet.Cells[6 + _agentFalse.Count, 1, 6 + _agentFalse.Count + _agentTrue.Count, 7].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                worksheet.Cells[6 + _agentFalse.Count, 1, 6 + _agentFalse.Count + _agentTrue.Count, 7].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                worksheet.Cells[6 + _agentFalse.Count, 1, 6 + _agentFalse.Count + _agentTrue.Count, 7].Style.Border.BorderAround(ExcelBorderStyle.Medium);

                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 9, 6 + _agentFalse.Count + _agentTrue.Count, 10].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[6 + _agentFalse.Count + _agentTrue.Count, 9, 6 + _agentFalse.Count + _agentTrue.Count, 10].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
            }

            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 4].Value = "ИТОГО:";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 5].Formula = "=SUM(totalS1)+SUM(totalS2)";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 6].Formula = "=SUM(totalM1)+SUM(totalM2)";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 7].Formula = "=SUM(total1)+SUM(total2)";
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 4, 8 + _agentFalse.Count + _agentTrue.Count, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[8 + _agentFalse.Count + _agentTrue.Count, 4, 8 + _agentFalse.Count + _agentTrue.Count, 7].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightPink);

            worksheet.Cells[10 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Прибыль месяца:";
            worksheet.Cells[10 + _agentFalse.Count + _agentTrue.Count, 2].Formula =
                "=(SUM(totalS1)-SUM(totalS1)/1.3)/1.2+(SUM(totalS2)-SUM(totalS2)/1.3)/1.2+(SUM(totalM1)-SUM(totalM1)/1.15)/1.2+SUM(totalM2)-SUM(totalM2)/1.15";
            worksheet.Cells[10 + _agentFalse.Count + _agentTrue.Count, 3].Formula =
                "=(((SUM(totalS1)-SUM(totalS1)/1.3)/1.2+(SUM(totalS2)-SUM(totalS2)/1.3)/1.2+(SUM(totalM1)-SUM(totalM1)/1.15)/1.2+SUM(totalM2)-SUM(totalM2)/1.15)-150000)*0.15";

            worksheet.Cells[12 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Доп бонус за ИП и ПК:";
            worksheet.Cells[12 + _agentFalse.Count + _agentTrue.Count, 2].Formula = "=SUM(total2)*0.2-SUM(total2)/6";

            worksheet.Cells[14 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Оклад:";
            worksheet.Cells[14 + _agentFalse.Count + _agentTrue.Count, 2].Value = 30000;
            worksheet.Cells[15 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Премия за выполнение плана:";
            worksheet.Cells[15 + _agentFalse.Count + _agentTrue.Count, 1].Style.WrapText = true;
            worksheet.Cells[15 + _agentFalse.Count + _agentTrue.Count, 2].Value = 20000;
            worksheet.Cells[16 + _agentFalse.Count + _agentTrue.Count, 1].Value = "%:";

            worksheet.Names.Add("totalPlus",worksheet.Cells[12 + _agentFalse.Count + _agentTrue.Count, 2, 16 + _agentFalse.Count + _agentTrue.Count, 2]);

            worksheet.Cells[18 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Аванс:";
            worksheet.Cells[19 + _agentFalse.Count + _agentTrue.Count, 1].Value = "На карту:";

            worksheet.Names.Add("totalMinus", worksheet.Cells[18 + _agentFalse.Count + _agentTrue.Count, 2, 19 + _agentFalse.Count + _agentTrue.Count, 2]);

            worksheet.Cells[21 + _agentFalse.Count + _agentTrue.Count, 1].Value = "Итоговая за месяц:";
            worksheet.Cells[21 + _agentFalse.Count + _agentTrue.Count, 2].Formula = "=SUM(totalPlus)";
            worksheet.Cells[22 + _agentFalse.Count + _agentTrue.Count, 1].Value = "К доплате:";
            worksheet.Cells[22 + _agentFalse.Count + _agentTrue.Count, 2].Formula = "=SUM(totalPlus)-SUM(totalMinus)";

            ExcelRange table = worksheet.Cells[10 + _agentFalse.Count + _agentTrue.Count, 1, 22 + _agentFalse.Count + _agentTrue.Count, 3];
            table.Style.Fill.PatternType = ExcelFillStyle.Solid;
            table.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

            ExcelRange bonus = worksheet.Cells[10 + _agentFalse.Count + _agentTrue.Count, 2, 22 + _agentFalse.Count + _agentTrue.Count, 2];
            bonus.Style.Fill.PatternType = ExcelFillStyle.Solid;
            bonus.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);

            worksheet.Cells.AutoFitColumns();
            workbook.SaveAs(path.Remove(path.LastIndexOf(".")) + ".xlsx");      //сохраняем отчет .xlsx
        }

        private string ExtractAgent(Offer offer)
        {
            string _char = string.Empty;
            string substring = "\"IsAgent\"";
            if (offer.Data != null && offer.Data.Contains(substring)) _char = $"{offer.Data[offer.Data.IndexOf(substring) + 10]}";

            return _char;
        }

        private void OpenExample(object sender, RoutedEventArgs e)
        {
            ExampleWindow exampleWindow = new();
            exampleWindow.Show();
        }

        private void UpdateOffer(object sender, RoutedEventArgs e)
        {
                //подключаемся к базе данных
            using ManagerContext db = new(isLocal ? connections[0] : connections[1]);
            bool isAvalaible = db.Database.CanConnect();                        //проверяем, свободна ли база для подключения
            if (isAvalaible && OffersGrid.SelectedItem is Offer offer)          //если база свободна, получаем выбранный расчет
            {
                try
                {
                    Offer? _offer = db.Offers.FirstOrDefault(o => o.Id == offer.Id);    //ищем этот расчет по Id
                    if (_offer != null)
                    {
                        _offer.Invoice = offer.Invoice;
                        db.Entry(_offer).Property(o => o.Invoice).IsModified = true;
                        _offer.Order = offer.Order;
                        db.Entry(_offer).Property(o => o.Order).IsModified = true;
                        _offer.Act = offer.Act;
                        db.Entry(_offer).Property(o => o.Act).IsModified = true;

                        db.SaveChanges();                                               //сохраняем изменения в базе данных
                        StatusBegin("Изменения в базе сохранены");
                    }
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    StatusBegin(ex.Message);
                }
            }
        }

        private string ShortManager()       //метод, возвращающий сокращенное имя менеджера
        {
            return ManagerDrop.Text switch
            {
                "Гамолина Светлана" => "с",
                "Андрейченко Алексей" => "аа",
                "Сергеев Юрий" => "ю",
                "Сергеев Алексей" => "ас",
                "Серых Михаил" => "мс",
                "Мешеронова Мария" => "м",
                _ => ""
            };
        }

        private void CreateDelivery(object sender, RoutedEventArgs e)       //метод построения строки запроса в логистику
        {
            StringBuilder sb = new(DateFormat(EndDate()));      //инициализируем строку датой отгрузки в формате "d MMM"
            
                //если есть номер заказа, добавляем его; иначе добавляем номер КП
            if (ActiveOffer != null && ActiveOffer.Order != null) sb.Append($", №{ActiveOffer.Order}");
            else sb.Append($", №{Order.Text}");

            sb.Append($", {Company.Text}({ShortManager()})");   //добавляем заказчика и менеджера в сокращенном виде
            sb.Append($", примерно {GetTotalMass()} кг;");      //добавляем массу всех деталей
            sb.Append($" {Adress.Text}");                       //и, наконец, адрес доставки и контакт

            StatusBegin($"Запрос в логистику: {sb}");
        }

        private float GetTotalMass()           //метод расчета общей массы ВСЕХ деталей
        {
            float total = 0;

                //если есть нарезанные детали, подсчитываем их массу
            if (Parts.Count > 0) foreach (Part part in Parts) total += part.Mass * part.Count;
                //подсчитываем массу всех деталей, если они не "Комплект деталей!
            foreach (DetailControl det in DetailControls.Where(d => !d.Detail.IsComplect)) total += det.Detail.Mass;

            return (float)Math.Ceiling(total);      //округляем до целого в большую сторону
        }

        public static float MassRatio(float _mass)
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

        private void Test(object sender, TextChangedEventArgs e)
        {
            if (float.TryParse(Wide.Text, out float wide) && int.TryParse(Holes.Text, out int holes)) Test(wide, holes);
        }
        private void Test(float wide, int holes)
        {
            NewProject();
            DetailControls[0].TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedIndex = 4;
            DetailControls[0].TypeDetailControls[^1].A = DetailControls[0].TypeDetailControls[^1].B = 500;
            for (int i = 0; i < Destinies.Count; i++)
            {
                DetailControls[0].TypeDetailControls[^1].A = DetailControls[0].TypeDetailControls[^1].B = 500;
                DetailControls[0].TypeDetailControls[^1].S = (float)Destinies[i];
                DetailControls[0].TypeDetailControls[^1].Count = 1;
                if (DetailControls[0].TypeDetailControls[^1].WorkControls[^1].workType is ThreadControl thread)
                {
                    thread.SetWide($"{wide}");
                    thread.SetHoles($"{holes}");
                }

                if (DetailControls[0].TypeDetailControls.Count < Destinies.Count)
                {
                    DetailControls[0].AddTypeDetail();
                    DetailControls[0].TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedIndex = 4;
                }
            }
        }
    }
}