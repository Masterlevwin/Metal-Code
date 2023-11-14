using Microsoft.EntityFrameworkCore;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для MetalSettings.xaml
    /// </summary>
    public partial class MetalWindow : Window
    {
        MetalContext db = new();
        public MetalWindow()
        {
            InitializeComponent();
            Loaded += MetalWindow_Loaded;
        }

        // при загрузке окна
        private void MetalWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // гарантируем, что база данных создана
            db.Database.EnsureCreated();
            // загружаем данные из БД
            db.Metals.Load();
            // и устанавливаем данные в качестве контекста
            DataContext = db.Metals.Local.ToObservableCollection();
            MetalGrid.ItemsSource = db.Metals.Local.ToObservableCollection();
            MetalGrid.Columns[0].Header = "N";
            MetalGrid.Columns[1].Header = "Марка";
            MetalGrid.Columns[2].Header = "Плотность";
            MetalGrid.Columns[3].Header = "Цена за кг";
            MetalGrid.Columns[4].Header = "Цена за метр";
            MetalGrid.Columns[5].Header = "Цена за прокол";
            MetalGrid.Columns[6].Header = "Цена за пог метр";
        }

        // добавление
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            MetalSettings MetalSettings = new(new Metal());
            if (MetalSettings.ShowDialog() == true)
            {
                Metal Metal = MetalSettings.Metal;
                db.Metals.Add(Metal);
                db.SaveChanges();
            }
        }
        // редактирование
        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            // получаем выделенный объект
            Metal? metal = MetalGrid.SelectedItem as Metal;
            // если ни одного объекта не выделено, выходим
            if (metal is null) return;

            MetalSettings MetalSettings = new(new Metal
            {
                Id = metal.Id,
                Name = metal.Name,
                Density = metal.Density,
                MassPrice = metal.MassPrice,
                WayPrice = metal.WayPrice,
                PinholePrice = metal.PinholePrice,
                MoldPrice = metal.MoldPrice
            });

            if (MetalSettings.ShowDialog() == true)
            {
                // получаем измененный объект
                metal = db.Metals.Find(MetalSettings.Metal.Id);
                if (metal != null)
                {
                    metal.Name = MetalSettings.Metal.Name;
                    metal.Density = MetalSettings.Metal.Density;
                    metal.MassPrice = MetalSettings.Metal.MassPrice;
                    metal.WayPrice = MetalSettings.Metal.WayPrice;
                    metal.PinholePrice = MetalSettings.Metal.PinholePrice;
                    metal.MoldPrice = MetalSettings.Metal.MoldPrice;
                    db.SaveChanges();
                    MetalGrid.Items.Refresh();
                }
            }
        }
        // удаление
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            // получаем выделенный объект
            // если ни одного объекта не выделено, выходим
            if (MetalGrid.SelectedItem is not Metal type) return;
            db.Metals.Remove(type);
            db.SaveChanges();
        }

        private void FocusMainWindow(object sender, System.EventArgs e)
        {
            MainWindow.M.IsEnabled = true;
        }
    }
}
