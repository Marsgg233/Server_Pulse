using Docker.DotNet;
using Docker.DotNet.Models;
using GameServerManager.Application.Interfaces;
using GameServerManager.Application.Models;
using System.IO;
using System.Text;

namespace GameServerManager.Infrastructure.Services;

public sealed class DockerService : IDockerService
{
    private readonly IDockerClient _dockerClient;

    public DockerService(IDockerClient dockerClient)
    {
        _dockerClient = dockerClient;
    }

    public async Task<IEnumerable<ContainerModel>> GetContainersAsync(CancellationToken cancellationToken = default)
    {
        IList<ContainerListResponse> containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters { All = true }, cancellationToken);
        List<ContainerModel> containerModels = new List<ContainerModel>();
        foreach (ContainerListResponse c in containers)
        {
            string portsStr = string.Empty;
            if (c.Ports is not null && c.Ports.Count > 0)
            {
                portsStr = string.Join(", ", c.Ports
                    .Select(p => p.PublicPort > 0 ? $"{p.PublicPort}:{p.PrivatePort}" : $"{p.PrivatePort}")
                    .Distinct());
            }
            else
            {
                try
                {
                    ContainerInspectResponse inspect = await _dockerClient.Containers.InspectContainerAsync(c.ID, cancellationToken);
                    if (inspect.HostConfig?.PortBindings is not null && inspect.HostConfig.PortBindings.Count > 0)
                    {
                        List<string> portList = new List<string>();
                        foreach (KeyValuePair<string, IList<PortBinding>> kvp in inspect.HostConfig.PortBindings)
                        {
                            string containerPortPart = kvp.Key.Split('/')[0];
                            if (kvp.Value is not null && kvp.Value.Count > 0)
                            {
                                string? hostPort = kvp.Value[0].HostPort;
                                if (!string.IsNullOrEmpty(hostPort))
                                {
                                    portList.Add($"{hostPort}:{containerPortPart}");
                                }
                                else
                                {
                                    portList.Add(containerPortPart);
                                }
                            }
                            else
                            {
                                portList.Add(containerPortPart);
                            }
                        }
                        portsStr = string.Join(", ", portList.Distinct());
                    }
                }
                catch
                {
                    // Ignore inspection failures
                }
            }

            containerModels.Add(new ContainerModel
            {
                Id = c.ID,
                Image = c.Image,
                State = c.State,
                Status = c.Status,
                Names = c.Names is null || c.Names.Count == 0 ? string.Empty : string.Join(", ", c.Names).Replace("/", string.Empty),
                Ports = portsStr
            });
        }
        return containerModels;
    }

    public async Task<string> CreateAndStartContainerAsync(
        string imageName,
        string? containerName = null,
        int? hostPort = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageName);

        if (hostPort is int port)
        {
            IList<ContainerListResponse> existingContainers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters { All = true }, cancellationToken);
            foreach (ContainerListResponse container in existingContainers)
            {
                if (container.Ports is not null)
                {
                    foreach (Port containerPort in container.Ports)
                    {
                        if (containerPort.PublicPort == (ushort)port)
                        {
                            string containerIdentifier = container.Names is not null && container.Names.Count > 0 ? container.Names[0] : container.ID;
                            throw new InvalidOperationException($"Host port {port} is already in use by container '{containerIdentifier}' (ID: {container.ID}).");
                        }
                    }
                }
            }
        }

        await PullImageAsync(imageName, cancellationToken);

        CreateContainerParameters createParameters = new CreateContainerParameters
        {
            Image = imageName,
            Name = containerName
        };

        if (hostPort is int portConfig)
        {
            string portKey = $"{portConfig}/tcp";
            createParameters.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                [portKey] = default
            };
            createParameters.HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    [portKey] = new List<PortBinding>
                    {
                        new() { HostPort = portConfig.ToString() }
                    }
                }
            };
        }

        CreateContainerResponse created;
        try
        {
            created = await _dockerClient.Containers.CreateContainerAsync(createParameters, cancellationToken);
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException("Контейнер с таким именем уже существует.", ex);
        }

        bool started = await _dockerClient.Containers.StartContainerAsync(
            created.ID,
            new ContainerStartParameters(),
            cancellationToken);

        if (!started)
        {
            throw new InvalidOperationException($"Failed to start container '{created.ID}'.");
        }

        return created.ID;
    }

    public async Task StartExistingContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        try
        {
            ContainerInspectResponse inspectResponse = await _dockerClient.Containers.InspectContainerAsync(containerId, cancellationToken);
            if (inspectResponse.State.Running)
            {
                return;
            }
        }
        catch (DockerContainerNotFoundException)
        {
            throw;
        }
        catch
        {
            // Proceed to start attempt if inspection fails for unrelated reasons
        }

        try
        {
            bool started = await _dockerClient.Containers.StartContainerAsync(
                containerId,
                new ContainerStartParameters(),
                cancellationToken);

            if (!started)
            {
                throw new InvalidOperationException($"Failed to start existing container '{containerId}'.");
            }
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotModified)
        {
            // Container is already running
        }
    }

    public async Task StopContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        try
        {
            await _dockerClient.Containers.StopContainerAsync(
                containerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 10 },
                cancellationToken);
        }
        catch (DockerContainerNotFoundException)
        {
            return;
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotModified)
        {
            // Already stopped
        }
    }

    public async Task RemoveContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        try
        {
            await _dockerClient.Containers.StopContainerAsync(
                containerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 5 },
                cancellationToken);
        }
        catch
        {
            // Ignore stop errors during removal
        }

        try
        {
            await _dockerClient.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters { Force = true },
                cancellationToken);
        }
        catch (DockerContainerNotFoundException)
        {
            return;
        }
    }

    public async Task PruneContainersAsync(CancellationToken cancellationToken = default)
    {
        await _dockerClient.Containers.PruneContainersAsync(new ContainersPruneParameters(), cancellationToken);
    }

    public async Task<DockerContainerStats> GetContainerStatsAsync(
        string containerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        ContainerStatsResponse stats = await ReadStatsAsync(containerId, cancellationToken);

        return new DockerContainerStats
        {
            CpuPercentage = CalculateCpuPercentage(stats),
            MemoryBytes = (long)(stats.MemoryStats?.Usage ?? 0)
        };
    }

    public async Task<string> GetContainerLogsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        ContainerLogsParameters logsParameters = new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Tail = "100"
        };

        MultiplexedStream stream = await _dockerClient.Containers.GetContainerLogsAsync(
            containerId,
            false,
            logsParameters,
            cancellationToken);

        (string stdout, string stderr) = await stream.ReadOutputToEndAsync(cancellationToken);
        return string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}";
    }

    public async Task<string> ExecuteCommandAsync(string containerId, string command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        ContainerExecCreateResponse execResponse = await _dockerClient.Exec.ExecCreateContainerAsync(
            containerId,
            new ContainerExecCreateParameters
            {
                Cmd = new List<string> { "/bin/sh", "-c", command },
                AttachStdout = true,
                AttachStderr = true
            },
            cancellationToken);

        MultiplexedStream stream = await _dockerClient.Exec.StartAndAttachContainerExecAsync(
            execResponse.ID,
            false,
            cancellationToken);

        (string stdout, string stderr) = await stream.ReadOutputToEndAsync(cancellationToken);

        return string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}";
    }

    private async Task PullImageAsync(string imageName, CancellationToken cancellationToken)
    {
        (string fromImage, string tag) = SplitImageName(imageName);

        await _dockerClient.Images.CreateImageAsync(
            new ImagesCreateParameters
            {
                FromImage = fromImage,
                Tag = tag
            },
            authConfig: null,
            new Progress<JSONMessage>(),
            cancellationToken);
    }

    private async Task<ContainerStatsResponse> ReadStatsAsync(string containerId, CancellationToken cancellationToken)
    {
        ContainerStatsResponse? snapshot = null;
        TaskCompletionSource<ContainerStatsResponse> completion = new TaskCompletionSource<ContainerStatsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Progress<ContainerStatsResponse> progress = new Progress<ContainerStatsResponse>(stats =>
        {
            snapshot = stats;
            completion.TrySetResult(stats);
            linkedCts.Cancel();
        });

        try
        {
            await _dockerClient.Containers.GetContainerStatsAsync(
                containerId,
                new ContainerStatsParameters
                {
                    Stream = false
                },
                progress,
                linkedCts.Token);
        }
        catch (OperationCanceledException) when (snapshot is not null)
        {
        }

        if (snapshot is not null)
        {
            return snapshot;
        }

        return await completion.Task.WaitAsync(cancellationToken);
    }

    private static double CalculateCpuPercentage(ContainerStatsResponse stats)
    {
        double cpuDelta = (double)stats.CPUStats.CPUUsage.TotalUsage - stats.PreCPUStats.CPUUsage.TotalUsage;
        double systemDelta = (double)stats.CPUStats.SystemUsage - stats.PreCPUStats.SystemUsage;

        if (systemDelta > 0 && cpuDelta > 0)
        {
            uint cpuCount = stats.CPUStats.OnlineCPUs;
            if (cpuCount == 0)
            {
                cpuCount = (uint)(stats.CPUStats.CPUUsage.PercpuUsage?.Count ?? Environment.ProcessorCount);
            }

            return cpuDelta / systemDelta * cpuCount * 100.0;
        }

        if (cpuDelta <= 0 || stats.NumProcs == 0)
        {
            return 0;
        }

        double intervalNs = (stats.Read - stats.PreRead).TotalNanoseconds;
        if (intervalNs <= 0)
        {
            return 0;
        }

        double possibleIntervals = intervalNs / 100.0 * stats.NumProcs;
        return possibleIntervals <= 0 ? 0 : cpuDelta / possibleIntervals * 100.0;
    }

    private static (string Name, string Tag) SplitImageName(string imageName)
    {
        int lastSlash = imageName.LastIndexOf('/');
        int lastColon = imageName.LastIndexOf(':');
        if (lastColon > lastSlash)
        {
            return (imageName[..lastColon], imageName[(lastColon + 1)..]);
        }

        return (imageName, "latest");
    }

    public async Task<IEnumerable<ImageModel>> GetImagesAsync(CancellationToken cancellationToken = default)
    {
        IList<ImagesListResponse> images = await _dockerClient.Images.ListImagesAsync(new ImagesListParameters { All = true }, cancellationToken);
        List<ImageModel> list = new List<ImageModel>();
        foreach (ImagesListResponse img in images)
        {
            string repo = "<none>";
            string tag = "<none>";
            if (img.RepoTags is not null && img.RepoTags.Count > 0 && img.RepoTags[0] != "<none>:<none>")
            {
                string repoTag = img.RepoTags[0];
                int lastColon = repoTag.LastIndexOf(':');
                if (lastColon > 0)
                {
                    repo = repoTag[..lastColon];
                    tag = repoTag[(lastColon + 1)..];
                }
                else
                {
                    repo = repoTag;
                }
            }

            string rawId = img.ID ?? string.Empty;
            if (rawId.StartsWith("sha256:"))
            {
                rawId = rawId[7..];
            }
            string shortId = rawId.Length > 12 ? rawId[..12] : rawId;

            string createdStr = img.Created.ToString("yyyy-MM-dd HH:mm");
            string sizeStr = $"{img.Size / (1024.0 * 1024.0):F1} MB";

            list.Add(new ImageModel
            {
                Id = shortId,
                Repository = repo,
                Tag = tag,
                Created = createdStr,
                Size = sizeStr
            });
        }
        return list;
    }

    public async Task RemoveImageAsync(string imageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageId);
        await _dockerClient.Images.DeleteImageAsync(imageId, new ImageDeleteParameters { Force = true }, cancellationToken);
    }
}