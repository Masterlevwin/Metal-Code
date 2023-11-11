using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public readonly UserControl owner;
        public PaintControl(UserControl _work)
        {
            InitializeComponent();
            owner = _work;

            // формирование списка типов расчета окраски
            foreach (string s in TypeDict.Keys) TypeDrop.Items.Add(s);

            if (owner is WorkControl work)
            {
                work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
                work.type.Priced += OnPriceChanged;                 // подписка на изменение материала типовой детали
                BtnEnable();       // проверяем типовую деталь: если не "Лист металла", делаем кнопку неактивной и наоборот
            }
            else if (owner is PartControl part)
            {
                part.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
                PartBtn.IsEnabled = false;
            }
        }

        private void BtnEnable()
        {
            if (owner is WorkControl work && work.type.TypeDetailDrop.SelectedItem is TypeDetail typeDetail && typeDetail.Name == "Лист металла")
            {
                foreach (WorkControl w in work.type.WorkControls)
                    if (w != owner && w.workType is CutControl cut && cut.WindowParts != null)
                    {
                        if (Parts.Count == 0)
                        {
                            /*foreach (PartControl part in cut.WindowParts.Parts)
                            {
                                PaintControl paint = new(part);
                                part.AddControl(paint);
                            }*/
                            CreateParts(cut.WindowParts.Parts);
                        }
                        PartBtn.IsEnabled = true;
                        break;
                    }
            }
            else PartBtn.IsEnabled = false;
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

        public void OnPriceChanged()        // добавить расчет отдельный Part
        {
            if (owner is not WorkControl work) return;

            BtnEnable();
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
                else if (uc is PartControl p)       // первый элемент списка {2} - это (MenuItem)PartControl.Controls.Items[2]
                    p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{2}", $"{Ral}", $"{TypeDrop.SelectedIndex}" };
            }
            else
            {
                if (uc is WorkControl w)
                {
                    SetRal(w.propsList[0]);
                    SetType((int)MainWindow.Parser(w.propsList[1]));
                }
                else if (uc is PartControl p)
                {
                    SetRal(p.Part.PropsDict[p.UserControls.IndexOf(this)][1]);
                    SetType((int)MainWindow.Parser(p.Part.PropsDict[p.UserControls.IndexOf(this)][2]));
                }
            }
        }

        public List<PartControl> Parts = new();
        private void ViewPartWindow(object sender, RoutedEventArgs e)
        {
            if (owner is not WorkControl work || work.type.TypeDetailDrop.SelectedItem is not TypeDetail typeDetail || typeDetail.Name != "Лист металла") return;

            foreach (WorkControl w in work.type.WorkControls)
                if (w != work && w.workType is CutControl cut && cut.WindowParts != null) cut.WindowParts.ShowDialog();
        }
        public void CreateParts(List<PartControl> parts) { Parts.AddRange(parts); }     // метод, необходимый для корректной загрузки расчета
    }
}
