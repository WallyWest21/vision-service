using Serilog;
using VisionService.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext());

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Vision services (options registration)
builder.Services.AddVisionServices(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Health check
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Service = "VisionService",
    Version = "1.0.0"
}))
.WithName("HealthCheck")
.WithOpenApi();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
