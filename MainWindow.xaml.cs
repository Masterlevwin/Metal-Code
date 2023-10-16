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
using System.Globalization;
using Org.BouncyCastle.Bcpg;

namespace Metal_Code
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow M = new();

        public readonly TypeDetailContext dbTypeDetails = new();
        public readonly WorkContext dbWorks = new();
        public readonly ApplicationViewModel DetailsModel = new(new DefaultDialogService(), new JsonFileService());

        public MainWindow()
        {
            InitializeComponent();
            M = this;
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
        }

        public string? Product { get; set; }
        public int Count { get; set; }
        public float Price { get; set; }

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

        private void ClearDetails()
        {
            if (DetailControls.Count > 0) foreach (DetailControl d in DetailControls) d.Remove();
            DetailControls.Clear();
        }

        private void SetDate(object sender, SelectionChangedEventArgs e)
        {
            DateTime? date = datePicker.SelectedDate;
            TimeSpan duration = date.Value - DateTime.Now;
            MessageBox.Show(duration.TotalDays.ToString());
        }

        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int c)) Count = c;
            if (Count > 0) TotalResult();
        }

        public void TotalResult()
        {
            Price = 0;
            foreach (DetailControl d in DetailControls) Price += d.Price;
            Total.Text = $"{Price * Count}";
            SaveDetails();
        }

        public ObservableCollection<Detail> SaveDetails()
        {
            ObservableCollection<Detail> details = new();
            for (int i = 0; i < DetailControls.Count; i++)
            {
                Detail detail = new(i + 1, DetailControls[i].NameDetail, DetailControls[i].Count, DetailControls[i].Price / DetailControls[i].Count, DetailControls[i].Price);
                for (int j = 0; j < DetailControls[i].TypeDetailControls.Count; j++)
                {
                    SaveTypeDetail typeDetail = new(DetailControls[i].TypeDetailControls[j].TypeDetailDrop.SelectedIndex, DetailControls[i].TypeDetailControls[j].Count);
                    for (int k = 0; k < DetailControls[i].TypeDetailControls[j].WorkControls.Count; k++)
                    {
                        SaveWork saveWork = new(DetailControls[i].TypeDetailControls[j].WorkControls[k].WorkDrop.SelectedIndex);
                        typeDetail.Works.Add(saveWork);

                        if (DetailControls[i].TypeDetailControls[j].WorkControls[k].WorkDrop.SelectedItem is Work work)
                        {
                            if (work.Name == "Покупка" && DetailControls[i].TypeDetailControls[j].TypeDetailDrop.SelectedItem is TypeDetail type)
                                detail.Description += $"{work.Name} ({type.Name})\n";
                            else detail.Description += work.Name + "\n";
                        }
                    }
                    detail.TypeDetails.Add(typeDetail);
                }
                details.Add(detail);
            }

            DetailsGrid.ItemsSource = details;
            DetailsGrid.Columns[0].Header = "N";
            DetailsGrid.Columns[1].Header = "Наименование";
            DetailsGrid.Columns[2].Header = "Кол-во, шт";
            DetailsGrid.Columns[3].Header = "Цена, руб";
            DetailsGrid.Columns[4].Header = "Стоимость";
            DetailsGrid.Columns[5].Header = "Работы";
            return details;
        }

        public void LoadDetails()
        {
            //ClearDetails();
            for (int i = 0; i < DetailsModel.Details.Count; i++)
            {
                AddDetail();
                DetailControls[i].NameDetail = DetailsModel.Details[i].Title;
                DetailControls[i].Count = DetailsModel.Details[i].Count;

                for (int j = 0; j < DetailsModel.Details[i].TypeDetails.Count; j++)
                {
                    DetailControls[i].TypeDetailControls[j].TypeDetailDrop.SelectedIndex = DetailsModel.Details[i].TypeDetails[j].Index;
                    DetailControls[i].TypeDetailControls[j].Count = DetailsModel.Details[i].TypeDetails[j].Count;

                    for (int k = 0; k < DetailsModel.Details[i].TypeDetails[j].Works.Count; k++)
                    {
                        DetailControls[i].TypeDetailControls[j].WorkControls[k].WorkDrop.SelectedIndex = DetailsModel.Details[i].TypeDetails[j].Works[k].Index;
                        if (DetailControls[i].TypeDetailControls[j].WorkControls.Count < DetailsModel.Details[i].TypeDetails[j].Works.Count)
                            DetailControls[i].TypeDetailControls[j].AddWork();
                    }
                    if (DetailControls[i].TypeDetailControls.Count < DetailsModel.Details[i].TypeDetails.Count)
                        DetailControls[i].AddTypeDetail();
                }
            }
            DetailsGrid.ItemsSource = DetailsModel.Details;
        }


        public void ExportToExcel(string path)
        {
            SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY");

            ExcelFile workbook = new();
            ExcelWorksheet worksheet = workbook.Worksheets.Add("Лист1");

            worksheet.Pictures.Add("excel_logo.jpg", "A1");

            worksheet.Cells["E1"].Value = "ООО Провэлд " + Phone.Text;
            worksheet.Rows["1"].Style = workbook.Styles[BuiltInCellStyleName.Heading3];
            worksheet.Rows["1"].Style.HorizontalAlignment = HorizontalAlignmentStyle.Right;
            worksheet.Cells[1, 4].Value = "КП № " + Order.Text + " для " + Company.Text + " от " + DateTime.Now.ToString("d");
            worksheet.Rows["2"].Style = workbook.Styles[BuiltInCellStyleName.Heading2];
            worksheet.Rows["2"].Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;
            worksheet.Cells["A3"].Value = "Данный расчет действителен в течении 2-х банковских дней";

            worksheet.Cells.GetSubrange("C1:F1").Merged = true;
            worksheet.Cells.GetSubrange("B2:F2").Merged = true;
            worksheet.Cells.GetSubrange("A3:F3").Merged = true;
            worksheet.Cells.GetSubrange("A4:A5").Merged = true;
            worksheet.Cells.GetSubrange("B4:B5").Merged = true;
            worksheet.Cells.GetSubrange("C4:C5").Merged = true;
            worksheet.Cells.GetSubrange("D4:D5").Merged = true;
            worksheet.Cells.GetSubrange("E4:E5").Merged = true;
            worksheet.Cells.GetSubrange("F4:F5").Merged = true;

            CellStyle style = new()
            {
                HorizontalAlignment = HorizontalAlignmentStyle.Center,
                VerticalAlignment = VerticalAlignmentStyle.Center,
                WrapText = true
            };
            style.FillPattern.SetSolid(SpreadsheetColor.FromArgb(255, 180, 100));
            style.Borders.SetBorders(MultipleBorders.Inside, SpreadsheetColor.FromName(ColorName.Black), LineStyle.Thin);
            style.Borders.SetBorders(MultipleBorders.Outside, SpreadsheetColor.FromName(ColorName.Black), LineStyle.Thin);
            worksheet.Cells.GetSubrange("A4:F5").Style = style;

            DataTable dt = ToDataTable(SaveDetails());
            worksheet.InsertDataTable(dt, new InsertDataTableOptions()
                {
                    ColumnHeaders = true,
                    StartRow = 4
                });
            for (int col = 0; col < DetailsGrid.Columns.Count; col++) worksheet.Cells[4, col].Value = DetailsGrid.Columns[col].Header;

            worksheet.Cells.GetSubrangeAbsolute(4, 0, dt.Rows.Count + 5, 5).AutoFitColumnWidth();

            CellRange cells = worksheet.Cells.GetSubrangeAbsolute(4, 0, dt.Rows.Count + 4, 5);
            cells.Style.Borders.SetBorders(MultipleBorders.Inside, SpreadsheetColor.FromName(ColorName.Black), LineStyle.Thin);
            cells.Style.Borders.SetBorders(MultipleBorders.Outside, SpreadsheetColor.FromName(ColorName.Black), LineStyle.Thin);
            cells.Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;
            cells.Style.VerticalAlignment = VerticalAlignmentStyle.Center;

            workbook.Save(path.Remove(path.LastIndexOf(".")) + ".xlsx");
        }

        public DataTable ToDataTable<T>(ObservableCollection<T> items)
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

    }
}
