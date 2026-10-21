using Metal_Code.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Metal_Code
{
    public partial class BendTableWindow : Window
    {
        private readonly BendTableService _bendTableService;
        private string _selectedMaterialId = "steel";

        public BendTableWindow()
        {
            InitializeComponent();
            _bendTableService = new BendTableService();
            LoadMaterials();
            LoadData();
        }

        private void LoadMaterials()
        {
            var materials = _bendTableService.GetMaterials();
            MaterialComboBox.ItemsSource = materials;

            // По умолчанию — сталь
            var defaultMaterial = materials.FirstOrDefault(m => m.Id == "steel");
            if (defaultMaterial != null)
                MaterialComboBox.SelectedItem = defaultMaterial;
        }

        private void MaterialComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MaterialComboBox.SelectedItem is BendMaterialData material)
            {
                _selectedMaterialId = material.Id;
                UpdateFormulaDisplay(material);
                LoadData();
            }
        }

        private void UpdateFormulaDisplay(BendMaterialData material)
        {
            // Обновляем значение σв в формуле
            SigmaRun.Text = material.Sigma.ToString("0");
            SigmaRun.Foreground = new BrushConverter().ConvertFromString(material.Color) as SolidColorBrush;

            // Формируем описание с учётом min_v_ratio
            FormulaDescriptionText.Text = material.MinVRatio switch
            {
                8 => $"σв = {material.Sigma} МПа | Требует V ≥ 8S (нержавейка)",
                7 => $"σв = {material.Sigma} МПа | Требует V ≥ 7S (латунь)",
                _ => $"σв = {material.Sigma} МПа | Требует V ≥ 6S"
            };
        }

        private void LoadData()
        {
            var matrices = _bendTableService.GetAllMatrices();

            var displayData = matrices.Select(m => new BendMatrixDisplayItem
            {
                MatrixWidth = m.MatrixWidth,
                MinFlange = m.MinFlange,
                InnerRadius = m.InnerRadius,
                Force_08 = GetForceDisplay(m, "0.8"),
                Force_10 = GetForceDisplay(m, "1.0"),
                Force_12 = GetForceDisplay(m, "1.2"),
                Force_15 = GetForceDisplay(m, "1.5"),
                Force_20 = GetForceDisplay(m, "2.0"),
                Force_25 = GetForceDisplay(m, "2.5"),
                Force_30 = GetForceDisplay(m, "3.0"),
                Force_40 = GetForceDisplay(m, "4.0"),
                Force_50 = GetForceDisplay(m, "5.0"),
                Force_60 = GetForceDisplay(m, "6.0"),
                Force_80 = GetForceDisplay(m, "8.0"),
                Force_100 = GetForceDisplay(m, "10.0")
            }).ToList();

            BendDataGrid.ItemsSource = displayData;
        }

        private string GetForceDisplay(BendMatrixData matrix, string thicknessKey)
        {
            if (!matrix.Forces.TryGetValue(_selectedMaterialId, out var materialForces))
                return "—";

            if (!materialForces.TryGetValue(thicknessKey, out double force))
                return "—";

            return force.ToString("0.#");
        }

        private BendMatrixData? _selectedMatrix;
        private double? _selectedThickness;

        // Соответствие индексов колонок с усилиями и толщин металла.
        // Индексы 0, 1, 2 — служебные колонки (матрица, полка, радиус).
        private static readonly double?[] ThicknessByColumnIndex =
        {
    null, null, null,       // колонки 0-2: служебные
    0.8, 1.0, 1.2, 1.5,     // колонки 3-6
    2.0, 2.5, 3.0, 4.0,     // колонки 7-10
    5.0, 6.0, 8.0, 10.0     // колонки 11-14
};

        private void BendDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            var cellInfo = BendDataGrid.CurrentCell;
            if (cellInfo.Item is not BendMatrixDisplayItem item) return;

            int columnIndex = cellInfo.Column.DisplayIndex;
            if (columnIndex < 0 || columnIndex >= ThicknessByColumnIndex.Length) return;

            double? thickness = ThicknessByColumnIndex[columnIndex];
            if (thickness == null) return; // клик по служебной колонке

            _selectedMatrix = _bendTableService.GetMatrixData(item.MatrixWidth);
            _selectedThickness = thickness;

            RecalculateForce();
        }

        private void BendLengthInput_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            RecalculateForce();
        }

        private void RecalculateForce()
        {
            if (_selectedMatrix == null || _selectedThickness == null) return;

            double bendLength = BendLengthInput.Value;
            var force = _bendTableService.GetBendingForce(
                _selectedMatrix.MatrixWidth, _selectedThickness.Value,
                _selectedMaterialId, bendLength);

            if (force == null)
            {
                CalculatedForceText.Text = "—";
                CanBendText.Text = "Недопустимая комбинация";
                CanBendText.Foreground = Brushes.Red;
                return;
            }

            CalculatedForceText.Text = $"{force:0.#} т";

            const double maxPressTonnage = 100;
            if (force.Value <= maxPressTonnage)
            {
                CanBendText.Text = "✓ Согнём";
                CanBendText.Foreground = Brushes.Green;
            }
            else
            {
                CanBendText.Text = $"✗ Превышает {maxPressTonnage} т";
                CanBendText.Foreground = Brushes.Red;
            }

            // Обновляем заголовок — показываем что именно выбрано
            Title = $"Справочник гибки — V={_selectedMatrix.MatrixWidth} мм, S={_selectedThickness:0.#} мм";
        }
    }

    public class BendMatrixDisplayItem
    {
        public double MatrixWidth { get; set; }
        public double MinFlange { get; set; }
        public double InnerRadius { get; set; }
        public string Force_08 { get; set; } = "—";
        public string Force_10 { get; set; } = "—";
        public string Force_12 { get; set; } = "—";
        public string Force_15 { get; set; } = "—";
        public string Force_20 { get; set; } = "—";
        public string Force_25 { get; set; } = "—";
        public string Force_30 { get; set; } = "—";
        public string Force_40 { get; set; } = "—";
        public string Force_50 { get; set; } = "—";
        public string Force_60 { get; set; } = "—";
        public string Force_80 { get; set; } = "—";
        public string Force_100 { get; set; } = "—";
    }
}