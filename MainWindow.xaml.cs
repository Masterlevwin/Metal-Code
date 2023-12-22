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
using System.Windows.Media.Imaging;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Data;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

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
        readonly string version = "2.0.0";

        public readonly TypeDetailContext dbTypeDetails = new();
        public readonly WorkContext dbWorks = new();
        public readonly ManagerContext dbManagers = new();
        public readonly MetalContext dbMetals = new();
        public readonly ProductViewModel ProductModel = new(new DefaultDialogService(), new JsonFileService(), new Product());

        public Manager CurrentManager = new();

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
            dbWorks.Database.EnsureCreated();
            dbWorks.Works.Load();

            dbTypeDetails.Database.EnsureCreated();
            dbTypeDetails.TypeDetails.Load();

            //dbManagers.Database.EnsureDeleted();
            dbManagers.Database.EnsureCreated();
            dbManagers.Managers.Load();
            dbManagers.Offers.Load();
            ManagerDrop.ItemsSource = dbManagers.Managers.Local.ToObservableCollection().Where(m => !m.IsEngineer);

            dbMetals.Database.EnsureCreated();
            dbMetals.Metals.Load();
            CreateMetalDict();

            ViewLoginWindow();
        }

        private void ViewLoginWindow()
        {
            LoginWindow loginWindow = new();
            IsEnabled = false;
            if (loginWindow.ShowDialog() == true)
            {
                IsEnabled = true;
                Boss.Text = "ООО Провэлд  ";
                Phone.Text = "тел:(812)603-45-33";
                AddDetail();
                ViewOffersGrid();
            }
        }

        public void UpdateDataBases()
        {
            dbManagers.Managers.Load();
            dbTypeDetails.TypeDetails.Load();
            dbMetals.Metals.Load();
            dbWorks.Works.Load();
        }

        private List<double> Destinies = new() { .5f, .7f, .8f, 1, 1.2f, 1.5f, 2, 2.5f, 3, 4, 5, 6, 8, 10, 12, 14, 16, 18, 20, 25 };
        public Dictionary<string, Dictionary<double, (float, float, float)>> MetalDict = new();
        private void CreateMetalDict()
        {
            List<Metal> metals = new(dbMetals.Metals.Local.ToList());

            foreach (Metal metal in metals)
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
            if (Count > 0) TotalResult();
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
                    HasDelivery = false;
                    Delivery.Text = "";
                }
            }
            UpdateResult();
        }
        private void SetDelivery(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(Delivery.Text, out _)) TotalResult();
        }

        private int count;
        public int Count
        {
            get => count;
            set
            { 
                count = value;
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
            if (Count > 0) TotalResult();
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
        private void SetPaint(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (float.TryParse(tBox.Text, out float p)) Paint = p;
        }
        public float PaintResult()
        {
            float result = 0;
            if (CheckPaint.IsChecked != false)
            {
                foreach (DetailControl d in DetailControls) if (!d.Detail.IsComplect)
                    foreach (TypeDetailControl t in d.TypeDetailControls)
                        result += 87 * t.L * t.Count / 1000;     // простая формула окраски через пог м типовой детали

                    // проверяем наличие работы "Окраска" и добавляем её минималку к расчету, если она есть
                if (dbWorks.Works.Contains(dbWorks.Works.FirstOrDefault(n => n.Name == "Окраска"))
                        && dbWorks.Works.FirstOrDefault(n => n.Name == "Окраска") is Work work) result += work.Price;
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
                // проверяем наличие работы "Конструкторские работы" и добавляем её минималку к расчету
                if (dbWorks.Works.Contains(dbWorks.Works.FirstOrDefault(n => n.Name == "Конструкторские работы"))
                    && dbWorks.Works.FirstOrDefault(n => n.Name == "Конструкторские работы") is Work work) result += work.Price;
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

            if (int.TryParse(Delivery.Text, out int del)) Result += del;
            Result = (float)Math.Round(Result, 2);

            ViewDetailsGrid();
        }

        private void ViewDetailsGrid()
        {
            Parts = PartsSource();

            if (IsLaser)
            {
                Boss.Text = $"ООО ЛАЗЕРФЛЕКС  ";
                Phone.Text = "тел:(812)509-60-11";
                DetailsGrid.ItemsSource = Parts;
            }
            else
            {
                Boss.Text = $"ООО ПРОВЭЛД  ";
                Phone.Text = "тел:(812)603-45-33";
                DetailsGrid.ItemsSource = DetailsSource();
            }
            
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

        private void ViewOffersGrid()
        {
            if (ManagerDrop.SelectedItem is Manager man) OffersGrid.ItemsSource = man.Offers;

            OffersGrid.Columns[0].Header = "N";
            OffersGrid.Columns[1].Header = "Компания";
            OffersGrid.Columns[2].Header = "Итого, руб.";
            OffersGrid.Columns[3].Header = "Материал, руб.";
            OffersGrid.Columns[4].Header = "Дата создания";
            (OffersGrid.Columns[4] as DataGridTextColumn).Binding.StringFormat = "d.MM.y";
            OffersGrid.Columns[5].Header = "Дата отгрузки";
            (OffersGrid.Columns[5] as DataGridTextColumn).Binding.StringFormat = "d.MM.y";
            OffersGrid.Columns[6].Header = "Счёт";
            OffersGrid.Columns[7].Header = "Заказ";
            OffersGrid.Columns[8].Header = "УПД / Акт";
            OffersGrid.Columns[9].Header = "Автор";
            OffersGrid.FrozenColumnCount = 2;
        }

        private void OffersDateProduction(object sender, DataGridRowEventArgs e)        // при загрузке строк (OffersGrid.LoadingRow="OffersDateProduction")
        {
            if (e.Row.DataContext is not Offer offer) return;

            SolidColorBrush hb = new(Colors.Gold);
            SolidColorBrush nb = new(Colors.White);

            // если наступила дата отгрузки, а закрывающий документ еще не записан,
            // предполагаем, что отгрузка еще не произведена, и окрашиваем такое КП в оранжевый цвет
            // иначе КП окрашиваем в стандартный белый цвет
            if (offer.EndDate <= DateTime.Now && (offer.Act == null || offer.Act == ""))
                e.Row.Background = hb;
            else
                e.Row.Background = nb;
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
                            if (_cut.WindowParts != null && _cut.WindowParts.Parts.Count > 0)
                                foreach (PartControl p in _cut.WindowParts.Parts)
                                    parts.Add(p.Part);
            return parts;
        }

        private void UpdateResult(object sender, TextChangedEventArgs e)
        {
            UpdateResult();
        }
        private void UpdateResult(object sender, RoutedEventArgs e)
        {
            UpdateResult();
        }
        private void UpdateResult()         // метод принудительного обновления стоимости
        {
            Paint = PaintResult();
            Construct = ConstructResult();

            foreach (DetailControl d in DetailControls)
                foreach (TypeDetailControl t in d.TypeDetailControls) t.PriceChanged();
        }

        private void SetAllMetal(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cBox)
                foreach (DetailControl d in DetailControls)
                    foreach (TypeDetailControl t in d.TypeDetailControls) t.CheckMetal.IsChecked = cBox.IsChecked;
            UpdateResult(sender, e);
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
                detail.Margin = new Thickness(0,
                    DetailControls[^1].Margin.Top + 25 * DetailControls[^1].TypeDetailControls.Sum(t => t.WorkControls.Count), 0, 0);

            DetailControls.Add(detail);
            ProductGrid.Children.Add(detail);

            detail.AddTypeDetail();   // при добавлении новой детали добавляем дроп комплектации
        }

        private void ClearDetails(object sender, RoutedEventArgs e)     // метод очищения текущего расчета
        {
            ClearCalculate();   // очищаем расчет
            ClearDetails();     // удаляем все детали
            AddDetail();        // добавляем пустой блок детали
            SetCount(1);
        }
        private void ClearDetails()         // метод удаления всех деталей и очищения текущего расчета
        {
            while (DetailControls.Count > 0) DetailControls[^1].Remove();
        }
        private void ClearCalculate()
        {
            SetCount(1);
            ProductName.Text = Order.Text = Company.Text = DateProduction.Text = Delivery.Text = "";
            ManagerDrop.SelectedItem = CurrentManager;
        }

        private void SetDate(object sender, SelectionChangedEventArgs e)
        {
            DateProduction.Text = $"{GetBusinessDays(DateTime.Now, (DateTime)datePicker.SelectedDate)}";

            int GetBusinessDays(DateTime startD, DateTime endD)
            {
                int calcBusinessDays =
                    (int)(1 + ((endD - startD).TotalDays * 5 -
                    (startD.DayOfWeek - endD.DayOfWeek) * 2) / 7);

                if (endD.DayOfWeek == DayOfWeek.Saturday) calcBusinessDays--;
                if (startD.DayOfWeek == DayOfWeek.Sunday) calcBusinessDays--;

                return calcBusinessDays;
            }
        }

        private DateTime? EndDate()
        {
            if (int.TryParse(DateProduction.Text, out int d)) return DateTime.Now.AddDays(d);
            else return null;
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

        public Product SaveProduct()
        {
            Product product = new()
            {
                Name = ProductName.Text,
                Order = Order.Text,
                Company = Company.Text,
                Production = DateProduction.Text,
                Manager = ManagerDrop.Text,
                PaintRatio = PaintRatio.Text,
                ConstructRatio = ConstructRatio.Text,
                Count = Count,
                IsLaser = IsLaser,
                IsAgent = IsAgent,
                HasDelivery = HasDelivery
            };
            if (int.TryParse(Delivery.Text, out int d))product.Delivery = d;

            if (CheckPaint.IsChecked != null) product.HasPaint = (bool)CheckPaint.IsChecked;
            if (CheckConstruct.IsChecked != null) product.HasConstruct = (bool)CheckConstruct.IsChecked;

            product.Details = SaveDetails();

            return product;
        }
        public ObservableCollection<Detail> SaveDetails()
        {
            ObservableCollection<Detail> details = new();
            for (int i = 0; i < DetailControls.Count; i++)
            {
                DetailControl det = DetailControls[i];
                Detail _detail = det.Detail;
                _detail.Metal = _detail.Destiny = _detail.Description = "";     //очищаем описания свойств детали

                //int partsCount = 0;     // считаем количество ВСЕХ нарезанных деталей,
                //                        // чтобы в дальнейшем "размазывать" конструкторские работы в их ценах
                //foreach (TypeDetailControl t in det.TypeDetailControls)
                //    foreach (WorkControl w in t.WorkControls)
                //        if (w.workType is CutControl _cut && _cut.PartDetails.Count > 0)
                //            partsCount += _cut.PartDetails.Sum(c => c.Count);

                if (_detail.TypeDetails.Count > 0) _detail.TypeDetails.Clear();     //как будто решаем проблему дублирования при пересохранении

                for (int j = 0; j < det.TypeDetailControls.Count; j++)
                {
                    TypeDetailControl type = det.TypeDetailControls[j];
                    SaveTypeDetail _typeDetail = new(type.TypeDetailDrop.SelectedIndex, type.Count, type.MetalDrop.SelectedIndex, type.HasMetal,
                        (type.SortDrop.SelectedIndex, type.A, type.B, type.S, type.L));

                    //с помощью повторения символа переноса строки визуализируем дерево деталей и работ
                    if (type.TypeDetailDrop.SelectedItem is TypeDetail _type)
                        _detail.Metal += $"{_type.Name}" + string.Join("", Enumerable.Repeat('\n', type.WorkControls.Count));
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
                            SaveWork _saveWork = new(_work.Name, work.Ratio);

                            if (Parts.Count > 0 && work.workType is CutControl _cut)
                            {
                                foreach (Part p in _cut.PartDetails)
                                {
                                    p.Description = "Л";
                                    p.Price = 0;
                                    p.PropsDict.Clear();
                                    //p.Price += Construct / partsCount;
                                }

                                if (_cut.WindowParts != null && _cut.WindowParts.Parts.Count > 0)
                                    foreach (PartControl part in _cut.WindowParts.Parts) part.PropertiesChanged?.Invoke(part, true);

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
            return details;
        }
        public void SaveOffer(string path)
        {
            foreach (Manager man in dbManagers.Managers.Local.ToObservableCollection()) if (man == ManagerDrop.SelectedItem)
                {
                    foreach (Offer of in man.Offers) if (of.Path == path)
                        {
                            of.N = Order.Text;
                            of.Company = Company.Text;
                            of.Amount = Result;
                            of.Material = GetMetalPrice();
                            of.Services = GetServices();
                            of.EndDate = EndDate();
                            of.Autor = CurrentManager.Name;
                            dbManagers.SaveChanges();
                            OffersGrid.Items.Refresh();
                            return;
                        }

                    Offer offer = new(Order.Text, Company.Text, Result, GetMetalPrice(), GetServices())
                    {
                        EndDate = EndDate(),
                        Path = path,
                        Autor = CurrentManager.Name,
                        Manager = man
                    };

                    man.Offers.Add(offer);
                    dbManagers.SaveChanges();
                    break;
                }
        }

        private void EditOffers(object sender, System.Windows.Input.MouseEventArgs e)       //когда мышь покидает OffersGrid
        {
            dbManagers.SaveChanges();       //сохраняем изменения в базе данных
        }

        public void LoadProduct()
        {            
            ProductName.Text = ProductModel.Product.Name;
            Order.Text = ProductModel.Product.Order;
            Company.Text = ProductModel.Product.Company;
            DateProduction.Text = ProductModel.Product.Production;
            ManagerDrop.Text = ProductModel.Product.Manager;
            CheckPaint.IsChecked = ProductModel.Product.HasPaint;
            PaintRatio.Text = ProductModel.Product.PaintRatio;
            CheckConstruct.IsChecked = ProductModel.Product.HasConstruct;
            ConstructRatio.Text = ProductModel.Product.ConstructRatio;
            SetCount(ProductModel.Product.Count);
            IsLaser = ProductModel.Product.IsLaser;
            IsAgent = ProductModel.Product.IsAgent;
            HasDelivery = ProductModel.Product.HasDelivery;
            Delivery.Text = $"{ProductModel.Product.Delivery}";

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
                            _cut.WindowParts = new(_cut, _cut.PartList());

                            if (_cut.WindowParts != null && _cut.WindowParts.Parts.Count > 0)
                                foreach (PartControl part in _cut.WindowParts.Parts)
                                {
                                    if (part.Part.PropsDict.Count > 0)
                                        foreach (int key in  part.Part.PropsDict.Keys)
                                            part.AddControl((int)Parser(part.Part.PropsDict[key][0]));
                                            
                                    part.PropertiesChanged?.Invoke(part, false);
                                }
                        }

                        _work.propsList = details[i].TypeDetails[j].Works[k].PropsList;
                        _work.PropertiesChanged?.Invoke(_work, false);
                        _work.Ratio = details[i].TypeDetails[j].Works[k].Ratio;

                        if (_type.WorkControls.Count < details[i].TypeDetails[j].Works.Count) _type.AddWork();
                    }
                    if (_det.TypeDetailControls.Count < details[i].TypeDetails.Count) _det.AddTypeDetail();
                }
            }
        }

        public void ExportToExcel(string path)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet worksheet = workbook.Workbook.Worksheets.Add("Лист1");

            worksheet.Drawings.AddPicture("A1", IsLaser ? "laser_logo.jpg" : "app_logo.jpg");  // файлы должны быть в директории bin/Debug...

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

                //если есть нарезанные детали, вычисляем общую стоимость каждой и затем оформляем их в КП
            if (Parts.Count > 0)
            {
                foreach (Part part in Parts) part.Total = part.Count * part.Price;

                DataTable partTable = ToDataTable(Parts);
                worksheet.Cells[row, 1].LoadFromDataTable(partTable, false);
                row += partTable.Rows.Count;
            }

                //далее оформляем остальные детали и работы
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
                if (cell.Value != null) cell.Value = IsAgent ? $"{cell.Value}".Insert(0, "Изготовление детали ") : $"{cell.Value}".Insert(0, "Деталь ");

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
                worksheet.Cells[row, 6].Value = 1;
                worksheet.Cells[row, 7].Value = worksheet.Cells[row, 8].Value = Construct;
                row++;
            }

            if (HasDelivery)        //если требуется доставка
            {
                worksheet.Cells[row, 5].Value = "Доставка";
                worksheet.Cells[row, 6].Value = 1;
                if (int.TryParse(Delivery.Text, out int d)) worksheet.Cells[row, 7].Value = worksheet.Cells[row, 8].Value = d;
                row++;
                worksheet.Cells[row + 4, 2].Value = "Доставка силами Исполнителя по адресу Заказчика.";
            }
            else
            {
                worksheet.Cells[row + 4, 2].Value = "Самовывоз со склада Исполнителя по адресу: Ленинградская область, Всеволожский район," +
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
            worksheet.Cells[row + 1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[row + 1, 2].Value = DetailControls[0].TypeDetailControls[0].HasMetal ? "Исполнителя" : "Заказчика";
            worksheet.Cells[row + 1, 2].Style.Font.Bold = true;
            worksheet.Cells[row + 1, 2, row + 1, 3].Merge = true;

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
            worksheet.Cells[row + 4, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[row + 4, 2].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[row + 4, 2, row + 4, 8].Merge = true;

            if (Parts.Count > 0)        //в случае с нарезанными деталями, оформляем расшифровку работ
            {
                worksheet.Cells[row + 5, 1].Value = "Расшифровка работ: ";
                worksheet.Cells[row + 5, 2].Value = "";
                foreach (ExcelRangeBase cell in worksheet.Cells[8, 3, row + 8, 3])
                {
                    if (cell.Value != null && $"{cell.Value}".Contains("Л") && !$"{worksheet.Cells[row + 5, 2].Value}".Contains("Л")) worksheet.Cells[row + 5, 2].Value += "Л - Лазер ";
                    if (cell.Value != null && $"{cell.Value}".Contains("Г ") && !$"{worksheet.Cells[row + 5, 2].Value}".Contains("Г ")) worksheet.Cells[row + 5, 2].Value += "Г - Гибка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("С ") && !$"{worksheet.Cells[row + 5, 2].Value}".Contains("С ")) worksheet.Cells[row + 5, 2].Value += "С - Сварка ";
                    if (cell.Value != null && $"{cell.Value}".Contains("О ") && !$"{worksheet.Cells[row + 5, 2].Value}".Contains("О ")) worksheet.Cells[row + 5, 2].Value += "О - Окраска ";
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

            CreateScore(worksheet, row - 8, path);      //создаем файл для счета на основе полученного КП
        }

        private void CreateScore(ExcelWorksheet worksheet, int row, string _path)
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

            for (int i = 0; i < row; i++)
            {
                if (float.TryParse($"{scoresheet.Cells[i + 2, 3].Value}", out float p)) scoresheet.Cells[i + 2, 4].Value = Math.Round(p / 1.2f, 2);
                scoresheet.Cells[i + 2, 5].Value = "шт";
                if (float.TryParse($"{scoresheet.Cells[i + 2, 2].Value}", out float f)
                    && float.TryParse($"{scoresheet.Cells[i + 2, 3].Value}", out float c)) scoresheet.Cells[i + 2, 6].Value = Math.Round(f * c, 2);

            }
            scoresheet.Names.Add("totalOrder", scoresheet.Cells[2, 6, row + 1, 6]);
            scoresheet.Cells[row + 2, 6].Formula = "=SUM(totalOrder)";
            scoresheet.Cells[row + 2, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
            scoresheet.Cells[row + 2, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Yellow);

            ExcelRange table = scoresheet.Cells[1, 1, row + 1, 5];
            table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            table.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            table.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            scoresheet.Cells.AutoFitColumns();

            workbook.SaveAs(Path.GetDirectoryName(_path) + "\\" + "Файл для счета " + Order.Text + ".xlsx");
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

        private void ThemeChange(string style)
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

        public void CreateReport(string path)           //метод создания отчета по заказам - нужно доработать!
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();
            ExcelWorksheet worksheet = workbook.Workbook.Worksheets.Add("Лист1");

            if (ManagerDrop.SelectedItem is Manager man)
            {
                //создаем новый список КП, которые выложены в работу, т.е. оплачены и имеют номер заказа
                ObservableCollection<Offer> _offers = new(man.Offers.Where(o => o.Order != null && o.Order != "").ToList());

                //конвертируем список в таблицу и переносим в excel
                DataTable reportTable = ToDataTable(_offers);
                worksheet.Cells["A1"].LoadFromDataTable(reportTable);
            }

            //настраиваем отчет согласно принятому ранее порядку отображения столбцов
            worksheet.DeleteColumn(1, 2);
            worksheet.DeleteColumn(5, 2);
            worksheet.DeleteColumn(8, 4);
            worksheet.InsertColumn(1, 1);
            worksheet.Cells[1, 6, 100, 6].Copy(worksheet.Cells[1, 1, 100, 1]);
            worksheet.InsertColumn(3, 3);
            worksheet.Cells[1, 10, 100, 10].Copy(worksheet.Cells[1, 3, 100, 3]);
            worksheet.Cells[1, 8, 100, 8].Copy(worksheet.Cells[1, 4, 100, 4]);
            worksheet.Cells[1, 7, 100, 7].Copy(worksheet.Cells[1, 5, 100, 5]);
            worksheet.DeleteColumn(7, 5);
            worksheet.InsertRow(1, 1);
            worksheet.Cells[1, 1].Value = $"Счет";
            worksheet.Cells[1, 2].Value = $"Проект";
            worksheet.Cells[1, 3].Value = $"Заказ";
            worksheet.Cells[1, 4].Value = $"Работа";
            worksheet.Cells[1, 5].Value = $"Металл";
            worksheet.Cells[1, 6].Value = $"Итого";

            workbook.SaveAs(path.Remove(path.LastIndexOf(".")) + ".xlsx");      //сохраняем отчет .xlsx
        }
    }
}
