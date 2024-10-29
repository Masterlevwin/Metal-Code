using System.IO;
using Path = System.IO.Path;
using System.Collections.Generic;
using System.Linq;
using ExcelDataReader;
using System.Data;
using System;

namespace Metal_Code
{
    public class Tech
    {
        public string ExcelFile;

        public List<TechItem> TechItems = new();

        public Tech(string path)
        {
            ExcelFile = path;
        }

        public string Run()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            string notify = $"Не удается прочитать файл";

            try
            {
                //преобразуем открытый Excel-файл в DataTable для парсинга
                using FileStream stream = File.Open(ExcelFile, FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTable table = result.Tables[0];

                //перебираем строки таблицы и заполняем список объектами TechItem
                for (int i = 1; i < table.Rows.Count; i++)
                {
                    if ($"{table.Rows[i].ItemArray[1]}" is null || $"{table.Rows[i].ItemArray[1]}" == "") continue;

                    TechItem techItem = new(
                        $"{table.Rows[i].ItemArray[1]}",        //номер чертежа
                        $"{table.Rows[i].ItemArray[2]}",        //наименование детали
                        $"{table.Rows[i].ItemArray[3]}",        //материал
                        $"s{table.Rows[i].ItemArray[4]}",       //толщина
                        $"n{table.Rows[i].ItemArray[5]}");      //количество

                    TechItems.Add(techItem);
                }

                //создаем папку "Лазер" в директории Excel-файла
                DirectoryInfo dirLaser = Directory.CreateDirectory(Path.GetDirectoryName(ExcelFile) + "\\" + "Лазер");

                //получаем коллекцию уникальных строк на основе группировки по материалу и толщине
                var dirMaterials = TechItems.GroupBy(m => new { m.Material, m.Destiny })
                    .Select(g => $"{g.Key.Destiny} {g.Key.Material}");

                //создаем папки для каждого материала в директории "Лазера"
                foreach (var item in dirMaterials) Directory.CreateDirectory(dirLaser + "\\" + $"{item}");

                //получаем коллекцию dxf-файлов в папке с Excel-файлом
                IEnumerable<string> files = Directory.EnumerateFiles(Path.GetDirectoryName(ExcelFile), "*.dxf", SearchOption.TopDirectoryOnly);

                //сортируем полученные ранее объекты TechItem по соответствующим папкам
                foreach (TechItem techItem in TechItems)
                {
                    //для этого перебираем dxf-файлы
                    foreach (string file in files)
                    {
                        //и на основе имени без расширения
                        string nameFile = Path.GetFileNameWithoutExtension(file);

                        //находим совпадение TechItem и dxf-файла по номеру чертежа
                        if (nameFile.Contains(techItem.NumberName))
                        {
                            //копируем исходный dxf-файл в нужный каталог с рабочим именем для нашего производства
                            foreach (var item in dirMaterials)
                                if ($"{techItem.Destiny} {techItem.Material}" == item)
                                    File.Copy(file, dirLaser + "\\" + $"{item}" + "\\"
                                        + $"{techItem.NumberName} {techItem.Name} {techItem.Material} {techItem.Destiny} {techItem.Count}" + ".dxf");
                            break;
                        }
                    }
                }

                if (files.ToArray().Length > 0) notify = $"Обработано {TechItems.Count} строк и {files.ToArray().Length} dxf-файлов";
                else notify = $"Не найдено dxf-файлов";
            }
            catch (Exception ex) { notify = ex.Message; }

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

        public TechItem(string numberName, string name, string material, string destiny, string count)
        {
            NumberName = numberName;
            Name = name;
            Material = material;
            Destiny = destiny;
            Count = count;
        }
    }
}
