using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WorkControl.xaml
    /// </summary>
    public partial class WorkControl : UserControl
    {
        public List<string> propsList = new();
        public delegate void PropsChanged(WorkControl w, bool b);
        public PropsChanged? PropertiesChanged;

        //Text="{Binding Price, Mode=OneWay, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:WorkControl}}}"
        public static readonly DependencyProperty MyPropertyPrice =
            DependencyProperty.Register("Price", typeof(float), typeof(WorkControl));
        public float Price
        {
            get { return (float)GetValue(MyPropertyPrice); }
            set { SetValue(MyPropertyPrice, value); }
        }

        public float Result { get; set; }

        public readonly TypeDetailControl type;
        public WorkControl(TypeDetailControl t)
        {
            InitializeComponent();
            type = t;
            type.Priced += PriceView;
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
            type.det.PriceResult();
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

        public UserControl? workType;
        private void CreateWork(object sender, SelectionChangedEventArgs e)
        {
            CreateWork();
        }

        public void CreateWork()
        {
            if (WorkDrop.SelectedItem is not Work work) return;
            if (WorkGrid.Children.Contains(workType)) WorkGrid.Children.Remove(workType);

            switch (work.Name)
            {
                case "Покупка":
                    /*PropertyControl prop = new(this);
                    WorkGrid.Children.Add(prop);
                    Grid.SetColumn(prop, 2);
                    workType = prop;
                    if (type.TypeDetailDrop.SelectedItem is TypeDetail typeDetail && typeDetail.Name == "Лист металла")
                    {
                        type.AddWork();                                     // если типовой деталью является лист металла,
                        type.WorkControls[^1].WorkDrop.SelectedIndex = 3;   // то добавляем резку
                    }*/
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
                case "Резка листа":
                    CutControl cut = new(this, new ExcelDialogService());
                    WorkGrid.Children.Add(cut);
                    Grid.SetColumn(cut, 2);
                    workType = cut;
                    break;
            }

            if (ValidateProp(out int index))
            {
                if (WorkDrop.Items.Count <= index) MessageBox.Show($"Такая работа уже есть!\nУдалите лишнее!");
                else WorkDrop.SelectedIndex = index;
            }

            PriceView();
        }

        public void PriceView()         // метод отображения цены типовой детали или работы
        {
            if (WorkDrop.SelectedItem is not Work work) return;

            if (work.Name == "Покупка")
            {
                if (type.TypeDetailDrop.SelectedItem is not TypeDetail typeDetail) return;

                if (typeDetail.Name == "Лист металла" && type.MetalDrop.SelectedItem is Metal metal) Price = metal.MassPrice;
                else Price = typeDetail.Price;
            }
            else Price = work.Price;

            if (workType == null) Result = Price * type.Count;
        }

        private bool ValidateProp(out int ndx)
        {
            if (type.WorkControls.Count > 0) ndx = type.WorkControls.Max(i => i.WorkDrop.SelectedIndex);
            else ndx = WorkDrop.SelectedIndex;

            foreach (WorkControl w in type.WorkControls) if (w != this && w.WorkDrop.SelectedIndex == WorkDrop.SelectedIndex)
                {
                    ndx++;
                    return true;
                }

            return false;
        }
    }
}
