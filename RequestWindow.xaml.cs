using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WorkWindow.xaml
    /// </summary>
    public partial class RequestWindow : Window
    {
        public string[] Paths { get; set; }
        public RequestTemplate RequestTemplate { get; set; } = new("Шаблон по умолчанию");
        public ObservableCollection<RequestTemplate> Templates { get; set; } = new();

        public RequestWindow(string[] paths)
        {
            InitializeComponent();
            Paths = paths;
            DataContext = this;
        }

        private void Save_Changes(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"Выбран шаблон {RequestTemplate.Name}");
        }

        private void Save_Template(object sender, RoutedEventArgs e)
        {
            RequestTemplate.Name = "";
            if (TemplateNameStack.Children.Count > 0)
                foreach (TextBlock child in TemplateNameStack.Children)
                    if (child.Visibility == Visibility.Visible) RequestTemplate.Name += $"{child.Text} ";

            Templates.Add(RequestTemplate);
        }

        private void Template_Selected(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox list && list.SelectedItem is RequestTemplate template)
            {
                MessageBox.Show($"Выбран шаблон {template.Name}. Текущий шаблон - {RequestTemplate.Name}");
            }
        }
    }

    public class RequestTemplate
    {
        public string Name { get; set; } = null!;
        public string DestinyPattern { get; set; } = "s";
        public string CountPattern { get; set; } = "n";
        public bool BeforeDestiny {  get; set; } = true;
        public bool BeforeCount { get; set; } = true;
        public bool AfterDestiny {  get; set; } = false;
        public bool AfterCount { get; set; } = false;

        public RequestTemplate(string name) { Name = name; }
    }
}