namespace WFI.Core;

public sealed record AgentSetupOptions(Uri Server,Guid DeviceId,string? TokenFile,bool Upgrade,string? BigFixHash)
{
    public static AgentSetupOptions Parse(string[] args)
    {
        var values=new Dictionary<string,string>(StringComparer.Ordinal);var upgrade=false;var silent=false;
        for(var i=0;i<args.Length;i++)
        {
            if(args[i]=="--silent"){if(silent)throw new ArgumentException();silent=true;continue;}
            if(args[i]=="--upgrade"){if(upgrade)throw new ArgumentException();upgrade=true;continue;}
            if(args[i] is not ("--server" or "--device-id" or "--token-file" or "--bigfix-sha256")||i+1>=args.Length||!values.TryAdd(args[i],args[++i]))throw new ArgumentException("Geçersiz parametre.");
        }
        if(!silent||!values.TryGetValue("--server",out var address)||!Uri.TryCreate(address,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||uri.AbsolutePath!="/"||uri.Query.Length>0||uri.Fragment.Length>0)throw new ArgumentException("HTTPS kök adresi gerekli.");
        if(!values.TryGetValue("--device-id",out var id)||!Guid.TryParse(id,out var device)||device==Guid.Empty)throw new ArgumentException("DeviceId gerekli.");
        values.TryGetValue("--token-file",out var file);values.TryGetValue("--bigfix-sha256",out var hash);
        if(!upgrade&&string.IsNullOrWhiteSpace(file))throw new ArgumentException("Token dosyası gerekli.");
        if(hash!=null&&(hash.Length!=64||!hash.All(Uri.IsHexDigit)))throw new ArgumentException("Geçersiz SHA256.");
        return new(uri,device,file,upgrade,hash);
    }
}
