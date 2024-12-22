using ExcelDataReader;
using OfficeOpenXml.Drawing;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using ACadSharp.Objects;

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

                        List<List<LaserItem>> _grouped = items.GroupBy(m => new { m.metal, m.destiny })
                            .Select(g => g.ToList()).ToList();

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

                            List<List<Part>> grouped = MainWindow.M.Parts.GroupBy(m => new { m.Metal, m.Destiny })
                                           .Select(g => g.ToList()).ToList();

                            if (grouped.Count != _grouped.Count)
                                return "Состав раскладок не соответствует составу деталей. Вероятно существует ошибка в отчете.";

                            for (int k = 0; k < _grouped.Count; k++)
                            {
                                TypeDetailControl type = MainWindow.M.DetailControls[^1].TypeDetailControls[^1];

                                // устанавливаем "Лист металла"
                                foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Лист металла")
                                    {
                                        type.TypeDetailDrop.SelectedItem = t;
                                        break;
                                    }

                                // устанавливаем металл
                                foreach (Metal metal in MainWindow.M.Metals) if (metal.Name == _grouped[k].First().metal)
                                    {
                                        type.MetalDrop.SelectedItem = metal;
                                        break;
                                    }
                                type.S = MainWindow.Parser(_grouped[k].First().destiny);        // устанавливаем толщину

                                // устанавливаем "Лазерная резка"
                                foreach (Work w in MainWindow.M.Works) if (w.Name == "Лазерная резка")
                                    {
                                        type.WorkControls[^1].WorkDrop.SelectedItem = w;
                                        break;
                                    }

                                if (type.WorkControls[0].workType is CutControl cut)
                                {
                                    cut.MassTotal = grouped[k].Sum(w => w.Mass * w.Count);
                                    cut.WayTotal = (float)Math.Ceiling(grouped[k].Sum(w => w.Way * w.Count));
                                    cut.Items = _grouped[k];
                                    cut.SumProperties(cut.Items);

                                    cut.PartDetails = grouped[k];
                                    cut.Parts = cut.PartList();
                                    cut.PartsControl = new(cut, cut.Parts);
                                    cut.AddPartsTab();

                                    cut.work.type.CreateSort();

                                    if (cut.Parts.Count > 0)
                                        foreach (PartControl part in cut.Parts)
                                            cut.PartTitleAnalysis(part);
                                }

                                if (MainWindow.M.DetailControls[^1].TypeDetailControls.Count != _grouped.Count)
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

        public void SetImagesForParts(FileStream stream)        //метод извлечения картинок из файла и установки их для каждой детали в виде массива байтов
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var workbook = new ExcelPackage(stream);                      //получаем книгу Excel из потока
            ExcelWorksheet worksheet = workbook.Workbook.Worksheets[0];

            //извлекаем все изображения на листе в список картинок
            List<ExcelPicture> pictures = worksheet.Drawings.Where(x => x.DrawingType == eDrawingType.Picture).Select(x => x.As.Picture).ToList();

            //if (Parts?.Count > 0)
            //    for (int i = 0; i < Parts.Count; i++)
            //        Parts[i].Part.ImageBytes = pictures[i].Image.ImageBytes;    //для каждой детали записываем массив байтов соответствующей картинки

            ////получаем выборку изображений самих раскладок на основе размера массива байтов
            //var images = pictures.Where(x => x.Image.ImageBytes.Length > 9999).ToList();

            //if (Items?.Count > 0)
            //    for (int j = 0; j < Items.Count; j++)
            //        Items[j].imageBytes = images[j].Image.ImageBytes;           //для каждой раскладки записываем массив байтов соответствующей картинки
        }
    }
}
