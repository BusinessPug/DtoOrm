using DtoOrm.Api.Application.Common;
using DtoOrm.Api.Features.Departments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DtoOrm.Api.Tests;

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public DepartmentHandlerStub Departments { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MariaDb"] = "Server=localhost;Database=dtoorm_tests;Uid=test;Pwd=test;",
                ["Jwt:Issuer"] = "DtoOrm.School.Tests",
                ["Jwt:Audience"] = "DtoOrm.School.Tests",
                ["Jwt:SigningKey"] = "test-only-dtoorm-school-jwt-signing-key",
                ["Jwt:ExpirationMinutes"] = "30"
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveCqrsHandlers(services);

            services.AddSingleton<IQueryHandler<ListDepartmentsQuery, IReadOnlyList<DepartmentDto>>>(Departments);
            services.AddSingleton<IQueryHandler<GetDepartmentByIdQuery, DepartmentDto?>>(Departments);
            services.AddSingleton<ICommandHandler<CreateDepartmentCommand, int>>(Departments);
            services.AddSingleton<ICommandHandler<UpdateDepartmentCommand, bool>>(Departments);
            services.AddSingleton<ICommandHandler<DeleteDepartmentCommand, bool>>(Departments);
        });
    }

    private static void RemoveCqrsHandlers(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var serviceType = services[i].ServiceType;
            if (!serviceType.IsGenericType)
                continue;

            var genericType = serviceType.GetGenericTypeDefinition();
            if (genericType == typeof(IQueryHandler<,>) || genericType == typeof(ICommandHandler<,>))
                services.RemoveAt(i);
        }
    }
}
