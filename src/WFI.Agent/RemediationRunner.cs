using System.Diagnostics;
using System.Text;
using WFI.Core;

namespace WFI.Agent;
public sealed record CommandResult(int ExitCode,string Output);
public class PowerShellRunner
{
    public virtual async Task<CommandResult> RunAsync(string script,CancellationToken ct)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        ct.ThrowIfCancellationRequested();
        var exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
        var start=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("-NoProfile");start.ArgumentList.Add("-NonInteractive");start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; try { "+script+" } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }")));
        using var process=Process.Start(start)??throw new IOException("Process could not start.");
        var output=Drain(process.StandardOutput);var error=Drain(process.StandardError);
        try{await process.WaitForExitAsync(ct);}
        catch
        {
            try{process.Kill(true);}catch(InvalidOperationException){}
            await process.WaitForExitAsync(CancellationToken.None);
            // Observe both pipe readers before disposing their streams after cancellation.
            await Task.WhenAll(output,error);
            throw;
        }
        return new(process.ExitCode,LogAnalyzer.Redact((await output)+"\n"+(await error)));
    }
    private static async Task<string> Drain(StreamReader reader)
    {
        var output=new StringBuilder();var buffer=new char[1024];int count;
        while((count=await reader.ReadAsync(buffer))>0){var take=Math.Min(count,1800-output.Length);if(take>0)output.Append(buffer,0,take);}
        return output.ToString();
    }
}
public sealed class RemediationRunner(PowerShellRunner runner, AgentConfig? config = null)
{
    public async Task<RemediationReceipt> ExecuteAsync(RemediationAssignment job,CancellationToken ct)
    {
        var definition=RemediationEngine.Find(job.Code);
        var remaining=job.ExpiresAt-DateTime.UtcNow;
        if(job.Id==Guid.Empty||job.LeaseToken==Guid.Empty||definition==null||job.Version!=RemediationEngine.Version||remaining<=TimeSpan.Zero)
            return new(job.LeaseToken,false,"Rejected","Unknown catalog version, operation or expired assignment.","Not executed");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(remaining<TimeSpan.FromSeconds(definition.TimeoutSeconds)?remaining:TimeSpan.FromSeconds(definition.TimeoutSeconds));
        string pre="Not started",action="Not started";
        try
        {
            timeout.Token.ThrowIfCancellationRequested();
            var check=await runner.RunAsync(definition.Precheck,timeout.Token);pre=check.Output;
            if(check.ExitCode!=0)return new(job.LeaseToken,false,pre,"Not executed","Precheck failed");
            timeout.Token.ThrowIfCancellationRequested();
            var script=definition.Action;
            if(definition.Code=="PB-BIGFIX-REPAIR")
            {
                var hash=config?.BigFixReinstallSha256 ?? "";
                if(!System.Text.RegularExpressions.Regex.IsMatch(hash,"\\A[0-9a-fA-F]{64}\\z"))hash="";
                script=script.Replace("__WFI_BIGFIX_SHA256__",hash);
            }
            var result=await runner.RunAsync(script,timeout.Token);action=result.Output;
            if(result.ExitCode!=0)return new(job.LeaseToken,false,pre,action,"Action failed; inspect device before retry");
            timeout.Token.ThrowIfCancellationRequested();
            var verify=await runner.RunAsync(definition.Verify,timeout.Token);
            return new(job.LeaseToken,verify.ExitCode==0,pre,action,verify.Output);
        }
        catch(OperationCanceledException){return new(job.LeaseToken,false,pre,action,"Interrupted or timeout; inspect device before retry");}
        catch(Exception ex) when(ex is IOException or System.ComponentModel.Win32Exception){return new(job.LeaseToken,false,pre,action,"Runner unavailable; inspect device");}
    }
}
