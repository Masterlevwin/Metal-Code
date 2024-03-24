using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для DetailControl.xaml
    /// </summary>
    public partial class DetailControl : UserControl
    {
        public List<TypeDetailControl> TypeDetailControls = new();

        public Detail Detail {  get; set; }

        public DetailControl(Detail detail)
        {
            InitializeComponent();
            Detail = detail;
            DataContext = Detail;
        }

        private void AddDetail(object sender, RoutedEventArgs e)
        {
            MainWindow.M.AddDetail();
        }
        public void AddTypeDetail()
        {
            TypeDetailControl type = new(this);

            if (TypeDetailControls.Count > 0)
                type.Margin = new Thickness(0,
                    TypeDetailControls[^1].Margin.Top + 30 * TypeDetailControls[^1].WorkControls.Count, 0, 0);

            TypeDetailControls.Add(type);
            type.Priced += MassCalculate;       // подписка на изменение типовой детали для расчета общей массы детали
            DetailGrid.Children.Add(type);

            Grid.SetRow(type, 1);
            
            type.AddWork();   // при добавлении дропа типовой детали добавляем дроп работ
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (MainWindow.M.DetailControls.Count == 1) return;
            Remove();
        }
        public void Remove()
        {
            while (TypeDetailControls.Count > 0) TypeDetailControls[^1].Remove();
            MainWindow.M.DetailControls.Remove(this);
            MainWindow.M.ProductGrid.Children.Remove(this);
        }

        public void UpdatePosition(bool direction)
        {
            int num = MainWindow.M.DetailControls.IndexOf(this);
            if (MainWindow.M.DetailControls.Count > 1)
            {
                for (int i = num + 1; i < MainWindow.M.DetailControls.Count; i++)
                {
                    MainWindow.M.DetailControls[i].Margin = new Thickness(0,
                        direction ? MainWindow.M.DetailControls[i].Margin.Top + 30 : MainWindow.M.DetailControls[i].Margin.Top - 30, 0, 0);
                }
            }
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
            DetailName.IsEnabled = false;
        }

        private void SetCount(object sender, TextChangedEventArgs e)    // свойство временно не используется, так как заказчик посчитал его ненужным
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
            Detail.Price = 0;
            foreach (TypeDetailControl t in TypeDetailControls)
            {
                Detail.Price += t.Result;
                foreach (WorkControl w in t.WorkControls) Detail.Price += w.Result;
            }

            // добавляем окраску, если она отмечена галочкой или квадратиком
            if (MainWindow.M.CheckPaint.IsChecked != false && !Detail.IsComplect)
                Detail.Price += MainWindow.M.Paint / MainWindow.M.DetailControls.Count(d => !d.Detail.IsComplect);

            // добавляем конструкторские работы
            Detail.Price += MainWindow.M.Construct / MainWindow.M.DetailControls.Count;

            Detail.Total = Detail.Price * Detail.Count;

            MainWindow.M.TotalResult();
        }
    }
}
