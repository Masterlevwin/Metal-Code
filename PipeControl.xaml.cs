using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PipeControl.xaml
    /// </summary>
    public partial class PipeControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float mold;
        public float Mold
        {
            get => mold;
            set
            {
                if (value != mold)
                {
                    mold = value;
                    OnPropertyChanged(nameof(Mold));
                }
            }
        }

        private float way;
        public float Way
        {
            get => way;
            set
            {
                if (value != way)
                {
                    way = value;
                    OnPropertyChanged(nameof(Way));
                }
            }
        }

        private int pinhole;
        public int Pinhole
        {
            get => pinhole;
            set
            {
                if (value != pinhole)
                {
                    pinhole = value;
                    OnPropertyChanged(nameof(Pinhole));
                }
            }
        }

        public List<PartControl>? Parts { get; set; }

        public readonly WorkControl work;

        public PipeControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;

            work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
            work.type.Priced += OnPriceChanged;                 // подписка на изменение материала типовой детали

            SetMold($"{work.type.L * work.type.Count * 0.95f/ 1000}");      //переносим погонные метры из типовой детали
        }

        private void SetMold(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetMold(tBox.Text);
        }
        private void SetMold(string _mold)
        {
            if (float.TryParse(_mold, out float m)) Mold = m;
            OnPriceChanged();
        }

        private void SetWay(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetWay(tBox.Text);
        }
        private void SetWay(string _way)
        {
            if (float.TryParse(_way, out float w)) Way = w;
            OnPriceChanged();
        }

        private void SetPinhole(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetPinhole(tBox.Text);
        }
        private void SetPinhole(string _pinhole)
        {
            if (int.TryParse(_pinhole, out int p)) Pinhole = p;
            OnPriceChanged();
        }

        public void OnPriceChanged()
        {
            if (Mold == 0 || Way == 0 || Pinhole == 0) return;
            if (work.type.MetalDrop.SelectedItem is not Metal metal) return;

            float price = 0;

            if (metal.Name != null && MainWindow.M.MetalDict[metal.Name].ContainsKey(work.type.S))
                price = Mold * MainWindow.M.MetalDict[metal.Name][work.type.S].Item3
                    + Way * MainWindow.M.MetalDict[metal.Name][work.type.S].Item1
                    + Pinhole * MainWindow.M.MetalDict[metal.Name][work.type.S].Item2;

            // стоимость резки трубы должна быть не ниже минимальной
            if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

            work.SetResult(price, false);
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (uc is not WorkControl w) return;
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{Mold}");
                w.propsList.Add($"{Way}");
                w.propsList.Add($"{Pinhole}");
            }
            else
            {
                SetMold(w.propsList[0]);
                SetWay(w.propsList[1]);
                SetPinhole(w.propsList[2]);
            }
        }
    }
}
