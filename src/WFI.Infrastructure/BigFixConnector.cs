using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using WFI.Core;
namespace WFI.Infrastructure;

public interface IBigFixConnector
{
    bool Configured {get;}
    string Fingerprint {get;}
    Task<BigFixComputer> ReadAsync(long computerId,CancellationToken ct);
}
public sealed class BigFixConnector(IHttpClientFactory clients,IConfiguration config):IBigFixConnector
{
    private Uri? Endpoint
    {
        get
        {
            var raw=config["BigFix:BaseUrl"];
            if(!Uri.TryCreate(raw,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||uri.AbsolutePath!="/"||uri.Query.Length>0||uri.Fragment.Length>0)return null;
            var allowed=config.GetSection("BigFix:AllowedOrigins").Get<string[]>()??[];
            return allowed.Any(x=>Uri.TryCreate(x,UriKind.Absolute,out var permitted)&&permitted==uri)?uri:null;
        }
    }
    public bool Configured=>Endpoint!=null&&!string.IsNullOrWhiteSpace(config["BigFix:Username"])&&!string.IsNullOrWhiteSpace(config["BigFix:Password"])&&!config["BigFix:Username"]!.Contains(':');
    public string Fingerprint=>Endpoint==null?"":Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Endpoint.AbsoluteUri)));
    public async Task<BigFixComputer> ReadAsync(long computerId,CancellationToken ct)
    {
        if(!Configured||computerId<=0)throw new InputRejectedException("BigFix connector yapılandırılmadı veya kimlik geçersiz.");
        using var client=clients.CreateClient("bigfix");using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(Endpoint!,"api/computer/"+computerId.ToString(CultureInfo.InvariantCulture)));
        request.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(config["BigFix:Username"]+":"+config["BigFix:Password"])));
        using var response=await client.SendAsync(request,timeout.Token);response.EnsureSuccessStatusCode();
        var xml=await response.Content.ReadAsStringAsync(timeout.Token);
        return Parse(computerId,xml,DateTime.UtcNow);
    }
    public static BigFixComputer Parse(long computerId,string xml,DateTime observedAt)
    {
        if(xml.Length>131072)throw new InvalidDataException("BigFix response exceeds limit.");
        using var text=new StringReader(xml);using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=131072});
        var doc=XDocument.Load(reader);var computers=doc.Root?.Elements().Where(x=>x.Name.LocalName=="Computer").ToArray()??[];
        if(computers.Length!=1||computers[0].Elements().Any(x=>x.Name.LocalName=="ManagementExtension"))throw new InvalidDataException("Ambiguous BigFix identity.");
        string Property(string name)
        {
            var values=computers[0].Elements().Where(x=>x.Name.LocalName=="Property"&&(string?)x.Attribute("Name")==name).ToArray();
            if(values.Length!=1||values[0].Value.Length>256)throw new InvalidDataException("Missing or ambiguous BigFix property.");
            return values[0].Value.Trim();
        }
        if(!DateTimeOffset.TryParse(Property("Last Report Time"),CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var last)||last.UtcDateTime>observedAt.AddMinutes(5))throw new InvalidDataException("Invalid BigFix report time.");
        return new(computerId,Property("Computer Name"),Property("DNS Name").TrimEnd('.').ToLowerInvariant(),last.UtcDateTime,observedAt);
    }
}
