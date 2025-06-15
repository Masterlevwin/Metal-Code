using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для MillingControl.xaml
    /// </summary>
    public partial class MillingControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float depth;
        public float Depth
        {
            get => depth;
            set
            {
                if (value != depth)
                {
                    depth = value;
                    OnPropertyChanged(nameof(Depth));
                }
            }
        }

        private float wide;
        public float Wide
        {
            get => wide;
            set
            {
                if (value != wide)
                {
                    wide = value;
                    OnPropertyChanged(nameof(Wide));
                }
            }
        }

        private int cutter;
        public int Cutter
        {
            get => cutter;
            set
            {
                cutter = value;
                OnPropertyChanged(nameof(Cutter));
            }
        }

        private int holes;
        public int Holes
        {
            get => holes;
            set
            {
                if (value != holes)
                {
                    holes = value;
                    OnPropertyChanged(nameof(Holes));
                }
            }
        }

        private int time;
        public int Time
        {
            get => time;
            set
            {
                time = value;
                OnPropertyChanged(nameof(Time));
            }
        }

        public readonly MillingWindow owner;
        public MillingControl(MillingWindow _window)
        {
            InitializeComponent();
            owner = _window;

            owner.DataChanged += SetTime;
        }
        
        private void SetDepth(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox && tBox.Text != "") SetDepth(tBox.Text);
        }
        public void SetDepth(string _depth)
        {
            if (float.TryParse(_depth, out float d)) Depth = d;
            SetTime();
            owner.SetTotalTime();
        }

        private void SetWide(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox && tBox.Text != "") SetWide(tBox.Text);
        }
        public void SetWide(string _wide)
        {
            if (float.TryParse(_wide, out float w)) Wide = w;
            SetTime();
            owner.SetTotalTime();
        }

        private void SetHoles(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) SetHoles(tBox.Text);
        }
        public void SetHoles(string _holes)
        {
            if (int.TryParse(_holes, out int h)) Holes = h;
            SetTime();
            owner.SetTotalTime();
        }

        private void SetTime()
        {
            if (owner.Metal is null ||
                !owner.MinutesDict["сталь"].ContainsKey((int)MainWindow.M.CorrectDestiny(0.75f * Wide)))
            {
                Cutter = Time = 0;
                return;
            }

            Cutter = (int)MainWindow.M.CorrectDestiny(0.75f * Wide);

            if (owner.Metal.Contains("амг", StringComparison.OrdinalIgnoreCase)
                || owner.Metal.Contains("д16", StringComparison.OrdinalIgnoreCase))
                Time = (int)Math.Ceiling(Holes * (Math.PI * Wide + 10) * Depth / (0.15f * Wide * owner.Slowdown) / owner.MinutesDict["алюминий"][Cutter]);
            else Time = (int)Math.Ceiling(Holes * (Math.PI * Wide + 10) * Depth / (0.15f * Wide * owner.Slowdown) / owner.MinutesDict["сталь"][Cutter]);
        }

        private void Remove(object sender, RoutedEventArgs e) { owner.RemoveControl(this); }
        private void EnterBorder(object sender, MouseEventArgs e) { BorderBrush = Brushes.OrangeRed; }
        private void LeaveBorder(object sender, MouseEventArgs e) { BorderBrush = Brushes.White; }
    }
}
