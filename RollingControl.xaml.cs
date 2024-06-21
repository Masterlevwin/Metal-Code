using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для RollingControl.xaml
    /// </summary>
    public partial class RollingControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float side;
        public float Side
        {
            get => side;
            set
            {
                if (value != side)
                {
                    side = value;
                    OnPropertyChanged(nameof(Side));
                }
            }
        }

        public List<PartControl>? Parts { get; set; }

        public List<string> Sides = new() { "(А) выс", "(В) шир" };

        public Dictionary<double, float> DestinyDict = new()
        {
            [.5f] = 0,
            [.7f] = 0,
            [.8f] = 0,
            [1] = .1f,
            [1.2f] = .1f,
            [1.5f] = .1f,
            [2] = .2f,
            [2.5] = .2f,
            [3] = 2,
            [4] = 3,
            [5] = 4,
            [6] = 5,
        };

        public readonly UserControl owner;
        public RollingControl(UserControl _control)
        {
            InitializeComponent();
            owner = _control;
            Tuning();
        }

        private void Tuning()               // настройка блока после инициализации
        {
            // формирование списка сторон расчета вальцовки
            foreach (string s in Sides) TypeDrop.Items.Add(s);

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

                //PartBtn.Visibility = Visibility.Visible;
                //PartBtn.Click += (o, e) => { part.RemoveControl(this); };

                foreach (WorkControl w in part.work.type.WorkControls)
                    if (w.workType is RollingControl roll) return;

                part.work.type.AddWork();

                // добавляем "Вальцовку" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Вальцовка")
                    {
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
        }
        
        private void SetType(object sender, SelectionChangedEventArgs e)
        {
            SetType(TypeDrop.SelectedIndex);
        }
        public void SetType(int ndx = 0)
        {
            TypeDrop.SelectedIndex = ndx;
            OnPriceChanged();
        }

        private void SetSide(int ndx = 0)       //метод определения стороны вальцовки
        {
            if (owner is WorkControl w && (Parts == null || Parts.Count == 0))
                Side = ndx == 0 ? w.type.A : w.type.B;
            else if (owner is PartControl p && p.Part.PropsDict.ContainsKey(100))
                Side = ndx == 0 ? MainWindow.Parser(p.Part.PropsDict[100][0]) : MainWindow.Parser(p.Part.PropsDict[100][1]);
        }

        public void OnPriceChanged()
        {
            SetSide(TypeDrop.SelectedIndex);    //переопределяем значение стороны вальцовки при каждой изменении параметров типовой детали

            if (owner is not WorkControl work || work.WorkDrop.SelectedItem is not Work _work) return;

            float price = 0;

            if (Parts?.Count > 0)
            {
                foreach (PartControl p in Parts)
                    foreach (RollingControl item in p.UserControls.OfType<RollingControl>())
                        if (item.Side > 0 && item.Side < 2000)
                            price += (_work.Price / p.Part.Count + Time(item.Side, p.Part.Mass, work) * 2000 / 60) * p.Part.Count;
            }
            else if (Side > 0 && Side < 2000 && work.type.Mass > 0)
            {
                price = (_work.Price / work.type.Count + Time(Side, work.type.Mass, work) * 2000 / 60) * work.type.Count;
            }
            work.SetResult(price, false);
        }

        private float Time(float _side, float _mass, WorkControl work)
        {
            if (work.WorkDrop.SelectedItem is not Work _work || work.type.MetalDrop.SelectedItem is not Metal metal) return 0;

            return DestinyDict.ContainsKey(work.type.S) ?
                _work.Time * (DestinyDict[work.type.S] + SideRatio(_side) + MainWindow.M.MetalRatioDict[metal] + MainWindow.MassRatio(_mass)) : 0;
        }

        private float SideRatio(float side)         //метод определения коэффициента взависимости от длины вальцуемой стороны
        {
            float _sideRatio = side switch
            {
                <= 50 => 1,
                <= 100 => 1.2f,
                <= 200 => 1.5f,
                <= 400 => 1.7f,
                <= 500 => 2,
                <= 1000 => 2.5f,
                _ => 3
            };
            return _sideRatio;
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (isSaved)
            {
                if (uc is WorkControl w)
                {
                    w.propsList.Clear();
                    w.propsList.Add($"{TypeDrop.SelectedIndex}");
                }
                else if (uc is PartControl p)
                {
                    if (!p.Part.PropsDict.ContainsKey(100) || Side == 0)
                    {
                        p.RemoveControl(this);
                        return;
                    }

                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{6}", $"{TypeDrop.SelectedIndex}" };
                    if (p.Part.Description != null && !p.Part.Description.Contains(" + В ")) p.Part.Description += " + В ";

                    foreach (WorkControl _w in p.work.type.WorkControls)        // находим вальцовку среди работ и получаем её минималку
                        if (_w.workType is RollingControl && _w.WorkDrop.SelectedItem is Work _work)
                        {
                            float _send = (_work.Price / p.Part.Count + Time(Side, p.Part.Mass, _w) * 2000 / 60) * _w.Ratio * _w.TechRatio;
                            p.Part.Price += _send;
                            p.Part.PropsDict[58] = new() { $"{_send}" };

                            break;
                        }
                }
            }
            else
            {
                if (uc is WorkControl w)
                {
                    SetType((int)MainWindow.Parser(w.propsList[0]));
                }
                else if (uc is PartControl p && owner is PartControl _owner)
                {
                    SetType((int)MainWindow.Parser(p.Part.PropsDict[_owner.UserControls.IndexOf(this)][1]));
                }
            }
        }
    }
}
