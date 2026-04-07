using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using VisionService.Diagnostics;
using VisionService.Endpoints;
using VisionService.Extensions;
using VisionService.HealthChecks;
using VisionService.Jobs;
using VisionService.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "VisionService"));

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "VisionService API", Version = "v1" });
    c.AddSecurityDefinition("ApiKey", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-Api-Key",
        Description = "API key authentication"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" } },
            Array.Empty<string>()
        }
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<YoloHealthCheck>("yolo", tags: ["ready"])
    .AddCheck<QwenVlHealthCheck>("qwen-vl", tags: ["ready"]);

// Vision services (options, clients, image service, event bus)
builder.Services.AddVisionServices(builder.Configuration);

// OpenTelemetry distributed tracing
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("VisionService"))
            .AddSource(VisionActivitySource.Name)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));

        if (builder.Environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }
    });

// Background jobs
builder.Services.AddHostedService<ImageCleanupJob>();
builder.Services.AddHostedService<ModelHealthCheckJob>();

// Response compression
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<GzipCompressionProvider>();
    opts.Providers.Add<BrotliCompressionProvider>();
});

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseResponseCompression();
app.UseCors("VisionCors");
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VisionService v1");
    c.RoutePrefix = "swagger";
});

// WebSockets
app.UseWebSockets();

// Prometheus metrics
app.UseMetricServer();
app.UseHttpMetrics();

// Health endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

// YOLO endpoints
app.MapYoloEndpoints();

// Qwen-VL endpoints
app.MapQwenVlEndpoints();

// Pipeline endpoints
app.MapPipelineEndpoints();

// WebSocket streaming
app.MapWebSocketEndpoints();

// Admin and playground
app.MapAdminEndpoints();
app.MapPlaygroundEndpoints();

app.Run();

/// <summary>Entry point for integration tests.</summary>
public partial class Program { }
