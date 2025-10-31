using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Metal_Code
{
    /// <summary>
    /// Логика взаимодействия для ProductWindow.xaml
    /// </summary>
    public partial class ProductWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public string ProductName { get; set; } = "Изделие";
        public double Ratio { get; set; } = 1;
        public float Amount { get; set; } = 0;
        public string Agent { get; set; } = string.Empty;
        public bool? HasDelivery { get; set; } = false;
        public int Delivery { get; set; } = 0;
        public int DeliveryRatio { get; set; } = 1;
        public bool? HasConstruct { get; set; } = false;
        public float Construct {  get; set; } = 0;
        public float BonusRatio { get; set; } = 0;

        private float bonus = 0;
        public float Bonus
        {
            get => bonus;
            set
            {
                bonus = value;
                OnPropertyChanged(nameof(Bonus));
            }
        }

        public string Production { get; set; } = string.Empty;

        public Product Product { get; set; } = new();
        public List<PartControl> Parts { get; set; } = new();

        public ProductWindow(Product product)
        {
            InitializeComponent();
            DataContext = this;
            Product = product;

            ProductName = product.Name;
            Ratio = Product.Ratio;
            Agent = product.IsAgent is true ? "без НДС" : "с НДС";
            HasDelivery = product.HasDelivery;
            Delivery = product.Delivery;
            DeliveryRatio = product.DeliveryRatio;
            HasConstruct = product.HasConstruct;
            if (product.ConstructRatio is not null) Construct = MainWindow.Parser(product.ConstructRatio) * 1500;
            BonusRatio = product.BonusRatio;
            Production = product.Production;

            if (Product.Details.Count > 0) LoadDetails(Product.Details);
        }

        //-----------Загрузка окна---------------------------------------//
        private void Loaded_Window(object sender, RoutedEventArgs e)
        {
            if (BonusRatio > 0) Bonus = Amount - (Amount / (1 + BonusRatio / 100));
        }

        //-----------Загрузка контролов----------------------------------//
        public void LoadDetails(ObservableCollection<Detail> details)
        {
            for (int i = 0; i < details.Count; i++)
            {
                AddDetail();
                DetailControl _det = DetailControls[i];
                _det.Detail.Title = details[i].Title;

                //если деталь является Комплектом, запускаем ограничения
                if (_det.Detail.Title != null && _det.Detail.Title.Contains("Комплект")) _det.IsComplectChanged();

                _det.Detail.Count = details[i].Count;

                for (int j = 0; j < details[i].TypeDetails.Count; j++)
                {
                    TypeDetailControl _type = DetailControls[i].TypeDetailControls[j];
                    _type.TypeDetailDrop.SelectedIndex = details[i].TypeDetails[j].Index;
                    _type.Count = details[i].TypeDetails[j].Count;
                    _type.MetalDrop.SelectedIndex = details[i].TypeDetails[j].Metal;
                    _type.SortDrop.SelectedIndex = details[i].TypeDetails[j].Tuple.Item1;
                    _type.A = details[i].TypeDetails[j].Tuple.Item2;
                    _type.B = details[i].TypeDetails[j].Tuple.Item3;
                    _type.S = details[i].TypeDetails[j].Tuple.Item4;
                    _type.L = details[i].TypeDetails[j].Tuple.Item5;
                    _type.HasMetal = details[i].TypeDetails[j].HasMetal;
                    _type.ExtraResult = details[i].TypeDetails[j].ExtraResult;
                    _type.SetComment(details[i].TypeDetails[j].Comment);

                    for (int k = 0; k < details[i].TypeDetails[j].Works.Count; k++)
                    {
                        WorkControl _work = DetailControls[i].TypeDetailControls[j].WorkControls[k];

                        foreach (Work w in _work.WorkDrop.Items)        // чтобы не подвязываться на сохраненный индекс работы, ориентируемся на ее имя
                                                                        // таким образом избегаем ошибки, когда админ изменит порядок работ в базе данных
                            if (w.Name == details[i].TypeDetails[j].Works[k].NameWork)
                            {
                                _work.WorkDrop.SelectedIndex = _work.WorkDrop.Items.IndexOf(w);
                                break;
                            }

                        if (_work.workType is ICut _cut && details[i].TypeDetails[j].Works[k].Items?.Count > 0)
                        {
                            _cut.Items = details[i].TypeDetails[j].Works[k].Items;
                            _cut.PartDetails = details[i].TypeDetails[j].Works[k].Parts;

                            if (_cut is CutControl cut)
                            {
                                if (_cut.Items?.Count > 0) cut.SumProperties(_cut.Items);
                                cut.Parts = cut.PartList();
                                cut.PartsControl = new(cut, cut.Parts);
                                cut.AddPartsControl(this);
                            }
                            else if (_cut is PipeControl pipe)
                            {
                                pipe.Parts = pipe.PartList();
                                pipe.PartsControl = new(pipe, pipe.Parts);
                                pipe.AddPartsControl(this);
                                pipe.SetTotalProperties();
                            }

                            if (_cut.PartsControl?.Parts.Count > 0)
                            {
                                foreach (PartControl part in _cut.PartsControl.Parts)
                                {
                                    if (part.Part.WorksDict?.Count > 0)
                                        foreach (var guid in part.Part.WorksDict.Keys)
                                            part.AddControl((int)MainWindow.Parser(part.Part.WorksDict[guid][0]), guid);

                                    if (part.Part.PropsDict.Count > 0)
                                        foreach (int key in part.Part.PropsDict.Keys) if (key < 50)
                                                part.AddControl((int)MainWindow.Parser(part.Part.PropsDict[key][0]));
                                    part.PropertiesChanged?.Invoke(part, false);
                                }

                                Parts.AddRange(_cut.PartsControl.Parts);
                            }
                        }

                        _work.propsList = details[i].TypeDetails[j].Works[k].PropsList;
                        _work.PropertiesChanged?.Invoke(_work, false);
                        _work.Ratio = details[i].TypeDetails[j].Works[k].Ratio;
                        _work.TechRatio = details[i].TypeDetails[j].Works[k].TechRatio;
                        _work.ExtraResult = details[i].TypeDetails[j].Works[k].ExtraResult;

                        if (_type.WorkControls.Count < details[i].TypeDetails[j].Works.Count) _type.AddWork();
                    }
                    if (_det.TypeDetailControls.Count < details[i].TypeDetails.Count) _det.AddTypeDetail();
                }
            }
        }

        //-----------Добавление контрола детали--------------------------//
        public List<DetailControl> DetailControls = new();
        private void AddDetail(object sender, RoutedEventArgs e) { AddDetail(); }
        public void AddDetail()
        {
            DetailControl detail = new(new());

            DetailControls.Add(detail);
            detail.Counter.Text = $"{DetailControls.IndexOf(detail) + 1}";

            DetailsStack.Children.Add(detail);

            detail.AddTypeDetail();   // при добавлении новой детали добавляем дроп заготовки
        }


        //-----------Копирование всех контролов списка вложений----------//
        private void CopyUserControls(object sender, RoutedEventArgs e)
        {
            if (MainWindow.M.Parts.Count == 0)
            {
                MessageBox.Show("В текущем расчете нет нарезанных деталей.\nДобавьте раскладки или загрузите расчет с ними.");
                return;
            }

            // Получаем все "целевые" панели
            var targetPanels = MainWindow.M.DetailControls
                .Where(d => d.Detail.IsComplect)
                .SelectMany(t => t.TypeDetailControls)
                .Select(p => p.PartsStack)
                .Where(panel => panel != null && panel.Children.Count > 0 && panel.Children[0] is PartsControl);

            bool isMatch = false;

            // Группируем исходные Parts по "базовому имени" для быстрого поиска
            var sourcePartsByBaseName = Parts
                .Where(p => p.Part.Title != null)
                .ToLookup(p => ExtractBaseName(p.Part.Title), StringComparer.OrdinalIgnoreCase);

            foreach (var panel in targetPanels)
            {
                var partsMain = (PartsControl)panel.Children[0];
                foreach (PartControl partMain in partsMain.Parts)
                {
                    if (partMain.Part.Title == null) continue;

                    string baseName = ExtractBaseName(partMain.Part.Title);
                    var matchingSourceParts = sourcePartsByBaseName[baseName];

                    if (!matchingSourceParts.Any()) continue;

                    // Очистка текущих контролов
                    while (partMain.UserControls.Count > 0)
                        partMain.RemoveControl(partMain.UserControls[^1]);

                    // Копируем данные из первого подходящего источника (можно уточнить логику, если нужно)
                    foreach (var sourcePart in matchingSourceParts)
                    {
                        // Копируем работы
                        if (sourcePart.Part.WorksDict?.Count > 0)
                        {
                            foreach (var kvp in sourcePart.Part.WorksDict)
                            {
                                var guid = kvp.Key;
                                var value = kvp.Value;
                                if (value != null && value.Count > 0)
                                {
                                    int controlType = (int)MainWindow.Parser(value[0]);
                                    partMain.AddControl(controlType, guid);
                                }
                            }
                        }

                        // Копируем свойства (key < 50)
                        if (sourcePart.Part.PropsDict?.Count > 0)
                        {
                            foreach (var kvp in sourcePart.Part.PropsDict)
                            {
                                if (kvp.Key < 50 && kvp.Value?.Count > 0)
                                {
                                    int controlType = (int)MainWindow.Parser(kvp.Value[0]);
                                    partMain.AddControl(controlType);
                                }
                            }
                        }

                        partMain.PropertiesChanged?.Invoke(sourcePart, false);
                        isMatch = true;
                        break;
                    }
                }
            }

            if (!isMatch)
                MessageBox.Show("Совпадений не найдено.\nПоменяйте имена нарезанных деталей и попробуйте снова.");
            else
                MessageBox.Show("Работы по нарезанным деталям текущего расчета добавлены!");

            MainWindow.M.StatusBegin("Если открытый для чтения расчет больше не требуется, рекомендуется закрыть его окно.");
        }

        // Вспомогательный метод: извлекает "базовое имя" возле количества
        private static string ExtractBaseName(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            title = title.Trim();

            var match = System.Text.RegularExpressions.Regex.Match(
                title,
                @"^(.*?)\s*(?:(?:\d+\s*шт\.?|n\.?\d+))\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
                return match.Groups[1].Value.Trim();

            return title;
        }
    }
}
