using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows;
using System.Collections.Generic;
using System;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для BendControl.xaml
    /// </summary>
    public partial class BendControl : UserControl, INotifyPropertyChanged
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

        public readonly WorkControl work;

        public BendControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;

            // формирование списка длин стороны гиба
            foreach (string s in MainWindow.M.BendDict[0.5f].Keys) ShelfDrop.Items.Add(s);

            work.PropertiesChanged += SaveOrLoadProperties; // подписка на сохранение и загрузку файла
            work.type.Priced += PriceChanged;               // подписка на изменение материала типовой детали
            BtnEnabled();       // проверяем типовую деталь: если не "Лист металла", делаем кнопку неактивной и наоборот
        }

        private void BtnEnabled()
        {
            if (work.type.TypeDetailDrop.SelectedItem is TypeDetail typeDetail && typeDetail.Name != "Лист металла")
            {
                PartBtn.IsEnabled = false;
                Parts.Clear();
            }
            else PartBtn.IsEnabled = true;
        }

        private void SetRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRatio(tBox.Text);
        }
        private void SetRatio(string _ratio)
        {
            if (float.TryParse(_ratio, out float r)) Ratio = r;     // стандартный парсер избавляет от проблемы с запятой
            PriceChanged();
        }

        private void SetBend(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetBend(tBox.Text);
        }
        private void SetBend(string _bend)
        {
            if (int.TryParse(_bend, out int b)) Bend = b;
            PriceChanged();
        }

        private void SetShelf(object sender, SelectionChangedEventArgs e)
        {
            SetShelf(ShelfDrop.SelectedIndex);
        }
        public void SetShelf(int ndx = 0)
        {
            ShelfDrop.SelectedIndex = ndx;
            PriceChanged();
        }

        public void PriceChanged()
        {
            BtnEnabled();
            Price = 0;

            if (Parts.Count > 0)
            {
                foreach (PartControl p in Parts) foreach (PartBendControl b in p.Bends) Price += b.PriceChanged();
                if (work.WorkDrop.SelectedItem is Work _work) Price -= _work.Price;
            }
            else
            {
                if (Bend == 0) return;

                float _bendRatio = Bend * work.type.Count switch
                {
                    <= 10 => 3,
                    <= 20 => 2,
                    <= 50 => 1.5f,
                    <= 200 => 1,
                    _ => 0.8f,
                };
                if (MainWindow.M.BendDict.ContainsKey(work.type.S))
                    Price = _bendRatio * Bend * work.type.Count * MainWindow.M.BendDict[work.type.S][$"{ShelfDrop.SelectedItem}"];
            }

            Price = (float)Math.Round(Price * Ratio, 2);
            work.SetResult(Price);
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{Bend}");
                w.propsList.Add($"{ShelfDrop.SelectedIndex}");
                w.propsList.Add($"{Ratio}");
            }
            else
            {
                SetBend(w.propsList[0]);
                SetShelf((int)MainWindow.Parser(w.propsList[1]));
                SetRatio(w.propsList[2]);
            }
        }

        public List<PartControl> Parts = new();
        private void ViewBendWindow(object sender, RoutedEventArgs e)
        {
            if (work.type.TypeDetailDrop.SelectedItem is not TypeDetail typeDetail || typeDetail.Name != "Лист металла") return;
            
            foreach (WorkControl w in work.type.WorkControls) if ( w != work && w.workType is CutControl cutControl && cutControl.Parts.Count > 0)
                {
                    if (Parts.Count == 0) Parts.AddRange(cutControl.Parts);

                    BendWindow bendWindow = new(this, Parts);
                    bendWindow.Show();
                }
        }
    }
}
