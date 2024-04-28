using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для MetalSettings.xaml
    /// </summary>
    public partial class MetalWindow : Window
    {
        MetalContext db = new(MainWindow.M.IsLocal ? MainWindow.M.connections[6] : MainWindow.M.connections[7]);
        public MetalWindow()
        {
            InitializeComponent();
            Loaded += MetalWindow_Loaded;
        }

        // при загрузке окна
        private void MetalWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // загружаем данные из БД
            db.Metals.Load();
            // и устанавливаем данные в качестве контекста
            DataContext = db.Metals.Local.ToObservableCollection();
            MetalGrid.ItemsSource = db.Metals.Local.ToObservableCollection();
            MetalGrid.Columns[0].Header = "N";
            MetalGrid.Columns[1].Header = "Марка";
            MetalGrid.Columns[2].Header = "Плотность";
            MetalGrid.Columns[3].Header = "Цена за кг";

            if (!MainWindow.M.CurrentManager.IsAdmin) foreach (UIElement element in ButtonsStack.Children)
                    if (element is Button) element.IsEnabled = false;
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
            if (MetalGrid.SelectedItem is not Metal met) return;
            db.Metals.Remove(met);
            db.SaveChanges();
        }

        private void FocusMainWindow(object sender, System.EventArgs e)
        {
            MainWindow.M.IsEnabled = true;
        }

        void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (((PropertyDescriptor)e.PropertyDescriptor).IsBrowsable == false) e.Cancel = true;   //скрываем свойства с атрибутом [IsBrowsable]
        }

        private void InsMetal(object sender, SelectionChangedEventArgs e)
        {
            // получаем выделенный объект
            // если ни одного объекта не выделено, выходим
            if (MetalGrid.SelectedItem is not Metal met) return;

            List<InsMetal> insMetals = new();

            if (met.Name != null && MainWindow.M.MetalDict.ContainsKey(met.Name))
            {
                foreach (float key in MainWindow.M.MetalDict[met.Name].Keys)
                {
                    InsMetal _ins = new(
                        key, MainWindow.M.MetalDict[met.Name][key].Item1,
                        MainWindow.M.MetalDict[met.Name][key].Item2,
                        MainWindow.M.MetalDict[met.Name][key].Item3
                        );
                    insMetals.Add(_ins);
                }

                InsMetalGrid.ItemsSource = insMetals;
                InsMetalGrid.Columns[0].Header = "Толщина";
                InsMetalGrid.Columns[1].Header = "Цена за м";
                InsMetalGrid.Columns[2].Header = "Цена за пр";
                InsMetalGrid.Columns[3].Header = "Цена за пог";
            }
        }
    }

    public class InsMetal
    {
        public float Density { get; set; }
        public float WayPrice { get; set; }
        public float PinholePrice { get; set; }
        public float MoldPrice { get; set; }

        public InsMetal(float density, float way, float pinhole, float mold)
        {
            Density = density;
            WayPrice = way;
            PinholePrice = pinhole;
            MoldPrice = mold;
        }
    }
}
