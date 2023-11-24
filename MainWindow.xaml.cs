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
using System.Windows.Media.Animation;
using OfficeOpenXml.Style;
using OfficeOpenXml;
using System.Diagnostics;

namespace Metal_Code
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public static MainWindow M = new();
        readonly string version = "1.0.0";

        public readonly TypeDetailContext dbTypeDetails = new();
        public readonly WorkContext dbWorks = new();
        public readonly ManagerContext dbManagers = new();
        public readonly MetalContext dbMetals = new();
        public readonly ProductViewModel DetailsModel = new(new DefaultDialogService(), new JsonFileService());

        public MainWindow()
        {
            InitializeComponent();
            M = this;
            Version.Text = version;
            DataContext = DetailsModel;
            Loaded += LoadDataBases;
            AddDetail();
            Boss.Text = "ООО Провэлд  ";
            Phone.Text = "тел:(812)603-45-33";
            Agent = true;
        }

        private void LoadDataBases(object sender, RoutedEventArgs e)  // при загрузке окна
        {
            dbWorks.Database.EnsureCreated();
            dbWorks.Works.Load();

            dbTypeDetails.Database.EnsureCreated();
            dbTypeDetails.TypeDetails.Load();

            dbManagers.Database.EnsureCreated();
            dbManagers.Managers.Load();
            ManagerDrop.ItemsSource = dbManagers.Managers.Local.ToObservableCollection();

            dbMetals.Database.EnsureCreated();
            dbMetals.Metals.Load();
            CreateMetalDict();
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

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

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
        private void IsLaserChanged(object sender, RoutedEventArgs e)
        {
            TotalResult();
        }

        private bool agent;
        public bool Agent
        {
            get => agent;
            set
            {
                agent = value;
                OnPropertyChanged(nameof(Agent));
            }
        }
        private void SetAgent(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cBox) cBox.Content = Agent ? "счёт от ИП или ПК" : "счёт от ООО";
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
        private void SetCount(int _count)
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
                foreach (DetailControl d in DetailControls)
                    foreach (TypeDetailControl t in d.TypeDetailControls)
                        result += 110 * t.L * t.Count / 1000;     // простая формула окраски через пог м типовой детали
                                                                  // проверяем наличие работы "Окраска" и добавляем её минималку к расчету
                if (dbWorks.Works.Contains(dbWorks.Works.FirstOrDefault(n => n.Name == "Окраска"))
                    && dbWorks.Works.FirstOrDefault(n => n.Name == "Окраска") is Work work) result += work.Price;
            }
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

            Paint = PaintResult();
            Construct = ConstructResult();

            foreach (DetailControl d in DetailControls) Result += d.Price * d.Count;

            Result *= Count;
            Result = (float)Math.Round(Result, 2);

            ViewDetailsGrid();
        }

        private void ViewDetailsGrid()
        {
            if (IsLaser)
            {
                Boss.Text = $"ООО ЛАЗЕРФЛЕКС  ";
                Phone.Text = "тел:(812)509-60-11";
                DetailsGrid.ItemsSource = PartsSource();
                DetailsGrid.Columns[0].Header = "Материал";
                DetailsGrid.Columns[1].Header = "Толщина";
                DetailsGrid.Columns[2].Header = "Работы";
                DetailsGrid.Columns[3].Header = "Точность";
                DetailsGrid.Columns[4].Header = "Наименование";
                DetailsGrid.Columns[5].Header = "Кол-во, шт";
                DetailsGrid.Columns[6].Header = "Цена за шт, руб";
                DetailsGrid.Columns[7].Header = "Стоимость, руб";
                Parts = PartsSource();
            }
            else
            {
                Boss.Text = $"ООО ПРОВЭЛД  ";
                Phone.Text = "тел:(812)603-45-33";
                DetailsGrid.ItemsSource = DetailsSource();
                DetailsGrid.Columns[0].Header = "Наименование";
                DetailsGrid.Columns[1].Header = "Работы";
                DetailsGrid.Columns[2].Header = "Масса, кг";
                DetailsGrid.Columns[3].Header = "Кол-во, шт";
                DetailsGrid.Columns[4].Header = "Цена за шт, руб";
                DetailsGrid.Columns[5].Header = "Стоимость, руб";
                DetailsGrid.FrozenColumnCount = 1;
            }
        }

        void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (((PropertyDescriptor)e.PropertyDescriptor).IsBrowsable == false) e.Cancel = true;
        }

        public string GetMetalPrice()
        {
            float metalprice = 0;
            foreach (DetailControl d in DetailControls)
                foreach (TypeDetailControl t in d.TypeDetailControls) metalprice += t.Result;

            return $"{Math.Round(metalprice, 2)}";
        }

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
            if (sender is TextBox tBox)
            {
                switch (tBox.Name)
                {
                    case "PaintRatio":
                        if (float.TryParse(PaintRatio.Text, out float p)) Paint *= p;
                        break;
                    case "ConstructRatio":
                        if (float.TryParse(ConstructRatio.Text, out float c)) Construct *= c;
                        break;
                }
            }
        }
        private void UpdateResult(object sender, RoutedEventArgs e)   // метод принудительного обновления стоимости
        {
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
            DetailControl detail = new();

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
        }
        private void ClearDetails()         // метод удаления всех деталей и очищения текущего расчета
        {
            while (DetailControls.Count > 0) DetailControls[^1].Remove();
        }
        private void ClearCalculate()
        {
            SetCount(0);
            CheckDelivery.IsChecked = IsLaser = false;
            ProductName.Text = Order.Text = Company.Text = DateProduction.Text = ManagerDrop.Text = Comment.Text = Delivery.Text = "";
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

        private ObservableCollection<Detail> DetailsSource()
        {
            ObservableCollection<Detail> details = new();
            for (int i = 0; i < DetailControls.Count; i++)
            {
                Detail _detail = new(DetailControls[i].NameDetail, DetailControls[i].Count, DetailControls[i].Mass,
                    DetailControls[i].Price, DetailControls[i].Price * DetailControls[i].Count);
                for (int j = 0; j < DetailControls[i].TypeDetailControls.Count; j++)
                {
                    for (int k = 0; k < DetailControls[i].TypeDetailControls[j].WorkControls.Count; k++)
                    {
                        if (DetailControls[i].TypeDetailControls[j].WorkControls[k].WorkDrop.SelectedItem is Work work)
                        {
                            if (work.Name == "Окраска" && DetailControls[i].TypeDetailControls[j].WorkControls[k].workType is PaintControl _paint)
                                _detail.Description += $"{work.Name} (цвет - {_paint.Ral})\n";
                            else _detail.Description += work.Name + "\n";
                        }
                    }
                }
                details.Add(_detail);
            }
            return details;
        }

        public Product SaveProduct()
        {
            Product prod = new()
            {
                Name = ProductName.Text,
                Order = Order.Text,
                Company = Company.Text,
                Production = DateProduction.Text,
                Manager = ManagerDrop.Text,
                Comment = Comment.Text,
                Count = Count,
                IsLaser = IsLaser,
            };
            if (CheckDelivery.IsChecked != null) prod.HasDelivery = (bool)CheckDelivery.IsChecked;
            if (int.TryParse(Delivery.Text, out int d)) prod.Delivery = d;

            if (CheckPaint.IsChecked != null) prod.HasPaint = (bool)CheckPaint.IsChecked;
            if (CheckConstruct.IsChecked != null) prod.HasConstruct = (bool)CheckConstruct.IsChecked;

            DetailsModel.Product = prod;
            DetailsModel.Product.Details = SaveDetails();
            return DetailsModel.Product;
        }
        public ObservableCollection<Detail> SaveDetails()
        {
            ObservableCollection<Detail> details = new();
            for (int i = 0; i < DetailControls.Count; i++)
            {
                DetailControl det = DetailControls[i];
                Detail _detail = new(det.NameDetail, det.Count, det.Mass, det.Price, det.Price * det.Count);
                    
                for (int j = 0; j < det.TypeDetailControls.Count; j++)
                {
                    TypeDetailControl type = det.TypeDetailControls[j];
                    SaveTypeDetail _typeDetail = new(type.TypeDetailDrop.SelectedIndex, type.Count, type.MetalDrop.SelectedIndex, type.HasMetal,
                        (type.SortDrop.SelectedIndex, type.A, type.B, type.S, type.L));

                    if (type.TypeDetailDrop.SelectedItem is TypeDetail _type) _detail.Description += $"{_type.Name}:" + " ";

                    for (int k = 0; k < type.WorkControls.Count; k++)
                    {
                        WorkControl work = type.WorkControls[k];
                        SaveWork _saveWork = new(work.WorkDrop.SelectedIndex, work.Ratio);

                        if (work.WorkDrop.SelectedItem is Work _work)
                        {
                            if (_work.Name == "Окраска" && work.workType is PaintControl _paint)
                                _detail.Description += $"{_work.Name} (цвет - {_paint.Ral}) ";
                            else _detail.Description += $"{_work.Name}" + " ";
                        }

                        if (IsLaser && work.workType is CutControl _cut)
                        {
                            foreach (Part p in _cut.Parts)
                            {
                                p.Description = "Л";
                                p.Price = 0;
                                p.PropsDict.Clear();
                            }

                            if (_cut.WindowParts != null && _cut.WindowParts.Parts.Count > 0)
                                foreach (PartControl part in _cut.WindowParts.Parts) part.PropertiesChanged?.Invoke(part, true);

                            _saveWork.Parts = _cut.Parts;
                            _saveWork.Items = _cut.items;
                        }

                        work.PropertiesChanged?.Invoke(work, true);
                        _saveWork.PropsList = work.propsList;

                        _typeDetail.Works.Add(_saveWork);
                    }
                    _detail.TypeDetails.Add(_typeDetail);
                }
                details.Add(_detail);
            }
            return details;
        }

        public void LoadProduct()
        {            
            SetCount(DetailsModel.Product.Count);
            CheckDelivery.IsChecked = DetailsModel.Product.HasDelivery;
            Delivery.Text = $"{DetailsModel.Product.Delivery}";
            ProductName.Text = DetailsModel.Product.Name;
            Order.Text = DetailsModel.Product.Order;
            Company.Text = DetailsModel.Product.Company;
            DateProduction.Text = DetailsModel.Product.Production;
            ManagerDrop.Text = DetailsModel.Product.Manager;
            Comment.Text = DetailsModel.Product.Comment;
            IsLaser = DetailsModel.Product.IsLaser;
            CheckPaint.IsChecked = DetailsModel.Product.HasPaint;
            CheckConstruct.IsChecked = DetailsModel.Product.HasConstruct;

            LoadDetails(DetailsModel.Product.Details);
        }
        public void LoadDetails(ObservableCollection<Detail> details)
        {
            ClearDetails();   // очищаем текущий расчет

            for (int i = 0; i < details.Count; i++)
            {
                AddDetail();
                DetailControl _det = DetailControls[i];
                _det.NameDetail = details[i].Title;
                _det.Count = details[i].Count;

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
                        _work.WorkDrop.SelectedIndex = details[i].TypeDetails[j].Works[k].Index;

                        if (IsLaser && _work.workType is CutControl _cut)
                        {                            
                            _cut.items = details[i].TypeDetails[j].Works[k].Items;
                            _cut.SumProperties(_cut.items);

                            _cut.Parts = details[i].TypeDetails[j].Works[k].Parts;
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
                        if (!IsLaser) _work.PropertiesChanged?.Invoke(_work, false);
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

            worksheet.Drawings.AddPicture("A1", IsLaser ? "laser_logo.jpg" : "excel_logo.jpg");  // файлы должны быть в директории bin/Debug...

            worksheet.Cells["A1"].Value = Boss.Text + Phone.Text;
            worksheet.Cells[1, 1, 1, IsLaser ? 8 : 6].Merge = true;
            worksheet.Rows[1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            worksheet.Cells[1, 1, 1, IsLaser ? 8 : 6].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
            worksheet.Cells[1, 1, 1, IsLaser ? 8 : 6].Style.Border.Bottom.Color.SetColor(0, IsLaser ? 120 : 255, IsLaser ? 180 : 170, IsLaser ? 255 : 0);

            worksheet.Rows[2].Style.Font.Size = 16;
            worksheet.Rows[2].Style.Font.Bold = true;
            worksheet.Rows[2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[IsLaser ? "D2" : "B2"].Value = "КП № " + Order.Text + " для " + Company.Text + " от " + DateTime.Now.ToString("d");
            worksheet.Cells[2, IsLaser ? 4 : 2, 2, IsLaser ? 8 : 6].Merge = true;
            worksheet.Cells[2, IsLaser ? 4 : 2, 2, IsLaser ? 8 : 6].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
            worksheet.Cells[2, IsLaser ? 4 : 2, 2, IsLaser ? 8 : 6].Style.Border.Bottom.Color.SetColor(0, IsLaser ? 120 : 255, IsLaser ? 180 : 170, IsLaser ? 255 : 0);

            worksheet.Cells["A3"].Value = "Данный расчет действителен в течении 2-х банковских дней";
            worksheet.Cells[3, 1, 3, IsLaser ? 8 : 6].Merge = true;
            worksheet.Rows[3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

            if (!IsLaser)
            {
                worksheet.Cells["B4"].Value = $"Для изготовления изделия";
                worksheet.Cells["C4"].Value = ProductName.Text;
                worksheet.Cells["C4"].Style.Font.Bold = true;
                worksheet.Cells["B5"].Value = "понадобятся следующие детали и работы:";
            }

            foreach (Part part in Parts) part.Total = (float)Math.Round(part.Count * part.Price, 2);

            DataTable dt = IsLaser ? ToDataTable(Parts) : ToDataTable(DetailsModel.Product.Details);
            worksheet.Cells["A7"].LoadFromDataTable(dt, true);
            if (IsLaser)
            {
                worksheet.Columns[9].Hidden = true;
                worksheet.Columns[10].Hidden = true;
            }

            int num = dt.Rows.Count;

            if (IsLaser) foreach (var cell in worksheet.Cells[8, 5, num + 8, 5])
                    if (cell.Value != null) cell.Value = Agent ? $"{cell.Value}".Insert(0, "Изготовление детали ") : $"{cell.Value}".Insert(0, "Деталь ");

            for (int col = 0; col < DetailsGrid.Columns.Count; col++)
            {
                worksheet.Cells[6, col + 1].Value = DetailsGrid.Columns[col].Header;
                worksheet.Cells[6, col + 1, 7, col + 1].Merge = true;
                worksheet.Cells[6, col + 1, 7, col + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[6, col + 1, 7, col + 1].Style.Fill.BackgroundColor.SetColor(0, IsLaser ? 120 : 255, IsLaser ? 180 : 170, IsLaser ? 255 : 0);
                worksheet.Cells[6, col + 1].Style.WrapText = true;
            }

            if (!IsLaser && CheckPaint.IsChecked == null)
            {
                worksheet.Cells[num + 8, 1].Value = "Окраска";
                worksheet.Cells[num + 8, 4].Value = 1;
                worksheet.Cells[num + 8, 5].Value = worksheet.Cells[num + 8, 6].Value = Paint;
                num++;
            }

            if (CheckConstruct.IsChecked == null)
            {
                worksheet.Cells[num + 8, IsLaser ? 5 : 1].Value = "Конструкторские работы";
                worksheet.Cells[num + 8, IsLaser ? 6 : 4].Value = 1;
                worksheet.Cells[num + 8, IsLaser ? 7 : 5].Value = worksheet.Cells[num + 8, IsLaser ? 8 : 6].Value = Construct;
                num++;
            }

            if (CheckDelivery.IsChecked == true)
            {
                worksheet.Cells[num + 8, IsLaser ? 5 : 1].Value = "Доставка";
                worksheet.Cells[num + 8, IsLaser ? 6 : 4].Value = 1;
                if (int.TryParse(Delivery.Text, out int d)) worksheet.Cells[num + 8, IsLaser ? 7 : 5].Value = worksheet.Cells[num + 8, IsLaser ? 8 : 6].Value = d;
                num++;
                worksheet.Cells[num + 12, 2].Value = "Доставка силами Исполнителя по адресу Заказчика.";
            }
            else
            {
                worksheet.Cells[num + 12, 2].Value = "Самовывоз со склада Исполнителя по адресу: Ленинградская область, Всеволожский район," +
                    "Колтушское сельское поселение, деревня Мяглово, ул. Дорожная, уч. 4Б.";
                worksheet.Cells[num + 12, 2].Style.WrapText = true;
            }

            ExcelRange table = worksheet.Cells[6, 1, num + 7, IsLaser ? 8 : 6];
            if (!IsLaser) table.Style.WrapText = true;
            table.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            table.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            table.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            table.Style.Border.BorderAround(ExcelBorderStyle.Medium);

            foreach (var cell in worksheet.Cells[8, 2, num + 8, 2])
                if (cell.Value != null && $"{cell.Value}".Contains("0,7") || $"{cell.Value}".Contains("0,8"))
                    cell.Style.Numberformat.Format = "0.0";

            worksheet.Cells[num + 8, IsLaser ? 7 : 5].Value = Agent ? "ИТОГО:" : "ИТОГО с НДС:";
            worksheet.Cells[num + 8, IsLaser ? 7 : 5].Style.Font.Bold = true;
            worksheet.Cells[num + 8, IsLaser ? 7 : 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Names.Add("totalOrder", worksheet.Cells[7, IsLaser ? 8 : 6, num + 7, IsLaser ? 8 : 6]);
            worksheet.Cells[num + 8, IsLaser ? 8 : 6].Formula = "=SUM(totalOrder)";
            worksheet.Cells[num + 8, IsLaser ? 8 : 6].Style.Font.Bold = true;
            worksheet.Cells[num + 8, IsLaser ? 8 : 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[num + 8, 1, num + 8, IsLaser ? 8 : 6].Style.Border.BorderAround(ExcelBorderStyle.Medium);
            worksheet.Cells[8, IsLaser ? 7 : 5, num + 8, IsLaser ? 8 : 6].Style.Numberformat.Format = "#,##0.00";
            if (!IsLaser) worksheet.Cells[8, 3, num + 8, 3].Style.Numberformat.Format = "#,##0.00";

            worksheet.Cells[num + 9, 1].Value = "Материал:";
            worksheet.Cells[num + 9, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[num + 9, 2].Value = DetailControls[0].TypeDetailControls[0].HasMetal ? "Исполнителя" : "Заказчика";
            worksheet.Cells[num + 9, 2].Style.Font.Bold = true;
            worksheet.Cells[num + 9, 2, num + 9, 3].Merge = true;

            worksheet.Cells[num + 10, 1].Value = "Срок изготовления:";
            worksheet.Cells[num + 10, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[num + 10, 2].Value = DateProduction.Text + " раб/дней.";
            worksheet.Cells[num + 10, 2].Style.Font.Bold = true;
            worksheet.Cells[num + 10, 2, num + 10, 3].Merge = true;

            worksheet.Cells[num + 11, 1].Value = "Условия оплаты:";
            worksheet.Cells[num + 11, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[num + 11, 2].Value = "Предоплата 100% по счету Исполнителя.";
            worksheet.Cells[num + 11, 2, num + 11, 5].Merge = true;

            worksheet.Cells[num + 12, 1].Value = "Порядок отгрузки:";
            worksheet.Cells[num + 12, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[num + 12, 2].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            worksheet.Cells[num + 12, 2, num + 12, 8].Merge = true;

            worksheet.Cells[num + 14, 1].Value = "Ваш менеджер:";
            worksheet.Cells[num + 14, 2].Value = ManagerDrop.Text;
            worksheet.Cells[num + 14, 2].Style.Font.Bold = true;
            worksheet.Cells[num + 14, 2, num + 14, 4].Merge = true;

            if (IsLaser)
            {
                worksheet.Cells[num + 13, 1].Value = "Расшифровка работ: ";
                worksheet.Cells[num + 13, 2].Value = "";
                foreach (var cell in worksheet.Cells[8, 3, num + 8, 3])
                {
                    if (cell.Value != null && $"{cell.Value}".Contains('Л') && !$"{worksheet.Cells[num + 13, 2].Value}".Contains('Л')) worksheet.Cells[num + 13, 2].Value += "Л - Лазер ";
                    if (cell.Value != null && $"{cell.Value}".Contains('Г') && !$"{worksheet.Cells[num + 13, 2].Value}".Contains('Г')) worksheet.Cells[num + 13, 2].Value += "Г - Гибка ";
                    if (cell.Value != null && $"{cell.Value}".Contains('С') && !$"{worksheet.Cells[num + 13, 2].Value}".Contains('С')) worksheet.Cells[num + 13, 2].Value += "С - Сварка ";
                    if (cell.Value != null && $"{cell.Value}".Contains('О') && !$"{worksheet.Cells[num + 13, 2].Value}".Contains('О')) worksheet.Cells[num + 13, 2].Value += "О - Окраска ";
                }
                worksheet.Cells[num + 13, 2, num + 13, 5].Merge = true;
            }

            worksheet.Cells[num + 14, IsLaser ? 8 : 6].Value = "версия: " + version;
            worksheet.Cells[num + 14, IsLaser ? 8 : 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            worksheet.Cells.AutoFitColumns();
            if (worksheet.Rows[num + 12].Height < 35) worksheet.Rows[num + 12].Height = 35;
            if (IsLaser && worksheet.Columns[5].Width < 15) worksheet.Columns[5].Width = 15;
            if (IsLaser) worksheet.DeleteRow(4, 2);
            else worksheet.DeleteRow(num + 13, 1);

            worksheet.PrinterSettings.FitToPage = true;
            worksheet.PrinterSettings.FitToWidth = 1;
            worksheet.PrinterSettings.FitToHeight = 0;
            worksheet.PrinterSettings.HorizontalCentered = true;

            workbook.SaveAs(path.Remove(path.LastIndexOf(".")) + ".xlsx");

            CreateScore(worksheet, num, path);
        }

        private void CreateScore(ExcelWorksheet worksheet, int num, string _path)
        {
            ExcelRange extable = worksheet.Cells[IsLaser ? 6 : 8, IsLaser ? 5 : 1, IsLaser ? num + 5 : num + 7, IsLaser ? 7 : 5];

            using var workbook = new ExcelPackage();
            ExcelWorksheet scoresheet = workbook.Workbook.Worksheets.Add("Лист1");

            extable.Copy(scoresheet.Cells["A2"]);

            if (!IsLaser) scoresheet.DeleteColumn(2, 2);

            scoresheet.Cells["A1"].Value = "Наименование";
            scoresheet.Cells["B1"].Value = "Кол-во";
            scoresheet.Cells["C1"].Value = "Цена";
            scoresheet.Cells["D1"].Value = "Цена";
            scoresheet.Cells["E1"].Value = "Ед. изм.";

            for (int i = 0; i < num; i++)
            {
                if (float.TryParse($"{scoresheet.Cells[i + 2, 3].Value}", out float p)) scoresheet.Cells[i + 2, 4].Value = Math.Round(p / 1.2f, 2);
                scoresheet.Cells[i + 2, 5].Value = "шт";
                if (float.TryParse($"{scoresheet.Cells[i + 2, 2].Value}", out float f)
                    && float.TryParse($"{scoresheet.Cells[i + 2, 3].Value}", out float c)) scoresheet.Cells[i + 2, 6].Value = Math.Round(f * c, 2);

            }
            scoresheet.Names.Add("totalOrder", scoresheet.Cells[2, 6, num + 1, 6]);
            scoresheet.Cells[num + 2, 6].Formula = "=SUM(totalOrder)";
            scoresheet.Cells[num + 2, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
            scoresheet.Cells[num + 2, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Yellow);

            ExcelRange table = scoresheet.Cells[1, 1, num + 1, 5];
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
            else Environment.Exit(0);       //Application.Current.Shutdown(); - вызывает два диаологовых окна
        }
        private void Exit(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

    }
}
