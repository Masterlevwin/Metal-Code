using ExcelDataReader;
using Metal_Code.Models;
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
                    try
                    {
                        string dateProduction = MainWindow.M.DateProduction.Text;
                        MainWindow.M.UpdateResult();
                        MainWindow.M.DateProduction.Text = dateProduction;

                        if (!MainWindow.M.WarningSave()) return;

                        if (dialogService.SaveFileDialog() == true && dialogService.FilePaths != null)
                        {
                            string _path = Path.GetDirectoryName(dialogService.FilePaths[0])
                            + "\\" + Path.GetFileNameWithoutExtension(dialogService.FilePaths[0]);

                            _path += $" с материалом {MainWindow.M.GetMaterial()}";

                            fileService.Save(_path + ".mcm", MainWindow.M.SaveProduct());

                            if (AssemblyWindow.A.Assemblies.Count > 0)
                            {
                                MessageBoxResult response = MessageBox.Show(
                                    "Сформировать сборочное КП?\nЕсли \"Да\", в КП будут отражены СБОРКИ.\nЕсли \"Нет\", в КП будут отражены ДЕТАЛИ!",
                                    "Выбор формата КП", MessageBoxButton.YesNo, MessageBoxImage.Question);

                                MainWindow.M.isAssemblyOffer = response == MessageBoxResult.Yes;
                            }

                            MainWindow.M.ExportToExcel(dialogService.FilePaths[0]);

                            string selectedFilePath = dialogService.FilePaths[0];
                            string fileName = Path.GetFileName(selectedFilePath);
                            string folderToRename = Path.GetDirectoryName(selectedFilePath)!;
                            string parentDir = Path.GetDirectoryName(folderToRename)!;

                            string newFolderName = $"КП (от {DateTime.Now:dd.MM.yyyy HH-mm})";
                            string destinationPath = Path.Combine(parentDir, newFolderName);

                            MainWindow.M.CreateFolderTagsForCalculation(parentDir);
                            MainWindow.M.CreateSelectedFolders(parentDir);

                            try
                            {
                                Directory.Move(folderToRename, destinationPath);
                                string fullPath = Path.Combine(destinationPath, fileName);

                                // ⭐ СОХРАНЕНИЕ В БАЗУ ЧЕРЕЗ СЕРВИС
                                MainWindow.M.StatusBegin("Сохранение расчета в базу...", MainWindow.StatusMessageType.Info);

                                string now = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                                string? currentManager = MainWindow.M.CurrentManager.Name;
                                string? autor;

                                bool isNewOffer = MainWindow.M.ActiveOffer is null;
                                bool isNumberChanged = MainWindow.M.ActiveOffer?.N != MainWindow.M.Order.Text;
                                bool isSameAuthor = MainWindow.M.ActiveOffer?.Autor == currentManager;

                                if (isNewOffer || isNumberChanged || isSameAuthor)
                                {
                                    autor = $"{currentManager} ({now})";
                                }
                                else
                                {
                                    autor = $"{MainWindow.M.ActiveOffer?.Autor}\n{currentManager} ({now})";
                                }

                                // Сериализуем данные расчета
                                string? dataJson = MainWindow.M.SaveOfferData();

                                // Сохраняем через сервис
                                var savedOffer = await MainWindow.M.DataService.SaveOfferAsync(
                                    orderNumber: MainWindow.M.Order.Text,
                                    companyName: MainWindow.M.CustomerDrop.Text,
                                    amount: MainWindow.M.Result,
                                    material: MainWindow.M.GetMaterial(),
                                    services: MainWindow.M.GetServices(),
                                    isAgent: MainWindow.M.IsAgent,
                                    autor: autor,
                                    actPath: fullPath,
                                    dataJson: dataJson,
                                    managerId: MainWindow.M.TargetManager.Id
                                );

                                // Обновляем ActiveOffer
                                MainWindow.M.ActiveOffer = savedOffer;
                                MainWindow.M.LimitCheck.IsChecked = false;

                                // Обновляем список расчетов в UI
                                await MainWindow.M.LoadManagerDataAsync(MainWindow.M.TargetManager);

                                // ⭐ ПРОКРУТКА К НОВОМУ РАСЧЁТУ С ПОДСВЕТКОЙ
                                MainWindow.M.ScrollToOfferAndHighlight(savedOffer);

                                // ⭐ ФОРМИРУЕМ СООБЩЕНИЕ С УЧЁТОМ ТИПА МЕНЕДЖЕРА
                                bool isDraftManager = MainWindow.M.TargetManager.Name == "Расчетный менеджер";

                                string statusMessage;
                                if (isDraftManager)
                                {
                                    statusMessage = $"Расчет {savedOffer.N} {savedOffer.Company} сохранен локально (расчетный менеджер).";
                                }
                                else if (MainWindow.M.DataService.IsOnline)
                                {
                                    statusMessage = $"Расчет {savedOffer.N} {savedOffer.Company} сохранен на сервере.";
                                }
                                else
                                {
                                    statusMessage = $"Расчет {savedOffer.N} {savedOffer.Company} сохранен локально (ожидает синхронизации).";
                                }

                                MainWindow.M.StatusBegin(statusMessage, MainWindow.StatusMessageType.Success);
                                Trace.WriteLine($"💾 {statusMessage}");

                                // 🔑 Сохраняем последний каталог
                                dialogService.LastUsedDirectory = destinationPath;

                                // Проверка заказчика
                                using var checkCtx = new ManagerContext(MainWindow.M.connections[0]);
                                var customer = await checkCtx.Customers.FirstOrDefaultAsync(x => x.Name == MainWindow.M.CustomerDrop.Text);
                                if (customer is null)
                                {
                                    MainWindow.M.Log += $"\nЗаказчик {MainWindow.M.CustomerDrop.Text} не сохранен в базе. Добавьте его данные в базу, чтобы использовать их повторно.\n";
                                }

                                // Проверка версии
                                if (!MainWindow.M.CheckVersion(out string _version) &&
                                    (MainWindow.M.Log is null || !MainWindow.M.Log.Contains("Текущая версия не актуальна. Рекомендуется обновить программу.")))
                                {
                                    MainWindow.M.Log += $"\nТекущая версия не актуальна. Рекомендуется обновить программу.\n";
                                }

                                // ⭐ ЖДЁМ, ПОКА UI ПОЛНОСТЬЮ ОТРИСУЕТСЯ
                                await System.Threading.Tasks.Task.Delay(500);

                                // Вывод лога
                                if (!string.IsNullOrEmpty(MainWindow.M.Log))
                                {
                                    MessageBox.Show(MainWindow.M.Log, "Обратите внимание!", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    MainWindow.M.Log = null;
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Не удалось переименовать папку:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (dialogService.FilePaths != null && File.Exists(dialogService.FilePaths[0]))
                        {
                            dialogService.ShowMessage("Ошибка: Файл используется другим процессом.");
                        }
                        else dialogService.ShowMessage(ex.Message);
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

                        Trace.WriteLine($"📂 Открытие расчета Id={offer.Id}, N={offer.N}");

                        // Проверяем, загружены ли полные данные
                        if (string.IsNullOrEmpty(offer.Data))
                        {
                            MainWindow.M.StatusBegin($"Загрузка данных расчета {offer.N}...", MainWindow.StatusMessageType.Info);

                            // ⭐ Вызываем сервис напрямую
                            var fullOffer = await MainWindow.M.DataService.LoadOfferDataAsync(offer.Id);

                            if (fullOffer != null)
                            {
                                offer.Data = fullOffer.Data;
                                Trace.WriteLine($"✅ Данные расчета {offer.N} загружены (размер: {offer.Data?.Length ?? 0} байт)");
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
                            MainWindow.M.LoadProduct();
                            MainWindow.M.StatusBegin($"Расчет {MainWindow.M.ActiveOffer.N} загружен.", MainWindow.StatusMessageType.Success);
                            
                            MainWindow.M.HighlightOfferRow(offer);
                            MainWindow.M.CheckAndPromptForUnknownCustomer(MainWindow.M.ActiveOffer);
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"❌ Ошибка открытия расчета: {ex.Message}");
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

                          bool appendMode = response == MessageBoxResult.No; // "Нет" = добавить к существующим

                          // Очистка расчета при режиме "заново"
                          if (!appendMode)
                          {
                              MainWindow.M.ClearDetails();     // удаляем все детали
                              MainWindow.M.ClearCalculate();   // очищаем расчет
                          }

                          if (MainWindow.M.IsRequest)
                              MainWindow.M.CloseRequestControl();

                          System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                          // Формируем путь на основе lastInputDirectory
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
                                      laserComplect.AddTypeDetail();
                                      // устанавливаем "Лазерная резка" в работу по умолчанию
                                      foreach (Work w in MainWindow.M.Works) if (w.Name == "Лазерная резка")
                                      {
                                          laserComplect.TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedItem = w;
                                          break;
                                      }
                                      if (laserComplect.TypeDetailControls[^1].WorkControls[^1].workType is CutControl cut)
                                          cut.LoadExcel(_lasers.ToArray());
                                  }
                                  else
                                  {
                                      MainWindow.M.AddDetail();

                                      // устанавливаем "Лазерная резка" в работу по умолчанию
                                      foreach (Work w in MainWindow.M.Works) if (w.Name == "Лазерная резка")
                                      {
                                          MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedItem = w;
                                          break;
                                      }
                                      if (MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].workType is CutControl cut)
                                          cut.LoadExcel(_lasers.ToArray());
                                  }
                              }

                              if (_tubes.Count > 0)
                              {
                                  var tubeComplect = MainWindow.M.DetailControls.FirstOrDefault(d => d.Detail.Title == "Комплект труб");

                                  if (appendMode && tubeComplect != null)
                                  {
                                      tubeComplect.AddTypeDetail();
                                      // устанавливаем "Труба профильная" в заготовке по умолчанию
                                      foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Труба профильная")
                                      {
                                          tubeComplect.TypeDetailControls[^1].TypeDetailDrop.SelectedItem = t;
                                          foreach (Work w in MainWindow.M.Works) if (w.Name == "Труборез")
                                          {
                                              tubeComplect.TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedItem = w;
                                              break;
                                          }
                                          break;
                                      }
                                      if (tubeComplect.TypeDetailControls[^1].WorkControls[^1].workType is PipeControl pipe)
                                          pipe.LoadExcel(_tubes.ToArray());
                                  }

                                  else
                                  {
                                      MainWindow.M.AddDetail();

                                      // устанавливаем "Труба профильная" в заготовке по умолчанию
                                      foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Труба профильная")
                                      {
                                          MainWindow.M.DetailControls[^1].TypeDetailControls[^1].TypeDetailDrop.SelectedItem = t;
                                          foreach (Work w in MainWindow.M.Works) if (w.Name == "Труборез")
                                          {
                                              MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedItem = w;
                                              break;
                                          }
                                          break;
                                      }
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

                          MainWindow.M.NewProject();        // создаем новый расчет
                          // устанавливаем "Лазерная резка" в работу по умолчанию
                          foreach (Work w in MainWindow.M.Works) if (w.Name == "Лазерная резка")
                              {
                                  MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedItem = w;
                                  break;
                              }
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

                          MainWindow.M.NewProject();        // создаем новый расчет
                          // устанавливаем "Труба профильная" в заготовке по умолчанию
                          foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Труба профильная")
                              {
                                  MainWindow.M.DetailControls[^1].TypeDetailControls[^1].TypeDetailDrop.SelectedItem = t;
                                  foreach (Work w in MainWindow.M.Works) if (w.Name == "Труборез")
                                      {
                                          MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedItem = w;
                                          break;
                                      }
                                  break;
                              }
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