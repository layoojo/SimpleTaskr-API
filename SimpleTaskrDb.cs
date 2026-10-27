using Microsoft.EntityFrameworkCore;

public class SimpleTaskrDb : DbContext
{
    public SimpleTaskrDb(DbContextOptions<SimpleTaskrDb> options)
        : base(options) { }

    public DbSet<SimpleTaskr> Tasks => Set<SimpleTaskr>();
}