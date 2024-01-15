using System;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для SetManagerWindow.xaml
    /// </summary>
    public partial class SetManagerWindow : Window
    {
        public Manager SelectManager = new();
        public SetManagerWindow()
        {
            InitializeComponent();
            ManagerDrop.ItemsSource = MainWindow.M.ManagerDrop.ItemsSource;
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (ManagerDrop.SelectedItem is Manager manager)
            {
                SelectManager = manager;
                DialogResult = true;
            }
            else MessageBox.Show("Выберите менеджера из выпадающего списка.\nВ дальнейшем Вы сможете его поменять.");
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
