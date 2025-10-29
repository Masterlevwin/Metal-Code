using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
        public int priceMeter = 500;    //стоимость обработки 1 квадратного метра

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
                foreach (Part part in CurrentParts)
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
            if (blocks.Count > 0) foreach (TextBlock block in blocks) block.Foreground = Brushes.Black;

            if (Assemblies.Count > 0)
            {
                var particles = Assemblies.SelectMany(a => a.Particles, (a, p) => new { p.Title, CountP = p.Count, CountA = a.Count }).GroupBy(x => x.Title);
                foreach (var particle in particles)
                {
                    Part? part = MainWindow.M.Parts.FirstOrDefault(x => x.Title == particle.Key);
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

                if (child is TextBlock block && (tag == block.Text || tag is null)) blocks.Add(block);

                blocks.AddRange(FindTextBlock(child, tag));
            }
            return blocks;
        }

        private void ShowPopup(object sender, MouseEventArgs e)
        {
            Popup.IsOpen = true;

            Details.Text = "В данном списке находятся только нарезанные детали,\n" +
                "и только они могут быть распределены по сборкам.\n" +
                "Создайте одну или несколько сборок, назовите каждую,\n" +
                "укажите их количество, и распределите эти детали.\n" +
                "Проверьте целостность сборок кнопкой \"Проверить сборки\".";
        }

        public void Set_WorksPrice()
        {
            if (Assemblies.Count > 0)
            {
                foreach (Assembly assembly in Assemblies)
                {
                    if (assembly.Particles.Count == 0) continue;
                    var part = MainWindow.M.Parts.FirstOrDefault(p => p.Title == assembly.Particles[0].Title);
                    
                    assembly.Description = string.Empty;
                    assembly.WeldPrice = assembly.PaintPrice = assembly.Square = 0;

                    //стоимость сварки
                    float weld = ParserWeld(assembly.Weld) * assembly.Count;    //парсим длину шва
                    if (weld > 0)
                    {
                        var sideRatio = weld switch                 //коэф за общую длину шва
                        {
                            < 1000 => 1,
                            < 3000 => 3,
                            < 10000 => 10,
                            _ => 100,
                        };

                        var metal = part?.Metal;    //металл по умолчанию - алгоритм?
                        if (metal != null && WeldDict.ContainsKey(metal))
                        {
                            //коэф "1.5" добавляется за зачистку от сварки, коэф "1.7" - за двустороннюю сварку
                            assembly.WeldPrice = WeldDict[metal][sideRatio] * 1.5f * weld * (assembly.Type == "одн" ? 1 : 1.7f);

                            // стоимость данной работы должна быть не ниже минимальной
                            foreach (Work w in MainWindow.M.Works)
                                if (w.Name == "Сварка")
                                {
                                    assembly.WeldPrice =
                                        assembly.WeldPrice > 0 && assembly.WeldPrice < w.Price ?
                                        w.Price : assembly.WeldPrice;
                                    break;
                                }

                            if (!assembly.Description.Contains("Св")) assembly.Description = "Св";
                        }
                    }

                    //стоимость окраски
                    if (string.IsNullOrEmpty(assembly.Ral)) continue;

                    foreach (Particle particle in assembly.Particles)
                    {
                        Part? _part = MainWindow.M.Parts.FirstOrDefault(p => p.Title == particle.Title);
                        if (_part is not null)
                        {
                            if (_part.PropsDict.ContainsKey(100) && _part.PropsDict[100].Count > 2)
                            {
                                var width = MainWindow.Parser(_part.PropsDict[100][0]);
                                var height = MainWindow.Parser(_part.PropsDict[100][1]);
                                if (height == 0) height = 1;

                                Trace.WriteLine($"{_part.Title} - {_part.PropsDict[100][0]} x {_part.PropsDict[100][1]} ({height})");

                                assembly.Square +=
                                    _part.Mass switch       //рассчитываем наценку за тяжелые детали
                                    {
                                        <= 50 => 1,
                                        <= 100 => 1.5f,
                                        <= 150 => 2,
                                        _ => 3,
                                    }
                                    * _part.Destiny switch  //рассчитываем наценку за прогрев толщин
                                    {
                                        >= 10 => 1.5f,
                                        >= 8 => 1.4f,
                                        >= 5 => 1.3f,
                                        _ => 1,
                                    }
                                    * width * height * particle.Count * assembly.Count / (height != 1 ? 500_000 : 1);
                            }
                        }
                    }

                    if (assembly.Square > 0 && assembly.Square < 1) assembly.Square = 1;   //расчетная площадь окраски должна быть не меньше 1 кв м

                    assembly.PaintPrice = (float)Math.Ceiling(priceMeter * assembly.Square);

                    // стоимость данной работы должна быть не ниже минимальной
                    foreach (Work w in MainWindow.M.Works)
                        if (w.Name == "Окраска")
                        {
                            assembly.PaintPrice =
                                assembly.PaintPrice > 0 && assembly.PaintPrice < w.Price ?
                                w.Price : assembly.PaintPrice;
                            break;
                        }

                    if (!assembly.Description.Contains("Св")) assembly.Description = $"О ({assembly.Ral} {assembly.Structure})";
                    else assembly.Description += $" + О ({assembly.Ral} {assembly.Structure})";
                }
            }
        }

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
    }
}
