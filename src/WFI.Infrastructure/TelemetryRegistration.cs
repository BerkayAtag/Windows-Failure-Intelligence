using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
namespace WFI.Infrastructure;

public static class TelemetryRegistration
{
    public static IServiceCollection AddWfiTelemetry(this IServiceCollection services,IConfiguration config)
    {
        if(config.GetValue("Ollama:MaxConcurrent",1) is <1 or >64)throw new InvalidOperationException("Ollama MaxConcurrent must be 1..64.");
        var raw=config["Telemetry:OtlpEndpoint"];
        // No exporter or external network call unless an operator configures a collector.
        if(string.IsNullOrWhiteSpace(raw))return services;
        if(!Uri.TryCreate(raw,UriKind.Absolute,out var endpoint)||endpoint.UserInfo.Length>0||
            endpoint.Query.Length>0||endpoint.Fragment.Length>0||
            (endpoint.Scheme!="https"&&!(endpoint.Scheme=="http"&&endpoint.IsLoopback)))
            throw new InvalidOperationException("OTLP requires HTTPS or loopback HTTP without credentials/query.");
        services.AddOpenTelemetry()
            .WithMetrics(m=>m.AddMeter(WfiTelemetry.Name).AddOtlpExporter(o=>o.Endpoint=endpoint))
            .WithTracing(t=>t.AddSource(WfiTelemetry.Name).SetSampler(new TraceIdRatioBasedSampler(0.05))
                .AddOtlpExporter(o=>o.Endpoint=endpoint));
        return services;
    }
}
