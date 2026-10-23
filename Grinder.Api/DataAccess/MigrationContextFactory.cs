using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Grinder.Api.DataAccess;

public class MigrationContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();

        optionsBuilder
            .UseNpgsql("Host=localhost;Database=temp;Username=temp;Password=temp");

        return new DatabaseContext(optionsBuilder.Options);
    }
}
