using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Animation;
using OfficeOpenXml.Style;
using OfficeOpenXml;

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

        private List<float> Destinies = new() { .5f, .7f, .8f, 1, 1.2f, 1.5f, 2, 2.5f, 3, 4, 5, 6, 8, 10, 12, 14, 16, 18, 20, 25 };
        public Dictionary<string, Dictionary<float, (float, float)>> MetalDict = new();
        private void CreateMetalDict()
        {
            List<Metal> metals = new(dbMetals.Metals.Local.ToList());

            foreach (Metal metal in metals)
            {
                Dictionary<float, (float, float)> prices = new();

                string[] _ways = metal.WayPrice.Split('/');
                string[] _pinholes = metal.PinholePrice.Split('/');

                for (int i = 0; i < _ways.Length; i++) prices[Destinies[i]] = (Parser(_ways[i]), Parser(_pinholes[i]));

                MetalDict[metal.Name] = prices;
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
            get { return isLaser; }
            set
            {
                isLaser = value;
                OnPropertyChanged(nameof(IsLaser));
            }

        }

        private int count;
        public int Count
        {
            get { return count; }
            set { 
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

        private float result;
        public float Result
        {
            get { return result; }
            set
            {
                result = value;
                OnPropertyChanged(nameof(Result));
            }
        }
        public void TotalResult()
        {
            Result = 0;
            foreach (DetailControl d in DetailControls) Result += d.Price * d.Count;
            Result *= Count;
            Result = (float)Math.Round(Result, 2);
            //AnimBtn();

            ViewDetailsGrid();
        }

        private void IsLaserChanged(object sender, RoutedEventArgs e)
        {
            TotalResult();
        }
        private void ViewDetailsGrid()
        {
            if (IsLaser)
            {
                DetailsGrid.ItemsSource = PartsSource();
                DetailsGrid.Columns[0].Header = "Материал";
                DetailsGrid.Columns[1].Header = "Толщина";
                DetailsGrid.Columns[2].Header = "Работы";
                DetailsGrid.Columns[3].Header = "Точность";
                DetailsGrid.Columns[4].Header = "Наименование";
                DetailsGrid.Columns[5].Header = "Кол-во, шт";
                DetailsGrid.Columns[6].Header = "Цена, руб";
                DetailsGrid.Columns[7].Header = "Стоимость, руб";
                //DetailsGrid.Columns[8].Header = "Периметр детали";
                //DetailsGrid.Columns[9].Header = "Вес детали";
                Parts = PartsSource();
            }
            else
            {
                DetailsGrid.ItemsSource = DetailsSource();
                DetailsGrid.Columns[0].Header = "Наименование";
                DetailsGrid.Columns[1].Header = "Работы";
                DetailsGrid.Columns[2].Header = "N";
                DetailsGrid.Columns[3].Header = "Кол-во, шт";
                DetailsGrid.Columns[4].Header = "Цена, руб";
                DetailsGrid.Columns[5].Header = "Стоимость, руб";
                DetailsGrid.FrozenColumnCount = 1;
            }
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

        private void AnimBtn()
        {
            DoubleAnimation buttonAnimation = new();
            buttonAnimation.From = ResultBtn.ActualWidth;
            buttonAnimation.To = 150;
            buttonAnimation.Duration = TimeSpan.FromSeconds(3);
            buttonAnimation.RepeatBehavior = new RepeatBehavior(TimeSpan.FromSeconds(7));
            buttonAnimation.AutoReverse = true;
            ResultBtn.BeginAnimation(WidthProperty, buttonAnimation);
        }

        /*private void UpdateResult(object sender, RoutedEventArgs e)   // метод принудительного обновления стоимости;
                                                                        // создан для исправления ситуации с запятой в коэффициентах работ
                                                                        //(вроде пропала проблема)
        {
            foreach (DetailControl d in DetailControls)
                foreach (TypeDetailControl t in d.TypeDetailControls) t.OnPriceChanged();
        }*/

        public List<DetailControl> DetailControls = new();
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
            CheckDelivery.IsChecked = false;
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
                Detail _detail = new(i + 1, DetailControls[i].NameDetail, DetailControls[i].Count,
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
                HasDelivery = (bool)CheckDelivery.IsChecked
            };
            if (int.TryParse(Delivery.Text, out int d)) prod.Delivery = d;

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
                Detail _detail = new(i + 1, det.NameDetail, det.Count, det.Price, det.Price * det.Count);
                    
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

                        work.PropertiesChanged?.Invoke(work, true);  
                        _saveWork.PropsList = work.propsList;

                        _typeDetail.Works.Add(_saveWork);
                    }
                    _detail.TypeDetails.Add(_typeDetail);
                }
                details.Add(_detail);
            }

            // добавить механизм сохранения Parts, всех соответствующих контролов и т.д.

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
                        _work.Ratio = details[i].TypeDetails[j].Works[k].Ratio;
                        _work.propsList = details[i].TypeDetails[j].Works[k].PropsList;
                        _work.PropertiesChanged?.Invoke(_work, false);

                        if (_type.WorkControls.Count < details[i].TypeDetails[j].Works.Count) _type.AddWork(); 
                    }
                    if (_det.TypeDetailControls.Count < details[i].TypeDetails.Count) _det.AddTypeDetail();
                }
            }
        }

        public void ExportToExcel(string path)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using (var workbook = new ExcelPackage())
            {
                ExcelWorksheet worksheet = workbook.Workbook.Worksheets.Add("Лист1");

                worksheet.Drawings.AddPicture("A1", IsLaser ? "laser_logo.jpg" : "excel_logo.jpg");  // файлы должны быть в директории bin/Debug...

                worksheet.Cells["A1"].Value = IsLaser ? "ООО ЛАЗЕРФЛЕКС  " + Phone.Text : "ООО ПРОВЭЛД  " + Phone.Text;   // тел:(812)509-60-11 или тел:(812)603-45-33 - по умолчанию
                worksheet.Cells[1, 1, 1, IsLaser ? 8 : 6].Merge = true;
                worksheet.Rows[1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                if (IsLaser)
                {
                    worksheet.Cells["D2"].Value = "КП № " + Order.Text + " для " + Company.Text + " от " + DateTime.Now.ToString("d");
                    worksheet.Cells[2, 4, 2, 8].Merge = true;
                }
                else
                {
                    worksheet.Cells["B2"].Value = "КП № " + Order.Text + " для " + Company.Text + " от " + DateTime.Now.ToString("d");
                    worksheet.Cells[2, 2, 2, 6].Merge = true;
                }
                // здесь нужен стиль заголовка
                worksheet.Rows[2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

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

                DataTable dt = IsLaser ? ToDataTable(Parts) : ToDataTable(DetailsModel.Product.Details);
                worksheet.Cells["A7"].LoadFromDataTable(dt, true);
                int num = dt.Rows.Count;

                for (int col = 0; col < DetailsGrid.Columns.Count; col++)
                {
                    worksheet.Cells[6, col + 1].Value = DetailsGrid.Columns[col].Header;
                    worksheet.Cells[6, col + 1, 7, col + 1].Merge = true;
                    worksheet.Cells[6, col + 1, 7, col + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[6, col + 1, 7, col + 1].Style.Fill.BackgroundColor.SetColor(0, 120, 180, 255);
                    worksheet.Cells[6, col + 1].Style.WrapText = true;
                }

                if (CheckDelivery.IsChecked == true)
                {
                    worksheet.Cells[num + 8, IsLaser ? 5 : 3].Value = "Доставка";
                    worksheet.Cells[num + 8, IsLaser ? 6 : 4].Value = 1;
                    if (int.TryParse(Delivery.Text, out int d)) worksheet.Cells[num + 8, IsLaser ? 7 : 5].Value = worksheet.Cells[num + 8, IsLaser ? 8 : 6].Value = d;
                    num++;
                    worksheet.Cells[num + 12, 2].Value = "Доставка силами Исполнителя по адресу Заказчика.";
                }
                else
                {
                    worksheet.Cells[num + 12, 2].Value = "Самовывоз со склада Исполнителя по адресу: Ленинградская область, Всеволожский район, Колтушское сельское поселение, деревня Мяглово, ул. Дорожная, уч. 4Б.";
                    worksheet.Cells[num + 12, 2].Style.WrapText = true;
                }

                ExcelRange table = worksheet.Cells[6, 1, num + 7, IsLaser ? 8 : 6];
                if (!IsLaser) table.Style.WrapText = true;
                table.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                table.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                table.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                table.Style.Border.BorderAround(ExcelBorderStyle.Medium);

                if (IsLaser) worksheet.Cells[6, 2, num + 7, 2].Style.Numberformat.Format = "0.0";

                worksheet.Cells[num + 8, IsLaser ? 7 : 5].Value = "ИТОГО:";
                worksheet.Cells[num + 8, IsLaser ? 7 : 5].Style.Font.Bold = true;
                worksheet.Cells[num + 8, IsLaser ? 7 : 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Names.Add("totalOrder", worksheet.Cells[7, IsLaser ? 8 : 6, num + 7, IsLaser ? 8 : 6]);
                worksheet.Cells[num + 8, IsLaser ? 8 : 6].Formula = "=SUM(totalOrder)";
                worksheet.Cells[num + 8, IsLaser ? 8 : 6].Style.Font.Bold = true;
                worksheet.Cells[num + 8, IsLaser ? 8 : 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[num + 8, 1, num + 8, IsLaser ? 8 : 6].Style.Border.BorderAround(ExcelBorderStyle.Medium);

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
                worksheet.Cells[num + 12, 2, num + 12, 5].Merge = true;

                worksheet.Cells[num + 14, 1].Value = "Ваш менеджер:";
                worksheet.Cells[num + 14, 2].Value = ManagerDrop.Text;
                worksheet.Cells[num + 14, 2].Style.Font.Bold = true;
                worksheet.Cells[num + 14, 2, num + 14, 3].Merge = true;


                worksheet.Cells[num + 13, 1].Value = "Расшифровка работ: ";
                worksheet.Cells[num + 13, 2].Value = "";
                foreach (var cell in worksheet.Cells[8, 3, num + 8, 3])
                {
                    if (cell.Value != null && $"{cell.Value}".Contains('Л') && !$"{worksheet.Cells[num + 13, 2].Value}".Contains('Л')) worksheet.Cells[num + 13, 2].Value += "Л - Лазер";
                }
                worksheet.Cells[num + 13, 2, num + 13, 5].Merge = true;

                worksheet.Cells[num + 14, IsLaser ? 8 : 6].Value = version;
                worksheet.Cells[num + 14, IsLaser ? 8 : 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                worksheet.Cells.AutoFitColumns();
                if (worksheet.Rows[num + 12].Height < 35) worksheet.Rows[num + 12].Height = 35;

                workbook.SaveAs(path.Remove(path.LastIndexOf(".")) + ".xlsx");
            }
        }

        /*private byte Commerce(ExcelWorksheet worksheet)
        {
            List<LaserBlock> lasers = Data.D.lasers.OrderBy(p => p.destinyDrop.Text).ToList();

            byte k = 0;

            for (int i = 0; i < lasers.Count; i++)
            {
                if (lasers[i].details.Count > 0)
                {
                    for (int j = 0; j < lasers[i].details.Count; j++)
                    {
                        worksheet.Cells[k + j + 5, 1].Value = lasers[i].metalDrop.Text;
                        worksheet.Cells[k + j + 5, 2].Value = lasers[i].destinyDrop.Text;
                        worksheet.Cells[k + j + 5, 3].Value = "Л";
                        if (lasers[i].details[j].det.hasBend) worksheet.Cells[k + j + 5, 3].Value += " + Г";
                        if (lasers[i].details[j].det.hasRoll) worksheet.Cells[k + j + 5, 3].Value += " + В";
                        if (lasers[i].details[j].det.hasWeld) worksheet.Cells[k + j + 5, 3].Value += " + С";
                        if (lasers[i].details[j].det.hasPaint) worksheet.Cells[k + j + 5, 3].Value += " + О(" + $" RAL {lasers[i].details[j].det.ral}" + " )";
                        worksheet.Cells[k + j + 5, 4].Value = "H14/h14 +-IT 14/2";
                        worksheet.Cells[k + j + 5, 5].Value = lasers[i].details[j].det.name.Substring(0, lasers[i].details[j].det.name.LastIndexOf(' ') + 1);
                        worksheet.Cells[k + j + 5, 6].Value = lasers[i].details[j].det.parts;
                        worksheet.Cells[k + j + 5, 7].Value = Math.Round(lasers[i].details[j].price, 2);
                        worksheet.Cells[k + j + 5, 8].Value = Math.Round(lasers[i].details[j].det.parts * lasers[i].details[j].price, 2);
                    }

                    k += (byte)lasers[i].details.Count;
                }
            }

            return k;
        }*/

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
    }
}
