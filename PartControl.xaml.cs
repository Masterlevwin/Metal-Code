using Metal_Code.Utils;
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

            if (owner is PipeControl or SawControl && float.TryParse(Part.PropsDict[100][0], out float l)) Square = (float)Math.Round(l, 3);
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

        private void AddControl(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string workName)
            {
                // Находим индекс работы в массиве `works`
                int index = Array.IndexOf(works, workName);
                if (index >= 0)
                {
                    AddControl(index);
                }
            }
        }
        
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
            // Устанавливаем отступ сверху (например, 5 пикселей между работами)
            uc.Margin = new Thickness(0, 5, 0, 0);

            UserControls.Add(uc);
            WorksStackPanel.Children.Add(uc);
        }

        public void RemoveControl(UserControl uc)
        {
            if (uc is IPriceChanged work)
            {
                PropertiesChanged -= work.SaveOrLoadProperties;
                Part.WorksDict.Remove(work.Id);
            }

            UserControls.Remove(uc);
            WorksStackPanel.Children.Remove(uc);
        }

        private void SetPicture(object sender, RoutedEventArgs e)       //метод вызывается при загрузке элемента Image (Image.Loaded) 
        {
            if (Part.DisplayGeometry != null)
            {
                Picture.Visibility = Visibility.Collapsed;
                GeometryViewbox.Visibility = Visibility.Visible;
            }
            else if (Part.ImageBytes != null)
            {
                GeometryViewbox.Visibility = Visibility.Collapsed;
                Picture.Visibility = Visibility.Visible;

                Picture.Source = MainWindow.CreateBitmap(Part.ImageBytes);
            }
        }

        private void RemovePart(object sender, RoutedEventArgs e)
        {
            if (owner is not CutControl cut || cut.PartsControl == null || !cut.PartsControl.Parts.Contains(this) || cut.Items is null)
                return;

            if (cut.PartsControl.Parts.Count == 1)
            {
                var warningResponse = MessageBox.Show(
                    "Вы удаляете последнюю деталь в заготовке.\n" +
                    "После этого заготовка останется пустой.\n\n" +
                    "Продолжить удаление?",
                    "Удаление последней детали",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (warningResponse != MessageBoxResult.Yes) return;
            }
            else
            {
                var response = MessageBox.Show(
                    "Уверены, что хотите удалить эту деталь из расчета?\n" +
                    "Она будет полностью удалена из списка, со всех листов раскладки, а её количество обнулится.",
                    "Удаление детали", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (response != MessageBoxResult.Yes) return;
            }

            // 1. Полное обнуление счетчика
            Part.Count = 0;
            Part.NotifyTotalChanged();

            // 2. Отвязываем деталь от всех видов работ
            DetachPartFromWorks();

            // 3. Удаляем все экземпляры этой детали со всех листов раскладки
            RemovePartFromAllNestingSheets(Part);

            // 4. Удаляем из ВСЕХ релевантных коллекций данных
            cut.PartsControl.Parts.Remove(this);
            cut.Parts?.Remove(this);
            cut.PartDetails?.Remove(Part);
            MainWindow.M.Parts?.Remove(Part);

            // 5. Пересчитываем общие итоги расчета
            cut.SumProperties(cut.Items);
            cut.work.type.MassCalculate();

            MainWindow.M.StatusBegin($"Деталь \"{Part.Title}\" полностью удалена из расчета.", MainWindow.StatusMessageType.Success);
        }

        /// <summary>
        /// Отвязывает текущую деталь (PartControl) от всех связанных с ней работ.
        /// </summary>
        private void DetachPartFromWorks()
        {
            if (UserControls == null || work?.type?.WorkControls == null) return;

            foreach (var uc in UserControls)
            {
                foreach (var _work in work.type.WorkControls)
                {
                    if (_work.workType is ThreadControl thread && uc is ThreadControl _thread &&
                        thread.CharName == _thread.CharName && thread.Wide == _thread.Wide)
                    {
                        thread.Parts?.Remove(this);
                    }
                    else if (_work.workType is PaintControl paint && uc is PaintControl _paint &&
                             paint.Ral == _paint.Ral)
                    {
                        paint.Parts?.Remove(this);
                    }
                    else if (_work.workType?.GetType() == uc.GetType() && _work.workType is IPriceChanged w)
                    {
                        w.Parts?.Remove(this);
                        break; // Деталь удалена из этой группы работ, переходим к следующему UserControl
                    }
                }
            }
        }

        /// <summary>
        /// Находит и удаляет указанную деталь со ВСЕХ листов раскладки в текущем расчете.
        /// Сам лист при этом остается в системе (даже если становится пустым).
        /// </summary>
        private void RemovePartFromAllNestingSheets(Part partToRemove)
        {
            if (owner is not CutControl cut || cut.Items == null) return;

            bool anySheetModified = false;

            foreach (var item in cut.Items)
            {
                if (item.NestingSheet != null && item.NestingSheet.Parts != null)
                {
                    // Находим все размещения, которые ссылаются на удаляемую деталь
                    var placementsToRemove = item.NestingSheet.Parts
                        .Where(p => ReferenceEquals(p.Part, partToRemove))
                        .ToList();

                    if (placementsToRemove.Any())
                    {
                        // Удаляем их с листа
                        foreach (var placement in placementsToRemove)
                        {
                            item.NestingSheet.Parts.Remove(placement);
                        }

                        anySheetModified = true;

                        NestingHelper.OptimizeSheetSize(item.NestingSheet);
                        // 🔥 Пересчитываем метрики листа (массу, длину реза), так как детали убрали
                        // Используем наш оптимизированный метод из прошлого шага
                        RecalculateSheetMetricsForItem(item);
                    }
                }
            }

            if (anySheetModified)
            {
                // Если раскладки сейчас отображаются на экране, обновляем их визуализацию
                cut.PartsControl?.RefreshNestingPreview();
            }
        }

        /// <summary>
        /// Вспомогательный метод для пересчета метрик конкретного листа (вынесен для чистоты кода).
        /// </summary>
        private void RecalculateSheetMetricsForItem(LaserItem item)
        {
            if (item.NestingSheet == null) return;

            double sheetWay = item.NestingSheet.Parts.Sum(p => p.Part.Way);
            int sheetPinholes = item.NestingSheet.Parts.Sum(p =>
                int.TryParse(p.Part.PropsDict.GetValueOrDefault(200)?.FirstOrDefault(), out var val) ? val : 0);

            double sheetArea = item.NestingSheet.OptimizedWidth * item.NestingSheet.OptimizedHeight;
            var metal = MainWindow.M.Metals?.FirstOrDefault(m => m.Name == item.metal);
            double density = metal?.Density ?? 0;
            double destiny = double.TryParse(item.destiny, out var d) ? d : 0;

            item.way = (float)sheetWay;
            item.pinholes = sheetPinholes;
            item.mass = (float)(sheetArea * destiny * density / 1_000_000);
            item.sheetSize = $"{item.NestingSheet.OptimizedWidth:0}x{item.NestingSheet.OptimizedHeight:0}";

            // Обновляем общие итоги в CutControl
            if (owner is CutControl cut && cut.PartDetails != null && cut.Items != null)
            {
                cut.WayTotal = cut.PartDetails.Sum(p => p.Way * p.Count);
                cut.MassTotal = cut.PartDetails.Sum(p => p.Mass * p.Count);
                cut.SumProperties(cut.Items);
                cut.work.type.MassCalculate();
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


        private Point _dragStartPoint;
        private bool _isDragging;

        private void PartControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _isDragging = false;
        }

        private void PartControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            // Начинаем перетаскивание только если зажата левая кнопка и DataContext это Part
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging && this.DataContext is Part part)
            {
                Point currentPos = e.GetPosition(this);
                Vector diff = _dragStartPoint - currentPos;

                // Проверяем, что мышь сдвинулась больше, чем на минимальное расстояние для драга (защита от случайных кликов)
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;

                    // Создаем объект данных для передачи
                    var dataObject = new DataObject(typeof(Part), part);

                    // Запускаем стандартную операцию Drag-and-Drop
                    DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);

                    _isDragging = false; // Сбрасываем флаг после завершения операции
                }
            }
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
        public bool HaveCut { get; set; }
    }
}
