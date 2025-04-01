using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        public AssemblyWindow()
        {
            InitializeComponent();
            A = this;
            DataContext = this;
        }

        private void AddAssembly(object sender, RoutedEventArgs e)
        {
            AddAssembly();
        }
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

            Details.Text = "В данном списке деталей находятся НЕ все,\n" +
                "а только те детали, которые были добавлены для распределения.\n" +
                "Создайте одну или несколько сборок, назовите каждую,\n" +
                "укажите их количество, и распределите эти детали.\n" +
                "Проверьте целостность сборок кнопкой \"Проверить сборки\".";
        }
    }
}
