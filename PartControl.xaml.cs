using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
            // Подписываемся на изменения списка сборок
            AssemblyWindow.A.Assemblies.CollectionChanged += OnAssembliesCollectionChanged;

            // Подписываемся на изменения Particles в каждой существующей сборке
            SubscribeToAllAssemblies();

            // Заполняем ComboBox сборками
            AssembliesDrop.ItemsSource = AssemblyWindow.A.Assemblies;

            // Заполняем ComboBox работами
            WorksDrop.ItemsSource = works;
        }

        private void OnAssembliesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Assembly assembly in e.NewItems)
                {
                    SubscribeToAssembly(assembly);
                }
            }

            if (e.OldItems != null)
            {
                foreach (Assembly assembly in e.OldItems)
                {
                    UnsubscribeFromAssembly(assembly);
                }
            }

            // Обновляем отображение CheckBox'ов
            UpdateAllCheckBoxStates();
        }

        private void SubscribeToAllAssemblies()
        {
            foreach (Assembly assembly in AssemblyWindow.A.Assemblies)
            {
                SubscribeToAssembly(assembly);
            }
        }

        private void SubscribeToAssembly(Assembly assembly)
        {
            if (assembly.Particles != null)
            {
                assembly.Particles.CollectionChanged += OnParticlesCollectionChanged;
            }
        }

        private void UnsubscribeFromAssembly(Assembly assembly)
        {
            if (assembly.Particles != null)
            {
                assembly.Particles.CollectionChanged -= OnParticlesCollectionChanged;
            }
        }

        private void OnParticlesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Обновляем состояние всех CheckBox'ов
            UpdateAllCheckBoxStates();
        }

        private void UpdateAllCheckBoxStates()
        {
            // Перебираем все элементы ComboBox
            for (int i = 0; i < AssembliesDrop.Items.Count; i++)
            {
                var item = AssembliesDrop.Items[i];
                var container = AssembliesDrop.ItemContainerGenerator.ContainerFromIndex(i) as ComboBoxItem;

                if (container != null && container.ContentTemplate != null)
                {
                    var checkBox = FindVisualChild<CheckBox>(container);
                    if (checkBox != null && item is Assembly assembly)
                        checkBox.IsChecked = assembly.Particles.Any(p => p.Title == Part.Title);
                }
            }
        }

        private T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                if (child is T result)
                    return result;

                var childResult = FindVisualChild<T>(child);
                if (childResult != null)
                    return childResult;
            }
            return null;
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
        public void AddControl(int index, Guid? guid = null)
        {
            switch (index)
            {
                case 0:
                    AddControl(new BendControl(this, guid));
                    break;
                case 1:
                    if (UserControls.Count > 0 && UserControls.Any(r => r is WeldControl))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одной сварки на деталь");
                        return;
                    }
                    AddControl(new WeldControl(this, guid));
                    break;
                case 2:
                    AddControl(new PaintControl(this, guid));
                    break;
                case 3:
                    AddControl(new ThreadControl(this, "Р", guid));
                    break;
                case 4:
                    AddControl(new ThreadControl(this, "З", guid));
                    break;
                case 5:
                    AddControl(new ThreadControl(this, "С", guid));
                    break;
                case 6:
                    if (UserControls.Count > 0 && UserControls.Any(r => r is RollingControl))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одной вальцовки на деталь");
                        return;
                    }
                    AddControl(new RollingControl(this, guid));
                    break;
                case 7:
                    if (UserControls.Count > 0 && UserControls.Any(r => r is ZincControl))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одной оцинковки на деталь");
                        return;
                    }
                    AddControl(new ZincControl(this, guid));
                    break;
                case 8:
                    if (UserControls.Count > 0 && UserControls.Any(r => r is MillingTotalControl))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одной фрезеровки на деталь");
                        return;
                    }
                    AddControl(new MillingTotalControl(this, guid));
                    break;
                case 9:
                    AddControl(new ThreadControl(this, "Зк", guid));
                    break;
                case 10:
                    if (UserControls.Count > 0 && UserControls.Any(r => r is AquaControl))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одного аквабластинга на деталь");
                        return;
                    }
                    AddControl(new AquaControl(this, guid));
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
            if (uc is IPriceChanged work)
            {
                PropertiesChanged -= work.SaveOrLoadProperties;
                Part.WorksDict.Remove(work.Id);
            }

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

        private void SetFixedPrice(object sender, RoutedEventArgs e)
        {
            Part.IsFixed = !Part.IsFixed;
            PricePart.Foreground = Part.IsFixed ? Brushes.Red : Brushes.Black;

            Part.FixedPrice = Part.IsFixed ? Part.Price : 0;
        }

        private void ViewPopupFixedPrice(object sender, MouseEventArgs e)
        {
            Popup.IsOpen = true;

            Details.Text = $"Чтобы установить фиксированную цену,\n" +
                $"нужно выделить ее целиком (включая \"р\"),\n" +
                $"ввести значение и нажать эту кнопку,\n" +
                $"- фиксированное значение станет красным.\n" +
                $"Чтобы снять фиксацию, нажмите кнопку снова.\n" +
                $"Внимание: нельзя установить цену детали ниже рассчитанной!";
        }
    }

    public interface ICut
    {
        public ObservableCollection<PartControl>? Parts { get; set; }
        public PartsControl? PartsControl { get; set; }
        public TabItem TabItem { get; set; }
        public List<Part>? PartDetails {  get; set; }
        public List<LaserItem>? Items { get; set; }
        public float Mass {  get; set; }
        public float Way { get; set; }
        public int Pinhole { get; set; }
    }
}
