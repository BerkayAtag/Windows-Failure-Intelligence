using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WFI.Core;
using WFI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace WFI.Tests;
public class AiQualityTests
{
    [Theory][InlineData("Queued")][InlineData("Running")]
    public async Task NonLocalModelBlocksNewAndRecoveredRunsWithoutSendingData(string status)
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var run=new AiQualityRun{Domain="synthetic.invalid",Model="synthetic",Status=status,LeaseToken=Guid.NewGuid(),LeaseUntil=DateTime.UtcNow.AddMinutes(-1)};
        db.AiQualityRuns.Add(run);await db.SaveChangesAsync();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Ollama:BaseUrl","https://blocked.invalid"}}).Build();
        using var worker=new AiQualityWorker(app.Services.GetRequiredService<IServiceScopeFactory>(),new NoNetwork(),config,NullLogger<AiQualityWorker>.Instance);
        Assert.True(await worker.ProcessOneAsync(default));await db.Entry(run).ReloadAsync();
        Assert.Equal("BlockedLocalModelRequired",run.Status);Assert.Null(run.LeaseToken);Assert.Null(run.LeaseUntil);Assert.Equal(0,run.NextCase);
    }
    private sealed class NoNetwork:IHttpClientFactory {public HttpClient CreateClient(string name)=>throw new InvalidOperationException("Network must not be invoked");}
    [Fact]
    public void DatasetHasFortyUniqueSyntheticCasesAndRejectsUnsupportedRecommendations()
    {
        Assert.Equal(40,AiQualityDataset.Cases.Length);Assert.Equal(40,AiQualityDataset.Cases.Select(x=>x.Id).Distinct().Count());
        var sample=AiQualityDataset.Cases.First(x=>x.Category=="Missing");
        var safe=new AiAnalysisJob{Status="completed",RawResponse=JsonSerializer.Serialize(new{recommendedPlaybooks=Array.Empty<string>(),hypotheses=Array.Empty<object>(),missingEvidence=new[]{"Collect observations"}})};
        Assert.True(AiQualityDataset.Score(sample,safe,false,10).Passed);
        safe.RawResponse=JsonSerializer.Serialize(new{recommendedPlaybooks=new[]{"PB-UNKNOWN"},hypotheses=Array.Empty<object>(),missingEvidence=new[]{"Collect observations"}});
        Assert.False(AiQualityDataset.Score(sample,safe,true,10).AllowedRecommendations);
    }
    private sealed class Handler:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new{response=JsonSerializer.Serialize(new{conclusion="Synthetic",confidence=0.5,hypotheses=Array.Empty<object>(),missingEvidence=new[]{"Need evidence"},recommendedPlaybooks=new[]{"PB-UNKNOWN","PB-BIGFIX-REPAIR"}})})});
    }
    [Fact]
    public async Task ModelCannotRecommendCatalogOutsideCurrentEvidenceAndVersionsAreRecorded()
    {
        using var client=new HttpClient(new Handler());
        var result=await new OllamaEngine(client).DiagnoseAsync(new(),new(),[],"synthetic");
        Assert.Equal("completed",result.Status);Assert.Equal("[]",result.RecommendedPlaybooksJson);
        Assert.Contains(OllamaEngine.PromptVersion,result.VersionsJson);Assert.Contains(RemediationEngine.Version,result.VersionsJson);
    }
}
