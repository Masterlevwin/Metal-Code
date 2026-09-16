using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Metal_Code
{
    public partial class ExtraControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private bool _isLoading = false;  // ← Флаг для защиты от перезаписи propsList при загрузке

        private string? nameExtra;
        public string? NameExtra
        {
            get => nameExtra;
            set
            {
                if (value != nameExtra)
                {
                    nameExtra = value;
                    OnPropertyChanged(nameof(NameExtra));

                    // Обновляем propsList только если НЕ идёт загрузка
                    if (!_isLoading)
                        UpdatePropsList();
                }
            }
        }

        private string? price;
        public string? Price
        {
            get => price;
            set
            {
                if (value != price)
                {
                    price = value;
                    OnPropertyChanged(nameof(Price));

                    // Обновляем propsList только если НЕ идёт загрузка
                    if (!_isLoading)
                        UpdatePropsList();

                    OnPriceChanged();
                }
            }
        }

        public Guid Id { get; } = Guid.NewGuid();
        public ObservableCollection<PartControl>? Parts { get; set; }

        public readonly WorkControl work;

        public ExtraControl(WorkControl _work)
        {
            InitializeComponent();
            work = _work;

            work.PropertiesChanged += SaveOrLoadProperties;
            work.type.Priced += OnPriceChanged;
        }

        /// <summary>
        /// Синхронизирует propsList с текущими значениями свойств.
        /// Вызывается автоматически при каждом изменении NameExtra или Price (кроме загрузки).
        /// </summary>
        private void UpdatePropsList()
        {
            if (work.propsList == null)
                work.propsList = new List<string>();

            work.propsList.Clear();
            work.propsList.Add(NameExtra ?? "");
            work.propsList.Add(Price ?? "");
        }

        private void SetName(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetName(tBox.Text);
        }

        private void SetName(string _name)
        {
            if (_name != null && _name != "") NameExtra = _name;
        }

        private void SetPrice(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetPrice(tBox.Text);
        }

        public void SetPrice(string _price)
        {
            Price = _price;
        }

        private float ParserPrice(string _price)
        {
            try
            {
                object result = new DataTable().Compute(_price, null);
                if (float.TryParse($"{result}", out float f)) return f;
            }
            catch
            {
                MainWindow.M.StatusBegin("В поле стоимости доп работы должно быть число или математическое выражение");
            }
            return 0;
        }

        public void OnPriceChanged()
        {
            if (Price != null && Price != "")
                work.SetResult(ParserPrice(Price), false);
        }

        public void SaveOrLoadProperties(UserControl uc, bool isSaved)
        {
            if (uc is not WorkControl w) return;
            if (w.propsList == null) return;

            if (isSaved)
            {
                // При сохранении обновляем propsList
                UpdatePropsList();
            }
            else
            {
                // При загрузке устанавливаем флаг, чтобы сеттеры не перезаписывали propsList
                _isLoading = true;

                try
                {
                    if (w.propsList.Count >= 1 && !string.IsNullOrEmpty(w.propsList[0]))
                        NameExtra = w.propsList[0];

                    if (w.propsList.Count >= 2 && !string.IsNullOrEmpty(w.propsList[1]))
                    {
                        Price = w.propsList[1];
                        OnPriceChanged();
                    }
                }
                finally
                {
                    _isLoading = false;
                }
            }
        }
    }
}