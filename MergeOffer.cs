using Metal_Code.Models;
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
            "Лазер", "Гибка", "Сварка", "Окраска", "Резьба", "Зенковка", "Заклепки", "Труборез",
            "Фрезеровка", "Сверловка", "Вальцовка", "Цинкование", "Лентопил", "Аквабластинг"
        };

        /// <summary>
        /// Асинхронно объединяет выбранные расчёты в одно КП.
        /// Загружает данные каждого расчёта из базы, если они не в памяти.
        /// </summary>
        public async System.Threading.Tasks.Task RunAsync()
        {
            var selectedOffers = MainWindow.M.OffersGrid.SelectedItems.Cast<Offer>().ToList();

            // --- Определяем компанию ---
            string? company = selectedOffers[^1].Company;
            if (company is null) return;

            MainWindow.M.StatusBegin($"Загрузка данных {selectedOffers.Count} расчётов...", MainWindow.StatusMessageType.Info);

            // --- Генерируем путь к новой папке ---
            string combinedKpPath;
            try
            {
                combinedKpPath = GenerateCombinedKpFolderPath(selectedOffers, company);
            }
            catch (Exception ex)
            {
                MainWindow.M.StatusBegin($"Ошибка создания папки: {ex.Message}", MainWindow.StatusMessageType.Error);
                return;
            }

            string folderName = Path.GetFileName(combinedKpPath);

            // --- Очищаем интерфейс ---
            MainWindow.M.ClearDetails();

            List<Detail> details = new();
            string comment = $"Объединённое КП из:";
            int loadedCount = 0;
            int failedCount = 0;

            // --- Собираем детали (с загрузкой данных из базы) ---
            foreach (var offer in selectedOffers)
            {
                string? dataJson = offer.Data;

                // ⭐ Если Data нет в памяти — загружаем из базы
                if (string.IsNullOrEmpty(dataJson))
                {
                    try
                    {
                        dataJson = await MainWindow.M.DataService.GetOfferDataAsync(offer.Id);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"⚠️ Не удалось загрузить данные расчёта {offer.N}: {ex.Message}");
                        failedCount++;
                        continue;
                    }
                }

                if (string.IsNullOrEmpty(dataJson))
                {
                    Trace.WriteLine($"⚠️ Данные расчёта {offer.N} отсутствуют в базе");
                    failedCount++;
                    continue;
                }

                try
                {
                    var product = MainWindow.OpenOfferData(dataJson);
                    if (product != null)
                    {
                        details.AddRange(product.Details);
                        comment += $" {offer.N};";
                        loadedCount++;
                    }
                    else
                    {
                        Trace.WriteLine($"⚠️ Не удалось десериализовать данные расчёта {offer.N}");
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"⚠️ Ошибка обработки расчёта {offer.N}: {ex.Message}");
                    failedCount++;
                }
            }

            if (loadedCount == 0)
            {
                MainWindow.M.StatusBegin("Не удалось загрузить данные ни одного расчёта.", MainWindow.StatusMessageType.Error);
                return;
            }

            if (failedCount > 0)
            {
                MainWindow.M.StatusBegin(
                    $"Загружено {loadedCount} из {selectedOffers.Count} расчётов. {failedCount} расчётов пропущено.",
                    MainWindow.StatusMessageType.Warning);
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
            try
            {
                Process.Start("explorer.exe", combinedKpPath);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Не удалось открыть проводник: {ex.Message}");
            }

            // --- Выводим итоговый статус ---
            MainWindow.M.StatusBegin(
                $"Расчеты успешно объединены в папке «{folderName}» (загружено {loadedCount} из {selectedOffers.Count}).",
                MainWindow.StatusMessageType.Success);
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

        /// <summary>
        /// Загружает детали в интерфейс программы.
        /// Поддерживает объединение деталей с одинаковыми названиями из разных расчётов.
        /// </summary>
        public void LoadDetails(List<Detail> details)
        {
            // ⭐ Группируем детали по названию для объединения
            var groupedDetails = details
                .Where(d => !string.IsNullOrEmpty(d.Title))
                .GroupBy(d => d.Title)
                .ToList();

            foreach (var group in groupedDetails)
            {
                string? title = group.Key;

                // ⭐ Ищем существующую деталь или создаём новую
                DetailControl? existingDetail = MainWindow.M.DetailControls
                    .FirstOrDefault(dc => dc.Detail.Title == title);

                bool isNewDetail = existingDetail == null;

                if (isNewDetail)
                {
                    // Создаём новую деталь (автоматически добавит одну TypeDetail + Work)
                    MainWindow.M.AddDetail();
                    existingDetail = MainWindow.M.DetailControls.Last();
                    existingDetail.Detail.Title = title;

                    if (title != null && title.Contains("Комплект"))
                        existingDetail.IsComplectChanged();

                    // Берём свойства из первой детали группы
                    var firstDetail = group.First();
                    existingDetail.Detail.Count = firstDetail.Count;
                    existingDetail.Detail.MillingHoles = firstDetail.MillingHoles;
                    existingDetail.Detail.MillingGrooves = firstDetail.MillingGrooves;
                }

                // ⭐ Собираем все заготовки из всех деталей группы
                var allTypeDetails = group.SelectMany(d => d.TypeDetails).ToList();

                if (existingDetail != null)
                    // ⭐ Индексная модель: проходим по заготовкам
                    for (int j = 0; j < allTypeDetails.Count; j++)
                    {
                        TypeDetailControl _type;

                        if (j < existingDetail.TypeDetailControls.Count)
                        {
                            // ⭐ Используем существующую заготовку (в т.ч. ту, что создана по умолчанию при j==0)
                            _type = existingDetail.TypeDetailControls[j];
                        }
                        else
                        {
                            // ⭐ Создаём новую заготовку
                            existingDetail.AddTypeDetail();
                            _type = existingDetail.TypeDetailControls[^1];
                        }

                        var typeDetail = allTypeDetails[j];

                        // ⭐ Заполняем свойства заготовки
                        _type.TypeDetailDrop.SelectedIndex = typeDetail.Index;
                        _type.Count = typeDetail.Count;
                        _type.MetalDrop.SelectedIndex = typeDetail.Metal;
                        _type.SortDrop.SelectedIndex = typeDetail.Tuple.Item1;
                        _type.A = typeDetail.Tuple.Item2;
                        _type.B = typeDetail.Tuple.Item3;
                        _type.S = typeDetail.Tuple.Item4;
                        _type.L = typeDetail.Tuple.Item5;
                        _type.HasMetal = typeDetail.HasMetal;
                        _type.ExtraResult = typeDetail.ExtraResult;
                        _type.SetComment(typeDetail.Comment);

                        // ⭐ Обрабатываем работы (индексная модель, как в MainWindow)
                        for (int w = 0; w < typeDetail.Works.Count; w++)
                        {
                            var workItem = typeDetail.Works[w];
                            if (workItem.NameWork is null) continue;

                            WorkControl _work;

                            if (w == 0 && j == 0 && isNewDetail)
                            {
                                // ⭐ Для новой заготовки в новой детали используем работу, созданную по умолчанию
                                _work = _type.WorkControls[0];
                            }
                            else if (w < _type.WorkControls.Count)
                            {
                                // ⭐ Используем существующую работу
                                _work = _type.WorkControls[w];
                            }
                            else
                            {
                                // ⭐ Создаём новую работу
                                _type.AddWork();
                                _work = _type.WorkControls[^1];
                            }

                            // ⭐ Проверка дубликатов (как в MainWindow)
                            WorkControl? existingWork = _type.WorkControls.FirstOrDefault(ww =>
                                workItem.NameWork != null && !workItem.NameWork.Contains("Доп") &&
                                (
                                    (ww.WorkDrop?.SelectedItem is Work selectedWork && selectedWork.Name == workItem.NameWork) ||
                                    ww.WorkDrop?.Text == workItem.NameWork
                                ));

                            if (existingWork is not null && existingWork != _work)
                            {
                                // ⭐ Работа уже есть в другой заготовке — обновляем её
                                existingWork.Ratio = workItem.Ratio;
                                existingWork.TechRatio = workItem.TechRatio;
                                existingWork.ExtraResult = workItem.ExtraResult;
                                continue;
                            }

                            // ⭐ Устанавливаем работу по имени
                            foreach (Work workInCombo in _work.WorkDrop.Items)
                            {
                                if (workInCombo.Name == workItem.NameWork)
                                {
                                    _work.WorkDrop.SelectedIndex = _work.WorkDrop.Items.IndexOf(workInCombo);
                                    break;
                                }
                            }

                            // ⭐ Обработка ICut (как в MainWindow)
                            if (_work.workType is ICut _cut)
                            {
                                if (workItem.Items?.Count > 0) _cut.Items = workItem.Items;
                                if (workItem.Parts?.Count > 0) _cut.PartDetails = workItem.Parts;

                                if (_cut is CutControl cut)
                                {
                                    cut.IsGrooved = workItem.IsGrooved;
                                    if (_cut.Items?.Count > 0) cut.SumProperties(_cut.Items);
                                    cut.Parts = cut.PartList();
                                    cut.PartsControl = new(cut, cut.Parts);
                                    cut.AddPartsControl();
                                }
                                else if (_cut is PipeControl pipe)
                                {
                                    pipe.Parts = pipe.PartList();
                                    pipe.PartsControl = new(pipe, pipe.Parts);
                                    pipe.AddPartsControl();
                                    pipe.SetTotalProperties();
                                }
                                else if (_cut is SawControl saw)
                                {
                                    saw.Parts = saw.PartList();
                                    saw.PartsControl = new(saw, saw.Parts);
                                    saw.AddPartsControl();
                                    saw.SetTotalProperties();
                                }

                                // ⭐ Обработка частей
                                if (_cut.Parts?.Count > 0)
                                {
                                    foreach (PartControl part in _cut.Parts)
                                    {
                                        if (part.Part.WorksDict?.Count > 0)
                                        {
                                            foreach (var guid in part.Part.WorksDict.Keys)
                                                part.AddControl((int)MainWindow.Parser(part.Part.WorksDict[guid][0]), guid);
                                        }
                                        else if (part.Part.PropsDict?.Count > 0)
                                        {
                                            foreach (int key in part.Part.PropsDict.Keys)
                                            {
                                                if (key < 50)
                                                    part.AddControl((int)MainWindow.Parser(part.Part.PropsDict[key][0]));
                                            }
                                        }
                                        part.PropertiesChanged?.Invoke(part, false);
                                    }
                                }
                            }

                            // ⭐ Применяем свойства работы
                            _work.propsList = workItem.PropsList;
                            _work.PropertiesChanged?.Invoke(_work, false);
                            _work.Ratio = workItem.Ratio;
                            _work.TechRatio = workItem.TechRatio;
                            _work.ExtraResult = workItem.ExtraResult;
                        }
                    }
            }
        }
    }
}