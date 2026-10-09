using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Metal_Code.Models
{
    public class CommentTag : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string _name = null!;
        private string _text = null!;
        private bool _createsFolder;
        private bool _isProtected;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); }
        }

        public bool CreatesFolder
        {
            get => _createsFolder;
            set { _createsFolder = value; OnPropertyChanged(); }
        }

        public bool IsProtected
        {
            get => _isProtected;
            set { _isProtected = value; OnPropertyChanged(); }
        }

        public float PriceRatio { get; }
        public int DestinyOffset { get; }

        public string PriceBadge => PriceRatio > 1.0f ? $"×{PriceRatio:g4}" : string.Empty;
        public string DestinyBadge => DestinyOffset > 0 ? $"s+{DestinyOffset}" : string.Empty;
        public bool HasAnyBadge => !string.IsNullOrEmpty(PriceBadge) || !string.IsNullOrEmpty(DestinyBadge);

        public CommentTag(string name, string text, bool createsFolder = false,
                          float priceRatio = 1.0f, int destinyOffset = 0, bool isProtected = false)
        {
            Name = name;
            Text = text;
            CreatesFolder = createsFolder;
            PriceRatio = priceRatio;
            DestinyOffset = destinyOffset;
            IsProtected = isProtected;
        }
    }
}