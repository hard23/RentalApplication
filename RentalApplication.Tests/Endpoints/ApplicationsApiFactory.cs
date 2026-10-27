using HB.RentalApplication.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RentalApplication.Tests.Endpoints;

/// <summary>
/// Hosts the API in-process and replaces SQL Server with a per-test EF Core InMemory database.
/// </summary>
public sealed class ApplicationsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RentalApplicationDbContext>>();
            services.AddDbContext<RentalApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
