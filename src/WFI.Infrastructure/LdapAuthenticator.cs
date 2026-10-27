using System.DirectoryServices.Protocols;
using System.Net;
using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace WFI.Infrastructure;
public sealed class LdapOptions
{
    public string Domain { get; set; } = "example.invalid";
    public string Server { get; set; } = "";
    public int Port { get; set; } = 636;
    public string BaseDn { get; set; } = "DC=example,DC=invalid";
    public int TimeoutSeconds { get; set; } = 10;
    public Dictionary<string, string> GroupRoles { get; set; } = [];
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Domain) || string.IsNullOrWhiteSpace(BaseDn) || Port is < 1 or > 65535 || TimeoutSeconds is < 1 or > 30)
            throw new InvalidOperationException("LDAP Server, Domain, BaseDn, Port ve TimeoutSeconds ayarlarını kontrol edin.");
        if (GroupRoles.Count == 0 || GroupRoles.Any(x => string.IsNullOrWhiteSpace(x.Key) || !new[] { "Viewer", "Analyst", "Admin", "ConnectorAdmin" }.Contains(x.Value)))
            throw new InvalidOperationException("LDAP GroupRoles tam grup DN → Viewer/Analyst/Admin/ConnectorAdmin eşlemesi gerektirir.");
    }
}
public interface IAuthenticator { ClaimsPrincipal? Authenticate(string username, string password); }
public sealed class LdapAuthenticator(IOptions<LdapOptions> options) : IAuthenticator
{
    public static string? SecurityGroupRole(string? groupType,string configuredRole)
    {
        if(!long.TryParse(groupType,NumberStyles.Integer,CultureInfo.InvariantCulture,out var flags)||flags<int.MinValue||flags>uint.MaxValue)return null;
        return (flags&0x80000000L)!=0?configuredRole:null;
    }
    public static string EscapeFilter(string value) => value.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
    public static string? NormalizeUser(string username, string domain)
    {
        var value = username.Trim();
        if (value.Contains('\\')) return null; // UPN or sAMAccountName only; no ambiguous NetBIOS mapping.
        var parts = value.Split('@');
        if (parts.Length > 2 || (parts.Length == 2 && !parts[1].Equals(domain, StringComparison.OrdinalIgnoreCase))) return null;
        var local = parts[0];
        return local.Length is > 0 and <= 128 && !local.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || "*()\\/".Contains(c)) ? local + "@" + domain : null;
    }
    public ClaimsPrincipal? Authenticate(string username, string password)
    {
        var o = options.Value; o.Validate();
        var upn = NormalizeUser(username, o.Domain);
        if (upn == null || string.IsNullOrWhiteSpace(password) || password.Length > 1024) return null;
        using var connection = new LdapConnection(new LdapDirectoryIdentifier(o.Server, o.Port), new NetworkCredential(upn, password), AuthType.Basic);
        connection.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = true;
        connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
        // OS trust and hostname validation remain enabled. Never accept arbitrary certificates.
        try
        {
            connection.Bind();
            var filter = $"(&(objectCategory=person)(objectClass=user)(userPrincipalName={EscapeFilter(upn)})(!(userAccountControl:1.2.840.113556.1.4.803:=2)))";
            var request = new SearchRequest(o.BaseDn, filter, SearchScope.Subtree, "displayName", "objectGUID");
            request.SizeLimit = 2;
            var result = (SearchResponse)connection.SendRequest(request);
            if (result.Entries.Count != 1) return null;
            var entry = result.Entries[0];
            var roles = new HashSet<string>();
            foreach (var group in o.GroupRoles)
            {
                // AD transitive membership, including nested security groups. Primary groups deliberately do not grant access.
                var groupRequest = new SearchRequest(group.Key, $"(&(objectClass=group)(member:1.2.840.113556.1.4.1941:={EscapeFilter(entry.DistinguishedName)}))", SearchScope.Base, "groupType");
                var membership=(SearchResponse)connection.SendRequest(groupRequest);
                if(membership.Entries.Count==1)
                {
                    var attribute=membership.Entries[0].Attributes["groupType"];
                    var type=attribute?.Count==1?attribute.GetValues(typeof(string))[0] as string:null;
                    var role=SecurityGroupRole(type,group.Value);
                    if(role!=null)roles.Add(role);
                }
            }
            if (roles.Count == 0) return null;
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, new Guid((byte[])entry.Attributes["objectGUID"][0]).ToString()), new(ClaimTypes.Name, upn), new("domain", o.Domain.ToLowerInvariant()) };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
            return new(new ClaimsIdentity(claims, "Cookies"));
        }
        catch (LdapException e) when (e.ErrorCode == 49) { return null; }
    }
}
