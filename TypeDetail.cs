using Microsoft.EntityFrameworkCore;

public class TypeDetail
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public TypeDetail()
    {

    }
}

public class Work
{
    public int Id { get; set; }
    public string? Name { get; set; }
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
