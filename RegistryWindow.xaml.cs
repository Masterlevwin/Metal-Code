using ExcelDataReader;
using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для RegistryWindow.xaml
    /// </summary>
    public partial class RegistryWindow : Window
    {
        public ObservableCollection<Task> Tasks { get; set; } = new();

        public RegistryWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (((PropertyDescriptor)e.PropertyDescriptor).IsBrowsable == false) e.Cancel = true;   //скрываем свойства с атрибутом [IsBrowsable]
            else e.Column.HeaderTemplate = (DataTemplate)Resources["HeaderTemplateWithIcon"];

            if (e.PropertyName == "Order") e.Column.Header = "№ заказа";
            if (e.PropertyName == "Customer") e.Column.Header = "Заказчик";
            if (e.PropertyName == "Manager") e.Column.Header = "Менеджер";
            if (e.PropertyName == "Mass") e.Column.Header = "Количество материала\n(масса в кг)";
            if (e.PropertyName == "Laser")e.Column.Header = "Лазерные работы\n(заготовка)";
            if (e.PropertyName == "Pipe")e.Column.Header = "Труборез\n(заготовка)";
            if (e.PropertyName == "BendTime")e.Column.Header = "Гибочные работы\n(время в мин)";
            if (e.PropertyName == "CutTime") e.Column.Header = "Время лазерных работ\n(время в мин)";
            if (e.PropertyName == "Production") e.Column.Header = "Производство\n(виды работ)";
            if (e.PropertyName == "Paint") e.Column.Header = "Нанесение покрытий\n(цвет)";
            if (e.PropertyName == "Delivery") e.Column.Header = "Логистика";
            if (e.PropertyName == "Comment") e.Column.Header = "Комментарий";
            if (e.PropertyName == "EndDate") e.Column.Header = "Дата сдачи";
            if (e.PropertyName == "MillingTime") e.Column.Header = "Время фрезерных работ\n(время в мин)";
        }

        private void Load_Registry(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new() { Filter = "Excel (*.xlsx)|*.xlsx|All files (*.*)|*.*" };
            
            if (openFileDialog.ShowDialog() == true) Load_Registry(openFileDialog.FileNames[0]);
            else MainWindow.M.StatusBegin($"Не выбрано ни одного файла");
        }

        private void Load_Registry(string path)
        {
            Tasks.Clear();

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            try
            {
                //преобразуем открытый Excel-файл в DataTable для парсинга
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTable table = result.Tables[0];

                //перебираем строки таблицы и заполняем список объектами Task
                for (int i = 1; i < table.Rows.Count; i++)
                {
                    Task task = new()
                    {
                        Order = $"{table.Rows[i].ItemArray[6]}",
                        Customer = $"{table.Rows[i].ItemArray[7]}",
                        Manager = $"{table.Rows[i].ItemArray[8]}",
                        Mass = $"{table.Rows[i].ItemArray[9]}",
                        Laser = $"{table.Rows[i].ItemArray[10]}",
                        Pipe = $"{table.Rows[i].ItemArray[11]}",
                        BendTime = $"{table.Rows[i].ItemArray[12]}",
                        CutTime = $"{table.Rows[i].ItemArray[13]}",
                        Production = $"{table.Rows[i].ItemArray[14]}",
                        Paint = $"{table.Rows[i].ItemArray[15]}",
                        Delivery = $"{table.Rows[i].ItemArray[16]}",
                        Comment = $"{table.Rows[i].ItemArray[17]}",
                        EndDate = $"{table.Rows[i].ItemArray[18]}",
                        MillingTime = $"{table.Rows[i].ItemArray[19]}"
                    };

                    Tasks.Add(task);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void Save_Registry(object sender, RoutedEventArgs e)
        {
            if (Tasks.Count == 0)
            {
                MessageBox.Show("Невозможно сохранить список задач, так как ни одной задачи не создано!");
                return;
            }

            SaveFileDialog saveFileDialog = new() { FileName = $"Список задач" };

            if (saveFileDialog.ShowDialog() == true && saveFileDialog.FileName != null)
            {
                Create_Registry(saveFileDialog.FileName);
                MainWindow.M.StatusBegin($"Создан список задач по пути \"{saveFileDialog.FileName}\"");
            }
            else MainWindow.M.StatusBegin($"Список задач не создан");
        }

        private void Create_Registry(string path)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage();

            ExcelWorksheet registrysheet = workbook.Workbook.Worksheets.Add("Реестр");

            foreach (Task task in Tasks) task.Manager = MainWindow.M.ShortManager();

            DataTable taskTable = MainWindow.ToDataTable(Tasks);
            registrysheet.Cells[2, 7].LoadFromDataTable(taskTable, false);

            List<string> headers = new()
            {
                "№ заказа", "Заказчик", "Менеджер", "Количество материала", "Лазерные работы", "Труборез",
                "Гибочные работы", "Время лазерных работ", "Производство", "Нанесение покрытий",
                "Логистика", "Комментарий", "Дата сдачи", "Время фрезерных работ"
            };
            for (int col = 0; col < headers.Count; col++) registrysheet.Cells[1, col + 7].Value = headers[col];

            ExcelRange registryRange = registrysheet.Cells[1, 7, taskTable.Rows.Count + 1, 20];
            registryRange.Style.Fill.SetBackground(System.Drawing.Color.LavenderBlush);
            registrysheet.Row(1).Style.Font.Bold = true;

            //обводка границ и авторастягивание столбцов
            registryRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            registryRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            registryRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            registryRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            registryRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
            registrysheet.Cells.AutoFitColumns();

            if (File.Exists(path)) workbook.SaveAs(Path.GetFileNameWithoutExtension(path) + ".xlsx");
            else workbook.SaveAs(path + ".xlsx");
        }

        private void ShowPopup_HeaderDataGrid(object sender, MouseEventArgs e)
        {
            Popup.IsOpen = true;

            if (sender is Image image && image.DataContext is string propertyName)
            {
                if (propertyName.Contains("Количество материала"))
                    Details.Text = $"Укажите массу всех заготовок этого типа в кг.";
                else if (propertyName.Contains("Лазерные работы"))
                    Details.Text = $"Укажите заготовку в формате [толщина материал],\n" +
                        $"например, \"s2 aisi304\", или \"al1.5 амг5\".";
                else if (propertyName.Contains("Труборез"))
                    Details.Text = $"Укажите заготовку в формате [тип материал],\n" +
                        $"например, \"(ТР) Труба профильная 60х30х2\",\n" +
                        $"или \"(ТР) Уголок равнополочный 40х40х2 амг2\".\n" +
                        $"Для лентопила \"(ЛП) Труба круглая 30х30х1.5\".";
                else if (propertyName.Contains("Гибочные работы"))
                    Details.Text = $"Оставляйте ячейку \"Гибочные работы\" пустой,\n" +
                        "если в этой заготовке гибка отсутствует.\n" +
                        "Случайный пробел вызовет создание задачи на гибку!";
                else if (propertyName.Contains("Производство"))
                    Details.Text = $"Перечислите требуемые работы по порядку.";
                else if (propertyName.Contains("Нанесение покрытий"))
                    Details.Text = $"Укажите цвет и структуру окраски\n" +
                        $"или \"БП\", если окраска не нужна.";
            }
        }
    }

    public class Task: INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string order = string.Empty;
        [Browsable(false)]
        public string Order
        {
            get => order;
            set
            {
                if (value != order)
                {
                    order = value;
                    OnPropertyChanged(nameof(Order));
                }
            }
        }

        private string customer = string.Empty;
        [Browsable(false)]
        public string Customer
        {
            get => customer;
            set
            {
                if (value != customer)
                {
                    customer = value;
                    OnPropertyChanged(nameof(Customer));
                }
            }
        }

        private string manager = string.Empty;
        [Browsable(false)]
        public string Manager
        {
            get => manager;
            set
            {
                if (value != manager)
                {
                    manager = value;
                    OnPropertyChanged(nameof(Manager));
                }
            }
        }

        private string mass = string.Empty;
        public string Mass
        {
            get => mass;
            set
            {
                if (value != mass)
                {
                    mass = value;
                    OnPropertyChanged(nameof(Mass));
                }
            }
        }

        private string laser = string.Empty;
        public string Laser
        {
            get => laser;
            set
            {
                if (value != laser)
                {
                    laser = value;
                    OnPropertyChanged(nameof(Laser));
                }
            }
        }

        private string pipe = string.Empty;
        public string Pipe
        {
            get => pipe;
            set
            {
                if (value != pipe)
                {
                    pipe = value;
                    OnPropertyChanged(nameof(Pipe));
                }
            }
        }

        private string bendTime = string.Empty;
        public string BendTime
        {
            get => bendTime;
            set
            {
                if (value != bendTime)
                {
                    bendTime = value;
                    OnPropertyChanged(nameof(BendTime));
                }
            }
        }

        private string cutTime = string.Empty;
        [Browsable(false)]
        public string CutTime
        {
            get => cutTime;
            set
            {
                if (value != cutTime)
                {
                    cutTime = value;
                    OnPropertyChanged(nameof(CutTime));
                }
            }
        }

        private string production = string.Empty;
        public string Production
        {
            get => production;
            set
            {
                if (value != production)
                {
                    production = value;
                    OnPropertyChanged(nameof(Production));
                }
            }
        }

        private string paint = string.Empty;
        public string Paint
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

        private string delivery = string.Empty;
        [Browsable(false)]
        public string Delivery
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

        private string comment = string.Empty;
        [Browsable(false)]
        public string Comment
        {
            get => comment;
            set
            {
                if (value != comment)
                {
                    comment = value;
                    OnPropertyChanged(nameof(Comment));
                }
            }
        }

        private string endDate = string.Empty;
        [Browsable(false)]
        public string EndDate
        {
            get => endDate;
            set
            {
                if (value != endDate)
                {
                    endDate = value;
                    OnPropertyChanged(nameof(EndDate));
                }
            }
        }

        private string millingTime = string.Empty;
        [Browsable(false)]
        public string MillingTime
        {
            get => millingTime;
            set
            {
                if (value != millingTime)
                {
                    millingTime = value;
                    OnPropertyChanged(nameof(MillingTime));
                }
            }
        }

        public Task() { }
    }
}
