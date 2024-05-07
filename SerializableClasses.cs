using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Metal_Code
{
    [Serializable]
    public class Product
    {
        public string? Name, Order, Company, Production, Manager, PaintRatio, ConstructRatio;       //поле "Manager" сохраняет ссылку на адрес доставки
        public int Count, Delivery, DeliveryRatio;
        public bool IsLaser, IsAgent, HasDelivery;
        public bool? HasConstruct, HasPaint;

        [OptionalField]             //атрибут, который позволяет игнорировать это поле при загрузке старых сохранений
        public float Ratio = 1;

        [OptionalField]
        public bool HasAssembly = false;

        public ObservableCollection<Detail> Details { get; set; } = new();
        public Product()
        {

        }
    }

    [Serializable]
    public class Detail
    {
        public string? Metal {  get; set; }
        public string? Destiny { get; set; }
        public string? Description { get; set; }
        public string? Accuracy { get; set; }
        public string? Title { get; set; }
        public int Count { get; set; }
        public float Price { get; set; }
        public float Total { get; set; }

        [Browsable(false)]
        public float Mass { get; set; }
        [Browsable(false)]
        public bool IsComplect { get; set; } = false;

        public List<SaveTypeDetail> TypeDetails = new();
        public Detail(string? _name = null, int _count = 1, string? _accuracy = null)
        {
            Title = _name;
            Count = _count;
            Accuracy = _accuracy;
        }
    }

    [Serializable]
    public class Part
    {
        public string? Metal { get; set; }
        public float Destiny { get; set; }
        public string? Description { get; set; }
        public string? Accuracy { get; set; }
        public string? Title { get; set; }
        public int Count { get; set; }
        public float Price { get; set; }
        public float Total { get; set; }

        [Browsable(false)]
        public float Mass { get; set; }
        [Browsable(false)]
        public float Way { get; set; }

        [OptionalField]
        public byte[]? ImageBytes;

        [OptionalField]
        public bool MakeModel;

        public Dictionary<int, List<string>> PropsDict = new();

        public Part(string? _name = null, int _count = 1, string? _accuracy = null)
        {
            Title = _name;
            Count = _count;
            Accuracy = _accuracy;
        }
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

        public List<SaveWork> Works = new();
        public SaveTypeDetail(int _index = 0, int _count = 0, int _metal = 0, bool _hasMetal = true, (int, float, float, float, float) _tuple = default, float _extraResult = 0)
        {
            Index = _index;
            Count = _count;
            Metal = _metal;
            HasMetal = _hasMetal;
            Tuple = _tuple;
            ExtraResult = _extraResult;
        }
    }
    
    [Serializable]
    public class SaveWork
    {
        public string? NameWork { get; set; }
        public float Ratio { get; set; }

        [OptionalField]
        public float TechRatio = 1;

        public List<string>? PropsList = new();

        public List<LaserItem> Items = new();
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
        public string? Act { get; set; }
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
            CreatedDate = DateTime.Now;
        }
    }

    public class Customer
    {
        public int Id { get; set; }

        public string? Name { get; set; }
        public string? Adress { get; set; }
        public bool Agent { get; set; }

        public Manager? Manager { get; set; }
        public int ManagerId { get; set; }

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

        public string connectionString;
        public ManagerContext(string connectionString)
        {
            this.connectionString = connectionString;   // получаем извне строку подключения
            //Database.EnsureCreated();                   // гарантируем, что база данных создана
            Database.Migrate();                         //вместо создания базы используем миграции
            Database.SetCommandTimeout(9000);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(connectionString);
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
}