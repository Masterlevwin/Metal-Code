using System.ComponentModel;

namespace Metal_Code.Models
{
    public class PipeRemnant : INotifyPropertyChanged
    {
        private string _profileName = string.Empty;
        private double _length;
        private int _count;

        public string ProfileName
        {
            get => _profileName;
            set { _profileName = value; OnPropertyChanged(nameof(ProfileName)); }
        }

        public double Length
        {
            get => _length;
            set { _length = value; OnPropertyChanged(nameof(Length)); }
        }

        public int Count
        {
            get => _count;
            set { _count = value; OnPropertyChanged(nameof(Count)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}