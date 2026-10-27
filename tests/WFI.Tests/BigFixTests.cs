using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WFI.Agent;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;
public class BigFixTests
{
    [Theory]
    [InlineData("WFI_BESCLIENT_STOPPED Status=Stopped",true)]
    [InlineData("WFI_BESCLIENT_MISSING",true)]
    [InlineData("BESClient Running",false)]
    [InlineData("Service state unavailable",false)]
    public void ServiceEvidenceMustBeActionable(string content,bool expected)
    {
        var findings=DiagnosticJobProcessor.AnalyzeEvidence(JsonSerializer.Serialize(new[]{new EvidenceItem{Source="BESClient",Category="BigFix",Content=content}}),default);
        Assert.Equal(expected,findings.Any(x=>x.RuleId=="BF-001"));
    }
    [Fact]
    public void TextInLogsCannotImpersonateServiceProbe()
    {
        var findings=DiagnosticJobProcessor.AnalyzeEvidence(JsonSerializer.Serialize(new[]{new EvidenceItem{Source="System/123",Category="EventLog",Content="WFI_BESCLIENT_MISSING"}}),default);
        Assert.DoesNotContain(findings,x=>x.RuleId=="BF-001");
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task RunningOrStartableServiceDoesNotInvokeReinstaller(bool running)
    {
        if(!OperatingSystem.IsWindows())return;
        var mock="function Get-Service { $s=[pscustomobject]@{Status='"+(running?"Running":"Stopped")+"'}; $s | Add-Member ScriptMethod WaitForStatus { param($a,$b) }; return $s }; function Start-Service { Write-Output 'Synthetic start' }; function Get-Item { throw 'Reinstaller must not be reached' }; ";
        var result=await new PowerShellRunner().RunAsync(mock+RemediationEngine.BigFixRepair,default);
        Assert.Equal(0,result.ExitCode);
    }
    [Fact]
    public async Task FailedServiceWithoutApprovedHashStopsBeforeFileAccess()
    {
        if(!OperatingSystem.IsWindows())return;
        var mock="function Get-Service { return $null }; function Start-Service { throw 'Synthetic failure' }; function Get-Item { throw 'Unexpected file access' }; ";
        var result=await new PowerShellRunner().RunAsync(mock+RemediationEngine.BigFixRepair.Replace("__WFI_BIGFIX_SHA256__",""),default);
        Assert.NotEqual(0,result.ExitCode);Assert.Contains("Approved BigFix reinstall SHA256 is not configured on agent",result.Output);
    }
    [Fact]
    public async Task DescriptionIsBoundedAndAgentPackageRequiresAdmin()
    {
        using var app=new TestApp();using var client=app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/distribution/agent")).StatusCode);
        async Task Csrf(){client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");var t=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",t.GetProperty("token").GetString());}
        await Csrf();await client.PostAsJsonAsync("/api/v1/auth/login",new{username="analyst",password="test-password"});await Csrf();
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/v1/distribution/agent")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/diagnostics",new{hostname="synthetic",scanType="BigFixScan",problemDescription=new string('a',4001)})).StatusCode);
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        db.Devices.Add(new Device{Hostname="synthetic",Domain="example.invalid"});await db.SaveChangesAsync();
        var response=await client.PostAsJsonAsync("/api/v1/diagnostics",new{hostname=" SYNTHETIC ",scanType="BigFixScan",problemDescription="BigFix stopped; WFI_BESCLIENT_MISSING"});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var created=await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail=await client.GetFromJsonAsync<JsonElement>("/api/v1/diagnostics/"+created.GetProperty("id").GetString());
        Assert.Equal("BigFix stopped; WFI_BESCLIENT_MISSING",detail.GetProperty("problemDescription").GetString());
        Assert.Empty(DiagnosticJobProcessor.AnalyzeEvidence(null,default));
    }
    [Theory]
    [InlineData("reparse","Reparse path rejected")]
    [InlineData("owner","BigFix path owned by untrusted principal")]
    [InlineData("write","BigFix path writable by untrusted principal")]
    public async Task UnsafeReinstallPathIsRejectedBeforeOpeningFile(string scenario,string expected)
    {
        if(!OperatingSystem.IsWindows())return;
        var mock="""
            function Get-Service { return $null }
            function Start-Service { throw 'Synthetic failure' }
            function Get-Item { return [pscustomobject]@{FullName='C:\Windows\Bigfix\Cache\reinstall.cmd';Attributes=ATTR;Parent=$null} }
            function Get-Acl {
                $a=[pscustomobject]@{Access=@()}
                $a | Add-Member ScriptMethod GetOwner {param($t); [Security.Principal.SecurityIdentifier]::new('OWNER') }
                if('SCENARIO' -eq 'write'){$a.Access=@([pscustomobject]@{PropagationFlags=0;IdentityReference=[Security.Principal.SecurityIdentifier]::new('S-1-5-32-545');AccessControlType='Allow';FileSystemRights=2})}
                return $a
            }
            """;
        mock=mock.Replace("ATTR",scenario=="reparse"?"1024":"0").Replace("OWNER",scenario=="owner"?"S-1-5-32-545":"S-1-5-32-544").Replace("SCENARIO",scenario);
        var result=await new PowerShellRunner().RunAsync(mock+RemediationEngine.BigFixRepair.Replace("__WFI_BIGFIX_SHA256__",new string('a',64)),default);
        Assert.NotEqual(0,result.ExitCode);Assert.True(result.Output.Contains(expected),result.Output);
    }
}
