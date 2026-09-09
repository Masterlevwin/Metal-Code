using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для AssemblyWindow.xaml
    /// </summary>
    public partial class AssemblyWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public static AssemblyWindow A = new();
        public ObservableCollection<Part> CurrentParts { get; set; } = new();
        public ObservableCollection<Part> CurrentBaskets { get; set; } = new();
        public ObservableCollection<Assembly> Assemblies { get; set; } = new();

        // === Свойства для нераспределенных деталей ===
        private string _looseWeld = "";
        public string LooseWeld
        {
            get => _looseWeld;
            set
            {
                if (_looseWeld != value)
                {
                    _looseWeld = value;
                    OnPropertyChanged();
                    Set_WorksPrice();
                }
            }
        }

        private string _looseWeldType = "одн";
        public string LooseWeldType
        {
            get => _looseWeldType;
            set
            {
                if (_looseWeldType != value)
                {
                    _looseWeldType = value;
                    OnPropertyChanged();
                    Set_WorksPrice();
                }
            }
        }

        private string _looseRal = "";
        public string LooseRal
        {
            get => _looseRal;
            set
            {
                if (_looseRal != value)
                {
                    _looseRal = value;
                    OnPropertyChanged();
                    Set_WorksPrice();
                }
            }
        }

        private string _looseStructure = "глян";
        public string LooseStructure
        {
            get => _looseStructure;
            set
            {
                if (_looseStructure != value)
                {
                    _looseStructure = value;
                    OnPropertyChanged();
                    Set_WorksPrice();
                }
            }
        }

        private float _looseMass;
        public float LooseMass
        {
            get => _looseMass;
            set
            {
                if (_looseMass != value)
                {
                    _looseMass = value;
                    OnPropertyChanged();
                }
            }
        }

        private float _looseSquare;
        public float LooseSquare
        {
            get => _looseSquare;
            set
            {
                if (_looseSquare != value)
                {
                    _looseSquare = value;
                    OnPropertyChanged();
                }
            }
        }

        private float _looseWeldPrice;
        public float LooseWeldPrice
        {
            get => _looseWeldPrice;
            set
            {
                if (_looseWeldPrice != value)
                {
                    _looseWeldPrice = value;
                    OnPropertyChanged();
                }
            }
        }

        private float _loosePaintPrice;
        public float LoosePaintPrice
        {
            get => _loosePaintPrice;
            set
            {
                if (_loosePaintPrice != value)
                {
                    _loosePaintPrice = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _looseDescription = "";
        public string LooseDescription
        {
            get => _looseDescription;
            set
            {
                if (_looseDescription != value)
                {
                    _looseDescription = value;
                    OnPropertyChanged();
                }
            }
        }

        //сварка
        public string[] Types { get; set; } = { "одн", "дву" };
        public Dictionary<string, Dictionary<float, float>> WeldDict = new()
        {
            ["ст3"] = new Dictionary<float, float>() { [1] = 10, [3] = 8, [10] = 7, [100] = 5 },
            ["09г2с"] = new Dictionary<float, float>() { [1] = 10, [3] = 8, [10] = 7, [100] = 5 },
            ["хк"] = new Dictionary<float, float>() { [1] = 10, [3] = 8, [10] = 7, [100] = 5 },
            ["цинк"] = new Dictionary<float, float>() { [1] = 15, [3] = 12, [10] = 11, [100] = 7 },
            ["aisi430"] = new Dictionary<float, float>() { [1] = 20, [3] = 17, [10] = 15, [100] = 10 },
            ["aisi430шлиф"] = new Dictionary<float, float>() { [1] = 20, [3] = 17, [10] = 15, [100] = 10 },
            ["aisi430зерк"] = new Dictionary<float, float>() { [1] = 20, [3] = 17, [10] = 15, [100] = 10 },
            ["aisi304"] = new Dictionary<float, float>() { [1] = 20, [3] = 17, [10] = 15, [100] = 10 },
            ["aisi304шлиф"] = new Dictionary<float, float>() { [1] = 20, [3] = 17, [10] = 15, [100] = 10 },
            ["aisi304зерк"] = new Dictionary<float, float>() { [1] = 20, [3] = 17, [10] = 15, [100] = 10 },
            ["aisi321"] = new Dictionary<float, float>() { [1] = 20, [3] = 17, [10] = 15, [100] = 10 },
            ["aisi316"] = new Dictionary<float, float>() { [1] = 20, [3] = 17, [10] = 15, [100] = 10 },
            ["aisi201"] = new Dictionary<float, float>() { [1] = 20, [3] = 17, [10] = 15, [100] = 10 },
            ["амг2"] = new Dictionary<float, float>() { [1] = 30, [3] = 27, [10] = 25, [100] = 20 },
            ["амг5"] = new Dictionary<float, float>() { [1] = 30, [3] = 27, [10] = 25, [100] = 20 },
            ["амг6"] = new Dictionary<float, float>() { [1] = 30, [3] = 27, [10] = 25, [100] = 20 },
            ["д16АМ"] = new Dictionary<float, float>() { [1] = 30, [3] = 27, [10] = 25, [100] = 20 },
            ["д16АТ"] = new Dictionary<float, float>() { [1] = 30, [3] = 27, [10] = 25, [100] = 20 },
            ["рифл"] = new Dictionary<float, float>() { [1] = 30, [3] = 27, [10] = 25, [100] = 20 }
        };

        //окраска
        public string[] Structures { get; set; } = { "глян", "мат", "шагр", "муар" };

        public AssemblyWindow()
        {
            InitializeComponent();
            A = this;
            DataContext = this;
        }

        public void RefreshCurrentPartsUI()
        {
            var assignedCounts = Assemblies
                .SelectMany(a => a.Particles, (a, p) => new
                {
                    Title = p.Title ?? string.Empty,
                    Assigned = p.Count * a.Count
                })
                .GroupBy(x => x.Title)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Assigned));

            // 1. Обновляем производственные детали
            var originalItems = MainWindow.M.Parts.ToList();
            CurrentParts.Clear();
            foreach (var original in originalItems.OrderBy(p => p.Title))
            {
                string title = original.Title ?? string.Empty;
                int assigned = assignedCounts.TryGetValue(title, out int count) ? count : 0;
                int remainingCount = Math.Max(0, original.Count - assigned);

                CurrentParts.Add(new Part
                {
                    Title = original.Title,
                    Count = remainingCount,
                    ImageBytes = original.ImageBytes,
                    Metal = original.Metal,
                    Destiny = original.Destiny,
                    Description = original.Description,
                    Accuracy = original.Accuracy,
                    Price = original.Price,
                    Mass = original.Mass,
                    Way = original.Way,
                    PropsDict = original.PropsDict
                });
            }

            // 2. НОВОЕ: Обновляем ПКИ с учетом распределения
            var originalBaskets = MainWindow.M.BasketControls.Select(b => b.Basket).ToList();
            CurrentBaskets.Clear();
            foreach (var original in originalBaskets.OrderBy(p => p.Title))
            {
                string title = original.Title ?? string.Empty;
                int assigned = assignedCounts.TryGetValue(title, out int count) ? count : 0;
                int remainingCount = Math.Max(0, original.Count - assigned);

                CurrentBaskets.Add(new Part // Basket наследуется от Part
                {
                    Title = original.Title,
                    Count = remainingCount,
                    ImageBytes = original.ImageBytes,
                    Metal = original.Metal,
                    Destiny = original.Destiny,
                    Description = original.Description,
                    Accuracy = original.Accuracy,
                    Price = original.Price,
                    Mass = original.Mass,
                    Way = original.Way,
                    PropsDict = original.PropsDict,
                    FixedPrice = original.FixedPrice,
                    IsFixed = original.IsFixed
                });
            }

            CheckAssemblies();
        }

        // ============ Управление сборками ============
        private void AddAssembly(object sender, RoutedEventArgs e) { AddAssembly(); }
        public void AddAssembly()
        {
            Assembly assembly = new();
            assembly.Title += $" {Assemblies.Count + 1}";
            EnsureSortedView(assembly.Particles);
            Assemblies.Add(assembly);
        }

        private void HideWindow(object sender, CancelEventArgs e)
        {
            var focusedElement = Keyboard.FocusedElement as FrameworkElement;
            if (focusedElement != null)
            {
                // Пробуем обновить привязку для TextBox (например, поле Count)
                var textBinding = focusedElement.GetBindingExpression(TextBox.TextProperty);
                if (textBinding != null)
                {
                    textBinding.UpdateSource();
                }
            }

            // Проверяем целостность
            string report = CheckAssemblies();

            // Если есть ошибки (isFakes == true), предупреждаем пользователя
            if (!report.Contains("Сборки сформированы корректно!"))
            {
                var result = MessageBox.Show(
                    "В сборках обнаружены ошибки (количество деталей превышает доступное). " +
                    "Закрыть окно и сохранить текущее (некорректное) состояние?",
                    "Внимание",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true; // Отменяем закрытие, оставляем окно открытым для исправления
                    return;
                }
            }

            CurrentParts.Clear();
            CurrentBaskets.Clear();
            Hide();
            e.Cancel = true;
        }

        private void SetPicture(object sender, RoutedEventArgs e)
        {
            if (sender is Image image && image.DataContext is Part part && part.ImageBytes != null)
                image.Source = MainWindow.CreateBitmap(part.ImageBytes);
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Assembly assembly)
            {
                Assemblies.Remove(assembly);
                RefreshCurrentPartsUI();
            }
            else if (sender is Button _btn && _btn.DataContext is Particle particle)
            {
                foreach (Assembly ac in Assemblies)
                {
                    if (ac.Particles.Contains(particle) && _btn.DataContext == particle)
                    {
                        ac.Particles.Remove(particle);
                        RefreshCurrentPartsUI();
                        break;
                    }
                }
            }
        }

        private void AddCurrentParts(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Assembly assembly)
            {
                var collect = CurrentParts.Where(p => p.Count > 0);
                bool wasEmpty = assembly.Particles.Count == 0;

                foreach (Part part in collect)
                {
                    Particle? _particle = assembly.Particles.FirstOrDefault(p => p.Title == part.Title);
                    if (_particle is null)
                    {
                        _particle = new()
                        {
                            Title = part.Title,
                            Count = part.Count,
                            ImageBytes = part.ImageBytes
                        };
                        assembly.Particles.Add(_particle);
                    }
                }

                if (wasEmpty && assembly.Particles.Count > 0)
                {
                    ExpandAssemblyItem(assembly);
                }

                RefreshCurrentPartsUI();
            }
        }

        private void SetAssemblyCount(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box && int.TryParse(box.Text, out int num) && num > 0 && box.DataContext is Assembly assembly && assembly.Particles.Count > 0)
            {
                bool isCorrect = assembly.Particles.All(p => p.Count % num == 0);

                if (isCorrect)
                {
                    assembly.Count = num;
                    foreach (Particle particle in assembly.Particles)
                        particle.Count /= num;

                    RefreshCurrentPartsUI();
                }
                else
                {
                    MessageBox.Show("Количество хотя бы одной детали не делится нацело на указанное количество сборок!",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ParticleCount_LostFocus(object sender, RoutedEventArgs e)
        {
            RefreshCurrentPartsUI();
        }

        private void ResetDistribution(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Очистить все сборки и вернуть детали в исходное состояние?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Assemblies.Clear();
                LooseWeld = "";
                LooseRal = "";
                RefreshCurrentPartsUI();
            }
        }


        // ============ Проверка сборок ============
        private void CheckAssemblies(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(CheckAssemblies(), "Проверка целостности сборок",
                MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        public string CheckAssemblies()
        {
            string report = "Сборки сформированы со следующими ошибками:\n";
            bool isFakes = false;

            // Очищаем список нераспределенных деталей (только для производственных)
            MainWindow.M.LooseParts.Clear();

            List<TextBlock> blocks = FindTextBlock(ParticleStack);
            if (blocks.Count > 0) foreach (TextBlock block in blocks)
                block.ClearValue(TextBlock.ForegroundProperty);

            if (Assemblies.Count > 0)
            {
                // 1. Получаем списки производственных деталей и ПКИ отдельно
                var manufacturedParts = MainWindow.M.Parts.ToList();
                var purchasedItems = MainWindow.M.BasketControls.Select(b => b.Basket).ToList();

                // 2. Объединяем их для проверки общего количества
                var allSourceItems = manufacturedParts.Union(purchasedItems).ToList();

                var assignedCounts = Assemblies
                    .SelectMany(a => a.Particles, (a, p) => new { Title = p.Title ?? string.Empty, Assigned = p.Count * a.Count })
                    .GroupBy(x => x.Title)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Assigned));

                foreach (var sourceItem in allSourceItems)
                {
                    string title = sourceItem.Title ?? string.Empty;
                    int assigned = assignedCounts.TryGetValue(title, out int count) ? count : 0;
                    int remainder = sourceItem.Count - assigned;

                    if (remainder < 0)
                    {
                        // ОШИБКА: В сборках указано больше, чем есть в наличии 
                        // (Это работает и для деталей, и для ПКИ!)
                        var _particles = Assemblies.SelectMany(a => a.Particles).Where(x => x.Title == title);
                        foreach (Particle _particle in _particles)
                        {
                            List<TextBlock> _blocks = FindTextBlock(ParticleStack, title);
                            if (_blocks.Count > 0) foreach (TextBlock block in _blocks) block.Foreground = Brushes.Red;
                        }
                        report += $"Общее кол-во \"{sourceItem.Title}\" ({sourceItem.Count} шт) не должно быть меньше, чем их определено в сборках ({assigned} шт)\n";
                        isFakes = true;
                    }
                    else if (remainder > 0)
                    {
                        // ОСТАТОК: Добавляем в LooseParts ТОЛЬКО производственные детали.
                        // ПКИ с остатком > 0 просто игнорируются здесь (они остаются в основном списке ПКИ).
                        bool isPurchased = purchasedItems.Any(p => p.Title == title);

                        if (!isPurchased)
                        {
                            Part loosePart = new()
                            {
                                Metal = sourceItem.Metal,
                                Destiny = sourceItem.Destiny,
                                Description = sourceItem.Description,
                                Accuracy = sourceItem.Accuracy,
                                Title = sourceItem.Title,
                                Count = remainder,
                                Price = sourceItem.Price,
                                Mass = sourceItem.Mass,
                                Way = sourceItem.Way,
                                ImageBytes = sourceItem.ImageBytes,
                                PropsDict = new Dictionary<int, List<string>>(sourceItem.PropsDict),
                                FixedPrice = sourceItem.FixedPrice,
                                IsFixed = sourceItem.IsFixed
                            };
                            MainWindow.M.LooseParts.Add(loosePart);
                        }
                    }
                }
            }

            Set_WorksPrice();

            if (!isFakes) report = "Сборки сформированы корректно!";
            return report;
        }

        public static List<TextBlock> FindTextBlock(Visual vis, string? tag = null)
        {
            List<TextBlock> blocks = new();

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(vis); i++)
            {
                Visual child = (Visual)VisualTreeHelper.GetChild(vis, i);

                if (child is TextBlock block && (tag == block.Text || tag is null)
                    && block.Tag?.ToString() == "ParticleTitle") blocks.Add(block);

                blocks.AddRange(FindTextBlock(child, tag));
            }
            return blocks;
        }

        private static void EnsureSortedView(ObservableCollection<Particle> particles)
        {
            var view = CollectionViewSource.GetDefaultView(particles);
            if (view.SortDescriptions.Count == 0)
            {
                view.SortDescriptions.Add(new SortDescription(nameof(Particle.Title), ListSortDirection.Ascending));
            }
        }


        // ============ Расчет стоимости работ ============
        public void Set_WorksPrice()
        {
            // Константы
            const float ChamberCapacitySqM = 10.0f;
            const decimal MinLoadPrice = 5000m;
            const decimal MinPiecePrice = 100m;
            const decimal PricePerSqM = 500m;

            // 0. Сброс значений
            foreach (var assembly in Assemblies)
            {
                assembly.Description = string.Empty;
                assembly.WeldPrice = 0;
                assembly.PaintPrice = 0;
                assembly.Square = 0;
                assembly.Mass = 0;
            }

            LooseMass = 0;
            LooseSquare = 0;
            LooseWeldPrice = 0;
            LoosePaintPrice = 0;
            LooseDescription = string.Empty;

            // ==========================================
            // ЧАСТЬ 1: РАСЧЕТ МАССЫ И ПЛОЩАДИ (для каждой сборки)
            // ==========================================
            foreach (Assembly assembly in Assemblies)
            {
                foreach (Particle particle in assembly.Particles)
                {
                    Part? part = MainWindow.M.Parts.FirstOrDefault(p => p.Title == particle.Title);
                    if (part == null) continue;

                    assembly.Mass += part.Mass * particle.Count;
                    float partSquare = CalculatePartSquare(part, isDoubleSided: true);
                    assembly.Square += partSquare * particle.Count * assembly.Count;
                }
            }

            // ==========================================
            // ЧАСТЬ 2: ПОДГОТОВКА СПИСКА НЕРАСПРЕДЕЛЕННЫХ ДЕТАЛЕЙ
            // (Нужен и для сварки, и для окраски)
            // ==========================================
            var originalItems = MainWindow.M.Parts.Union(MainWindow.M.BasketControls.Select(b => b.Basket)).ToList();
            var assignedCounts = Assemblies
                .SelectMany(a => a.Particles, (a, p) => new { Title = p.Title ?? string.Empty, Assigned = p.Count * a.Count })
                .GroupBy(x => x.Title)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Assigned));

            var loosePartsList = new List<(Part part, int count)>();
            foreach (var original in originalItems)
            {
                string title = original.Title ?? string.Empty;
                int assigned = assignedCounts.TryGetValue(title, out int count) ? count : 0;
                int remainder = Math.Max(0, original.Count - assigned);
                if (remainder > 0)
                {
                    loosePartsList.Add((original, remainder));
                }
            }

            // Расчет массы и площади для нераспределенных
            foreach (var (part, count) in loosePartsList)
            {
                LooseMass += part.Mass * count;
                LooseSquare += CalculatePartSquare(part, isDoubleSided: true) * count;
            }

            // ==========================================
            // ЧАСТЬ 3: ЕДИНЫЙ РАСЧЕТ СВАРКИ (Сборки + Нераспределенные детали)
            // ==========================================

            // Структура для хранения данных о сварке каждого блока
            var weldItems = new List<(float Length, float MaxPricePerUnit, float Multiplier, Assembly? Assembly, bool IsLoose)>();

            // 1. Собираем данные по сборкам
            foreach (var assembly in Assemblies)
            {
                float weldLen = ParserWeld(assembly.Weld) * assembly.Count;
                if (weldLen > 0)
                {
                    var sideRatio = weldLen switch { < 1000 => 1, < 3000 => 3, < 10000 => 10, _ => 100 };
                    float maxPrice = -1;

                    foreach (var particle in assembly.Particles)
                    {
                        var part = MainWindow.M.Parts.FirstOrDefault(p => p.Title == particle.Title);
                        if (part?.Metal != null && WeldDict.TryGetValue(part.Metal, out var priceMap) && priceMap.TryGetValue(sideRatio, out float pricePerUnit))
                        {
                            if (pricePerUnit > maxPrice) maxPrice = pricePerUnit;
                        }
                    }

                    if (maxPrice > 0)
                    {
                        float multiplier = assembly.Type == "одн" ? 1f : 1.7f;
                        weldItems.Add((weldLen, maxPrice, multiplier, assembly, false));
                    }
                }
            }

            // 2. Собираем данные по нераспределенным деталям
            float looseWeldLen = ParserWeld(LooseWeld);
            if (looseWeldLen > 0)
            {
                var sideRatio = looseWeldLen switch { < 1000 => 1, < 3000 => 3, < 10000 => 10, _ => 100 };
                float maxPrice = -1;

                foreach (var (part, _) in loosePartsList)
                {
                    if (part?.Metal != null && WeldDict.TryGetValue(part.Metal, out var priceMap) && priceMap.TryGetValue(sideRatio, out float pricePerUnit))
                    {
                        if (pricePerUnit > maxPrice) maxPrice = pricePerUnit;
                    }
                }

                if (maxPrice > 0)
                {
                    float multiplier = LooseWeldType == "одн" ? 1f : 1.7f;
                    weldItems.Add((looseWeldLen, maxPrice, multiplier, null, true));
                }
            }

            // 3. Рассчитываем общую "сырую" стоимость и применяем минималку ОДИН РАЗ
            decimal totalRawWeldCost = 0;
            foreach (var item in weldItems)
            {
                // Формула: maxPrice * 1.5f * длина * множитель
                totalRawWeldCost += (decimal)(item.MaxPricePerUnit * 1.5f * item.Length * item.Multiplier);
            }

            decimal minWeldPrice = (decimal)(MainWindow.M.Works.FirstOrDefault(w => w.Name == "Сварка")?.Price ?? 0);
            decimal finalWeldCost = totalRawWeldCost;

            if (minWeldPrice > 0 && finalWeldCost > 0 && finalWeldCost < minWeldPrice)
            {
                finalWeldCost = minWeldPrice;
            }

            // 4. Распределяем итоговую стоимость пропорционально вкладу каждого блока в "сырую" стоимость
            foreach (var item in weldItems)
            {
                decimal itemRawCost = (decimal)(item.MaxPricePerUnit * 1.5f * item.Length * item.Multiplier);
                decimal ratio = totalRawWeldCost > 0 ? itemRawCost / totalRawWeldCost : 0;
                decimal assignedCost = finalWeldCost * ratio;

                if (item.IsLoose)
                {
                    LooseWeldPrice = (float)assignedCost;
                    LooseDescription = "Св";
                }
                else if (item.Assembly != null)
                {
                    item.Assembly.WeldPrice = (float)assignedCost;
                    if (!item.Assembly.Description.Contains("Св"))
                    {
                        item.Assembly.Description = string.IsNullOrEmpty(item.Assembly.Description) ? "Св" : $"{item.Assembly.Description} + Св";
                    }
                }
            }

            // ==========================================
            // ЧАСТЬ 4: ЕДИНЫЙ РАСЧЕТ ОКРАСКИ (Сборки + Нераспределенные детали)
            // ==========================================
            var paintItems = new List<(decimal Area, float ChamberArea, int PieceCount, Assembly? Assembly, bool IsLoose)>();

            // 1. Добавляем сборки
            foreach (var assembly in Assemblies.Where(a => !string.IsNullOrEmpty(a.Ral)))
            {
                paintItems.Add((
                    Area: (decimal)assembly.Square,
                    ChamberArea: assembly.Square / 2f,
                    PieceCount: assembly.Particles.Sum(p => p.Count) * assembly.Count,
                    Assembly: assembly,
                    IsLoose: false
                ));
            }

            // 2. Добавляем нераспределенные детали
            if (!string.IsNullOrEmpty(LooseRal) && LooseSquare > 0)
            {
                int loosePieceCount = loosePartsList.Sum(x => x.count);
                paintItems.Add((
                    Area: (decimal)LooseSquare,
                    ChamberArea: LooseSquare / 2f,
                    PieceCount: loosePieceCount,
                    Assembly: null,
                    IsLoose: true
                ));
            }

            // 3. Группируем и рассчитываем
            if (paintItems.Count > 0)
            {
                var paintGroups = paintItems.GroupBy(item => new
                {
                    Ral = item.IsLoose ? LooseRal : item.Assembly?.Ral,
                    Structure = item.IsLoose ? LooseStructure : (string.IsNullOrEmpty(item.Assembly?.Structure) ? "глян" : item.Assembly.Structure)
                });

                foreach (var group in paintGroups)
                {
                    decimal totalArea = group.Sum(item => item.Area);
                    float totalChamberArea = group.Sum(item => item.ChamberArea);
                    int totalCountPieces = group.Sum(item => item.PieceCount);

                    if (totalArea <= 0) continue;

                    int loadCount = (int)Math.Ceiling(totalChamberArea / ChamberCapacitySqM);
                    if (loadCount < 1) loadCount = 1;

                    decimal costByArea = PricePerSqM * totalArea;
                    decimal costByLoad = loadCount * MinLoadPrice;
                    decimal costByPiece = totalCountPieces * MinPiecePrice;

                    decimal finalGroupPrice = Math.Max(costByArea, Math.Max(costByLoad, costByPiece));

                    decimal minPaintWork = (decimal)(MainWindow.M.Works.FirstOrDefault(w => w.Name == "Окраска")?.Price ?? 0);
                    if (minPaintWork > 0 && finalGroupPrice < minPaintWork)
                    {
                        finalGroupPrice = minPaintWork;
                    }

                    string paintDesc = $"О ({group.Key.Ral} {group.Key.Structure})";

                    // 4. Распределяем стоимость пропорционально площади
                    foreach (var item in group)
                    {
                        decimal ratio = totalArea > 0 ? item.Area / totalArea : 0;
                        decimal assignedPrice = finalGroupPrice * ratio;

                        if (item.IsLoose)
                        {
                            LoosePaintPrice += (float)assignedPrice;
                            if (string.IsNullOrEmpty(LooseDescription) || !LooseDescription.Contains("О ("))
                            {
                                LooseDescription = string.IsNullOrEmpty(LooseDescription) ? paintDesc : $"{LooseDescription} + {paintDesc}";
                            }
                        }
                        else if (item.Assembly != null)
                        {
                            item.Assembly.PaintPrice += (float)assignedPrice;
                            if (string.IsNullOrEmpty(item.Assembly.Description) || !item.Assembly.Description.Contains("О ("))
                            {
                                item.Assembly.Description = string.IsNullOrEmpty(item.Assembly.Description) ? paintDesc : $"{item.Assembly.Description} + {paintDesc}";
                            }
                        }
                    }
                }
            }
        }

        // Рассчитывает площадь одной детали в кв.м.
        public static float CalculatePartSquare(Part part, bool isDoubleSided = true)
        {
            if (!part.PropsDict.ContainsKey(100) || part.PropsDict[100].Count < 2)
                return 0;

            var width = MainWindow.Parser(part.PropsDict[100][0]);
            var height = MainWindow.Parser(part.PropsDict[100][1]);
            if (height == 0)
            {
                height = 1;
                width *= 1000000;
            }

            float massFactor = part.Mass switch
            {
                <= 50 => 1,
                <= 100 => 1.5f,
                <= 150 => 2,
                _ => 3,
            };

            float destinyFactor = part.Destiny switch
            {
                >= 10 => 1.5f,
                >= 8 => 1.4f,
                >= 5 => 1.3f,
                _ => 1,
            };

            float baseAreaMm2 = massFactor * destinyFactor * width * height;
            float divisor = isDoubleSided ? 500_000f : 1_000_000f;

            return baseAreaMm2 / divisor;
        }

        // Переводит математическое выражение в число мм сварки
        private static float ParserWeld(string _weld)
        {
            try
            {
                object result = new DataTable().Compute(_weld, null);
                if (float.TryParse($"{result}", out float f)) return f / 10;
            }
            catch
            {
                MainWindow.M.StatusBegin("В поле длины свариваемой поверхности должно быть число или математическое выражение");
            }
            return 0;
        }

        private void ApplyGlobalToAllAssemblies(object sender, RoutedEventArgs e)
        {
            if (Assemblies.Count == 0)
            {
                MessageBox.Show("Нет сборок для применения параметров!", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string? globalWeld = LooseWeld?.Trim();
            string? globalType = LooseWeldType;
            string? globalRal = LooseRal?.Trim();
            string? globalStructure = LooseStructure;

            foreach (var assembly in Assemblies)
            {
                if (!string.IsNullOrEmpty(globalWeld))
                    assembly.Weld = globalWeld;
                if (!string.IsNullOrEmpty(globalType))
                    assembly.Type = globalType;

                if (!string.IsNullOrEmpty(globalRal))
                    assembly.Ral = globalRal;
                if (!string.IsNullOrEmpty(globalStructure))
                    assembly.Structure = globalStructure;
            }

            Set_WorksPrice();

            MessageBox.Show($"Параметры применены ко всем сборкам ({Assemblies.Count} шт)",
                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        //----------Перетаскивание детали в сборки----------//
        private Point _dragStartPoint;
        private Part? _draggedPart;
        private TreeViewItem? _dragOverItem;

        private void ListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ListView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(null);
                var diff = Point.Subtract(currentPoint, _dragStartPoint);

                if (Math.Abs(diff.X) > 10 || Math.Abs(diff.Y) > 10)
                {
                    var listView = sender as ListView;
                    if (listView?.SelectedItem is Part item && item.Count > 0)
                    {
                        _draggedPart = item;
                        var data = new DataObject("PARTICLE", item);
                        DragDrop.DoDragDrop(listView, data, DragDropEffects.Move);
                    }
                }
            }
        }

        private TreeViewItem? GetTreeViewItemFromPoint(Point point)
        {
            var element = ParticleStack.InputHitTest(point) as DependencyObject;
            while (element != null && element is not TreeViewItem)
            {
                element = VisualTreeHelper.GetParent(element);
            }
            return element as TreeViewItem;
        }

        private void TreeView_DragOver(object sender, DragEventArgs e)
        {
            var hitItem = GetTreeViewItemFromPoint(e.GetPosition(ParticleStack));

            if (_dragOverItem != null)
            {
                _dragOverItem.ClearValue(BackgroundProperty);
                _dragOverItem = null;
            }

            if (e.Data.GetDataPresent("PARTICLE") && hitItem?.DataContext is Assembly)
            {
                _dragOverItem = hitItem;
                _dragOverItem.Background = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void TreeView_Drop(object sender, DragEventArgs e)
        {
            if (_dragOverItem != null)
            {
                _dragOverItem.ClearValue(BackgroundProperty);
                _dragOverItem = null;
            }

            if (e.Data.GetDataPresent("PARTICLE") && GetTreeViewItemFromPoint(e.GetPosition(ParticleStack)) is TreeViewItem targetItem)
            {
                if (targetItem.DataContext is Assembly assembly && _draggedPart is Part part && part.Count > 0)
                {
                    bool wasEmpty = assembly.Particles.Count == 0;

                    Particle? _particle = assembly.Particles.FirstOrDefault(p => p.Title == part.Title);
                    if (_particle is null)
                    {
                        _particle = new()
                        {
                            Title = part.Title,
                            Count = part.Count,
                            ImageBytes = part.ImageBytes
                        };
                        assembly.Particles.Add(_particle);
                        HighlightNewItem(assembly, _particle);
                    }

                    if (wasEmpty && assembly.Particles.Count > 0)
                    {
                        ExpandAssemblyItem(assembly);
                    }

                    RefreshCurrentPartsUI();
                    e.Handled = true;
                    return;
                }
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private async void HighlightNewItem(Assembly assembly, Particle particle)
        {
            await Dispatcher.Yield(DispatcherPriority.Background);

            var treeViewItem = FindVisualChildByDataContext<TreeViewItem>(ParticleStack, particle);
            if (treeViewItem != null)
            {
                treeViewItem.BringIntoView();
                var originalBg = treeViewItem.Background;
                treeViewItem.Background = Brushes.LightBlue;

                await System.Threading.Tasks.Task.Delay(3000);

                treeViewItem.Background = originalBg;
            }
        }

        public static T? FindVisualChildByDataContext<T>(DependencyObject parent, object dataContext) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t && t.DataContext == dataContext)
                    return t;

                var result = FindVisualChildByDataContext<T>(child, dataContext);
                if (result != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// Асинхронно раскрывает TreeViewItem для сборки, если он ещё не раскрыт.
        /// </summary>
        private async void ExpandAssemblyItem(Assembly assembly)
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            var tvi = ParticleStack.ItemContainerGenerator.ContainerFromItem(assembly) as TreeViewItem;
            if (tvi != null && !tvi.IsExpanded)
                tvi.IsExpanded = true;
        }
    }
}