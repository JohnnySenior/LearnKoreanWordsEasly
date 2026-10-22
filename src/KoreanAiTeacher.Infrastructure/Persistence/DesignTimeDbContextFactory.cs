using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KoreanAiTeacher.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef migrations add</c>; no database connection is made.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time;Username=design;Password=design")
            .Options);
}
