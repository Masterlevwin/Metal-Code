using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для ZincControl.xaml
    /// </summary>
    public partial class ZincControl : UserControl, INotifyPropertyChanged, IPriceChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float mass;
        public float Mass
        {
            get { return mass; }
            set
            {
                mass = (float)Math.Round(value, 2);
                OnPropertyChanged(nameof(Mass));
            }
        }

        public List<PartControl>? Parts { get; set; }

        public readonly UserControl owner;
        public ZincControl(UserControl _control)
        {
            InitializeComponent();
            owner = _control;
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
                    if (w.workType is ZincControl) return;

                part.work.type.AddWork();

                // добавляем "Цинкование" в список общих работ "Комплекта деталей"
                foreach (Work w in MainWindow.M.Works) if (w.Name == "Цинкование")
                    {
                        part.work.type.WorkControls[^1].WorkDrop.SelectedItem = w;
                        break;
                    }
            }
        }

        private void SetMass()
        {
            Mass = 0;
            if (owner is WorkControl work)
            {
                if (Parts?.Count > 0)
                {
                    foreach (PartControl p in Parts)
                        foreach (ZincControl item in p.UserControls.OfType<ZincControl>())
                            if (item.Mass > 0) Mass += item.Mass;
                }
                else Mass = work.type.Mass * work.type.Count;
            }
            else if (owner is PartControl part)
            {
                Mass = part.Part.Mass * part.Part.Count;
            }
        }

        public void OnPriceChanged()
        {
            SetMass();                      //обновляем текущий вес деталей, отправляемых на оцинковку

            if (owner is not WorkControl work || work.type.MetalDrop.SelectedItem is not Metal metal) return;

            if (metal.Name == "ст3" || metal.Name == "хк" || metal.Name == "09г2с") work.SetResult(Mass * 110, false);
            else MainWindow.M.StatusBegin($"Детали из {metal.Name} на оцинковку не отправляем!");
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
                    if (p.work.type.MetalDrop.SelectedItem is Metal metal &&
                        (metal.Name == "ст3" || metal.Name == "хк" || metal.Name == "09г2с"))
                    {
                        p.Part.PropsDict[p.UserControls.IndexOf(this)] = new() { $"{7}" };
                        if (p.Part.Description != null && !p.Part.Description.Contains(" + Ц ")) p.Part.Description += " + Ц ";

                        p.Part.Price += p.Part.Mass * 110;
                        p.Part.PropsDict[64] = new() { $"{p.Part.Mass * 110}" };
                    }
                }
            }
        }

        private void ShowManual(object sender, MouseWheelEventArgs e)
        {
            PopupZinc.IsOpen = true;
            Manual.Text = $"Цинкование";
        }
    }
}
