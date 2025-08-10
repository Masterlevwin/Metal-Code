using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        private string[] works = { "Гибка", "Сварка", "Окраска", "Резьба", "Зенковка", "Сверловка",
                                    "Вальцовка", "Цинкование", "Фрезеровка", "Заклепки", "Аквабластинг"};

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

        private void SetProperties(object sender, RoutedEventArgs e)
        {
            AssembliesDrop.ItemsSource = AssemblyWindow.A.Assemblies;
            WorksDrop.ItemsSource = works;
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

        private void ViewPopupDimensions(object sender, MouseWheelEventArgs e) { PopupDimensions.IsOpen = true; }

        private void AddControl(object sender, RoutedEventArgs e) { AddControl(WorksDrop.SelectedIndex); }
        public void AddControl(int index)
        {
            switch (index)
            {
                case 0:
                    AddControl(new BendControl(this));
                    break;
                case 1:
                    if (UserControls.Count > 0 && UserControls.Any(r => r is WeldControl))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одной сварки на деталь");
                        return;
                    }
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
                case 10:
                    if (UserControls.Count > 0 && UserControls.Any(r => r is AquaControl))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одного аквабластинга на деталь");
                        return;
                    }
                    AddControl(new AquaControl(this));
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
            if (Part.PropsDict.ContainsKey(UserControls.IndexOf(uc))) Part.PropsDict.Remove(UserControls.IndexOf(uc));
            UserControls.Remove(uc);
            ControlGrid.Children.Remove(uc);
        }

        private void SetPicture(object sender, RoutedEventArgs e)       //метод вызывается при загрузке элемента Image (Image.Loaded) 
        {
            if (MainWindow.M.IsExpressOffer && Part.Geometries?.Count > 0)
            {
                Picture.Visibility = Visibility.Collapsed;
                GeometryCanvas.Visibility = Visibility.Visible;

                CanvasHelper.SetGeometryDescriptors(GeometryCanvas, Part.Geometries);
            }
            else if (Part.ImageBytes != null)
            {
                GeometryCanvas.Visibility = Visibility.Collapsed;
                Picture.Visibility = Visibility.Visible;

                Picture.Source = MainWindow.CreateBitmap(Part.ImageBytes);
            }
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

        private void OpenPlan(object sender, MouseButtonEventArgs e) { OpenPlan(); }
        private void OpenPlan()
        {
            try
            {
                if (Part.PathToScan != null && File.Exists(Part.PathToScan))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Part.PathToScan,
                        UseShellExecute = true // Использовать оболочку для открытия файла
                    });
                }
                else MainWindow.M.StatusBegin("Чертеж детали не найден");
            }
            catch (Exception ex) { MainWindow.M.StatusBegin(ex.Message); }
        }

        private void AddToAssembly(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cBox && cBox.DataContext is Assembly assembly)
            {
                Particle? particle = assembly.Particles.FirstOrDefault(x => x.Title == Part.Title);

                if (cBox.IsChecked == true && particle is null)
                    assembly.Particles.Add(new()
                    {
                        Title = Part.Title,
                        Count = (int)Math.Floor((double)Part.Count / assembly.Count),
                        ImageBytes = Part.ImageBytes
                    });
                else if (cBox.IsChecked == false && particle != null) assembly.Particles.Remove(particle);
            }
        }
    }

    public interface ICut
    {
        public List<PartControl>? Parts { get; set; }
        public PartsControl? PartsControl { get; set; }
        public TabItem TabItem { get; set; }
        public List<Part>? PartDetails {  get; set; }
        public List<LaserItem>? Items { get; set; }
        public float Mass {  get; set; }
        public float Way { get; set; }
        public int Pinhole { get; set; }
    }
}
