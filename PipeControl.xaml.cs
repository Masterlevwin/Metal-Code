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
using System.Text.RegularExpressions;
using System.Windows.Media;

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

        private float mass;
        public float Mass
        {
            get => mass;
            set => mass = value;
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

        private bool isMassPipe;
        public bool IsMassPipe
        {
            get => isMassPipe;
            set => isMassPipe = value;
        }

        public enum TubeType
        {
            rect,
            round,
            circle,
            square,
            rod,
            channel,
            corner,
            hbeam,
            freeform
        }

        public TubeType Tube { get; set; }
        public List<PartControl>? Parts { get; set; }
        public PartsControl? PartsControl { get; set; }
        public List<Part>? PartDetails { get; set; } = new();
        public List<LaserItem>? Items { get; set; } = new();

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

            BtnEnabled();       // проверяем типовую деталь: если не труба или балка, делаем кнопку неактивной и наоборот

            SetMold($"{work.type.L * work.type.Count * 0.95f / 1000}");      //переносим погонные метры из типовой детали
        }

        private void SetMold(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetMold(tBox.Text);
        }
        public void SetMold(string _mold)
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
            BtnEnabled();

            if (Mold == 0 || Way == 0 || Pinhole == 0) return;
            if (work.type.MetalDrop.SelectedItem is not Metal metal) return;

            float price = 0;

            float destiny = MainWindow.M.CorrectDestiny(work.type.S);    //получаем расчетную толщину

            if (metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(destiny))
                price = Mold * MainWindow.M.MetalDict[metal.Name][destiny].Item3 * 0.7f
                    + Way * MainWindow.M.MetalDict[metal.Name][destiny].Item1
                    + Pinhole * MainWindow.M.MetalDict[metal.Name][destiny].Item2 * 3;

            // проверяем стоимость материала
            float _result = (float)Math.Round((work.type.det.Detail.IsComplect ? 1 : work.type.Count) *
                (work.type.ExtraResult > 0 ? work.type.ExtraResult : work.type.Price * work.type.Mass), 2);

            // стоимость резки должна быть не ниже 10% от стоимости материала
            if (_result > 0 && (price / _result) < 0.1f) price = _result * 0.1f;

            // стоимость резки трубы должна быть не ниже минимальной
            if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

            work.SetResult(price, false);
        }

        private void BtnEnabled()
        {
            if (work.type.TypeDetailDrop.SelectedItem is TypeDetail type && type.Name == "Лист металла") CutBtn.IsEnabled = false;
            else CutBtn.IsEnabled = true;
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
                w.propsList.Add($"{Tube}");
                w.propsList.Add($"{IsMassPipe}");

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
                if (w.propsList.Count > 3 && Enum.TryParse(w.propsList[3], out TubeType tube)) Tube = tube;
                if (w.propsList.Count > 4 && bool.TryParse(w.propsList[4], out bool isMassPipe)) SetMassPipe(isMassPipe);
            }
        }

        private void SetToolTipForMold(object sender, ToolTipEventArgs e)
        {
            float destiny = MainWindow.M.CorrectDestiny(work.type.S);

            if (sender is TextBox box && work.type.MetalDrop.SelectedItem is Metal metal
                && metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(destiny))
                box.ToolTip = $"Длина трубы, пог м\n(цена резки пог м - {MainWindow.M.MetalDict[metal.Name][destiny].Item3 * 0.7f} руб)";
        }

        private void SetToolTipForWay(object sender, ToolTipEventArgs e)
        {
            float destiny = MainWindow.M.CorrectDestiny(work.type.S);

            if (sender is TextBox box && work.type.MetalDrop.SelectedItem is Metal metal
                && metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(destiny))
                box.ToolTip = $"Длина пути резки, м\n(цена метра резки - {MainWindow.M.MetalDict[metal.Name][destiny].Item1} руб)";
        }

        private void SetToolTipForPinhole(object sender, ToolTipEventArgs e)
        {
            float destiny = MainWindow.M.CorrectDestiny(work.type.S);

            if (sender is TextBox box && work.type.MetalDrop.SelectedItem is Metal metal
                && metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(destiny))
                box.ToolTip = $"Количество проколов, шт\n(цена прокола - {MainWindow.M.MetalDict[metal.Name][destiny].Item2 * 3} руб)";
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

            //определяем деталь, в которой загрузили раскладки, как комплект труб
            if (!work.type.det.Detail.IsComplect) work.type.det.IsComplectChanged("Комплект труб");

            for (int i = 0; i < paths.Length; i++)
            {
                using FileStream stream = File.Open(paths[i], FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTableCollection tables = result.Tables;

                if (i == 0)
                {
                    Parts = PartList(tables, paths[i]); // формируем список элементов PartControl
                    SetImagesForParts(stream);          // устанавливаем поле Part.ImageBytes для каждой детали
                    PartsControl = new(this, Parts);    // создаем форму списка нарезанных деталей
                    AddPartsTab();                      // добавляем вкладку в "Список нарезанных деталей"
                    SetTotalProperties();               // определяем общую массу и общую длину нарезанных труб
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
                        _pipe.Parts = _pipe.PartList(tables, paths[i]);
                        _pipe.SetImagesForParts(stream);
                        _pipe.PartsControl = new(_pipe, _pipe.Parts);
                        _pipe.AddPartsTab();
                        _pipe.SetTotalProperties();
                    }
                }
            }
        }

        public void AddPartsTab(ProductWindow? clon = null)                 //метод добавления вкладки для PartsControl
        {
            TabItem.Header = new TextBlock { Text = $"(ТР) s{work.type.S} {work.type.MetalDrop.Text} ({PartDetails?.Sum(x => x.Count)} шт)" };    // установка заголовка вкладки
            TabItem.Content = PartsControl;                                 // установка содержимого вкладки

            if (clon is not null) clon.PartsTab.Items.Add(TabItem);
            else MainWindow.M.PartsTab.Items.Add(TabItem);
        }

        private void RemovePartsTab(object sender, RoutedEventArgs e)       //метод удаления вкладки нарезанных деталей этой резки,
                                                                            //вызывается по событию PipeControl.Unloaded
        {
            MainWindow.M.PartsTab.Items.Remove(TabItem);
        }

        public List<PartControl> PartList(DataTableCollection? tables = null, string? path = null)
        {
            if (tables is not null && $"{tables[0].Rows[0].ItemArray[0]}".Contains("ИН сечения")) return PartList(true, tables, path);

            List<PartControl> _parts = new();

            if (tables != null)
            {
                PartDetails?.Clear();

                string _tube = $"{tables[0].Rows[1].ItemArray[1]}";

                Regex rect = new(@"[+-]?((\d+\,?\d*)|(\,\d+))|((\d+\.?\d*)|(\.\d+))");

                List<Match> matchesTube = rect.Matches(_tube).ToList();

                if (matchesTube.Count > 0)
                {
                    if (_tube.Contains("Круглая"))
                    {
                        foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Труба круглая")
                            {
                                work.type.TypeDetailDrop.SelectedItem = t;
                                break;
                            }
                        work.type.A = work.type.B = MainWindow.Parser(matchesTube[0].Value) * 2;
                        Tube = TubeType.round;
                    }
                    else if (_tube.Contains("Квадратная"))
                    {
                        work.type.A = work.type.B = MainWindow.Parser(matchesTube[0].Value);
                        Tube = TubeType.square;
                    }
                    else if (_tube.Contains("Швеллер") || _tube.Contains("Труба U"))
                    {
                        foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Швеллер П")
                            {
                                work.type.TypeDetailDrop.SelectedItem = t;

                                string _match = $"{MainWindow.Parser(matchesTube[0].Value) / 10}".Replace(',', '.') + 'П';

                                foreach (string s in work.type.SortDrop.Items)
                                    if (s == _match)
                                    {
                                        work.type.SortDrop.SelectedItem = s;
                                        break;
                                    }
                                break;
                            }
                        Tube = TubeType.channel;
                    }
                    else if (_tube.Contains("равнополочный") || _tube.Contains("труба(L)"))
                    {
                        foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Уголок равнополочный")
                            {
                                work.type.TypeDetailDrop.SelectedItem = t;

                                string _match = $"{MainWindow.Parser(matchesTube[0].Value)}".Replace(',', '.');

                                foreach (string s in work.type.SortDrop.Items)
                                    if (s == _match)
                                    {
                                        work.type.SortDrop.SelectedItem = s;
                                        break;
                                    }
                                break;
                            }
                        Tube = TubeType.corner;
                    }
                    else if (_tube.Contains("неравнополочный"))
                    {
                        foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Уголок неравнополочный")
                            {
                                work.type.TypeDetailDrop.SelectedItem = t;

                                string _match = $"{MainWindow.Parser(matchesTube[0].Value)}".Replace(',', '.');

                                foreach (string s in work.type.SortDrop.Items)
                                    if (s == _match)
                                    {
                                        work.type.SortDrop.SelectedItem = s;
                                        break;
                                    }
                                break;
                            }
                        Tube = TubeType.freeform;
                    }
                    else if (_tube.Contains("Двутавр"))
                    {
                        foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Двутавр парал")
                            {
                                work.type.TypeDetailDrop.SelectedItem = t;

                                //в данном случае берем matchesTube[1].Value за основу для анализа сорта двутавра
                                string _match = $"{MainWindow.Parser(matchesTube[1].Value)}".Replace(',', '.');

                                foreach (string str in work.type.Kinds.Keys)
                                {
                                    if (work.type.Kinds[str].Item1.Contains(_match))    //проверяем первый итем словаря сортов
                                    {                                                   //если находим совпадение, устанавливаем сорт заготовки
                                        foreach (string s in work.type.SortDrop.Items)
                                            if (s == str)
                                            {
                                                work.type.SortDrop.SelectedItem = s;
                                                break;
                                            }
                                        break;
                                    }
                                }
                                break;
                            }
                        Tube = TubeType.hbeam;
                    }
                    else
                    {
                        work.type.A = MainWindow.Parser(matchesTube[0].Value);
                        work.type.B = MainWindow.Parser(matchesTube[1].Value);
                        Tube = TubeType.rect;
                    }
                }
                else MainWindow.M.StatusBegin("Не удалось определить размеры трубы");

                int ndx = $"{tables[2].Rows[1].ItemArray[2]}".IndexOf(':');
                work.type.SetCount((int)MainWindow.Parser($"{tables[2].Rows[1].ItemArray[2]}".Substring(ndx + 1)));             //Кол.сечений
                ndx = $"{tables[2].Rows[1].ItemArray[3]}".IndexOf(':');
                SetPinhole($"{tables[2].Rows[1].ItemArray[3]}".Substring(ndx + 1));                                             //Контур
                ndx = $"{tables[2].Rows[1].ItemArray[4]}".IndexOf(':');
                Way = (float)Math.Ceiling(MainWindow.Parser($"{tables[2].Rows[1].ItemArray[4]}".Substring(ndx + 1)) / 1000);    //Длина резки сечения(mm)

                if (Items?.Count > 0) Items.Clear();

                for (int j = 3; j < tables[2].Rows.Count; j++)                                  //заполняем список труб
                {
                    if (tables[2].Rows[j] == null) break;

                    LaserItem? item = new();
                    item.sheets = (int)MainWindow.Parser($"{tables[2].Rows[j].ItemArray[1]}");  //Кол-во

                    string lengthTube = $"{tables[2].Rows[j].ItemArray[3]}";                    //Длина трубы(mm)

                    if (lengthTube.Contains(',')) lengthTube = lengthTube.Remove(lengthTube.IndexOf(','));
                    else if (lengthTube.Contains('.')) lengthTube = lengthTube.Remove(lengthTube.IndexOf('.'));

                    item.sheetSize = lengthTube;

                    Items?.Add(item);
                }

                //устанавливаем толщину заготовки, если она равна нулю
                if (work.type.S == 0 && path != null)
                {
                    Regex _destiny = new(@"x[+-]?((\d+\.?\d*)|(\.\d+))", RegexOptions.IgnoreCase);
                    List<Match> matches = _destiny.Matches(path.ToLower().Replace('х', 'x')).ToList();
                    if (matches.Count > 0) work.type.S = MainWindow.Parser(matches[^1].Value.Trim('x'));
                }

                //устанавливаем материал заготовки
                foreach (Metal metal in work.type.MetalDrop.Items)
                    if (path != null && metal.Name != null && path.ToLower().Contains(metal.Name))
                        work.type.MetalDrop.SelectedItem = metal;

                for (int j = 3; j < tables[0].Rows.Count; j++)
                {
                    if ($"{tables[0].Rows[j].ItemArray[1]}" == " ") break;

                    //если строка не пуста, инициализируем новую деталь
                    Part part = new()
                    {
                        Title = $"{tables[0].Rows[j].ItemArray[1]}",
                        Description = "ТР",
                        Accuracy = $"H12/h12 +-IT 12/2"
                    };

                    //определяем количество деталей
                    string? _count = tables[0].Rows[j].ItemArray[2]?.ToString();

                    if (_count != null && _count.Contains('/')) part.Count = (int)MainWindow.Parser(_count.Split('/')[0]);

                    if (part.Count > 0)     //если количество деталей успешно определено, далее устанавливаем
                                            //толщину, материал, массу и площадь окрашиваемой поверхности детали
                    {
                        part.Destiny = work.type.S;
                        part.Metal = work.type.MetalDrop.Text;
                        part.Way = (float)Math.Round(MainWindow.Parser($"{tables[0].Rows[j].ItemArray[3]}"), 3);       //Длина нарезанной трубы

                        if (work.type.MetalDrop.SelectedItem is Metal metal && work.type.S > 0)
                            switch (Tube)
                            {
                                case TubeType.rect:
                                    part.Mass = (float)Math.Round(0.0157f * work.type.S * (work.type.A + work.type.B - 2.86f * work.type.S) * part.Way * metal.Density / 7850, 3);
                                    part.PropsDict[100] = new() { $"{part.Way * (work.type.A + work.type.B) * 2 / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.round:
                                    part.Mass = (float)Math.Round(Math.PI * work.type.S * (work.type.A - work.type.S) * part.Way * metal.Density / 1000000, 3);
                                    part.PropsDict[100] = new() { $"{part.Way * work.type.A * Math.PI / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.square:
                                    part.Mass = (float)Math.Round(0.0157f * work.type.S * (work.type.A + work.type.B - 2.86f * work.type.S) * part.Way * metal.Density / 7850, 3);
                                    part.PropsDict[100] = new() { $"{part.Way * (work.type.A + work.type.B) * 2 / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.channel:
                                    part.Mass = (float)Math.Round(work.type.Channels[work.type.SortDrop.SelectedIndex] * part.Way / 1000, 3);
                                    part.PropsDict[100] = new() { $"{work.type.ChannelsSquare[work.type.SortDrop.SelectedIndex] * part.Mass / 1000}", "", $"{part.Way}" };     //площадь окрашиваемой поверхности
                                    break;
                                case TubeType.corner:
                                    part.Mass = (float)Math.Round((work.type.S * (work.type.A + work.type.A - work.type.S) + 0.2146f * (work.type.Corners[work.type.SortDrop.SelectedIndex].Item1
                                        * work.type.Corners[work.type.SortDrop.SelectedIndex].Item1 - 2 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2
                                        * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2)) * part.Way * metal.Density / 1000000, 3);
                                    part.PropsDict[100] = new() { $"{part.Way * work.type.S * (work.type.A + work.type.A - work.type.S) / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.freeform:
                                    part.Mass = (float)Math.Round((work.type.S * (work.type.A + work.type.B - work.type.S) + 0.2146f * (work.type.Corners[work.type.SortDrop.SelectedIndex].Item1
                                        * work.type.Corners[work.type.SortDrop.SelectedIndex].Item1 - 2 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2
                                        * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2)) * part.Way * metal.Density / 1000000, 3);
                                    part.PropsDict[100] = new() { $"{part.Way * work.type.S * (work.type.A + work.type.B - work.type.S) / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.hbeam:
                                    part.Mass = (float)Math.Round(work.type.BeamDict[work.type.TypeDetailDrop.Text][work.type.SortDrop.SelectedIndex].Item1 * part.Way / 1000, 3);
                                    part.PropsDict[100] = new() { $"{work.type.BeamDict[work.type.TypeDetailDrop.Text][work.type.SortDrop.SelectedIndex].Item2 * part.Mass / 1000}", "", $"{part.Way}" };     //площадь окрашиваемой поверхности
                                    break;

                            }
                        else MainWindow.M.StatusBegin("Не удалось определить массу и размеры деталей. Возможно в названии деталей не указана толщина!");

                        if (MainWindow.M.TechItems.Count > 0)
                            foreach (TechItem item in MainWindow.M.TechItems)
                            {
                                if (part.Title.ToLower().Contains(item.NumberName.ToLower()))
                                {
                                    if (item.IsGenerated && !string.IsNullOrEmpty(item.OriginalName)) part.Title = item.OriginalName;
                                    if (!string.IsNullOrEmpty(item.PdfPath)) part.PdfPath = item.PdfPath;
                                    break;
                                }
                            }
                        _parts.Add(new(this, work, part));
                        PartDetails?.Add(part);
                    }
                }

                work.type.L = MainWindow.Parser($"{tables[2].Rows[3].ItemArray[3]}");      //Длина первой трубы(mm)
                SetMold($"{work.type.L * work.type.Count * 0.95f / 1000}");                //переносим погонные метры из типовой детали
            }
            else if (PartDetails?.Count > 0) foreach (Part part in PartDetails) _parts.Add(new(this, work, part));

            return _parts;
        }

        public List<PartControl> PartList(bool b, DataTableCollection? tables = null, string? path = null)
        {
            List<PartControl> _parts = new();

            if (tables != null)
            {
                PartDetails?.Clear();

                work.type.SetCount((int)MainWindow.Parser($"{tables[2].Rows[1].ItemArray[2]}"));                                //Кол.сечений
                SetPinhole($"{tables[2].Rows[1].ItemArray[3]}");                                                                //Контур
                if (float.TryParse($"{tables[2].Rows[1].ItemArray[4]}", out float val)) Way = (float)Math.Ceiling(val / 1000);  //Длина резки сечения(mm)

                if (Items?.Count > 0) Items.Clear();

                //заполняем список труб
                for (int j = 3; j < tables[2].Rows.Count; j++)
                {
                    if (tables[2].Rows[j] == null) break;

                    LaserItem? item = new();
                    item.sheets = (int)MainWindow.Parser($"{tables[2].Rows[j].ItemArray[2]}");  //Кол-во

                    string lengthTube = $"{tables[2].Rows[j].ItemArray[4]}";                    //Длина трубы(mm)

                    if (lengthTube.Contains(',')) lengthTube = lengthTube.Remove(lengthTube.IndexOf(','));
                    else if (lengthTube.Contains('.')) lengthTube = lengthTube.Remove(lengthTube.IndexOf('.'));

                    item.sheetSize = lengthTube;

                    if (MainWindow.Parser(item.sheetSize) > 6000)
                    {
                        work.type.L_prop.BorderBrush = new SolidColorBrush(Colors.Red);
                        work.type.L_prop.BorderThickness = new Thickness(2);
                        MessageBox.Show("Труба не должна быть длиннее 6000 мм!");
                    }

                    Items?.Add(item);
                }

                //заполняем список деталей
                for (int i = 0; i < tables[0].Rows.Count; i++)
                {
                    if (tables[0].Rows[i] == null) continue;

                    if (tables[0].Rows[i].ItemArray[2]?.ToString() == "Название деталей")
                    {
                        for (int j = i + 1; j < tables[0].Rows.Count; j++)
                        {
                            if ($"{tables[0].Rows[j].ItemArray[2]}" == " ") break;

                            Part part = new()
                            {
                                Title = $"{tables[0].Rows[j].ItemArray[2]}",
                                Description = "ТР",
                                Accuracy = $"H12/h12 +-IT 12/2"
                            };

                            if (float.TryParse($"{tables[0].Rows[j].ItemArray[4]}", out float w)) part.Way = (float)Math.Round(w, 3);

                            string? _count = tables[0].Rows[j].ItemArray[3]?.ToString();
                            if (_count != null && _count.Contains('/')) part.Count = (int)MainWindow.Parser(_count.Split('/')[0]);

                            if (part.Count > 0)
                            {
                                if (MainWindow.M.TechItems.Count > 0)
                                    foreach (TechItem item in MainWindow.M.TechItems)
                                    {
                                        if (part.Title.ToLower().Contains(item.NumberName.ToLower()))
                                        {
                                            if (item.IsGenerated && !string.IsNullOrEmpty(item.OriginalName)) part.Title = item.OriginalName;
                                            if (!string.IsNullOrEmpty(item.PdfPath)) part.PdfPath = item.PdfPath;
                                            break;
                                        }
                                    }
                                PartDetails?.Add(part);
                            }
                        }
                        break;
                    }
                }

                //устанавливаем толщину заготовки, если она равна нулю
                if (work.type.S == 0 && path != null)
                {
                    Regex _destiny = new(@"x[+-]?((\d+\.?\d*)|(\.\d+))", RegexOptions.IgnoreCase);
                    List<Match> _matches = _destiny.Matches(path.ToLower().Replace('х', 'x').Replace(',', '.')).ToList();
                    if (_matches.Count > 0) work.type.S = MainWindow.Parser(_matches[^1].Value.Trim('x'));
                }

                float destiny = work.type.S;    //кэшируем ссылку на толщину заготовки, потому что в дальнейшем
                            //при установке сорта заготовки толщина сбрасывается в ноль, и ее нужно установить вновь

                //устанавливаем материал заготовки
                foreach (Metal metal in work.type.MetalDrop.Items)
                    if (path != null && metal.Name != null && path.ToLower().Contains(metal.Name))
                        work.type.MetalDrop.SelectedItem = metal;

                //определяем вид заготовки
                string _tube = $"{tables[0].Rows[1].ItemArray[1]}";
                Regex rect = new(@"[+-]?((\d+\,?\d*)|(\,\d+))|((\d+\.?\d*)|(\.\d+))");
                List<Match> matches = rect.Matches(_tube).ToList();
                if (matches.Count > 0)
                {
                    //профильная труба
                    if (_tube.Contains("Rect tube") || _tube.Contains("Free Form"))
                    {
                        work.type.A = MainWindow.Parser(matches[0].Value);
                        work.type.B = MainWindow.Parser(matches[1].Value);
                        Tube = TubeType.rect;
                    }
                    //круг или круглая труба
                    else if (_tube.Contains("Round tube"))
                    {
                        if (work.type.S == 0)
                        {
                            foreach (TypeDetail t in MainWindow.M.TypeDetails)
                                if (t.Name == "Круг")
                                {
                                    work.type.TypeDetailDrop.SelectedItem = t;
                                    Tube = TubeType.circle;
                                    break;
                                }
                        }
                        else
                        {
                            foreach (TypeDetail _t in MainWindow.M.TypeDetails)
                                if (_t.Name == "Труба круглая")
                                {
                                    work.type.TypeDetailDrop.SelectedItem = _t;
                                    Tube = TubeType.round;
                                    break;
                                }
                        }
                        work.type.A = work.type.B = MainWindow.Parser(matches[0].Value) * 2;
                    }
                    //квадрат или квадратная труба
                    else if (_tube.Contains("Square tube"))
                    {
                        if (work.type.S == 0)
                        {
                            foreach (TypeDetail t in MainWindow.M.TypeDetails)
                                if (t.Name == "Квадрат")
                                {
                                    work.type.TypeDetailDrop.SelectedItem = t;
                                    Tube = TubeType.rod;
                                    break;
                                }
                        }
                        else
                        {
                            foreach (TypeDetail _t in MainWindow.M.TypeDetails)
                                if (_t.Name == "Труба профильная")
                                {
                                    work.type.TypeDetailDrop.SelectedItem = _t;
                                    Tube = TubeType.square;
                                    break;
                                }
                        }
                        work.type.A = work.type.B = MainWindow.Parser(matches[0].Value);
                    }
                    //швеллер
                    else if (_tube.Contains("U tube"))
                    {
                        foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Швеллер П")
                            {
                                work.type.TypeDetailDrop.SelectedItem = t;

                                string _match = $"{MainWindow.Parser(matches[0].Value) / 10}".Replace(',', '.') + 'П';

                                foreach (string s in work.type.SortDrop.Items)
                                    if (s == _match)
                                    {
                                        work.type.SortDrop.SelectedItem = s;
                                        destiny = MainWindow.Parser(work.type.Kinds[$"{work.type.SortDrop.SelectedItem}"].Item3);

                                        work.type.SortDrop.BorderBrush = new SolidColorBrush(Colors.Red);
                                        work.type.SortDrop.BorderThickness = new Thickness(2);
                                        MainWindow.M.StatusBegin($"Обратите внимание на типы заготовки и их толщины!");

                                        break;
                                    }
                                break;
                            }
                        Tube = TubeType.channel;
                    }
                    //уголки 
                    else if (_tube.Contains("L tube"))
                    {
                        if (matches.Count > 1 && $"{MainWindow.Parser(matches[0].Value)}".Replace(',', '.') == $"{MainWindow.Parser(matches[1].Value)}".Replace(',', '.'))
                        {
                            foreach (TypeDetail t in MainWindow.M.TypeDetails)
                                if (t.Name == "Уголок равнополочный")
                                {
                                    work.type.TypeDetailDrop.SelectedItem = t;

                                    string _match = $"{MainWindow.Parser(matches[0].Value)}".Replace(',', '.');

                                    foreach (string s in work.type.SortDrop.Items)
                                        if (s == _match)
                                        {
                                            work.type.SortDrop.SelectedItem = s;
                                            break;
                                        }
                                    break;
                                }
                            Tube = TubeType.corner;
                        }
                        else if (matches.Count > 1)
                        {
                            foreach (TypeDetail t in MainWindow.M.TypeDetails)
                                if (t.Name == "Уголок неравнополочный")
                                {
                                    work.type.TypeDetailDrop.SelectedItem = t;

                                    string _match = $"{MainWindow.Parser(matches[0].Value)}".Replace(',', '.');

                                    foreach (string s in work.type.SortDrop.Items)
                                        if (s == _match)
                                        {
                                            work.type.SortDrop.SelectedItem = s;
                                            break;
                                        }
                                    break;
                                }
                            Tube = TubeType.freeform;
                        }
                    }
                    //двутавр парал
                    else if (_tube.Contains("H-beam"))
                    {
                        foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Двутавр парал")
                            {
                                work.type.TypeDetailDrop.SelectedItem = t;

                                //в данном случае берем matchesTube[1].Value за основу для анализа сорта двутавра
                                string _match = $"{MainWindow.Parser(matches[1].Value)}".Replace(',', '.');

                                foreach (string str in work.type.Kinds.Keys)
                                {
                                    if (work.type.Kinds[str].Item1.Contains(_match))    //проверяем первый итем словаря сортов
                                    {                                                   //если находим совпадение, устанавливаем сорт заготовки
                                        foreach (string s in work.type.SortDrop.Items)
                                            if (s == str)
                                            {
                                                work.type.SortDrop.SelectedItem = s;
                                                break;
                                            }
                                        break;
                                    }
                                }
                                break;
                            }
                        Tube = TubeType.hbeam;
                    }

                    SizesValidate();        //проверяем размеры проката
                }
                else MainWindow.M.StatusBegin("Не удалось определить размеры трубы");

                if (float.TryParse($"{tables[2].Rows[3].ItemArray[4]}", out float l)) work.type.L = l;      //Длина первой трубы(mm)
                SetMold($"{work.type.L * work.type.Count * 0.95f / 1000}");             //переносим погонные метры из типовой детали

                work.type.S = destiny;      //возвращаем сохраненную толщину

                //дополняем свойства деталей рассчитанными значениями
                if (PartDetails?.Count > 0)
                    foreach (Part part in PartDetails)
                    {
                        part.Destiny = work.type.S;
                        part.Metal = work.type.MetalDrop.Text;

                        if (work.type.MetalDrop.SelectedItem is Metal metal && work.type.S >= 0)
                        {
                            switch (Tube)
                            {
                                case TubeType.rect:
                                    part.Mass = (float)Math.Round(0.0157f * work.type.S * (work.type.A + work.type.B - 2.86f * work.type.S) * part.Way * metal.Density / 7850, 3);
                                    part.PropsDict[100] = new() { $"{part.Way * (work.type.A + work.type.B) * 2 / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.round:
                                    part.Mass = (float)Math.Round(Math.PI * work.type.S * (work.type.A - work.type.S) * part.Way * metal.Density / 1000000, 3);
                                    part.PropsDict[100] = new() { $"{part.Way * work.type.A * Math.PI / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.circle:
                                    part.Mass = (float)Math.Round(Math.PI * work.type.A * work.type.A * part.Way / 4 * metal.Density / 1000000, 3);
                                    part.PropsDict[100] = new() { $"{2 * part.Way * work.type.A * Math.PI / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.square:
                                    part.Mass = (float)Math.Round(0.0157f * work.type.S * (work.type.A + work.type.B - 2.86f * work.type.S) * part.Way * metal.Density / 7850, 3);
                                    part.PropsDict[100] = new() { $"{part.Way * (work.type.A + work.type.B) * 2 / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.rod:
                                    part.Mass = (float)Math.Round(work.type.A * work.type.A * part.Way * metal.Density / 1000000, 3);
                                    part.PropsDict[100] = new() { $"{2 * (part.Way * work.type.A + part.Way * work.type.B + work.type.A * work.type.B) / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.channel:
                                    part.Mass = (float)Math.Round(work.type.Channels[work.type.SortDrop.SelectedIndex] * part.Way / 1000, 3);
                                    part.PropsDict[100] = new() { $"{work.type.ChannelsSquare[work.type.SortDrop.SelectedIndex] * part.Mass / 1000}", "", $"{part.Way}" };     //площадь окрашиваемой поверхности
                                    break;
                                case TubeType.corner:
                                    part.Mass = (float)Math.Round((work.type.S * (work.type.A + work.type.A - work.type.S) + 0.2146f * (work.type.Corners[work.type.SortDrop.SelectedIndex].Item1
                                        * work.type.Corners[work.type.SortDrop.SelectedIndex].Item1 - 2 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2
                                        * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2)) * part.Way * metal.Density / 1000000, 3);
                                    part.PropsDict[100] = new() { $"{part.Way * work.type.S * (work.type.A + work.type.A - work.type.S) / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.freeform:
                                    part.Mass = (float)Math.Round((work.type.S * (work.type.A + work.type.B - work.type.S) + 0.2146f * (work.type.Corners[work.type.SortDrop.SelectedIndex].Item1
                                        * work.type.Corners[work.type.SortDrop.SelectedIndex].Item1 - 2 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2
                                        * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2)) * part.Way * metal.Density / 1000000, 3);
                                    part.PropsDict[100] = new() { $"{part.Way * work.type.S * (work.type.A + work.type.B - work.type.S) / 1000000}", "", $"{part.Way}" };
                                    break;
                                case TubeType.hbeam:
                                    part.Mass = (float)Math.Round(work.type.BeamDict[work.type.TypeDetailDrop.Text][work.type.SortDrop.SelectedIndex].Item1 * part.Way / 1000, 3);
                                    part.PropsDict[100] = new() { $"{work.type.BeamDict[work.type.TypeDetailDrop.Text][work.type.SortDrop.SelectedIndex].Item2 * part.Mass / 1000}", "", $"{part.Way}" };     //площадь окрашиваемой поверхности
                                    break;
                            }
                            _parts.Add(new(this, work, part));
                        }
                        else MainWindow.M.StatusBegin("Не удалось определить массу и размеры деталей. Возможно в названии деталей не указана толщина!");
                    }
            }
            else if (PartDetails?.Count > 0) foreach (Part part in PartDetails) _parts.Add(new(this, work, part));

            return _parts;
        }

        private void SizesValidate()
        {
            if (work.type.A < 10)
            {
                work.type.A_prop.BorderBrush = new SolidColorBrush(Colors.Red);
                work.type.A_prop.BorderThickness = new Thickness(2);
                MessageBox.Show($"Минимальный размер трубы - 10 мм!");
            }

            if (work.type.B < 10)
            {
                work.type.B_prop.BorderBrush = new SolidColorBrush(Colors.Red);
                work.type.B_prop.BorderThickness = new Thickness(2);
                MessageBox.Show($"Минимальный размер трубы - 10 мм!");
            }

            if (work.type.A > 230)
            {
                work.type.A_prop.BorderBrush = new SolidColorBrush(Colors.Red);
                work.type.A_prop.BorderThickness = new Thickness(2);
                MessageBox.Show($"Максимальный размер трубы - 230 мм!");
            }

            if (work.type.B > 230)
            {
                work.type.B_prop.BorderBrush = new SolidColorBrush(Colors.Red);
                work.type.B_prop.BorderThickness = new Thickness(2);
                MessageBox.Show($"Максимальный размер трубы - 230 мм!");
            }
        }

        private void SetMassPipe(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox box && box.IsChecked is not null) SetMassPipe((bool)box.IsChecked);
        }
        public void SetMassPipe(bool isMassPipe = false)
        {
            IsMassPipe = isMassPipe;
            SetTotalProperties();
        }

        public void SetTotalProperties()
        {
            Mass = MassTotal = WayTotal = 0;

            //если не требуется посчитать массу нарезанных труб, считаем массу полных заготовок
            if (!IsMassPipe && work.type.MetalDrop.SelectedItem is Metal metal && work.type.S >= 0)
                switch (Tube)
                {
                    case TubeType.rect:
                        Mass = (float)Math.Round(0.0157f * work.type.S * (work.type.A + work.type.B - 2.86f * work.type.S) * work.type.L * work.type.Count * metal.Density / 7850, 3);
                        break;
                    case TubeType.round:
                        Mass = (float)Math.Round(Math.PI * work.type.S * (work.type.A - work.type.S) * work.type.L * work.type.Count * metal.Density / 1000000, 3);
                        break;
                    case TubeType.circle:
                        Mass = (float)Math.Round(Math.PI * work.type.A * work.type.A * work.type.L / 4 * work.type.Count * metal.Density / 1000000, 3);
                        break;
                    case TubeType.square:
                        Mass = (float)Math.Round(0.0157f * work.type.S * (work.type.A + work.type.B - 2.86f * work.type.S) * work.type.L * work.type.Count * metal.Density / 7850, 3);
                        break;
                    case TubeType.rod:
                        Mass = (float)Math.Round(work.type.A * work.type.A * work.type.L * work.type.Count * metal.Density / 1000000, 3);
                        break;
                    case TubeType.channel:
                        Mass = (float)Math.Round(work.type.Channels[work.type.SortDrop.SelectedIndex] * work.type.L * work.type.Count / 1000, 3);
                        break;
                    case TubeType.corner:
                        Mass = (float)Math.Round((work.type.S * (work.type.A + work.type.A - work.type.S) + 0.2146f * (work.type.Corners[work.type.SortDrop.SelectedIndex].Item1
                            * work.type.Corners[work.type.SortDrop.SelectedIndex].Item1 - 2 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2
                            * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2)) * work.type.L * work.type.Count * metal.Density / 1000000, 3);
                        break;
                    case TubeType.freeform:
                        Mass = (float)Math.Round((work.type.S * (work.type.A + work.type.B - work.type.S) + 0.2146f * (work.type.Corners[work.type.SortDrop.SelectedIndex].Item1
                            * work.type.Corners[work.type.SortDrop.SelectedIndex].Item1 - 2 * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2
                            * work.type.Corners[work.type.SortDrop.SelectedIndex].Item2)) * work.type.L * work.type.Count * metal.Density / 1000000, 3);
                        break;
                    case TubeType.hbeam:
                        Mass = (float)Math.Round(work.type.BeamDict[work.type.TypeDetailDrop.Text][work.type.SortDrop.SelectedIndex].Item1 * work.type.L * work.type.Count / 1000, 3);
                        break;

                }

            if (PartDetails?.Count > 0) foreach (Part part in PartDetails)
                {
                    if (IsMassPipe) Mass += part.Mass * part.Count;     //если требуется рассчитать материал ТОЛЬКО нарезанных труб
                    MassTotal += part.Mass * part.Count;                //масса всех нарезанных труб
                    WayTotal += part.Way * part.Count;                  //путь резки всех нарезанных труб
                }

            //если требуется рассчитать материал ТОЛЬКО нарезанных труб
            if (IsMassPipe && PartDetails?.Count > 0)
            {
                float _ratio = Mass / MassTotal;                        //получаем коэффициент разницы между текущим значением массы и массой нарезанных труб
                foreach (Part part in PartDetails) part.Mass *= _ratio; //выравниваем массу каждого кусочка согласно коэффициенту
            }

            work.type.MassCalculate();          // обновляем значение массы заготовки
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
                    PartDetails[i].ImageBytes = pictures[0].Image.ImageBytes;   //для каждой детали записываем массив байтов соответствующей картинки
                                                                                //в случае с трубами на данный момент картинка одна и та же!
        }
    }
}