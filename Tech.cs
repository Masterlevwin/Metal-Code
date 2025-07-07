using ExcelDataReader;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Path = System.IO.Path;

namespace Metal_Code
{
    public class Tech
    {
        public string ExcelFile;                            //путь к файлу
        public int CountTechItems = 0;                      //кол-во строк заявки
        public List<TechItem> TechItems = new();            //список полученных объектов из строк файла
        public List<TechItem> FoundItems = new();           //список найденных строк
        public IEnumerable<string> DirMaterials = null!;    //коллекция папок материалов

        public Tech(string path) { ExcelFile = path; }

        public string Run(bool createOffer = false)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            string notify = $"Не удается прочитать файл заявки";

            try
            {
                //преобразуем открытый Excel-файл в DataTable для парсинга
                using FileStream stream = File.Open(ExcelFile, FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTable table = result.Tables[0];

                int countAssembly = 1;      //количество комплектов
                if ($"{table.Rows[^1].ItemArray[4]}" == "Кол-во комплектов" && $"{table.Rows[^1].ItemArray[5]}" is not null && ((int)MainWindow.Parser($"{table.Rows[^1].ItemArray[5]}") > 0))
                    countAssembly = (int)MainWindow.Parser($"{table.Rows[^1].ItemArray[5]}");

                //перебираем строки таблицы и заполняем список объектами TechItem
                for (int i = 2; i < table.Rows.Count; i++)
                {
                    if ($"{table.Rows[i].ItemArray[1]}" is null || $"{table.Rows[i].ItemArray[1]}" == "") continue;

                    TechItem techItem = new(
                        $"{table.Rows[i].ItemArray[1]}",        //номер чертежа
                        $"{table.Rows[i].ItemArray[2]}",        //размеры
                        $"{table.Rows[i].ItemArray[3]}",        //материал
                        $"{table.Rows[i].ItemArray[4]}",        //толщина
                        $"{(int)MainWindow.Parser($"{table.Rows[i].ItemArray[5]}") * countAssembly}",  //количество
                        $"{table.Rows[i].ItemArray[6]}",        //маршрут
                        $"{table.Rows[i].ItemArray[7]}",        //давальческий материал      
                        $"{table.Rows[i].ItemArray[8]}",        //оригинальное наименование от заказчика
                        $"{table.Rows[i].ItemArray[9]}",        //путь к файлу модели
                        $"{table.Rows[i].ItemArray[10]}");      //сгенерирован ли номер чертежа
                    TechItems.Add(techItem);
                }
                CountTechItems = TechItems.Count;

                stream.Close();

                //сначала формируем предварительный расчет, для этого создаем заготовки и работы
                if (createOffer)
                {
                    string message = "";
                    foreach (TechItem techItem in TechItems)
                        if (techItem.Sizes is null || techItem.Sizes == "") message += $"\n{techItem.NumberName}";
                    if (message != "") MessageBox.Show(message.Insert(0, "Не удалось создать быстрый расчет,\n" +
                        "так как не удалось получить габариты следующих деталей:"));
                    else
                    {
                        try { CreateExpressOffer(); }
                        catch (Exception ex) { MessageBox.Show($"Не удалось создать быстрый расчет.\n{ex.Message}"); }
                    }
                }

                //затем переопределяем материалы и толщины для формирования имен деталей
                foreach (TechItem techItem in TechItems)
                {
                    if (techItem.Material.ToLower().Contains("al") || techItem.Material.ToLower().Contains("амг2"))
                    {
                        techItem.Material = "";
                        techItem.Destiny = $"al{techItem.Destiny}";
                    }
                    else if (techItem.Material.ToLower().Contains("амг") || techItem.Material.ToLower().Contains("д16")) techItem.Destiny = $"al{techItem.Destiny}";
                    else if (techItem.Material.ToLower().Contains("br"))
                    {
                        techItem.Material = "";
                        techItem.Destiny = $"br{techItem.Destiny}";
                    }
                    else if (techItem.Material.ToLower().Contains("cu"))
                    {
                        techItem.Material = "";
                        techItem.Destiny = $"cu{techItem.Destiny}";
                    }
                    else techItem.Destiny = $"s{techItem.Destiny}";

                    if (techItem.Destiny.ToLower().Contains('x') || techItem.Destiny.ToLower().Contains('х'))
                        techItem.Destiny = techItem.Destiny.TrimStart('s');
                }

                //получаем коллекцию уникальных строк на основе группировки по материалу и толщине
                DirMaterials = TechItems.GroupBy(m => new { m.Material, m.Destiny, m.HasMaterial })
                    .Select(g => $"{g.Key.Destiny} {g.Key.Material} {g.Key.HasMaterial}");

                //получаем коллекцию, разбитую на группы по работам
                var works = TechItems.GroupBy(w => w.Route);

                string[] extensions = { "pdf", "tiff", "gpeg", "gpg" };     //основные расширения чертежей

                //сортируем pdf-файлы по папкам работ
                foreach (var work in works)
                    if (work.Key != "")
                    {
                        if (work.Key.ToLower().Contains("гиб"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Гибка");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }
                        
                        if (work.Key.ToLower().Contains("вальц"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Вальцовка");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }

                        if (work.Key.ToLower().Contains("фрез"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Фрезеровка");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }
                        else if (work.Key.ToLower().Contains("рез"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Резьба");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }

                        if (work.Key.ToLower().Contains("зен"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Зенковка");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }

                        if (work.Key.ToLower().Contains("зак"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Заклепки");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }

                        if (work.Key.ToLower().Contains("свер"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Сверловка");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }

                        if (work.Key.ToLower().Contains("свар"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Сварка");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }

                        if (work.Key.ToLower().Contains("окр"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Окраска");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }

                        if (work.Key.ToLower().Contains("оц"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Оцинковка");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }

                        if (work.Key.ToLower().Contains("лен"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Лентопил");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }

                        if (work.Key.ToLower().Contains("аква"))
                        {
                            DirectoryInfo dirWork = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Аквабластинг");
                            foreach (string extension in extensions) SortExtension(dirWork, extension.ToLower(), work.Select(t => t).ToList());
                        }
                    }

                if (MainWindow.M.TechItems.Count > 0) MainWindow.M.TechItems.Clear();
                MainWindow.M.TechItems = TechItems;

                Create_DirectoriesForCut();     //создаем директории для резки

                ClearDirectories();             //очищаем пустые папки

                //создаем папку "КП" в директории заявки
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "КП");
            }
            catch (Exception ex) { notify = ex.Message; }

            //return RequestReport();
            return $"Обработано {TechItems.Count} строк заявки. Все файлы найдены.";
        }

        private void SortExtension(DirectoryInfo dirMain, string extension, List<TechItem> techItems)
        {
            FoundItems.Clear();      //очищаем список ненайденных файлов

            //создаем папки для каждого материала в директории работы
            foreach (var item in DirMaterials) Directory.CreateDirectory(dirMain + "\\" + $"{item}");

            //получаем коллекцию файлов в папке с Excel-файлом
            string? path = Path.GetDirectoryName(ExcelFile);
            if (path is null) return;

            IEnumerable<string> files = Directory.EnumerateFiles(path, $"*.{extension}", SearchOption.TopDirectoryOnly);

            //сортируем полученные ранее объекты TechItem по соответствующим папкам
            foreach (TechItem techItem in techItems)
            {
                //для этого перебираем файлы
                foreach (string file in files)
                {
                    //и на основе имени без расширения (заменяем неразрывные пробелы, очищаем и нормализуем, игнорируем регистр)
                    string nameFile = Path.GetFileNameWithoutExtension(file).Replace("\u00A0", " ").Trim().Normalize().ToLower();

                    //находим совпадение TechItem и файла по номеру чертежа (при этом заменяем неразрывные пробелы, очищаем и нормализуем, игнорируем регистр и добавляем пробел во избежании ошибки похожих названий)
                    if (nameFile.Insert(nameFile.Length, " ").Contains(techItem.NumberName.Replace("\u00A0", " ").Trim().Normalize().ToLower().Insert(techItem.NumberName.Length, " ")))
                    {
                        //находим сборочные чертежи на основе метки "СБ" и копируем их в корень директории работы
                        if (techItem.NumberName.Contains("СБ"))
                        {
                            TechItems.Remove(techItem);
                            File.Copy(file, dirMain + "\\" + $"{techItem.NumberName}.{extension}");
                        }
                        else
                            //копируем исходный файл в нужный каталог с рабочим именем для нашего производства
                            foreach (var item in DirMaterials)
                                if ($"{techItem.Destiny} {techItem.Material} {techItem.HasMaterial}" == item)
                                {
                                    TechItem? isFounded = FoundItems.FirstOrDefault(x => x.PathToScan == file);
                                    if (isFounded is null)
                                    {
                                        File.Copy(file, dirMain + "\\" + $"{item}".Trim() + "\\"
                                            + $"{techItem.NumberName} n{techItem.Count}" + $".{extension}", true);
                                        techItem.PathToScan = file;
                                        FoundItems.Add(techItem);           //файл найден
                                    }
                                    else MessageBox.Show($"Проверьте файлы с именами {isFounded.NumberName} и {techItem.NumberName}.\n"
                                        + $"Возможна ошибка!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                                    break;
                                }
                        break;
                    }
                }
            }
        }

        private void Create_DirectoriesForCut()     //метод создания директорий для лазера и трубореза
        {
            //создаем папку "Лазер" в директории Excel-файла
            DirectoryInfo dirLaser = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Лазер");

            //создаем папку "Труборез" в директории Excel-файла
            DirectoryInfo dirPipe = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Труборез");

            //создаем папки для каждого материала в директории работы
            foreach (var item in DirMaterials)
            {
                Directory.CreateDirectory(dirLaser + "\\" + $"{item}");
                Directory.CreateDirectory(dirPipe + "\\" + $"{item}");
            }

            //сортируем полученные ранее объекты TechItem по соответствующим папкам
            foreach (TechItem techItem in TechItems)
            {
                //копируем исходный файл в нужный каталог с рабочим именем для нашего производства
                foreach (var item in DirMaterials)
                    if ($"{techItem.Destiny} {techItem.Material} {techItem.HasMaterial}" == item)
                    {
                        if (File.Exists(techItem.PathToModel))
                        {
                            string extension = Path.GetExtension(techItem.PathToModel);
                            string destination = string.Empty;

                            if (extension.ToLower() == ".dxf")
                                destination = $"{techItem.Route}" == "" ?
                                    dirLaser + "\\" + $"{item}".Trim() + "\\"
                                    + $"{techItem.NumberName} {techItem.Material} {techItem.Destiny} n{techItem.Count}" + $".dxf"
                                    : dirLaser + "\\" + $"{item}".Trim() + "\\"
                                    + $"{techItem.NumberName} {techItem.Material} {techItem.Destiny} n{techItem.Count} ({techItem.Route})" + $".dxf";
                            
                            else if (extension.ToLower() == ".igs")
                                destination = $"{techItem.Route}" == "" ?
                                    dirPipe + "\\" + $"{item}".Trim() + "\\"
                                    + $"{techItem.NumberName} {techItem.Material} {techItem.Destiny} n{techItem.Count}" + $".igs"
                                    : dirPipe + "\\" + $"{item}".Trim() + "\\"
                                    + $"{techItem.NumberName} {techItem.Material} {techItem.Destiny} n{techItem.Count} ({techItem.Route})" + $".igs";

                            File.Copy(techItem.PathToModel, destination, true);
                            break;
                        }
                    }
            }
        }

        public void ClearDirectories()              //метод очищения пустых директорий
        {
            string? dirMain = Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile));

            if (dirMain != null)
            {
                string[] dirs = Directory.GetDirectories(dirMain);
                if (dirs?.Length > 0)
                    foreach (var dir in dirs)
                    {
                        string[] dirMaterials = Directory.GetDirectories(dir);
                        foreach (string dm in dirMaterials)
                            if (Directory.GetFileSystemEntries(dm).Length == 0) Directory.Delete(dm);

                        if (Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
                    }
            }
        }

        private string RequestReport()              //метод создания сообщения пользователю
        {
            string notify;

            if (TechItems.Count > 0)
            {
                //открываем заявку для редактирования
                ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                using var requestbook = new ExcelPackage(new FileInfo(ExcelFile));
                ExcelWorksheet? registrysheet = requestbook.Workbook.Worksheets[0];

                //создаем текстовый файл со списком ненайденных файлов
                using StreamWriter stream = new(Path.GetDirectoryName(ExcelFile) + $"\\Не хватает файлов!.txt");

                //перебираем список ненайденных файлов
                foreach (TechItem item in TechItems)
                {
                    //окрашиваем строчку ненайденного файла в желтый цвет
                    foreach (var cell in registrysheet.Cells)
                        if (cell.Value is not null && $"{cell.Value}".Contains(item.NumberName))
                        {
                            cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Yellow);
                        }

                    //добавляем имя файла в текстовый отчет
                    stream.WriteLine(item.NumberName + " " + item.Sizes);
                }
                requestbook.Save();     //сохраняем книгу
                notify = $"Обработано {CountTechItems} строк заявки. Создан перечень недостающих {TechItems.Count} файлов.";
            }
            else notify = $"Обработано {CountTechItems} строк заявки. Все файлы найдены.";

            return notify;
        }

        private void CreateExpressOffer()           //метод создания предварительного расчета
        {
            MainWindow.M.NewProject();
            MainWindow.M.IsExpressOffer = true;

            //группируем детали по материалу и толщине
            var groups = TechItems.Where(d => d.Destiny != "").GroupBy(m => new { m.Material, m.Destiny });

            foreach (var group in groups)
            {
                int sortIndex = 0;

                //определяем раскрой листа группы по умолчанию
                if (group.Key.Material.ToLower() == "br" ||
                    group.Key.Material.ToLower() == "cu")
                    sortIndex = 3;
                else if (group.Key.Material.ToLower().Contains("aisi") ||
                    group.Key.Material.ToLower().Contains("цинк") ||
                    MainWindow.Parser(group.Key.Destiny) < 3)
                    sortIndex = 1;
                else sortIndex = 0;

                TypeDetailControl type = MainWindow.M.DetailControls[0].TypeDetailControls[^1];

                float density = 7.8f;      //плотность материала по умолчанию

                //устанавливаем "Лист металла" и заполняем эту заготовку
                foreach (TypeDetail t in MainWindow.M.TypeDetails)
                    if (t.Name == "Лист металла")
                    {
                        type.TypeDetailDrop.SelectedItem = t;
                        type.CreateSort(sortIndex);
                        type.S = MainWindow.Parser(group.Key.Destiny);

                        //определяем материал заготовки
                        string met = group.Key.Material.ToLower() switch
                        {
                            "br" => "латунь",
                            "cu" => "медь",
                            "al" => "амг2",
                            "" => "ст3",
                            _ => group.Key.Material.ToLower()
                        };
                        foreach (Metal metal in type.MetalDrop.Items)
                            if (metal.Name == met)
                            {
                                type.MetalDrop.SelectedItem = metal;
                                density = metal.Density;
                                break;
                            }
                    }

                //устанавливаем "Лазерная резка" и заполняем эту резку
                foreach (Work w in MainWindow.M.Works)
                    if (w.Name == "Лазерная резка")
                    {
                        type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        if (type.WorkControls[^1].workType is CutControl cut)
                        {
                            List<Part> parts = new();       //список нарезанных деталей

                            float square = 0,   //площадь деталей
                                    way = 0,    //путь резки
                                indent = 10;    //отступ
                            int pinholes = 0;   //проколы

                            //рассчитываем количество листов заготовки, путь резки и проколы, заодно заполняем коллекцию деталей
                            foreach (var item in group)
                            {
                                string[] properties = item.Sizes.ToLower().Split('x');
                                if (properties.Length > 1)
                                {
                                    square += (MainWindow.Parser(properties[0]) + indent)
                                            * (MainWindow.Parser(properties[1]) + indent)
                                            * MainWindow.Parser(item.Count);
                                    way += (MainWindow.Parser(properties[0]) + MainWindow.Parser(properties[1]) + indent)
                                            * 2 * MainWindow.Parser(item.Count);
                                    pinholes += (int)MainWindow.Parser(item.Count);

                                    Part part = new(item.NumberName, (int)MainWindow.Parser(item.Count))
                                    {
                                        Metal = group.Key.Material.ToLower(),
                                        Destiny = MainWindow.Parser(group.Key.Destiny),
                                        Way = (float)Math.Round((MainWindow.Parser(properties[0]) + MainWindow.Parser(properties[1]) + indent) / 500, 3),
                                        Mass = (float)Math.Round((MainWindow.Parser(properties[0]) + indent) * (MainWindow.Parser(properties[1]) + indent) * type.S * density / 1000000, 3)
                                    };
                                    //записываем полученные габариты и саму строку для их отображения в словарь свойств
                                    part.PropsDict[100] = new() { properties[0], properties[1], item.Sizes.ToLower() };
                                    parts.Add(part);
                                }
                            }

                            //получаем количество листов на основе общей площади деталей
                            type.Count = (int)Math.Ceiling(square / type.A / type.B);

                            //алгоритм определения обрезка
                            var sizes = parts.SelectMany(p => p.PropsDict[100]);    //получаем габариты всех деталей как строки
                            List<float> _sizes = new();                             //список габаритов в виде чисел кратных 100
                            foreach (string size in sizes)
                            {
                                float _size = MainWindow.Parser(size);
                                if (_size > 0) _sizes.Add((float)Math.Ceiling(_size / 100) * 100);
                            }
                            _sizes = _sizes.Distinct().OrderDescending().ToList();  //очищаем список от дубликатов и сортируем его

                            float width = 0;                                        //ширина обрезка
                            foreach (float _size in _sizes)                         
                            {                                                       //располагаем габариты по ширине листа
                                width += _size;                                     //до тех пор, пока полученной площади листа
                                if (width * type.B * type.Count - square > 0)       //не хватит для обеспечений общей площади деталей
                                {
                                    type.A = width;                                 //таким образом определяем нужную ширину обрезка
                                    break;
                                }
                            }

                            cut.Way = (int)Math.Ceiling(way / 1000);
                            cut.WayTotal = parts.Sum(p => p.Way * p.Count);
                            cut.Pinhole = pinholes * 2;
                            cut.Mass = type.Count * type.A * type.B * type.S * density / 1000000;
                            cut.MassTotal = parts.Sum(p => p.Mass * p.Count);
                            
                            LaserItem laser = new() { sheetSize = $"{type.A}X{type.B}", sheets = type.Count };
                            laser.way = cut.Way / laser.sheets;
                            laser.pinholes = cut.Pinhole / laser.sheets;
                            laser.mass = cut.Mass / laser.sheets;
                            cut.Items?.Add(laser);
                            if (cut.Items?.Count > 0) cut.SumProperties(cut.Items);

                            cut.PartDetails = parts;
                            cut.Parts = cut.PartList();
                            cut.PartsControl = new(cut, cut.Parts);
                            cut.AddPartsTab();
                        }
                        break;
                    }

                if (groups.Count() > MainWindow.M.DetailControls[0].TypeDetailControls.Count)
                    MainWindow.M.DetailControls[0].AddTypeDetail();
            }

            //определяем деталь, в которой загрузили раскладки, как комплект деталей
            if (!MainWindow.M.DetailControls[0].Detail.IsComplect) MainWindow.M.DetailControls[0].IsComplectChanged("Комплект деталей");
            if (MainWindow.M.DetailControls[0].TypeDetailControls.Count > 0)
                foreach (TypeDetailControl type in MainWindow.M.DetailControls[0].TypeDetailControls)
                    type.CreateSort();
        }
    }

    public class TechItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string numberName = null!;
        public string NumberName
        {
            get => numberName;
            set
            {
                if (numberName != value)
                {
                    numberName = value;
                    OnPropertyChanged(nameof(NumberName));
                }
            }
        }

        private string sizes = null!;
        public string Sizes
        {
            get => sizes;
            set
            {
                if (sizes != value)
                {
                    sizes = value;
                    OnPropertyChanged(nameof(Sizes));
                }
            }
        }

        private string material = "";
        public string Material
        {
            get => material;
            set
            {
                if (material != value)
                {
                    material = value;
                    OnPropertyChanged(nameof(Material));
                }
            }
        }

        private string destiny = null!;
        public string Destiny
        {
            get => destiny;
            set
            {
                if (destiny != value)
                {
                    destiny = value;
                    OnPropertyChanged(nameof(Destiny));
                }
            }
        }

        private string count = null!;
        public string Count
        {
            get => count;
            set
            {
                if (count != value)
                {
                    count = value;
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        private string route = null!;
        public string Route
        {
            get => route;
            set
            {
                if (route != value)
                {
                    route = value;
                    OnPropertyChanged(nameof(Route));
                }
            }
        }

        private string hasMaterial = null!;
        public string HasMaterial
        {
            get => hasMaterial;
            set
            {
                if (hasMaterial != value)
                {
                    hasMaterial = value;
                    OnPropertyChanged(nameof(HasMaterial));
                }
            }
        }

        private string originalName = null!;
        public string OriginalName
        {
            get => originalName;
            set
            {
                if (originalName != value)
                {
                    originalName = value;
                    OnPropertyChanged(nameof(OriginalName));
                }
            }
        }

        [Browsable(false)]
        public bool IsGenerated { get; set; } = false;

        [Browsable(false)]
        public string PathToModel { get; set; } = null!;

        [Browsable(false)]
        public string PathToScan { get; set; } = null!;

        public TechItem() { }
        public TechItem(string numberName, string sizes, string material, string destiny, string count, string route, string hasMaterial, string originalName, string pathToModel, string isGenerated)
        {
            NumberName = numberName;
            Sizes = sizes;
            if (material.ToLower().Contains("ст")) Material = "";
            else Material = material;
            Destiny = destiny;
            Count = count;
            Route = route;
            if (hasMaterial.ToLower().Contains("дав")) HasMaterial = "Давальч";
            else HasMaterial = "";
            OriginalName = originalName;
            PathToModel = pathToModel;
            IsGenerated = isGenerated == "да";
        }
    }
}
