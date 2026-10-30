using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WFI.Core;
using Xunit;

namespace WFI.Tests;
public class OllamaEngineTests
{
    private sealed class CaptureHandler(string modelResponse):HttpMessageHandler
    {
        public JsonDocument? Request;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Request=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new{response=modelResponse})};
        }
    }
    private static (DiagnosticJob job,List<Finding> findings) Sample()
    {
        var rule=Rules.All.Single(r=>r.Id=="AD-001");
        return (new DiagnosticJob{Domain="synthetic.invalid",Hostname="engine-test",ScanType="NetworkScan",ProblemDescription="Synthetic",EvidenceJson="[]"},
            [new(rule.Id,Rules.Version,rule.Category,"Medium",rule.Title,rule.Explanation,rule.Checks,[new("synthetic-source",1,"ERROR_NO_LOGON_SERVERS")])]);
    }
    private static async Task<(AiAnalysisJob result,CaptureHandler handler)> Run(string modelResponse)
    {
        var handler=new CaptureHandler(modelResponse);var (job,findings)=Sample();
        var result=await new OllamaEngine(new HttpClient(handler)).DiagnoseAsync(new(){BaseUrl="http://127.0.0.1:11434",ActiveModel="phi4-mini:latest"},job,findings,"test");
        return (result,handler);
    }

    [Fact]
    public async Task RequestUsesJsonSchemaAndTurkishInstructionsWithoutAnExampleConfidence()
    {
        var (_,handler)=await Run("""{"conclusion":"Synthetic","confidence":0.4,"hypotheses":[],"missingEvidence":["x"],"recommendedPlaybooks":[]}""");
        var root=handler.Request!.RootElement;
        Assert.Equal(JsonValueKind.Object,root.GetProperty("format").ValueKind);
        var required=root.GetProperty("format").GetProperty("required").EnumerateArray().Select(x=>x.GetString()).ToArray();
        Assert.Contains("confidence",required);Assert.Contains("hypotheses",required);
        var allowed=root.GetProperty("format").GetProperty("properties").GetProperty("recommendedPlaybooks").GetProperty("items").GetProperty("enum").EnumerateArray().Select(x=>x.GetString()!).ToArray();
        Assert.Equal(new[]{"PB-DNS-FLUSH"},allowed);
        var prompt=root.GetProperty("prompt").GetString()!;
        Assert.Contains("Türkçe",prompt);
        // A literal sample value in the prompt is copied by small models; the prompt must not contain one.
        Assert.DoesNotContain("0.85",prompt);
    }

    [Theory]
    [InlineData("0.6",0.6)][InlineData("\"0,7\"",0.7)][InlineData("\"%85\"",0.85)][InlineData("85",0.85)][InlineData("\"0.3\"",0.3)]
    public async Task CommonConfidenceFormatsAreNormalised(string confidence,double expected)
    {
        var (result,_)=await Run("{\"conclusion\":\"Synthetic\",\"confidence\":"+confidence+",\"hypotheses\":[],\"missingEvidence\":[\"x\"],\"recommendedPlaybooks\":[\"PB-DNS-FLUSH\",\"PB-UNKNOWN\"]}");
        Assert.Equal("completed",result.Status);
        Assert.Equal(expected,result.ConfidenceScore,3);
        Assert.Equal("[\"PB-DNS-FLUSH\"]",result.RecommendedPlaybooksJson);
    }

    [Theory][InlineData("\"high\"")][InlineData("150")][InlineData("-1")][InlineData("null")]
    public async Task InvalidConfidenceFallsBackToRuleOnlyResult(string confidence)
    {
        var (result,_)=await Run("{\"conclusion\":\"Synthetic\",\"confidence\":"+confidence+",\"hypotheses\":[]}");
        Assert.Equal("failed",result.Status);Assert.Equal(0,result.ConfidenceScore);
        // Fallback missing evidence comes from the matched rule's own checks, not unrelated fixed text.
        var missing=JsonSerializer.Deserialize<string[]>(result.MissingEvidenceJson)!;
        Assert.Equal(Rules.All.Single(r=>r.Id=="AD-001").Checks.Take(10),missing);
    }
}
