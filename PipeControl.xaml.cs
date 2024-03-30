﻿using ExcelDataReader;
using OfficeOpenXml.Drawing;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PipeControl.xaml
    /// </summary>
    public partial class PipeControl : UserControl, INotifyPropertyChanged, IPriceChanged, ICut
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float mold;
        public float Mold
        {
            get => mold;
            set
            {
                if (value != mold)
                {
                    mold = value;
                    OnPropertyChanged(nameof(Mold));
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

        public enum TubeType
        {
            rect,
            round,
            square,
            channel,
            corner
        }

        public TubeType Tube { get; set; }
        public List<PartControl>? Parts { get; set; }
        public PartsControl? PartsControl { get; set; }
        public List<Part>? PartDetails { get; set; } = new();   //свойство интерфейса ICut, в коде нигде не инициализируется, поэтому это делаем здесь

        readonly TabItem TabItem = new();

        public readonly WorkControl work;
        readonly IDialogService dialogService;

        public PipeControl(WorkControl _work, IDialogService _dialogService)
        {
            InitializeComponent();
            work = _work;
            dialogService = _dialogService;

            work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
            work.type.Priced += OnPriceChanged;                 // подписка на изменение материала типовой детали

            SetMold($"{work.type.L * work.type.Count * 0.95f / 1000}");      //переносим погонные метры из типовой детали
        }

        private void SetMold(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetMold(tBox.Text);
        }
        private void SetMold(string _mold)
        {
            if (float.TryParse(_mold, out float m)) Mold = m;
            OnPriceChanged();
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
            if (Mold == 0 || Way == 0 || Pinhole == 0) return;
            if (work.type.MetalDrop.SelectedItem is not Metal metal) return;

            float price = 0;

            if (metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
                price = Mold * MainWindow.M.MetalDict[metal.Name][work.type.S].Item3
                    + Way * MainWindow.M.MetalDict[metal.Name][work.type.S].Item1
                    + Pinhole * MainWindow.M.MetalDict[metal.Name][work.type.S].Item2;

            // стоимость резки трубы должна быть не ниже минимальной
            if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

            work.SetResult(price, false);
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (uc is not WorkControl w) return;
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{Mold}");
                w.propsList.Add($"{Way}");
                w.propsList.Add($"{Pinhole}");

                SetTotalProperties();       //определяем общую массу и общую длину нарезанных труб

                if (PartDetails?.Count > 0)
                {
                    int count = PartDetails.Sum(p => p.Count);

                    foreach (Part p in PartDetails)
                    {
                        p.Price += work.type.Result * p.Mass / MassTotal;
                        p.Price += work.Result * p.Way / WayTotal;

                        p.PropsDict[50] = new() { $"{work.type.Result * p.Mass / MassTotal}" };
                        p.PropsDict[61] = new() { $"{work.Result * p.Way / WayTotal}" };
                    }
                }
            }
            else
            {
                SetMold(w.propsList[0]);
                SetWay(w.propsList[1]);
                SetPinhole(w.propsList[2]);
            }
        }

        private void ViewPopupMold(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            PopupMold.IsOpen = true;

            if (work.type.MetalDrop.SelectedItem is Metal metal && metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
                MoldPrice.Text = $"Цена резки пог м\n{MainWindow.M.MetalDict[metal.Name][work.type.S].Item3} руб";
        }

        private void ViewPopupWay(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            PopupWay.IsOpen = true;

            if (work.type.MetalDrop.SelectedItem is Metal metal && metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
                WayPrice.Text = $"Цена метра резки\n{MainWindow.M.MetalDict[metal.Name][work.type.S].Item1} руб";
        }

        private void ViewPopupPinhole(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            PopupPinhole.IsOpen = true;

            if (work.type.MetalDrop.SelectedItem is Metal metal && metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
                PinholePrice.Text = $"Цена прокола\n{MainWindow.M.MetalDict[metal.Name][work.type.S].Item2} руб";
        }

        private void LoadFiles(object sender, RoutedEventArgs e)
        {
            LoadFiles();
        }
        public void LoadFiles()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            try
            {
                if (dialogService.OpenFileDialog() == true)
                {
                    if (dialogService.FilePaths != null) LoadExcel(dialogService.FilePaths);
                }
            }
            catch (Exception ex)
            {
                dialogService.ShowMessage(ex.Message);
            }
        }

        public void LoadExcel(string[] paths)
        {
            if (PartsControl != null)
            {
                MainWindow.M.StatusBegin($"Эта заготовка уже нарезана. Добавьте новую заготовку из списка типовых деталей!");
                return;
            }

            if (work.type.TypeDetailDrop.SelectedItem is TypeDetail _t && _t.Name != null && !_t.Name.Contains("Труба"))
                foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Труба профильная")
                    {
                        work.type.TypeDetailDrop.SelectedItem = t;
                        break;
                    }

            if (!MainWindow.M.IsLaser) MainWindow.M.IsLaser = true;     //внедрить свойство Компании и убрать переключение

            for (int i = 0; i < paths.Length; i++)
            {
                using FileStream stream = File.Open(paths[i], FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTableCollection tables = result.Tables;

                if (i == 0)
                {
                    Parts = PartList(tables);           // формируем список элементов PartControl
                    SetImagesForParts(stream);          // устанавливаем поле Part.ImageBytes для каждой детали
                    PartsControl = new(this, Parts);    // создаем форму списка нарезанных деталей
                    AddPartsTab();                      // добавляем вкладку в "Список нарезанных деталей" 
                }
                else
                {   // добавляем типовую деталь
                    work.type.det.AddTypeDetail();

                    // устанавливаем "Труба профильная"
                    foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Труба профильная")
                        {
                            work.type.det.TypeDetailControls[^1].TypeDetailDrop.SelectedItem = t;
                            break;
                        }

                    // устанавливаем "Труборез"
                    foreach (Work w in MainWindow.M.Works) if (w.Name == "Труборез")
                        {
                            work.type.det.TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedItem = w;
                            break;
                        }

                    // заполняем эту резку
                    if (work.type.det.TypeDetailControls[^1].WorkControls[^1].workType is PipeControl _pipe)
                    {
                        _pipe.Parts = _pipe.PartList(tables);
                        _pipe.SetImagesForParts(stream);
                        _pipe.PartsControl = new(this, _pipe.Parts);
                        _pipe.AddPartsTab();
                    }
                }
            }

            //определяем деталь, в которой загрузили раскладки, как комплект деталей
            if (!work.type.det.Detail.IsComplect) work.type.det.IsComplectChanged("Комплект труб");
        }

        public void AddPartsTab()                                           //метод добавления вкладки для PartsControl
        {
            TabItem.Header = new TextBlock { Text = $"(ТР) s{work.type.S} {work.type.MetalDrop.Text}" };    // установка заголовка вкладки
            TabItem.Content = PartsControl;                                                                 // установка содержимого вкладки
            MainWindow.M.PartsTab.Items.Add(TabItem);
        }

        private void RemovePartsTab(object sender, RoutedEventArgs e)       //метод удаления вкладки нарезанных деталей этой резки,
                                                                            //вызывается по событию PipeControl.Unloaded
        {
            MainWindow.M.PartsTab.Items.Remove(TabItem);
        }

        public List<PartControl> PartList(DataTableCollection? tables = null)
        {
            List<PartControl> _parts = new();

            if (tables != null)
            {
                PartDetails?.Clear();

                for (int j = 0; j < tables[2].Rows.Count; j++)
                {
                    if (tables[2].Rows[j] == null) continue;

                    if (tables[2].Rows[j].ItemArray[2]?.ToString() == "Кол. сечений")
                    {
                        work.type.SetCount((int)MainWindow.Parser(tables[2].Rows[j + 1].ItemArray[2].ToString()));
                    }

                    if (tables[2].Rows[j].ItemArray[3]?.ToString() == "Контур")
                    {
                        SetPinhole(tables[2].Rows[j + 1].ItemArray[3].ToString());
                    }

                    if (tables[2].Rows[j].ItemArray[4]?.ToString() == "Длина резки сечения(mm)")
                    {
                        if (float.TryParse(tables[2].Rows[j + 1].ItemArray[4].ToString(), out float w)) Way = (float)Math.Ceiling(w / 1000);
                    }
                }

                for (int i = 0; i < tables[0].Rows.Count; i++)
                {
                    if (tables[0].Rows[i] == null) continue;

                    if (tables[0].Rows[i].ItemArray[1]?.ToString() == "Имя сечения")
                    {
                        string _tube = $"{tables[0].Rows[i + 1].ItemArray[1]}";

                        Regex rect = new(@"[+-]?((\d+\.?\d*)|(\.\d+))");

                        List<Match> matches = rect.Matches(_tube).ToList();

                        if (matches.Count > 0)
                        {
                            if (_tube.Contains("Rect tube"))            //профильная труба
                            {
                                work.type.A = MainWindow.Parser(matches[0].Value);
                                work.type.B = MainWindow.Parser(matches[1].Value);
                                Tube = TubeType.rect;
                            }
                            else if (_tube.Contains("Round tube"))      //круглая труба
                            {
                                foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Труба круглая")
                                    {
                                        work.type.TypeDetailDrop.SelectedItem = t;
                                        break;
                                    }
                                work.type.A = work.type.B = MainWindow.Parser(matches[0].Value) * 2;
                                Tube = TubeType.round;
                            }
                            else if (_tube.Contains("Square tube"))     //квадратная труба
                            {
                                work.type.A = work.type.B = MainWindow.Parser(matches[0].Value);
                                Tube = TubeType.square;
                            }
                            else if (_tube.Contains("U tube"))          //швеллер
                            {
                                foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Швеллер")
                                    {
                                        work.type.TypeDetailDrop.SelectedItem = t;

                                        string _match = $"{MainWindow.Parser(matches[0].Value) / 10}".Replace(',', '.');

                                        foreach (string s in work.type.SortDrop.Items)
                                            if (s == _match)
                                            {
                                                {
                                                    work.type.SortDrop.SelectedItem = s;
                                                    break;
                                                }
                                            }
                                        break;
                                    }
                                Tube = TubeType.channel;
                            }
                        }
                        else
                        {
                            MainWindow.M.StatusBegin("Не удалось определить размеры трубы");
                        }
                        continue;
                    }

                    if (tables[0].Rows[i].ItemArray[2]?.ToString() == "Название деталей")
                    {
                        for (int j = i + 1; j < tables[0].Rows.Count; j++)
                        {
                            Part part = new()
                            {
                                Title = $"{tables[0].Rows[j].ItemArray[2]}",
                                Description = "ТР",
                                Accuracy = $"H12/h12 +-IT 12/2"
                            };

                            string? _count = tables[0].Rows[j].ItemArray[3]?.ToString();

                            if (_count != null && _count.Contains('/')) part.Count = (int)MainWindow.Parser(_count.Split('/')[0]);

                            if (part.Count > 0)
                            {
                                //устанавливаем толщину заготовки
                                Regex destiny = new(@"х[+-]?((\d+\.?\d*)|(\.\d+))");

                                List<Match> matches = destiny.Matches(part.Title).ToList();

                                if (matches.Count > 0 && work.type.S == 0) work.type.S = MainWindow.Parser(matches[^1].Value.Trim('х'));

                                part.Destiny = work.type.S;
                                part.Metal = work.type.MetalDrop.Text;

                                part.Way = (float)Math.Round(MainWindow.Parser($"{tables[0].Rows[j].ItemArray[4]}"), 3);
                                if (work.type.MetalDrop.SelectedItem is Metal metal)
                                    part.Mass = Tube switch
                                    {
                                        TubeType.rect => (float)Math.Round(0.0157f * work.type.S * (work.type.A + work.type.B - 2.86f * work.type.S) * part.Way * metal.Density / 7850, 3),
                                        TubeType.round => (float)Math.Round(Math.PI * work.type.S * (work.type.A - work.type.S) * part.Way * metal.Density / 1000000, 3),
                                        TubeType.square => (float)Math.Round(0.0157f * work.type.S * (work.type.A + work.type.B - 2.86f * work.type.S) * part.Way * metal.Density / 7850, 3),
                                        TubeType.channel => (float)Math.Round(work.type.Channels[work.type.SortDrop.SelectedIndex] * part.Way / 1000, 3),
                                        TubeType.corner => (float)Math.Round((work.type.S * (work.type.A + work.type.B - work.type.S) + 0.2146f * (work.type.Corners[work.type.SortDrop.SelectedIndex].Item1 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item1 - 2 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2)) * part.Way * metal.Density / 1000000, 3),
                                        _ => 1
                                    };

                                _parts.Add(new(this, work, part));
                                PartDetails?.Add(part);
                            }

                            if ($"{tables[0].Rows[j].ItemArray[2]}" == " ") break;
                        }
                        break;
                    }
                }

                SetMold($"{work.type.L * work.type.Count * 0.95f / 1000}");      //переносим погонные метры из типовой детали
            }
            else if (PartDetails?.Count > 0) foreach (Part part in PartDetails) _parts.Add(new(this, work, part));

            return _parts;
        }

        public void SetTotalProperties()
        {
            MassTotal = WayTotal = 0;

            if (PartDetails?.Count > 0) foreach (Part part in PartDetails)
                {
                    MassTotal += part.Mass * part.Count;
                    WayTotal += part.Way * part.Count;
                }
        }

        public void SetImagesForParts(FileStream stream)        //метод извлечения картинок из файла и установки их для каждой детали в виде массива байтов
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage(stream);                      //получаем книгу Excel из потока
            ExcelWorksheet worksheet = workbook.Workbook.Worksheets[0];

            //извлекаем все изображения на листе в список картинок
            List<ExcelPicture> pictures = worksheet.Drawings.Where(x => x.DrawingType == eDrawingType.Picture).Select(x => x.As.Picture).ToList();

            if (PartDetails?.Count > 0 && pictures.Count > 0)
                for (int i = 0; i < PartDetails.Count; i++)
                    PartDetails[i].ImageBytes = pictures[i].Image.ImageBytes;    //для каждой детали записываем массив байтов соответствующей картинки
        }
    }
}
