using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

        private readonly WorkControl work;
        public PaintControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;

            // формирование списка видов расчета окраски
            foreach (string s in TypeDict.Keys) TypeDrop.Items.Add(s);

            work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
            work.type.Priced += OnPriceChanged;                   // подписка на изменение свойств типовой детали
        }

        private void SetRal(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRal(tBox.Text);
        }
        public void SetRal(string _ral)
        {
            Ral = _ral;
        }

        private Dictionary<string, float> TypeDict = new()
        {
            ["кг"] = 812,
            ["шт"] = 350,
            ["пог"] = 87
        };
        private void SetType(object sender, SelectionChangedEventArgs e)
        {
            SetType(TypeDrop.SelectedIndex);
        }
        public void SetType(int ndx = 0)
        {
            TypeDrop.SelectedIndex = ndx;
            OnPriceChanged();
        }

        public void OnPriceChanged()
        {
            float price = 0;
            if (work.type.MetalDrop.SelectedItem is not Metal metal) return;
                switch (TypeDrop.SelectedItem)
                {
                    case "кг":
                        price = work.type.Mass / work.type.S / metal.Density * TypeDict[$"{TypeDrop.SelectedItem}"] * work.type.Count;
                        break;
                    case "шт":
                        price = TypeDict[$"{TypeDrop.SelectedItem}"] * work.type.Count;
                        break;
                    case "пог":
                        price = TypeDict[$"{TypeDrop.SelectedItem}"] * work.type.Count;     // здесь нужна формула расчета пог.м
                        break;
                }
            price = (float)Math.Round(price, 2);

            work.SetResult(price);
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{Ral}");
                w.propsList.Add($"{TypeDrop.SelectedIndex}");
            }
            else
            {
                SetRal(w.propsList[0]);
                SetType((int)MainWindow.Parser(w.propsList[1]));
            }
        }
    }
}
