using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameServerManager.Application.Models;

namespace GameServerManager.Application.Interfaces;

public interface IDockerService
{
    Task<IEnumerable<ContainerModel>> GetContainersAsync(CancellationToken cancellationToken = default);

    Task<string> CreateAndStartContainerAsync(
        string imageName,
        string? containerName = null,
        int? hostPort = null,
        CancellationToken cancellationToken = default);

    Task StartExistingContainerAsync(string containerId, CancellationToken cancellationToken = default);

    Task StopContainerAsync(string containerId, CancellationToken cancellationToken = default);

    Task RemoveContainerAsync(string containerId, CancellationToken cancellationToken = default);

    Task PruneContainersAsync(CancellationToken cancellationToken = default);

    Task<DockerContainerStats> GetContainerStatsAsync(
        string containerId,
        CancellationToken cancellationToken = default);

    Task<string> GetContainerLogsAsync(string containerId, CancellationToken cancellationToken = default);

    Task<string> ExecuteCommandAsync(string containerId, string command, CancellationToken cancellationToken = default);

    Task<IEnumerable<ImageModel>> GetImagesAsync(CancellationToken cancellationToken = default);

    Task RemoveImageAsync(string imageId, CancellationToken cancellationToken = default);
}