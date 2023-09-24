using Microsoft.EntityFrameworkCore;

public class TypeDetail
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public TypeDetail()
    {

    }
}

public class ApplicationContext : DbContext
{
    public DbSet<TypeDetail> TypeDetails { get; set; } = null!;
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=typedetails.db");
    }
}
