using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PartBendControl.xaml
    /// </summary>
    public partial class PartBendControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private int bend;
        public int Bend
        {
            get => bend;
            set
            {
                if (value != bend)
                {
                    bend = value;
                    OnPropertyChanged(nameof(Bend));
                }
            }
        }

        public PartControl partControl;
        public BendControl bendControl;
        public PartBendControl(BendControl _bendControl, PartControl _partControl)
        {
            InitializeComponent();
            partControl = _partControl;
            bendControl = _bendControl;

            // формирование списка длин стороны гиба
            foreach (string s in MainWindow.M.BendDict[0.5f].Keys) ShelfDrop.Items.Add(s);
        }

        private void AddBend(object sender, RoutedEventArgs e)
        {
            partControl.AddBend(bendControl);
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (partControl.Bends.Count == 1) return;
            Remove();
        }
        public void Remove()
        {
            UpdatePosition(false);
            partControl.Bends.Remove(this);
            partControl.BendGrid.Children.Remove(this);
        }

        public void UpdatePosition(bool direction)
        {
            int numB = partControl.Bends.IndexOf(this);
            if (partControl.Bends.Count > 1)
            {
                for (int i = numB + 1; i < partControl.Bends.Count; i++)
                {
                    partControl.Bends[i].Margin = new Thickness(0,
                        direction ? partControl.Bends[i].Margin.Top + 25 : partControl.Bends[i].Margin.Top - 25, 0, 0);
                }
            }
        }

        private void SetBend(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetBend(tBox.Text);
        }
        private void SetBend(string _bend)
        {
            if (int.TryParse(_bend, out int b)) Bend = b;
        }

        private void SetShelf(object sender, SelectionChangedEventArgs e)
        {
            SetShelf(ShelfDrop.SelectedIndex);
        }
        public void SetShelf(int ndx = 0)
        {
            ShelfDrop.SelectedIndex = ndx;
        }

        public float PriceChanged()
        {
            if (Bend == 0) return 0;
            float _bendRatio = Bend * partControl.Part.Count switch
            {
                <= 10 => 3,
                <= 20 => 2,
                <= 50 => 1.5f,
                <= 200 => 1,
                _ => 0.8f,
            };

            float _price = MainWindow.M.BendDict.ContainsKey(bendControl.work.type.S) ?
                _bendRatio * Bend * partControl.Part.Count * MainWindow.M.BendDict[bendControl.work.type.S][$"{ShelfDrop.SelectedItem}"] : 0;

            // стоимость данной гибки должна быть не ниже минимальной
            if (bendControl.work.WorkDrop.SelectedItem is Work work) _price = _price > 0 && _price < work.Price? work.Price : _price;

            return _price;
        }
    }
}
