using System.ComponentModel;
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

        private float price;
        public float Price
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
            OnPriceChanged();
        }

        private void SetPrice(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetPrice(tBox.Text);
        }
        private void SetPrice(string _price)
        {
            if (float.TryParse(_price, out float p)) Price = p;
            OnPriceChanged();
        }

        public void OnPriceChanged()
        {
            work.SetResult(Price * work.type.Count, false);
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
