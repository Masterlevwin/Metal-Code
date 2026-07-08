using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
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
    public partial class AssemblyWindow : Window
    {
        public static AssemblyWindow A = new();
        public ObservableCollection<Part> CurrentParts { get; set; } = new();
        public ObservableCollection<Assembly> Assemblies { get; set; } = new();
        public ObservableCollection<Part> CurrentBaskets { get; set; } = new();

        //сварка
        public string[] Types { get; set; } = { "одн", "дву" };
        public Dictionary<string, Dictionary<float, float>> WeldDict = new()
        {
            ["ст3"] = new Dictionary<float, float>()
            {
                [1] = 10,
                [3] = 8,
                [10] = 7,
                [100] = 5
            },
            ["09г2с"] = new Dictionary<float, float>()
            {
                [1] = 10,
                [3] = 8,
                [10] = 7,
                [100] = 5
            },
            ["хк"] = new Dictionary<float, float>()
            {
                [1] = 10,
                [3] = 8,
                [10] = 7,
                [100] = 5
            },
            ["цинк"] = new Dictionary<float, float>()
            {
                [1] = 15,
                [3] = 12,
                [10] = 11,
                [100] = 7
            },
            ["aisi430"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi430шлиф"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi430зерк"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi304"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi304шлиф"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi304зерк"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi321"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi316"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["aisi201"] = new Dictionary<float, float>()
            {
                [1] = 20,
                [3] = 17,
                [10] = 15,
                [100] = 10
            },
            ["амг2"] = new Dictionary<float, float>()
            {
                [1] = 30,
                [3] = 27,
                [10] = 25,
                [100] = 20
            },
            ["амг5"] = new Dictionary<float, float>()
            {
                [1] = 30,
                [3] = 27,
                [10] = 25,
                [100] = 20
            },
            ["амг6"] = new Dictionary<float, float>()
            {
                [1] = 30,
                [3] = 27,
                [10] = 25,
                [100] = 20
            },
            ["д16АМ"] = new Dictionary<float, float>()
            {
                [1] = 30,
                [3] = 27,
                [10] = 25,
                [100] = 20
            },
            ["д16АТ"] = new Dictionary<float, float>()
            {
                [1] = 30,
                [3] = 27,
                [10] = 25,
                [100] = 20
            },
            ["рифл"] = new Dictionary<float, float>()
            {
                [1] = 30,
                [3] = 27,
                [10] = 25,
                [100] = 20
            }
        };
        
        //окраска
        public string[] Structures { get; set; } = { "глян", "мат", "шагр", "муар" };

        public AssemblyWindow()
        {
            InitializeComponent();
            A = this;
            DataContext = this;
        }

        private void AddAssembly(object sender, RoutedEventArgs e) { AddAssembly(); }
        public void AddAssembly()
        {
            Assembly assembly = new();
            assembly.Title += $" {Assemblies.Count + 1}";

            //создаем отсортированное по имени представление деталей
            EnsureSortedView(assembly.Particles);

            Assemblies.Add(assembly);
        }

        private void HideWindow(object sender, CancelEventArgs e)
        {
            Hide();
            CurrentParts.Clear();
            e.Cancel = true;
        }

        private void SetPicture(object sender, RoutedEventArgs e)
        {
            if (sender is Image image && image.DataContext is Part part && part.ImageBytes != null) image.Source = MainWindow.CreateBitmap(part.ImageBytes);
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Assembly assembly) Assemblies.Remove(assembly);
            else if (sender is Button _btn && _btn.DataContext is Particle particle)
            {
                foreach (Assembly ac in Assemblies)
                    if (ac.Particles.Contains(particle) && _btn.DataContext == particle)
                    {
                        ac.Particles.Remove(particle);
                        break;
                    }
            }
        }

        private void AddCurrentParts(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Assembly assembly)
            {
                var collect = CurrentParts.Union(CurrentBaskets);

                // Запоминаем, была ли сборка пустой ДО добавления
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

                // Если сборка была пустой и теперь содержит детали — раскрываем её
                if (wasEmpty && assembly.Particles.Count > 0)
                {
                    ExpandAssemblyItem(assembly);
                }
            }
        }

        private void SetAssemblyCount(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box && (int)MainWindow.Parser(box.Text) > 0 && box.DataContext is Assembly assembly && assembly.Particles.Count > 0)
            {
                int num = (int)MainWindow.Parser(box.Text);
                bool isCorrect = true;

                foreach (Particle particle in assembly.Particles)
                    if ((particle.Count % num) != 0)
                    {
                        isCorrect = false;
                        break;
                    }

                if (isCorrect)
                {
                    assembly.Count = num;
                    foreach (Particle particle in assembly.Particles)
                        particle.Count /= num;
                }
                else MessageBox.Show($"Как минимум количество одной из добавленных деталей целочисленно не делится на указанное количество сборок!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckAssemblies(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(CheckAssemblies(), "Проверка целостности сборок",
                MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        public string CheckAssemblies()
        {
            string report = "Сборки сформированы со следующими ошибками:\n";
            bool isFakes = false;
            MainWindow.M.LooseParts.Clear();
            List<TextBlock> blocks = FindTextBlock(ParticleStack);
            if (blocks.Count > 0) foreach (TextBlock block in blocks)
                    block.ClearValue(TextBlock.ForegroundProperty);     //возврат к цвету по умолчанию

            if (Assemblies.Count > 0)
            {
                var parts = MainWindow.M.Parts.Union(MainWindow.M.BasketControls.Select(b => b.Basket));

                var particles = Assemblies.SelectMany(a => a.Particles, (a, p) => new { p.Title, CountP = p.Count, CountA = a.Count }).GroupBy(x => x.Title);
                foreach (var particle in particles)
                {
                    Part? part = parts.FirstOrDefault(x => x.Title == particle.Key);
                    if (part is not null)
                    {
                        int sum = particle.Sum(x => x.CountP * x.CountA);
                        if (part.Count < sum)
                        {
                            var _particles = Assemblies.SelectMany(a => a.Particles).Where(x => x.Title == part.Title);
                            foreach (Particle _particle in _particles)
                            {
                                if (particle.Key != null)
                                {
                                    List<TextBlock> _blocks = FindTextBlock(ParticleStack, particle.Key);
                                    if (_blocks.Count > 0) foreach (TextBlock block in _blocks) block.Foreground = Brushes.Red;
                                }
                            }
                            report += $"Общее кол-во \"{part.Title}\" ({part.Count} шт) не должно быть меньше, чем их определено в сборках ({sum} шт)\n";
                            isFakes = true;
                        }
                        else if (part.Count > sum)
                        {
                            Part loosePart = new()
                            {
                                Metal = part.Metal,
                                Destiny = part.Destiny,
                                Description = part.Description,
                                Accuracy = part.Accuracy,
                                Title = part.Title,
                                Count = part.Count - sum,
                                Price = part.Price,
                                Mass = part.Mass,
                                Way = part.Way,
                                ImageBytes = part.ImageBytes,
                                PropsDict = part.PropsDict,
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

        public void Set_WorksPrice()
        {
            if (Assemblies.Count == 0) return;

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

            // ==========================================
            // ЧАСТЬ 1: РАСЧЕТ МАССЫ И ПЛОЩАДИ (Базовые данные для каждой сборки)
            // ==========================================
            foreach (Assembly assembly in Assemblies)
            {
                foreach (Particle particle in assembly.Particles)
                {
                    Part? part = MainWindow.M.Parts.FirstOrDefault(p => p.Title == particle.Title);
                    if (part == null) continue;

                    // Масса
                    assembly.Mass += part.Mass * particle.Count;

                    // Площадь сборки (сумма площадей всех её деталей)
                    // Используем двустороннюю площадь для расчета стоимости окраски
                    float partSquare = CalculatePartSquare(part, isDoubleSided: true);

                    // Умножаем на кол-во таких деталей и на кол-во самих сборок
                    assembly.Square += partSquare * particle.Count * assembly.Count;
                }
            }

            // ==========================================
            // ЧАСТЬ 2: РАСЧЕТ СВАРКИ (Индивидуально для каждой сборки)
            // ==========================================
            foreach (Assembly assembly in Assemblies)
            {
                float weld = ParserWeld(assembly.Weld) * assembly.Count;
                if (weld > 0)
                {
                    var sideRatio = weld switch
                    {
                        < 1000 => 1,
                        < 3000 => 3,
                        < 10000 => 10,
                        _ => 100,
                    };

                    string? mostExpensiveMetal = null;
                    float maxPrice = -1;

                    foreach (var particle in assembly.Particles)
                    {
                        var part = MainWindow.M.Parts.FirstOrDefault(p => p.Title == particle.Title);
                        if (part?.Metal == null) continue;

                        if (WeldDict.TryGetValue(part.Metal, out var priceMap))
                        {
                            if (priceMap.TryGetValue(sideRatio, out float pricePerUnit))
                            {
                                if (pricePerUnit > maxPrice)
                                {
                                    maxPrice = pricePerUnit;
                                    mostExpensiveMetal = part.Metal;
                                }
                            }
                        }
                    }

                    if (mostExpensiveMetal != null && maxPrice > 0)
                    {
                        assembly.WeldPrice = maxPrice * 1.5f * weld * (assembly.Type == "одн" ? 1 : 1.7f);

                        var minWeldPrice = MainWindow.M.Works.FirstOrDefault(w => w.Name == "Сварка")?.Price ?? 0;
                        if (assembly.WeldPrice > 0 && assembly.WeldPrice < minWeldPrice)
                            assembly.WeldPrice = minWeldPrice;

                        if (!assembly.Description.Contains("Св"))
                            assembly.Description = "Св";
                    }
                }
            }

            // ==========================================
            // ЧАСТЬ 3: РАСЧЕТ ОКРАСКИ (Группировка по СБОРКАМ)
            // ==========================================

            var paintedAssemblies = Assemblies
                .Where(a => !string.IsNullOrEmpty(a.Ral))
                .ToList();

            if (paintedAssemblies.Count > 0)
            {
                var paintGroups = paintedAssemblies.GroupBy(a => new
                {
                    Ral = a.Ral,
                    Structure = string.IsNullOrEmpty(a.Structure) ? "глян" : a.Structure
                });

                foreach (var group in paintGroups)
                {
                    decimal totalArea = 0;
                    float totalChamberArea = 0;
                    int totalCountPieces = 0;

                    var assemblyContributions = new Dictionary<Assembly, decimal>();

                    foreach (var assembly in group)
                    {
                        decimal area = (decimal)assembly.Square;
                        totalArea += area;
                        totalChamberArea += assembly.Square / 2f; // Площадь для камеры (1 сторона)

                        int piecesInAssembly = assembly.Particles.Sum(p => p.Count) * assembly.Count;
                        totalCountPieces += piecesInAssembly;

                        assemblyContributions[assembly] = area;
                    }

                    if (totalArea <= 0) continue;

                    // --- РАСЧЕТ СТОИМОСТИ ГРУППЫ ---

                    int loadCount = (int)Math.Ceiling(totalChamberArea / ChamberCapacitySqM);
                    if (loadCount < 1) loadCount = 1;

                    decimal costByArea = PricePerSqM * totalArea;
                    decimal costByLoad = loadCount * MinLoadPrice;
                    decimal costByPiece = totalCountPieces * MinPiecePrice;

                    // Итоговая цена группы
                    decimal finalGroupPrice = Math.Max(costByArea, Math.Max(costByLoad, costByPiece));

                    // === ИСПРАВЛЕНИЕ: Применяем минималку из справочника к ВСЕЙ группе, а не к частям ===
                    decimal minPaintWork = (decimal)(MainWindow.M.Works.FirstOrDefault(w => w.Name == "Окраска")?.Price ?? 0);
                    if (minPaintWork > 0 && finalGroupPrice < minPaintWork)
                    {
                        // Если стоимость всей группы окраски этого цвета меньше минималки за работу,
                        // то поднимаем стоимость всей группы до минималки.
                        finalGroupPrice = minPaintWork;
                    }

                    System.Diagnostics.Trace.WriteLine(
                        $"\n[Окраска] Группа: {group.Key.Ral} {group.Key.Structure}\n" +
                        $"  Сборок: {group.Count()}, Площадь: {totalArea:0.00} кв.м., Деталей: {totalCountPieces}\n" +
                        $"  Загрузок: {loadCount}, Мин.загр: {costByLoad:0}₽, Мин.шт: {costByPiece:0}₽, По пл: {costByArea:0}₽\n" +
                        $"  → ИТОГ ГРУППЫ: {finalGroupPrice:0}₽");

                    // Распределение цены по сборкам пропорционально площади
                    foreach (var kvp in assemblyContributions)
                    {
                        Assembly assembly = kvp.Key;
                        decimal ratio = totalArea > 0 ? kvp.Value / totalArea : 0;

                        // Присваиваем долю от УЖЕ проверенной на минималки итоговой суммы группы
                        assembly.PaintPrice = (float)(finalGroupPrice * ratio);
                    }

                    // Описание
                    string paintDesc = $"О ({group.Key.Ral} {group.Key.Structure})";
                    foreach (var assembly in group)
                    {
                        if (!string.IsNullOrEmpty(assembly.Description) && !assembly.Description.Contains("О ("))
                            assembly.Description += " + " + paintDesc;
                        else if (string.IsNullOrEmpty(assembly.Description))
                            assembly.Description = paintDesc;
                    }
                }
            }
        }

        // Рассчитывает площадь одной детали в кв.м.
        private float CalculatePartSquare(Part part, bool isDoubleSided = true)
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

            // Коэффициенты массы и толщины (как в вашем оригинальном коде)
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

            // Базовая площадь в мм² с коэффициентами
            float baseAreaMm2 = massFactor * destinyFactor * width * height;

            // Перевод в м²
            // Если двусторонняя окраска: делим на 500 000 (это равносильно *2 / 1 000 000)
            // Если односторонняя (для камеры): делим на 1 000 000
            float divisor = isDoubleSided ? 500_000f : 1_000_000f;

            return baseAreaMm2 / divisor;
        }

        // Переводит математическое выражение в число мм сварки
        private static float ParserWeld(string _weld)
        {
            try
            {
                object result = new DataTable().Compute(_weld, null);
                if (float.TryParse($"{result}", out float f)) return f / 10;    //возвращаем длину свариваемой поверхности в см
            }
            catch
            {
                MainWindow.M.StatusBegin("В поле длины свариваемой поверхности должно быть число или математическое выражение");
            }
            return 0;
        }

        private void ApplyGlobalToAllAssemblies(object sender, RoutedEventArgs e)
        {
            string? globalWeld = GlobalWeldBox.Text.Trim();
            string? globalType = GlobalWeldTypeBox.SelectedItem?.ToString();
            string? globalRal = GlobalRalBox.Text.Trim();
            string? globalStructure = GlobalStructureBox.SelectedItem?.ToString();

            foreach (var assembly in Assemblies)
            {
                // Сварка
                if (!string.IsNullOrEmpty(globalWeld))
                    assembly.Weld = globalWeld;
                if (!string.IsNullOrEmpty(globalType))
                    assembly.Type = globalType;

                // Окраска
                if (!string.IsNullOrEmpty(globalRal))
                    assembly.Ral = globalRal;
                if (!string.IsNullOrEmpty(globalStructure))
                    assembly.Structure = globalStructure;
            }

            Set_WorksPrice();
        }

        /// <summary>
        /// Асинхронно раскрывает TreeViewItem для сборки, если он ещё не раскрыт.
        /// Используется Dispatcher.Yield, чтобы дождаться генерации контейнера после изменения коллекции.
        /// </summary>
        private async void ExpandAssemblyItem(Assembly assembly)
        {
            // Даём UI время обновиться после добавления элемента в коллекцию
            await Dispatcher.Yield(DispatcherPriority.Background);

            // Пытаемся получить TreeViewItem через ItemContainerGenerator
            var tvi = ParticleStack.ItemContainerGenerator.ContainerFromItem(assembly) as TreeViewItem;

            // Если контейнер найден и ещё не раскрыт — раскрываем
            if (tvi != null && !tvi.IsExpanded)
                tvi.IsExpanded = true;
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

                // Минимальное расстояние для начала drag (например, 10 пикселей)
                if (Math.Abs(diff.X) > 10 || Math.Abs(diff.Y) > 10)
                {
                    var listView = sender as ListView;
                    if (listView?.SelectedItem is Part item)
                    {
                        _draggedPart = item; // Сохраняем ссылку на перетаскиваемую деталь
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

            // Сброс предыдущего выделения
            if (_dragOverItem != null)
            {
                _dragOverItem.ClearValue(BackgroundProperty);
                _dragOverItem = null;
            }

            if (e.Data.GetDataPresent("PARTICLE") && hitItem?.DataContext is Assembly)
            {
                _dragOverItem = hitItem;
                // Полупрозрачный синий фон (как в системных выделениях)
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
            // Сброс подсветки независимо от результата drop
            if (_dragOverItem != null)
            {
                _dragOverItem.ClearValue(BackgroundProperty);
                _dragOverItem = null;
            }

            if (e.Data.GetDataPresent("PARTICLE") && GetTreeViewItemFromPoint(e.GetPosition(ParticleStack)) is TreeViewItem targetItem)
            {
                if (targetItem.DataContext is Assembly assembly && _draggedPart is Part part)
                {
                    // Запоминаем, была ли сборка пустой ДО добавления
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

                        // Подсвечиваем добавленную деталь!
                        HighlightNewItem(assembly, _particle);
                    }

                    // Раскрываем сборку, если она была пустой и теперь содержит детали
                    if (wasEmpty && assembly.Particles.Count > 0)
                    {
                        ExpandAssemblyItem(assembly);
                    }

                    e.Handled = true;
                    return;
                }
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private async void HighlightNewItem(Assembly assembly, Particle particle)
        {
            // Дайте UI время обновиться после добавления и сортировки
            await Dispatcher.Yield(DispatcherPriority.Background);

            // Находим TreeViewItem, соответствующий particle
            var treeViewItem = FindVisualChildByDataContext<TreeViewItem>(ParticleStack, particle);
            if (treeViewItem != null)
            {
                // Прокручиваем к элементу
                treeViewItem.BringIntoView();

                // Временная подсветка (например, жёлтый фон на 1.5 сек)
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
    }
}
