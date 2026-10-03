using GameServerManager.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddInfrastructure();

WebApplication app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
