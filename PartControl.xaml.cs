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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Metal_Code
{
    public partial class PartControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float square;
        public float Square
        {
            get => square;
            set { if (value != square) { square = value; OnPropertyChanged(nameof(Square)); } }
        }

        private string? dimensions;
        public string? Dimensions
        {
            get => dimensions;
            set { if (value != dimensions) { dimensions = value; OnPropertyChanged(nameof(Dimensions)); } }
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

            InitializeWorkCheckItems();
            UpdateCountColor();
            SetProperties();
        }

        private void SetProperties(object sender, RoutedEventArgs e)
        {
            AssemblyWindow.A.Assemblies.CollectionChanged += OnAssembliesCollectionChanged;
            SubscribeToAllAssemblies();
            AssembliesDrop.ItemsSource = AssemblyWindow.A.Assemblies;
        }

        private void SetProperties()
        {
            // Настройка группировки в списке работ
            var worksView = CollectionViewSource.GetDefaultView(WorkCheckItems);
            worksView.GroupDescriptions?.Clear();
            worksView.GroupDescriptions?.Add(new PropertyGroupDescription("Group"));
            if (WorksListBox != null) WorksListBox.ItemsSource = worksView;

            if (!Part.PropsDict.ContainsKey(100)) return;

            if (owner is PipeControl or SawControl && float.TryParse(Part.PropsDict[100][0], out float l))
                Square = (float)Math.Round(l, 3);
            else if (float.TryParse(Part.PropsDict[100][0], out float h) && float.TryParse(Part.PropsDict[100][1], out float w))
                Square = (float)Math.Round(h * w / 500000, 3);

            if (Part.PropsDict[100].Count > 2)
                Dimensions = Part.PropsDict[100][2];

            if (Part.PropsDict[100].Count > 1 && MainWindow.Parser(Part.PropsDict[100][0]) >= 1500 && MainWindow.Parser(Part.PropsDict[100][1]) >= 1500)
            {
                DimensionsText.Foreground = Brushes.Red;
                MainWindow.M.Log += $"\nПроверьте габаритные размеры детали\n{Part.Title}:\nнеобходимо предусмотреть отступы от края листа\nили согласовать их отсутствие с производством!\n";
            }
            else
            {
                DimensionsText.ClearValue(TextBlock.ForegroundProperty);
            }
        }

        public void AddControl(int index, Guid? guid = null)
        {
            switch (index)
            {
                case 0: AddControl(new BendControl(this, guid)); break;
                case 1:
                    if (UserControls.Any(r => r is WeldControl)) { MainWindow.M.StatusBegin("Нельзя добавить больше одной сварки на деталь"); return; }
                    AddControl(new WeldControl(this, guid)); break;
                case 2: AddControl(new PaintControl(this, guid)); break;
                case 3: AddControl(new ThreadControl(this, "Р", guid)); break;
                case 4: AddControl(new ThreadControl(this, "З", guid)); break;
                case 5: AddControl(new ThreadControl(this, "С", guid)); break;
                case 6:
                    if (UserControls.Any(r => r is RollingControl)) { MainWindow.M.StatusBegin("Нельзя добавить больше одной вальцовки на деталь"); return; }
                    AddControl(new RollingControl(this, guid)); break;
                case 7:
                    if (UserControls.Any(r => r is ZincControl)) { MainWindow.M.StatusBegin("Нельзя добавить больше одной оцинковки на деталь"); return; }
                    AddControl(new ZincControl(this, guid)); break;
                case 8:
                    if (UserControls.Any(r => r is MillingTotalControl)) { MainWindow.M.StatusBegin("Нельзя добавить больше одной фрезеровки на деталь"); return; }
                    AddControl(new MillingTotalControl(this, guid)); break;
                case 9: AddControl(new ThreadControl(this, "Зк", guid)); break;
                case 10:
                    if (UserControls.Any(r => r is AquaControl)) { MainWindow.M.StatusBegin("Нельзя добавить больше одного аквабластинга на деталь"); return; }
                    AddControl(new AquaControl(this, guid)); break;
            }
        }

        public void AddControl(UserControl uc)
        {
            uc.Margin = new Thickness(0, 5, 0, 0);
            UserControls.Add(uc);
            WorksStackPanel.Children.Add(uc);
            WorksCount = UserControls.Count;
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
            WorksCount = UserControls.Count;
        }

        private void SetPicture(object sender, RoutedEventArgs e)
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
            if (owner is not ICut cut || cut.PartsControl == null || !cut.PartsControl.Parts.Contains(this) || cut.Items is null)
                return;

            if (cut.PartsControl.Parts.Count == 1)
            {
                if (MessageBox.Show("Вы удаляете последнюю деталь в заготовке.\nПосле этого заготовка останется пустой.\n\nПродолжить удаление?",
                    "Удаление последней детали", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }
            else
            {
                if (MessageBox.Show("Уверены, что хотите удалить эту деталь из расчета?\nОна будет полностью удалена из списка, со всех листов раскладки, а её количество обнулится.",
                    "Удаление детали", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            }

            Part.Count = 0;
            Part.NotifyTotalChanged();
            DetachPartFromWorks();
            RemovePartFromAllNestingSheets(Part);

            cut.PartsControl.Parts.Remove(this);
            cut.Parts?.Remove(this);
            cut.PartDetails?.Remove(Part);
            MainWindow.M.Parts?.Remove(Part);

            if (cut is CutControl _cut)
            {
                _cut.SumProperties(cut.Items);
                _cut.work.type.MassCalculate();
            }

            MainWindow.M.StatusBegin($"Деталь \"{Part.Title}\" полностью удалена из расчета.", MainWindow.StatusMessageType.Success);
        }

        private void DetachPartFromWorks()
        {
            if (UserControls == null || work?.type?.WorkControls == null) return;
            foreach (var uc in UserControls)
            {
                foreach (var _work in work.type.WorkControls)
                {
                    if (_work.workType is ThreadControl thread && uc is ThreadControl _thread &&
                        thread.CharName == _thread.CharName && thread.Wide == _thread.Wide)
                        thread.Parts?.Remove(this);
                    else if (_work.workType is PaintControl paint && uc is PaintControl _paint && paint.Ral == _paint.Ral)
                        paint.Parts?.Remove(this);
                    else if (_work.workType?.GetType() == uc.GetType() && _work.workType is IPriceChanged w)
                    {
                        w.Parts?.Remove(this);
                        break;
                    }
                }
            }
        }

        private void RemovePartFromAllNestingSheets(Part partToRemove)
        {
            if (owner is not CutControl cut || cut.Items == null) return;
            bool anySheetModified = false;
            foreach (var item in cut.Items)
            {
                if (item.NestingSheet?.Parts != null)
                {
                    var placementsToRemove = item.NestingSheet.Parts.Where(p => ReferenceEquals(p.Part, partToRemove)).ToList();
                    if (placementsToRemove.Any())
                    {
                        foreach (var placement in placementsToRemove) item.NestingSheet.Parts.Remove(placement);
                        anySheetModified = true;
                        NestingHelper.OptimizeSheetSize(item.NestingSheet);
                        RecalculateSheetMetricsForItem(item);
                    }
                }
            }
            if (anySheetModified) cut.PartsControl?.RefreshNestingPreview();
        }

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
                    Process.Start(new ProcessStartInfo { FileName = Part.PathToScan, UseShellExecute = true });
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
                    assembly.Particles.Add(new() { Title = Part.Title, Count = (int)Math.Floor((double)Part.Count / assembly.Count), ImageBytes = Part.ImageBytes });
                else if (cBox.IsChecked == false && particle != null) assembly.Particles.Remove(particle);
            }
        }

        private void SetFixedPrice(object sender, RoutedEventArgs e)
        {
            Part.IsFixed = !Part.IsFixed;
            PricePart.Foreground = Part.IsFixed ? Brushes.Red : Brushes.Black;
            Part.FixedPrice = Part.IsFixed ? Part.Price : 0;
        }

        private int _targetCount = 0;
        public int TargetCount
        {
            get => _targetCount;
            set { if (_targetCount != value) { _targetCount = value; OnPropertyChanged(nameof(TargetCount)); } }
        }

        private void CountInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && int.TryParse(tb.Text, out int typedCount))
                if (TargetCount == 0 && typedCount > 0) TargetCount = typedCount;
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
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging && DataContext is Part part && part.DisplayGeometry is not null)
            {
                Point currentPos = e.GetPosition(this);
                Vector diff = _dragStartPoint - currentPos;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;
                    DragDrop.DoDragDrop(this, new DataObject(typeof(Part), part), DragDropEffects.Copy);
                    _isDragging = false;
                }
            }
        }

        private void CountInput_TextChanged(object sender, TextChangedEventArgs e) => UpdateCountColor();
        private void UpdateCountColor() => CountInput.Foreground = Part.Count == 0 ? Brushes.Red : Brushes.Blue;

        // Логика списка работ (Popup)
        public ObservableCollection<WorkCheckItem> WorkCheckItems { get; } = new();

        private int _worksCount;
        public int WorksCount
        {
            get => _worksCount;
            set { if (_worksCount != value) { _worksCount = value; OnPropertyChanged(); } }
        }

        private void InitializeWorkCheckItems()
        {
            WorkCheckItems.Clear();
            WorkCheckItems.Add(new WorkCheckItem { Index = 0, Name = "Гибка", Group = "Основные работы", IsSingleInstance = false });
            WorkCheckItems.Add(new WorkCheckItem { Index = 6, Name = "Вальцовка", Group = "Основные работы", IsSingleInstance = true });
            WorkCheckItems.Add(new WorkCheckItem { Index = 1, Name = "Сварка", Group = "Основные работы", IsSingleInstance = true });
            WorkCheckItems.Add(new WorkCheckItem { Index = 2, Name = "Окраска", Group = "Основные работы", IsSingleInstance = false });

            WorkCheckItems.Add(new WorkCheckItem { Index = 4, Name = "Зенковка", Group = "Отверстия", ThreadChar = "З", IsSingleInstance = false });
            WorkCheckItems.Add(new WorkCheckItem { Index = 3, Name = "Резьба", Group = "Отверстия", ThreadChar = "Р", IsSingleInstance = false });
            WorkCheckItems.Add(new WorkCheckItem { Index = 5, Name = "Сверловка", Group = "Отверстия", ThreadChar = "С", IsSingleInstance = false });
            WorkCheckItems.Add(new WorkCheckItem { Index = 9, Name = "Заклепки", Group = "Отверстия", ThreadChar = "Зк", IsSingleInstance = false });

            WorkCheckItems.Add(new WorkCheckItem { Index = 10, Name = "Аквабластинг", Group = "Обработка", IsSingleInstance = true });
            WorkCheckItems.Add(new WorkCheckItem { Index = 8, Name = "Фрезеровка", Group = "Обработка", IsSingleInstance = true });
            WorkCheckItems.Add(new WorkCheckItem { Index = 7, Name = "Цинкование", Group = "Обработка", IsSingleInstance = true });
        }

        private bool MatchesWorkItem(UserControl uc, WorkCheckItem item)
        {
            return uc switch
            {
                BendControl => item.Index == 0,
                WeldControl => item.Index == 1,
                PaintControl => item.Index == 2,
                ThreadControl t => item.Index is 3 or 4 or 5 or 9 && t.CharName == item.ThreadChar,
                RollingControl => item.Index == 6,
                ZincControl => item.Index == 7,
                MillingTotalControl => item.Index == 8,
                AquaControl => item.Index == 10,
                _ => false
            };
        }

        private bool _isSyncingWorkStates = false;

        private void SyncWorkCheckStates()
        {
            _isSyncingWorkStates = true;
            try
            {
                foreach (var item in WorkCheckItems)
                    item.Count = UserControls.Count(uc => MatchesWorkItem(uc, item));
            }
            finally { _isSyncingWorkStates = false; }
        }

        private void WorkCheckItem_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingWorkStates) return;
            if (sender is not CheckBox cb || cb.DataContext is not WorkCheckItem item) return;

            if (cb.IsChecked == true) AddControl(item.Index);
            else
            {
                var toRemove = UserControls.FirstOrDefault(uc => MatchesWorkItem(uc, item));
                if (toRemove != null) RemoveControl(toRemove);
            }
            SyncWorkCheckStates();
        }

        private void WorkPlus_Click(object sender, RoutedEventArgs e)
        {
            if (_isSyncingWorkStates) return;
            if (sender is not Button btn || btn.DataContext is not WorkCheckItem item) return;
            AddControl(item.Index);
            SyncWorkCheckStates();
        }

        private void WorkMinus_Click(object sender, RoutedEventArgs e)
        {
            if (_isSyncingWorkStates) return;
            if (sender is not Button btn || btn.DataContext is not WorkCheckItem item) return;
            var toRemove = UserControls.LastOrDefault(uc => MatchesWorkItem(uc, item));
            if (toRemove != null) RemoveControl(toRemove);
            SyncWorkCheckStates();
        }

        private void WorksPopup_Opened(object sender, EventArgs e)
        {
            SyncWorkCheckStates();
            Dispatcher.BeginInvoke(new Action(() => WorksListBox?.Focus()), System.Windows.Threading.DispatcherPriority.Input);
        }

        // События сборок
        private void OnAssembliesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (Assembly assembly in e.NewItems) SubscribeToAssembly(assembly);
            if (e.OldItems != null) foreach (Assembly assembly in e.OldItems) UnsubscribeFromAssembly(assembly);
            UpdateAllCheckBoxStates();
        }

        private void SubscribeToAllAssemblies()
        {
            foreach (Assembly assembly in AssemblyWindow.A.Assemblies) SubscribeToAssembly(assembly);
        }

        private void SubscribeToAssembly(Assembly assembly)
        {
            if (assembly.Particles != null) assembly.Particles.CollectionChanged += OnParticlesCollectionChanged;
        }

        private void UnsubscribeFromAssembly(Assembly assembly)
        {
            if (assembly.Particles != null) assembly.Particles.CollectionChanged -= OnParticlesCollectionChanged;
        }

        private void OnParticlesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateAllCheckBoxStates();

        private void UpdateAllCheckBoxStates()
        {
            for (int i = 0; i < AssembliesDrop.Items.Count; i++)
            {
                var item = AssembliesDrop.Items[i];
                var container = AssembliesDrop.ItemContainerGenerator.ContainerFromIndex(i) as ComboBoxItem;
                if (container?.ContentTemplate != null)
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
                if (child is T result) return result;
                var childResult = FindVisualChild<T>(child);
                if (childResult != null) return childResult;
            }
            return null;
        }
    }

    public interface ICut
    {
        public ObservableCollection<PartControl>? Parts { get; set; }
        public PartsControl? PartsControl { get; set; }
        public TabItem TabItem { get; set; }
        public List<Part>? PartDetails { get; set; }
        public List<LaserItem>? Items { get; set; }
        public float Mass { get; set; }
        public float Way { get; set; }
        public int Pinhole { get; set; }
        public bool HaveCut { get; set; }
    }
}