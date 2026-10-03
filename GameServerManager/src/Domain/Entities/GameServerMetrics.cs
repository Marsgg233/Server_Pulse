namespace GameServerManager.Domain.Entities;

public class GameServerMetrics
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public double CpuPercentage { get; set; }
    public long MemoryBytes { get; set; }
    public DateTime Timestamp { get; set; }
}
