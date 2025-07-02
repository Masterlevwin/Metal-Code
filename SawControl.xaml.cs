using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для SawControl.xaml
    /// </summary>
    public partial class SawControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private bool usedAssistant = false;
        public bool UsedAssistant
        {
            get => usedAssistant;
            set
            {
                usedAssistant = value;
                OnPropertyChanged(nameof(UsedAssistant));
            }
        }

        public List<PartControl>? Parts { get; set; }
        
        public Dictionary<double, float> DestinyDict = new()
        {
            [.5f] = 1,
            [.7f] = 1,
            [.8f] = 1,
            [1] = 1,
            [1.2f] = 1,
            [1.5f] = 1,
            [2] = 1.3f,
            [2.5] = 1.3f,
            [3] = 1.6f,
            [4] = 2,
            [5] = 2.5f,
            [6] = 3,
            [8] = 4,
            [10] = 6,
            [12] = 8,
            [14] = 10,
        };

        public readonly WorkControl work;

        public SawControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;

            work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
            work.type.Priced += OnPriceChanged;                 // подписка на изменение типовой детали
        }

        private void SetAssistant(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cBox && cBox.IsChecked is not null) UsedAssistant = (bool)cBox.IsChecked;
            OnPriceChanged();
        }

        public void OnPriceChanged()
        {
            float destiny = MainWindow.M.CorrectDestiny(work.type.S);    //получаем расчетную толщину

            if (work.WorkDrop.SelectedItem is not Work _work
                || work.type.MetalDrop.SelectedItem is not Metal _metal
                || !DestinyDict.ContainsKey(destiny)) return;
      
            work.SetResult(_work.Price +                    //минимальная стоимость работы +
                (_work.Time + DestinyDict[destiny]          //(минимальное время работы + коэф за толщину
                + MainWindow.M.MetalRatioDict[_metal]       //+ коэф за металл
                + MainWindow.MassRatio(work.type.Mass))     //+ коэф за вес заготовки)
            * (UsedAssistant ? 1.5f : 1) * 2000 / 60        //* коэф за помощника
            * work.type.det.Detail.Count                    //* количество деталей
            , false);
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (uc is not WorkControl w) return;
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{UsedAssistant}");
            }
            else if (w.propsList.Count > 0 && bool.TryParse(w.propsList[0], out bool prop))
            {
                UsedAssistant = prop;
            }
        }
    }
}
