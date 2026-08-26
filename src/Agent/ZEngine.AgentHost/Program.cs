using ModelContextProtocol.Server;
using ZEngine.AgentHost;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<AgentHostState>();
builder.Services.AddHostedService<AgentFramePump>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<EngineMcpTools>();

WebApplication app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapMcp("/mcp");
await app.RunAsync();
