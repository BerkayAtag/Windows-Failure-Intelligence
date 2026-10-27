using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WFI.Core;

namespace WFI.Agent;
public sealed class AgentService(HttpClient http,AgentConfig config,AgentStateStore store,WindowsEvidenceCollector collector,RemediationRunner runner)
{
    private AgentState? state;
    private bool legacyServer;
    public bool LastCycleBusy {get;private set;}
    public int ServerIdleSeconds {get;private set;}=300;
    public int? RetryAfterSeconds {get;private set;}
    private void CheckResponse(HttpResponseMessage response)
    {
        if(response.StatusCode==HttpStatusCode.TooManyRequests)
        {
            var delay=response.Headers.RetryAfter?.Delta??(response.Headers.RetryAfter?.Date-DateTimeOffset.UtcNow);
            RetryAfterSeconds=delay.HasValue?(int)Math.Clamp(Math.Ceiling(delay.Value.TotalSeconds),10,3600):null;
        }
        response.EnsureSuccessStatusCode();
    }
    public async Task CycleAsync(CancellationToken ct)
    {
        LastCycleBusy=false;RetryAfterSeconds=null;
        state??=await store.LoadAsync(ct);
        // Persist credential before enrollment so a lost response can be recovered via heartbeat.
        await store.SaveAsync(state,ct);
        http.DefaultRequestHeaders.Authorization=new("Bearer",state.Credential);
        http.DefaultRequestHeaders.Remove("X-WFI-Device");http.DefaultRequestHeaders.Add("X-WFI-Device",state.DeviceId.ToString());
        AgentCheckInResponse? combined=null;
        if(state.Enrolled&&state.ActiveJob==null&&!legacyServer)
        {
            using var check=await http.PostAsJsonAsync("api/v1/agent/me/check-in",new AgentCheckIn(new(RemediationEngine.Version,Environment.OSVersion.VersionString),["combined-check-in-v1"]),ct);
            if(check.StatusCode==HttpStatusCode.NotFound)legacyServer=true;
            else
            {
                CheckResponse(check);
                combined=await check.Content.ReadFromJsonAsync<AgentCheckInResponse>(ct)??throw new InvalidDataException();
                if(combined.ProtocolVersion!=1||combined.Capabilities==null||!combined.Capabilities.Contains("combined-check-in-v1")||
                    (combined.Diagnostic!=null&&combined.Remediation!=null))throw new InvalidDataException("Unsupported check-in response.");
                ServerIdleSeconds=Math.Clamp(combined.IdlePollSeconds,10,600);
            }
        }
        if(combined==null)
        {
        using var pulse=await http.PostAsJsonAsync("api/v1/agent/me/heartbeat",new AgentPulse(RemediationEngine.Version,Environment.OSVersion.VersionString),ct);
        if(pulse.StatusCode==HttpStatusCode.Unauthorized)
        {
            if(state.Enrolled)throw new HttpRequestException("Device authentication revoked or invalid.");
            using var enrollment=await http.PostAsJsonAsync("api/v1/agent/enroll",new AgentEnrollment(state.DeviceId,config.EnrollmentToken,state.Credential),ct);
            CheckResponse(enrollment);state.Enrolled=true;await store.SaveAsync(state,ct);
        }
        else{CheckResponse(pulse);state.Enrolled=true;await store.SaveAsync(state,ct);}
        }
        if(state.ActiveJob!=null)
        {
            // A crash with no receipt means execution outcome is uncertain, never execute again.
            state.PendingReceipt??=new(state.ActiveJob.LeaseToken,false,"Uncertain","Agent restarted during execution; inspect device.","Not verified");
            await SendReceipt(ct);
        }
        var job=combined?.Diagnostic;
        if(combined==null)
        {
            using var diagnostic=await http.PostAsync("api/v1/agent/me/diagnostics/claim",null,ct);CheckResponse(diagnostic);
            if(diagnostic.StatusCode!=HttpStatusCode.NoContent)job=await diagnostic.Content.ReadFromJsonAsync<DiagnosticAssignment>(ct)??throw new InvalidDataException();
        }
        if(job!=null)
        {
            LastCycleBusy=true;
            var evidence=await collector.CollectAsync(job.ScanType,ct);
            using var result=await http.PostAsJsonAsync($"api/v1/agent/me/diagnostics/{job.JobId}/evidence",new EvidenceSubmission(job.LeaseToken,evidence),ct);
            CheckResponse(result);
        }
        var assignment=combined?.Remediation;
        if(combined==null||job!=null)
        {
            using var remediation=await http.PostAsync("api/v1/agent/me/remediations/claim",null,ct);CheckResponse(remediation);
            if(remediation.StatusCode!=HttpStatusCode.NoContent)assignment=await remediation.Content.ReadFromJsonAsync<RemediationAssignment>(ct)??throw new InvalidDataException();
        }
        if(assignment==null)return;
        LastCycleBusy=true;
        state.ActiveJob=assignment;state.PendingReceipt=null;await store.SaveAsync(state,ct);
        state.PendingReceipt=await runner.ExecuteAsync(assignment,ct);
        await store.SaveAsync(state,CancellationToken.None);
        await SendReceipt(ct);
    }
    private async Task SendReceipt(CancellationToken ct)
    {
        using var response=await http.PostAsJsonAsync($"api/v1/agent/me/remediations/{state!.ActiveJob!.Id}/result",state.PendingReceipt,ct);
        CheckResponse(response);state.ActiveJob=null;state.PendingReceipt=null;await store.SaveAsync(state,ct);
    }
}
public sealed class AgentHost(AgentService agent,AgentConfig config,ILogger<AgentHost> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(1,Math.Max(2,config.PollSeconds))),stoppingToken);
        var polling=new AgentPolling(config.PollSeconds);
        while(!stoppingToken.IsCancellationRequested)
        {
            int seconds;
            try{await agent.CycleAsync(stoppingToken);seconds=polling.Success(agent.LastCycleBusy,agent.ServerIdleSeconds);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){seconds=polling.Failure(agent.RetryAfterSeconds);logger.LogWarning("Agent cycle failed ({Type}); state retained for retry.",ex.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(seconds+Random.Shared.Next(0,Math.Max(2,seconds/10))),stoppingToken);
        }
    }
}
