using GameServerManager.Domain.Enums;

namespace GameServerManager.Domain.Entities;

public class Server
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DockerContainerId { get; set; } = string.Empty;
    public int Port { get; set; }
    public ServerStatus Status { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
}
