using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Metal_Code
{
    public class MergeOffer
    {
        private static readonly HashSet<string> WorkFolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Лазер", "Гибка", "Сварка", "Окраска", "Резьба", "Зенковка", "Заклепки",
            "Фрезеровка", "Сверловка", "Вальцовка", "Цинкование", "Лентопил", "Аквабластинг"
        };

        public void Run()
        {
            var selectedOffers = MainWindow.M.OffersGrid.SelectedItems.Cast<Offer>().ToList();

            // --- Определяем компанию ---
            string? company = selectedOffers[^1].Company;
            if (company is null) return;

            // --- Генерируем путь к новой папке ---
            string combinedKpPath;
            combinedKpPath = GenerateCombinedKpFolderPath(selectedOffers, company);

            // Извлекаем имя папки
            string folderName = Path.GetFileName(combinedKpPath);

            // --- Очищаем интерфейс ---
            MainWindow.M.ClearDetails();

            List<Detail> details = new();
            string comment = $"Объединённое КП из:";

            // --- Собираем детали ---
            foreach (var offer in selectedOffers)
            {
                if (offer.Data != null)
                {
                    var product = MainWindow.OpenOfferData(offer.Data);
                    if (product != null)
                    {
                        details.AddRange(product.Details);
                        comment += $" {offer.N};";
                    }
                }
            }

            // --- Копируем папки работ с суффиксами номеров ---
            MergeWorkFoldersFromOffers(selectedOffers, combinedKpPath);

            // --- Загружаем данные в интерфейс ---
            LoadDetails(details);
            int mergeOfferNumber = int.Parse(folderName.Split(' ', StringSplitOptions.RemoveEmptyEntries).First());
            MainWindow.M.Order.Text = mergeOfferNumber.ToString();
            MainWindow.M.CustomerDrop.Text = company;
            MainWindow.M.Comment.Text = comment;

            // --- Создаём пустую папку "КП" ---
            string kpFolder = Path.Combine(combinedKpPath, "КП");
            if (!Directory.Exists(kpFolder)) Directory.CreateDirectory(kpFolder);

            // --- Открываем папку в проводнике ---
            Process.Start("explorer.exe", combinedKpPath);

            // --- Выводим путь к объединенному КП ---
            MainWindow.M.StatusBegin($"Расчеты успешно объединены в папке «{folderName}».");
        }

        /// <summary>
        /// Генерирует путь к новой папке объединённого КП с автоматическим номером.
        /// Берёт корневую папку из последнего выбранного расчёта.
        /// </summary>
        /// <param name="selectedOffers">Список выбранных расчётов</param>
        /// <param name="companyName">Название компании для новой папки</param>
        /// <returns>Полный путь к новой папке, например: C:\Projects\40580 ООО "Ромашка"</returns>
        public static string GenerateCombinedKpFolderPath(List<Offer> selectedOffers, string companyName)
        {
            // Берём последний выбранный Offer (по порядку выделения)
            var lastSelectedOffer = selectedOffers.Last();

            if (string.IsNullOrEmpty(lastSelectedOffer.Act))
                throw new InvalidOperationException("У последнего выбранного расчёта отсутствует путь к файлу КП.");

            // Извлекаем корневую папку проектов
            string projectsRoot = GetProjectsRootFromActPath(lastSelectedOffer.Act);

            if (!Directory.Exists(projectsRoot))
                throw new DirectoryNotFoundException($"Корневая папка расчетов не найдена: {projectsRoot}");

            // Находим максимальный номер среди папок проектов
            int maxExistingNumber = 0;

            try
            {
                var directories = Directory.GetDirectories(projectsRoot);
                foreach (string dir in directories)
                {
                    string dirName = Path.GetFileName(dir);
                    if (string.IsNullOrWhiteSpace(dirName)) continue;

                    // Извлекаем первую часть имени до пробела — предположительно номер
                    string firstPart = dirName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

                    if (int.TryParse(firstPart, out int number))
                    {
                        if (number > maxExistingNumber)
                            maxExistingNumber = number;
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new Exception($"Нет доступа к папке расчетов: {ex.Message}");
            }

            // Следующий номер
            int nextNumber = maxExistingNumber + 1;

            // Формируем имя новой папки
            string newFolderName = $"{nextNumber} {companyName.Trim()}";
            string fullPath = Path.Combine(projectsRoot, newFolderName);

            // Создаём папку
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            return fullPath;
        }

        /// <summary>
        /// Извлекает корневую папку проектов из пути к .act-файлу расчёта.
        /// Например: 
        ///   Вход: C:\Projects\40535 ООО "Ромашка"\КП\КП 40535.act
        ///   Выход: C:\Projects
        /// </summary>
        /// <param name="actPath">Путь к .act файлу расчёта</param>
        /// <returns>Путь к корневой папке проектов</returns>
        public static string GetProjectsRootFromActPath(string actPath)
        {
            if (string.IsNullOrEmpty(actPath))
                throw new ArgumentException("Путь к файлу КП не может быть пустым.");

            // Получаем папку, где лежит КП-файл
            var fileDir = Path.GetDirectoryName(actPath);
            if (fileDir == null)
                throw new ArgumentException("Не удалось определить директорию файла.");

            // Получаем папку проекта (например, "40535 ООО Ромашка")
            var projectFolder = Directory.GetParent(fileDir);
            if (projectFolder == null)
                throw new InvalidOperationException("Не удалось определить папку расчета.");

            // Получаем корневую папку (родитель папки проекта)
            var rootFolder = projectFolder.Parent;
            if (rootFolder == null)
                throw new InvalidOperationException("Папка расчета находится на диске без родительской директории.");

            return rootFolder.FullName;
        }

        /// <summary>
        /// Объединяет папки работ из нескольких расчетов в одну директорию, 
        /// переименовывая папки материалов добавлением номера расчета.
        /// </summary>
        /// <param name="offers">Список выбранных расчетов (Offer)</param>
        /// <param name="destinationRoot">Корневая папка для объединённого КП</param>
        public static void MergeWorkFoldersFromOffers(List<Offer> offers, string destinationRoot)
        {
            // Создаем корневую папку
            if (!Directory.Exists(destinationRoot))
                Directory.CreateDirectory(destinationRoot);

            foreach (var offer in offers)
            {
                if (string.IsNullOrEmpty(offer.Act))
                    continue; // Пропускаем, если путь к расчету не задан

                // Папка, где хранятся работы (например, рядом с .act-файлом)
                string? sourceBaseDir = Path.GetDirectoryName(Path.GetDirectoryName(offer.Act));
                if (sourceBaseDir == null) continue;

                // Предполагается, что внутри baseDir лежат папки: "Лазер", "Гибка", "Сварка" и т.д.
                var workFolders = Directory.GetDirectories(sourceBaseDir)
                                           .Select(Path.GetFileName)
                                           .Where(name => IsWorkFolder(name)) // фильтр: только рабочие папки
                                           .ToList();

                foreach (var workFolderName in workFolders)
                {
                    if (workFolderName == null) continue;

                    string sourceWorkPath = Path.Combine(sourceBaseDir, workFolderName);
                    string destWorkPath = Path.Combine(destinationRoot, workFolderName);

                    if (!Directory.Exists(destWorkPath))
                        Directory.CreateDirectory(destWorkPath);

                    // Получаем все папки материалов внутри папки работы
                    var materialDirs = Directory.GetDirectories(sourceWorkPath);
                    foreach (var materialDir in materialDirs)
                    {
                        string materialName = Path.GetFileName(materialDir);
                        // Формируем новое имя: "s2 aisi304" → "s2 aisi304_40567"
                        string newMaterialName = $"{materialName}_{offer.N}";
                        string destMaterialPath = Path.Combine(destWorkPath, newMaterialName);

                        // Копируем содержимое с переименованием
                        CopyDirectory(materialDir, destMaterialPath, true);
                    }
                }
            }
        }

        private static bool IsWorkFolder(string? folderName)
        {
            return !string.IsNullOrWhiteSpace(folderName) && WorkFolderNames.Contains(folderName.Trim());
        }
        
        /// <summary>
        /// Копирует каталог и, при необходимости, все вложенные подкаталоги и файлы.
        /// </summary>
        /// <param name="sourceDir">Путь к исходному каталогу.</param>
        /// <param name="destinationDir">Путь к целевому каталогу.</param>
        /// <param name="recursive">Если true — копируются также все подкаталоги рекурсивно.</param>
        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Исходный каталог не найден: {sourceDir}");

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, overwrite: true); // можно убрать, если нужна защита
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dir.GetDirectories())
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        //загрузка деталей в интерфейс программы
        public void LoadDetails(List<Detail> details)
        {
            foreach (var detail in details)
            {
                if (string.IsNullOrEmpty(detail.Title)) continue;

                DetailControl? existingDetail = MainWindow.M.DetailControls
                    .FirstOrDefault(dc => dc.Detail.Title == detail.Title);

                if (existingDetail == null)
                {
                    // Создаём новую деталь (автоматически добавит одну TypeDetail + Work)
                    MainWindow.M.AddDetail();
                    var newDetailControl = MainWindow.M.DetailControls.Last();

                    newDetailControl.Detail.Title = detail.Title;

                    if (detail.Title.Contains("Комплект"))
                        newDetailControl.IsComplectChanged();

                    newDetailControl.Detail.Count = detail.Count;
                    newDetailControl.Detail.MillingHoles = detail.MillingHoles;
                    newDetailControl.Detail.MillingGrooves = detail.MillingGrooves;

                    if (newDetailControl.TypeDetailControls.Last().Count == 0)
                        newDetailControl.TypeDetailControls.Last().Remove();

                    // Добавляем все заготовки из detail
                    foreach (var typeDetail in detail.TypeDetails)
                        AddAndFillTypeDetail(newDetailControl, typeDetail);
                }
                else
                {
                    // Деталь уже существует — просто добавляем новые заготовки
                    foreach (var typeDetail in detail.TypeDetails)
                        AddAndFillTypeDetail(existingDetail, typeDetail);
                }
            }
        }
        private void AddAndFillTypeDetail(DetailControl detailControl, SaveTypeDetail typeDetail)
        {
            // Добавляем новую заготовку — это вызовет AddWork() внутри
            detailControl.AddTypeDetail();
            var typeControl = detailControl.TypeDetailControls.Last();

            // Заполняем свойства заготовки
            typeControl.TypeDetailDrop.SelectedIndex = typeDetail.Index;
            typeControl.Count = typeDetail.Count;
            typeControl.MetalDrop.SelectedIndex = typeDetail.Metal;
            typeControl.SortDrop.SelectedIndex = typeDetail.Tuple.Item1;
            typeControl.A = typeDetail.Tuple.Item2;
            typeControl.B = typeDetail.Tuple.Item3;
            typeControl.S = typeDetail.Tuple.Item4;
            typeControl.L = typeDetail.Tuple.Item5;
            typeControl.HasMetal = typeDetail.HasMetal;
            typeControl.ExtraResult = typeDetail.ExtraResult;
            typeControl.SetComment(typeDetail.Comment);

            foreach (var workItem in typeDetail.Works)
            {
                if (workItem.NameWork is null) continue;

                var workControl = typeControl.WorkControls.Last();

                // Проверяем дубликаты (кроме "Доп" работ)
                var existingWork = !workItem.NameWork.Contains("Доп")
                    ? typeControl.WorkControls
                        .FirstOrDefault(w => w.WorkDrop.Text == workItem.NameWork)
                    : null;

                if (existingWork != null)
                {
                    existingWork.Ratio = workItem.Ratio;
                    existingWork.TechRatio = workItem.TechRatio;
                    existingWork.ExtraResult = workItem.ExtraResult;
                    continue;
                }

                // Подбираем работу по имени (устойчиво к изменению порядка)
                foreach (Work workInCombo in workControl.WorkDrop.Items)
                {
                    if (workInCombo.Name == workItem.NameWork)
                    {
                        workControl.WorkDrop.SelectedItem = workInCombo;
                        break;
                    }
                }

                // Обработка ICut
                if (workControl.workType is ICut cut)
                {
                    if (workItem.Items?.Count > 0) cut.Items = workItem.Items;
                    if (workItem.Parts?.Count > 0) cut.PartDetails = workItem.Parts;

                    if (cut is CutControl c)
                    {
                        if (c.Items?.Count > 0) c.SumProperties(c.Items);
                        c.Parts = c.PartList();
                        c.PartsControl = new(c, c.Parts);
                        c.AddPartsTab();
                    }
                    else if (cut is PipeControl pipe)
                    {
                        pipe.Parts = pipe.PartList();
                        pipe.PartsControl = new(pipe, pipe.Parts);
                        pipe.AddPartsTab();
                        pipe.SetTotalProperties();
                    }

                    // Обработка частей (PartControl)
                    if (cut.Parts?.Count > 0)
                    {
                        foreach (var part in cut.Parts)
                        {
                            if (part.Part.PropsDict?.Count > 0)
                            {
                                foreach (int key in part.Part.PropsDict.Keys)
                                {
                                    if (key < 50)
                                    {
                                        var valueStr = part.Part.PropsDict[key][0];
                                        if (double.TryParse(valueStr, out double parsed))
                                            part.AddControl((int)parsed);
                                        else
                                            part.AddControl((int)MainWindow.Parser(valueStr));
                                    }
                                }
                            }
                            part.PropertiesChanged?.Invoke(part, false);
                        }
                    }
                }

                // Применяем свойства работы
                workControl.propsList = workItem.PropsList;
                workControl.PropertiesChanged?.Invoke(workControl, false);
                workControl.Ratio = workItem.Ratio;
                workControl.TechRatio = workItem.TechRatio;
                workControl.ExtraResult = workItem.ExtraResult;

                if (typeControl.WorkControls.Count < typeDetail.Works.Count) typeControl.AddWork();
            }
        }
    }
}
