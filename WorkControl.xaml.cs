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

        private float ratio = 1;
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

        private float techratio = 1;
        public float TechRatio
        {
            get => techratio;
            set
            {
                if (value != techratio)
                {
                    techratio = value;
                    if (techratio <= 0) techratio = 1;
                    OnPropertyChanged(nameof(TechRatio));
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
            type.WorkControls.Remove(this);
            type.det.PriceResult();
            type.WorksStack.Children.Remove(this);
        }

        private void SetRatio(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox)
                switch (tBox.Name)
                {
                    case "TechRatioText":
                        SetTechRatio(tBox.Text);
                        break;
                    default:
                        SetRatio(tBox.Text);
                        break;
                }
        }
        private void SetRatio(string _ratio)
        {
            if (float.TryParse(_ratio, out float r)) Ratio = r;

            if (workType != null && workType is IPriceChanged control) control.OnPriceChanged();
            else if (WorkDrop.SelectedItem is Work work) SetResult(work.Price, false);
        }
        private void SetTechRatio(string _ratio)
        {
            if (float.TryParse(_ratio, out float r)) TechRatio = r;

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
                case "Труборез":
                    PipeControl pipe = new(this, new ExcelDialogService());
                    WorkGrid.Children.Add(pipe);
                    Grid.SetColumn(pipe, 1);
                    workType = pipe;
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
                case "Сверловка":
                    ThreadControl drilling = new(this, 'С');
                    WorkGrid.Children.Add(drilling);
                    Grid.SetColumn(drilling, 1);
                    workType = drilling;
                    break;
                case "Вальцовка":
                    RollingControl roll = new(this);
                    WorkGrid.Children.Add(roll);
                    Grid.SetColumn(roll, 1);
                    workType = roll;
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
                case "Доп работа П":
                    ExtraControl extraP = new(this);
                    WorkGrid.Children.Add(extraP);
                    Grid.SetColumn(extraP, 1);
                    workType = extraP;
                    break;
                case "Доп работа Л":
                    ExtraControl extraL = new(this);
                    WorkGrid.Children.Add(extraL);
                    Grid.SetColumn(extraL, 1);
                    workType = extraL;
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
        }

        public void SetResult(float price, bool addMin = true)
        {
            if (WorkDrop.SelectedItem is not Work work) return;

            Result = (float)Math.Round(addMin ? (price + work.Price) * Ratio * TechRatio : price * Ratio * TechRatio, 2);

            if (workType is CutControl) TimeSpan.Text = $"{Math.Ceiling(Result * 0.012f) / Ratio}";         //"Лазер (время работ)"
            else if (workType is BendControl) TimeSpan.Text = $"{Math.Ceiling(Result * 0.018f) / Ratio}";   //"Гибка (время работ)"

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
