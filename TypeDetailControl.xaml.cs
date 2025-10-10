using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using System.Runtime.Serialization;
using System.Linq;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для Billet.xaml
    /// </summary>
    public partial class TypeDetailControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private int count;
        public int Count
        {
            get => count;
            set
            {
                if (value != count)
                {
                    count = value;
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        private float a;
        public float A
        {
            get => a;
            set
            {
                if (value != a)
                {
                    a = value;
                    OnPropertyChanged(nameof(A));
                }
            }
        }

        private float b;
        public float B
        {
            get => b;
            set
            {
                if (value != b)
                {
                    b = value;
                    OnPropertyChanged(nameof(B));
                }
            }
        }

        private float s;
        public float S
        {
            get => s;
            set
            {
                if (value != s)
                {
                    s = value;
                    OnPropertyChanged(nameof(S));
                }
            }
        }

        private float l;
        public float L
        {
            get => l;
            set
            {
                if (value != l)
                {
                    l = value;
                    OnPropertyChanged(nameof(L));
                }
            }
        }

        private float mass;
        public float Mass
        {
            get { return (float)Math.Ceiling(mass); }
            set
            {
                mass = value;
                OnPropertyChanged(nameof(Mass));
            }
        }

        private bool hasMetal;
        public bool HasMetal
        {
            get => hasMetal;
            set
            {
                if (value != hasMetal)
                {
                    hasMetal = value;
                    OnPropertyChanged(nameof(HasMetal));
                }
            }
        }

        private float price;
        public float Price
        {
            get { return price; }
            set
            {
                price = value;
                OnPropertyChanged(nameof(Price));
            }
        }

        private float result;
        public float Result
        {
            get { return result; }
            set
            {
                result = value;
                OnPropertyChanged(nameof(Result));
            }
        }

        private float extraresult;
        public float ExtraResult
        {
            get { return extraresult; }
            set
            {
                if (value != extraresult)
                {
                    extraresult = value;
                    OnPropertyChanged(nameof(ExtraResult));
                }
            }
        }

        [OptionalField]
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

        private string? comment;
        public string? Comment
        {
            get => comment;
            set
            {
                if (value != comment)
                {
                    comment = value;
                    OnPropertyChanged(nameof(Comment));
                }
            }
        }

        public readonly DetailControl det;
        public List<WorkControl> WorkControls = new();

        public TypeDetailControl(DetailControl d)
        {
            InitializeComponent();
            det = d;
            DataContext = this;
            TypeDetailDrop.ItemsSource = MainWindow.M.TypeDetails.OrderBy(x => x.Id);
            MetalDrop.ItemsSource = MainWindow.M.Metals.OrderBy(x => x.Id);
            HasMetal = true;
            CreateSort();
        }

        private void AddTypeDetail(object sender, RoutedEventArgs e)
        {
            det.AddTypeDetail();
        }

        public void AddWork()
        {
            WorkControl work = new(this);

            WorkControls.Add(work);
            WorksStack.Children.Insert(WorksStack.Children.Count - 1, work);

            //MainWindow.M.DetailsScroll.PageDown();
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            if (det.TypeDetailControls.Count == 1)
            {
                MessageBox.Show("Нельзя удалить единственную заготовку в детали.\n" +
                    "Вместо этого удалите саму деталь или сначала добавьте новую заготовку.");
                return;
            }
            Remove();
        }
        public void Remove()
        {
            det.TypeDetailControls.Remove(this);
            det.BilletsStack.Children.Remove(this);
        }

        public delegate void Changed();
        public event Changed? Priced;
        private void SetCount(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox) if (int.TryParse(tBox.Text, out int count)) SetCount(count);
        }
        public void SetCount(int _count)
        {
            Count = _count;
            PriceChanged();
        }

        private void ResultTextEnabled(object sender, MouseButtonEventArgs e)
        {
            SetCount(1);
            ResultText.IsReadOnly = false;
        }
        private void SetExtraResult(object sender, RoutedEventArgs e)
        {
            if (float.TryParse(ResultText.Text, out float extra)) SetExtraResult(extra);
        }
        public void SetExtraResult(float extra)
        {
            ExtraResult = extra;
            ResultText.IsReadOnly = true;
            PriceChanged();
        }

        private void SetComment(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox box) SetComment(box.Text);
        }
        public void SetComment(string? _comment)
        {
            Comment = _comment;
            if (Comment != null && Comment != "" && CommentExpander.IsExpanded == false) CommentExpander.IsExpanded = true;
        }

        public Dictionary<string, (string, string, string)> Kinds = new();

        private void CreateSort(object sender, SelectionChangedEventArgs e)
        {
            if (det.Detail.Title == "Комплект деталей" && TypeDetailDrop.SelectedItem is TypeDetail _c && _c.Name != "Лист металла")
            {
                foreach (TypeDetail t in MainWindow.M.TypeDetails) if (t.Name == "Лист металла")
                    {
                        TypeDetailDrop.SelectedItem = t;
                        break;
                    }
                MainWindow.M.StatusBegin($"В \"Комплекте деталей\" могут быть только \"Листы металла\". Чтобы добавить другую заготовку, сначала добавьте новую деталь!");
                return;
            }
            CreateSort();
        }
        public void CreateSort(int ndx = 0)
        {
            Kinds.Clear();
            SortDrop.Items.Clear();

            if (TypeDetailDrop.SelectedItem is not TypeDetail type) return;

            if (type.Name == "Лист металла") L = 1;
            else
            {
                A = B = S = 0;
                L = 6000;
            }

            if (det.Detail.IsComplect && type.Name == "Лист металла" && MetalDrop.SelectedItem is Metal metal)
            {
                foreach (WorkControl w in WorkControls)
                {
                    if (w.workType is ICut cut && cut.Items?.Count > 0)
                    {
                        float squareParts = 0;        //общая площадь нарезанных деталей
                        if (cut.Parts?.Count > 0) squareParts = (float)Math.Round(cut.Parts.Sum(p => p.Square * p.Part.Count) / 2, 2);

                        float squareItems = 0;        //общая площадь листов раскладки
                        var _items = cut.Items?.GroupBy(c => c.sheetSize);      //группируем все листы по размеру
                        if (_items is not null)
                            foreach (var item in _items)
                            {
                                if (cut is CutControl && item.Key is not null && item.Key.ToLower().Contains('x'))
                                {
                                    string[] props = item.Key.ToLower().Split('x');

                                    if (props.Length > 1)
                                    {
                                        Kinds[$"{item.Key} - {item.Sum(s => s.sheets)} шт"] = (props[0], props[1], $"{S}");

                                        float a = MainWindow.Parser(props[0]);
                                        float b = MainWindow.Parser(props[1]);

                                        //добавляем общую площадь листов этой группы
                                        squareItems += (float)Math.Round(a * b * item.Sum(s => s.sheets) / 1000000, 2);

                                        if ((metal.Name == "хк" || metal.Name == "ст3" || metal.Name == "09г2с" || metal.Name == "цинк")
                                            && S < 3 && (a > 2500 || b > 1250))
                                        {
                                            if (MainWindow.M.Log is null || !MainWindow.M.Log.Contains($"Проверьте раскрой листов!\nДля материала {metal.Name} толщиной до 3 мм используется лист 2500х1250."))
                                                MainWindow.M.Log += $"\nПроверьте раскрой листов!\nДля материала {metal.Name} толщиной до 3 мм используется раскрой 2500х1250.\n";
                                        }
                                        else if ((metal.Name == "латунь" || metal.Name == "медь") && (a > 1500 || b > 600))
                                        {
                                            if (MainWindow.M.Log is null || !MainWindow.M.Log.Contains($"Проверьте раскрой листов!\nДля материала {metal.Name} используется лист 1500х600."))
                                                MainWindow.M.Log += $"\nПроверьте раскрой листов!\nДля материала {metal.Name} используется раскрой 1500х600.\n";
                                        }
                                    }
                                }
                            }
                        //определяем плотность раскладки
                        if (squareParts > 0 && squareItems > 0)
                        {
                            float delta = (float)Math.Round(squareParts * 100 / squareItems, 1);
                            if (delta < 60)
                                MainWindow.M.Log += $"\nАнализ заготовки {type.Name} s{S} {metal.Name}:\n" +
                                    $"Общая площадь деталей - {squareParts} м2.\n" +
                                    $"Общая площадь листов - {squareItems} м2.\n" +
                                    $"Плотность раскладки - {delta}%.\n";
                        }
                    }
                    break;
                }
                foreach (string s in Kinds.Keys) SortDrop.Items.Add(s);
                SortDrop.SelectedIndex = 0;
            }
            else if (type.Sort is not null && type.Sort != "")
            {
                string[] strings = type.Sort.Split(',');
                for (int i = 0; i < strings.Length; i += 4) Kinds[strings[i]] = (strings[i + 1], strings[i + 2], strings[i + 3]);

                foreach (string s in Kinds.Keys) SortDrop.Items.Add(s);

                if (type.Name != "Лист металла") A_prop.IsReadOnly = B_prop.IsReadOnly = true;
                SortDrop.SelectedIndex = ndx;
            }
            else A_prop.IsReadOnly = B_prop.IsReadOnly = false;
        }

        private void ChangeSort(object sender, SelectionChangedEventArgs e)
        {
            ChangeSort();
        }
        public void ChangeSort()
        {
            if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
            {
                A = MainWindow.Parser(Kinds[$"{SortDrop.SelectedItem}"].Item1);
                B = MainWindow.Parser(Kinds[$"{SortDrop.SelectedItem}"].Item2);
                S = MainWindow.Parser(Kinds[$"{SortDrop.SelectedItem}"].Item3);

                SortDrop.BorderBrush = new SolidColorBrush(Colors.Gray);
                SortDrop.BorderThickness = new Thickness(1);
            }
            MassCalculate();
        }

        private void SetProperty(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tBox)
            {
                tBox.BorderThickness = new Thickness(1);
                switch (tBox.Name)
                {
                    case "A_prop": if (float.TryParse(tBox.Text, out float a)) A = a; break;
                    case "B_prop": if (float.TryParse(tBox.Text, out float b)) B = b; break;
                    case "S_prop": if (float.TryParse(tBox.Text, out float s)) S = s; break;
                    case "L_prop": if (float.TryParse(tBox.Text, out float l)) L = l; break;
                }
                MassCalculate();
            }
        }

        public List<(float, float)> Corners = new()     //размеры полок уголка
        {
            (3.5f, 1.2f), (3.5f, 1.2f), (4, 1.3f), (4, 1.3f), (5, 1.7f), (5.5f, 1.8f), (6, 2), (7, 2.3f),
            (7.5f, 2.5f), (7.5f, 2.5f), (8, 2.7f), (8, 2.7f), (9, 3), (9, 3), (10, 3.3f), (12, 4), (12, 4),
            (14, 4.6f), (14, 4.6f), (16, 5.3f)
        };
        public List<float> Channels = new()             //масса 1 м типоразмера швеллера
        {
            4.84f, 5.9f, 7.05f, 8.59f, 10.4f, 12.3f, 14.2f, 15.3f, 16.3f, 17.4f, 18.4f, 21, 24, 27.7f, 31.8f, 36.5f, 41.9f, 48.3f
        };
        public List<float> ChannelsSquare = new()       //суммарная площадь поверхности со всех сторон 1 тонны горячекатаных  швеллеров, м2
        {
            47.1f, 46.4f, 45.4f, 44.7f, 43.1f, 41.6f, 40.5f, 38.7f, 39.3f, 37.7f, 38.3f, 36.6f, 35, 33.2f, 31, 29.6f, 27.7f, 26.1f
        };

        public Dictionary<string, List<(float, float)>> BeamDict = new()
        {
            ["Двутавр"] = new() { (9.46f, 44.4f), (11.5f, 43.1f), (13.7f, 41.8f), (15.9f, 40.5f), (18.4f, 39.1f), (21, 38.1f), (24, 36.7f), (27.3f, 34.4f), (31.5f, 33), (36.5f, 31.2f) },
            ["Двутавр парал"] = new() { (8.1f, 49.4f), (8.7f, 54.3f), (10.4f, 45.7f), (10.5f, 52.1f), (12.9f, 42.7f), (12.7f, 48.7f), (15.8f, 39.4f), (15.4f, 45.1f), (18, 37.1f), (22.4f, 31.3f) },
            ["Двутавр широк"] = new() { (30.6f, 33.8f), (36.2f, 30.9f), (42.7f, 28.6f), (49.2f, 25.9f), (53.6f, 26), (61, 23.4f), (68.3f, 21.1f), (75.1f, 22.7f), (82.2f, 20.8f), (91.3f, 19.1f) },
            ["Двутавр колон"] = new() { (41.5f, 29.6f), (46.9f, 26.1f), (52.2f, 27.5f), (59.5f, 25.7f), (65.2f, 26.1f), (73.2f, 23.3f), (83.1f, 20.9f), (84.8f, 21.4f), (93.6f, 19.9f), (108.9f, 18.3f) }
        };

        private void MassCalculate(object sender, SelectionChangedEventArgs e)
        {
            ExtraResult = 0;
            ResultText.IsReadOnly = true;
            MetalDrop.BorderThickness = new Thickness(1);

            MassCalculate();
        }

        public void MassCalculate()
        {
            if (TypeDetailDrop.SelectedItem is not TypeDetail type || MetalDrop.SelectedItem is not Metal metal) return;

            float destiny = MainWindow.M.CorrectDestiny(S);    //получаем расчетную толщину

            if (metal.Name is not null && MainWindow.M.MetalDict[metal.Name].ContainsKey(destiny))
            {
                Price = S switch
                {
                    < 14 => type.Name == "Лист металла" ? metal.MassPrice : metal.MassPrice * 1.3f,
                    < 18 => type.Name == "Лист металла" ? metal.MassPrice * 1.05f : metal.MassPrice * 1.3f * 1.05f,
                    _ => type.Name == "Лист металла" ? metal.MassPrice * 1.15f : metal.MassPrice * 1.3f * 1.15f,
                };
            }
            else if (S == 0) Price = metal.MassPrice * 1.3f;       //для кругов и квадратов

            if (det.Detail.IsComplect)
            {
                foreach (WorkControl w in WorkControls)
                    if (w.workType is ICut cut)
                    {
                        Mass = cut.Mass;

                        Square = 0;
                        if (cut.PartsControl != null && cut.PartsControl.Parts.Count > 0)
                            foreach (PartControl p in cut.PartsControl.Parts)
                                Square += p.Square * p.Part.Count;

                        //меняем свойство материала у каждой детали при изменении металла, и толщину при изменении толщины
                        if (cut.PartDetails?.Count > 0)
                        {
                            foreach (Part part in cut.PartDetails)
                            {
                                if (part.Metal != metal.Name) part.Metal = metal.Name;
                                if (part.Destiny != S) part.Destiny = S;
                            }

                            // определяем заголовок списка нарезанных деталей
                            PartsToggle.Content = $"{(cut is PipeControl ? "(ТР) " : "")}s{S} {metal.Name} ({cut.PartDetails.Sum(x => x.Count)} деталей)";
                        }
                        break;
                    }
            }
            else
            {
                switch (type.Name)
                {
                    case "Труба профильная":
                        Mass = 0.0157f * S * (A + B - 2.86f * S) * L * metal.Density / 7850;
                        Square = L * (A + B) * 2 / 1000000;
                        break;
                    case "Труба круглая":
                        Mass = (float)Math.PI * S * (A - S) * L * metal.Density / 1000000;
                        Square = L * A * (float)Math.PI / 1000000;
                        break;
                    case "Труба круглая ВГП":
                        Mass = (float)Math.PI * S * (A - S) * L * metal.Density / 1000000;
                        Square = L * A * (float)Math.PI / 1000000;
                        break;
                    case "Уголок неравнополочный":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
                        {
                            Mass = (S * (A + B - S) + 0.2146f * (Corners[SortDrop.SelectedIndex].Item1 * Corners[SortDrop.SelectedIndex].Item1
                                - 2 * Corners[SortDrop.SelectedIndex].Item2 * Corners[SortDrop.SelectedIndex].Item2)) * L * metal.Density / 1000000;
                            Square = L * S * (A + B - S) / 1000000;
                        }  
                        break;
                    case "Уголок равнополочный":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
                        {
                            Mass = (S * (A + A - S) + 0.2146f * (Corners[SortDrop.SelectedIndex].Item1 * Corners[SortDrop.SelectedIndex].Item1
                                - 2 * Corners[SortDrop.SelectedIndex].Item2 * Corners[SortDrop.SelectedIndex].Item2)) * L * metal.Density / 1000000;
                            Square = L * S * (A + A - S) / 1000000;
                        }
                        break;
                    case "Круг":
                        Mass = (float)Math.PI * A * A * L / 4 * metal.Density / 1000000;
                        Square = 2 * (float)Math.PI * A * L / 1000000;
                        break;
                    case "Квадрат":
                        Mass = A * A * L * metal.Density / 1000000;
                        Square = 2 * (A * L + B * L + A * B) / 1000000;
                        break;
                    case "Швеллер П":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
                        {
                            Mass = Channels[SortDrop.SelectedIndex] * L / 1000;
                            Square = ChannelsSquare[SortDrop.SelectedIndex] * L / 1000000;
                        }
                        break;
                    case "Швеллер У":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
                        {
                            Mass = Channels[SortDrop.SelectedIndex] * L / 1000;
                            Square = ChannelsSquare[SortDrop.SelectedIndex] * L / 1000000;
                        }
                        break;
                    case "Двутавр":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
                        {
                            Mass = BeamDict[type.Name][SortDrop.SelectedIndex].Item1 * L / 1000;
                            Square = BeamDict[type.Name][SortDrop.SelectedIndex].Item2 * Mass / 1000;
                        }
                        break;
                    case "Двутавр парал":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
                        {
                            Mass = BeamDict[type.Name][SortDrop.SelectedIndex].Item1 * L / 1000;
                            Square = BeamDict[type.Name][SortDrop.SelectedIndex].Item2 * Mass / 1000;
                        }
                        break;
                    case "Двутавр широк":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
                        {
                            Mass = BeamDict[type.Name][SortDrop.SelectedIndex].Item1 * L / 1000;
                            Square = BeamDict[type.Name][SortDrop.SelectedIndex].Item2 * Mass / 1000;
                        }
                        break;
                    case "Двутавр колон":
                        if (Kinds.Count > 0 && SortDrop.SelectedIndex != -1)
                        {
                            Mass = BeamDict[type.Name][SortDrop.SelectedIndex].Item1 * L / 1000;
                            Square = BeamDict[type.Name][SortDrop.SelectedIndex].Item2 * Mass / 1000;
                        }
                        break;
                    default:
                        Mass = A * B * S * L * metal.Density / 1000000;
                        Square = L * (A + B) * 2 / 1000;                //для "Лист металла" (при L = 1)
                        break;
                }
            }
            PriceChanged();
        }

        private void HasMetalChanged(object sender, RoutedEventArgs e)
        {
            PriceChanged();
        }

        public void PriceChanged()
        {
            Result = HasMetal ? (float)Math.Round(                  //проверяем наличие материала
                (det.Detail.IsComplect ? 1 : Count) *               //проверяем количество заготовок
                (ExtraResult > 0 ? ExtraResult : Price * Mass)      //проверяем наличие стоимости пользователя
                , 2) : 0;

            Priced?.Invoke();
        }

        private void EnterBorder(object sender, MouseEventArgs e)
        {
            TypeDetailBox.BorderBrush = Brushes.OrangeRed;
            TypeDetailBox.BorderThickness = new Thickness(2);
        }

        private void LeaveBorder(object sender, MouseEventArgs e)
        {
            TypeDetailBox.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 213, 223, 229));  //#FFD5DFE5           
            TypeDetailBox.BorderThickness = new Thickness(1);
        }

        private void SetToolTipForResult(object sender, ToolTipEventArgs e)
        {
            if (sender is not TextBox box || TypeDetailDrop.SelectedItem is not TypeDetail type || MetalDrop.SelectedItem is not Metal metal) return;

            float destiny = MainWindow.M.CorrectDestiny(S);    //получаем расчетную толщину

            if (metal.Name is not null && MainWindow.M.MetalDict[metal.Name].ContainsKey(destiny))
            {
                box.ToolTip = S switch
                {
                    < 14 => $"Стоимость материала, руб\n(цена металла - {Math.Ceiling(type.Name == "Лист металла" ? metal.MassPrice : metal.MassPrice * 1.3f)} руб)",
                    < 18 => $"Стоимость материала, руб\n(цена металла - {Math.Ceiling(type.Name == "Лист металла" ? metal.MassPrice * 1.05f : metal.MassPrice * 1.3f * 1.05f)} руб)",
                    _ => $"Стоимость материала, руб\n(цена металла - {Math.Ceiling(type.Name == "Лист металла" ? metal.MassPrice * 1.15f : metal.MassPrice * 1.3f * 1.15f)} руб)"
                };
            }
            else if (S == 0)       //для кругов и квадратов
                box.ToolTip = $"Стоимость материала, руб\n(цена металла - {Math.Ceiling(type.Name == "Лист металла" ? metal.MassPrice : metal.MassPrice * 1.3f)} руб)";
        }

        private void AddWork(object sender, RoutedEventArgs e)
        {
            AddWork();
        }
        private void AddInfoToComment(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content != null)
                switch (btn.Content)
                {
                    case "азот":
                        var work = WorkControls.FirstOrDefault(w => w.workType is CutControl);
                        if (work != null && work.workType is CutControl cut)
                        {
                            cut.HaveNitro = !cut.HaveNitro;
                            work.Ratio = cut.HaveNitro ? 1.5f : 1;
                            btn.Background = cut.HaveNitro ? Brushes.PaleGreen : Brushes.White;

                            if (cut.HaveNitro && (Comment is null || Comment == "")) SetComment(" Азот!");
                            else if (cut.HaveNitro && Comment != null && !Comment.Contains(" Азот")) SetComment(Comment.Insert(Comment.Length, " Азот!"));
                            else if (Comment != null && Comment.Contains(" Азот")) SetComment(Comment.Replace(" Азот!", ""));
                        }
                        break;
                    case "кром":
                        if (Comment is null || Comment == "")
                        {
                            SetComment(" Или зачистка кромки!");
                            btn.Background = Brushes.PaleGreen;
                        }
                        else if (Comment != null && !Comment.Contains(" Или зачистка кромки!"))
                        {
                            SetComment(Comment.Insert(Comment.Length, " Или зачистка кромки!"));
                            btn.Background = Brushes.PaleGreen;
                        }
                        else if (Comment != null && Comment.Contains(" Или зачистка кромки!"))
                        {
                            SetComment(Comment.Replace(" Или зачистка кромки!", ""));
                            btn.Background = Brushes.White;
                        }
                        break;
                    case "шлиф":
                        if (Comment is null || Comment == "")
                        {
                            SetComment(" Внимание на направление шлифовки!");
                            btn.Background = Brushes.PaleGreen;
                        }
                        else if (Comment != null && !Comment.Contains(" Внимание на направление шлифовки!"))
                        {
                            SetComment(Comment.Insert(Comment.Length, " Внимание на направление шлифовки!"));
                            btn.Background = Brushes.PaleGreen;
                        }
                        else if (Comment != null && Comment.Contains(" Внимание на направление шлифовки!"))
                        {
                            SetComment(Comment.Replace(" Внимание на направление шлифовки!", ""));
                            btn.Background = Brushes.White;
                        }
                        break;
                    case "плен":
                        if (Comment is null || Comment == "")
                        {
                            SetComment(" Пленку не снимать!");
                            btn.Background = Brushes.PaleGreen;
                        }
                        else if (Comment != null && !Comment.Contains(" Пленку не снимать!"))
                        {
                            SetComment(Comment.Insert(Comment.Length, " Пленку не снимать!"));
                            btn.Background = Brushes.PaleGreen;
                        }
                        else if (Comment != null && Comment.Contains(" Пленку не снимать!"))
                        {
                            SetComment(Comment.Replace(" Пленку не снимать!", ""));
                            btn.Background = Brushes.White;
                        }
                        break;
                };
        }

        private void OnPartsToggleClick(object sender, RoutedEventArgs e)
        {
            if (PartsToggle.IsChecked == true)
            {
                ((Storyboard)FindResource("ExpandParts")).Begin(this);
            }
            else
            {
                ((Storyboard)FindResource("CollapseParts")).Begin(this);
            }
        }
    }
}
