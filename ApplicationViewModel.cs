using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System;
using System.Windows.Input;
using System.IO;
using System.Runtime.Serialization.Json;
using Microsoft.Win32;
using System.Windows;

namespace Metal_Code
{
    public class ApplicationViewModel : INotifyPropertyChanged
    {
        readonly IFileService fileService;
        readonly IDialogService dialogService;

        public ApplicationViewModel(IDialogService _dialogService, IFileService _fileService)
        {
            dialogService = _dialogService;
            fileService = _fileService;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));


        Detail selectedDetail;
        public Detail SelectedDetail
        {
            get { return selectedDetail; }
            set
            {
                selectedDetail = value;
                OnPropertyChanged("SelectedDetail");
            }
        }

        public ObservableCollection<Detail> Details { get; set; } = new ObservableCollection<Detail>();

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
                              fileService.Save(dialogService.FilePath, MainWindow.M.SaveDetails());
                              dialogService.ShowMessage("Файл сохранен");
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
                              var details = fileService.Open(dialogService.FilePath);
                              Details.Clear();
                              foreach (Detail d in details) Details.Add(d);
                              dialogService.ShowMessage("Файл открыт");
                              MainWindow.M.LoadDetails();
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
                return addCommand ??
                  (addCommand = new RelayCommand(obj =>
                  {
                      Detail detail = new();
                      Details.Insert(0, detail);
                      SelectedDetail = detail;
                  }));
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
                      if (obj is Detail detail) Details.Remove(detail);
                  },
                 (obj) => Details.Count > 0);
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
                          Detail detailCopy = new(detail.N, detail.Title, detail.Count,detail.Price, detail.Total);
                          Details.Insert(0, detailCopy);
                      }
                  });
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private Action<object> execute;
        private Func<object, bool> canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            execute(parameter);
        }
    }

    public interface IDialogService
    {
        void ShowMessage(string message);   // показ сообщения
        string FilePath { get; set; }   // путь к выбранному файлу
        bool OpenFileDialog();  // открытие файла
        bool SaveFileDialog();  // сохранение файла
    }

    public class DefaultDialogService : IDialogService
    {
        public string? FilePath { get; set; }

        public bool OpenFileDialog()
        {
            OpenFileDialog openFileDialog = new();
            openFileDialog.Filter = "Metal-Code (*.mcm)|*.mcm|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                FilePath = openFileDialog.FileName;
                return true;
            }
            return false;
        }

        public bool SaveFileDialog()
        {
            SaveFileDialog saveFileDialog = new();
            saveFileDialog.Filter = "Metal-Code (*.mcm)|*.mcm|All files (*.*)|*.*";
            if (saveFileDialog.ShowDialog() == true)
            {
                FilePath = saveFileDialog.FileName;
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
        ObservableCollection<Detail> Open(string filename);
        void Save(string filename, ObservableCollection<Detail> details);
    }

    public class JsonFileService : IFileService
    {
        public ObservableCollection<Detail> Open(string filename)
        {
            ObservableCollection<Detail>? details = new();
            DataContractJsonSerializer jsonFormatter = new(typeof(ObservableCollection<Detail>));

            using (FileStream fs = new(filename, FileMode.OpenOrCreate))
            {
                details = jsonFormatter.ReadObject(fs) as ObservableCollection<Detail>;
            }

            return details;
        }

        public void Save(string filename, ObservableCollection<Detail> details)
        {
            DataContractJsonSerializer jsonFormatter = new(typeof(ObservableCollection<Detail>));
            using FileStream fs = new(filename, FileMode.Create);
            jsonFormatter.WriteObject(fs, details);

            MainWindow.M.ExportToExcel(filename);
        }
    }
}