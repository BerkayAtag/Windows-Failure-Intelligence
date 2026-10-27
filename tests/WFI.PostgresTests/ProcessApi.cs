using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Xunit;

namespace WFI.PostgresTests;

internal sealed class ProcessApi:IDisposable
{
    private readonly Process process;
    private readonly Task output;
    private readonly Task error;
    public Uri Address {get;}
    public int Id=>process.Id;
    public ProcessApi(string connectionString,string root,string keys)
    {
        var runtime=Environment.GetEnvironmentVariable("WFI_TEST_DOTNET")??throw new InvalidOperationException("Use scripts/Test-Postgres.ps1 to select the test runtime.");
        var repository=new DirectoryInfo(AppContext.BaseDirectory);
        while(repository!=null&&!File.Exists(Path.Combine(repository.FullName,"WFI.slnx")))repository=repository.Parent;
        Assert.NotNull(repository);
        var dll=Path.Combine(repository.FullName,"src","WFI.Api","bin","Release","net10.0","WFI.Api.dll");
        Assert.True(File.Exists(dll));Directory.CreateDirectory(root);
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        Address=new Uri($"http://127.0.0.1:{port}");
        var start=new ProcessStartInfo(runtime){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(dll);
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";
        start.Environment["ASPNETCORE_URLS"]=Address.ToString();
        start.Environment["ASPNETCORE_CONTENTROOT"]=root;
        start.Environment["Demo__Enabled"]="true";
        start.Environment["HighAvailability__Enabled"]="false";
        start.Environment["Worker__Enabled"]="false";
        start.Environment["ConnectionStrings__Wfi"]=connectionString;
        start.Environment["DataProtection__Path"]=keys;
        start.Environment["DataProtection__CertificateThumbprint"]="";
        start.Environment["Storage__Path"]=Path.Combine(root,"logs");
        start.Environment["Ldap__Domain"]="synthetic.example";
        start.Environment["AllowedHosts"]="127.0.0.1;localhost";
        start.Environment["Logging__LogLevel__Default"]="None";
        process=Process.Start(start)??throw new InvalidOperationException("Test API could not start.");
        output=process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        error=process.StandardError.BaseStream.CopyToAsync(Stream.Null);
    }
    public async Task WaitReady()
    {
        using var client=new HttpClient{BaseAddress=Address,Timeout=TimeSpan.FromSeconds(2)};
        var deadline=DateTime.UtcNow.AddSeconds(30);
        while(DateTime.UtcNow<deadline)
        {
            if(process.HasExited)throw new InvalidOperationException("Test API process exited before readiness.");
            try{using var response=await client.GetAsync("/api/v1/ready");if(response.IsSuccessStatusCode)return;}
            catch(HttpRequestException){}catch(TaskCanceledException){}
            await Task.Delay(200);
        }
        throw new TimeoutException("Test API readiness deadline exceeded.");
    }
    public void Stop()
    {
        if(!process.HasExited){process.Kill(true);if(!process.WaitForExit(10000))throw new TimeoutException("Test API did not stop.");}
        Task.WhenAll(output,error).GetAwaiter().GetResult();
    }
    public void Dispose(){Stop();process.Dispose();}
    private static async Task<string> DemoLogin(HttpClient client)
    {
        async Task Csrf(){client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");var token=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());}
        await Csrf();using var response=await client.PostAsync("/api/v1/auth/demo",null);response.EnsureSuccessStatusCode();await Csrf();
        return response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("WFI.Session=",StringComparison.Ordinal)).Split(';')[0];
    }
    public static async Task Verify(string connectionString)
    {
        var root=Path.Combine(Path.GetTempPath(),"wfi-process-test-"+Guid.NewGuid().ToString("N"));
        try
        {
            using var first=new ProcessApi(connectionString,Path.Combine(root,"first"),Path.Combine(root,"keys"));await first.WaitReady();
            using var firstClient=new HttpClient{BaseAddress=first.Address,Timeout=TimeSpan.FromSeconds(10)};
            var cookie=await DemoLogin(firstClient);
            using var second=new ProcessApi(connectionString,Path.Combine(root,"second"),Path.Combine(root,"keys"));await second.WaitReady();
            Assert.NotEqual(first.Id,second.Id);
            using var secondClient=new HttpClient(new HttpClientHandler{UseCookies=false}){BaseAddress=second.Address,Timeout=TimeSpan.FromSeconds(10)};
            secondClient.DefaultRequestHeaders.Add("Cookie",cookie);
            Assert.Equal(HttpStatusCode.OK,(await secondClient.GetAsync("/api/v1/auth/me")).StatusCode);
            (await firstClient.PostAsync("/api/v1/auth/logout",null)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized,(await secondClient.GetAsync("/api/v1/auth/me")).StatusCode);
            cookie=await DemoLogin(firstClient);secondClient.DefaultRequestHeaders.Remove("Cookie");secondClient.DefaultRequestHeaders.Add("Cookie",cookie);
            await using var proxy=await TestProxy.Start(first.Address,second.Address);
            using var balanced=new HttpClient{BaseAddress=proxy.Address,Timeout=TimeSpan.FromSeconds(5)};
            await DemoLogin(balanced);
            var backends=new HashSet<string>();
            for(var i=0;i<8;i++)
            {
                using var response=await balanced.GetAsync("/api/v1/auth/me");response.EnsureSuccessStatusCode();
                backends.Add(response.Headers.GetValues("X-WFI-Test-Backend").Single());
            }
            Assert.Contains(first.Address.Port.ToString(),backends);Assert.Contains(second.Address.Port.ToString(),backends);
            first.Stop();
            await Assert.ThrowsAsync<HttpRequestException>(()=>firstClient.GetAsync("/api/v1/health"));
            Assert.Equal(HttpStatusCode.OK,(await secondClient.GetAsync("/api/v1/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await secondClient.GetAsync("/api/v1/automation")).StatusCode);
            var deadline=DateTime.UtcNow.AddSeconds(15);var consecutive=0;
            while(DateTime.UtcNow<deadline&&consecutive<10)
            {
                using var response=await balanced.GetAsync("/api/v1/auth/me");
                if(response.StatusCode==HttpStatusCode.OK)
                {
                    Assert.Equal(second.Address.Port.ToString(),response.Headers.GetValues("X-WFI-Test-Backend").Single());consecutive++;
                }
                else{Assert.Contains(response.StatusCode,new[]{HttpStatusCode.BadGateway,HttpStatusCode.ServiceUnavailable});consecutive=0;}
                await Task.Delay(200);
            }
            Assert.Equal(10,consecutive);
            (await balanced.PostAsync("/api/v1/auth/logout",null)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized,(await balanced.GetAsync("/api/v1/auth/me")).StatusCode);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
