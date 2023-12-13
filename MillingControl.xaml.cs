﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для MillingControl.xaml
    /// </summary>
    public partial class MillingControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float hours;
        public float Hours
        {
            get => hours;
            set
            {
                if (value != hours)
                {
                    hours = value;
                    OnPropertyChanged(nameof(Hours));
                }
            }
        }

        public readonly WorkControl work;

        public MillingControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;

            work.PropertiesChanged += SaveOrLoadProperties;     // подписка на сохранение и загрузку файла
            work.type.Priced += OnPriceChanged;                 // подписка на изменение материала типовой детали
        }

        private void SetHours(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetHours(tBox.Text);
        }
        private void SetHours(string _hours)
        {
            if (float.TryParse(_hours, out float h)) Hours = h;
            OnPriceChanged();
        }

        public void OnPriceChanged()
        {
            if (Hours == 0) return;

            if (work.WorkDrop.SelectedItem is Work _work) work.SetResult(_work.Price * Hours, false);
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (uc is not WorkControl w) return;
            if (isSaved)
            {
                w.propsList.Clear();
                w.propsList.Add($"{Hours}");
            }
            else
            {
                SetHours(w.propsList[0]);
            }
        }
    }
}