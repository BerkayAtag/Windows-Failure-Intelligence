namespace WFI.Core;

// No server hint can create an unbounded delay or a tight retry loop.
public sealed class AgentPolling(int minimumSeconds)
{
    private int failures;
    private int idle;
    public int Success(bool busy,int serverIdleSeconds)
    {
        failures=0;
        idle=busy?0:Math.Min(idle+1,5);
        var baseline=Math.Clamp(minimumSeconds,10,600);
        return busy?baseline:Math.Min(Math.Max(baseline,Math.Clamp(serverIdleSeconds,10,600)),baseline*(1<<idle));
    }
    public int Failure(int? retryAfterSeconds=null)
    {
        failures=Math.Min(failures+1,6);
        return Math.Clamp(Math.Max(Math.Clamp(minimumSeconds,10,600)*(1<<failures),retryAfterSeconds??0),10,3600);
    }
}
