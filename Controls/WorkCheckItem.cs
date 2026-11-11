using System.ComponentModel;
using System.Runtime.CompilerServices;

public class WorkCheckItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string prop = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Group { get; set; } = "";
    public bool IsSingleInstance { get; set; }
    public string ThreadChar { get; set; } = "";

    private int _count;
    public int Count
    {
        get => _count;
        set
        {
            if (_count != value)
            {
                _count = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsChecked));
                OnPropertyChanged(nameof(HasItems));
            }
        }
    }

    public bool IsChecked => _count > 0;
    public bool HasItems => _count > 0;
}