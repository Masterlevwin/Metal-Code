using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для EngravingControl.xaml
    /// </summary>
    public partial class EngravingControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private string textMarking = string.Empty;
        public string TextMarking
        {
            get => textMarking;
            set
            {
                if (textMarking != value)
                {
                    textMarking = value;
                    OnPropertyChanged(nameof(TextMarking));
                }
            }
        }

        public List<string> FontList = new()
        {
            "Danger",
            "GOST type A",
            "GOST type B"
        };

        public EngravingControl()
        {
            InitializeComponent();
            DataContext = this;

            FontBox.ItemsSource = FontList;
        }
    }
}
