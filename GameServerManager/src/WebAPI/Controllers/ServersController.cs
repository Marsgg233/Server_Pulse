using Microsoft.AspNetCore.Mvc;
using GameServerManager.Application.Interfaces;
using GameServerManager.Application.Models;

namespace GameServerManager.WebAPI.Controllers;

[ApiController]
[Route("api/servers")]
public sealed class ServersController : ControllerBase
{
    private readonly IDockerService _dockerService;

    public ServersController(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContainerModel>>> GetContainers(CancellationToken cancellationToken)
    {
        IEnumerable<ContainerModel> containers = await _dockerService.GetContainersAsync(cancellationToken);
        return Ok(containers);
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartContainer([FromBody] StartContainerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ImageName))
            return BadRequest("Image name is required.");

        try
        {
            string containerId = await _dockerService.CreateAndStartContainerAsync(
                request.ImageName,
                request.ContainerName,
                request.Port,
                cancellationToken);

            return Ok(new { id = containerId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/start-existing")]
    public async Task<IActionResult> StartExistingContainer(string id, CancellationToken cancellationToken)
    {
        await _dockerService.StartExistingContainerAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> StopContainer(string id, CancellationToken cancellationToken)
    {
        await _dockerService.StopContainerAsync(id, cancellationToken);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveContainer(string id, CancellationToken cancellationToken)
    {
        await _dockerService.RemoveContainerAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("prune")]
    public async Task<IActionResult> PruneContainers(CancellationToken cancellationToken)
    {
        await _dockerService.PruneContainersAsync(cancellationToken);
        return Ok();
    }

    [HttpGet("{id}/stats")]
    public async Task<ActionResult<DockerContainerStats>> GetStats(string id, CancellationToken cancellationToken)
    {
        DockerContainerStats stats = await _dockerService.GetContainerStatsAsync(id, cancellationToken);
        return Ok(stats);
    }

    [HttpGet("{id}/logs")]
    public async Task<ActionResult<string>> GetLogs(string id, CancellationToken cancellationToken)
    {
        string logs = await _dockerService.GetContainerLogsAsync(id, cancellationToken);
        return Ok(logs);
    }

    [HttpPost("{id}/exec")]
    public async Task<ActionResult<string>> ExecuteCommand(string id, [FromBody] ExecuteCommandRequest request, CancellationToken cancellationToken)
    {
        string result = await _dockerService.ExecuteCommandAsync(id, request.Command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("images")]
    public async Task<ActionResult<IEnumerable<ImageModel>>> GetImages(CancellationToken cancellationToken)
    {
        IEnumerable<ImageModel> images = await _dockerService.GetImagesAsync(cancellationToken);
        return Ok(images);
    }

    [HttpDelete("images/{id}")]
    public async Task<IActionResult> RemoveImage(string id, CancellationToken cancellationToken)
    {
        try
        {
            await _dockerService.RemoveImageAsync(id, cancellationToken);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

public sealed class StartContainerRequest
{
    public string ImageName { get; init; } = string.Empty;
    public string? ContainerName { get; init; }
    public int? Port { get; init; }
}

public sealed class ExecuteCommandRequest
{
    public string Command { get; init; } = string.Empty;
}