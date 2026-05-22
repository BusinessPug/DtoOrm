using DtoOrm.Core;
using DtoOrm.MariaDb;

namespace DtoOrm.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrmServices(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new MariaDbConnectionFactory(connectionString));
        services.AddSingleton<ISqlDialect, MariaDbDialect>();
        services.AddScoped(sp => new OrmSession(
            sp.GetRequiredService<IDbConnectionFactory>(),
            sp.GetRequiredService<ISqlDialect>()));
        return services;
    }

    public static IServiceCollection AddCqrsHandlers(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        var openHandlers = new[] { typeof(Application.Common.IQueryHandler<,>), typeof(Application.Common.ICommandHandler<,>) };

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && openHandlers.Contains(iface.GetGenericTypeDefinition()))
                    services.AddScoped(iface, type);
            }
        }

        services.AddScoped<Application.Common.Dispatcher>();
        return services;
    }
}
