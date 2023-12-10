using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using System.Windows.Input;
using System.IO;
using System.Runtime.Serialization.Json;
using Microsoft.Win32;
using System.Windows;

namespace Metal_Code
{
    public class ProductViewModel : INotifyPropertyChanged
    {
        readonly IFileService fileService;
        readonly IDialogService dialogService;

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
                          if (dialogService.SaveFileDialog() == true) 
                          {
                              string _path = Path.GetDirectoryName(dialogService.FilePaths[0])
                              + "\\" + Path.GetFileNameWithoutExtension(dialogService.FilePaths[0]);

                              if (MainWindow.M.IsLaser) _path += $" с материалом {MainWindow.M.GetMetalPrice()}";

                              fileService.Save(_path + ".mcm", MainWindow.M.SaveProduct());
                              dialogService.ShowMessage("Расчёт сохранен");

                              MainWindow.M.SaveOffer(_path + ".mcm");        //сохраняем расчет в базе данных

                              MainWindow.M.ExportToExcel(dialogService.FilePaths[0]);
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
                          if (dialogService.OpenFileDialog() == true)
                          {
                              Product = fileService.Open(dialogService.FilePaths[0]);
                              dialogService.ShowMessage("Файл открыт");
                              MainWindow.M.LoadProduct();
                          }
                      }
                      catch (Exception ex)
                      {
                          dialogService.ShowMessage(ex.Message);
                      }
                  });
            }
        }

        // команда загрузки сохранения
        private RelayCommand openOfferCommand;
        public RelayCommand OpenOfferCommand
        {
            get
            {
                return openOfferCommand ??= new RelayCommand(obj =>
                  {
                      try
                      {
                          if (MainWindow.M.DetailsGrid.SelectedItem is Offer offer)
                          {
                              Product = fileService.Open(offer.Path);
                              dialogService.ShowMessage("Файл загружен");
                              MainWindow.M.LoadProduct();
                          }
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
                          //Detail detailCopy = new(detail.Title, detail.Count, detail.Price, detail.Total, detail.Mass);
                          //Product.Details.Insert(0, detailCopy);
                      }
                  });
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private Action<object?> execute;
        private Func<object?, bool>? canExecute;

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
            OpenFileDialog openFileDialog = new();
            openFileDialog.Filter = "Metal-Code (*.mcm)|*.mcm|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                FilePaths = openFileDialog.FileNames;
                return true;
            }
            return false;
        }

        public bool SaveFileDialog()
        {
            SaveFileDialog saveFileDialog = new();
            saveFileDialog.Filter = "Excel-File (*.xlsx)|*.xlsx|All files (*.*)|*.*";
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
        Product Open(string filename);
        void Save(string filename, Product product);
    }

    public class JsonFileService : IFileService
    {
        public Product Open(string filename)
        {
            Product product = new();
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