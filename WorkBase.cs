using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Metal_Code
{
    public class WorkBase : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float price;
        public float Price
        {
            get => price;
            set
            {
                price = value;
                OnPropertyChanged(nameof(Price));
            }
        }

        private float ratio;
        public float Ratio
        {
            get => ratio;
            set
            {
                if (value != ratio)
                {
                    ratio = value;
                    OnPropertyChanged(nameof(Ratio));
                }
            }
        }

        private readonly WorkControl work;

        public WorkBase(WorkControl _work)
        {
            work = _work;

            work.PropertiesChanged += SaveOrLoadProperties; // подписка на сохранение и загрузку файла
            work.type.Priced += PriceChanged;               // подписка на изменение материала типовой детали
        }

        private void SetRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRatio(tBox.Text);
        }
        private void SetRatio(string _ratio)
        {
            if (float.TryParse(_ratio, out float r)) Ratio = r; // стандартный парсер избавляет от проблемы с запятой
            PriceChanged();
        }

        private void PriceChanged()
        {
            work.SetResult(Price);
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{Ratio}");
            }
            else
            {
                SetRatio(w.propsList[0]);
            }
        }
    }
}
