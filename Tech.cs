using System.IO;
using Path = System.IO.Path;
using System.Collections.Generic;
using System.Linq;
using ExcelDataReader;
using System.Data;
using System;
using OfficeOpenXml;
using System.Windows;

namespace Metal_Code
{
    public class Tech
    {
        public string ExcelFile;                        //путь к файлу
        public int CountTechItems = 0;                  //кол-во строк заявки
        public List<TechItem> TechItems = new();        //список полученных объектов из строк файла
        public List<TechItem> FoundItems = new();       //список найденных строк

        public Tech(string path)
        {
            ExcelFile = path;
        }

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
                        $"{table.Rows[i].ItemArray[2]}",        //наименование детали
                        $"{table.Rows[i].ItemArray[3]}",        //материал
                        $"s{table.Rows[i].ItemArray[4]}",       //толщина
                        $"n{(int)MainWindow.Parser($"{table.Rows[i].ItemArray[5]}") * countAssembly}",  //количество
                        $"{table.Rows[i].ItemArray[6]}");       //маршрут

                    if (techItem.Material.Contains("al") || techItem.Material.Contains("амг2"))
                    {
                        techItem.Material = "";
                        techItem.Destiny = $"al{table.Rows[i].ItemArray[4]}";
                    }
                    else if (techItem.Material.Contains("амг") || techItem.Material.Contains("д16")) techItem.Destiny = $"al{table.Rows[i].ItemArray[4]}";
                    else if (techItem.Material.Contains("br"))
                    {
                        techItem.Material = "";
                        techItem.Destiny = $"br{table.Rows[i].ItemArray[4]}";
                    }
                    else if (techItem.Material.Contains("cu"))
                    {
                        techItem.Material = "";
                        techItem.Destiny = $"cu{table.Rows[i].ItemArray[4]}";
                    }

                    TechItems.Add(techItem);
                }
                CountTechItems = TechItems.Count;

                stream.Close();

                //получаем коллекцию уникальных строк на основе группировки по материалу и толщине
                var dirMaterials = TechItems.GroupBy(m => new { m.Material, m.Destiny })
                    .Select(g => $"{g.Key.Destiny} {g.Key.Material}");

                //получаем коллекцию, разбитую на группы по работам
                var works = TechItems.GroupBy(w => w.Route);

                //сортируем pdf-файлы по папкам работ
                foreach (var work in works)
                    if (work.Key != "")
                    {
                        if (work.Key.Contains("гиб"))
                        {
                            DirectoryInfo dirBend = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Гибка");
                            SortExtension(dirBend, dirMaterials, "pdf", work.Select(t => t).ToList());
                        }
                        
                        if (work.Key.Contains("вальц"))
                        {
                            DirectoryInfo dirRoll = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Вальцовка");
                            SortExtension(dirRoll, dirMaterials, "pdf", work.Select(t => t).ToList());
                        }
                        
                        if (work.Key.Contains("рез"))
                        {
                            DirectoryInfo dirThread = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Резьба");
                            SortExtension(dirThread, dirMaterials, "pdf", work.Select(t => t).ToList());
                        }
                        
                        if (work.Key.Contains("зен"))
                        {
                            DirectoryInfo dirCountersink = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Зенковка");
                            SortExtension(dirCountersink, dirMaterials, "pdf", work.Select(t => t).ToList());
                        }
                        
                        if (work.Key.Contains("свер"))
                        {
                            DirectoryInfo dirDrilling = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Сверловка");
                            SortExtension(dirDrilling, dirMaterials, "pdf", work.Select(t => t).ToList());
                        }
                        
                        if (work.Key.Contains("свар"))
                        {
                            DirectoryInfo dirWeld = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Сварка");
                            SortExtension(dirWeld, dirMaterials, "pdf", work.Select(t => t).ToList());
                        }
                        
                        if (work.Key.Contains("окр"))
                        {
                            DirectoryInfo dirPaint = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Окраска");
                            SortExtension(dirPaint, dirMaterials, "pdf", work.Select(t => t).ToList());
                        }
                    }

                //получаем коллекцию файлов "igs" в папке с Excel-файлом
                IEnumerable<string> _igs = Directory.EnumerateFiles(Path.GetDirectoryName(ExcelFile), $"*.igs", SearchOption.TopDirectoryOnly);
                if (_igs.Any())
                {
                    //создаем папку "Труборез" в директории Excel-файла
                    DirectoryInfo dirPipe = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Труборез");
                    IEnumerable<string> files = SortExtension(dirPipe, dirMaterials, "igs", TechItems);

                    //удаляем найденные объекты заявки из общего списка
                    if (FoundItems.Count > 0) TechItems = TechItems.Except(FoundItems).ToList();
                }

                //получаем коллекцию файлов "dxf" в папке с Excel-файлом
                IEnumerable<string> _dxf = Directory.EnumerateFiles(Path.GetDirectoryName(ExcelFile), $"*.dxf", SearchOption.TopDirectoryOnly);
                if (_dxf.Any())
                {
                    //создаем папку "Лазер" в директории Excel-файла
                    DirectoryInfo dirLaser = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "Лазер");
                    IEnumerable<string> files = SortExtension(dirLaser, dirMaterials, "dxf", TechItems);

                    //удаляем найденные объекты заявки из общего списка
                    if (FoundItems.Count > 0) TechItems = TechItems.Except(FoundItems).ToList();
                }

                //создаем папку "КП" в директории заявки
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)) + "\\" + "КП");

                ClearDirectories();     //очищаем пустые папки
            }
            catch (Exception ex) { notify = ex.Message; }

            return RequestReport();
        }

        private IEnumerable<string> SortExtension(DirectoryInfo dirMain, IEnumerable<string> dirMaterials, string extension, List<TechItem> techItems)
        {
            FoundItems.Clear();      //очищаем список ненайденных файлов

            //создаем папки для каждого материала в директории работы
            foreach (var item in dirMaterials) Directory.CreateDirectory(dirMain + "\\" + $"{item}");

            //получаем коллекцию файлов в папке с Excel-файлом
            IEnumerable<string> files = Directory.EnumerateFiles(Path.GetDirectoryName(ExcelFile), $"*.{extension}", SearchOption.TopDirectoryOnly);

            //сортируем полученные ранее объекты TechItem по соответствующим папкам
            foreach (TechItem techItem in techItems)
            {
                //для этого перебираем файлы
                foreach (string file in files)
                {
                    //и на основе имени без расширения
                    string nameFile = Path.GetFileNameWithoutExtension(file);

                    //находим совпадение TechItem и файла по номеру чертежа
                    if (nameFile.Contains(techItem.NumberName))
                    {
                        //копируем исходный файл в нужный каталог с рабочим именем для нашего производства
                        foreach (var item in dirMaterials)
                            if ($"{techItem.Destiny} {techItem.Material}" == item)
                            {
                                if (techItems == TechItems)
                                {
                                    TechItem? isFounded = FoundItems.FirstOrDefault(x => x.Path == file);
                                    if (isFounded is null)
                                    {
                                        string destination = $"{techItem.Route}" == "" ?
                                            dirMain + "\\" + $"{item}".Trim() + "\\"
                                            + $"{techItem.NumberName} {techItem.Name} {techItem.Material} {techItem.Destiny} {techItem.Count}" + $".{extension}"
                                            : dirMain + "\\" + $"{item}".Trim() + "\\"
                                            + $"{techItem.NumberName} {techItem.Name} {techItem.Material} {techItem.Destiny} {techItem.Count} ({techItem.Route})" + $".{extension}";

                                        File.Copy(file, destination);
                                        techItem.Path = file;
                                        FoundItems.Add(techItem);           //файл найден
                                    }
                                    else MessageBox.Show($"Проверьте файлы с именами {isFounded.NumberName} и {techItem.NumberName}.\n" +
                                        $"Из-за сходства имён они могли быть обработаны неверно.\n" +
                                        $"Проверьте их по пути {file} и скопируйте вручную.");
                                }
                                else
                                {
                                    File.Copy(file, dirMain + "\\" + $"{item}".Trim() + "\\"
                                        + $"{techItem.NumberName} {techItem.Name} {techItem.Count}" + $".{extension}");
                                }
                            }
                        break;
                    }
                }
            }
            return files;
        }

        public void ClearDirectories()      //метод очищения пустых директорий
        {
            //удаляем пустые папки
            string[] dirs = Directory.GetDirectories(Path.GetDirectoryName(Path.GetDirectoryName(ExcelFile)));
            if (dirs.Length > 0)
                foreach (var dir in dirs)
                {
                    string[] dirMaterials = Directory.GetDirectories(dir);
                    foreach (string dm in dirMaterials)
                        if (Directory.GetFileSystemEntries(dm).Length == 0)
                            Directory.Delete(dm);
                }
        }

        private string RequestReport()
        {
            string notify;                  //сообщение пользователю

            if (TechItems.Count > 0)
            {
                //открываем заявку для редактирования
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
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
                    stream.WriteLine(item.NumberName + " " + item.Name);
                }
                requestbook.Save();     //сохраняем книгу
                notify = $"Обработано {CountTechItems} строк заявки. Создан перечень недостающих {TechItems.Count} файлов.";
            }
            else notify = $"Обработано {CountTechItems} строк заявки. Все файлы найдены.";

            return notify;
        }
    }

    public class TechItem
    {
        public string NumberName { get; set; }
        public string Name { get; set; }
        public string Material { get; set; }
        public string Destiny { get; set; }
        public string Count { get; set; }
        public string Route { get; set; }
        public string? Path { get; set; }

        public TechItem(string numberName, string name, string material, string destiny, string count, string route)
        {
            NumberName = numberName;
            Name = name;
            if (material.ToLower().Contains("ст")) Material = "";
            else Material = material;
            Destiny = destiny;
            Count = count;
            Route = route;
        }
    }
}
