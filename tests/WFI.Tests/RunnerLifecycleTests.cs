using System.Diagnostics;
using WFI.Agent;
using WFI.Core;
using Xunit;

namespace WFI.Tests;
[CollectionDefinition("Native process lifecycle", DisableParallelization = true)]
public sealed class NativeProcessLifecycleCollection { }
[Collection("Native process lifecycle")]
public class RunnerLifecycleTests
{
    private sealed class WaitUntilCancelled:PowerShellRunner
    {
        public int Calls;
        public override async Task<CommandResult> RunAsync(string script,CancellationToken ct)
        {
            Calls++;
            await Task.Delay(Timeout.InfiniteTimeSpan,ct);
            return new(0,"Unexpected completion");
        }
    }
    [Fact]
    public async Task AssignmentDeadlineInterruptsPrecheckBeforeCatalogTimeoutAndNeverStartsAction()
    {
        var shell=new WaitUntilCancelled();
        using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var result=await new RemediationRunner(shell).ExecuteAsync(new(Guid.NewGuid(),Guid.NewGuid(),"PB-DNS-FLUSH",RemediationEngine.Version,DateTime.UtcNow.AddSeconds(1)),guard.Token);
        Assert.False(guard.IsCancellationRequested);
        Assert.False(result.Success);Assert.Equal(1,shell.Calls);
        Assert.Contains("Interrupted or timeout",result.Verification);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task MissingAssignmentIdentityNeverStartsShell(bool missingJob)
    {
        var shell=new NeverRun();
        var result=await new RemediationRunner(shell).ExecuteAsync(new(missingJob?Guid.Empty:Guid.NewGuid(),missingJob?Guid.NewGuid():Guid.Empty,"PB-DNS-FLUSH",RemediationEngine.Version,DateTime.UtcNow.AddMinutes(1)),default);
        Assert.False(result.Success);Assert.Equal(0,shell.Calls);
    }
    [Fact]
    public async Task AlreadyCancelledRequestDoesNotLaunchCommand()
    {
        if(!OperatingSystem.IsWindows())return;
        var marker=Path.Combine(Path.GetTempPath(),"wfi-cancel-"+Guid.NewGuid().ToString("N")+".txt");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new PowerShellRunner().RunAsync($"[IO.File]::WriteAllText('{marker.Replace("'","''")}', 'started')",cancel.Token));
            Assert.False(File.Exists(marker));
        }
        finally{File.Delete(marker);}
    }
    [Fact]
    public async Task CancellationTerminatesRunningShellBeforeReturning()
    {
        if(!OperatingSystem.IsWindows())return;
        var marker=Path.Combine(Path.GetTempPath(),"wfi-process-"+Guid.NewGuid().ToString("N")+".txt");
        // Startup on a cold shared runner is separate from the cancellation deadline.
        // Publish the PID atomically: file existence must mean a complete PID is readable.
        var quoted=marker.Replace("'","''");
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var task=new PowerShellRunner().RunAsync($"[IO.File]::WriteAllText('{quoted}.tmp', [string]$PID); [IO.File]::Move('{quoted}.tmp','{quoted}'); Start-Sleep -Seconds 120",cancel.Token);
        try
        {
            var startup=Stopwatch.StartNew();
            while(!File.Exists(marker)&&startup.Elapsed<TimeSpan.FromSeconds(60)&&!task.IsCompleted)await Task.Delay(50);
            Assert.True(File.Exists(marker),"Synthetic shell did not start within the test window.");
            var pid=int.Parse(await File.ReadAllTextAsync(marker));
            using var process=Process.GetProcessById(pid);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(process.HasExited);
        }
        finally
        {
            cancel.Cancel();
            try{await task.WaitAsync(TimeSpan.FromSeconds(5));}catch(OperationCanceledException){}
            File.Delete(marker);File.Delete(marker+".tmp");
        }
    }
    [Fact]
    public async Task LargeStdoutAndStderrAreDrainedButOnlyBoundedOutputIsRetained()
    {
        if(!OperatingSystem.IsWindows())return;
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var result=await new PowerShellRunner().RunAsync("[Console]::Out.Write(('x' * 100000)); [Console]::Error.Write(('y' * 100000))",cancel.Token);
        Assert.Equal(0,result.ExitCode);Assert.InRange(result.Output.Length,1,3601);
        Assert.Contains('x',result.Output);Assert.Contains('y',result.Output);
    }
    private sealed class NeverRun:PowerShellRunner
    {
        public int Calls;
        public override Task<CommandResult> RunAsync(string script,CancellationToken ct){Calls++;throw new InvalidOperationException("Invalid assignment reached shell.");}
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ExpiredOrWrongVersionAssignmentNeverRuns(bool expired)
    {
        var shell=new NeverRun();
        var result=await new RemediationRunner(shell).ExecuteAsync(new(Guid.NewGuid(),Guid.NewGuid(),"PB-DNS-FLUSH",expired?RemediationEngine.Version:"invalid-version",DateTime.UtcNow.AddMinutes(expired?-1:1)),default);
        Assert.False(result.Success);Assert.Equal(0,shell.Calls);
    }
}
