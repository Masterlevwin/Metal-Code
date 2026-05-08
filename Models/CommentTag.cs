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

        public CommentTag() { }

        public CommentTag(string name, string text, bool createsFolder = false)
        {
            Name = name;
            Text = text;
            CreatesFolder = createsFolder;
        }
    }
}