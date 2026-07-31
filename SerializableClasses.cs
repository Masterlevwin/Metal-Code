using Metal_Code.Models;
using Metal_Code.Utils;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        public List<Part> Baskets = new();

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

        [OptionalField]
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

        /// <summary>
        /// Вызывать после изменения Count для обновления привязанного Total в UI
        /// </summary>
        public void NotifyTotalChanged()
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(Total));
        }

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
        public ObservableCollection<PlacedHole> _placedHoles = new();
        public ObservableCollection<PlacedHole> PlacedHoles
        {
            get => _placedHoles;
            set => _placedHoles = value;
        }

        [OptionalField]
        public ObservableCollection<HoleGroup> _holeGroups = new();
        public ObservableCollection<HoleGroup> HoleGroups
        {
            get => _holeGroups;
            set => _holeGroups = value;
        }

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

    public class PlacedHole
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Diameter { get; set; }
    }

    public enum PartType
    {
        Unknown,          // Не определено
        Rectangle,        // Прямоугольная листовая деталь
        Round,            // Круглая листовая деталь или круг (пруток)
        RectangularTube,  // Прямоугольная труба
        RoundTube,        // Круглая труба
        SquareBar,        // Квадратный пруток 
        Angle,            // Уголок
        Channel,          // Швеллер
        IBeam,            // Двутавр
        Triangle,         // Треугольник
        Custom            // Произвольная форма
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

        [OptionalField]
        public ObservableCollection<Part> Baskets = new();

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

        [OptionalField]
        public bool IsGrooved = false;

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

    // ==================== SQLiteContext ====================
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
            EnsureMachineNameColumnExists();
            EnsurePendingSyncColumnExists();

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

            modelBuilder.Entity<Manager>(entity =>
            {
                entity.Property(m => m.MachineName)
                      .HasColumnName("machine_name")
                      .HasColumnType("TEXT")
                      .IsRequired(false); // Разрешаем NULL для старых пользователей
            });

            modelBuilder.Entity<Offer>()
                .Property(o => o.IsPendingSync)
                .HasColumnName("IsPendingSync")
                .HasDefaultValue(true);

            modelBuilder.Entity<Customer>().Ignore(c => c.SpecTemplateJson);

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

        private void EnsureMachineNameColumnExists()
        {
            try
            {
                // Просто пытаемся добавить колонку. Если она уже есть, SQLite выдаст ошибку, 
                // которую мы перехватим и проигнорируем.
                Database.ExecuteSqlRaw(@"ALTER TABLE Managers ADD COLUMN machine_name TEXT;");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                // Код ошибки 1 обычно означает "SQL logic error", что бывает при попытке добавить существующую колонку.
                // Игнорируем, так как наша цель достигнута.
                if (ex.SqliteErrorCode != 1)
                    System.Diagnostics.Trace.WriteLine($"Unexpected SQLite error: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Игнорируем любые другие ошибки при миграции схемы, чтобы не ломать старт приложения
                System.Diagnostics.Trace.WriteLine($"Warning: Could not add machine_name column: {ex.Message}");
            }
        }

        private void EnsurePendingSyncColumnExists()
        {
            try
            {
                Database.ExecuteSqlRaw(@"ALTER TABLE Offers ADD COLUMN IsPendingSync INTEGER NOT NULL DEFAULT 1;");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                if (ex.SqliteErrorCode != 1)
                    System.Diagnostics.Trace.WriteLine($"Unexpected SQLite error: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Warning: Could not add IsPendingSync column: {ex.Message}");
            }
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


    // Вспомогательный класс для чтения PRAGMA
    public class ColumnInfo
    {
        public int cid { get; set; }
        public string name { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public int notnull { get; set; }
        public string dflt_value { get; set; } = string.Empty;
        public int pk { get; set; }
    }

    // ==================== AppDbContext ====================
    public class AppDbContext : DbContext
    {
        public DbSet<Offer> Offers { get; set; } = null!;
        public DbSet<Manager> Managers { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Metal> Metals { get; set; } = null!;
        public DbSet<TypeDetail> TypeDetails { get; set; } = null!;
        public DbSet<Work> Works { get; set; } = null!;
        public DbSet<RequestTemplate> RequestTemplates { get; set; } = null!;
        public DbSet<UpdateItem> UpdateItems { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==========================================
            // 1. TYPE DETAILS (Типы деталей)
            // ==========================================
            modelBuilder.Entity<TypeDetail>(entity =>
            {
                entity.ToTable("type_details");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Price).HasColumnName("price").HasColumnType("real");
                entity.Property(e => e.Sort).HasColumnName("sort");
            });

            // ==========================================
            // 2. WORKS (Работы)
            // ==========================================
            modelBuilder.Entity<Work>(entity =>
            {
                entity.ToTable("works");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired();
                entity.Property(e => e.Price).HasColumnName("price").HasColumnType("real");
                entity.Property(e => e.Time).HasColumnName("time").HasColumnType("real");
            });

            // ==========================================
            // 3. METALS (Материалы)
            // ==========================================
            modelBuilder.Entity<Metal>(entity =>
            {
                entity.ToTable("metals");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Density).HasColumnName("density").HasColumnType("real");
                entity.Property(e => e.MassPrice).HasColumnName("mass_price").HasColumnType("real");
                entity.Property(e => e.WayPrice).HasColumnName("way_price");
                entity.Property(e => e.PinholePrice).HasColumnName("pinhole_price");
                entity.Property(e => e.MoldPrice).HasColumnName("mold_price");

                // Уникальный индекс на имя металла
                entity.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ix_metals_name_unique");
            });

            // ==========================================
            // 4. MANAGERS (Менеджеры)
            // ==========================================
            modelBuilder.Entity<Manager>(entity =>
            {
                entity.ToTable("managers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired();
                entity.Property(e => e.MachineName).HasColumnName("machine_name");
                entity.Property(e => e.Contact).HasColumnName("contact");
                entity.Property(e => e.Password).HasColumnName("password");
                entity.Property(e => e.IsAdmin).HasColumnName("is_admin");
                entity.Property(e => e.IsEngineer).HasColumnName("is_engineer");
                entity.Property(e => e.IsLaser).HasColumnName("is_laser");

                // ❌ УДАЛЕНО: entity.HasMany(m => m.Offers)...
                // ❌ УДАЛЕНО: entity.HasMany(m => m.Customers)...
                // Так как эти свойства помечены [NotMapped] в классе Manager, 
                // настраивать связь нужно только с дочерней стороны (Offer/Customer).
            });

            // === CUSTOMERS ===
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("customers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Address).HasColumnName("address");
                entity.Property(e => e.Agent).HasColumnName("agent");
                entity.Property(e => e.DeliveryPrice).HasColumnName("delivery_price");

                // Явно указываем имя колонки для внешнего ключа
                entity.Property(e => e.ManagerId).HasColumnName("manager_id");

                entity.Property(e => e.SpecTemplateJson).HasColumnName("spec_template").HasColumnType("jsonb");
                entity.Ignore(e => e.SpecTemplate);

                // ✅ ИСПРАВЛЕНО: WithOne -> WithMany() (без параметров)
                entity.HasOne(c => c.Manager)
                      .WithMany() // Пусто! Это означает, что у Manager нет навигационного свойства Customers
                      .HasForeignKey(c => c.ManagerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.Name).HasDatabaseName("ix_customers_name");
            });

            // === OFFERS ===
            modelBuilder.Entity<Offer>(entity =>
            {
                entity.ToTable("offers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();

                entity.Property(e => e.N).HasColumnName("n");
                entity.Property(e => e.Company).HasColumnName("company");
                entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("real");
                entity.Property(e => e.Material).HasColumnName("material").HasColumnType("real");
                entity.Property(e => e.Services).HasColumnName("services").HasColumnType("real");
                entity.Property(e => e.Act).HasColumnName("act");
                entity.Property(e => e.Autor).HasColumnName("autor");
                entity.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb");
                entity.Property(e => e.CreatedDate).HasColumnName("created_date").HasColumnType("timestamp with time zone");
                entity.Property(e => e.EndDate).HasColumnName("end_date").HasColumnType("timestamp with time zone");
                entity.Property(e => e.Agent).HasColumnName("agent");
                entity.Property(e => e.Invoice).HasColumnName("invoice");
                entity.Property(e => e.Order).HasColumnName("order");

                // Явно указываем имя колонки для внешнего ключа
                entity.Property(e => e.ManagerId).HasColumnName("manager_id");

                entity.Ignore(e => e.IsPendingSync);
                entity.Ignore(e => e.ParentQuoteNumber);

                // ✅ ИСПРАВЛЕНО: WithOne -> WithMany() (без параметров)
                entity.HasOne(o => o.Manager)
                      .WithMany() // Пусто! Это означает, что у Manager нет навигационного свойства Offers
                      .HasForeignKey(o => o.ManagerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.N).HasDatabaseName("ix_offers_n");
                entity.HasIndex(e => e.CreatedDate).HasDatabaseName("ix_offers_created_date");
            });

            // ==========================================
            // 7. REQUEST TEMPLATES (Шаблоны заявок)
            // ==========================================
            modelBuilder.Entity<RequestTemplate>(entity =>
            {
                entity.ToTable("request_templates");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.DestinyPattern).HasColumnName("destiny_pattern");
                entity.Property(e => e.CountPattern).HasColumnName("count_pattern");
                entity.Property(e => e.PosDestiny).HasColumnName("pos_destiny");
                entity.Property(e => e.PosCount).HasColumnName("pos_count");

                entity.HasIndex(e => e.Name).HasDatabaseName("ix_request_templates_name");
            });

            // ==========================================
            // 8. UPDATE ITEMS (Обновления)
            // ==========================================
            modelBuilder.Entity<UpdateItem>(entity =>
            {
                entity.ToTable("update_items");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.VersionTitle).HasColumnName("version_title");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.ScreenshotPath).HasColumnName("screenshot_path");
                entity.Property(e => e.ReleaseDate).HasColumnName("release_date").HasColumnType("timestamp with time zone");
                entity.Property(e => e.IsShownAtStartup).HasColumnName("is_shown_at_startup");
            });
        }
    }
}