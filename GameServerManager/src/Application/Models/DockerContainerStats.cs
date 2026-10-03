namespace GameServerManager.Application.Models;

public sealed class DockerContainerStats
{
    public double CpuPercentage { get; init; }
    public long MemoryBytes { get; init; }
}
