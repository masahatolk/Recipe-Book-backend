using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeBook.Api.Data;
using Xunit;

namespace RecipeBook.Api.Tests.Infrastructure;

public class TestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"recipebook-tests-{Guid.NewGuid():N}.db");
    
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeBookDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        SqliteConnection.ClearAllPools();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RecipeBookDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<RecipeBookDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RecipeBookDbContext>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();

        if (!File.Exists(_dbPath))
            return;

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.Delete(_dbPath);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(100 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                await Task.Delay(100 * attempt);
            }
        }
    }

    async Task IAsyncLifetime.InitializeAsync() => await Task.CompletedTask;
}