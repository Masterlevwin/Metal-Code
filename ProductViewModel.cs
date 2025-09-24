using ExcelDataReader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
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
        readonly IFileService fileService;
        public readonly IDialogService dialogService;

        public ProductViewModel(IDialogService _dialogService, IFileService _fileService, Product product)
        {
            dialogService = _dialogService;
            fileService = _fileService;
            Product = product;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public Product Product;
        Detail selectedDetail;      //временно не используется
        public Detail SelectedDetail
        {
            get { return selectedDetail; }
            set
            {
                selectedDetail = value;
                OnPropertyChanged(nameof(SelectedDetail));
            }
        }

        // команда обновления общей стоимости
        private RelayCommand updateCommand;
        public RelayCommand UpdateCommand
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
        private RelayCommand newProjectCommand;
        public RelayCommand NewProjectCommand
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
        private RelayCommand saveCommand;
        public RelayCommand SaveCommand
        {
            get
            {
                return saveCommand ??= new RelayCommand(obj =>
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

                              _path += $" с материалом {MainWindow.M.GetMetalPrice()}";

                              fileService.Save(_path + ".mcm", MainWindow.M.SaveProduct());     //сохраняем расчет в папке

                              if (AssemblyWindow.A.Assemblies.Count > 0)
                              {
                                  MessageBoxResult response = MessageBox.Show(
                                      "Сформировать сборочное КП?\nЕсли \"Да\", в КП будут отражены СБОРКИ.\nЕсли \"Нет\", в КП будут отражены ДЕТАЛИ!",
                                      "Выбор формата КП", MessageBoxButton.YesNo, MessageBoxImage.Question);

                                  if (response == MessageBoxResult.Yes) MainWindow.M.isAssemblyOffer = true;
                                  else MainWindow.M.isAssemblyOffer = false;
                              }
                              MainWindow.M.ExportToExcel(dialogService.FilePaths[0]);           //формируем КП в формате excel
                              MainWindow.M.SaveOrRemoveOffer(true, dialogService.FilePaths[0]); //сохраняем расчет в базе данных
                          }
                      }
                      catch (Exception ex)
                      {
                          dialogService.ShowMessage(ex.Message);
                      }
                  });
            }
        }

        // команда открытия файла
        private RelayCommand openCommand;
        public RelayCommand OpenCommand
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
                              MainWindow.M.StatusBegin($"Расчет {Product?.Order} открыт.");
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
        private RelayCommand openOfferCommand;
        public RelayCommand OpenOfferCommand
        {
            get
            {
                return openOfferCommand ??= new RelayCommand(obj =>
                  {
                      try
                      {
                          if (MainWindow.M.OffersGrid.SelectedItem is not Offer offer) return;
                          if (offer.Data != null)
                          {
                              MainWindow.M.ActiveOffer = offer;
                              Product = MainWindow.OpenOfferData(offer.Data);
                              MainWindow.M.LoadProduct();
                              MainWindow.M.StatusBegin($"Расчет {MainWindow.M.ActiveOffer.N} загружен.");
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
        private RelayCommand mergeOffersCommand;
        public RelayCommand MergeOffersCommand
        {
            get
            {
                return mergeOffersCommand ??= new RelayCommand(obj =>
                  {
                      try
                      {
                          if (MainWindow.M.OffersGrid.SelectedItems.Count < 2)
                          {
                              dialogService.ShowMessage("Выбрано меньше двух расчетов для объединения.");
                              return;
                          }

                          MainWindow.M.ClearDetails();      //очищаем текущий расчет

                          List<Detail> details = new();
                          string comment = "Объединенное КП из:";
                          foreach (Offer offer in MainWindow.M.OffersGrid.SelectedItems)
                          {
                              if (offer.Data != null)
                              {
                                  Product = MainWindow.OpenOfferData(offer.Data);   //десериализуем расчет
                                  if (Product != null)
                                  {
                                      details.AddRange(Product.Details);    //собираем детали в список
                                      comment += $" {offer.N};";            //собираем номера расчетов
                                  }
                              }
                          }
                          LoadDetails(details);                 //загружаем все детали
                          MainWindow.M.Comment.Text = comment;  //формируем комментарий с ссылками
                          MainWindow.M.StatusBegin($"Расчеты успешно объединены.");
                      }
                      catch (Exception ex) { dialogService.ShowMessage(ex.Message); }
                  });
            }
        }

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

        // команда загрузки раскладок и отчетов Excel общей кнопкой
        private RelayCommand loadCommand;
        public RelayCommand LoadCommand
        {
            get
            {
                return loadCommand ??= new RelayCommand(obj =>
                  {
                      try
                      {
                          MessageBoxResult response = MessageBox.Show(
                              "Загрузить раскладки?\nЕсли \"Да\", текущий расчет будет очищен!",
                              "Загрузка раскладок", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                          if (response == MessageBoxResult.No) return;

                          MainWindow.M.NewProject();        // создаем новый расчет

                          if (MainWindow.M.IsRequest) MainWindow.M.CloseRequestControl();

                          System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                          OpenFileDialog openFileDialog = new()
                          {
                              Filter = "All files (*.*)|*.*",
                              Multiselect = true
                          };

                          if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames != null)
                          {
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
                                  MainWindow.M.StatusBegin($"{metalix.Run()}");
                              }

                              if (_lasers.Count > 0)
                              {
                                  if (_metalix.Count > 0) MainWindow.M.AddDetail();

                                  // устанавливаем "Лазерная резка" в работу по умолчанию
                                  foreach (Work w in MainWindow.M.Works) if (w.Name == "Лазерная резка")
                                      {
                                          MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].WorkDrop.SelectedItem = w;
                                          break;
                                      }
                                  if (MainWindow.M.DetailControls[^1].TypeDetailControls[^1].WorkControls[^1].workType is CutControl cut)
                                      cut.LoadExcel(_lasers.ToArray());
                              }

                              if (_tubes.Count > 0)
                              {
                                  if (_lasers.Count > 0 || _metalix.Count > 0) MainWindow.M.AddDetail();

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
        private RelayCommand loadExcelCommand;
        public RelayCommand LoadExcelCommand
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
        private RelayCommand loadTubeCommand;
        public RelayCommand LoadTubeCommand
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
        private RelayCommand addCommand;
        public RelayCommand AddCommand
        {
            get
            {
                return addCommand ??= new RelayCommand(obj =>
                  {
                      Detail detail = new();
                      Product.Details.Insert(0, detail);
                      SelectedDetail = detail;
                  });
            }
        }

        // команда удаления объекта
        private RelayCommand removeCommand;
        public RelayCommand RemoveCommand
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
        private RelayCommand removeOfferCommand;
        public RelayCommand RemoveOfferCommand
        {
            get
            {
                return removeOfferCommand ??= new RelayCommand(obj =>
                  {
                      if (MainWindow.M.ManagerDrop.SelectedItem is Manager man && MainWindow.M.CurrentManager == man)
                      {
                          MessageBoxResult response = MessageBox.Show("Уверены? Расчет будет удален как из локальной базы, так и из основной!", "Удаление расчета",
                              MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                          if (response == MessageBoxResult.No) return;
                      }
                      else
                      {
                          MessageBoxResult response = MessageBox.Show("Уверены? Расчет будет удален из локальной базы!", "Удаление расчета",
                              MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                          if (response == MessageBoxResult.No) return;
                      }
                      MainWindow.M.SaveOrRemoveOffer(false);
                  });
            }
        }

        // команда копирования объекта
        private RelayCommand doubleCommand;
        public RelayCommand DoubleCommand
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
        void ShowMessage(string message);   // показ сообщения
        string[]? FilePaths { get; set; }   // путь к выбранному файлу
        bool OpenFileDialog();  // открытие файла
        bool SaveFileDialog();  // сохранение файла
    }

    public class DefaultDialogService : IDialogService
    {
        public string[]? FilePaths { get; set; }

        public bool OpenFileDialog()
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Metal-Code (*.mcm)|*.mcm|All files (*.*)|*.*"
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
                FileName = $"КП {MainWindow.M.Order.Text}"
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