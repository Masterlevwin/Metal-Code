using Metal_Code.Utils;
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

            WorkDrop.ItemsSource = MainWindow.M.Works
                .OrderByPriority(w => w.Name, "Лазерная резка", "Труборез", "Лентопил");
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

        public bool IsProgrammaticChange { get; set; } = false;
        public UserControl? workType;
        public void CreateWork(object sender, SelectionChangedEventArgs e) { CreateWork(); }
        public void CreateWork()
        {
            if (WorkDrop.SelectedItem is not Work work) return;

            // === Режим 1: Загрузка из файла (глобальный флаг) ===
            if (MainWindow.M.IsLoadData)
            {
                InitializeWorkControlSilent(work);
                return;
            }

            // === Режим 2: Программное изменение из SetWide (локальный флаг) ===
            if (IsProgrammaticChange)
            {
                IsProgrammaticChange = false; // Сбрасываем для следующего раза
                InitializeWorkControlSilent(work);
                return;
            }

            // === Режим 3: Ручной ввод пользователя (полная валидация) ===
            if (WorkGrid.Children.Contains(workType))
                WorkGrid.Children.Remove(workType);

            switch (work.Name)
            {
                case "Лазерная резка":
                    if (type.WorkControls.Any(x => x.workType is ICut))
                    {
                        ShowUserWarning("Нельзя добавить \"Лазерную резку\" повторно,\n" +
                            "или, если уже добавлен \"Труборез\"!");
                        break;
                    }
                    CreateAndAddControl(new CutControl(this, new ExcelDialogService()));
                    break;

                case "Гибка":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Гибку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    CreateAndAddControl(new BendControl(this));
                    break;

                case "Труборез":
                    if (type.WorkControls.Any(x => x.workType is ICut))
                    {
                        ShowUserWarning("Нельзя добавить \"Труборез\" повторно,\n" +
                            "или, если уже добавлена \"Лазерная резка\"!");
                        break;
                    }
                    CreateAndAddControl(new PipeControl(this, new ExcelDialogService()));
                    break;

                case "Резьба":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Резьбу\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    CreateAndAddControl(new ThreadControl(this, "Р"));
                    break;

                case "Зенковка":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Зенковку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    CreateAndAddControl(new ThreadControl(this, "З"));
                    break;

                case "Сверловка":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Сверловку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    CreateAndAddControl(new ThreadControl(this, "С"));
                    break;

                case "Вальцовка":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Вальцовку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    if (type.WorkControls.Any(x => x.workType is RollingControl))
                    {
                        ShowUserWarning("Нельзя добавить \"Вальцовку\" повторно!");
                        break;
                    }
                    CreateAndAddControl(new RollingControl(this));
                    break;

                case "Сварка":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Сварку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    if (type.WorkControls.Any(x => x.workType is WeldControl))
                    {
                        ShowUserWarning("Нельзя добавить \"Сварку\" повторно!");
                        break;
                    }
                    CreateAndAddControl(new WeldControl(this));
                    break;

                case "Окраска":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Окраску\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    CreateAndAddControl(new PaintControl(this));
                    break;

                case "Доп работа П":
                case "Доп работа Л":
                    CreateAndAddControl(new ExtraControl(this));
                    break;

                case "Лентопил":
                    if (type.WorkControls.Any(x => x.workType is SawControl))
                    {
                        ShowUserWarning("Нельзя добавить \"Лентопил\" повторно!");
                        break;
                    }
                    CreateAndAddControl(new SawControl(this));
                    break;

                case "Цинкование":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Оцинковку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    if (type.WorkControls.Any(x => x.workType is ZincControl))
                    {
                        ShowUserWarning("Нельзя добавить \"Цинкование\" повторно!");
                        break;
                    }
                    CreateAndAddControl(new ZincControl(this));
                    break;

                case "Фрезеровка":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Фрезеровку\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    if (type.WorkControls.Any(x => x.workType is MillingTotalControl))
                    {
                        ShowUserWarning("Нельзя добавить \"Фрезеровку\" повторно!");
                        break;
                    }
                    CreateAndAddControl(new MillingTotalControl(this));
                    break;

                case "Заклепки":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Заклепки\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    CreateAndAddControl(new ThreadControl(this, "Зк"));
                    break;

                case "Аквабластинг":
                    if (type.det.Detail.IsComplect)
                    {
                        ShowUserWarning("Нельзя добавить \"Аквабластинг\" на \"Комплект деталей\"!\n" +
                            "Добавьте работу на конкретную нарезанную деталь из списка.");
                        break;
                    }
                    if (type.WorkControls.Any(x => x.workType is AquaControl))
                    {
                        ShowUserWarning("Нельзя добавить \"Аквабластинг\" повторно!");
                        break;
                    }
                    CreateAndAddControl(new AquaControl(this));
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

        // === Вспомогательный метод для тихой инициализации (без валидации) ===
        private void InitializeWorkControlSilent(Work work)
        {
            if (WorkGrid.Children.Contains(workType))
                WorkGrid.Children.Remove(workType);

            switch (work.Name)
            {
                case "Лазерная резка":
                    CreateAndAddControl(new CutControl(this, new ExcelDialogService()));
                    break;
                case "Гибка":
                    CreateAndAddControl(new BendControl(this));
                    break;
                case "Труборез":
                    CreateAndAddControl(new PipeControl(this, new ExcelDialogService()));
                    break;
                case "Резьба":
                    CreateAndAddControl(new ThreadControl(this, "Р"));
                    break;
                case "Зенковка":
                    CreateAndAddControl(new ThreadControl(this, "З"));
                    break;
                case "Сверловка":
                    CreateAndAddControl(new ThreadControl(this, "С"));
                    break;
                case "Вальцовка":
                    CreateAndAddControl(new RollingControl(this));
                    break;
                case "Сварка":
                    CreateAndAddControl(new WeldControl(this));
                    break;
                case "Окраска":
                    CreateAndAddControl(new PaintControl(this));
                    break;
                case "Доп работа П":
                case "Доп работа Л":
                    CreateAndAddControl(new ExtraControl(this));
                    break;
                case "Лентопил":
                    CreateAndAddControl(new SawControl(this));
                    break;
                case "Цинкование":
                    CreateAndAddControl(new ZincControl(this));
                    break;
                case "Фрезеровка":
                    CreateAndAddControl(new MillingTotalControl(this));
                    break;
                case "Заклепки":
                    CreateAndAddControl(new ThreadControl(this, "Зк"));
                    break;
                case "Аквабластинг":
                    CreateAndAddControl(new AquaControl(this));
                    break;
            }
        }

        // === Унифицированное создание контрола ===
        private void CreateAndAddControl(UserControl control)
        {
            if (control is FrameworkElement fe)
            {
                WorkGrid.Children.Add(control);
                Grid.SetColumn(fe, 1);
                workType = control;
            }
        }

        // === Вынос MessageBox в отдельный метод ===
        private void ShowUserWarning(string message)
        {
            MessageBox.Show(message);
            WorkDrop.SelectedIndex = -1;
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
    }

    public interface IPriceChanged
    {
        Guid Id { get; }
        ObservableCollection<PartControl>? Parts { get; set; }
        void OnPriceChanged();
        void SaveOrLoadProperties(UserControl uc, bool isSaved);
    }
}
