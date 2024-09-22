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

            TypeDetailControls.Add(type);
            type.Priced += MassCalculate;       // подписка на изменение типовой детали для расчета общей массы детали
            
            BilletsStack.Children.Add(type);
            
            type.AddWork();   // при добавлении дропа типовой детали добавляем дроп работ
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (MainWindow.M.DetailControls.Count == 1) return;
            Remove();
        }
        public void Remove()
        {
            if (MainWindow.M.DetailControls.Count > 1)
                for (int i = MainWindow.M.DetailControls.IndexOf(this) + 1; i < MainWindow.M.DetailControls.Count; i++)
                    MainWindow.M.DetailControls[i].Counter.Content = MainWindow.M.DetailControls.IndexOf(MainWindow.M.DetailControls[i]);

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

            // добавляем окраску, если она отмечена галочкой или квадратиком
            if (MainWindow.M.CheckPaint.IsChecked != false && !Detail.IsComplect)
                Detail.Total += MainWindow.M.Paint / MainWindow.M.DetailControls.Count(d => !d.Detail.IsComplect);

            // добавляем конструкторские работы
            Detail.Total += MainWindow.M.Construct / MainWindow.M.DetailControls.Count;

            Detail.Price = (float)Math.Round(Detail.Total / Detail.Count, 2);

            MainWindow.M.TotalResult();
        }
    }
}
