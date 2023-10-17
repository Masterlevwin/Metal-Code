﻿using Microsoft.EntityFrameworkCore;
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
        public int Count { get; set; }
        public float Price { get; set; }
        public float Total { get; set; }
        public string? Description { get; set; }

        public List<SaveTypeDetail> TypeDetails = new();
        public Detail(int id = 0, string? _name = "", int _count = 0, float _price = 0, float _total = 0, string? _description = null)
        {
            N = id;
            Title = _name;
            Count = _count;
            Price = _price;
            Total = _total;
            Description = _description;
        }
    }

    [Serializable]
    public class SaveTypeDetail
    {
        public int Index { get; set; }
        public int Count { get; set; }

        public List<SaveWork> Works = new();
        public SaveTypeDetail(int _index = 0, int _count = 0)
        {
            Index = _index;
            Count = _count;
        }
    }
    
    [Serializable]
    public class SaveWork
    {
        public int Index { get; set; }
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
}