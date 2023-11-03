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
    public partial class PaintControl : UserControl, INotifyPropertyChanged
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

            work.PropertiesChanged += SaveOrLoadProperties; // подписка на сохранение и загрузку файла
            work.type.Priced += PriceChanged;               // подписка на изменение свойств типовой детали
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
            PriceChanged();
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

        private void PriceChanged()
        {
            if (work.type.MetalDrop.SelectedItem is Metal metal)
                switch (TypeDrop.SelectedItem)
                {
                    case "кг":
                        Price = work.type.Mass / work.type.S / metal.Density * TypeDict[$"{TypeDrop.SelectedItem}"] * Ratio * work.type.Count;
                        break;
                    case "шт":
                        Price = TypeDict[$"{TypeDrop.SelectedItem}"] * Ratio * work.type.Count;
                        break;
                    case "пог":
                        Price = TypeDict[$"{TypeDrop.SelectedItem}"] * Ratio * work.type.Count;     // здесь нужна формула расчета пог.м
                        break;
                }
            Price = (float)Math.Round(Price, 2);

            work.SetResult(Price);
        }

        public void SaveOrLoadProperties(WorkControl w, bool isSaved)
        {
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add(Ral);
                w.propsList.Add($"{TypeDrop.SelectedIndex}");
                w.propsList.Add($"{Ratio}");
            }
            else
            {
                SetRal(w.propsList[0]);
                SetType((int)MainWindow.Parser(w.propsList[1]));
                SetRatio(w.propsList[2]);
            }
        }

    }
}
