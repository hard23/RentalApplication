using HB.RentalApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HB.RentalApplication.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructurePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<RentalApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<RentalApplicationRepository>();
        services.AddScoped<RentalApplicationHistoryRepository>();
        return services;
    }
}
