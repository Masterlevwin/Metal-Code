using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WorkControl.xaml
    /// </summary>
    public partial class WorkControl : UserControl
    {
        public float Price { get; set; }
        public float TotalPrice { get; set; }
        public string? NameWork { get; set; }

        private readonly TypeDetailControl type;
        public WorkControl(TypeDetailControl t)
        {
            InitializeComponent();
            DataContext = this;
            type = t;
            type.WorkControls.Add(this);
            WorkDrop.ItemsSource = MainWindow.M.dbWorks.Works.Local.ToObservableCollection();
            WorkPrice.DataContext = WorkDrop.SelectedItem;
        }

        private void PriceView()
        {
            if (WorkDrop.SelectedItem is not Work work) return;

            if (work.Name == "Покупка")
            {
                if (type.TypeDetailDrop.SelectedItem is not TypeDetail typeDetail) WorkPrice.Text = $"{0}";
                else WorkPrice.Text = $"{typeDetail.Price}";
            }    
            else WorkPrice.Text = $"{work.Price}";
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            Remove();
        }
        public void Remove()
        {
            Update();
            MainWindow.M.ProductGrid.Children.Remove(this);
        }
        public void Update()
        {
            int num = type.WorkControls.IndexOf(this);
            for (int i = num + 1; i < type.WorkControls.Count; i++)
                type.WorkControls[i].Margin = new Thickness(0, type.WorkControls[i].Margin.Top - 25, 0, 0);
            MainWindow.M.AddWorkBtn.Margin = new Thickness(0, MainWindow.M.AddWorkBtn.Margin.Top - 25, 0, 0);
            type.Update();
        }

        private void PriceView(object sender, RoutedEventArgs e)
        {
            PriceView();
        }
    }
}
