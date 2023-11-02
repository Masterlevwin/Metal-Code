﻿using ExcelDataReader;
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

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для CutControl.xaml
    /// </summary>
    public partial class CutControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float price;
        public float Price
        {
            get => price;
            set
            {
                price = value;
                OnPropertyChanged(nameof(Price));
            }
        }

        private float ratio;
        public float Ratio
        {
            get => ratio;
            set
            {
                if (value != ratio)
                {
                    ratio = value;
                    OnPropertyChanged(nameof(Ratio));
                }
            }
        }

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

        public readonly WorkControl work;
        private PropertyControl? prop;
        readonly IDialogService dialogService;

        public CutControl(WorkControl _work, IDialogService _dialogService)
        {
            InitializeComponent();
            work = _work;
            dialogService = _dialogService;

            work.PropertiesChanged += SaveOrLoadProperties; // подписка на сохранение и загрузку файла
            work.type.Counted += PriceChanged;              // подписка на изменение количества типовых деталей
            work.type.Priced += PriceChanged;               // подписка на изменение материала типовой детали
            foreach (WorkControl w in work.type.WorkControls)
                if (w != work && w.workType is PropertyControl _prop)
                {
                    prop = _prop;
                    _prop.MassChanged += MassChanged;        // подписка на изменение массы типовой детали или толщины листа
                    MassChanged(_prop);
                }
            BtnEnabled();       // проверяем типовую деталь: если не "Лист металла", делаем кнопку неактивной
        }

        private void SetRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRatio(tBox.Text);
        }
        private void SetRatio(string _ratio)
        {
            if (float.TryParse(_ratio, out float r)) Ratio = r;     // стандартный парсер избавляет от проблемы с запятой
            PriceChanged();
        }

        private void SetWay(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetWay(tBox.Text);
        }
        private void SetWay(string _way)
        {
            if (float.TryParse(_way, out float w)) Way = w;
            PriceChanged();
        }

        private void SetPinhole(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetPinhole(tBox.Text);
        }
        private void SetPinhole(string _pinhole)
        {
            if (int.TryParse(_pinhole, out int p)) Pinhole = p;
            PriceChanged();
        }

        float Wratio, Pratio;
        private void MassChanged(PropertyControl prop)
        {
            Wratio = prop.S * 1.05f;
            Pratio = prop.S * 1.5f;
            PriceChanged();
        }

        private void PriceChanged()
        {
            BtnEnabled();
            if (Way == 0 || Pinhole == 0) return;
            if (work.type.MetalDrop.SelectedItem is not Metal metal) return;

            Price = work.Result = (float)Math.Round((Way * metal.WayPrice * Wratio + Pinhole * metal.PinholePrice * Pratio) * work.type.Count * Ratio + work.Price * work.type.Count, 2);

            work.type.det.PriceResult();
        }

        private void BtnEnabled()
        {
            if (work.type.TypeDetailDrop.SelectedItem is TypeDetail typeDetail && typeDetail.Name != "Лист металла")
            {
                CutBtn.IsEnabled = false;
                Way = Price = Pinhole = 0;
            }   
            else CutBtn.IsEnabled = true;
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{Way}");
                w.propsList.Add($"{Pinhole}");
                w.propsList.Add($"{Ratio}");
            }
            else
            {
                SetWay(w.propsList[0]);
                SetPinhole(w.propsList[1]);
                SetRatio(w.propsList[2]);
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
                    dialogService.ShowMessage("Файл открыт");
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

                if (i == 0) ItemList(table);
                else
                {
                    work.type.det.AddTypeDetail();                                                    // добавляем типовую деталь
                    work.type.det.TypeDetailControls[^1].TypeDetailDrop.SelectedIndex = 2;            // устанавливаем "Лист металла"
                    work.type.det.TypeDetailControls[^1].WorkControls[0].WorkDrop.SelectedIndex = 0;  // устанавливаем "Покупка", автоматически добавится "Резка листа"
                    if (work.type.det.TypeDetailControls[^1].WorkControls[^1].workType is CutControl _cut) _cut.ItemList(table);    // заполняем эту резку
                }
            }
        }

        public List<LaserItem> items = new();

        public void ItemList(DataTable table)
        {
            if (items.Count > 0) items.Clear();

            //сначала считываем общие данные для всей раскладки:
            //марку металла и толщину
            for (int i = 0; i < table.Rows.Count; i++)
            {
                if ($"{table.Rows[i].ItemArray[8]}" == "Материал")
                {
                    foreach (Metal metal in work.type.MetalDrop.Items) if (metal.Name == $"{table.Rows[i].ItemArray[9]}")
                        {
                            work.type.MetalDrop.SelectedItem = metal;
                            break;
                        }   
                }

                if ($"{table.Rows[i].ItemArray[4]}" == "Толщина (mm)")
                {
                    if (prop != null) prop.S = MainWindow.Parser($"{table.Rows[i].ItemArray[5]}");
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
                    if ($"{work.type.MetalDrop.SelectedItem}".Contains("шлиф") || $"{work.type.MetalDrop.SelectedItem}".Contains("зер")) item.pinholes *= 2;
                }

                if ($"{table.Rows[i].ItemArray[6]}".Contains("Вес листа  (Kg) ="))
                {
                    item.mass = (float)Math.Ceiling(MainWindow.Parser($"{table.Rows[i].ItemArray[8]}"));
                }

                if ($"{table.Rows[i].ItemArray[6]}".Contains("Длина пути резки (mm) ="))
                {
                    item.way = (float)Math.Ceiling(MainWindow.Parser($"{table.Rows[i].ItemArray[8]}") / 1000);
                    if ($"{work.type.MetalDrop.SelectedItem}".Contains("шлиф") || $"{work.type.MetalDrop.SelectedItem}".Contains("зер")) item.way *= 2;

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
            }

            work.type.SetCount(_items.Sum(s => s.sheets));      // устанавливаем общее количество порезанных листов

            if (prop != null) MassChanged(prop);
        }

        private void SetSheetSize(string _sheetsize)
        {
            if (_sheetsize == null || !_sheetsize.Contains('X')) return;

            string[] properties = _sheetsize.Split('X');

            if (prop != null)
            {
                prop.A = MainWindow.Parser(properties[0]);
                prop.B = MainWindow.Parser(properties[1]);
            }
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
