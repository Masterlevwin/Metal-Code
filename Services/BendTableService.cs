using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Metal_Code.Services
{
    /// <summary>
    /// Сервис справочной таблицы параметров гибки листового металла.
    /// Загружает данные из JSON-файла или использует встроенные значения по умолчанию.
    /// </summary>
    public class BendTableService
    {
        private BendTableData? _tableData;
        private readonly string _jsonFilePath;

        public BendTableService(string jsonFilePath = "bend_table.json")
        {
            _jsonFilePath = jsonFilePath;
            LoadTable();
        }

        private void LoadTable()
        {
            try
            {
                if (File.Exists(_jsonFilePath))
                {
                    string json = File.ReadAllText(_jsonFilePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    _tableData = JsonSerializer.Deserialize<BendTableData>(json, options);
                }

                if (_tableData == null || _tableData.Matrices.Count == 0)
                    _tableData = CreateDefaultTable();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"⚠️ Ошибка загрузки таблицы гибки: {ex.Message}");
                _tableData = CreateDefaultTable();
            }
        }

        /// <summary>Получить параметры матрицы по ширине раскрытия.</summary>
        public BendMatrixData? GetMatrixData(double matrixWidth)
        {
            return _tableData?.Matrices.FirstOrDefault(m => Math.Abs(m.MatrixWidth - matrixWidth) < 0.1);
        }

        /// <summary>Получить список доступных материалов.</summary>
        public List<BendMaterialData> GetMaterials()
            => _tableData?.Materials ?? new List<BendMaterialData>();

        /// <summary>Получить усилие гибки (тонны) для заданных параметров и материала.</summary>
        public double? GetBendingForce(double matrixWidth, double thickness,
            string materialId = "steel", double bendLengthMm = 1000)
        {
            var matrix = GetMatrixData(matrixWidth);
            if (matrix == null) return null;
            if (!matrix.Forces.TryGetValue(materialId, out var materialForces)) return null;

            // Используем инвариантную культуру: десятичная точка, а не запятая
            string key = thickness.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (!materialForces.TryGetValue(key, out double forcePerMeter)) return null;

            return forcePerMeter * (bendLengthMm / 1000.0);
        }

        /// <summary>
        /// Подобрать оптимальную матрицу для заданных параметров.
        /// Возвращает наименьшую подходящую матрицу.
        /// </summary>
        public BendMatrixData? FindOptimalMatrix(double thickness, double flangeWidth,
            string materialId = "steel")
        {
            if (_tableData == null) return null;

            string thicknessKey = thickness.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

            return _tableData.Matrices
                .Where(m =>
                {
                    bool hasValidForce = m.Forces.TryGetValue(materialId, out var forces)
                        && forces.ContainsKey(thicknessKey);
                    return hasValidForce && m.MinFlange <= flangeWidth;
                })
                .OrderBy(m => m.MatrixWidth)
                .FirstOrDefault();
        }

        /// <summary>Получить все доступные матрицы.</summary>
        public List<BendMatrixData> GetAllMatrices() => _tableData?.Matrices ?? new List<BendMatrixData>();

        /// <summary>Получить метаданные таблицы (используется в BendTableWindow).</summary>
        public BendTableMetadata GetMetadata() => _tableData?.Metadata ?? new BendTableMetadata();

        #region Default Data

        private BendTableData CreateDefaultTable()
        {
            var materials = new List<BendMaterialData>
            {
                new() { Id = "steel",     Name = "Сталь (Ст3, 09Г2С, х/к)",        Sigma = 450, MinVRatio = 6, RadiusFactor = 0.16, Color = "#1565C0" },
                new() { Id = "stainless", Name = "Нержавейка (AISI 304, 316, 430)", Sigma = 580, MinVRatio = 8, RadiusFactor = 0.20, Color = "#2E7D32" },
                new() { Id = "aluminum",  Name = "Алюминий (АМг2, АМг5, Д16)",      Sigma = 250, MinVRatio = 6, RadiusFactor = 0.12, Color = "#E65100" },
                new() { Id = "brass",     Name = "Латунь (Л63, ЛС59)",              Sigma = 350, MinVRatio = 7, RadiusFactor = 0.14, Color = "#F9A825" },
                new() { Id = "copper",    Name = "Медь (М1, М2)",                   Sigma = 210, MinVRatio = 6, RadiusFactor = 0.12, Color = "#BF360C" }
            };

            var matrices = new List<BendMatrixData>
            {
                new() { MatrixWidth = 6, MinFlange = 6.0, InnerRadius = 1.0, Forces = new() {
                    { "steel",     new() { { "0.5", 2.5 }, { "0.8", 6.5 }, { "1.0", 10.2 } } },
                    { "stainless", new() { { "0.5", 3.3 } } },
                    { "aluminum",  new() { { "0.5", 1.4 }, { "0.8", 3.6 }, { "1.0", 5.7 } } },
                    { "brass",     new() { { "0.5", 2.0 }, { "0.8", 5.1 } } },
                    { "copper",    new() { { "0.5", 1.2 }, { "0.8", 3.0 }, { "1.0", 4.7 } } } } },
                new() { MatrixWidth = 8, MinFlange = 6.0, InnerRadius = 1.3, Forces = new() {
                    { "steel",     new() { { "0.5", 1.9 }, { "0.8", 4.9 }, { "1.0", 7.6 }, { "1.2", 11.0 } } },
                    { "stainless", new() { { "0.5", 2.5 }, { "0.8", 6.3 }, { "1.0", 9.8 } } },
                    { "aluminum",  new() { { "0.5", 1.1 }, { "0.8", 2.7 }, { "1.0", 4.2 }, { "1.2", 6.1 } } },
                    { "brass",     new() { { "0.5", 1.5 }, { "0.8", 3.8 }, { "1.0", 5.9 } } },
                    { "copper",    new() { { "0.5", 0.9 }, { "0.8", 2.3 }, { "1.0", 3.6 }, { "1.2", 5.1 } } } } },
                new() { MatrixWidth = 10, MinFlange = 7.0, InnerRadius = 1.6, Forces = new() {
                    { "steel",     new() { { "0.5", 1.5 }, { "0.8", 3.9 }, { "1.0", 6.1 }, { "1.2", 8.8 }, { "1.5", 13.7 } } },
                    { "stainless", new() { { "0.5", 2.0 }, { "0.8", 5.0 }, { "1.0", 7.9 }, { "1.2", 11.3 } } },
                    { "aluminum",  new() { { "0.5", 0.8 }, { "0.8", 2.2 }, { "1.0", 3.4 }, { "1.2", 4.9 }, { "1.5", 7.6 } } },
                    { "brass",     new() { { "0.5", 1.2 }, { "0.8", 3.0 }, { "1.0", 4.7 }, { "1.2", 6.8 } } },
                    { "copper",    new() { { "0.5", 0.7 }, { "0.8", 1.8 }, { "1.0", 2.8 }, { "1.2", 4.1 }, { "1.5", 6.4 } } } } },
                new() { MatrixWidth = 12, MinFlange = 8.4, InnerRadius = 1.9, Forces = new() {
                    { "steel",     new() { { "0.8", 3.3 }, { "1.0", 5.1 }, { "1.2", 7.3 }, { "1.5", 11.4 }, { "2.0", 20.3 } } },
                    { "stainless", new() { { "0.8", 4.2 }, { "1.0", 6.6 }, { "1.2", 9.4 }, { "1.5", 14.8 } } },
                    { "aluminum",  new() { { "0.8", 1.8 }, { "1.0", 2.8 }, { "1.2", 4.1 }, { "1.5", 6.4 }, { "2.0", 11.3 } } },
                    { "brass",     new() { { "0.8", 2.5 }, { "1.0", 4.0 }, { "1.2", 5.7 }, { "1.5", 8.9 } } },
                    { "copper",    new() { { "0.8", 1.5 }, { "1.0", 2.4 }, { "1.2", 3.4 }, { "1.5", 5.3 }, { "2.0", 9.5 } } } } },
                new() { MatrixWidth = 16, MinFlange = 11.2, InnerRadius = 2.6, Forces = new() {
                    { "steel",     new() { { "1.0", 3.8 }, { "1.2", 5.5 }, { "1.5", 8.6 }, { "2.0", 15.3 }, { "2.5", 23.8 } } },
                    { "stainless", new() { { "1.0", 4.9 }, { "1.2", 7.1 }, { "1.5", 11.1 }, { "2.0", 19.7 } } },
                    { "aluminum",  new() { { "1.0", 2.1 }, { "1.2", 3.0 }, { "1.5", 4.8 }, { "2.0", 8.5 }, { "2.5", 13.2 } } },
                    { "brass",     new() { { "1.0", 3.0 }, { "1.2", 4.3 }, { "1.5", 6.7 }, { "2.0", 11.9 } } },
                    { "copper",    new() { { "1.0", 1.8 }, { "1.2", 2.6 }, { "1.5", 4.0 }, { "2.0", 7.1 }, { "2.5", 11.1 } } } } },
                new() { MatrixWidth = 20, MinFlange = 14.0, InnerRadius = 3.2, Forces = new() {
                    { "steel",     new() { { "1.2", 4.4 }, { "1.5", 6.9 }, { "2.0", 12.2 }, { "2.5", 19.1 }, { "3.0", 27.5 } } },
                    { "stainless", new() { { "1.2", 5.7 }, { "1.5", 8.9 }, { "2.0", 15.7 }, { "2.5", 24.6 } } },
                    { "aluminum",  new() { { "1.2", 2.4 }, { "1.5", 3.8 }, { "2.0", 6.8 }, { "2.5", 10.6 }, { "3.0", 15.3 } } },
                    { "brass",     new() { { "1.2", 3.4 }, { "1.5", 5.3 }, { "2.0", 9.5 }, { "2.5", 14.8 } } },
                    { "copper",    new() { { "1.2", 2.1 }, { "1.5", 3.2 }, { "2.0", 5.7 }, { "2.5", 8.9 }, { "3.0", 12.8 } } } } },
                new() { MatrixWidth = 25, MinFlange = 17.5, InnerRadius = 4.0, Forces = new() {
                    { "steel",     new() { { "1.5", 5.5 }, { "2.0", 9.8 }, { "2.5", 15.3 }, { "3.0", 22.0 }, { "4.0", 39.0 } } },
                    { "stainless", new() { { "1.5", 7.1 }, { "2.0", 12.6 }, { "2.5", 19.7 }, { "3.0", 28.3 } } },
                    { "aluminum",  new() { { "1.5", 3.1 }, { "2.0", 5.4 }, { "2.5", 8.5 }, { "3.0", 12.2 }, { "4.0", 21.7 } } },
                    { "brass",     new() { { "1.5", 4.3 }, { "2.0", 7.6 }, { "2.5", 11.9 }, { "3.0", 17.1 } } },
                    { "copper",    new() { { "1.5", 2.6 }, { "2.0", 4.6 }, { "2.5", 7.1 }, { "3.0", 10.3 }, { "4.0", 18.2 } } } } },
                new() { MatrixWidth = 32, MinFlange = 22.4, InnerRadius = 5.1, Forces = new() {
                    { "steel",     new() { { "2.0", 7.6 }, { "2.5", 11.9 }, { "3.0", 17.2 }, { "4.0", 30.5 }, { "5.0", 47.7 } } },
                    { "stainless", new() { { "2.0", 9.8 }, { "2.5", 15.4 }, { "3.0", 22.1 }, { "4.0", 39.3 } } },
                    { "aluminum",  new() { { "2.0", 4.2 }, { "2.5", 6.6 }, { "3.0", 9.5 }, { "4.0", 16.9 }, { "5.0", 26.5 } } },
                    { "brass",     new() { { "2.0", 5.9 }, { "2.5", 9.3 }, { "3.0", 13.3 }, { "4.0", 23.7 } } },
                    { "copper",    new() { { "2.0", 3.6 }, { "2.5", 5.6 }, { "3.0", 8.0 }, { "4.0", 14.2 }, { "5.0", 22.3 } } } } },
                new() { MatrixWidth = 40, MinFlange = 28.0, InnerRadius = 6.4, Forces = new() {
                    { "steel",     new() { { "2.5", 9.5 }, { "3.0", 13.7 }, { "4.0", 24.4 }, { "5.0", 38.1 }, { "6.0", 54.9 } } },
                    { "stainless", new() { { "2.5", 12.3 }, { "3.0", 17.7 }, { "4.0", 31.5 }, { "5.0", 49.2 } } },
                    { "aluminum",  new() { { "2.5", 5.3 }, { "3.0", 7.6 }, { "4.0", 13.6 }, { "5.0", 21.2 }, { "6.0", 30.5 } } },
                    { "brass",     new() { { "2.5", 7.4 }, { "3.0", 10.7 }, { "4.0", 19.0 }, { "5.0", 29.7 } } },
                    { "copper",    new() { { "2.5", 4.5 }, { "3.0", 6.4 }, { "4.0", 11.4 }, { "5.0", 17.8 }, { "6.0", 25.6 } } } } },
                new() { MatrixWidth = 50, MinFlange = 35.0, InnerRadius = 8.0, Forces = new() {
                    { "steel",     new() { { "3.0", 11.0 }, { "4.0", 19.5 }, { "5.0", 30.5 }, { "6.0", 43.9 }, { "8.0", 78.1 } } },
                    { "stainless", new() { { "3.0", 14.2 }, { "4.0", 25.2 }, { "5.0", 39.3 }, { "6.0", 56.6 } } },
                    { "aluminum",  new() { { "3.0", 6.1 }, { "4.0", 10.8 }, { "5.0", 16.9 }, { "6.0", 24.4 }, { "8.0", 43.4 } } },
                    { "brass",     new() { { "3.0", 8.5 }, { "4.0", 15.2 }, { "5.0", 23.7 }, { "6.0", 34.2 } } },
                    { "copper",    new() { { "3.0", 5.1 }, { "4.0", 9.1 }, { "5.0", 14.2 }, { "6.0", 20.5 }, { "8.0", 36.5 } } } } },
                new() { MatrixWidth = 55, MinFlange = 38.5, InnerRadius = 8.8, Forces = new() {
                    { "steel",     new() { { "4.0", 17.7 }, { "5.0", 27.7 }, { "6.0", 40.0 }, { "8.0", 71.0 } } },
                    { "stainless", new() { { "4.0", 22.9 }, { "5.0", 35.8 }, { "6.0", 51.5 } } },
                    { "aluminum",  new() { { "4.0", 9.9 }, { "5.0", 15.4 }, { "6.0", 22.2 }, { "8.0", 39.4 } } },
                    { "brass",     new() { { "4.0", 13.8 }, { "5.0", 21.6 }, { "6.0", 31.1 } } },
                    { "copper",    new() { { "4.0", 8.3 }, { "5.0", 12.9 }, { "6.0", 18.6 }, { "8.0", 33.1 } } } } },
                new() { MatrixWidth = 63, MinFlange = 44.1, InnerRadius = 10.1, Forces = new() {
                    { "steel",     new() { { "4.0", 15.5 }, { "5.0", 24.2 }, { "6.0", 34.9 }, { "8.0", 62.0 }, { "10.0", 96.8 } } },
                    { "stainless", new() { { "4.0", 20.0 }, { "5.0", 31.2 }, { "6.0", 44.9 }, { "8.0", 79.9 } } },
                    { "aluminum",  new() { { "4.0", 8.6 }, { "5.0", 13.5 }, { "6.0", 19.4 }, { "8.0", 34.5 }, { "10.0", 53.8 } } },
                    { "brass",     new() { { "4.0", 12.1 }, { "5.0", 18.9 }, { "6.0", 27.2 }, { "8.0", 48.4 } } },
                    { "copper",    new() { { "4.0", 7.3 }, { "5.0", 11.3 }, { "6.0", 16.3 }, { "8.0", 29.0 }, { "10.0", 45.2 } } } } },
                new() { MatrixWidth = 70, MinFlange = 49.0, InnerRadius = 11.2, Forces = new() {
                    { "steel",     new() { { "5.0", 21.8 }, { "6.0", 31.4 }, { "8.0", 55.8 }, { "10.0", 87.2 } } },
                    { "stainless", new() { { "5.0", 28.1 }, { "6.0", 40.5 }, { "8.0", 71.9 } } },
                    { "aluminum",  new() { { "5.0", 12.1 }, { "6.0", 17.4 }, { "8.0", 31.0 }, { "10.0", 48.4 } } },
                    { "brass",     new() { { "5.0", 17.0 }, { "6.0", 24.4 }, { "8.0", 43.4 }, { "10.0", 67.8 } } },
                    { "copper",    new() { { "5.0", 10.2 }, { "6.0", 14.6 }, { "8.0", 26.0 }, { "10.0", 40.7 } } } } },
                new() { MatrixWidth = 80, MinFlange = 56.0, InnerRadius = 12.8, Forces = new() {
                    { "steel",     new() { { "5.0", 19.1 }, { "6.0", 27.5 }, { "8.0", 48.8 }, { "10.0", 76.3 } } },
                    { "stainless", new() { { "5.0", 24.6 }, { "6.0", 35.4 }, { "8.0", 62.9 }, { "10.0", 98.3 } } },
                    { "aluminum",  new() { { "5.0", 10.6 }, { "6.0", 15.3 }, { "8.0", 27.1 }, { "10.0", 42.4 } } },
                    { "brass",     new() { { "5.0", 14.8 }, { "6.0", 21.4 }, { "8.0", 38.0 }, { "10.0", 59.3 } } },
                    { "copper",    new() { { "5.0", 8.9 }, { "6.0", 12.8 }, { "8.0", 22.8 }, { "10.0", 35.6 } } } } },
                new() { MatrixWidth = 90, MinFlange = 63.0, InnerRadius = 14.4, Forces = new() {
                    { "steel",     new() { { "6.0", 24.4 }, { "8.0", 43.4 }, { "10.0", 67.8 } } },
                    { "stainless", new() { { "6.0", 31.5 }, { "8.0", 55.9 }, { "10.0", 87.4 } } },
                    { "aluminum",  new() { { "6.0", 13.6 }, { "8.0", 24.1 }, { "10.0", 37.7 } } },
                    { "brass",     new() { { "6.0", 19.0 }, { "8.0", 33.7 }, { "10.0", 52.7 } } },
                    { "copper",    new() { { "6.0", 11.4 }, { "8.0", 20.3 }, { "10.0", 31.6 } } } } },
                new() { MatrixWidth = 100, MinFlange = 70.0, InnerRadius = 16.0, Forces = new() {
                    { "steel",     new() { { "6.0", 22.0 }, { "8.0", 39.0 }, { "10.0", 61.0 } } },
                    { "stainless", new() { { "6.0", 28.3 }, { "8.0", 50.3 }, { "10.0", 78.7 } } },
                    { "aluminum",  new() { { "6.0", 12.2 }, { "8.0", 21.7 }, { "10.0", 33.9 } } },
                    { "brass",     new() { { "6.0", 17.1 }, { "8.0", 30.4 }, { "10.0", 47.5 } } },
                    { "copper",    new() { { "6.0", 10.3 }, { "8.0", 18.2 }, { "10.0", 28.5 } } } } }
            };

            return new BendTableData
            {
                Metadata = new BendTableMetadata
                {
                    Title = "Справочник гибки листового металла",
                    Description = "Параметры гибки для V-образных матриц листогибочных прессов",
                    Material = "Для всех материалов: Сталь, Нержавейка, Алюминий, Латунь, Медь",
                    UnitForce = "тонны на метр гиба",
                    Formula = "F = 1.33 × σв × S² × L / V",
                    MaxBendLength = 2550,
                    KFactor = 0.4
                },
                Materials = materials,
                Matrices = matrices
            };
        }

        #endregion
    }

    // ==================== Модели данных ====================

    public class BendMaterialData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("sigma")]
        public double Sigma { get; set; }

        [JsonPropertyName("min_v_ratio")]
        public double MinVRatio { get; set; } = 6;

        [JsonPropertyName("radius_factor")]
        public double RadiusFactor { get; set; } = 0.16;

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#000000";
    }

    public class BendTableData
    {
        [JsonPropertyName("metadata")]
        public BendTableMetadata Metadata { get; set; } = new();

        [JsonPropertyName("materials")]
        public List<BendMaterialData> Materials { get; set; } = new();

        [JsonPropertyName("matrices")]
        public List<BendMatrixData> Matrices { get; set; } = new();
    }

    public class BendTableMetadata
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("material")]
        public string Material { get; set; } = "";

        [JsonPropertyName("unit_force")]
        public string UnitForce { get; set; } = "";

        [JsonPropertyName("formula")]
        public string Formula { get; set; } = "";

        [JsonPropertyName("max_bend_length")]
        public double MaxBendLength { get; set; } = 2550;

        [JsonPropertyName("k_factor")]
        public double KFactor { get; set; } = 0.4;
    }

    public class BendMatrixData
    {
        [JsonPropertyName("matrix_width")]
        public double MatrixWidth { get; set; }

        [JsonPropertyName("min_flange")]
        public double MinFlange { get; set; }

        [JsonPropertyName("inner_radius")]
        public double InnerRadius { get; set; }

        /// <summary>
        /// Усилия по материалам: ключ — id материала ("steel", "stainless", ...),
        /// значение — словарь "толщина" → усилие (тонн/метр).
        /// </summary>
        [JsonPropertyName("forces")]
        public Dictionary<string, Dictionary<string, double>> Forces { get; set; } = new();
    }
}