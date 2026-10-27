using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;
namespace WFI.Tests;
public class BigFixConnectorTests
{
    private static string Xml(DateTime time)=>$"<BESAPI><Computer><Property Name='Computer Name'>synthetic</Property><Property Name='DNS Name'>synthetic.example.invalid</Property><Property Name='Last Report Time'>{time:R}</Property></Computer></BESAPI>";
    [Fact]
    public void ParserRejectsDtdAmbiguousIdentityAndFutureReports()
    {
        var now=DateTime.UtcNow;Assert.Equal("synthetic.example.invalid",BigFixConnector.Parse(10,Xml(now.AddHours(-1)),now).DnsName);
        Assert.Throws<System.Xml.XmlException>(()=>BigFixConnector.Parse(10,"<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///not-read'>]><BESAPI>&a;</BESAPI>",now));
        Assert.Throws<InvalidDataException>(()=>BigFixConnector.Parse(10,Xml(now.AddDays(1)),now));
        Assert.Throws<InvalidDataException>(()=>BigFixConnector.Parse(10,Xml(now).Replace("</Computer>","<Property Name='DNS Name'>other.example.invalid</Property></Computer>"),now));
        Assert.Throws<InvalidDataException>(()=>BigFixConnector.Parse(10,Xml(now).Replace("</Computer>","<ManagementExtension/></Computer>"),now));
    }
    private sealed class Connector:IBigFixConnector
    {
        public bool Configured=>true;public string Fingerprint=>"synthetic-fingerprint";
        public DateTime Report=DateTime.UtcNow.AddHours(-1);public bool Fail;
        public Task<BigFixComputer> ReadAsync(long id,CancellationToken ct)=>Fail?throw new HttpRequestException("synthetic outage"):Task.FromResult(new BigFixComputer(id,"synthetic","synthetic.example.invalid",Report,DateTime.UtcNow));
    }
    [Theory][InlineData("Executing")][InlineData("Success")]
    public async Task LateObservationCannotBecomePreRepairBaseline(string localStatus)
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Domain="example.invalid",Hostname="synthetic"};db.Devices.Add(device);
        var book=await db.Playbooks.SingleAsync(p=>p.Code=="PB-BIGFIX-REPAIR");
        var job=new RemediationExecution{Domain=device.Domain,DeviceId=device.Id,PlaybookId=book.Id,PlaybookCode=book.Code,Status=localStatus};db.RemediationExecutions.Add(job);
        db.BigFixVerifications.Add(new(){ExecutionId=job.Id,DeviceId=device.Id,Domain=device.Domain,ComputerId=10,ConnectorFingerprint="synthetic-fingerprint",ExpectedDnsName="synthetic.example.invalid"});await db.SaveChangesAsync();
        using var worker=new BigFixVerificationWorker(app.Services.GetRequiredService<IServiceScopeFactory>(),new Connector(),NullLogger<BigFixVerificationWorker>.Instance);
        Assert.True(await worker.ProcessOneAsync(default));db.ChangeTracker.Clear();var row=await db.BigFixVerifications.SingleAsync();
        Assert.Equal("BaselineUnavailable",row.Status);Assert.True(row.BaselineReady);Assert.Null(row.BeforeReportAt);Assert.Null(row.ObservedAt);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task VerificationRequiresNewReportAndOutageDoesNotBecomeClientFailure(bool outage)
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        var device=new Device{Domain="example.invalid",Hostname="synthetic"};db.Devices.Add(device);
        var book=await db.Playbooks.SingleAsync(p=>p.Code=="PB-BIGFIX-REPAIR");
        var job=new RemediationExecution{Domain=device.Domain,DeviceId=device.Id,PlaybookId=book.Id,PlaybookCode=book.Code,Status="Approved"};db.RemediationExecutions.Add(job);
        db.BigFixMappings.Add(new(){DeviceId=device.Id,Domain=device.Domain,ComputerId=10,ConnectorFingerprint="synthetic-fingerprint",VerifiedDnsName="synthetic.example.invalid"});await db.SaveChangesAsync();
        await using(var tx=await db.Database.BeginTransactionAsync()){Assert.False(await BigFixVerificationWorker.BaselineReadyAsync(db,job,device,default));await tx.CommitAsync();}
        var connector=new Connector();using var worker=new BigFixVerificationWorker(app.Services.GetRequiredService<IServiceScopeFactory>(),connector,NullLogger<BigFixVerificationWorker>.Instance);
        Assert.True(await worker.ProcessOneAsync(default));
        db.ChangeTracker.Clear();var row=await db.BigFixVerifications.SingleAsync();Assert.True(row.BaselineReady);Assert.Equal("BaselineCaptured",row.Status);
        await db.RemediationExecutions.Where(x=>x.Id==job.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"Success").SetProperty(x=>x.ExecutedAt,DateTime.UtcNow.AddMinutes(-1)).SetProperty(x=>x.CompletedAt,DateTime.UtcNow));
        await db.BigFixVerifications.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.NextCheckAt,DateTime.UtcNow.AddSeconds(-1)));
        connector.Fail=outage;await worker.ProcessOneAsync(default);await db.Entry(row).ReloadAsync();
        Assert.Equal(outage?"ConnectorUnavailable":"AwaitingReport",row.Status);
        connector.Fail=false;connector.Report=DateTime.UtcNow;
        await db.BigFixVerifications.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.NextCheckAt,DateTime.UtcNow.AddSeconds(-1)));
        await worker.ProcessOneAsync(default);await db.Entry(row).ReloadAsync();Assert.Equal("Verified",row.Status);
    }
}
