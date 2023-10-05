using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для Detail.xaml
    /// </summary>
    public partial class Detail : UserControl
    {
        public int Count { get; set; }
        public string? NameDetail { get; set; }

        public List<TypeDetailControl> TypeDetailControls = new();

        public Detail()
        {
            InitializeComponent();
            DataContext = this;
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
            
            type.UpdatePosition(true);

            type.AddWork();   // при добавлении дропа типовой детали добавляем дроп работ
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (MainWindow.M.Details.Count == 1) return;
            Remove();
        }
        public void Remove()
        {
            for (int i = 0; i < TypeDetailControls.Count; i++) TypeDetailControls[i]?.Remove();
            UpdatePosition(false);
            MainWindow.M.Details.Remove(this);
            MainWindow.M.ProductGrid.Children.Remove(this);
        }

        public void UpdatePosition(bool direction)
        {
            int num = MainWindow.M.Details.IndexOf(this);
            if (MainWindow.M.Details.Count > 1)
            {
                for (int i = num + 1; i < MainWindow.M.Details.Count; i++)
                {
                    MainWindow.M.Details[i].Margin = new Thickness(0,
                        direction ? MainWindow.M.Details[i].Margin.Top + 25 : MainWindow.M.Details[i].Margin.Top - 25, 0, 0);
                }
            }
            MainWindow.M.AddDetailBtn.Margin = new Thickness(0, MainWindow.M.Details[^1].Margin.Top + 25, 0, 0);
        }
    }
}
