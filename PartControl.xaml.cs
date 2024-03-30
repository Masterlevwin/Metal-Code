using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для PartControl.xaml
    /// </summary>
    public partial class PartControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private float square;
        public float Square
        {
            get => square;
            set
            {
                if (value != square)
                {
                    square = value;
                    OnPropertyChanged(nameof(Square));
                }
            }
        }

        public delegate void PropsChanged(UserControl uc, bool b);
        public PropsChanged? PropertiesChanged;

        public readonly UserControl owner;
        public readonly WorkControl work;
        public Part Part { get; set; }

        public List<UserControl> UserControls = new();

        public PartControl(UserControl _owner, WorkControl _work, Part _part)
        {
            InitializeComponent();
            owner = _owner;
            work = _work;
            Part = _part;
            DataContext = Part;

            SetSquare();        //устанавливаем площадь детали
        }

        private void SetSquare()        //метод определения площади детали
        {
            if (owner is PipeControl)
            {
                SquareText.Text = "м";
                Square = Part.Way;
            }
            else
            {
                if (!Part.PropsDict.ContainsKey(100)) return;

                if (float.TryParse(Part.PropsDict[100][0], out float h) && float.TryParse(Part.PropsDict[100][1], out float w))
                    Square = (float)Math.Round(h * w / 500000, 3);
            }
        }

        private void AddControl(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                switch (btn.Name)
                {
                    case "BendBtn":
                        AddControl(0);
                        break;
                    case "WeldBtn":
                        AddControl(1);
                        break;
                    case "PaintBtn":
                        AddControl(2);
                        break;
                    case "ThreadBtn":
                        AddControl(3);
                        break;
                    case "CountersinkBtn":
                        AddControl(4);
                        break;
                    case "DrillingBtn":
                        AddControl(5);
                        break;
                    case "RollingBtn":
                        AddControl(6);
                        break;
                }
        }
        public void AddControl(int index)
        {
            switch (index)
            {
                case 0:
                    AddControl(new BendControl(this));
                    break;
                case 1:
                    AddControl(new WeldControl(this));
                    break;
                case 2:
                    AddControl(new PaintControl(this));
                    break;
                case 3:
                    AddControl(new ThreadControl(this, 'Р'));
                    break;
                case 4:
                    AddControl(new ThreadControl(this, 'З'));
                    break;
                case 5:
                    AddControl(new ThreadControl(this, 'С'));
                    break;
                case 6:
                    if (UserControls.Count > 0 && UserControls.Contains(UserControls.FirstOrDefault(r => r is RollingControl)))
                    {
                        MainWindow.M.StatusBegin("Нельзя добавить больше одной вальцовки на деталь");
                        return;
                    }
                    AddControl(new RollingControl(this));
                    break;
            }
        }
        public void AddControl(UserControl uc)
        {
            if (UserControls.Count > 0) uc.Margin = new Thickness(0, UserControls[^1].Margin.Top + 30, 0, 0);

            UserControls.Add(uc);
            ControlGrid.Children.Add(uc);

            Grid.SetRow(uc, 1);
        }

        public void RemoveControl(UserControl uc)
        {
            if (uc is IPriceChanged work) PropertiesChanged -= work.SaveOrLoadProperties;
            UserControls.Remove(uc);
            ControlGrid.Children.Remove(uc);
        }

        private void SetPicture(object sender, RoutedEventArgs e)       //метод вызывается при загрузке элемента Image (Image.Loaded) 
        {
            if (Part.ImageBytes != null) Picture.Source = CreateBitmap(Part.ImageBytes);
        }

        private static BitmapImage CreateBitmap(byte[] imageBytes)      //метод преобразования массива байтов в изображение BitmapImage
        {
            BitmapImage? image = new();
            image.BeginInit();
            image.StreamSource = new MemoryStream(imageBytes);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }

    public interface ICut
    {
        public PartsControl? PartsControl { get; set; }
        public List<Part>? PartDetails {  get; set; }
    }
}
