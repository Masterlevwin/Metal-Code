using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PaintControl.xaml
    /// </summary>
    public partial class PaintControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string? ral;
        public string? Ral
        {
            get => ral;
            set
            {
                ral = value;
                OnPropertyChanged(nameof(Ral));
            }
        }

        public string[] structures = { "глян", "мат", "шагр", "муар" };
        public int priceMeter = 500;    //стоимость обработки 1 квадратного метра

        public ObservableCollection<PartControl>? Parts { get; set; }

        public readonly UserControl owner;
        public PaintControl(UserControl _work)
        {
            InitializeComponent();
            owner = _work;
            Tuning();
        }

        private void Tuning()               // настройка блока после инициализации
        {
            // формирование списка типов расчета окраски
            foreach (string s in structures) TypeDrop.Items.Add(s);

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
            }
        }

        private void SetRal(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tBox && tBox.Text != "") SetRal(tBox.Text);
        }
        public void SetRal(string _ral)
        {
            Ral = _ral;

            if (owner is PartControl part)
            {
                MainWindow.M.IsLoadData = true;

                foreach (var item in part.work.type.WorkControls)
                    if (item.workType is PaintControl paint && paint.Ral == Ral) return;

                part.work.type.AddWork();

                // добавляем "Окраску" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Окраска")
                    {
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        if (part.work.type.WorkControls[^1].workType is PaintControl _paint) _paint.SetRal(Ral);
                        break;
                    }
                MainWindow.M.IsLoadData = false;
            }
            OnPriceChanged();
        }

        private void SetType(object sender, SelectionChangedEventArgs e)
        {
            SetType(TypeDrop.SelectedIndex);
        }
        public void SetType(int ndx = 0)
        {
            TypeDrop.SelectedIndex = ndx;

            if (owner is PartControl part)
                foreach (var item in part.work.type.WorkControls)
                    if (item.workType is PaintControl paint && paint.Ral == Ral)
                    {
                        paint.SetType(ndx);
                        break;
                    }
            OnPriceChanged();
        }

        public void OnPriceChanged()
        {
            if (owner is not WorkControl work) return;

            float price = 0;

            if (Parts?.Count > 0)
            {
                foreach (PartControl p in Parts)
                    foreach (PaintControl item in p.UserControls.OfType<PaintControl>())
                        if (item.Ral != null && item.Ral == Ral)
                        {
                            if (p.Square > 0 && p.Square < .1f) p.Square = .1f;       //расчетная площадь окраски должна быть не меньше 0,1 кв м
                                                                        //к деталям площадью менее 0,3 кв м добавляем стоимость 0,2 кв м за подвес
                            if (p.Square >= .1f && p.Square < .3f) price += priceMeter * p.Part.Count / 10;

                            price += item.Price(p.Square, p.Part.Mass, p.Part.Count, work);
                        }


                // стоимость данной окраски должна быть не ниже минимальной
                if (work.WorkDrop.SelectedItem is Work _work) price = price > 0 && price < _work.Price ? _work.Price : price;

                work.SetResult(price, false);
            }
            else if (Ral != null && work.type.Square > 0 && work.type.Mass > 0)
            {
                if (work.type.Square > 0 && work.type.Square < .1f) work.type.Square = .1f;
                if (work.type.Square >= .1f && work.type.Square < .3f) price += priceMeter * work.type.Count / 10;

                work.SetResult(price + Price(work.type.Square, work.type.Mass, work.type.Count, work));
            }
        }

        private float Price(float _square, float _mass, float _count, WorkControl work)
        {
            float _massRatio = _mass switch             //рассчитываем наценку за тяжелые детали
            {
                <= 50 => 1,
                <= 100 => 1.5f,
                <= 150 => 2,
                _ => 3,
            };
            
            float _destinyRatio = work.type.S switch    //рассчитываем наценку за прогрев толщин
            {
                >= 10 => 1.5f,
                >= 8 => 1.4f,
                >= 5 => 1.3f,
                _ => 1,
            };

            return priceMeter * _square * _massRatio * _count * _destinyRatio;
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (isSaved)
            {
                if (uc is WorkControl w)
                {
                    w.propsList.Clear();
                    w.propsList.Add($"{Ral}");
                    w.propsList.Add($"{TypeDrop.SelectedIndex}");
                }
                else if (uc is PartControl p)
                {
                    if (Ral == null || Ral == "")
                    {
                        p.RemoveControl(this);
                        return;
                    }

                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{2}", $"{Ral}", $"{TypeDrop.SelectedIndex}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + О ")) p.Part.Description += $" + О ({Ral} {TypeDrop.SelectedItem}) ";

                    int count = 0;      //счетчик общего количества деталей

                    if (p.owner is ICut _cut && _cut.PartsControl != null) foreach (PartControl _p in _cut.PartsControl.Parts)
                            foreach (PaintControl item in _p.UserControls.OfType<PaintControl>())
                                if (item.Ral != null && item.Ral == Ral) count += _p.Part.Count;

                    // стоимость всей работы должна быть не ниже минимальной
                    foreach (WorkControl _w in p.work.type.WorkControls)            // находим окраску среди работ и получаем её минималку
                        if (_w.workType is PaintControl paint && paint.Ral == Ral && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            float _send;
                            // если чистая стоимость работы ниже минимальной, к цене детали добавляем
                            if (_w.Result / _w.Ratio / _w.TechRatio > 0 && _w.Result / _w.Ratio / _w.TechRatio <= _work.Price)
                                _send = _work.Price * _w.Ratio * _w.TechRatio / count;  // усредненную часть минималки от общего количества деталей
                            else                                                        // иначе добавляем часть от количества именно этой детали
                                _send = (Price(p.Square, p.Part.Mass, p.Part.Count, p.work) + (p.Square >= .1f && p.Square < .3f ?
                                    priceMeter * p.Part.Count / 10 : 0)) * _w.Ratio * _w.TechRatio / p.Part.Count;

                            p.Part.Price += _send;

                            if (p.Part.PropsDict.ContainsKey(54) && float.TryParse(p.Part.PropsDict[54][0], out float value))
                                p.Part.PropsDict[54][0] = $"{value + _send}";           //блоков окраски может быть несколько
                            else
                                p.Part.PropsDict[54] = new() { $"{_send}", $"{p.Square}", $"{Ral} {TypeDrop.SelectedItem}" };
                            break;
                        }
                }
            }
            else
            {
                if (uc is WorkControl w)
                {
                    SetRal(w.propsList[0]);
                    SetType((int)MainWindow.Parser(w.propsList[1]));
                }
                else if (uc is PartControl p && owner is PartControl _owner)
                {
                    SetRal(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][1]);
                    SetType((int)MainWindow.Parser(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][2]));
                }
            }
        }

        private void Remove(object sender, RoutedEventArgs e) { if (owner is PartControl part) part.RemoveControl(this); }
    }
}
