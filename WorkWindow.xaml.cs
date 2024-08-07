﻿using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для WorkWindow.xaml
    /// </summary>
    public partial class WorkWindow : Window
    {
        WorkContext db = new(MainWindow.M.IsLocal ? MainWindow.M.connections[4] : MainWindow.M.connections[5]);
        public WorkWindow()
        {
            InitializeComponent();
            Loaded += WorkWindow_Loaded;
        }

        // при загрузке окна
        private void WorkWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // загружаем данные из БД
            db.Works.Load();
            // и устанавливаем данные в качестве контекста
            DataContext = db.Works.Local.ToObservableCollection();

            if (!MainWindow.M.CurrentManager.IsAdmin) foreach (UIElement element in ButtonsStack.Children)
                    if (element is Button) element.IsEnabled = false;
        }

        // добавление
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            WorkSettings WorkSettings = new WorkSettings(new Work());
            if (WorkSettings.ShowDialog() == true)
            {
                Work Work = WorkSettings.Work;
                db.Works.Add(Work);
                db.SaveChanges();
            }
        }
        // редактирование
        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            // получаем выделенный объект
            Work? work = typesList.SelectedItem as Work;
            // если ни одного объекта не выделено, выходим
            if (work is null) return;

            WorkSettings WorkSettings = new WorkSettings(new Work
            {
                Id = work.Id,
                Name = work.Name,
                Price = work.Price,
                Time = work.Time
            });

            if (WorkSettings.ShowDialog() == true)
            {
                // получаем измененный объект
                work = db.Works.Find(WorkSettings.Work.Id);
                if (work != null)
                {
                    work.Name = WorkSettings.Work.Name;
                    work.Price = WorkSettings.Work.Price;
                    work.Time = WorkSettings.Work.Time;
                    db.SaveChanges();
                    typesList.Items.Refresh();
                }
            }
        }
        // удаление
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            // получаем выделенный объект
            Work? work = typesList.SelectedItem as Work;
            // если ни одного объекта не выделено, выходим
            if (work is null) return;
            db.Works.Remove(work);
            db.SaveChanges();
        }

        private void FocusMainWindow(object sender, System.EventArgs e)
        {
            MainWindow.M.IsEnabled = true;
        }
    }
}
