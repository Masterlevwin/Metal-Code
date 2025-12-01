using System;
using System.Collections.Generic;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : Window
    {
        public UpdateWindow(List<UpdateItem> updates)
        {
            InitializeComponent();
            ChangesListItemsControl.ItemsSource = updates;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) { Close(); }
    }

    public class UpdateItem
    {
        public int Id { get; set; }
        public string? VersionTitle { get; set; }
        public string? Description { get; set; }
        public string? ScreenshotPath { get; set; }
        public DateTime ReleaseDate { get; set; }
        public bool IsShownAtStartup { get; set; }
    }
}