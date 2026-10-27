using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WFI.Infrastructure;
using Xunit;
namespace WFI.Tests;
public sealed class AuditScopeTests
{
    [Fact]
    public async Task AdminAuditDoesNotDiscloseOtherDomainsOrUnscopedLegacyRows()
    {
        using var app=new TestApp();using var client=app.CreateClient();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WfiDb>();
        db.Audit.AddRange(new(){Domain="example.invalid",Actor="synthetic",Action="own.marker",Target="own"},new(){Domain="foreign.invalid",Actor="synthetic",Action="foreign.marker",Target="foreign"},new(){Actor="synthetic",Action="legacy.marker",Target="unassigned"});await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/audit")).StatusCode);
        var csrf=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());
        (await client.PostAsJsonAsync("/api/v1/auth/login",new{username="admin",password="test-password"})).EnsureSuccessStatusCode();
        var response=await client.GetStringAsync("/api/v1/audit");Assert.Contains("own.marker",response);Assert.DoesNotContain("foreign.marker",response);Assert.DoesNotContain("legacy.marker",response);
        Assert.True(await db.Audit.AnyAsync(x=>x.Action=="login.success"&&x.Domain=="example.invalid"));
    }
}
