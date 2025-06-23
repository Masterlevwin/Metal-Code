using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для AquaControl.xaml
    /// </summary>
    public partial class AquaControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        
        private float square;
        public float Square
        {
            get => square;
            set
            {
                square = value;
                OnPropertyChanged(nameof(Square));
            }
        }

        private const int priceMeter = 2000;    //стоимость обработки 1 квадратного метра
        public List<PartControl>? Parts { get; set; }

        public readonly UserControl owner;
        public AquaControl(UserControl _work)
        {
            InitializeComponent();
            owner = _work;
            Tuning();
            OnPriceChanged();
        }

        private void Tuning()               // настройка блока после инициализации
        {
            if (owner is WorkControl work)
            {
                work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
                work.type.Priced += OnPriceChanged;                 // подписка на изменение материала типовой детали

                foreach (WorkControl w in work.type.WorkControls)
                    if (w.workType != this && w.workType is ICut _cut && _cut.PartsControl != null)
                    {
                        Parts = new(_cut.PartsControl.Parts);
                        break;
                    }
            }
            else if (owner is PartControl part)
            {
                part.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла

                foreach (WorkControl w in part.work.type.WorkControls)
                    if (w.workType is AquaControl) return;

                part.work.type.AddWork();

                // добавляем "Аквабластинг" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Аквабластинг")
                    {
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
        }

        private void SetSquare()
        {
            Square = 0;
            if (owner is WorkControl work)
            {
                if (Parts?.Count > 0)
                {
                    foreach (PartControl p in Parts)
                        foreach (AquaControl item in p.UserControls.OfType<AquaControl>())
                            Square += item.Square;
                }
                else Square = work.type.Square * work.type.Count;
            }
            else if (owner is PartControl part)
            {
                Square = part.Square * part.Part.Count;
            }
        }

        public void OnPriceChanged()
        {
            SetSquare();        //обновляем площадь поверхности деталей

            if (owner is not WorkControl work) return;

            float price = 0;

            if (Parts?.Count > 0)
            {
                foreach (PartControl p in Parts)
                    foreach (AquaControl item in p.UserControls.OfType<AquaControl>())
                    {
                        if (item.Square > 0 && item.Square < .1f) item.Square = .1f;       //расчетная площадь аквабластинга должна быть не меньше 0,1 кв м

                        price += priceMeter * item.Square;
                    }

                // стоимость данной окраски должна быть не ниже минимальной
                if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

                work.SetResult(price, false);
            }
            else if (Square > 0 && work.type.Mass > 0)
            {
                if (Square > 0 && Square < .1f) Square = .1f;

                work.SetResult(priceMeter * Square);
            }
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (isSaved)
            {
                if (uc is WorkControl w)
                {
                    w.propsList.Clear();
                }
                else if (uc is PartControl p)
                {
                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{10}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + А ")) p.Part.Description += $" + А ";

                    int count = 0;      //счетчик общего количества деталей

                    if (p.owner is ICut _cut && _cut.PartsControl != null) foreach (PartControl _p in _cut.PartsControl.Parts)
                            foreach (AquaControl item in _p.UserControls.OfType<AquaControl>()) count += _p.Part.Count;

                    // стоимость всей работы должна быть не ниже минимальной
                    foreach (WorkControl _w in p.work.type.WorkControls)            // находим окраску среди работ и получаем её минималку
                        if (_w.workType is AquaControl && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            float _send;
                            // если чистая стоимость работы ниже минимальной, к цене детали добавляем
                            if (_w.Result / _w.Ratio / _w.TechRatio > 0 && _w.Result / _w.Ratio / _w.TechRatio <= _work.Price)
                                _send = _work.Price * _w.Ratio * _w.TechRatio / count;  // усредненную часть минималки от общего количества деталей
                            else                                                        // иначе добавляем часть от количества именно этой детали
                                _send = priceMeter * Square * _w.Ratio * _w.TechRatio / p.Part.Count;

                            p.Part.Price += _send;
                            p.Part.PropsDict[66] = new() { $"{_send}", $"{Square}" };
                            break;
                        }
                }
            }
        }

        private void Remove(object sender, RoutedEventArgs e) { if (owner is PartControl part) part.RemoveControl(this); }
    }
}
