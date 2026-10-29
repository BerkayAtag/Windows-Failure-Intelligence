using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = "WFI.Ollama");
builder.Services.AddHostedService<OllamaProcess>();
await builder.Build().RunAsync();

sealed class OllamaProcess : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var root = AppContext.BaseDirectory;
        var models = (await File.ReadAllTextAsync(Path.Combine(root, "models-path.txt"), stoppingToken)).Trim();
        if (!Path.IsPathFullyQualified(models)) throw new InvalidOperationException("Absolute model path required.");
        var start = new ProcessStartInfo(Path.Combine(root, "ollama.exe"), "serve") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root };
        start.Environment["OLLAMA_HOST"] = "127.0.0.1:11435";
        start.Environment["OLLAMA_MODELS"] = models;
        start.Environment["OLLAMA_NO_CLOUD"] = "1";
        var home = Path.Combine(Path.GetDirectoryName(models)!, "ollama-home");
        start.Environment["HOME"] = home;
        start.Environment["USERPROFILE"] = home;
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Ollama could not start.");
        try
        {
            await child.WaitForExitAsync(stoppingToken);
            if (!stoppingToken.IsCancellationRequested) Environment.Exit(1);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(CancellationToken.None); }
        }
    }
}
