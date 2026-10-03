namespace GameServerManager.Application.Models;

public sealed class ImageModel
{
    public string Id { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string Created { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
}
