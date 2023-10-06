using Microsoft.EntityFrameworkCore;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для TypeDetailSettings.xaml
    /// </summary>
    public partial class TypeDetailWindow : Window
    {
        TypeDetailContext db = new();
        public TypeDetailWindow()
        {
            InitializeComponent();
            Loaded += TypeDetailWindow_Loaded;
        }

        // при загрузке окна
        private void TypeDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // гарантируем, что база данных создана
            db.Database.EnsureCreated();
            // загружаем данные из БД
            db.TypeDetails.Load();
            // и устанавливаем данные в качестве контекста
            DataContext = db.TypeDetails.Local.ToObservableCollection();
        }

        // добавление
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            TypeDetailSettings TypeDetailSettings = new(new TypeDetail());
            if (TypeDetailSettings.ShowDialog() == true)
            {
                TypeDetail TypeDetail = TypeDetailSettings.TypeDetail;
                db.TypeDetails.Add(TypeDetail);
                db.SaveChanges();
            }
        }
        // редактирование
        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            // получаем выделенный объект
            TypeDetail? type = typesList.SelectedItem as TypeDetail;
            // если ни одного объекта не выделено, выходим
            if (type is null) return;

            TypeDetailSettings TypeDetailSettings = new(new TypeDetail
            {
                Id = type.Id,
                Name = type.Name,
                Price = type.Price
            });

            if (TypeDetailSettings.ShowDialog() == true)
            {
                // получаем измененный объект
                type = db.TypeDetails.Find(TypeDetailSettings.TypeDetail.Id);
                if (type != null)
                {
                    type.Name = TypeDetailSettings.TypeDetail.Name;
                    type.Price = TypeDetailSettings.TypeDetail.Price;
                    db.SaveChanges();
                    typesList.Items.Refresh();
                }
            }
        }
        // удаление
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            // получаем выделенный объект
            // если ни одного объекта не выделено, выходим
            if (typesList.SelectedItem is not TypeDetail type) return;
            db.TypeDetails.Remove(type);
            db.SaveChanges();
        }

        private void FocusMainWindow(object sender, System.EventArgs e)
        {
            MainWindow.M.IsEnabled = true;
        }
    }
}
