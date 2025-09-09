using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
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

        public string Run()
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

                //переносим папки работ в архив
                MigrateDirectories();

                //затем переопределяем материалы и толщины для формирования имен деталей
                foreach (TechItem techItem in TechItems)
                {
                    if (techItem.Material.ToLower().Contains("al") || techItem.Material.ToLower().Contains("амг2"))
                    {
                        techItem.Material = "";
                        techItem.Destiny = $"al{techItem.Destiny}";
                    }
                    else if (techItem.Material.ToLower().Contains("амг") || techItem.Material.ToLower().Contains("д16")) techItem.Destiny = $"al{techItem.Destiny}";
                    else if (techItem.Material.ToLower().Contains("br") || techItem.Material.ToLower().Contains("латунь"))
                    {
                        techItem.Material = "";
                        techItem.Destiny = $"br{techItem.Destiny}";
                    }
                    else if (techItem.Material.ToLower().Contains("cu") || techItem.Material.ToLower().Contains("медь"))
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

                string[] extensions = { "pdf", "tiff", "tif", "gpeg", "gpg" };     //основные расширения чертежей

                // Определяем маппинг: ключевое слово → имя папки
                var workMappings = new Dictionary<string, string>
                {
                    { "гиб", "Гибка" },
                    { "вальц", "Вальцовка" },
                    { "фрез", "Фрезеровка" },
                    { "рез", "Резьба" },
                    { "зен", "Зенковка" },
                    { "зак", "Заклепки" },
                    { "свер", "Сверловка" },
                    { "свар", "Сварка" },
                    { "окр", "Окраска" },
                    { "оц", "Оцинковка" },
                    { "лен", "Лентопил" },
                    { "аква", "Аквабластинг" }
                };

                // Базовый путь — вычисляем один раз
                string? baseDir = Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile));
                if (baseDir != null)
                {
                    // Сортируем PDF-файлы по папкам работ
                    foreach (var work in works)
                    {
                        if (string.IsNullOrWhiteSpace(work.Key)) continue;

                        string keyLower = work.Key.ToLower();

                        // Ищем первое совпадение по ключевому слову
                        foreach (var mapping in workMappings)
                        {
                            if (keyLower.Contains(mapping.Key))
                            {
                                string folderName = mapping.Value;
                                string folderPath = Path.Combine(baseDir, folderName);
                                DirectoryInfo dirWork = Directory.CreateDirectory(folderPath);

                                var fileList = work.ToList();
                                foreach (string extension in extensions)
                                    SortExtension(dirWork, extension.ToLower(), fileList);
                            }
                        }
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

            return $"Обработано {TechItems.Count} строк заявки.";
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
                                        string destination = dirMain + "\\" + $"{item}".Trim() + "\\"
                                            + $"{techItem.NumberName} n{techItem.Count}" + $".{extension}";

                                        int count = 0;
                                        
                                        while (File.Exists(destination))    //проверяем, существует ли уже такой файл
                                        {
                                            count++;
                                            destination = dirMain + "\\" + $"{item}".Trim() + "\\"
                                                + $"{techItem.NumberName}_{count} n{techItem.Count}" + $".{extension}";
                                        }

                                        File.Copy(file, destination);
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
                            string extension = Path.GetExtension(techItem.PathToModel).ToLower();
                            string dirMain = string.Empty;

                            if (extension == ".dxf") dirMain = dirLaser + "\\" + $"{item}".Trim() + "\\";
                            else if (extension == ".igs") dirMain= dirPipe + "\\" + $"{item}".Trim() + "\\";

                            string destination = $"{techItem.Route}" == "" ?
                                dirMain + $"{techItem.NumberName} {techItem.Material} {techItem.Destiny} n{techItem.Count}{extension}"
                                : dirMain + $"{techItem.NumberName} {techItem.Material} {techItem.Destiny} n{techItem.Count} ({Regex.Replace(techItem.Route, @"\b\d+", "")}){extension}";

                            //если файла с таким названием еще нет, просто копируем его
                            if (!File.Exists(destination)) File.Copy(techItem.PathToModel, destination);

                            //если файл с таким же названием уже есть, проверяем оба файла на идентичность
                            else if (!AreFilesTheSame(techItem.PathToModel, destination))
                            {
                                int count = 0;

                                while (File.Exists(destination))    //проверяем, существует ли уже такой файл
                                {
                                    count++;
                                    destination = $"{techItem.Route}" == "" ?
                                    dirMain + $"{techItem.NumberName}_{count} {techItem.Material} {techItem.Destiny} n{techItem.Count}{extension}"
                                    : dirMain + $"{techItem.NumberName}_{count} {techItem.Material} {techItem.Destiny} n{techItem.Count} ({Regex.Replace(techItem.Route, @"\b\d+", "")}){extension}";
                                }

                                File.Copy(techItem.PathToModel, destination);
                            }
                            break;
                        }
                    }
            }
        }

        // Метод сравнения по содержимому (побайтово)
        private static bool AreFilesTheSame(string file1, string file2)
        {
            if (!File.Exists(file1) || !File.Exists(file2))
                return false;

            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);

            if (info1.Length != info2.Length)
                return false;

            using var fs1 = File.OpenRead(file1);
            using var fs2 = File.OpenRead(file2);
            int b1, b2;
            do
            {
                b1 = fs1.ReadByte();
                b2 = fs2.ReadByte();
                if (b1 != b2) return false;
            } while (b1 != -1 && b2 != -1);

            return b1 == b2;
        }

        public void MigrateDirectories()            //метод переноса директорий работ в архив
        {
            string? dirMain = Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile));

            if (dirMain != null)
            {
                string archiveFolderName = $"Архив (от {DateTime.Now:dd.MM.yyyy HH-mm})";
                string archiveDirPath = Path.Combine(dirMain, archiveFolderName);

                // Если такая папка уже есть — добавляем номер
                int count = 1;
                while (Directory.Exists(archiveDirPath))
                {
                    archiveDirPath = Path.Combine(dirMain, $"{archiveFolderName} ({count})");
                    count++;
                }

                // Создаем папку "Архив..."
                Directory.CreateDirectory(archiveDirPath);

                foreach (string dirPath in Directory.GetDirectories(dirMain))
                {
                    DirectoryInfo dirInfo = new(dirPath);

                    if (dirPath.ToLower().Contains("архив") || dirPath.ToLower().Contains("тз")) continue;

                    string targetDirPath = Path.Combine(archiveDirPath, dirInfo.Name);

                    // Если такая папка уже есть в архиве — переименовываем
                    int counter = 1;
                    string uniqueTargetDirPath = targetDirPath;
                    while (Directory.Exists(uniqueTargetDirPath))
                    {
                        uniqueTargetDirPath = Path.Combine(archiveDirPath, $"{dirInfo.Name} ({counter})");
                        counter++;
                    }

                    // Перемещаем каталог
                    Directory.Move(dirPath, uniqueTargetDirPath);
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

                        if (Directory.GetFileSystemEntries(dir).Length == 0 && (dir.Contains("Лазер") ||
                            dir.Contains("Труборез") || dir.Contains("Архив"))) Directory.Delete(dir);
                    }
            }
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

        [Browsable(false)]
        public ObservableCollection<IGeometryDescriptor> Geometries { get; set; } = new();

        [Browsable(false)]
        public float Width { get; set; }

        [Browsable(false)]
        public float Height { get; set; }

        [Browsable(false)]
        public double X { get; set; }

        [Browsable(false)]
        public double Y { get; set; }

        [Browsable(false)]
        public bool IsRotated { get; set; }

        [Browsable(false)]
        public Brush Color { get; set; } = Brushes.LightBlue;

        [Browsable(false)]
        public float Way { get; set; }

        [Browsable(false)]
        public int Pinhole { get; set; }

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
