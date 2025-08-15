using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private float extraresult;
        public float ExtraResult
        {
            get { return extraresult; }
            set
            {
                if (value != extraresult)
                {
                    extraresult = value;
                    OnPropertyChanged(nameof(ExtraResult));
                }
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
            DataContext = this;
            WorkDrop.ItemsSource = MainWindow.M.Works.OrderBy(x => x.Id);
        }

        private void AddWork(object sender, RoutedEventArgs e)
        {
            type.AddWork();
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (type.WorkControls.Count == 1)
            {
                MessageBox.Show("Нельзя удалить единственную работу в заготовке.\n" +
                    "Вместо этого удалите саму заготовку или сначала добавьте новую работу.");
                return;
            }

            if (workType != null && workType is IPriceChanged _work && _work.Parts != null && _work.Parts.Count > 0)
            {
                MessageBoxResult response = MessageBox.Show(
                    "Уверены, что хотите удалить работу?\nВ случае удаления, все блоки этой работы\nбудут удалены из нарезанных деталей",
                    "Удаление работы", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                if (response == MessageBoxResult.No) return;

                if (workType is PaintControl paint)
                {                                           //удаляем окраску определенного цвета
                    foreach (PartControl p in _work.Parts)
                        foreach (PaintControl item in p.UserControls.OfType<PaintControl>().Where(p => p.Ral == paint.Ral).ToList())
                            p.RemoveControl(item);
                }
                else if (workType is ThreadControl thread)
                {                                           //удаляем определенную обработку отверстий
                    foreach (PartControl p in _work.Parts)
                        foreach (ThreadControl item in p.UserControls.OfType<ThreadControl>().Where(p => p.CharName == thread.CharName && p.Wide == thread.Wide).ToList())
                            p.RemoveControl(item);
                }
                else if (workType is BendControl bend)
                {                                           //удаляем группу однотипных гибов
                    foreach (PartControl p in _work.Parts)
                        foreach (BendControl item in p.UserControls.OfType<BendControl>().Where(p => p.Group == bend.Group).ToList())
                            p.RemoveControl(item);
                }
                else
                {                                           //удаляем работу соответствующего типа
                    foreach (PartControl p in _work.Parts)
                        foreach (UserControl item in p.UserControls.Where(w => w.GetType() == workType.GetType()).ToList())
                            p.RemoveControl(item);
                }
            }

            Remove();
        }

        public void Remove()
        {
            type.WorkControls.Remove(this);
            type.WorksStack.Children.Remove(this);
            type.det.PriceResult();
        }

        private void ResultTextEnabled(object sender, MouseButtonEventArgs e)
        {
            ResultText.IsReadOnly = false;
        }
        private void SetExtraResult(object sender, RoutedEventArgs e)
        {
            if (float.TryParse(ResultText.Text, out float extra)) SetExtraResult(extra);
        }
        public void SetExtraResult(float extra)
        {
            ExtraResult = extra;
            ResultText.IsReadOnly = true;
            Ratio = extra / Result;
        }

        private void SetRatio(object sender, TextChangedEventArgs e)
        {
            if (ExtraResult > 0) return;

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
        public void CreateWork(object sender, SelectionChangedEventArgs e) { CreateWork(); }
        public void CreateWork()
        {
            if (WorkDrop.SelectedItem is not Work work) return;
            if (WorkGrid.Children.Contains(workType)) WorkGrid.Children.Remove(workType);

            switch (work.Name)
            {
                case "Лазерная резка":
                    var _cut = type.WorkControls.FirstOrDefault(x => x.workType is ICut);
                    if (_cut != null)
                    {
                        MessageBox.Show("Нельзя добавить \"Лазерную резку\" повторно,\n" +
                            "или, если уже добавлен \"Труборез\"!");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    CutControl cut = new(this, new ExcelDialogService());
                    WorkGrid.Children.Add(cut);
                    Grid.SetColumn(cut, 1);
                    workType = cut;
                    break;
                case "Гибка":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Гибку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    BendControl bend = new(this);
                    WorkGrid.Children.Add(bend);
                    Grid.SetColumn(bend, 1);
                    workType = bend;
                    break;
                case "Труборез":
                    var _pipe = type.WorkControls.FirstOrDefault(x => x.workType is ICut);
                    if (_pipe != null)
                    {
                        MessageBox.Show("Нельзя добавить \"Труборез\" повторно,\n" +
                            "или, если уже добавлена \"Лазерная резка\"!");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    PipeControl pipe = new(this, new ExcelDialogService());
                    WorkGrid.Children.Add(pipe);
                    Grid.SetColumn(pipe, 1);
                    workType = pipe;
                    break;
                case "Резьба":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Резьбу\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    ThreadControl thread = new(this, "Р");
                    WorkGrid.Children.Add(thread);
                    Grid.SetColumn(thread, 1);
                    workType = thread;
                    break;
                case "Зенковка":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Зенковку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    ThreadControl countersink = new(this, "З");
                    WorkGrid.Children.Add(countersink);
                    Grid.SetColumn(countersink, 1);
                    workType = countersink;
                    break;
                case "Сверловка":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Сверловку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    ThreadControl drilling = new(this, "С");
                    WorkGrid.Children.Add(drilling);
                    Grid.SetColumn(drilling, 1);
                    workType = drilling;
                    break;
                case "Вальцовка":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Вальцовку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    var _roll = type.WorkControls.FirstOrDefault(x => x.workType is RollingControl);
                    if (_roll != null)
                    {
                        MessageBox.Show("Нельзя добавить \"Вальцовку\" повторно!");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    RollingControl roll = new(this);
                    WorkGrid.Children.Add(roll);
                    Grid.SetColumn(roll, 1);
                    workType = roll;
                    break;
                case "Сварка":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Сварку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    var _weld = type.WorkControls.FirstOrDefault(x => x.workType is WeldControl);
                    if (_weld != null)
                    {
                        MessageBox.Show("Нельзя добавить \"Сварку\" повторно!");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    WeldControl weld = new(this);
                    WorkGrid.Children.Add(weld);
                    Grid.SetColumn(weld, 1);
                    workType = weld;
                    break;
                case "Окраска":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Окраску\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
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
                case "Лентопил":
                    var _saw = type.WorkControls.FirstOrDefault(x => x.workType is SawControl);
                    if (_saw != null)
                    {
                        MessageBox.Show("Нельзя добавить \"Лентопил\" повторно!");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    SawControl saw = new(this);
                    WorkGrid.Children.Add(saw);
                    Grid.SetColumn(saw, 1);
                    workType = saw;
                    break;
                case "Цинкование":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Оцинковку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    var _zinc = type.WorkControls.FirstOrDefault(x => x.workType is ZincControl);
                    if (_zinc != null)
                    {
                        MessageBox.Show("Нельзя добавить \"Цинкование\" повторно!");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    ZincControl zinc = new(this);
                    WorkGrid.Children.Add(zinc);
                    Grid.SetColumn(zinc, 1);
                    workType = zinc;
                    break;
                case "Фрезеровка":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Фрезеровку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    var _milling = type.WorkControls.FirstOrDefault(x => x.workType is MillingTotalControl);
                    if (_milling != null)
                    {
                        MessageBox.Show("Нельзя добавить \"Фрезеровку\" повторно!");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    MillingTotalControl milling = new(this);
                    WorkGrid.Children.Add(milling);
                    Grid.SetColumn(milling, 1);
                    workType = milling;
                    break;
                case "Заклепки":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Заклепки\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    ThreadControl rivets = new(this, "Зк");
                    WorkGrid.Children.Add(rivets);
                    Grid.SetColumn(rivets, 1);
                    workType = rivets;
                    break;
                case "Аквабластинг":
                    if (!MainWindow.M.IsLoadData && type.det.Detail.IsComplect)
                    {
                        MessageBox.Show("Нельзя добавить \"Аквабластинг\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    var _aqua = type.WorkControls.FirstOrDefault(x => x.workType is AquaControl);
                    if (_aqua != null)
                    {
                        MessageBox.Show("Нельзя добавить \"Аквабластинг\" повторно!");
                        WorkDrop.SelectedIndex = -1;
                        break;
                    }
                    AquaControl aqua = new(this);
                    WorkGrid.Children.Add(aqua);
                    Grid.SetColumn(aqua, 1);
                    workType = aqua;
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

            Result = ExtraResult > 0 ? ExtraResult : (float)Math.Round(addMin ? (price + work.Price) * Ratio * TechRatio : price * Ratio * TechRatio, 2);

            if (addMin)
            {
                ResultText.Foreground = Brushes.Blue;       // если добавлена минималка, окрашиваем результат
                ResultText.ToolTip = $"Стоимость работы (добавлена минималка), руб\n(время работ - {Math.Ceiling(Result * work.Time / work.Price / Ratio)} мин)";
            }
            else if (ExtraResult > 0)
            {
                ResultText.Foreground = Brushes.Blue;       // если стоимость установлена вручную, окрашиваем результат
                ResultText.ToolTip = $"Стоимость работы установлена вручную, руб\n(время работ - {Math.Ceiling(Result * work.Time / work.Price / Ratio)} мин)";
            }
            else
            {
                ResultText.Foreground = Brushes.Black;
                ResultText.ToolTip = $"Стоимость работы, руб\n(время работ - {Math.Ceiling(Result * work.Time / work.Price / Ratio)} мин)";
            }

            //запрещаем устанавливать коэффициенты на гибку определенной группы
            RatioText.IsReadOnly = TechRatioText.IsReadOnly = workType is BendControl bend && bend.Group != "-";

            type.det.PriceResult();
        }

        private void EnterBorder(object sender, MouseEventArgs e)
        {
            BorderBrush = Brushes.OrangeRed;
        }

        private void LeaveBorder(object sender, MouseEventArgs e)
        {
            BorderBrush = null;
        }
    }

    public interface IPriceChanged
    {
        ObservableCollection<PartControl>? Parts { get; set; }
        void OnPriceChanged();
        void SaveOrLoadProperties(UserControl uc, bool isSaved);
    }
}
