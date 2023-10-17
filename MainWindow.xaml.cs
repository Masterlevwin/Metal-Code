﻿using Microsoft.EntityFrameworkCore;
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
using static System.Windows.Forms.AxHost;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace Metal_Code
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow M = new();
        public readonly string version = "1.0.0";
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

        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(ProductCount.Text, out int c)) SetCount(c);
        }
        private void SetCount(int _count)
        {
            Count = _count;
            ProductCount.Text = $"{Count}";
            if (Count > 0) TotalResult();
        }
        public void TotalResult()
        {
            Price = 0;
            foreach (DetailControl d in DetailControls) Price += d.Price;
            Total.Text = $"{Price * Count}";

            DetailsGrid.ItemsSource = SaveDetails();
            DetailsGrid.Columns[0].Header = "N";
            DetailsGrid.Columns[1].Header = "Наименование";
            DetailsGrid.Columns[2].Header = "Кол-во, шт";
            DetailsGrid.Columns[3].Header = "Цена, руб";
            DetailsGrid.Columns[4].Header = "Стоимость";
            DetailsGrid.Columns[5].Header = "Работы";
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
                Count = Count,
                HasDelivery = (bool)CheckDelivery.IsChecked
            };
            if (int.TryParse(Delivery.Text, out int d)) prod.Delivery = d;

            prod.Details = SaveDetails();
            return prod;
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
            return details;
        }

        public void LoadProduct()
        {
            ProductName.Text = DetailsModel.product.Name;
            Order.Text = DetailsModel.product.Order;
            Company.Text = DetailsModel.product.Company;
            DateProduction.Text = DetailsModel.product.Production;
            ManagerDrop.Text = DetailsModel.product.Manager;
            SetCount(DetailsModel.product.Count);
            CheckDelivery.IsChecked = DetailsModel.product.HasDelivery;
            Delivery.Text = $"{DetailsModel.product.Delivery}";

            LoadDetails(DetailsModel.product.Details);
        }
        public void LoadDetails(ObservableCollection<Detail> details)
        {
            //ClearDetails();
            for (int i = 0; i < details.Count; i++)
            {
                AddDetail();
                DetailControls[i].NameDetail = details[i].Title;
                DetailControls[i].Count = details[i].Count;

                for (int j = 0; j < details[i].TypeDetails.Count; j++)
                {
                    DetailControls[i].TypeDetailControls[j].TypeDetailDrop.SelectedIndex = details[i].TypeDetails[j].Index;
                    DetailControls[i].TypeDetailControls[j].Count = details[i].TypeDetails[j].Count;

                    for (int k = 0; k < details[i].TypeDetails[j].Works.Count; k++)
                    {
                        DetailControls[i].TypeDetailControls[j].WorkControls[k].WorkDrop.SelectedIndex = details[i].TypeDetails[j].Works[k].Index;
                        if (DetailControls[i].TypeDetailControls[j].WorkControls.Count < details[i].TypeDetails[j].Works.Count)
                            DetailControls[i].TypeDetailControls[j].AddWork();
                    }
                    if (DetailControls[i].TypeDetailControls.Count < details[i].TypeDetails.Count)
                        DetailControls[i].AddTypeDetail();
                }
            }
            DetailsGrid.ItemsSource = details;
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

            int num = dt.Rows.Count;

            if (CheckDelivery.IsChecked == true)
            {
                worksheet.Cells[num + 5, 1].Value = "Доставка";
                worksheet.Cells[num + 5, 2].Value = 1;
                if (int.TryParse(Delivery.Text, out int d)) worksheet.Cells[num + 5, 3].Value = worksheet.Cells[num + 5, 4].Value = d;
                num++;
                worksheet.Cells[num + 9, 2].Value = "Доставка силами Исполнителя по адресу Заказчика.";
            }
            else
            {
                worksheet.Cells.GetSubrangeAbsolute(num + 9, 2, num + 9, 5).Merged = true;
                worksheet.Cells[num + 9, 2].Style.WrapText = true;
                worksheet.Cells[num + 9, 2].Value = "Самовывоз со склада Исполнителя по адресу: Ленинградская область, Всеволожский район, Колтушское сельское поселение, деревня Мяглово, ул. Дорожная, уч. 4Б.";
                worksheet.Rows[num + 9].AutoFit(true);
            }

            CellRange cells = worksheet.Cells.GetSubrangeAbsolute(3, 0, num + 5, 5);
            cells.AutoFitColumnWidth();
            cells.Style.Borders.SetBorders(MultipleBorders.Inside, SpreadsheetColor.FromName(ColorName.Black), LineStyle.Thin);
            cells.Style.Borders.SetBorders(MultipleBorders.Outside,SpreadsheetColor.FromName(ColorName.Black), LineStyle.Medium);
            cells.Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;
            cells.Style.VerticalAlignment = VerticalAlignmentStyle.Center;

            worksheet.Cells[num + 5, 3].Value = "ИТОГО:";
            worksheet.Cells[num + 5, 3].Style.Font.Weight = ExcelFont.BoldWeight;
            worksheet.Cells[num + 5, 3].Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;
            worksheet.NamedRanges.Add("totalOrder", worksheet.Cells.GetSubrangeAbsolute(5, 4, num + 4, 4));
            worksheet.Cells[num + 5, 4].Formula = "=SUM(totalOrder)";
            worksheet.Cells[num + 5, 4].Style.Font.Weight = ExcelFont.BoldWeight;
            worksheet.Cells[num + 5, 4].Style.HorizontalAlignment = HorizontalAlignmentStyle.Center;
            worksheet.Cells[num + 5, 5].Value = $"{Price * Count} за {Count} изделий";

            worksheet.Cells[num + 7, 0].Value = "Срок изготовления:";
            worksheet.Cells[num + 7, 0].Style.VerticalAlignment = VerticalAlignmentStyle.Top;
            worksheet.Cells[num + 7, 2].Value = DateProduction.Text + " раб/дней.";
            worksheet.Cells[num + 7, 2].Style.Font.Weight = ExcelFont.BoldWeight;
            worksheet.Cells.GetSubrangeAbsolute(num + 7, 2, num + 7, 4).Merged = true;

            worksheet.Cells[num + 8, 0].Value = "Условия оплаты:";
            worksheet.Cells[num + 8, 0].Style.VerticalAlignment = VerticalAlignmentStyle.Top;
            worksheet.Cells[num + 8, 2].Value = "Предоплата 100% по счету Исполнителя.";
            worksheet.Cells.GetSubrangeAbsolute(num + 8, 2, num + 8, 5).Merged = true;

            worksheet.Cells[num + 9, 0].Value = "Порядок отгрузки:";

            worksheet.Cells[num + 10, 0].Value = "Ваш менеджер:";
            worksheet.Cells[num + 10, 2].Value = ManagerDrop.Text;
            worksheet.Cells[num + 10, 2].Style.Font.Weight = ExcelFont.BoldWeight;

            worksheet.Cells.GetSubrangeAbsolute(num + 6, 0, num + 11, 5).Style.VerticalAlignment = VerticalAlignmentStyle.Top;

            worksheet.Cells[num + 10, 5].Value = version;
            worksheet.Cells[num + 10, 5].Style.HorizontalAlignment = HorizontalAlignmentStyle.Right;

            worksheet.PrintOptions.FitWorksheetWidthToPages = 1;

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
