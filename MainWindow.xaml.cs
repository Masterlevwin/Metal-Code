using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using GemBox.Spreadsheet;
using System.Data;
using System.Reflection;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private int count;
        public int Count
        {
            get { return count; }
            set { 
                count = value;
                OnPropertyChanged("Count");
            }
        }
        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int c)) SetCount(c);
        }
        private void SetCount(int _count)
        {
            Count = _count;
            CountText.Text = $"{Count}";
            TotalResult();
        }

        public float Result { get; set; }

        public void TotalResult()
        {
            Result = 0;
            foreach (DetailControl d in DetailControls) Result += d.Price * d.Count;
            Total.Text = $"{Result * Count}";

            DetailsGrid.ItemsSource = SourceDetails();
            DetailsGrid.Columns[0].Header = "N";
            DetailsGrid.Columns[1].Header = "Наименование";
            DetailsGrid.Columns[2].Header = "Кол-во, шт";
            DetailsGrid.Columns[3].Header = "Цена, руб";
            DetailsGrid.Columns[4].Header = "Стоимость";
            DetailsGrid.Columns[5].Header = "Работы";
        }

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

        private ObservableCollection<Detail> SourceDetails()
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
                            if (work.Name == "Покупка" && DetailControls[i].TypeDetailControls[j].TypeDetailDrop.SelectedItem is TypeDetail type)
                                _detail.Description += $"{work.Name} ({type.Name})\n";
                            else if (work.Name == "Окраска" && DetailControls[i].TypeDetailControls[j].WorkControls[k].workType is PaintControl _paint)
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
                Detail _detail = new(i + 1, DetailControls[i].NameDetail, DetailControls[i].Count,
                    DetailControls[i].Price, DetailControls[i].Price * DetailControls[i].Count);
                for (int j = 0; j < DetailControls[i].TypeDetailControls.Count; j++)
                {
                    SaveTypeDetail _typeDetail = new(DetailControls[i].TypeDetailControls[j].TypeDetailDrop.SelectedIndex,
                        DetailControls[i].TypeDetailControls[j].Count, DetailControls[i].TypeDetailControls[j].MetalDrop.SelectedIndex);
                    for (int k = 0; k < DetailControls[i].TypeDetailControls[j].WorkControls.Count; k++)
                    {
                        SaveWork _saveWork = new(DetailControls[i].TypeDetailControls[j].WorkControls[k].WorkDrop.SelectedIndex);
                        if (DetailControls[i].TypeDetailControls[j].WorkControls[k].WorkDrop.SelectedItem is Work work)
                        {
                            if (work.Name == "Покупка" && DetailControls[i].TypeDetailControls[j].TypeDetailDrop.SelectedItem is TypeDetail type)
                                _detail.Description += $"{work.Name} ({type.Name})\n";
                            else if (work.Name == "Окраска" && DetailControls[i].TypeDetailControls[j].WorkControls[k].workType is PaintControl _paint)
                                _detail.Description += $"{work.Name} (цвет - {_paint.Ral})\n";
                            else _detail.Description += work.Name + "\n";
                        }

                        DetailControls[i].TypeDetailControls[j].WorkControls[k].PropertiesChanged?.Invoke(
                            DetailControls[i].TypeDetailControls[j].WorkControls[k], true);
                        _saveWork.PropsList = DetailControls[i].TypeDetailControls[j].WorkControls[k].propsList;

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
                    _type.SetMetal(details[i].TypeDetails[j].Metal);

                    for (int k = 0; k < details[i].TypeDetails[j].Works.Count; k++)
                    {
                        WorkControl _work = DetailControls[i].TypeDetailControls[j].WorkControls[k];
                        _work.WorkDrop.SelectedIndex = details[i].TypeDetails[j].Works[k].Index;

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
            SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY");

            ExcelFile workbook = new();
            ExcelWorksheet worksheet = workbook.Worksheets.Add("Лист1");

            worksheet.Pictures.Add("excel_logo.jpg", "A1");

            worksheet.Cells["E1"].Value = "ООО ПРОВЭЛД  " + Phone.Text;
            worksheet.Rows["1"].Style = workbook.Styles[BuiltInCellStyleName.Heading3];
            worksheet.Rows["1"].Style.HorizontalAlignment = HorizontalAlignmentStyle.Right;
            worksheet.Cells[1, 5].Value = "КП № " + Order.Text + " для " + Company.Text + " от " + DateTime.Now.ToString("d");
            worksheet.Rows["2"].Style = workbook.Styles[BuiltInCellStyleName.Heading1];
            worksheet.Rows["2"].Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;
            worksheet.Cells["A3"].Value = "Данный расчет действителен в течении 2-х банковских дней";
            //worksheet.Cells["A3"].Style.Font.Size = 10;
            worksheet.Cells["B4"].Value = $"Для изготовления изделия";
            worksheet.Cells["D4"].Value = ProductName.Text;
            worksheet.Cells["D4"].Style.Font.Weight = ExcelFont.BoldWeight;
            worksheet.Cells["B5"].Value = "понадобятся следующие детали и работы:";

            worksheet.Cells.GetSubrange("C1:F1").Merged = true;
            worksheet.Cells.GetSubrange("B2:F2").Merged = true;
            worksheet.Cells.GetSubrange("A3:F3").Merged = true;
            worksheet.Cells.GetSubrange("B4:C4").Merged = true;
            worksheet.Cells.GetSubrange("B5:E5").Merged = true;
            worksheet.Cells.GetSubrange("A6:A7").Merged = true;
            worksheet.Cells.GetSubrange("B6:B7").Merged = true;
            worksheet.Cells.GetSubrange("C6:C7").Merged = true;
            worksheet.Cells.GetSubrange("D6:D7").Merged = true;
            worksheet.Cells.GetSubrange("E6:E7").Merged = true;
            worksheet.Cells.GetSubrange("F6:F7").Merged = true;

            CellStyle style = new()
            {
                HorizontalAlignment = HorizontalAlignmentStyle.Center,
                VerticalAlignment = VerticalAlignmentStyle.Center,
                WrapText = true
            };
            style.FillPattern.SetSolid(SpreadsheetColor.FromArgb(255, 180, 100));
            style.Borders.SetBorders(MultipleBorders.Inside, SpreadsheetColor.FromName(ColorName.Black), LineStyle.Thin);
            style.Borders.SetBorders(MultipleBorders.Outside, SpreadsheetColor.FromName(ColorName.Black), LineStyle.Thin);
            worksheet.Cells.GetSubrange("A6:F7").Style = style;

            DataTable dt = ToDataTable(DetailsModel.Product.Details);
            worksheet.InsertDataTable(dt, new InsertDataTableOptions()
                {
                    ColumnHeaders = true,
                    StartRow = 6
                });
            for (int col = 0; col < DetailsGrid.Columns.Count; col++) worksheet.Cells[6, col].Value = DetailsGrid.Columns[col].Header;

            int num = dt.Rows.Count;

            if (CheckDelivery.IsChecked == true)
            {
                worksheet.Cells[num + 7, 1].Value = "Доставка";
                worksheet.Cells[num + 7, 2].Value = 1;
                if (int.TryParse(Delivery.Text, out int d)) worksheet.Cells[num + 7, 3].Value = worksheet.Cells[num + 7, 4].Value = d;
                num++;
                worksheet.Cells[num + 11, 2].Value = "Доставка силами Исполнителя по адресу Заказчика.";
            }
            else
            {
                worksheet.Cells.GetSubrangeAbsolute(num + 11, 2, num + 11, 5).Merged = true;
                worksheet.Cells[num + 11, 2].Style.WrapText = true;
                worksheet.Cells[num + 11, 2].Value = "Самовывоз со склада Исполнителя по адресу: Ленинградская область, Всеволожский район, Колтушское сельское поселение, деревня Мяглово, ул. Дорожная, уч. 4Б.";
                worksheet.Rows[num + 11].AutoFit(true);
            }

            worksheet.Cells.GetSubrangeAbsolute(5, 3, num + 7, 4).Style.NumberFormat = $"00.00";

            CellRange cells = worksheet.Cells.GetSubrangeAbsolute(5, 0, num + 7, 5);
            cells.AutoFitColumnWidth();
            cells.Style.Borders.SetBorders(MultipleBorders.Inside, SpreadsheetColor.FromName(ColorName.Black), LineStyle.Thin);
            cells.Style.Borders.SetBorders(MultipleBorders.Outside,SpreadsheetColor.FromName(ColorName.Black), LineStyle.Medium);
            cells.Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;
            cells.Style.VerticalAlignment = VerticalAlignmentStyle.Center;

            worksheet.Cells[num + 7, 3].Value = "Стоимость изделия:";
            worksheet.Cells[num + 7, 3].Style.Font.Weight = ExcelFont.BoldWeight;
            worksheet.Cells[num + 7, 3].Style.HorizontalAlignment = HorizontalAlignmentStyle.Right;
            worksheet.NamedRanges.Add("totalOrder", worksheet.Cells.GetSubrangeAbsolute(7, 4, num + 6, 4));
            worksheet.Cells[num + 7, 4].Formula = "=SUM(totalOrder)";
            worksheet.Cells[num + 7, 4].Style.Font.Weight = ExcelFont.BoldWeight;
            worksheet.Cells[num + 7, 4].Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;

            worksheet.Cells[num + 8, 3].Value = $"Всего за {Count} шт:";
            worksheet.Cells[num + 8, 3].Style.Font.Weight = ExcelFont.BoldWeight;
            worksheet.Cells[num + 8, 3].Style.HorizontalAlignment = HorizontalAlignmentStyle.Right;
            worksheet.Cells[num + 8, 4].Value = $"{Result * Count + Parser(Delivery.Text)}";
            worksheet.Cells[num + 8, 4].Style.Font.Weight = ExcelFont.BoldWeight;
            worksheet.Cells[num + 8, 4].Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;

            worksheet.Cells[num + 9, 0].Value = "Срок изготовления:";
            worksheet.Cells[num + 9, 0].Style.VerticalAlignment = VerticalAlignmentStyle.Top;
            worksheet.Cells[num + 9, 2].Value = DateProduction.Text + " раб/дней.";
            worksheet.Cells[num + 9, 2].Style.Font.Weight = ExcelFont.BoldWeight;
            worksheet.Cells.GetSubrangeAbsolute(num + 9, 2, num + 9, 4).Merged = true;

            worksheet.Cells[num + 10, 0].Value = "Условия оплаты:";
            worksheet.Cells[num + 10, 0].Style.VerticalAlignment = VerticalAlignmentStyle.Top;
            worksheet.Cells[num + 10, 2].Value = "Предоплата 100% по счету Исполнителя.";
            worksheet.Cells.GetSubrangeAbsolute(num + 10, 2, num + 10, 5).Merged = true;

            worksheet.Cells[num + 11, 0].Value = "Порядок отгрузки:";

            worksheet.Cells[num + 12, 0].Value = "Ваш менеджер:";
            worksheet.Cells[num + 12, 2].Value = ManagerDrop.Text;
            worksheet.Cells[num + 12, 2].Style.Font.Weight = ExcelFont.BoldWeight;

            worksheet.Cells.GetSubrangeAbsolute(num + 8, 0, num + 13, 5).Style.VerticalAlignment = VerticalAlignmentStyle.Top;

            worksheet.Cells[num + 12, 5].Value = version;
            worksheet.Cells[num + 12, 5].Style.HorizontalAlignment = HorizontalAlignmentStyle.Right;

            worksheet.PrintOptions.FitWorksheetWidthToPages = 1;

            workbook.Save(path.Remove(path.LastIndexOf(".")) + ".xlsx");
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
    }
}
