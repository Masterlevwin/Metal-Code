using Metal_Code.Converters;
using Metal_Code.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Metal_Code
{
    public partial class BendTableWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Максимальное усилие станка, тонн.</summary>
        private const double MaxPressTonnage = 100;

        private double _bendLength = 1000;
        /// <summary>Текущая длина гиба из калькулятора. Используется в привязке обводки.</summary>
        public double BendLength
        {
            get => _bendLength;
            set
            {
                if (_bendLength != value)
                {
                    _bendLength = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BendLength)));
                }
            }
        }

        private double? _selectedThickness;
        /// <summary>Толщина выбранной строки. Используется для подсветки заголовка строки.</summary>
        public double? SelectedThickness
        {
            get => _selectedThickness;
            set
            {
                if (_selectedThickness != value)
                {
                    _selectedThickness = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedThickness)));
                }
            }
        }

        /// <summary>Толщины, отображаемые строками таблицы.</summary>
        private static readonly double[] Thicknesses =
        {
            0.5, 0.8, 1.0, 1.2, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, 8.0, 10.0
        };

        private readonly BendTableService _bendTableService = new();

        private string _selectedMaterialId = "steel";
        private double? _selectedMatrixWidth;
        private Style? _cellTextStyle;
        private Style? _thicknessTextStyle;
        private Style? _forceCellStyle;

        public BendTableWindow()
        {
            InitializeComponent();

            _cellTextStyle = CreateCenteredTextStyle();
            _thicknessTextStyle = CreateThicknessTextStyle();
            _forceCellStyle = (Style)FindResource("ForceCellStyle");

            BendLengthInput.ValueChanged += (sender, e) =>
            {
                BendLength = BendLengthInput.Value;   // обновляет обводку всех ячеек через привязку
                RecalculateForce();
            };

            BuildColumns();
            LoadMaterials();
        }

        #region Построение колонок

        /// <summary>
        /// Создаёт колонки таблицы программно на основе данных сервиса.
        /// Заголовки колонок матриц — многострочные: номер матрицы, мин. полка, радиус.
        /// </summary>
        private void BuildColumns()
        {
            BendDataGrid.Columns.Clear();

            // === Первая колонка — толщина ===
            // Стиль ячейки с триггером подсветки при совпадении с выбранной толщиной
            var thicknessCellStyle = new Style(typeof(DataGridCell));
            var matchTrigger = new DataTrigger { Value = true };
            matchTrigger.Binding = new MultiBinding
            {
                Converter = new ThicknessMatchConverter(),
                Bindings =
        {
            new Binding("Thickness"),
            new Binding("SelectedThickness") { Source = this }
        }
            };
            matchTrigger.Setters.Add(new Setter(
                DataGridCell.BackgroundProperty,
                new SolidColorBrush(Color.FromRgb(255, 230, 150))));
            thicknessCellStyle.Triggers.Add(matchTrigger);

            var thicknessColumn = new DataGridTextColumn
            {
                Header = "Толщина, мм",
                Binding = new Binding("Thickness") { StringFormat = "0.#" },
                Width = new DataGridLength(100),
                IsReadOnly = true,
                ElementStyle = _thicknessTextStyle,
                CellStyle = thicknessCellStyle   // ← подсветка заголовка строки
            };
            BendDataGrid.Columns.Add(thicknessColumn);

            // === Колонки матриц ===
            var matrices = _bendTableService.GetAllMatrices()
                                            .OrderBy(m => m.MatrixWidth)
                                            .ToList();

            foreach (var matrix in matrices)
            {
                string matrixWidthStr = matrix.MatrixWidth.ToString("0", CultureInfo.InvariantCulture);
                string bindingPath = MatrixColumnPropertyName(matrix.MatrixWidth);

                // Многострочный заголовок: номер матрицы + мин. полка + радиус
                var headerPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                headerPanel.Children.Add(new TextBlock
                {
                    Text = matrixWidthStr,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                headerPanel.Children.Add(new TextBlock
                {
                    Text = $"полка {matrix.MinFlange:0.#}",
                    FontSize = 9,
                    Foreground = Brushes.DimGray,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                headerPanel.Children.Add(new TextBlock
                {
                    Text = $"R {matrix.InnerRadius:0.#}",
                    FontSize = 9,
                    Foreground = Brushes.DimGray,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                // Оборачиваем заголовок в Border для программной подсветки
                var headerBorder = new Border
                {
                    Child = headerPanel,
                    Padding = new Thickness(4, 2, 4, 2),
                    CornerRadius = new CornerRadius(3),
                    Background = Brushes.Transparent,
                    Tag = matrix.MatrixWidth
                };

                var column = new DataGridTextColumn
                {
                    Header = headerBorder,
                    Binding = new Binding(bindingPath),
                    SortMemberPath = bindingPath,
                    Width = new DataGridLength(62),
                    IsReadOnly = true,
                    CellStyle = _forceCellStyle
                };

                BendDataGrid.Columns.Add(column);
            }
        }

        /// <summary>Имя свойства для привязки колонки матрицы.</summary>
        private static string MatrixColumnPropertyName(double matrixWidth)
            => "Force_" + matrixWidth.ToString("0", CultureInfo.InvariantCulture);

        /// <summary>Стиль для ячеек с усилиями: центрирование.</summary>
        private static Style CreateCenteredTextStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Seal();
            return style;
        }

        /// <summary>Стиль для первой колонки: жирный, синий, по центру.</summary>
        private static Style CreateThicknessTextStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Blue));
            style.Seal();
            return style;
        }

        #endregion

        #region Загрузка данных

        private void LoadMaterials()
        {
            var materials = _bendTableService.GetMaterials();
            MaterialComboBox.ItemsSource = materials;

            var defaultMaterial = materials.FirstOrDefault(m => m.Id == "steel")
                                  ?? materials.FirstOrDefault();

            if (defaultMaterial != null)
                MaterialComboBox.SelectedItem = defaultMaterial;
            else
                LoadData();
        }

        /// <summary>
        /// Заполняет таблицу данными для текущего материала.
        /// Недопустимые комбинации отображаются пустыми ячейками.
        /// </summary>
        private void LoadData()
        {
            var matrices = _bendTableService.GetAllMatrices()
                                            .OrderBy(m => m.MatrixWidth)
                                            .ToList();

            var rows = new List<BendMatrixDisplayItem>();

            foreach (double thickness in Thicknesses)
            {
                var row = new BendMatrixDisplayItem { Thickness = thickness };

                foreach (var matrix in matrices)
                {
                    string value = GetForceDisplay(matrix, thickness);
                    SetForceProperty(row, matrix.MatrixWidth, value);
                }

                rows.Add(row);
            }

            BendDataGrid.ItemsSource = rows;
        }

        /// <summary>
        /// Возвращает строковое представление усилия на 1 метр гиба.
        /// Если комбинация недопустима — пустая строка.
        /// </summary>
        private string GetForceDisplay(BendMatrixData matrix, double thickness)
        {
            double? forcePerMeter = _bendTableService.GetBendingForce(
                matrix.MatrixWidth, thickness, _selectedMaterialId, 1000);

            return forcePerMeter.HasValue
                ? forcePerMeter.Value.ToString("0.#", CultureInfo.CurrentCulture)
                : "";
        }

        /// <summary>Записывает значение в соответствующее свойство модели.</summary>
        private static void SetForceProperty(BendMatrixDisplayItem row, double matrixWidth, string value)
        {
            switch (matrixWidth)
            {
                case 6: row.Force_6 = value; break;
                case 8: row.Force_8 = value; break;
                case 10: row.Force_10 = value; break;
                case 12: row.Force_12 = value; break;
                case 16: row.Force_16 = value; break;
                case 20: row.Force_20 = value; break;
                case 25: row.Force_25 = value; break;
                case 32: row.Force_32 = value; break;
                case 40: row.Force_40 = value; break;
                case 50: row.Force_50 = value; break;
                case 55: row.Force_55 = value; break;
                case 63: row.Force_63 = value; break;
                case 70: row.Force_70 = value; break;
                case 80: row.Force_80 = value; break;
                case 90: row.Force_90 = value; break;
                case 100: row.Force_100 = value; break;
            }
        }

        #endregion

        #region Выбор материала

        private void MaterialComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MaterialComboBox.SelectedItem is not BendMaterialData material)
                return;

            _selectedMaterialId = material.Id;

            UpdateFormulaDisplay(material);
            LoadData();

            // Сбрасываем подсветку и выбор при смене материала
            SelectedThickness = null;
            ClearHeaderHighlight();

            if (_selectedMatrixWidth.HasValue && _selectedThickness.HasValue)
                RecalculateForce();
            else
                ResetCalculation();
        }

        /// <summary>Обновляет формулу и описание под выбранный материал.</summary>
        private void UpdateFormulaDisplay(BendMaterialData material)
        {
            SigmaRun.Text = material.Sigma.ToString("0", CultureInfo.InvariantCulture);

            FormulaDescriptionText.Text =
                $"σв = {material.Sigma:0} МПа  |  " +
                $"Минимальное раскрытие: V ≥ {material.MinVRatio:0} × S  |  " +
                $"Радиус гиба ≈ {material.RadiusFactor * 100:0}% от V";
        }

        #endregion

        #region Расчёт усилия

        private void BendDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            var cell = BendDataGrid.CurrentCell;

            if (cell.Item is not BendMatrixDisplayItem row)
            {
                ClearHeaderHighlight();
                return;
            }

            // Устанавливаем выбранную толщину (подсвечивает заголовок строки через DataTrigger)
            SelectedThickness = row.Thickness;

            // Определяем ширину матрицы по SortMemberPath
            string? sortPath = cell.Column?.SortMemberPath;
            if (string.IsNullOrEmpty(sortPath) || !sortPath.StartsWith("Force_"))
            {
                ClearHeaderHighlight();
                return;
            }

            string matrixWidthStr = sortPath.Substring("Force_".Length);
            if (!double.TryParse(matrixWidthStr, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double matrixWidth))
            {
                ClearHeaderHighlight();
                return;
            }

            _selectedMatrixWidth = matrixWidth;
            _selectedThickness = row.Thickness;

            // Подсвечиваем заголовок колонки матрицы
            HighlightColumnHeader(matrixWidth);

            RecalculateForce();
        }

        /// <summary>Сбрасывает подсветку всех заголовков колонок матриц.</summary>
        private void ClearHeaderHighlight()
        {
            foreach (var column in BendDataGrid.Columns.Skip(1))
            {
                if (column.Header is Border border)
                    border.Background = Brushes.Transparent;
            }
        }

        /// <summary>Подсвечивает заголовок колонки с указанной шириной матрицы.</summary>
        private void HighlightColumnHeader(double matrixWidth)
        {
            ClearHeaderHighlight();

            foreach (var column in BendDataGrid.Columns.Skip(1))
            {
                if (column.Header is Border border &&
                    border.Tag is double tagWidth &&
                    Math.Abs(tagWidth - matrixWidth) < 0.001)
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(255, 230, 150));
                    break;
                }
            }
        }

        private void RecalculateForce()
        {
            if (!_selectedMatrixWidth.HasValue || !_selectedThickness.HasValue)
                return;

            double bendLength = BendLengthInput?.Value ?? 0;

            if (bendLength <= 0)
            {
                CalculatedForceText.Text = "—";
                CanBendText.Text = "Укажите длину гиба";
                CanBendText.Foreground = GetThemeBrush("SecondaryTextBrush", Brushes.Gray);
                return;
            }

            double matrixWidth = _selectedMatrixWidth.Value;
            double thickness = _selectedThickness.Value;

            var matrix = _bendTableService.GetMatrixData(matrixWidth);

            if (matrix != null)
            {
                MatrixInfoText.Text =
                    $"Матрица V = {matrix.MatrixWidth:0} мм   |   " +
                    $"Толщина S = {thickness:0.#} мм   |   " +
                    $"Мин. полка = {matrix.MinFlange:0.#} мм   |   " +
                    $"Радиус R = {matrix.InnerRadius:0.#} мм";
            }

            double? force = _bendTableService.GetBendingForce(
                matrixWidth, thickness, _selectedMaterialId, bendLength);

            if (force == null)
            {
                CalculatedForceText.Text = "—";
                CanBendText.Text = "Недопустимая комбинация";
                CanBendText.Foreground = GetThemeBrush("SecondaryTextBrush", Brushes.Gray);
                return;
            }

            CalculatedForceText.Text = $"{force.Value:0.#} т";

            bool canBend = force.Value <= MaxPressTonnage;

            CanBendText.Text = canBend
                ? $"✓ Согнём (≤ {MaxPressTonnage:0} т)"
                : $"✗ Превышает {MaxPressTonnage:0} т";

            CanBendText.Foreground = canBend
                ? GetThemeBrush("SuccessBrush", Brushes.Green)
                : GetThemeBrush("DangerBrush", Brushes.Red);

            Title = $"Справочник гибки — V = {matrixWidth:0} мм, S = {thickness:0.#} мм";
        }

        private void ResetCalculation()
        {
            CalculatedForceText.Text = "—";
            CanBendText.Text = "";
            CanBendText.Foreground = GetThemeBrush("SecondaryTextBrush", Brushes.Gray);

            MatrixInfoText.Text =
                "Выберите ячейку, чтобы увидеть параметры матрицы и расчётное усилие.";

            Title = "Справочник гибки листового металла";
        }

        private Brush GetThemeBrush(string resourceKey, Brush fallback)
            => TryFindResource(resourceKey) as Brush ?? fallback;

        #endregion
    }

    /// <summary>Модель строки таблицы: толщина + усилия для каждой матрицы.</summary>
    public class BendMatrixDisplayItem
    {
        public double Thickness { get; set; }
        public string Force_6 { get; set; } = "";
        public string Force_8 { get; set; } = "";
        public string Force_10 { get; set; } = "";
        public string Force_12 { get; set; } = "";
        public string Force_16 { get; set; } = "";
        public string Force_20 { get; set; } = "";
        public string Force_25 { get; set; } = "";
        public string Force_32 { get; set; } = "";
        public string Force_40 { get; set; } = "";
        public string Force_50 { get; set; } = "";
        public string Force_55 { get; set; } = "";
        public string Force_63 { get; set; } = "";
        public string Force_70 { get; set; } = "";
        public string Force_80 { get; set; } = "";
        public string Force_90 { get; set; } = "";
        public string Force_100 { get; set; } = "";
    }
}