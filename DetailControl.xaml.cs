using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для DetailControl.xaml
    /// </summary>
    public partial class DetailControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string? nameDetail;
        public string? NameDetail
        {
            get => nameDetail;
            set
            {
                nameDetail = value;
                OnPropertyChanged(nameof(NameDetail));
            }
        }

        private int count;
        public int Count
        {
            get => count;
            set
            {
                count = value;
                OnPropertyChanged(nameof(Count));
            }
        }

        private float mass;
        public float Mass
        {
            get => mass;
            set => mass = value;
        }

        private float price;
        public float Price
        {
            get => price;
            set => price = value;
        }

        public List<TypeDetailControl> TypeDetailControls = new();

        public DetailControl()
        {
            InitializeComponent();
            Count = 1;
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
                    TypeDetailControls[^1].Margin.Top + 25 * TypeDetailControls[^1].WorkControls.Count, 0, 0);
                    

            TypeDetailControls.Add(type);
            DetailGrid.Children.Add(type);

            Grid.SetColumn(type, 2);
            
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
                        direction ? MainWindow.M.DetailControls[i].Margin.Top + 25 : MainWindow.M.DetailControls[i].Margin.Top - 25, 0, 0);
                }
            }
        }

        private void SetName(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) NameDetail = tBox.Text;
        }

        private void SetCount(object sender, TextChangedEventArgs e)    // свойство временно не используется, так как заказчик посчитал его ненужным
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int count)) Count = count;
            MainWindow.M.TotalResult();
        }

        public void MassCalculate()
        {
            Mass = 0;
            foreach (TypeDetailControl t in TypeDetailControls) Mass += t.Mass;
            Mass = (float)Math.Round(Mass, 2);
        }

        public void PriceResult()
        {
            Price = 0;
            foreach (TypeDetailControl t in TypeDetailControls)
            {
                Price += t.Result;
                foreach (WorkControl w in t.WorkControls)
                {
                    Price += w.Result;
                }
            }

            if (!MainWindow.M.IsLaser)
            {
                MainWindow.M.Paint = MainWindow.M.PaintResult();
                Price += MainWindow.M.Paint / MainWindow.M.DetailControls.Count;      // размазываем окраску в деталях
            }
            

            MainWindow.M.TotalResult();
        }
    }
}
