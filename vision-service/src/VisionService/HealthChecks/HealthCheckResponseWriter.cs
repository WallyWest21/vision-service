using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VisionService.HealthChecks;

/// <summary>Writes a structured JSON health-check response.</summary>
public static class HealthCheckResponseWriter
{
    private static readonly string _version =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Writes the health report as structured JSON including per-check results,
    /// overall status, service name, version, and UTC timestamp.
    /// </summary>
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var result = new
        {
            status = report.Status.ToString(),
            service = "VisionService",
            version = _version,
            timestamp = DateTime.UtcNow,
            duration = report.TotalDuration,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration,
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            })
        };

        var json = JsonSerializer.Serialize(result, _jsonOptions);
        return context.Response.WriteAsync(json, Encoding.UTF8);
    }
}
