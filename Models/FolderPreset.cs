using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Metal_Code.Models
{
    public class FolderPreset : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string _name = null!;
        private bool _isEnabled; // Состояние для текущего расчета

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public FolderPreset() { }
        public FolderPreset(string name, bool isEnabled = false)
        {
            Name = name;
            IsEnabled = isEnabled;
        }

        // Клонирование для изоляции состояния расчета от глобальных настроек
        public FolderPreset Clone() => new(Name, IsEnabled);
    }
}