using ExcelDataReader;
using Metal_Code.Models;
using Metal_Code.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Metal_Code
{
    public class ProductViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public Product Product;
        public readonly IFileService fileService;
        public readonly IDialogService dialogService;

        public ProductViewModel(IDialogService _dialogService, IFileService _fileService, Product product)
        {
            dialogService = _dialogService;
            fileService = _fileService;
            Product = product;
        }


        // команда обновления общей стоимости
        private RelayCommand? updateCommand;
        public RelayCommand? UpdateCommand
        {
            get
            {
                return updateCommand ??= new RelayCommand(obj =>
                {
                    MainWindow.M.UpdateResult();
                });
            }
        }

        // команда создания нового проекта
        private RelayCommand? newProjectCommand;
        public RelayCommand? NewProjectCommand
        {
            get
            {
                return newProjectCommand ??= new RelayCommand(obj =>
                {
                    MessageBoxResult response = MessageBox.Show(
                        "Создать новый расчет?\nЕсли \"Да\", текущий расчет будет очищен!",
                        "Создание расчета", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                    if (response == MessageBoxResult.No) return;

                    MainWindow.M.NewProject();
                });
            }
        }

        // команда сохранения файла
        private RelayCommand? saveCommand;
        public RelayCommand? SaveCommand
        {
            get
            {
                return saveCommand ??= new RelayCommand(async obj =>
                {
                    string currentStep = "Инициализация";
                    try
                    {
                        string dateProduction = MainWindow.M.DateProduction.Text;
                        MainWindow.M.UpdateResult();
                        MainWindow.M.DateProduction.Text = dateProduction;

                        if (!MainWindow.M.WarningSave()) return;

                        if (dialogService.SaveFileDialog() != true || dialogService.FilePaths == null)
                            return;

                        string _path = Path.GetDirectoryName(dialogService.FilePaths[0])
                            + "\\" + Path.GetFileNameWithoutExtension(dialogService.FilePaths[0]);
                        _path += $" с материалом {MainWindow.M.GetMaterial()}";

                        currentStep = "Сохранение .mcm";
                        fileService.Save(_path + ".mcm", MainWindow.M.SaveProduct());

                        if (AssemblyWindow.A.Assemblies.Count > 0)
                        {
                            MessageBoxResult response = MessageBox.Show(
                                "Сформировать сборочное КП?\nЕсли \"Да\", в КП будут отражены СБОРКИ.\nЕсли \"Нет\", в КП будут отражены ДЕТАЛИ!",
                                "Выбор формата КП", MessageBoxButton.YesNo, MessageBoxImage.Question);

                            MainWindow.M.isAssemblyOffer = response == MessageBoxResult.Yes;
                        }

                        currentStep = "Экспорт в Excel";
                        MainWindow.M.ExportToExcel(dialogService.FilePaths[0]);

                        string selectedFilePath = dialogService.FilePaths[0];
                        string fileName = Path.GetFileName(selectedFilePath);
                        string folderToRename = Path.GetDirectoryName(selectedFilePath)!;
                        string parentDir = Path.GetDirectoryName(folderToRename)!;

                        string notePart = MainWindow.SanitizeFolderPart(MainWindow.M.OfferNote.Text);
                        string newFolderName = string.IsNullOrWhiteSpace(notePart)
                            ? $"КП (от {DateTime.Now:dd.MM.yyyy HH-mm})"
                            : $"КП_{notePart} (от {DateTime.Now:dd.MM.yyyy HH-mm})";

                        string destinationPath = Path.Combine(parentDir, newFolderName);

                        // ============================================================
                        // ЗОНА 1: файловые операции (папки + переименование)
                        // ============================================================
                        string fullPath;
                        try
                        {
                            currentStep = "Переименование КП";
                            MainWindow.M.CreateFolderTagsForCalculation(parentDir);
                            MainWindow.M.CreateSelectedFolders(parentDir);
                            Directory.Move(folderToRename, destinationPath);
                            fullPath = Path.Combine(destinationPath, fileName);
                        }
                        catch (Exception fileEx)
                        {
                            LogFullException(fileEx, currentStep);
                            ShowDiagnosticMessage(fileEx, currentStep);
                            return;
                        }

                        // --- Автоматический экспорт PDF раскладок (лазер) ---
                        try
                        {
                            currentStep = "Экспорт PDF раскладок (лазер)";
                            foreach (var detail in MainWindow.M.DetailControls)
                                foreach (var typeDetail in detail.TypeDetailControls)
                                    foreach (var work in typeDetail.WorkControls)
                                    {
                                        if (work.workType is not CutControl cut || cut.PartsControl == null || cut.Items == null)
                                            continue;

                                        var sheetsToExport = cut.Items
                                            .Where(i => i.NestingSheet != null && i.NestingSheet.Parts.Count > 0)
                                            .ToList();
                                        if (!sheetsToExport.Any()) continue;

                                        string metalName = cut.work.type.MetalDrop.Text;
                                        float thickness = cut.work.type.S;

                                        string description;
                                        if ((metalName.Contains("ст") && thickness >= 3) || (metalName.Contains("хк") && thickness < 3)) description = $"s{thickness}";
                                        else if (metalName.Contains("амг2")) description = $"al{thickness}";
                                        else if (metalName.Contains("амг") || metalName.Contains("д16")) description = $"al{thickness} {metalName}";
                                        else if (metalName.Contains("латунь")) description = $"br{thickness}";
                                        else if (metalName.Contains("медь")) description = $"cu{thickness}";
                                        else description = $"s{thickness} {metalName}";

                                        if (cut.IsGrooved) description += " рифл";
                                        if (cut.work.type.CheckMetal.IsChecked is false) description += " Давальч";

                                        foreach (char c in Path.GetInvalidFileNameChars())
                                            description = description.Replace(c, '_');

                                        string laserFolder = Path.Combine(destinationPath, "Раскладки");
                                        if (!Directory.Exists(laserFolder))
                                            Directory.CreateDirectory(laserFolder);

                                        cut.PartsControl.GenerateNestingPdf(Path.Combine(laserFolder, $"{description}.pdf"));
                                    }
                        }
                        catch (Exception pdfEx)
                        {
                            LogFullException(pdfEx, currentStep);
                            MainWindow.M.Log += $"\n⚠️ Не удалось сформировать PDF раскладки: {pdfEx.Message}";
                        }

                        // --- Автоматический экспорт PDF раскладок (трубы / лентопил) ---
                        try
                        {
                            currentStep = "Экспорт PDF раскладок (трубы/лентопил)";
                            foreach (var detail in MainWindow.M.DetailControls)
                                foreach (var typeDetail in detail.TypeDetailControls)
                                    foreach (var work in typeDetail.WorkControls)
                                    {
                                        if (work.workType is PipeControl pipe && pipe.PartsControl != null && pipe.Items != null)
                                        {
                                            bool hasStocks = pipe.Items.OfType<LaserItem>()
                                                .Any(i => i.PipeStocks != null && i.PipeStocks.Count > 0);
                                            if (hasStocks)
                                            {
                                                string targetFolder = Path.Combine(destinationPath, "Труборез");
                                                if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
                                                string _fileName = pipe.PartsControl.GetPipeNestingFileName(isSaw: false);
                                                pipe.PartsControl.GeneratePipeNestingPdf(Path.Combine(targetFolder, _fileName), isSaw: false);
                                            }
                                        }

                                        if (work.workType is SawControl saw && saw.PartsControl != null && saw.Items != null)
                                        {
                                            bool hasStocks = saw.Items.OfType<LaserItem>()
                                                .Any(i => i.PipeStocks != null && i.PipeStocks.Count > 0);
                                            if (hasStocks)
                                            {
                                                string targetFolder = Path.Combine(destinationPath, "Лентопил");
                                                if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
                                                string _fileName = saw.PartsControl.GetPipeNestingFileName(isSaw: true);
                                                saw.PartsControl.GeneratePipeNestingPdf(Path.Combine(targetFolder, _fileName), isSaw: true);
                                            }
                                        }
                                    }
                        }
                        catch (Exception pipePdfEx)
                        {
                            LogFullException(pipePdfEx, currentStep);
                            MainWindow.M.Log += $"\n⚠️ Не удалось сформировать PDF раскладки труб: {pipePdfEx.Message}";
                        }

                        // ============================================================
                        // ЗОНА 2: сохранение в базу данных
                        // ============================================================
                        try
                        {
                            currentStep = "Сохранение в базу";
                            MainWindow.M.StatusBegin("Сохранение расчета в базу...", MainWindow.StatusMessageType.Info);

                            string now = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                            string? currentManager = MainWindow.M.CurrentManager.Name;
                            string? autor;

                            bool isNewOffer = MainWindow.M.ActiveOffer is null;
                            bool isNumberChanged = MainWindow.M.ActiveOffer?.N != MainWindow.M.Order.Text;
                            bool isSameAuthor = MainWindow.M.ActiveOffer?.Autor == currentManager;

                            if (isNewOffer || isNumberChanged || isSameAuthor)
                                autor = $"{currentManager} ({now})";
                            else
                                autor = $"{MainWindow.M.ActiveOffer?.Autor}\n{currentManager} ({now})";

                            string? dataJson = MainWindow.M.SaveOfferData();

                            var savedOffer = await RetryAsync(
                                () => MainWindow.M.DataService.SaveOfferAsync(
                                    orderNumber: MainWindow.M.Order.Text,
                                    companyName: MainWindow.M.CustomerDrop.Text,
                                    amount: MainWindow.M.Result,
                                    material: MainWindow.M.GetMaterial(),
                                    services: MainWindow.M.GetServices(),
                                    isAgent: MainWindow.M.IsAgent,
                                    autor: autor,
                                    actPath: fullPath,
                                    dataJson: dataJson,
                                    managerId: MainWindow.M.TargetManager.Id,
                                    note: MainWindow.M.OfferNote.Text),
                                maxAttempts: 3);

                            if (savedOffer is null)
                                throw new InvalidOperationException("DataService.SaveOfferAsync вернул null");

                            MainWindow.M.ActiveOffer = savedOffer;
                            MainWindow.M.LimitCheck.IsChecked = false;

                            currentStep = "Обновление списка расчётов";
                            await MainWindow.M.LoadManagerDataAsync(MainWindow.M.TargetManager);

                            MainWindow.M.ScrollToOfferAndHighlight(savedOffer);

                            bool isDraftManager = MainWindow.M.TargetManager.Name == "Расчетный менеджер";
                            string statusMessage;
                            if (isDraftManager)
                                statusMessage = $"Расчет {savedOffer.N} {savedOffer.Company} сохранен локально (расчетный менеджер).";
                            else if (MainWindow.M.DataService.IsOnline)
                                statusMessage = $"Расчет {savedOffer.N} {savedOffer.Company} сохранен на сервере.";
                            else
                                statusMessage = $"Расчет {savedOffer.N} {savedOffer.Company} сохранен локально (ожидает синхронизации).";

                            MainWindow.M.StatusBegin(statusMessage, MainWindow.StatusMessageType.Success);

                            dialogService.LastUsedDirectory = destinationPath;

                            currentStep = "Проверка заказчика";
                            using var checkCtx = new ManagerContext(MainWindow.M.connections[0]);
                            var customer = await checkCtx.Customers
                                .FirstOrDefaultAsync(x => x.Name == MainWindow.M.CustomerDrop.Text);
                            if (customer is null)
                            {
                                MainWindow.M.Log +=
                                    $"\nЗаказчик {MainWindow.M.CustomerDrop.Text} не сохранен в базе. " +
                                    "Добавьте его данные в базу, чтобы использовать их повторно.\n";
                            }

                            currentStep = "Проверка версии";
                            if (!MainWindow.M.CheckVersion(out string _version) &&
                                (MainWindow.M.Log is null ||
                                 !MainWindow.M.Log.Contains("Текущая версия не актуальна. Рекомендуется обновить программу.")))
                            {
                                MainWindow.M.Log += "\nТекущая версия не актуальна. Рекомендуется обновить программу.\n";
                            }
                        }
                        catch (AutorLimitExceededException autorEx)
                        {
                            // Ожидаемая ситуация. Логировать как ошибку не нужно,
                            // `_isOnline` остался true — следующие расчёты пойдут на сервер.
                            MessageBox.Show(
                                autorEx.Message + "\n\n" +
                                "Текущий расчёт не сохранён ни на сервере, ни локально.\n" +
                                "Измените номер заказа в поле «Заказ» и повторите сохранение — " +
                                "расчёт будет сохранён как новый, история авторов начнётся заново.",
                                "Лимит пересохранений",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);

                            MainWindow.M.StatusBegin(
                                "Расчёт не сохранён: достигнут лимит пересохранений",
                                MainWindow.StatusMessageType.Info);
                            return;
                        }
                        catch (Exception dbEx)
                        {
                            LogFullException(dbEx, currentStep);
                            ShowDiagnosticMessage(dbEx, currentStep);
                            return;
                        }

                        await System.Threading.Tasks.Task.Delay(500);

                        if (!string.IsNullOrEmpty(MainWindow.M.Log))
                        {
                            MessageBox.Show(MainWindow.M.Log, "Обратите внимание!",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            MainWindow.M.Log = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogFullException(ex, currentStep);

                        if (!string.IsNullOrEmpty(MainWindow.M.Log))
                        {
                            try
                            {
                                File.AppendAllText(LogPath,
                                    "\n--- Накопленный Log ---\n" + MainWindow.M.Log);
                            }
                            catch { /* лог не должен ронять приложение */ }
                            MainWindow.M.Log = null;
                        }

                        ShowDiagnosticMessage(ex, currentStep);
                    }
                });
            }
        }

        // команда открытия файла
        private RelayCommand? openCommand;
        public RelayCommand? OpenCommand
        {
            get
            {
                return openCommand ??= new RelayCommand(obj =>
                  {
                      try
                      {
                          if (dialogService.OpenFileDialog() == true && dialogService.FilePaths != null)
                          {
                              MainWindow.M.ActiveOffer = null;
                              Product = fileService.Open(dialogService.FilePaths[0]);
                              MainWindow.M.LoadProduct();
                              MainWindow.M.StatusBegin($"Расчет {Product?.Order} открыт.", MainWindow.StatusMessageType.Success);
                              MainWindow.M.PdfMigrate(dialogService.FilePaths[0]);
                          }
                      }
                      catch (Exception ex)
                      {
                          dialogService.ShowMessage(ex.Message);
                      }
                  });
            }
        }

        // команда загрузки расчета
        private RelayCommand? openOfferCommand;
        public RelayCommand? OpenOfferCommand
        {
            get
            {
                return openOfferCommand ??= new RelayCommand(async obj =>
                {
                    try
                    {
                        if (MainWindow.M.OffersGrid.SelectedItem is not Offer offer) return;
                        if (MainWindow.M.DataService == null)
                        {
                            dialogService.ShowMessage("Сервис данных не инициализирован.");
                            return;
                        }

                        // Проверяем, загружены ли полные данные
                        if (string.IsNullOrEmpty(offer.Data))
                        {
                            MainWindow.M.StatusBegin($"Загрузка данных расчета {offer.N}...", MainWindow.StatusMessageType.Info);

                            // ⭐ Вызываем сервис напрямую
                            var fullOffer = await MainWindow.M.DataService.LoadOfferDataAsync(offer.Id);

                            if (fullOffer != null)
                            {
                                offer.Data = fullOffer.Data;
                            }
                            else
                            {
                                dialogService.ShowMessage($"Не удалось загрузить данные расчета {offer.N}");
                                return;
                            }
                        }

                        if (!string.IsNullOrEmpty(offer.Data))
                        {
                            MainWindow.M.ActiveOffer = offer;
                            Product = MainWindow.OpenOfferData(offer.Data);

                            // ⭐ НОВОЕ: Синхронизируем Note из Offer в Product (защита от рассинхронизации)
                            if (Product != null && !string.IsNullOrEmpty(offer.Note))
                            {
                                Product.Note = offer.Note;
                            }

                            MainWindow.M.LoadProduct();
                            MainWindow.M.StatusBegin($"Расчет {MainWindow.M.ActiveOffer.N} загружен.", MainWindow.StatusMessageType.Success);

                            MainWindow.M.HighlightOfferRow(offer);
                            MainWindow.M.CheckAndPromptForUnknownCustomer(MainWindow.M.ActiveOffer);
                        }
                    }
                    catch (Exception ex)
                    {
                        dialogService.ShowMessage(ex.Message);
                    }
                });
            }
        }

        // команда объединения расчетов в одно КП
        private RelayCommand? mergeOffersCommand;
        public RelayCommand? MergeOffersCommand
        {
            get
            {
                return mergeOffersCommand ??= new RelayCommand(async obj =>
                {
                    try
                    {
                        if (MainWindow.M.OffersGrid.SelectedItems.Count < 2)
                        {
                            dialogService.ShowMessage("Выбрано меньше двух расчетов для объединения.");
                            return;
                        }

                        MainWindow.M.ClearDetails();      // очищаем текущий расчет
                        MergeOffer merger = new();         // ⭐ исправлено: было "merge", стало "merger"
                        await merger.RunAsync();           // ⭐ асинхронный вызов
                    }
                    catch (Exception ex)
                    {
                        dialogService.ShowMessage(ex.Message);
                    }
                });
            }
        }

        // команда загрузки раскладок и отчетов Excel общей кнопкой
        private RelayCommand? loadCommand;
        public RelayCommand? LoadCommand
        {
            get
            {
                return loadCommand ??= new RelayCommand(obj =>
                {
                    try
                    {
                        MessageBoxResult response = MessageBox.Show(
                            "Выберите действие:\n\n" +
                            "• Да — Очистить текущий расчет и загрузить заново\n" +
                            "• Нет — Добавить раскладки к существующим комплектам\n" +
                            "• Отмена — Отменить загрузку",
                            "Загрузка раскладок",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question);

                        if (response == MessageBoxResult.Cancel)
                            return;

                        bool appendMode = response == MessageBoxResult.No;

                        if (!appendMode)
                        {
                            MainWindow.M.ClearDetails();
                            MainWindow.M.ClearCalculate();
                        }

                        if (MainWindow.M.IsRequest)
                            MainWindow.M.CloseRequestControl();

                        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                        string? targetDirectory = null;
                        if (MainWindow.M.lastInputDirectory != null && Directory.Exists(Path.GetDirectoryName(MainWindow.M.lastInputDirectory)))
                        {
                            var _targetDirectory = Path.GetDirectoryName(MainWindow.M.lastInputDirectory);
                            if (_targetDirectory != null) targetDirectory = Path.Combine(_targetDirectory, "КП");
                        }

                        OpenFileDialog openFileDialog = new()
                        {
                            InitialDirectory = targetDirectory,
                            Filter = "Файлы раскладок (*.xlsx;*.xls)|*.xlsx;*.xls|Все файлы (*.*)|*.*",
                            Multiselect = true,
                            Title = "Выберите файлы раскладок"
                        };

                        if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames != null)
                        {
                            dialogService.LastUsedDirectory = Path.GetDirectoryName(openFileDialog.FileName);

                            List<string> _lasers = new(), _tubes = new(), _metalix = new();

                            foreach (string path in openFileDialog.FileNames)
                            {
                                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read);
                                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                                DataSet result = reader.AsDataSet();
                                DataTable table = result.Tables[0];

                                if ($"{table.Rows[0].ItemArray[0]}".Contains("Заказ")) _metalix.Add(path);
                                else if ($"{table.Rows[0].ItemArray[0]}".Contains("Полный список вакансий")) _lasers.Add(path);
                                else _tubes.Add(path);
                            }

                            if (_metalix.Count > 0)
                            {
                                Metalix metalix = new(_metalix[0]);
                                MainWindow.M.StatusBegin($"{metalix.Run()}", MainWindow.StatusMessageType.Success);
                            }

                            if (_lasers.Count > 0)
                            {
                                var laserComplect = MainWindow.M.DetailControls.FirstOrDefault(d => d.Detail.Title == "Комплект деталей");

                                if (appendMode && laserComplect != null)
                                {
                                    // Создаём заготовку с предустановленным "Лист металла"
                                    laserComplect.AddTypeDetail("Лист металла");
                                    if (laserComplect.TypeDetailControls[^1].WorkControls[^1].workType is CutControl cut)
                                        cut.LoadExcel(_lasers.ToArray());
                                }
                                else
                                {
                                    MainWindow.M.AddDetail();
                                    // Создаём заготовку с предустановленным "Лист металла"
                                    MainWindow.M.DetailControls[^1].AddTypeDetail("Лист металла");
                                    if (MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].workType is CutControl cut)
                                        cut.LoadExcel(_lasers.ToArray());
                                }
                            }

                            if (_tubes.Count > 0)
                            {
                                var tubeComplect = MainWindow.M.DetailControls.FirstOrDefault(d => d.Detail.Title == "Комплект труб");

                                if (appendMode && tubeComplect != null)
                                {
                                    // Создаём заготовку с предустановленным "Труба профильная"
                                    tubeComplect.AddTypeDetail("Труба профильная");
                                    if (tubeComplect.TypeDetailControls[^1].WorkControls[^1].workType is PipeControl pipe)
                                        pipe.LoadExcel(_tubes.ToArray());
                                }
                                else
                                {
                                    MainWindow.M.AddDetail();
                                    // Создаём заготовку с предустановленным "Труба профильная"
                                    MainWindow.M.DetailControls[^1].AddTypeDetail("Труба профильная");
                                    if (MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].workType is PipeControl pipe)
                                        pipe.LoadExcel(_tubes.ToArray());
                                }
                            }
                        }

                        if (MainWindow.M.Log is not null && MainWindow.M.Log != "")
                        {
                            MessageBox.Show(MainWindow.M.Log, "Обратите внимание!", MessageBoxButton.OK, MessageBoxImage.Warning);
                            MainWindow.M.Log = null;
                        }
                    }
                    catch (IOException ex) when ((ex.HResult & 0xFFFF) == 32)
                    {
                        dialogService.ShowMessage("Ошибка: Файл используется другим процессом.");
                    }
                    catch (Exception ex)
                    {
                        dialogService.ShowMessage(ex.Message);
                    }
                });
            }
        }

        // команда загрузки раскладок Excel из блока работы
        private RelayCommand? loadExcelCommand;
        public RelayCommand? LoadExcelCommand
        {
            get
            {
                return loadExcelCommand ??= new RelayCommand(obj =>
                {
                    try
                    {
                        MessageBoxResult response = MessageBox.Show(
                            "Загрузить раскладки?\nЕсли \"Да\", текущий расчет будет очищен!",
                            "Загрузка раскладок", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                        if (response == MessageBoxResult.No) return;

                        MainWindow.M.NewProject();

                        // Создаём заготовку с предустановленным типом "Лист металла"
                        // SetDefaultWork автоматически установит "Лазерная резка"
                        MainWindow.M.DetailControls[^1].AddTypeDetail("Лист металла");

                        if (MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].workType is CutControl cut)
                            cut.LoadFiles();
                    }
                    catch (IOException ex) when ((ex.HResult & 0xFFFF) == 32)
                    {
                        dialogService.ShowMessage("Ошибка: Файл используется другим процессом.");
                    }
                    catch (Exception ex)
                    {
                        dialogService.ShowMessage(ex.Message);
                    }
                });
            }
        }

        // команда загрузки отчетов труб
        private RelayCommand? loadTubeCommand;
        public RelayCommand? LoadTubeCommand
        {
            get
            {
                return loadTubeCommand ??= new RelayCommand(obj =>
                {
                    try
                    {
                        MessageBoxResult response = MessageBox.Show(
                            "Загрузить отчеты труб?\nЕсли \"Да\", текущий расчет будет очищен!",
                            "Загрузка отчетов труб", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                        if (response == MessageBoxResult.No) return;

                        MainWindow.M.NewProject();

                        // Создаём заготовку с предустановленным типом "Труба профильная"
                        // SetDefaultWork автоматически установит "Труборез"
                        MainWindow.M.DetailControls[^1].AddTypeDetail("Труба профильная");

                        if (MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].workType is PipeControl pipe)
                            pipe.LoadFiles();
                    }
                    catch (IOException ex) when ((ex.HResult & 0xFFFF) == 32)
                    {
                        dialogService.ShowMessage("Ошибка: Файл используется другим процессом.");
                    }
                    catch (Exception ex)
                    {
                        dialogService.ShowMessage(ex.Message);
                    }
                });
            }
        }

        // команда добавления нового объекта
        private RelayCommand? addCommand;
        public RelayCommand? AddCommand
        {
            get
            {
                return addCommand ??= new RelayCommand(obj =>
                  {
                      Detail detail = new();
                      Product.Details.Insert(0, detail);
                  });
            }
        }

        // команда удаления объекта
        private RelayCommand? removeCommand;
        public RelayCommand? RemoveCommand
        {
            get
            {
                return removeCommand ??= new RelayCommand(obj =>
                  {
                      if (obj is Detail detail) Product.Details.Remove(detail);
                  },
                 (obj) => Product.Details.Count > 0);
            }
        }

        // команда удаления КП
        private RelayCommand? removeOfferCommand;
        public RelayCommand? RemoveOfferCommand
        {
            get
            {
                return removeOfferCommand ??= new RelayCommand(async obj =>
                {
                    try
                    {
                        if (MainWindow.M.OffersGrid.SelectedItem is not Offer offer) return;

                        var currentManager = MainWindow.M.CurrentManager;
                        if (currentManager == null)
                        {
                            MainWindow.M.StatusBegin("Пользователь не определён", MainWindow.StatusMessageType.Error);
                            return;
                        }

                        // ⭐ ПРОВЕРКА: является ли расчёт "расчетным"
                        bool isDraftOffer = await MainWindow.M.DataService.IsDraftOfferAsync(offer.Id);

                        // ⭐ ПРОВЕРКА ПРАВ ДОСТУПА (только для обычных расчётов)
                        if (!isDraftOffer)
                        {
                            bool isOwner = currentManager.Id == offer.ManagerId;
                            bool isAdmin = currentManager.IsAdmin;
                            bool isEngineer = currentManager.IsEngineer;

                            // Инженеры не могут удалять расчёты (как и редактировать)
                            if (isEngineer)
                            {
                                MainWindow.M.StatusBegin("Инженеры не могут удалять расчёты", MainWindow.StatusMessageType.Warning);
                                return;
                            }

                            // Менеджер может удалять только свои расчёты (админ — любые)
                            if (!isOwner && !isAdmin)
                            {
                                string ownerName = "неизвестно";
                                try
                                {
                                    using var localCtx = new ManagerContext(MainWindow.M.connections[0]);
                                    var owner = await localCtx.Managers.AsNoTracking()
                                        .FirstOrDefaultAsync(m => m.Id == offer.ManagerId);
                                    if (owner != null) ownerName = owner.Name ?? ownerName;
                                }
                                catch { /* игнорируем ошибку получения имени */ }

                                MessageBox.Show(
                                    $"Вы не можете удалить расчёт {offer.N}.\n" +
                                    $"Этот расчёт принадлежит менеджеру «{ownerName}».\n\n" +
                                    $"Удалять расчёты может только их владелец или администратор.",
                                    "Доступ запрещён",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                                return;
                            }
                        }

                        // ⭐ Формируем сообщение в зависимости от типа расчёта
                        string message;
                        bool isOnline = MainWindow.M.DataService.IsOnline;

                        if (isDraftOffer)
                        {
                            // ⭐ Расчетный расчёт — всегда только локальное удаление
                            message = $"Уверены? Предварительный расчёт {offer.N} будет удалён из локальной базы.\n\n" +
                                      $"Этот расчёт является тестовым и не хранится на сервере.";
                        }
                        else
                        {
                            bool isOwner = currentManager.Id == offer.ManagerId;
                            bool isAdmin = currentManager.IsAdmin;

                            if (isOwner)
                            {
                                message = isOnline
                                    ? $"Уверены? Расчёт {offer.N} будет удалён из основной базы (сервер)."
                                    : $"Уверены? Расчёт {offer.N} будет удалён из локальной базы.";
                            }
                            else
                            {
                                string ownerName = "неизвестно";
                                try
                                {
                                    using var localCtx = new ManagerContext(MainWindow.M.connections[0]);
                                    var owner = await localCtx.Managers.AsNoTracking()
                                        .FirstOrDefaultAsync(m => m.Id == offer.ManagerId);
                                    if (owner != null) ownerName = owner.Name ?? ownerName;
                                }
                                catch { /* игнорируем */ }

                                message = isOnline
                                    ? $"Уверены? Расчёт {offer.N} менеджера «{ownerName}» будет удалён из основной базы (сервер).\n\nДействие от имени администратора."
                                    : $"Уверены? Расчёт {offer.N} менеджера «{ownerName}» будет удалён из локальной базы.\n\nДействие от имени администратора.";
                            }
                        }

                        var response = MessageBox.Show(message, "Удаление расчёта",
                            MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                        if (response != MessageBoxResult.Yes) return;

                        MainWindow.M.StatusBegin($"Удаление расчёта {offer.N}...", MainWindow.StatusMessageType.Info);

                        bool removed = await MainWindow.M.DataService.RemoveOfferAsync(offer.Id);

                        if (removed)
                        {
                            MainWindow.M.CurrentOffers.Remove(offer);
                            MainWindow.M.InitializeOffersView();
                            await MainWindow.M.UpdateOffersCountCacheAsync();
                            MainWindow.M.SummaryInfoTextBlock.Text = $"Всего расчётов: {MainWindow.M.CurrentOffers.Count} шт.";

                            MainWindow.M.StatusBegin($"Расчёт {offer.N} удалён.", MainWindow.StatusMessageType.Success);

                            string logSuffix = isDraftOffer ? " (расчетный расчёт)"
                                            : (currentManager.Id == offer.ManagerId ? "" : " (администратор)");
                            Trace.WriteLine($"✅ Расчёт {offer.N} (Id={offer.Id}) удалён пользователем {currentManager.Name}{logSuffix}");
                        }
                        else
                        {
                            dialogService.ShowMessage($"Не удалось удалить расчёт {offer.N}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"❌ Ошибка удаления расчёта: {ex.Message}");
                        dialogService.ShowMessage(ex.Message);
                    }
                });
            }
        }

        // команда копирования объекта
        private RelayCommand? doubleCommand;
        public RelayCommand? DoubleCommand
        {
            get
            {
                return doubleCommand ??= new RelayCommand(obj =>
                  {
                      if (obj is Detail detail)
                      {

                      }
                  });
            }
        }

        // ================================================================
        // Диагностические помощники
        // ================================================================

        private static readonly string LogPath =
            Path.Combine(AppContext.BaseDirectory, "logs", "save-errors.log");

        private void LogFullException(Exception ex, string step)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var sb = new StringBuilder();
                sb.AppendLine(new string('=', 80));
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Шаг: {step}");
                sb.AppendLine($"Тип: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"StackTrace:\n{ex.StackTrace}");
                if (ex.InnerException is not null)
                {
                    sb.AppendLine("--- InnerException ---");
                    sb.AppendLine(ex.InnerException.ToString());
                }
                File.AppendAllText(LogPath, sb.ToString());
                Trace.WriteLine(sb.ToString());
            }
            catch
            {
                // Лог не должен ронять приложение
            }
        }

        private void ShowDiagnosticMessage(Exception ex, string step)
        {
            var root = ex;
            while (root.InnerException is not null) root = root.InnerException;

            MessageBox.Show(
                $"Не удалось сохранить расчёт.\n\n" +
                $"Шаг: {step}\n" +
                $"Ошибка: {root.Message}\n\n" +
                $"Если ошибка повторяется, пришлите разработчику файл:\n{LogPath}",
                "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static bool IsTransientDbError(Exception? ex)
        {
            while (ex is not null)
            {
                if (ex is TimeoutException) return true;
                ex = ex.InnerException;
            }
            return false;
        }

        private static async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxAttempts)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (attempt < maxAttempts && IsTransientDbError(ex))
                {
                    await System.Threading.Tasks.Task.Delay(1000 * attempt);
                }
            }
        }

    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> execute;
        private readonly Func<object?, bool>? canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return canExecute == null || canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            execute(parameter);
        }
    }

    public interface IDialogService
    {
        string[]? FilePaths { get; set; }   // путь к выбранному файлу
        string LastUsedDirectory { get; set; }  // путь к последней выбранной директории

        void ShowMessage(string message);   // показ сообщения
        bool OpenFileDialog();  // открытие файла
        bool SaveFileDialog();  // сохранение файла
    }

    public class DefaultDialogService : IDialogService
    {
        private string _lastUsedDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public string LastUsedDirectory
        {
            get => _lastUsedDirectory;
            set => _lastUsedDirectory = value?.Trim() is { Length: > 0 } ? Path.GetFullPath(value) : null;
        }

        public string[]? FilePaths { get; set; }

        public bool OpenFileDialog()
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Metal-Code (*.mcm)|*.mcm",
                InitialDirectory = LastUsedDirectory
            };
            if (openFileDialog.ShowDialog() == true)
            {
                FilePaths = openFileDialog.FileNames;
                return true;
            }
            return false;
        }

        public bool SaveFileDialog()
        {
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "Excel-File (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                FileName = $"КП {MainWindow.M.Order.Text} от {DateTime.Now:d}",
                InitialDirectory = LastUsedDirectory
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                FilePaths = saveFileDialog.FileNames;
                return true;
            }
            return false;
        }

        public void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }
    }

    public interface IFileService
    {
        Product? Open(string filename);
        void Save(string filename, Product product);
    }

    public class JsonFileService : IFileService
    {
        public Product? Open(string filename)
        {
            Product? product = new();
            DataContractJsonSerializer jsonFormatter = new(typeof(Product));

            using (FileStream fs = new(filename, FileMode.OpenOrCreate))
            {
                product = jsonFormatter.ReadObject(fs) as Product;
            }

            return product;
        }

        public void Save(string filename, Product product)
        {
            DataContractJsonSerializer jsonFormatter = new(typeof(Product));
            using FileStream fs = new(filename, FileMode.Create);
            jsonFormatter.WriteObject(fs, product);
        }
    }
}