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
        public string? NameWork { get; set; }

        public readonly TypeDetailControl type;
        public WorkControl(TypeDetailControl t)
        {
            InitializeComponent();
            DataContext = this;
            type = t;
            WorkDrop.ItemsSource = MainWindow.M.dbWorks.Works.Local.ToObservableCollection();
        }

        private void AddWork(object sender, RoutedEventArgs e)
        {
            type.AddWork();
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (type.WorkControls.Count == 1) return;
            Remove();
        }
        public void Remove()
        {
            UpdatePosition(false);
            type.WorkControls.Remove(this);
            type.TypeDetailGrid.Children.Remove(this);
        }
        public void UpdatePosition(bool direction)
        {
            int numW = type.WorkControls.IndexOf(this);
            if (type.WorkControls.Count > 1) for (int i = numW + 1; i < type.WorkControls.Count; i++)
                    type.WorkControls[i].Margin = new Thickness(0,
                        direction ? type.WorkControls[i].Margin.Top + 25 : type.WorkControls[i].Margin.Top - 25, 0, 0);
            type.UpdatePosition(direction);
        }


        UserControl? workType = null;
        private void CreateWork(object sender, SelectionChangedEventArgs e)
        {
            if (WorkDrop.SelectedItem is not Work work) return;
            if (WorkGrid.Children.Contains(workType)) WorkGrid.Children.Remove(workType);

            switch (work.Name)
            {
                case "Покупка":
                    break;
                case "Сварка":
                    WeldControl weld = new(this);
                    WorkGrid.Children.Add(weld);
                    Grid.SetColumn(weld, 2);
                    workType = weld;
                    break;
                case "Окраска":
                    PaintControl paint = new(this);
                    WorkGrid.Children.Add(paint);
                    Grid.SetColumn(paint, 2);
                    workType = paint;
                    break;
            }
            PriceView();
        }

        private void PriceView()
        {
            if (WorkDrop.SelectedItem is not Work work) return;
            if (work.Name == "Покупка")
            {
                if (type.TypeDetailDrop.SelectedItem is not TypeDetail typeDetail) Price = 0;
                else Price = typeDetail.Price;
            }
            else Price = work.Price;
            WorkPrice.Text = $"{Price}";
        }
    }
}
