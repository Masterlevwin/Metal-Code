using ExcelDataReader;
using Microsoft.Win32;
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
using System.Windows.Input;
using System.Runtime.Serialization;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для CutControl.xaml
    /// </summary>
    public partial class CutControl : UserControl, INotifyPropertyChanged, IPriceChanged, ICut
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

        public List<PartControl>? Parts { get; set; }
        public PartsControl? PartsControl { get; set; }
        public List<Part>? PartDetails { get; set; } = new();
        public List<LaserItem>? Items { get; set; } = new();

        readonly TabItem TabItem = new();

        public readonly WorkControl work;
        readonly IDialogService dialogService;

        public CutControl(WorkControl _work, IDialogService _dialogService)
        {
            InitializeComponent();
            work = _work;
            dialogService = _dialogService;

            work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
            work.type.Priced += OnPriceChanged;                 // подписка на изменение материала типовой детали
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

            if (Items?.Count > 0)
            {
                foreach (LaserItem item in Items) price += ItemPrice(item);

                // стоимость резки должна быть не ниже 10% от стоимости материала
                if (work.type.Result > 0 && (price / work.type.Result) < 0.1f) price = work.type.Result * 0.1f;

                work.SetResult(price, false);
            }
            else
            {
                if (Way == 0 || Pinhole == 0) return;
                if (work.type.MetalDrop.SelectedItem is not Metal metal) return;

                if (metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
                {
                    price = Way * MainWindow.M.MetalDict[metal.Name][work.type.S].Item1 + Pinhole * MainWindow.M.MetalDict[metal.Name][work.type.S].Item2;

                    // стоимость резки должна быть не ниже 10% от стоимости материала
                    if (work.type.Result > 0 && (price / work.type.Result) < 0.1f) price = work.type.Result * 0.1f;

                    work.SetResult(price);
                }
                else work.SetResult(price, false);
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

                if (PartDetails?.Count > 0)
                    foreach (Part p in PartDetails)
                    {
                        p.Price += work.type.Result * p.Mass / MassTotal;
                        p.Price += work.Result * p.Way / WayTotal;

                        p.PropsDict[50] = new() { $"{work.type.Result * p.Mass / MassTotal}" };
                        p.PropsDict[51] = new() { $"{work.Result * p.Way / WayTotal}" };
                    }
            }
            else
            {
                SetWay(w.propsList[0]);
                SetPinhole(w.propsList[1]);
                if (float.TryParse(w.propsList[2], out float _way)) WayTotal = _way;
                if (float.TryParse(w.propsList[3], out float _mass)) MassTotal = _mass;

                work.type.CreateSort();     //переопределяем содержимое SortDrop, если есть раскладки (List<LaserItem>))
            }
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
            if (PartsControl is not null)
            {
                MainWindow.M.StatusBegin($"Этот лист уже нарезан. Добавьте новый \"Лист металла\"!");
                return;
            }

            if (work.type.TypeDetailDrop.SelectedItem is TypeDetail _t && _t.Name != "Лист металла")
                foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Лист металла")
                    {
                        work.type.TypeDetailDrop.SelectedItem = t;
                        break;
                    }

            //определяем деталь, в которой загрузили раскладки, как комплект деталей
            if (!work.type.det.Detail.IsComplect) work.type.det.IsComplectChanged("Комплект деталей");

            for (int i = 0; i < paths.Length; i++)
            {
                using FileStream stream = File.Open(paths[i], FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTable table = result.Tables[0];

                if (i == 0)
                {
                    Parts = PartList(table);            // формируем список элементов PartControl
                    ItemList(table);                    // формируем список листов из раскладки
                    SetImagesForParts(stream);          // устанавливаем поле Part.ImageBytes для каждой детали                
                    PartsControl = new(this, Parts);    // создаем форму списка нарезанных деталей
                    AddPartsTab();                      // добавляем вкладку в "Список нарезанных деталей"

                    work.type.CreateSort();             // обновляем заготовку и все, связанные с ней, данные

                    if (Parts.Count > 0)
                        foreach (PartControl part in Parts)
                            PartTitleAnalysis(part);    // анализируем наименование каждой детали
                    PartsValidate();                    // проверяем резку и массу деталей
                }
                else
                {   // добавляем типовую деталь
                    work.type.det.AddTypeDetail();

                    // устанавливаем "Лист металла"
                    foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Лист металла")
                        {
                            work.type.det.TypeDetailControls[^1].TypeDetailDrop.SelectedItem = t;
                            break;
                        }

                    // устанавливаем "Лазерная резка"
                    foreach (Work w in MainWindow.M.Works) if (w.Name == "Лазерная резка")
                        {
                            work.type.det.TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedItem = w;
                            break;
                        }

                    // заполняем эту резку
                    if (work.type.det.TypeDetailControls[^1].WorkControls[^1].workType is CutControl _cut)
                    {
                        _cut.Parts = _cut.PartList(table);
                        _cut.ItemList(table);
                        _cut.SetImagesForParts(stream);
                        _cut.PartsControl = new(_cut, _cut.Parts);
                        _cut.AddPartsTab();

                        _cut.work.type.CreateSort();

                        if (_cut.Parts.Count > 0)
                            foreach (PartControl part in _cut.Parts)
                                _cut.PartTitleAnalysis(part);
                        _cut.PartsValidate();
                    }
                }
            }
        }

        private void PartTitleAnalysis(PartControl part)        //метод анализа наименования детали, для автоматического создания блоков работ
        {
            // если в наименовании детали есть скобки...
            if (part.Part.Title != null && part.Part.Title.Length > 0 && part.Part.Title.Contains('(') && part.Part.Title.Contains(')'))
            {
                // ...получаем массив строк, разделенных пробелами, внутри скобок, и добавляем соответствующие блоки работ
                string[] nameWorks = GetSubstringByString("(", ")", part.Part.Title).ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (nameWorks.Length > 0)
                    foreach (string str in nameWorks)
                    {
                        if (str.Contains("гиб"))    // в случае с гибкой дополнительно определяем количество разнотипных гибов
                        {
                            // разделяем строку на новый массив, разделенный символом "г"
                            string[] bends = str.Split(new[] { 'г' }, StringSplitOptions.RemoveEmptyEntries);

                            // если новый массив содержит больше одного элемента, и этот элемент успешно парсится в число...
                            if (bends.Length > 1 && int.TryParse(bends[0], out int num))
                                for (int i = 0; i < num; i++) part.AddControl(0);       // ...добавляем такое число блоков гибки
                            else part.AddControl(0);                                    // иначе просто добавляем один блок гибки
                        }
                        else if (str.Contains("рез"))    // в случае с резьбой дополнительно определяем количество разнотипных отверстий
                        {
                            // разделяем строку на новый массив, разделенный символом "р"
                            string[] threads = str.Split(new[] { 'р' }, StringSplitOptions.RemoveEmptyEntries);

                            // если новый массив содержит больше одного элемента, и этот элемент успешно парсится в число...
                            if (threads.Length > 1 && int.TryParse(threads[0], out int num))
                                for (int i = 0; i < num; i++) part.AddControl(3);       // ...добавляем такое число блоков резьбы
                            else part.AddControl(3);                                    // иначе просто добавляем один блок резьбы
                        }
                        else if (str.Contains("зен"))    // в случае с зенковкой дополнительно определяем количество разнотипных отверстий
                        {
                            // разделяем строку на новый массив, разделенный символом "з"
                            string[] threads = str.Split(new[] { 'з' }, StringSplitOptions.RemoveEmptyEntries);

                            // если новый массив содержит больше одного элемента, и этот элемент успешно парсится в число...
                            if (threads.Length > 1 && int.TryParse(threads[0], out int num))
                                for (int i = 0; i < num; i++) part.AddControl(4);       // ...добавляем такое число блоков зенковки
                            else part.AddControl(4);                                    // иначе просто добавляем один блок зенковки
                        }
                        else if (str.Contains("свер"))    // в случае с сверловкой дополнительно определяем количество разнотипных отверстий
                        {
                            // разделяем строку на новый массив, разделенный символом "р"
                            string[] threads = str.Split(new[] { 'с' }, StringSplitOptions.RemoveEmptyEntries);

                            // если новый массив содержит больше одного элемента, и этот элемент успешно парсится в число...
                            if (threads.Length > 1 && int.TryParse(threads[0], out int num))
                                for (int i = 0; i < num; i++) part.AddControl(5);       // ...добавляем такое число блоков сверловки
                            else part.AddControl(5);                                    // иначе просто добавляем один блок сверловки
                        }

                        switch (str)                // далее проверяем строку на наличие других работ
                        {
                            case "свар":
                                part.AddControl(1);
                                break;
                            case "окр":
                                part.AddControl(2);
                                break;
                            case "вальц":
                                part.AddControl(6);
                                break;
                        }
                    }
            }
        }

        private void PartsValidate()                            //метод анализа общего пути резки и общего веса деталей
        {
            if (PartDetails?.Count > 0)
            {
                float _wayTotal = 0;                            //вычисляем общий путь резки всех деталей
                foreach (Part part in PartDetails) _wayTotal += part.Way * part.Count;

                if (WayTotal != _wayTotal)                      //если считанный из файла excel общий путь резки отличается от вычисленного
                {
                    float _ratio = WayTotal / _wayTotal;        //получаем коэффициент этой разницы и устанавливаем новое значение периметра резки каждой детали
                                                                //это устраняет проблему, когда резка открытого контура и общий рез считались некорректно
                    foreach (Part part in PartDetails) part.Way *= _ratio;
                }

                if (MassTotal == Mass)                          //если общий вес деталей совпадает с общим весом листов
                {                           //переустанавливаем массу каждой детали как среднее арифметическое значение
                                            //это устраняет проблему, когда вес деталей слишком мал или равен нулю
                    int _count = PartDetails.Sum(x => x.Count);
                    foreach (Part part in PartDetails) part.Mass = (float)Math.Round(MassTotal / _count, 3);
                }
            }
        }

        public void AddPartsTab(bool isClon = false)                        //метод добавления вкладки для PartsControl
        {
            TabItem.Header = new TextBlock { Text = $"s{work.type.S} {work.type.MetalDrop.Text} ({PartDetails?.Sum(x => x.Count)} шт)" }; // установка заголовка вкладки
            TabItem.Content = PartsControl;                                 // установка содержимого вкладки

            if (isClon) ProductWindow.P.PartsTab.Items.Add(TabItem);
            else MainWindow.M.PartsTab.Items.Add(TabItem);
        }

        private void RemovePartsTab(object sender, RoutedEventArgs e)       //метод удаления вкладки нарезанных деталей этой резки,
                                                                            //вызывается по событию CutControl.Unloaded
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

                    if (table.Rows[i].ItemArray[1]?.ToString() == "Название детали")
                    {
                        for (int j = i + 1; j < table.Rows.Count; j++)
                        {
                            Part part = new()
                            {
                                Title = $"{table.Rows[j].ItemArray[2]}",
                                Count = (int)MainWindow.Parser($"{table.Rows[j].ItemArray[6]}"),
                                Description = "Л",
                                Accuracy = $"H14/h14 +-IT 14/2"
                            };

                            part.Mass = (float)Math.Round(MainWindow.Parser($"{table.Rows[j].ItemArray[4]}") / part.Count, 3);
                            part.Way = (float)Math.Round(MainWindow.Parser($"{table.Rows[j].ItemArray[7]}") / part.Count, 3);

                            if (part.Count > 0)
                            {
                                (float, float) tuple = SizesDetail($"{table.Rows[j].ItemArray[3]}");    //получаем габариты детали
                                //записываем полученные габариты и саму строку для их отображения в словарь свойств
                                part.PropsDict[100] = new() { $"{tuple.Item1}", $"{tuple.Item2}", $"{table.Rows[j].ItemArray[3]}" };

                                _parts.Add(new(this, work, part));
                                PartDetails?.Add(part);
                            }

                            if ($"{table.Rows[j].ItemArray[0]}" == "Имя клиента") break;
                        }
                        break;
                    }
                }
            }
            else if (PartDetails?.Count > 0) foreach (Part part in PartDetails) _parts.Add(new(this, work, part));

            return _parts;
        }

        public void SetImagesForParts(FileStream stream)        //метод извлечения картинок из файла и установки их для каждой детали в виде массива байтов
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage(stream);                      //получаем книгу Excel из потока
            ExcelWorksheet worksheet = workbook.Workbook.Worksheets[0];

            //извлекаем все изображения на листе в список картинок
            List<ExcelPicture> pictures = worksheet.Drawings.Where(x => x.DrawingType == eDrawingType.Picture).Select(x => x.As.Picture).ToList();
      
            if (Parts?.Count > 0)
                for (int i = 0; i < Parts.Count; i++)
                    Parts[i].Part.ImageBytes = pictures[i].Image.ImageBytes;    //для каждой детали записываем массив байтов соответствующей картинки

            //получаем выборку изображений самих раскладок на основе размера массива байтов
            var images = pictures.Where(x => x.Image.ImageBytes.Length > 9999).ToList();

            if (Items?.Count > 0)
                for (int j = 0; j < Items.Count; j++)
                    Items[j].imageBytes = images[j].Image.ImageBytes;           //для каждой раскладки записываем массив байтов соответствующей картинки
        }

        public void ItemList(DataTable table)
        {
            if (Items?.Count > 0) Items.Clear();

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
                            if (PartsControl != null && PartsControl.Parts.Count > 0)
                                foreach (PartControl p in PartsControl.Parts)
                                    p.Part.Metal = metal.Name;
                            break;
                        }   
                }

                if ($"{table.Rows[i].ItemArray[4]}" == "Толщина (mm)")
                {
                    work.type.S = MainWindow.Parser($"{table.Rows[i].ItemArray[5]}");
                    if (PartsControl != null && PartsControl.Parts.Count > 0)
                        foreach (PartControl p in PartsControl.Parts)
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
                    item.sheetSize = $"{table.Rows[i].ItemArray[1]}".TrimEnd('m').Trim();
                    SetSheetSize(item.sheetSize);
                }

                if (table.Rows[i].ItemArray[2]?.ToString() == "Кол-во повторов")
                {
                    item.sheets = (int)MainWindow.Parser($"{table.Rows[i].ItemArray[3]}");
                }

                if (table.Rows[i].ItemArray[6]?.ToString() == "Количество проколов  =")
                {
                    item.pinholes = (int)MainWindow.Parser($"{table.Rows[i].ItemArray[8]}");

                    if (work.type.MetalDrop.SelectedItem is Metal metal && metal.Name != null &&
                        (metal.Name.Contains("шлиф") || metal.Name.Contains("зер"))) item.pinholes *= 2;
                }

                if ($"{table.Rows[i].ItemArray[6]}".Contains("Вес листа  (Kg) ="))
                {
                    item.mass = (float)Math.Ceiling(MainWindow.Parser($"{table.Rows[i].ItemArray[8]}"));
                }

                if ($"{table.Rows[i].ItemArray[6]}".Contains("Длина пути резки (mm) ="))
                {
                    item.way = (float)Math.Ceiling(MainWindow.Parser($"{table.Rows[i].ItemArray[8]}") / 1000);
                    if (work.type.MetalDrop.SelectedItem is Metal metal && metal.Name != null &&
                        (metal.Name.Contains("шлиф") || metal.Name.Contains("зер"))) item.way *= 2;

                    Items?.Add(item);
                    item = null;
                }
            }
            if (Items?.Count > 0) SumProperties(Items);
        }

        public void SumProperties(List<LaserItem> _items)
        {
            Way = 0;
            Pinhole = 0;
            Mass = 0;

            for (int i = 0; i < _items.Count; i++)
            {
                Way += _items[i].way * _items[i].sheets;
                Pinhole += _items[i].pinholes * _items[i].sheets;
                Mass += _items[i].mass * _items[i].sheets;
            }

            if (MassTotal < 1) MassTotal = Mass;                // общий вес деталей не должен быть меньше 1 кг, иначе возникнет ошибка деления на 0

            work.type.SetCount(_items.Sum(s => s.sheets));      // устанавливаем общее количество порезанных листов

            OnPriceChanged();
        }

        public float ItemPrice(LaserItem _item)
        {
            if (work.type.MetalDrop.SelectedItem is not Metal metal) return 0;

            _item.price = 0;

            if (metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
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

            //алгоритм установки раскроя взависимости от обрезки по ширине листа; если лист будет обрезан по длине, раскрой может быть не верен!
            foreach (string s in work.type.SortDrop.Items)
                if (s.Contains($"{work.type.B / 1000}".Replace(',', '.')))
                {
                    work.type.SortDrop.SelectedItem = s;
                    break;
                }     
        }

        public (float, float) SizesDetail(string str)       //метод извлечения из строки габаритов детали
        {
            //выходим из метода, если строки нет, или она не содержит информацию о размере детали
            if (str == null || !str.Contains('X')) return (1, 1);

            //создаем и сразу инициализируем массив строк по следующему принципу:
            //если у детали есть отверстия('Ø'), то сначала обрезаем строку до знака диаметра,
            //а затем разделяем получившуюся строку на два числовых значения (размеры детали),
            //иначе сразу разделяем строку на размеры
            string[] sizes = str.Contains('Ø') ? str[..str.IndexOf('Ø')].Split('X') : str.Split('X');

            return (MainWindow.Parser(sizes[0]), MainWindow.Parser(sizes[1]));
        }

        public string GetSubstringByString(string a, string b, string c)
        {
            return c.Substring(c.IndexOf(a) + a.Length, c.IndexOf(b) - c.IndexOf(a) - a.Length);
        }

        private void ViewPopupWay(object sender, MouseWheelEventArgs e)
        {
            PopupWay.IsOpen = true;

            if (work.type.MetalDrop.SelectedItem is Metal metal && metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
                WayPrice.Text = $"Цена метра резки\n{MainWindow.M.MetalDict[metal.Name][work.type.S].Item1} руб";
        }

        private void ViewPopupPinhole(object sender, MouseWheelEventArgs e)
        {
            PopupPinhole.IsOpen = true;

            if (work.type.MetalDrop.SelectedItem is Metal metal && metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
                PinholePrice.Text = $"Цена прокола\n{MainWindow.M.MetalDict[metal.Name][work.type.S].Item2} руб";
        }
    }

    [Serializable]
    public class LaserItem
    {
        public string? sheetSize;
        public int sheets, pinholes;
        public float way, mass, price;
        [OptionalField]
        public byte[]? imageBytes;
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
