using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Metal_Code
{
    [Serializable]
    public class Detail
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Count { get; set; }
        public float Price { get; set; }

        public List<SaveTypeDetail> TypeDetails = new();
        public Detail(string? _name, int _count, float _price)
        {
            Name = _name;
            Count = _count;
            Price = _price;
        }
    }

    public class SaveTypeDetail
    {

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