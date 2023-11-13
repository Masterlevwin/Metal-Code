using ExcelDataReader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для CutControl.xaml
    /// </summary>
    public partial class CutControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float way;
        public float Way
        {
            get => way;
            set
            {
                if (value != way)
                {
                    way = value;
                    OnPropertyChanged(nameof(Way));
                }
            }
        }

        private int pinhole;
        public int Pinhole
        {
            get => pinhole;
            set
            {
                if (value != pinhole)
                {
                    pinhole = value;
                    OnPropertyChanged(nameof(Pinhole));
                }
            }
        }

        private float mass;
        public float Mass
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

        private float massTotal;
        public float MassTotal
        {
            get => massTotal;
            set => massTotal = value;
        }

        private float wayTotal;
        public float WayTotal
        {
            get => wayTotal;
            set => wayTotal = value;
        }

        public readonly WorkControl work;
        readonly IDialogService dialogService;

        public CutControl(WorkControl _work, IDialogService _dialogService)
        {
            InitializeComponent();
            work = _work;
            dialogService = _dialogService;

            work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
            work.type.Priced += OnPriceChanged;                   // подписка на изменение материала типовой детали
            BtnEnabled();       // проверяем типовую деталь: если не "Лист металла", делаем кнопку неактивной и наоборот
        }

        private void SetWay(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetWay(tBox.Text);
        }
        private void SetWay(string _way)
        {
            if (float.TryParse(_way, out float w)) Way = w;
            OnPriceChanged();
        }

        private void SetPinhole(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetPinhole(tBox.Text);
        }
        private void SetPinhole(string _pinhole)
        {
            if (int.TryParse(_pinhole, out int p)) Pinhole = p;
            OnPriceChanged();
        }

        public void OnPriceChanged()
        {
            BtnEnabled();
            float price = 0;

            if (items.Count > 0)
            {
                foreach (LaserItem item in items) price += ItemPrice(item);
                work.SetResult(price, false);
            }
            else
            {
                if (Way == 0 || Pinhole == 0) return;
                if (work.type.MetalDrop.SelectedItem is not Metal metal) return;

                if (MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
                    price = Way * MainWindow.M.MetalDict[metal.Name][work.type.S].Item1 + Pinhole * MainWindow.M.MetalDict[metal.Name][work.type.S].Item2;
                work.SetResult(price);
            }
        }

        private void BtnEnabled()
        {
            if (work.type.TypeDetailDrop.SelectedItem is TypeDetail typeDetail && typeDetail.Name != "Лист металла") CutBtn.IsEnabled = false;
            else CutBtn.IsEnabled = true;
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (uc is not WorkControl w) return;
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{Way}");
                w.propsList.Add($"{Pinhole}");
                w.propsList.Add($"{WayTotal}");
                w.propsList.Add($"{MassTotal}");

                foreach (Part p in Parts)
                {
                    p.Price += (float)Math.Round(work.Result * p.Way / WayTotal, 2);
                    p.Price += (float)Math.Round(work.type.Result * p.Mass / MassTotal, 2);
                }
            }
            else
            {
                SetWay(w.propsList[0]);
                SetPinhole(w.propsList[1]);
                if (float.TryParse(w.propsList[2], out float _way)) WayTotal = _way;
                if (float.TryParse(w.propsList[3], out float _mass)) MassTotal = _mass;
            }
        }

        private void LoadFiles(object sender, RoutedEventArgs e)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            try
            {
                if (dialogService.OpenFileDialog() == true)
                {
                    LoadExcel(dialogService.FilePaths);
                }
            }
            catch (Exception ex)
            {
                dialogService.ShowMessage(ex.Message);
            }
        }
        public void LoadExcel(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                using FileStream stream = File.Open(paths[i], FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTable table = result.Tables[0];

                if (!MainWindow.M.IsLaser) MainWindow.M.IsLaser = true;
                if (i == 0)
                {
                    WindowParts = new(this, PartList(table));
                    ItemList(table);
                } 
                else
                {
                    work.type.det.AddTypeDetail();                                                    // добавляем типовую деталь
                    work.type.det.TypeDetailControls[^1].TypeDetailDrop.SelectedIndex = 2;            // устанавливаем "Лист металла"
                    work.type.det.TypeDetailControls[^1].WorkControls[0].WorkDrop.SelectedIndex = 2;  // устанавливаем "Лазерная резка"
                    if (work.type.det.TypeDetailControls[^1].WorkControls[^1].workType is CutControl _cut)      // заполняем эту резку
                    {
                        _cut.WindowParts = new(this, _cut.PartList(table));
                        _cut.ItemList(table);
                    }
                }
            }
        }

        public PartWindow? WindowParts = null;
        public List<Part> Parts = new();
        public List<PartControl> PartList(DataTable? table = null)
        {
            List<PartControl> _parts = new();

            if (table != null && Parts != null)
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    if (table.Rows[i] == null) continue;

                    if (table.Rows[i].ItemArray[1]?.ToString() == "Название детали")
                    {
                        for (int j = i + 1; j < table.Rows.Count; j++)
                        {
                            Part part = new()
                            {
                                Name = $"{table.Rows[j].ItemArray[2]}",
                                Count = (int)MainWindow.Parser($"{table.Rows[j].ItemArray[6]}"),
                                Description = "Л",
                                Accuracy = $"H14/h14 +-IT 14/2"
                            };

                            part.Mass = MainWindow.Parser($"{table.Rows[j].ItemArray[4]}") / part.Count;
                            part.Way = MainWindow.Parser($"{table.Rows[j].ItemArray[7]}") / part.Count;

                            if (part.Count > 0)
                            {
                                _parts.Add(new(this, part));
                                Parts.Add(part);
                            }

                            if ($"{table.Rows[j].ItemArray[0]}" == "Имя клиента") break;
                        }
                        break;
                    }
                }
            else if (Parts != null && Parts.Count > 0) foreach (Part part in Parts) _parts.Add(new(this, part));

            return _parts;
        }

        public List<LaserItem> items = new();
        public void ItemList(DataTable table)
        {
            if (items.Count > 0) items.Clear();

            // сначала считываем общие данные для всей раскладки:
            // вес всех деталей, длину пути резки, марку металла и толщину
            for (int i = 0; i < table.Rows.Count; i++)
            {
                if ($"{table.Rows[i].ItemArray[6]}".Contains("Общий вес деталей (Kg) ="))
                    MassTotal = MainWindow.Parser($"{table.Rows[i].ItemArray[8]}");

                if ($"{table.Rows[i].ItemArray[6]}".Contains("Итого Длина пути резки (mm)"))
                    WayTotal = MainWindow.Parser($"{table.Rows[i].ItemArray[8]}");


                if ($"{table.Rows[i].ItemArray[8]}" == "Материал")
                {
                    foreach (Metal metal in work.type.MetalDrop.Items) if (metal.Name == $"{table.Rows[i].ItemArray[9]}")
                        {
                            work.type.MetalDrop.SelectedItem = metal;
                            if (WindowParts != null && WindowParts.Parts.Count > 0)
                                foreach (PartControl p in WindowParts.Parts)
                                    p.Part.Metal = metal.Name;
                            break;
                        }   
                }

                if ($"{table.Rows[i].ItemArray[4]}" == "Толщина (mm)")
                {
                    work.type.S = MainWindow.Parser($"{table.Rows[i].ItemArray[5]}");
                    if (WindowParts != null && WindowParts.Parts.Count > 0)
                        foreach (PartControl p in WindowParts.Parts)
                            p.Part.Destiny = work.type.S;  
                    break;
                }
            }

            //затем считываем значения для каждого листа
            LaserItem? item = null;

            for (int i = 0; i < table.Rows.Count; i++)
            {
                item ??= new();

                if (table.Rows[i].ItemArray[0]?.ToString() == "Размер листа")
                {
                    item.sheetSize = $"{table.Rows[i].ItemArray[1]}".TrimEnd('m');
                    SetSheetSize(item.sheetSize);
                }

                if (table.Rows[i].ItemArray[2]?.ToString() == "Кол-во повторов")
                {
                    item.sheets = (int)MainWindow.Parser($"{table.Rows[i].ItemArray[3]}");
                }

                if (table.Rows[i].ItemArray[6]?.ToString() == "Количество проколов  =")
                {
                    item.pinholes = (int)MainWindow.Parser($"{table.Rows[i].ItemArray[8]}");

                    if (work.type.MetalDrop.SelectedItem is Metal metal &&
                        (metal.Name.Contains("шлиф") || metal.Name.Contains("зер"))) item.pinholes *= 2;
                }

                if ($"{table.Rows[i].ItemArray[6]}".Contains("Вес листа  (Kg) ="))
                {
                    item.mass = (float)Math.Ceiling(MainWindow.Parser($"{table.Rows[i].ItemArray[8]}"));
                }

                if ($"{table.Rows[i].ItemArray[6]}".Contains("Длина пути резки (mm) ="))
                {
                    item.way = (float)Math.Ceiling(MainWindow.Parser($"{table.Rows[i].ItemArray[8]}") / 1000);
                    if (work.type.MetalDrop.SelectedItem is Metal metal &&
                        (metal.Name.Contains("шлиф") || metal.Name.Contains("зер"))) item.way *= 2;

                    items.Add(item);
                    item = null;
                }
            }
            SumProperties(items);
        }

        public void SumProperties(List<LaserItem> _items)
        {
            Way = 0;
            Pinhole = 0;

            for (int i = 0; i < _items.Count; i++)
            {
                Way += _items[i].way * _items[i].sheets;
                Pinhole += _items[i].pinholes * _items[i].sheets;
                Mass += _items[i].mass * _items[i].sheets;
            }

            work.type.SetCount(_items.Sum(s => s.sheets));      // устанавливаем общее количество порезанных листов

            OnPriceChanged();
        }
        public float ItemPrice(LaserItem _item)
        {
            if (work.type.MetalDrop.SelectedItem is not Metal metal) return 0;

            _item.price = 0;

            if (MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
            {
                _item.price = _item.way * MainWindow.M.MetalDict[metal.Name][work.type.S].Item1
                    + _item.pinholes * MainWindow.M.MetalDict[metal.Name][work.type.S].Item2;

                // стоимость резки должна быть не ниже минимальной
                if (work.WorkDrop.SelectedItem is Work _work) _item.price = _item.price > 0 && _item.price < _work.Price ? _work.Price : _item.price;
            }

            return _item.price * _item.sheets;
        }

        private void SetSheetSize(string _sheetsize)
        {
            if (_sheetsize == null || !_sheetsize.Contains('X')) return;

            string[] properties = _sheetsize.Split('X');
            work.type.A = MainWindow.Parser(properties[0]);
            work.type.B = MainWindow.Parser(properties[1]);
        }
    }

    [Serializable]
    public class LaserItem
    {
        public string? sheetSize;
        public int sheets, pinholes;
        public float way, mass, price;
    }

    public class ExcelDialogService : IDialogService
    {
        public string[]? FilePaths { get; set; }

        public bool OpenFileDialog()
        {
            OpenFileDialog openFileDialog = new();
            openFileDialog.Filter = "Excel (*.xlsx)|*.xlsx|All files (*.*)|*.*";
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == true)
            {
                FilePaths = openFileDialog.FileNames;
                return true;
            }
            return false;
        }

        public bool SaveFileDialog()
        {
            return false;
        }

        public void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }
    }
}
