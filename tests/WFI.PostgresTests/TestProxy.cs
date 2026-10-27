using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace WFI.PostgresTests;

internal sealed class TestProxy(WebApplication app):IAsyncDisposable
{
    public Uri Address=>new(app.Urls.Single());
    public static async Task<TestProxy> Start(Uri first,Uri second)
    {
        if(!first.IsLoopback||!second.IsLoopback)throw new InvalidOperationException("Test destinations must be loopback.");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development"});
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options=>options.Listen(IPAddress.Loopback,0));
        builder.Services.AddReverseProxy().LoadFromMemory(
            [new RouteConfig{RouteId="test",ClusterId="apis",Match=new RouteMatch{Path="/{**path}"}}],
            [new ClusterConfig{ClusterId="apis",LoadBalancingPolicy="RoundRobin",
                HealthCheck=new HealthCheckConfig{Active=new ActiveHealthCheckConfig{Enabled=true,Interval=TimeSpan.FromSeconds(1),Timeout=TimeSpan.FromSeconds(1),Policy="ConsecutiveFailures",Path="/api/v1/ready"}},
                Metadata=new Dictionary<string,string>{{"ConsecutiveFailuresHealthPolicy.Threshold","1"}},
                Destinations=new Dictionary<string,DestinationConfig>{{"first",new(){Address=first.ToString()}},{"second",new(){Address=second.ToString()}}}}])
            .AddTransforms(context=>context.AddResponseTransform(response=>{
                response.HttpContext.Response.Headers["X-WFI-Test-Backend"]=response.ProxyResponse?.RequestMessage?.RequestUri?.Port.ToString()??"unavailable";
                return ValueTask.CompletedTask;
            }));
        var app=builder.Build();app.MapReverseProxy();
        try{await app.StartAsync();return new(app);}catch{await app.DisposeAsync();throw;}
    }
    public async ValueTask DisposeAsync(){await app.StopAsync();await app.DisposeAsync();}
}
