using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WorkControl.xaml
    /// </summary>
    public partial class WorkControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public List<string> propsList = new();
        public delegate void PropsChanged(UserControl w, bool b);
        public PropsChanged? PropertiesChanged;
        
        private float result;
        public float Result
        {
            get => result;
            set
            {
                result = value;
                OnPropertyChanged(nameof(Result));
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

        public readonly TypeDetailControl type;
        public WorkControl(TypeDetailControl t)
        {
            InitializeComponent();
            type = t;
            WorkDrop.ItemsSource = MainWindow.M.Works;
        }

        private void AddWork(object sender, RoutedEventArgs e)
        {
            type.AddWork();
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (type.WorkControls.Count == 1) return;

            if (workType != null && workType is IPriceChanged _work && _work.Parts != null && _work.Parts.Count > 0)
            {
                MessageBoxResult response = MessageBox.Show(
                    "Уверены, что хотите удалить работу?\nВ случае удаления, все блоки этой работы\nбудут удалены из нарезанных деталей",
                    "Удаление работы", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                if (response == MessageBoxResult.No) return;

                foreach (PartControl p in _work.Parts)
                    foreach (UserControl item in p.UserControls.Where(w => w.GetType() == workType.GetType()).ToList()) p.RemoveControl(item);
                    //нужно учесть резьбу и зенковку, пока не знаю как...         
            }
            Remove();
        }

        public void Remove()
        {
            UpdatePosition(false);
            type.WorkControls.Remove(this);
            type.det.PriceResult();
            type.TypeDetailGrid.Children.Remove(this);
        }

        public void UpdatePosition(bool direction)
        {
            int numW = type.WorkControls.IndexOf(this);
            if (type.WorkControls.Count > 1) for (int i = numW + 1; i < type.WorkControls.Count; i++)
                    type.WorkControls[i].Margin = new Thickness(0,
                        direction ? type.WorkControls[i].Margin.Top + 25 : type.WorkControls[i].Margin.Top - 25, 0, 0);
            type.UpdatePosition(direction);
        }

        private void SetRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetRatio(tBox.Text);
        }
        private void SetRatio(string _ratio)
        {
            if (float.TryParse(_ratio, out float r)) Ratio = r;     // стандартный парсер избавляет от проблемы с запятой

            if (workType != null && workType is IPriceChanged control) control.OnPriceChanged();
            else if (WorkDrop.SelectedItem is Work work) SetResult(work.Price, false);
        }

        public UserControl? workType;
        private void CreateWork(object sender, SelectionChangedEventArgs e)
        {
            CreateWork();
        }
        public void CreateWork()
        {
            if (WorkDrop.SelectedItem is not Work work) return;
            if (WorkGrid.Children.Contains(workType)) WorkGrid.Children.Remove(workType);

            switch (work.Name)
            {
                case "Лазерная резка":
                    CutControl cut = new(this, new ExcelDialogService());
                    WorkGrid.Children.Add(cut);
                    Grid.SetColumn(cut, 1);
                    workType = cut;
                    break;
                case "Гибка":
                    BendControl bend = new(this);
                    WorkGrid.Children.Add(bend);
                    Grid.SetColumn(bend, 1);
                    workType = bend;
                    break;
                case "Сварка":
                    WeldControl weld = new(this);
                    WorkGrid.Children.Add(weld);
                    Grid.SetColumn(weld, 1);
                    workType = weld;
                    break;
                case "Окраска":
                    PaintControl paint = new(this);
                    WorkGrid.Children.Add(paint);
                    Grid.SetColumn(paint, 1);
                    workType = paint;
                    break;
                case "Резьба":
                    ThreadControl thread = new(this, 'Р');
                    WorkGrid.Children.Add(thread);
                    Grid.SetColumn(thread, 1);
                    workType = thread;
                    break;
                case "Зенковка":
                    ThreadControl countersink = new(this, 'З');
                    WorkGrid.Children.Add(countersink);
                    Grid.SetColumn(countersink, 1);
                    workType = countersink;
                    break;
                case "Вальцовка":
                    RollingControl roll = new(this);
                    WorkGrid.Children.Add(roll);
                    Grid.SetColumn(roll, 1);
                    workType = roll;
                    break;
                case "Доп работа":
                    ExtraControl extra = new(this);
                    WorkGrid.Children.Add(extra);
                    Grid.SetColumn(extra, 1);
                    workType = extra;
                    break;
                case "Труборез":
                    if (type.det.Detail.IsComplect)
                    {
                        WorkDrop.SelectedIndex = -1;
                        return;
                    }
                    PipeControl pipe = new(this);
                    WorkGrid.Children.Add(pipe);
                    Grid.SetColumn(pipe, 1);
                    workType = pipe;
                    break;
                default:
                    if (type.det.Detail.IsComplect)
                    {
                        WorkDrop.SelectedIndex = -1;
                        return;
                    }
                    SetResult(work.Price, false);
                    break;
            }

            //if (workType is not BendControl && workType is not ExtraControl && workType is not ThreadControl && ValidateProp(out int index))
            //{
            //    if (WorkDrop.Items.Count <= index) MessageBox.Show($"Такая работа уже есть!\nУдалите лишнее!");
            //    else WorkDrop.SelectedIndex = index;
            //}

            SetRatio("1");      // запускаем процесс формирования стоимости по умолчанию
        }

        private bool ValidateProp(out int ndx)
        {
            if (type.WorkControls.Count > 0) ndx = type.WorkControls.Max(i => i.WorkDrop.SelectedIndex);
            else ndx = WorkDrop.SelectedIndex;

            foreach (WorkControl w in type.WorkControls) if (w != this && w.WorkDrop.SelectedIndex == WorkDrop.SelectedIndex)
                {
                    ndx++;
                    return true;
                }

            return false;
        }

        public void SetResult(float price, bool addMin = true)
        {
            if (WorkDrop.SelectedItem is not Work work) return;

            Result = (float)Math.Round(addMin ? (price + work.Price) * Ratio : price * Ratio, 2);

            if (addMin)
            {
                ResultText.Foreground = Brushes.Blue;       // если добавлена минималка, окрашиваем результат
                ResultText.ToolTip = "Стоимость услуги\nДобавлена минималка";
            }
            else
            {
                ResultText.Foreground = Brushes.Black;
                ResultText.ToolTip = "Стоимость услуги";
            }

            type.det.PriceResult();
        }
    }

    public interface IPriceChanged
    {
        List<PartControl>? Parts { get; set; }
        void OnPriceChanged();
        void SaveOrLoadProperties(UserControl uc, bool isSaved);
    }
}
