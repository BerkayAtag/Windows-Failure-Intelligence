using System.Net;
using System.Text;
using WFI.Core;
using WFI.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WFI.Tests;

public class OllamaTests
{
    private sealed class TimeoutHandler:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromCanceled<HttpResponseMessage>(new CancellationToken(true));
    }
    private sealed class TimeoutClients:IHttpClientFactory
    {
        public HttpClient CreateClient(string name)=>new(new TimeoutHandler());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ModelDiscoveryTimeoutIsUnavailableButCallerCancellationPropagates(bool callerCancelled)
    {
        using var services=new ServiceCollection().BuildServiceProvider();
        using var service=new OllamaService(new TimeoutClients(),new ConfigurationBuilder().Build(),services.GetRequiredService<IServiceScopeFactory>());
        using var cancel=new CancellationTokenSource();if(callerCancelled)cancel.Cancel();
        if(callerCancelled)await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.GetModelsAsync(cancel.Token));
        else Assert.Empty(await service.GetModelsAsync(cancel.Token));
    }
    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"models\":{}}")]
    public async Task MalformedModelListIsUnavailableInsteadOfBreakingConfiguration(string body)
    {
        using var http=new HttpClient(new Reply(HttpStatusCode.OK,body));
        Assert.Empty(await new OllamaEngine(http).GetAvailableModelsAsync("http://localhost:11434"));
    }
    [Fact]
    public async Task NullAndEmptyModelNamesAreNotSelectable()
    {
        using var http=new HttpClient(new Reply(HttpStatusCode.OK,"{\"models\":[null,{}, {\"name\":null},{\"name\":\" \"},{\"name\":\"valid:latest\"}]}"));
        Assert.Equal(["valid:latest"],await new OllamaEngine(http).GetAvailableModelsAsync("http://localhost:11434"));
    }
    private sealed class Reply(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, "{\"response\":\"not json\"}")]
    public async Task UnavailableOrMalformedModelNeverClaimsHealthyOrVerified(HttpStatusCode status, string body)
    {
        using var http = new HttpClient(new Reply(status, body));
        var result = await new OllamaEngine(http).DiagnoseAsync(new AiConfig(), new DiagnosticJob { Hostname="synthetic" }, [], "test");
        Assert.Equal("failed", result.Status);
        Assert.Equal(0, result.ConfidenceScore);
        Assert.Equal("[]", result.HypothesesJson);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ModelListDoesNotInventInstalledModels()
    {
        using var http = new HttpClient(new Reply(HttpStatusCode.OK, "{\"models\":[{\"name\":\"local-test:latest\"}]}"));
        var models = await new OllamaEngine(http).GetAvailableModelsAsync("http://localhost:11434");
        Assert.Equal(["local-test:latest"], models);
    }
    [Fact]
    public async Task MissingConfidenceDoesNotBecomeFabricatedSuccess()
    {
        var body=System.Text.Json.JsonSerializer.Serialize(new {response="{\"conclusion\":\"Synthetic hypothesis\"}"});
        using var http=new HttpClient(new Reply(HttpStatusCode.OK,body));
        var result=await new OllamaEngine(http).DiagnoseAsync(new AiConfig(),new DiagnosticJob{Hostname="synthetic"},[],"test");
        Assert.Equal("failed",result.Status);Assert.Equal(0,result.ConfidenceScore);
    }
}
