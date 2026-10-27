using System.Diagnostics;
using System.Diagnostics.Metrics;
namespace WFI.Infrastructure;

public static class WfiTelemetry
{
    public const string Name="WFI.Operations";
    public static readonly ActivitySource Traces=new(Name);
    public static readonly Meter Meter=new(Name);
    public static readonly Histogram<double> CheckInSeconds=Meter.CreateHistogram<double>("wfi.agent.checkin.duration","s");
    public static readonly Histogram<double> ModelSeconds=Meter.CreateHistogram<double>("wfi.ai.duration","s");
    public static readonly Counter<long> ModelFailures=Meter.CreateCounter<long>("wfi.ai.failures");
    public static readonly Counter<long> ModelBusy=Meter.CreateCounter<long>("wfi.ai.capacity.busy");
    public static readonly Histogram<double> QueueAge=Meter.CreateHistogram<double>("wfi.diagnostic.queue.age","s");
}
