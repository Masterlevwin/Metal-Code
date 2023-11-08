using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows;
using System.Collections.Generic;
using System.Linq;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для BendControl.xaml
    /// </summary>
    public partial class BendControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private int bend;
        public int Bend
        {
            get => bend;
            set
            {
                if (value != bend)
                {
                    bend = value;
                    OnPropertyChanged(nameof(Bend));
                }
            }
        }

        public readonly UserControl owner;

        public BendControl(UserControl _control)
        {
            InitializeComponent();
            owner = _control;

            // формирование списка длин стороны гиба
            foreach (string s in MainWindow.M.BendDict[0.5f].Keys) ShelfDrop.Items.Add(s);

            if (owner is WorkControl work)
            {
                MessageBox.Show("owner - work");
                work.OnRatioChanged += OnPriceChanged;              // подписка на изменение коэффициента
                work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
                work.type.Priced += OnPriceChanged;                 // подписка на изменение материала типовой детали
                BtnEnable();       // проверяем типовую деталь: если не "Лист металла", делаем кнопку неактивной и наоборот
            }
            else PartBtn.IsEnabled = false;
        }

        private void BtnEnable()
        {
            if (owner is WorkControl work && work.type.TypeDetailDrop.SelectedItem is TypeDetail typeDetail && typeDetail.Name == "Лист металла")
            {
                foreach (WorkControl w in work.type.WorkControls)
                    if (w != owner && w.workType is CutControl cut && cut.WindowParts != null)
                        PartBtn.IsEnabled = true;
            }
            else PartBtn.IsEnabled = false;
        }

        private void SetBend(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetBend(tBox.Text);
        }
        private void SetBend(string _bend)
        {
            if (int.TryParse(_bend, out int b)) Bend = b;
            OnPriceChanged();
        }

        private void SetShelf(object sender, SelectionChangedEventArgs e)
        {
            SetShelf(ShelfDrop.SelectedIndex);
        }
        public void SetShelf(int ndx = 0)
        {
            ShelfDrop.SelectedIndex = ndx;
            OnPriceChanged();
        }

        List<PartControl> Parts = new();
        public void OnPriceChanged()
        {
            if (owner is not WorkControl work) return;

            BtnEnable();
            float price = 0;

            if (Parts.Count > 0)
            {
                foreach (PartControl p in Parts) foreach (BendControl b in p.UserControls.Cast<BendControl>())
                    {
                        MessageBox.Show($"{p.Part.Name}");
                        MessageBox.Show($"{b.Bend * p.Part.Count}");

                        float _price = b.Price(b.Bend * p.Part.Count, work);
                        MessageBox.Show($"{_price}");


                        // стоимость данной гибки должна быть не ниже минимальной
                        if (work.WorkDrop.SelectedItem is Work _work) _price = _price > 0 && _price < _work.Price ? _work.Price : _price;

                        price += _price;
                    }
                work.SetResult(price, false);
            }
            else work.SetResult(Price(Bend * work.type.Count, work));
        }

        private float Price(float _count, WorkControl work)
        {
            float _bendRatio = _count switch
            {
                <= 10 => 3,
                <= 20 => 2,
                <= 50 => 1.5f,
                <= 200 => 1,
                _ => 0.8f,
            };

            return MainWindow.M.BendDict.ContainsKey(work.type.S) ?
                _bendRatio * _count * MainWindow.M.BendDict[work.type.S][$"{ShelfDrop.SelectedItem}"] : 0;
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{Bend}");
                w.propsList.Add($"{ShelfDrop.SelectedIndex}");
            }
            else
            {
                SetBend(w.propsList[0]);
                SetShelf((int)MainWindow.Parser(w.propsList[1]));
            }
        }

        private void ViewPartWindow(object sender, RoutedEventArgs e)
        {
            if (owner is not WorkControl work || work.type.TypeDetailDrop.SelectedItem is not TypeDetail typeDetail || typeDetail.Name != "Лист металла") return;
            
            foreach (WorkControl w in work.type.WorkControls)
                if ( w != work && w.workType is CutControl cut && cut.WindowParts != null)
                {
                    if (Parts.Count == 0)
                    {
                        foreach (PartControl part in cut.WindowParts.Parts)
                        {
                            BendControl bend = new(part);
                            part.AddControl(bend);
                        }
                        Parts.AddRange(cut.WindowParts.Parts);
                    }
                    cut.WindowParts.ShowDialog();
                }      
        }
    }
}
