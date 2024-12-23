using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace Metal_Code
{
    public class Metalix
    {
        public string ExcelFile;        // путь к файлу
        public Metalix(string path) { ExcelFile = path; }

        public string Run()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            string notify = $"Не удается прочитать файл раскладки";
            try
            {
                // преобразуем открытый Excel-файл в DataTable для парсинга
                using FileStream stream = File.Open(ExcelFile, FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();
                DataTable table = result.Tables[0];

                // перебираем строки таблицы
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    if (table.Rows[i] is null) continue;

                    int sheets = (int)MainWindow.Parser($"{table.Rows[3].ItemArray[3]}");
                    int pinholes = (int)MainWindow.Parser($"{table.Rows[5].ItemArray[6]}");

                    // считываем раскладки
                    if ($"{table.Rows[i].ItemArray[0]}".Contains("Субраскладки в заказе"))
                    {
                        MainWindow.M.NewProject();
                        // определяем деталь, в которой загрузили раскладки, как комплект деталей
                        MainWindow.M.DetailControls[^1].IsComplectChanged("Комплект деталей");

                        List<LaserItem> items = new();

                        for (int layout = i + 2; !$"{table.Rows[layout].ItemArray[0]}".Contains("Детали в субраскладках"); layout++)
                        {
                            LaserItem item = new()
                            {                              
                                sheets = (int)MainWindow.Parser($"{table.Rows[layout].ItemArray[2]}"),
                                metal = $"{table.Rows[layout].ItemArray[3]}",
                                destiny = $"{table.Rows[layout].ItemArray[4]}",
                                sheetSize = $"{table.Rows[layout].ItemArray[5]}X{table.Rows[layout].ItemArray[6]}",
                                mass = (float)Math.Ceiling(MainWindow.Parser($"{table.Rows[layout].ItemArray[7]}")),
                            };
                            items.Add(item);
                        }

                        foreach (LaserItem item in items) item.pinholes = pinholes / sheets;

                        var groupedItems = items.GroupBy(m => new { m.metal, m.destiny });

                        i += items.Count + 3;

                        // считываем детали
                        if ($"{table.Rows[i].ItemArray[1]}".Contains("Имя файла детали"))
                        {
                            MainWindow.M.Parts.Clear();

                            for (int detail = i + 1; $"{table.Rows[detail].ItemArray[1]}" is not ""; detail++)
                            {
                                Part part = new()
                                {
                                    Title = $"{table.Rows[detail].ItemArray[1]}",
                                    Description = "Л",
                                    Accuracy = $"H14/h14 +-IT 14/2",
                                    Metal = $"{table.Rows[detail].ItemArray[3]}",
                                    Destiny = MainWindow.Parser($"{table.Rows[detail].ItemArray[4]}"),
                                    Mass = MainWindow.Parser($"{table.Rows[detail].ItemArray[7]}"),
                                    Count = (int)MainWindow.Parser($"{table.Rows[detail].ItemArray[8]}"),
                                    Way = (float)Math.Round(MainWindow.Parser($"{table.Rows[detail].ItemArray[11]}") / 1000, 2)
                                };
                                part.PropsDict[100] = new() { $"{table.Rows[detail].ItemArray[5]}", $"{table.Rows[detail].ItemArray[6]}", $"{table.Rows[detail].ItemArray[5]}x{table.Rows[detail].ItemArray[6]}" };
                                MainWindow.M.Parts.Add(part);
                            }

                            var _groupedParts = MainWindow.M.Parts.GroupBy(m => new { m.Metal, m.Destiny });

                            foreach (var item in groupedItems)
                            {
                                TypeDetailControl type = MainWindow.M.DetailControls[^1].TypeDetailControls[^1];

                                // устанавливаем "Лист металла"
                                foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Лист металла")
                                    {
                                        type.TypeDetailDrop.SelectedItem = t;
                                        break;
                                    }

                                // устанавливаем металл
                                foreach (Metal metal in MainWindow.M.Metals) if (metal.Name == item.Key.metal)
                                    {
                                        type.MetalDrop.SelectedItem = metal;
                                        break;
                                    }
                                type.S = MainWindow.Parser(item.Key.destiny);        // устанавливаем толщину

                                // устанавливаем "Лазерная резка"
                                foreach (Work w in MainWindow.M.Works) if (w.Name == "Лазерная резка")
                                    {
                                        type.WorkControls[^1].WorkDrop.SelectedItem = w;
                                        break;
                                    }

                                var _item = _groupedParts.FirstOrDefault(m => m.Key.Metal == item.Key.metal
                                                                    && $"{m.Key.Destiny}" == item.Key.destiny);

                                if (type.WorkControls[0].workType is CutControl cut && _item is not null)
                                {
                                    cut.MassTotal = _item.Sum(w => w.Mass * w.Count);
                                    cut.WayTotal = (float)Math.Ceiling(_item.Sum(w => w.Way * w.Count));
                                    foreach (LaserItem laser in item)
                                        laser.way = (float)Math.Ceiling(cut.WayTotal / _item.Count() / laser.sheets);
                                    cut.Items = item.ToList();
                                    cut.SumProperties(cut.Items);
                                    cut.PartDetails = _item.ToList();
                                    cut.Parts = cut.PartList();
                                    cut.PartsControl = new(cut, cut.Parts);
                                    cut.AddPartsTab();

                                    cut.work.type.CreateSort();

                                    if (cut.Parts.Count > 0)
                                        foreach (PartControl part in cut.Parts)
                                            cut.PartTitleAnalysis(part);
                                }

                                if (MainWindow.M.DetailControls[^1].TypeDetailControls.Count != groupedItems.Count())
                                    MainWindow.M.DetailControls[^1].AddTypeDetail();
                            }
                        }
                        break;
                    }
                }
                notify = "Отчет от Металикса загружен успешно";
            }
            catch (Exception ex) { notify = ex.Message; }

            return notify;
        }
    }
}
