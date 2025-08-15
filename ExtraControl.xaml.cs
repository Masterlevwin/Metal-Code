using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для ExtraControl.xaml
    /// </summary>
    public partial class ExtraControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string? nameExtra;
        public string? NameExtra
        {
            get => nameExtra;
            set
            {
                if (value != nameExtra)
                {
                    nameExtra = value;
                    OnPropertyChanged(nameof(NameExtra));
                }
            }
        }

        private string? price;
        public string? Price
        {
            get => price;
            set
            {
                if (value != price)
                {
                    price = value;
                    OnPropertyChanged(nameof(Price));
                }
            }
        }

        public ObservableCollection<PartControl>? Parts { get; set; }

        public readonly WorkControl work;

        public ExtraControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;

            work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
            work.type.Priced += OnPriceChanged;                 // подписка на изменение типовой детали
        }

        private void SetName(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetName(tBox.Text);
        }
        private void SetName(string _name)
        {
            if (_name != null && _name != "") NameExtra = _name;
        }

        private void SetPrice(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetPrice(tBox.Text);
        }
        public void SetPrice(string _price)
        {
            Price = _price;
            OnPriceChanged();
        }
        private float ParserPrice(string _price)
        {
            try
            {
                object result = new DataTable().Compute(_price, null);
                if (float.TryParse($"{result}", out float f)) return f;
            }
            catch
            {
                MainWindow.M.StatusBegin("В поле стоимости доп работы должно быть число или математическое выражение");
            }
            return 0;
        }

        public void OnPriceChanged()
        {
            if (Price != null && Price != "") work.SetResult(ParserPrice(Price), false);
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (uc is not WorkControl w) return;
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{NameExtra}");
                w.propsList.Add($"{Price}");
            }
            else
            {
                SetName(w.propsList[0]);
                SetPrice(w.propsList[1]);
            }
        }
    }
}
