using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Indlela.Data;

/// <summary>
/// Lets `dotnet ef migrations add` build the context without running Program.cs.
/// The protector here is only used to build the model; it never encrypts real data.
/// </summary>
public class IndlelaDbContextFactory : IDesignTimeDbContextFactory<IndlelaDbContext>
{
    public IndlelaDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<IndlelaDbContext>()
            .UseSqlServer(config.GetConnectionString("IndlelaDb"))
            .Options;

        return new IndlelaDbContext(options, DataProtectionProvider.Create("Indlela.DesignTime"));
    }
}
