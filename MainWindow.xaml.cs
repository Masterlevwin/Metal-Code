using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace Metal_Code
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public static MainWindow M = new();

        public readonly TypeDetailContext dbTypeDetails = new();
        public readonly WorkContext dbWorks = new();

        public readonly ApplicationViewModel DetailsModel = new(new DefaultDialogService(), new JsonFileService());

        public MainWindow()
        {
            InitializeComponent();
            M = this;
            DataContext = DetailsModel;
            Loaded += UpdateDrops;
            AddDetail();
        }

        private void UpdateDrops(object sender, RoutedEventArgs e)  // при загрузке окна
        {
            dbWorks.Database.EnsureCreated();
            dbWorks.Works.Load();

            dbTypeDetails.Database.EnsureCreated();
            dbTypeDetails.TypeDetails.Load();
        }

        private void OpenSettings(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            if (sender == Settings.Items[0])
            {
                TypeDetailWindow typeDetailWindow = new();
                typeDetailWindow.Show();
            }
            else if (sender == Settings.Items[1])
            {
                WorkWindow workWindow = new();
                workWindow.Show();
            }
        }

        public string? Product { get; set; }
        public int Count { get; set; }
        public float Price { get; set; }

        public List<DetailControl> DetailControls = new();
        public void AddDetail()
        {
            DetailControl detail = new();

            if (DetailControls.Count > 0)
                detail.Margin = new Thickness(0,
                    DetailControls[^1].Margin.Top + 25 * DetailControls[^1].TypeDetailControls.Sum(t => t.WorkControls.Count), 0, 0);
            
            DetailControls.Add(detail);
            ProductGrid.Children.Add(detail);

            detail.AddTypeDetail();   // при добавлении новой детали добавляем дроп комплектации
        }

        private void ClearDetails()
        {
            if (DetailControls.Count > 0) foreach (DetailControl d in DetailControls) d.Remove();
            DetailControls.Clear();
        }

        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int c)) Count = c;
            if (Count > 0) TotalResult();
        }

        public void TotalResult()
        {
            Price = 0;
            foreach (DetailControl d in DetailControls) Price += d.Price;
            Total.Text = $"{Price * Count}";
            SaveDetails();
        }

        public ObservableCollection<Detail> SaveDetails()
        {
            ObservableCollection<Detail> details = new();
            for (int i = 0; i < DetailControls.Count; i++)
            {
                Detail detail = new(i + 1, DetailControls[i].NameDetail, DetailControls[i].Price / DetailControls[i].Count, DetailControls[i].Count, DetailControls[i].Price);
                for (int j = 0; j < DetailControls[i].TypeDetailControls.Count; j++)
                {
                    SaveTypeDetail typeDetail = new(DetailControls[i].TypeDetailControls[j].TypeDetailDrop.SelectedIndex, DetailControls[i].TypeDetailControls[j].Count);
                    for (int k = 0; k < DetailControls[i].TypeDetailControls[j].WorkControls.Count; k++)
                    {
                        SaveWork saveWork = new(DetailControls[i].TypeDetailControls[j].WorkControls[k].WorkDrop.SelectedIndex);
                        typeDetail.Works.Add(saveWork);
                    }
                    detail.TypeDetails.Add(typeDetail);
                }
                details.Add(detail);
            }

            DetailsGrid.ItemsSource = details;
            return details;
        }

        public void LoadDetails()
        {
            //ClearDetails();
            for (int i = 0; i < DetailsModel.Details.Count; i++)
            {
                AddDetail();
                DetailControls[i].NameDetail = DetailsModel.Details[i].Наименование;
                DetailControls[i].Count = DetailsModel.Details[i].Кол;

                for (int j = 0; j < DetailsModel.Details[i].TypeDetails.Count; j++)
                {
                    DetailControls[i].TypeDetailControls[j].TypeDetailDrop.SelectedIndex = DetailsModel.Details[i].TypeDetails[j].Index;
                    DetailControls[i].TypeDetailControls[j].Count = DetailsModel.Details[i].TypeDetails[j].Count;

                    for (int k = 0; k < DetailsModel.Details[i].TypeDetails[j].Works.Count; k++)
                    {
                        DetailControls[i].TypeDetailControls[j].WorkControls[k].WorkDrop.SelectedIndex = DetailsModel.Details[i].TypeDetails[j].Works[k].Index;
                        if (DetailControls[i].TypeDetailControls[j].WorkControls.Count < DetailsModel.Details[i].TypeDetails[j].Works.Count)
                            DetailControls[i].TypeDetailControls[j].AddWork();
                    }
                    if (DetailControls[i].TypeDetailControls.Count < DetailsModel.Details[i].TypeDetails.Count)
                        DetailControls[i].AddTypeDetail();
                }
            }
            DetailsGrid.ItemsSource = DetailsModel.Details;
        }
    }
}
