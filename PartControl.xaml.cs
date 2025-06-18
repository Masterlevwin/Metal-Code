using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PartControl.xaml
    /// </summary>
    public partial class PartControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float square;
        public float Square
        {
            get => square;
            set
            {
                if (value != square)
                {
                    square = value;
                    OnPropertyChanged(nameof(Square));
                }
            }
        }

        private string? dimensions;
        public string? Dimensions
        {
            get => dimensions;
            set
            {
                if (value != dimensions)
                {
                    dimensions = value;
                    OnPropertyChanged(nameof(Dimensions));
                }
            }
        }

        public delegate void PropsChanged(UserControl uc, bool b);
        public PropsChanged? PropertiesChanged;

        public readonly UserControl owner;
        public readonly WorkControl work;
        public Part Part { get; set; }

        public List<UserControl> UserControls = new();

        public PartControl(UserControl _owner, WorkControl _work, Part _part)
        {
            InitializeComponent();
            owner = _owner;
            work = _work;
            Part = _part;
            DataContext = Part;

            SetProperties();        //устанавливаем свойства нарезанной детали
        }

        private void SetProperties()        //метод определения свойств детали
        {
            if (!Part.PropsDict.ContainsKey(100)) return;

            if (owner is PipeControl && float.TryParse(Part.PropsDict[100][0], out float l)) Square = (float)Math.Round(l, 3);
            else if (float.TryParse(Part.PropsDict[100][0], out float h) && float.TryParse(Part.PropsDict[100][1], out float w))
                Square = (float)Math.Round(h * w / 500000, 3);

            if (Part.PropsDict[100].Count > 2) Dimensions = Part.PropsDict[100][2];     //если присутствует строка размеров, показать ее
            //данной проверкой мы избегаем исключения по индексу в случае, если загружаются старые расчеты (элемент PropsDict[100][2] был добавлен позже)

            if (Part.PropsDict[100].Count > 1 && MainWindow.Parser(Part.PropsDict[100][0]) >= 1500 && MainWindow.Parser(Part.PropsDict[100][1]) >= 1500)
            {
                DimensionsText.Foreground = Brushes.Red;
                MainWindow.M.Log += $"\nПроверьте габаритные размеры детали\n" +
                    $"{Part.Title}:\n" +
                    $"необходимо предусмотреть отступы от края листа\n" +
                    $"или согласовать их остутствие с производством!\n";
            }
        }

        private void ViewPopupDimensions(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            PopupDimensions.IsOpen = true;
        }

        private void AddControl(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                switch (btn.Name)
                {
                    case "BendBtn":
                        AddControl(0);
                        break;
                    case "WeldBtn":
                        AddControl(1);
                        break;
                    case "PaintBtn":
                        AddControl(2);
                        break;
                    case "ThreadBtn":
                        AddControl(3);
                        break;
                    case "CountersinkBtn":
                        AddControl(4);
                        break;
                    case "DrillingBtn":
                        AddControl(5);
                        break;
                    case "RollingBtn":
                        AddControl(6);
                        break;
                    case "ZincBtn":
                        AddControl(7);
                        break;
                    case "MillingBtn":
                        AddControl(8);
                        break;
                    case "RivetsBtn":
                        AddControl(9);
                        break;
                }
        }
        public void AddControl(int index)
        {
            switch (index)
            {
                case 0:
                    AddControl(new BendControl(this));
                    break;
                case 1:
                    AddControl(new WeldControl(this));
                    break;
                case 2:
                    AddControl(new PaintControl(this));
                    break;
                case 3:
                    AddControl(new ThreadControl(this, "Р"));
                    break;
                case 4:
                    AddControl(new ThreadControl(this, "З"));
                    break;
                case 5:
                    AddControl(new ThreadControl(this, "С"));
                    break;
                case 6:
                    if (UserControls.Count > 0 && UserControls.Any(r => r is RollingControl))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одной вальцовки на деталь");
                        return;
                    }
                    AddControl(new RollingControl(this));
                    break;
                case 7:
                    if (UserControls.Count > 0 && UserControls.Any(r => r is ZincControl))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одной оцинковки на деталь");
                        return;
                    }
                    AddControl(new ZincControl(this));
                    break;
                case 8:
                    if (UserControls.Count > 0 && UserControls.Any(r => r is MillingTotalControl))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одной фрезеровки на деталь");
                        return;
                    }
                    AddControl(new MillingTotalControl(this));
                    break;
                case 9:
                    AddControl(new ThreadControl(this, "Зк"));
                    break;
            }
        }
        public void AddControl(UserControl uc)
        {
            if (UserControls.Count > 0) uc.Margin = new Thickness(0, UserControls[^1].Margin.Top + 30, 0, 0);

            UserControls.Add(uc);
            ControlGrid.Children.Add(uc);

            Grid.SetRow(uc, 1);
        }

        public void RemoveControl(UserControl uc)
        {
            if (uc is IPriceChanged work) PropertiesChanged -= work.SaveOrLoadProperties;
            UserControls.Remove(uc);
            ControlGrid.Children.Remove(uc);
        }

        private void SetPicture(object sender, RoutedEventArgs e)       //метод вызывается при загрузке элемента Image (Image.Loaded) 
        {
            if (Part.ImageBytes != null) Picture.Source = MainWindow.CreateBitmap(Part.ImageBytes);
        }

        private void ShowAssemblyWindow(object sender, RoutedEventArgs e)
        {
            AssemblyWindow.A.CurrentParts.Clear();
            AssemblyWindow.A.CurrentParts.Add(Part);
            AssemblyWindow.A.Show();
        }

        private void RemovePart(object sender, RoutedEventArgs e)
        {
            if (owner is ICut cut && cut.PartsControl is not null && cut.PartsControl.Parts.Contains(this))
            {
                if (cut.PartsControl.Parts.Count == 1)
                {
                    MessageBox.Show("Нельзя удалить единственную деталь в заготовке.\n" +
                        "Вместо этого удалите саму заготовку.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBoxResult response = MessageBox.Show(
                    "Уверены, что хотите удалить деталь?\n" +
                    "В случае удаления, стоимость расчета не изменится,\n" +
                    "но КП будет сохранено с другой суммой без этой детали.\n" +
                    "Не рекомендуется удалять детали вручную - лучше сделать раскладку заново!",
                    "Удаление детали", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                if (response == MessageBoxResult.No) return;

                if (UserControls.Count > 0)
                    foreach (UserControl uc in UserControls)
                    {
                        foreach (WorkControl _work in work.type.WorkControls)
                            if (_work.workType is ThreadControl thread &&
                                uc is ThreadControl _thread &&
                                thread.CharName == _thread.CharName &&
                                thread.Wide == _thread.Wide)
                            {
                                thread.Parts?.Remove(this);
                            }
                            else if (_work.workType is PaintControl paint &&
                                uc is PaintControl _paint &&
                                paint.Ral == _paint.Ral)
                            {
                                paint.Parts?.Remove(this);
                            }
                            else if (_work.workType?.GetType() == uc.GetType() && _work.workType is IPriceChanged w)
                            {
                                w.Parts?.Remove(this);
                                break;
                            }
                    }

                cut.PartsControl.Parts.Remove(this);
                cut.PartsControl?.partsList.Items.Refresh();
                cut.PartDetails?.Remove(Part);
                MainWindow.M.StatusBegin($"Деталь \"{Part.Title}\" удалена.");
            }
        }

        private void OpenPlan(object sender, MouseButtonEventArgs e)
        {
            OpenPlan();
        }

        private void OpenPlan()
        {
            try
            {
                if (Part.PdfPath != null && File.Exists(Part.PdfPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Part.PdfPath,
                        UseShellExecute = true // Использовать оболочку для открытия файла
                    });
                }
                else MainWindow.M.StatusBegin("Чертеж детали не найден");
            }
            catch (Exception ex) { MainWindow.M.StatusBegin(ex.Message); }
        }
    }

    public interface ICut
    {
        public List<PartControl>? Parts { get; set; }
        public PartsControl? PartsControl { get; set; }
        public List<Part>? PartDetails {  get; set; }
        public List<LaserItem>? Items { get; set; }
        public float Mass {  get; set; }
        public float Way { get; set; }
    }
}
