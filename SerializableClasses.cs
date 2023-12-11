using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using static OfficeOpenXml.ExcelErrorValue;

namespace Metal_Code
{
    [Serializable]
    public class Product
    {
        public string? Name, Order, Company, Production, Manager, Comment, PaintRatio, ConstructRatio;
        public int Count, Delivery;
        public bool IsLaser, HasDelivery, HasPaint, HasConstruct;
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

        public List<SaveWork> Works = new();
        public SaveTypeDetail(int _index = 0, int _count = 0, int _metal = 0, bool _hasMetal = true, (int, float, float, float, float) _tuple = default)
        {
            Index = _index;
            Count = _count;
            Metal = _metal;
            HasMetal = _hasMetal;
            Tuple = _tuple;
        }
    }
    
    [Serializable]
    public class SaveWork
    {
        public string? NameWork { get; set; }
        public float Ratio { get; set; }

        public List<string>? PropsList = new();

        public List<LaserItem> Items = new();
        public List<Part> Parts = new();
        public SaveWork(string? _namework, float _ratio = 0)
        {
            NameWork = _namework;
            Ratio = _ratio;
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
        public ObservableCollection<Offer> Offers { get; set; } = new();
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
        public DateTime? CreatedDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Invoice { get; set; }
        public string? Order { get; set; }
        public string? Act { get; set; }

        [Browsable(false)]
        public string? Path { get; set; }
        [Browsable(false)]
        public int ManagerId { get; set; }
        [Browsable(false)]
        public Manager? Manager { get; set; }
        public Offer(string? n = null, string? company = null, float amount = 0)
        {
            N = n;
            Company = company;
            Amount = amount;
            CreatedDate = DateTime.Now;
        }
    }

    public class Metal
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public float Density {  get; set; }
        public float MassPrice {  get; set; }
        public string? WayPrice { get; set; }
        public string? PinholePrice { get; set; }
        public string? MoldPrice { get; set; }

        public Metal()
        {

        }
    }

    public class TypeDetailContext : DbContext
    {
        public DbSet<TypeDetail> TypeDetails { get; set; } = null!;
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=typedetails.db");
        }
    }

    public class WorkContext : DbContext
    {
        public DbSet<Work> Works { get; set; } = null!;
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=works.db");
        }
    }

    public class ManagerContext : DbContext
    {
        public DbSet<Manager> Managers { get; set; } = null!;
        public DbSet<Offer> Offers { get; set; } = null!;
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=managers.db");
        }
    }
    
    public class MetalContext : DbContext
    {
        public DbSet<Metal> Metals { get; set; } = null!;
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=metals.db");
        }
    }
}