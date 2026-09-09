using Metal_Code.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для DetailControl.xaml
    /// </summary>
    public partial class DetailControl : UserControl
    {
        public List<TypeDetailControl> TypeDetailControls = new();

        public Detail Detail { get; set; }

        public DetailControl(Detail detail)
        {
            InitializeComponent();
            Detail = detail;
            DataContext = Detail;

            MetalDrop.ItemsSource = MainWindow.M.Metals;

            // При загрузке из БД скрываем блок выбора типа заготовки
            SelectionGrid.Visibility = MainWindow.M.IsLoadData ? Visibility.Collapsed : Visibility.Visible;
        }

        private void AddTypeDetail(object sender, RoutedEventArgs e) => AddTypeDetail();

        private void SelectSheet(object sender, RoutedEventArgs e) => AddTypeDetail("Лист металла");
        private void SelectPipe(object sender, RoutedEventArgs e) => AddTypeDetail("Труба профильная");

        public void AddTypeDetail(string presetType = "")
        {
            TypeDetailControl type = new(this);
            TypeDetailControls.Add(type);
            type.Priced += MassCalculate;

            // Устанавливаем предустановленную заготовку, если указано имя
            if (!string.IsNullOrEmpty(presetType))
            {
                foreach (TypeDetail t in MainWindow.M.TypeDetails)
                {
                    if (t.Name == presetType)
                    {
                        type.TypeDetailDrop.SelectedItem = t;
                        break;
                    }
                }
            }

            BilletsStack.Children.Insert(BilletsStack.Children.Count - 1, type);
            type.AddWork();

            // При программном создании (presetType не пустой) вызываем SetDefaultWork синхронно,
            // чтобы workType был создан до возврата из метода (нужно для загрузки Excel)
            // При ручном выборе используем отложенный вызов через Dispatcher
            if (!MainWindow.M.IsLoadData)
            {
                if (!string.IsNullOrEmpty(presetType))
                {
                    type.SetDefaultWork();
                }
                else
                {
                    Dispatcher.InvokeAsync(() => type.SetDefaultWork(),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }

            // После создания заготовки скрываем блок выбора
            SelectionGrid.Visibility = Visibility.Collapsed;
        }

        public void CheckEmptyBillets()
        {
            SelectionGrid.Visibility = TypeDetailControls.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (MainWindow.M.DetailControls.Count == 1)
            {
                MessageBox.Show("Нельзя удалить единственную деталь в расчете.\n" +
                    "Вместо этого создайте новый проект или сначала добавьте новую деталь.");
                return;
            }
            Remove();
        }
        public void Remove()
        {
            if (MainWindow.M.DetailControls.Count > 1)
                for (int i = MainWindow.M.DetailControls.IndexOf(this) + 1; i < MainWindow.M.DetailControls.Count; i++)
                    MainWindow.M.DetailControls[i].Counter.Text = $"{MainWindow.M.DetailControls.IndexOf(MainWindow.M.DetailControls[i])}";

            MainWindow.M.DetailControls.Remove(this);
            MainWindow.M.DetailsStack.Children.Remove(this);
        }

        public void IsComplectChanged(string _complect = "")
        {
            Detail.IsComplect = true;

            if (_complect != "")
                SetName(_complect);

            DetailName.IsReadOnly = Count.IsReadOnly = true;
        }

        private void SetName(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetName(tBox.Text);
        }
        public void SetName(string name)
        {
            Detail.Title = DetailName.Text = name;
        }

        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int count)) Detail.Count = count;
            MainWindow.M.TotalResult();
        }

        public void MassCalculate()
        {
            Detail.Mass = 0;
            foreach (TypeDetailControl t in TypeDetailControls) Detail.Mass += t.Mass;
            Detail.Mass = (float)Math.Round(Detail.Mass, 2);
        }

        public void PriceResult()
        {
            Detail.Total = 0;
            foreach (TypeDetailControl t in TypeDetailControls)
            {
                Detail.Total += t.Result;
                foreach (WorkControl w in t.WorkControls) Detail.Total += w.Result;
            }

            // добавляем конструкторские работы
            Detail.Total += MainWindow.M.Construct / MainWindow.M.DetailControls.Count;

            Detail.Price = (float)Math.Round(Detail.Total / Detail.Count, 2);

            MainWindow.M.TotalResult();
        }

        private void SetAllMetal(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cBox)
                foreach (TypeDetailControl t in TypeDetailControls) t.CheckMetal.IsChecked = cBox.IsChecked;
            MainWindow.M.UpdateResult();
        }

        private void SetAllMaterial(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cBox)
                foreach (TypeDetailControl t in TypeDetailControls) t.MetalDrop.SelectedIndex = cBox.SelectedIndex;
            MainWindow.M.UpdateResult();
        }
    }
}
