using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Metal_Code
{
    [Serializable]
    public class Detail
    {
        public int N { get; set; }
        public string? Наименование { get; set; }
        public float Цена { get; set; }
        public int Кол { get; set; }
        public float Стоимость { get; set; }

        public List<SaveTypeDetail> TypeDetails = new();
        public Detail(int id, string? _name, float _price, int _count, float _total)
        {
            N = id;
            Наименование = _name;
            Цена = _price;
            Кол = _count;
            Стоимость = _total;
        }
    }

    public class SaveTypeDetail
    {
        public string? Name { get; set; }
        public int Count { get; set; }
        public SaveTypeDetail()
        {

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