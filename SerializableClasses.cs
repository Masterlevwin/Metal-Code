using Metal_Code.Utils;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Media;

namespace Metal_Code
{
    [Serializable]
    public class Product
    {
        public string? Name, Order, Company, Production, Manager, PaintRatio, ConstructRatio;   //поле "Manager" сохраняет ссылку на адрес доставки
        public int Count, Delivery, DeliveryRatio;
        public bool IsLaser, IsAgent;
        public bool? HasConstruct, HasPaint, HasDelivery;

        [OptionalField]             //атрибут, который позволяет игнорировать это поле при загрузке старых сохранений
        public double Ratio = 1, MaterialFactor = 1, ServiceFactor = 1;

        [OptionalField]
        public bool IsExpressOffer = false;     //это поле сохраняет ссылку на предварительный расчет

        [OptionalField]
        public bool HasAssembly = false;        //это поле сохраняет ссылку на экспресс-изготовление

        [OptionalField]
        public string Comment = "";

        [OptionalField]
        public ObservableCollection<Assembly> Assemblies = new();

        [OptionalField]
        public float BonusRatio;

        [OptionalField]
        public List<Basket> Baskets = new();

        public ObservableCollection<Detail> Details { get; set; } = new();
        public Product() { }
    }

    [Serializable]
    public class Detail
    {
        public string? Metal {  get; set; }
        public string Destiny { get; set; } = string.Empty;
        public string? Accuracy { get; set; }
        public string? Description { get; set; }
        public string? Title { get; set; }
        public int Count { get; set; }
        public float Price { get; set; }
        public float Total { get; set; }

        [Browsable(false)]
        public float Mass { get; set; }
        [Browsable(false)]
        public bool IsComplect { get; set; } = false;

        [OptionalField]
        public ObservableCollection<MillingHole> MillingHoles = new();

        [OptionalField]
        public ObservableCollection<MillingGroove> MillingGrooves = new();

        public List<SaveTypeDetail> TypeDetails = new();
        public Detail(string? _name = null, int _count = 1, string? _accuracy = null)
        {
            Title = _name;
            Count = _count;
            Accuracy = _accuracy;
        }
    }

    [Serializable]
    public class Part : INotifyPropertyChanged
    {
        [field: NonSerialized]
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public string? Metal { get; set; }
        public float Destiny { get; set; }
        public string? Accuracy { get; set; }
        public string? Description { get; set; }
        public string? Title { get; set; }
        public int Count { get; set; }

        [field: NonSerialized]
        private float price;
        public float Price
        {
            get => price;
            set
            {
                if (Math.Abs(price - value) > 1e-6)
                {
                    price = value;
                    OnPropertyChanged(nameof(Price));
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        public float Total => Price * Count;

        [Browsable(false)]
        public float Mass { get; set; }
        [Browsable(false)]
        public float Way { get; set; }

        [OptionalField]
        public byte[]? ImageBytes;

        [OptionalField]
        public string? PathToScan;

        [OptionalField]
        public ObservableCollection<MillingHole> MillingHoles = new();

        [OptionalField]
        public ObservableCollection<MillingGroove> MillingGrooves = new();

        [OptionalField]
        [field: NonSerialized]
        public ObservableCollection<IGeometryDescriptor> Geometries = new();

        [OptionalField]
        private string? _displayGeometryXaml;
        [Browsable(false)]
        public PathGeometry? DisplayGeometry
        {
            get => GeometryHelper.FromXamlString(_displayGeometryXaml);
            set
            {
                var newXaml = GeometryHelper.ToXamlString(value);
                if (_displayGeometryXaml != newXaml)
                {
                    _displayGeometryXaml = newXaml;
                    OnPropertyChanged(nameof(DisplayGeometry));
                }
            }
        }

        [Browsable(false)]
        public double VisualStrokeThickness
        {
            get
            {
                if (DisplayGeometry?.Bounds is Rect bounds && !bounds.IsEmpty)
                {
                    double sourceSize = Math.Max(bounds.Width, bounds.Height);
                    if (sourceSize > 0)
                    {
                        // Viewbox растягивает до 60x60 (с учётом Uniform — меньшая сторона = 60)
                        // Но для оценки масштаба используем max, т.к. Stretch="Uniform"
                        double targetSize = 60.0;
                        double scale = targetSize / sourceSize;
                        // Чтобы визуальная толщина была ≈1, задаём:
                        double stroke = 1.0 / scale;
                        // Ограничиваем разумные пределы (на случай очень мелких или огромных геометрий)
                        return Math.Max(1.0, Math.Min(5.0, stroke));
                    }
                }
                // Если геометрия недоступна — используем 1 по умолчанию
                return 1.0;
            }
        }

        [OptionalField]
        public bool IsFixed = false;

        [OptionalField]
        public float FixedPrice = 0;

        [OptionalField]
        private bool _isHiddenInOffer;
        [Browsable(false)]
        public bool IsHiddenInOffer
        {
            get => _isHiddenInOffer;
            set
            {
                if (_isHiddenInOffer != value)
                {
                    _isHiddenInOffer = value;
                    OnPropertyChanged();
                }
            }
        }

        [OptionalField]
        private PartType _partType;
        [Browsable(false)]
        public PartType PartType
        {
            get => _partType;
            set => _partType = value;
        }

        [OptionalField]
        private double _width;
        [Browsable(false)]
        public double Width
        {
            get => _width;
            set
            {
                _width = value;
                OnPropertyChanged();
            }
        }

        [OptionalField]
        private double _height;
        [Browsable(false)]
        public double Height
        {
            get => _height;
            set
            {
                _height = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Длина трубы в мм (для трубных деталей)
        /// Для листовых деталей не используется
        /// </summary>
        [OptionalField]
        private double _length;
        [Browsable(false)]
        public double Length
        {
            get => _length;
            set
            {
                _length = value;
                OnPropertyChanged();
            }
        }

        [OptionalField]
        public List<HoleGroup> HoleGroups = new();

        public Dictionary<int, List<string>> PropsDict = new();

        [OptionalField]
        public Dictionary<Guid, List<string>> WorksDict = new();

        public Part(string? _name = null, int _count = 1, string? _accuracy = null)
        {
            Title = _name;
            Count = _count;
            Accuracy = _accuracy;
        }
    }


    public class Hole
    {
        public double Diameter { get; set; }

        public Hole(double diameter)
        {
            Diameter = diameter;
        }
    }

    [Serializable]
    public class HoleGroup
    {
        private int _count;
        private double _diameter;

        public double Diameter
        {
            get => _diameter;
            set
            {
                _diameter = Math.Max(1, Math.Min(100, value)); // Ограничение: 1-100мм
            }
        }

        public int Count
        {
            get => _count;
            set
            {
                _count = Math.Max(1, Math.Min(100, value)); // Ограничение: 1-100 шт
            }
        }

        /// <summary>
        /// Общая площадь всех отверстий в группе
        /// </summary>
        public double TotalArea => Count * Math.PI * Math.Pow(Diameter / 2, 2);

        public HoleGroup(double diameter = 10, int count = 1)
        {
            Diameter = diameter;
            Count = count;
        }
    }

    public enum PartType
    {
        Rectangle,        // Прямоугольная листовая деталь
        Round,            // Круглая листовая деталь или круг (пруток)
        RectangularTube,  // Прямоугольная труба
        RoundTube         // Круглая труба
    }

    [Serializable]
    public class Particle : Part
    {
        private int count;
        public new int Count
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

        public Particle() { }
    }

    [Serializable]
    public class Assembly : INotifyPropertyChanged
    {
        [field: NonSerialized]
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        
        private string title = "Новая сборка";
        public string Title
        {
            get => title;
            set
            {
                if (value != title)
                {
                    title = value;
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        private int count = 1;
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

        public float Price { get; set; } = 0;
        public float Total { get; set; } = 0;
        public ObservableCollection<Particle> Particles { get; set; } = new();

        [OptionalField]
        private string description = string.Empty;
        public string Description
        {
            get => description;
            set
            {
                if (value != description)
                {
                    description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        [OptionalField]
        private string weld = string.Empty;
        public string Weld
        {
            get => weld;
            set
            {
                if (value != weld)
                {
                    weld = value;
                    OnPropertyChanged(nameof(Weld));
                    AssemblyWindow.A.Set_WorksPrice();
                }
            }
        }

        [OptionalField]
        private string type = "одн";
        public string Type
        {
            get => type;
            set
            {
                if (value != type)
                {
                    type = value;
                    OnPropertyChanged(nameof(Type));
                    AssemblyWindow.A.Set_WorksPrice();
                }
            }
        }

        [OptionalField]
        private string ral = string.Empty;
        public string Ral
        {
            get => ral;
            set
            {
                if (value != ral)
                {
                    ral = value;
                    OnPropertyChanged(nameof(Ral));
                    AssemblyWindow.A.Set_WorksPrice();
                }
            }
        }

        [OptionalField]
        private string structure = "глян";
        public string Structure
        {
            get => structure;
            set
            {
                if (value != structure)
                {
                    structure = value;
                    OnPropertyChanged(nameof(Structure));
                }
            }
        }

        [OptionalField]
        private float square = 0;
        public float Square
        {
            get => (float)Math.Round(square, 2);
            set
            {
                if (value != square)
                {
                    square = value;
                    OnPropertyChanged(nameof(Square));
                }
            }
        }
        
        [OptionalField]
        private float mass = 0;
        public float Mass
        {
            get => (float)Math.Round(mass, 2);
            set
            {
                if (value != mass)
                {
                    mass = value;
                    OnPropertyChanged(nameof(Mass));
                }
            }
        }

        [OptionalField]
        private float weldPrice = 0;
        public float WeldPrice
        {
            get => weldPrice;
            set
            {
                if (value != weldPrice)
                {
                    weldPrice = value;
                    OnPropertyChanged(nameof(WeldPrice));
                }
            }
        }

        [OptionalField]
        private float paintPrice = 0;
        public float PaintPrice
        {
            get => paintPrice;
            set
            {
                if (value != paintPrice)
                {
                    paintPrice = value;
                    OnPropertyChanged(nameof(PaintPrice));
                }
            }
        }

        public Assembly() { }
    }

    [Serializable]
    public class SaveTypeDetail
    {
        public int Index { get; set; }
        public int Count { get; set; }
        public int Metal { get; set; }
        public bool HasMetal { get; set; }
        public (int, float, float, float, float) Tuple { get; set; }
        public float ExtraResult { get; set; }

        [OptionalField]
        public string? Comment;

        public List<SaveWork> Works = new();
        public SaveTypeDetail(int _index = 0, int _count = 0, int _metal = 0, bool _hasMetal = true, (int, float, float, float, float) _tuple = default, float _extraResult = 0, string? _comment = null)
        {
            Index = _index;
            Count = _count;
            Metal = _metal;
            HasMetal = _hasMetal;
            Tuple = _tuple;
            ExtraResult = _extraResult;
            Comment = _comment;
        }
    }
    
    [Serializable]
    public class SaveWork
    {
        public string? NameWork { get; set; }
        public float Ratio { get; set; }

        [OptionalField]
        public float TechRatio = 1;

        [OptionalField]
        public float ExtraResult = 0;

        public List<string>? PropsList = new();

        public List<LaserItem>? Items = new();
        public List<Part> Parts = new();
        public SaveWork(string? _namework, float _ratio = 0, float _techratio = 1)
        {
            NameWork = _namework;
            Ratio = _ratio;
            TechRatio = _techratio;
        }
    }

    public class TypeDetail
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public float Price { get; set; }
        public string? Sort { get; set; }
        public TypeDetail()
        {

        }
    }

    public class Work
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public float Price { get; set; }
        public float Time { get; set; }
        public Work()
        {

        }
    }

    public class Manager
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Contact {  get; set; }
        public string? Password {  get; set; }
        public bool IsAdmin {  get; set; }
        public bool IsEngineer {  get; set; }
        public bool IsLaser{  get; set; }
        public ObservableCollection<Offer> Offers { get; set; } = new();
        public ObservableCollection<Customer> Customers { get; set; } = new();
        public Manager()
        {

        }
    }

    public class Offer
    {
        [Browsable(false)]
        public int Id { get; set; }

        public string? N { get; set; }
        public string? Company { get; set; }
        public float Amount { get; set; }
        public float Material { get; set; }

        [ConcurrencyCheck]
        public bool Agent { get; set; }
        [ConcurrencyCheck]
        public string? Invoice { get; set; }
        [ConcurrencyCheck]
        public DateTime? CreatedDate { get; set; }
        [ConcurrencyCheck]
        public string? Order { get; set; }

        public string? Autor { get; set; }
        public DateTime? EndDate { get; set; }

        [Browsable(false)]
        public float Services { get; set; }
        [Browsable(false)]
        public string? Act { get; set; }        //путь к сохраненному КП
        [Browsable(false)]
        public Manager? Manager { get; set; }
        [Browsable(false)]
        public int ManagerId { get; set; }
        [Browsable(false)]
        public string? Data { get; set; }

        public Offer(string? n = null, string? company = null, float amount = 0, float material = 0, float services = 0)
        {
            N = n;
            Company = company;
            Amount = amount;
            Material = material;
            Services = services;
            CreatedDate = DateTime.UtcNow;
        }
    }

    public class Customer
    {
        public int Id { get; set; }

        public string? Name { get; set; }
        public string? Address { get; set; }
        public bool Agent { get; set; }
        public int DeliveryPrice { get; set; }
        public Manager? Manager { get; set; }
        public int ManagerId { get; set; }

        public SpecTemplate SpecTemplate { get; set; } = new();

        public Customer()
        {

        }
    }

    public class Metal
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public float Density {  get; set; }
        public float MassPrice {  get; set; }
        [Browsable(false)]
        public string? WayPrice { get; set; }
        [Browsable(false)]
        public string? PinholePrice { get; set; }
        [Browsable(false)]
        public string? MoldPrice { get; set; }

        public Metal()
        {

        }
    }

    public class TypeDetailContext : DbContext
    {
        public DbSet<TypeDetail> TypeDetails { get; set; } = null!;

        public string connectionString;
        public TypeDetailContext(string connectionString)
        {
            this.connectionString = connectionString;   // получаем извне строку подключения
            Database.EnsureCreated();                   // гарантируем, что база данных создана
            Database.SetCommandTimeout(9000);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(connectionString);
        }
    }

    public class WorkContext : DbContext
    {
        public DbSet<Work> Works { get; set; } = null!;

        public string connectionString;
        public WorkContext(string connectionString)
        {
            this.connectionString = connectionString;   // получаем извне строку подключения
            Database.EnsureCreated();                   // гарантируем, что база данных создана
            Database.SetCommandTimeout(9000);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(connectionString);
        }
    }

    public class ManagerContext : DbContext
    {
        public DbSet<Manager> Managers { get; set; } = null!;
        public DbSet<Offer> Offers { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;

        public string ConnectionString { get; }

        public ManagerContext(string connectionString)
        {
            ConnectionString = connectionString;
            Database.EnsureCreated();                   // гарантируем, что база данных создана
            Database.SetCommandTimeout(9000);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(ConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Customer>()
                .OwnsOne(c => c.SpecTemplate, builder =>
                {
                    builder.ToTable("Customers");
                    builder.Property(st => st.Header).HasColumnName("SpecTemplate_Header").HasMaxLength(500);
                    builder.Property(st => st.Number).HasColumnName("SpecTemplate_Number");
                    builder.Property(st => st.Provider).HasColumnName("SpecTemplate_Provider").HasMaxLength(200);
                    builder.Property(st => st.Buyer).HasColumnName("SpecTemplate_Buyer").HasMaxLength(200);
                });
        }
    }

    public class MetalContext : DbContext
    {
        public DbSet<Metal> Metals { get; set; } = null!;

        public string connectionString;
        public MetalContext(string connectionString)
        {
            this.connectionString = connectionString;   // получаем извне строку подключения
            Database.EnsureCreated();                   // гарантируем, что база данных создана
            Database.SetCommandTimeout(9000);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(connectionString);
        }
    }

    public class RequestContext : DbContext
    {
        public DbSet<RequestTemplate> Templates { get; set; } = null!;
        public DbSet<UpdateItem> UpdateItems { get; set; } = null!;

        public string connectionString;
        public RequestContext(string connectionString)
        {
            this.connectionString = connectionString;   // получаем извне строку подключения
            Database.EnsureCreated();                   // гарантируем, что база данных создана
            Database.SetCommandTimeout(9000);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(connectionString);
        }

        public void EnsureUpdateTableExists()
        {
            var createTableSql = @"
        CREATE TABLE IF NOT EXISTS UpdateItems (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            VersionTitle TEXT,
            Description TEXT,
            ScreenshotPath TEXT,
            ReleaseDate TEXT NOT NULL,
            IsShownAtStartup INTEGER NOT NULL
        );";

            Database.ExecuteSqlRaw(createTableSql);
        }

        public void EnsureUpdateHistoryInitialized()
        {
            // Если уже есть какие-то записи — ничего не делаем (идемпотентность)
            if (UpdateItems.Any())
                return;

            // Заполняем начальными обновлениями
            var initialUpdates = new List<UpdateItem>
            {
                new() {
                    VersionTitle = "v1.0.0.0",
                    ReleaseDate = new DateTime(2023, 08, 03),
                    Description = "Первый релиз. Добавлена основная функциональность.",
                    ScreenshotPath = null,
                    IsShownAtStartup = false // ← пользователь уже "видел" старые версии
                },
                new() {
                    VersionTitle = "v2.5.0.0",
                    ReleaseDate = new DateTime(2024, 12, 08),
                    Description = "Добавлено руководство пользователя. Добавлен режим заявки.",
                    ScreenshotPath = "/Images/example0.png",
                    IsShownAtStartup = false
                },
            };

            UpdateItems.AddRange(initialUpdates);
            SaveChanges();
        }

        public void AddNewUpdateIfNotExists(string version, DateTime releaseDate, string description, string? screenshotPath = null)
        {
            // Проверяем, существует ли уже обновление с такой версией
            var exists = UpdateItems.Any(u => u.VersionTitle == version);
            if (exists)
                return;

            var newItem = new UpdateItem
            {
                VersionTitle = version,
                ReleaseDate = releaseDate,
                Description = description,
                ScreenshotPath = screenshotPath,
                IsShownAtStartup = true // ← важно! чтобы показать при запуске
            };

            UpdateItems.Add(newItem);
            SaveChanges();
        }

        public List<UpdateItem> GetNewStartupUpdates()
        {
            return UpdateItems
                .Where(u => u.IsShownAtStartup)
                .OrderByDescending(u => u.ReleaseDate)
                .ToList();
        }

        public void MarkStartupUpdatesAsSeen()
        {
            var unseen = UpdateItems.Where(u => u.IsShownAtStartup).ToList();
            foreach (var item in unseen)
            {
                item.IsShownAtStartup = false;
            }
            SaveChanges();
        }
    }

    public class BaseContext : DbContext
    {
        public DbSet<Manager> Managers { get; set; } = null!;
        public DbSet<Offer> Offers { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<TypeDetail> TypeDetails { get; set; } = null!;
        public DbSet<Work> Works { get; set; } = null!;
        public DbSet<Metal> Metals { get; set; } = null!;

        public string connectionString;

        public BaseContext(string connectionString)
        {
            this.connectionString = connectionString;   // получаем извне строку подключения
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
    }
}