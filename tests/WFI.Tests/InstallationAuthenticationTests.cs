using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using WFI.Infrastructure;
using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace WFI.Tests;
public sealed class InstallationAuthenticationTests
{
    static string Hash(string password) { var salt=RandomNumberGenerator.GetBytes(32);return "pbkdf2-sha256:600000:"+Convert.ToBase64String(salt)+":"+Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password,salt,600000,HashAlgorithmName.SHA256,32)); }
    [Fact] public void LocalAdminIsExplicitAndDoesNotFallBackToLdap() {
        var local=new LocalAdminOptions{Enabled=true,PasswordHash=Hash("Synthetic-only-passphrase!")};
        var auth=new InstallationAuthenticator(Options.Create(local),Options.Create(new LdapOptions{Enabled=false,Domain="synthetic.invalid"}));
        Assert.Null(auth.Authenticate("admin","Synthetic-only-passphrase!"));Assert.Null(auth.Authenticate("local:other","Synthetic-only-passphrase!"));Assert.Null(auth.Authenticate("local:admin","Wrong-but-long-enough"));
        var user=auth.Authenticate("local:admin","Synthetic-only-passphrase!");Assert.NotNull(user);Assert.True(user.IsInRole("Admin"));Assert.Equal("synthetic.invalid",user.FindFirstValue("domain"));Assert.Equal(local.CredentialVersion,user.FindFirstValue("local_credential_version"));
        local.Enabled=false;Assert.Null(auth.Authenticate("local:admin","Synthetic-only-passphrase!"));
    }
    [Theory][InlineData("")][InlineData("pbkdf2-sha256:99999999:AA==:AA==")][InlineData("pbkdf2-sha256:1:AA==:AA==")]
    public void MalformedAndUnboundedHashesAreRejected(string value){var options=new LocalAdminOptions{Enabled=true,PasswordHash=value};Assert.Throws<InvalidOperationException>(options.Validate);}
    [Fact] public void CredentialVersionChangesWhenPasswordChanges(){var o=new LocalAdminOptions{PasswordHash=Hash("Synthetic-password-one!")};var old=o.CredentialVersion;o.PasswordHash=Hash("Synthetic-password-two!");Assert.NotEqual(old,o.CredentialVersion);}
    [Theory][InlineData(true)][InlineData(false)]
    public async Task LocalLoginRequiresCsrfAndRevokesOldSession(bool disable)
    {
        using var basis=new TestApp();
        var local=new LocalAdminOptions{Enabled=true,PasswordHash=Hash("Synthetic-only-password!")};
        using var app=basis.WithWebHostBuilder(b=>b.ConfigureServices(s=>{
            s.RemoveAll<IAuthenticator>();s.AddSingleton<IAuthenticator,InstallationAuthenticator>();
            s.AddSingleton<IOptions<LocalAdminOptions>>(Options.Create(local));
            s.AddSingleton<IOptions<LdapOptions>>(Options.Create(new LdapOptions{Enabled=false,Domain="synthetic.invalid"}));
        }));
        using var client=app.CreateClient();
        var body=new{username="local:admin",password="Synthetic-only-password!"};
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/auth/login",body)).StatusCode);
        var token=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/auth/login",body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/auth/me")).StatusCode);
        if(disable)local.Enabled=false;else local.PasswordHash=Hash("Different-synthetic-password!");
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }
}
