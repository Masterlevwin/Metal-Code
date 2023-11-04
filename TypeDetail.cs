using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Metal_Code
{
    [Serializable]
    public class Product
    {
        public string? Name, Order, Company, Production, Manager, Comment;
        public int Count, Delivery;
        public bool HasDelivery;
        public ObservableCollection<Detail> Details { get; set; } = new();
        public Product()
        {

        }
    }

    [Serializable]
    public class Detail
    {
        public int N { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int Count { get; set; }
        public float Price { get; set; }
        public float Total { get; set; }

        public List<SaveTypeDetail> TypeDetails = new();
        public Detail(int id = 0, string? _name = "", int _count = 0, float _price = 0, float _total = 0)
        {
            N = id;
            Title = _name;
            Count = _count;
            Price = _price;
            Total = _total;
        }
    }
    
    [Serializable]
    public class Part
    {
        public string? Name { get; set; }
        public int Count { get; set; }
        public float Way { get; set; }
        public float Mass { get; set; }
        public float Price { get; set; }

        public Part(string? _name = "", int _count = 0, float _way = 0, float _mass = 0, float _price = 0)
        {
            Name = _name;
            Count = _count;
            Way = _way;
            Mass = _mass;
            Price = _price;
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
        public int Index { get; set; }

        public List<string>? PropsList = new();
        public SaveWork(int _index = 0)
        {
            Index = _index;
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
        public Manager()
        {

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