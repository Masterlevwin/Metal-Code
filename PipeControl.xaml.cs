using ExcelDataReader;
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

                if (PartDetails?.Count > 0)
                {
                    int count = PartDetails.Sum(p => p.Count);

                    foreach (Part p in PartDetails)
                    {
                        p.Price += work.type.Result / count;
                        p.Price += work.Result / count;

                        p.PropsDict[50] = new() { $"{work.type.Result / count}" };
                        p.PropsDict[51] = new() { $"{work.Result / count}" };
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

            // раскладки можно загрузить только в отдельную деталь Комплект деталей,
            // в которой нет других типовых деталей, кроме Лист металла, и в этом "Листе" должна быть резка...
            // ...это условие необходимо соблюдать для корректного отображения сборных деталей в КП
            //foreach (TypeDetailControl _type in work.type.det.TypeDetailControls)
            //{
            //    if (_type.TypeDetailDrop.SelectedIndex != 1) //|| !_type.WorkControls.Contains(_type.WorkControls.FirstOrDefault(w => w.workType is PipeControl)))
                    
            //    {
            //        _type.TypeDetailDrop.SelectedIndex = 1;

            //        // добавляем типовую деталь
            //        work.type.det.AddTypeDetail();

            //        // устанавливаем по умолчанию "Труба профильная"
            //        foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Труба профильная")
            //            {
            //                MainWindow.M.DetailControls[^1].TypeDetailControls[^1].TypeDetailDrop.SelectedItem = t;
            //                break;
            //            }

            //        // устанавливаем "Труборез"
            //        foreach (Work w in MainWindow.M.Works) if (w.Name == "Труборез")
            //            {
            //                MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedItem = w;
            //                break;
            //            }

            //        // вызываем загрузку раскладок в новой детали
            //        if (MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].workType is PipeControl _pipe)
            //        {
            //            _pipe.LoadExcel(paths);
            //            MainWindow.M.StatusBegin($"Раскрой загружен в новую деталь \"Комплект деталей\"");
            //        }

            //        work.Remove();      // удаляем типовую деталь с этой работой
            //        return;
            //    }
            //}

            if (!MainWindow.M.IsLaser) MainWindow.M.IsLaser = true;     //внедрить свойство Компании и убрать переключение

            for (int i = 0; i < paths.Length; i++)
            {
                using FileStream stream = File.Open(paths[i], FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTable table = result.Tables[0];

                if (i == 0)
                {
                    Parts = PartList(table);            // формируем список элементов PartControl
                    SetImagesForParts(stream);          // устанавливаем поле Part.ImageBytes для каждой детали
                    PartsControl = new(this, Parts);    // создаем форму списка нарезанных деталей

                    SetupProperties();                  //временный метод для теста

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
                        _pipe.Parts = _pipe.PartList(table);
                        _pipe.SetImagesForParts(stream);
                        _pipe.PartsControl = new(this, _pipe.Parts);
                        _pipe.AddPartsTab();
                        
                        //_pipe.SetupProperties();
                    }
                }
            }

            //определяем деталь, в которой загрузили раскладки, как комплект деталей
            if (!work.type.det.Detail.IsComplect) work.type.det.IsComplectChanged();
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

        public List<PartControl> PartList(DataTable? table = null)
        {
            List<PartControl> _parts = new();

            if (table != null)
            {
                PartDetails?.Clear();

                for (int i = 0; i < table.Rows.Count; i++)
                {
                    if (table.Rows[i] == null) continue;

                    if (table.Rows[i].ItemArray[2]?.ToString() == "Название деталей")
                    {
                        for (int j = i + 1; j < table.Rows.Count; j++)
                        {
                            Part part = new()
                            {
                                Title = $"{table.Rows[j].ItemArray[2]}",
                                Count = (int)MainWindow.Parser($"{table.Rows[j].ItemArray[3].ToString().Remove(2)}"),
                                Description = "ТР",
                                Accuracy = $"H12/h12 +-IT 12/2"
                            };

                            //part.Mass = (float)Math.Round(MainWindow.Parser($"{table.Rows[j].ItemArray[4]}") / part.Count, 3);

                            if (float.TryParse($"{table.Rows[j].ItemArray[4]}", out float _way)) part.Way = _way / 1000;

                            if (part.Count > 0)
                            {
                                _parts.Add(new(this, work, part));
                                PartDetails?.Add(part);
                            }

                            if ($"{table.Rows[j].ItemArray[2]}" == " ") break;
                        }
                        break;
                    }
                }
            }
            else if (PartDetails?.Count > 0) foreach (Part part in PartDetails) _parts.Add(new(this, work, part));

            return _parts;
        }

        public void SetupProperties()
        {
            work.type.A = 60;
            work.type.B = 30;
            work.type.S = 4;
            work.type.L = 6000;
            work.type.Count = 1;

            SetMold($"{work.type.L * work.type.Count * 0.95f / 1000}");      //переносим погонные метры из типовой детали

            if (PartDetails?.Count > 0) foreach (Part part in PartDetails)
                {
                    Way += (float)Math.Ceiling(part.Way);
                    Pinhole += part.Count * 2;

                    part.Destiny = work.type.S;
                    part.Metal = work.type.MetalDrop.Text;
                }

            work.type.MassCalculate();
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
