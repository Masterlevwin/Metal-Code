using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

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
        }

        private void AddDetail(object sender, RoutedEventArgs e)
        {
            MainWindow.M.AddDetail();
        }

        private void AddTypeDetail(object sender, RoutedEventArgs e)
        {
            AddTypeDetail();
        }
        public void AddTypeDetail()
        {
            TypeDetailControl type = new(this);

            TypeDetailControls.Add(type);
            type.Priced += MassCalculate;       // подписка на изменение типовой детали для расчета общей массы детали
            
            BilletsStack.Children.Insert(BilletsStack.Children.Count - 1, type);

            type.AddWork();   // при добавлении дропа типовой детали добавляем дроп работ
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

        private void SetName(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetName(tBox.Text);
        }

        public void SetName(string name)
        {
            Detail.Title = DetailName.Text = name;
        }

        public void IsComplectChanged(string _complect = "")    // метод, в котором эта деталь определяется как Комплект деталей
                                                                // и устанавливаются ограничения на изменение полей типовых деталей
        {
            Detail.IsComplect = true;
            if (_complect != "") SetName(_complect);
            DetailName.IsEnabled = Count.IsEnabled = false;
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

        private void EnterBorder(object sender, MouseEventArgs e)
        {
            DetailBox.BorderBrush = Brushes.Red;
            DetailBox.BorderThickness = new Thickness(2);
        }

        private void LeaveBorder(object sender, MouseEventArgs e)
        {
            DetailBox.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 213, 223, 229));  //#FFD5DFE5           
            DetailBox.BorderThickness = new Thickness(1);
        }

        private void ShowDetailData(object sender, RoutedEventArgs e)
        {
            if (Detail.IsComplect)
            {
                MainWindow.M.StatusBegin("Редактирование комплекта не поддерживается!");
                return;
            }

            if (Detail.Title is null || Detail.Title == "")
            {
                MainWindow.M.StatusBegin("Редактирование недоступно, так как наименование детали не установлено!");
                return;
            }

            if (TypeDetailControls.Count > 1)
            {
                MainWindow.M.StatusBegin("Редактирование недоступно, так как количество заготовок больше одной!");
                return;
            }

            DetailDataWindow detailData = new(Detail, TypeDetailControls[0]);
            detailData.Show();
            MainWindow.M.IsEnabled = false;
        }
    }
}
