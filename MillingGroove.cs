using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Metal_Code
{
    [Serializable]
    public class MillingGroove : INotifyPropertyChanged
    {
        [field: NonSerialized]
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

        private float length;
        public float Length
        {
            get => length;
            set
            {
                if (value != length)
                {
                    length = value;
                    OnPropertyChanged(nameof(Length));
                }
            }
        }
        
        private float width;
        public float Width
        {
            get => width;
            set
            {
                if (value != width)
                {
                    width = value;
                    OnPropertyChanged(nameof(Width));
                }
            }
        }

        private int count;
        public int Count
        {
            get => count;
            set
            {
                if (value != count)
                {
                    count = value;
                    OnPropertyChanged(nameof(Count));
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

        public MillingGroove() { }

        public void SetTime(MillingWindow window)
        {
            if (window.Metal is null ||
                !window.MinutesDict["сталь"].ContainsKey((int)MainWindow.M.CorrectDestiny(0.75f * Width)))
            {
                Cutter = Time = 0;
                return;
            }

            Cutter = (int)MainWindow.M.CorrectDestiny(0.75f * Width);

            if (window.Metal.Contains("амг", StringComparison.OrdinalIgnoreCase)
                || window.Metal.Contains("д16", StringComparison.OrdinalIgnoreCase))
                Time = (int)Math.Ceiling(Count * 2 * (Length + Width + 10) * Depth / (0.15f * Width * window.Slowdown) / window.MinutesDict["алюминий"][Cutter]);
            else Time = (int)Math.Ceiling(Count * 2 * (Length + Width + 10) * Depth / (0.15f * Width * window.Slowdown) / window.MinutesDict["сталь"][Cutter]);
        }
    }
}