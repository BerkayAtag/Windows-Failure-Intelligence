using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WFI.Infrastructure;
public sealed class LocalAdminOptions
{
    public bool Enabled { get; set; }
    public string PasswordHash { get; set; } = "";
    public void Validate()
    {
        if(Enabled && !TryParse(PasswordHash,out _,out _,out _))
            throw new InvalidOperationException("Local administrator password hash is invalid.");
    }
    public static bool TryParse(string value,out int iterations,out byte[] salt,out byte[] hash)
    {
        iterations=0;salt=[];hash=[];
        if(string.IsNullOrEmpty(value)||value.Length>512)return false;
        var parts=value.Split(':');
        if(parts.Length!=4||parts[0]!="pbkdf2-sha256"||!int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out iterations)||iterations is <600000 or >1000000)return false;
        try{salt=Convert.FromBase64String(parts[2]);hash=Convert.FromBase64String(parts[3]);return salt.Length==32&&hash.Length==32;}catch(FormatException){return false;}
    }
    public string CredentialVersion => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PasswordHash)));
}
public sealed class InstallationAuthenticator(IOptions<LocalAdminOptions> local,IOptions<LdapOptions> ldap):IAuthenticator
{
    public ClaimsPrincipal? Authenticate(string username,string password)
    {
        if(username.Trim().StartsWith("local:",StringComparison.OrdinalIgnoreCase))
        {
            var o=local.Value;
            if(!username.Trim().Equals("local:admin",StringComparison.OrdinalIgnoreCase)||!o.Enabled||password.Length is <14 or >1024||!LocalAdminOptions.TryParse(o.PasswordHash,out var iterations,out var salt,out var expected))return null;
            var actual=Rfc2898DeriveBytes.Pbkdf2(password,salt,iterations,HashAlgorithmName.SHA256,32);
            try {
                if(!CryptographicOperations.FixedTimeEquals(actual,expected))return null;
                return new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"wfi-local-admin"),new Claim(ClaimTypes.Name,"local:admin"),new Claim("domain",ldap.Value.Domain.ToLowerInvariant()),new Claim("local_credential_version",o.CredentialVersion),new Claim(ClaimTypes.Role,"Admin"),new Claim(ClaimTypes.Role,"Analyst"),new Claim(ClaimTypes.Role,"Viewer"),new Claim(ClaimTypes.Role,"ConnectorAdmin")},"Cookies"));
            }finally{CryptographicOperations.ZeroMemory(actual);}
        }
        return ldap.Value.Enabled?new LdapAuthenticator(ldap).Authenticate(username,password):null;
    }
}
