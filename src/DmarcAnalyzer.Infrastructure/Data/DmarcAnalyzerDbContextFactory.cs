using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DmarcAnalyzer.Infrastructure.Data;

/// <summary>
/// Design-time factory used only by `dotnet ef migrations add` / `dotnet ef database update` when run
/// directly against this project. The real app wires up the DbContext (and its real connection string,
/// via Managed Identity against Azure SQL) in DmarcAnalyzer.Web's Program.cs — this factory never runs
/// in the deployed app.
/// </summary>
public class DmarcAnalyzerDbContextFactory : IDesignTimeDbContextFactory<DmarcAnalyzerDbContext>
{
    public DmarcAnalyzerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DmarcAnalyzerDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("DMARC_DESIGN_TIME_CONNECTION_STRING")
            ?? "Server=(localdb)\\mssqllocaldb;Database=DmarcAnalyzer.DesignTime;Trusted_Connection=True;";
        optionsBuilder.UseSqlServer(connectionString);
        return new DmarcAnalyzerDbContext(optionsBuilder.Options);
    }
}
