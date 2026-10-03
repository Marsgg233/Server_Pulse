using Docker.DotNet;
using GameServerManager.Application.Interfaces;
using GameServerManager.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GameServerManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDockerClient>(_ => new DockerClientConfiguration().CreateClient());
        services.AddSingleton<IDockerService, DockerService>();
        return services;
    }
}
