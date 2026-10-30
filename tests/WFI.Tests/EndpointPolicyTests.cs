using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WFI.Core;
using WFI.Infrastructure;
using Xunit;

namespace WFI.Tests;
public class EndpointPolicyTests
{
    [Fact]
    public async Task EveryEndpointPolicyIsRegistered()
    {
        // An unknown policy name only fails at request time (HTTP 503), so check the whole route table.
        using var app=new TestApp();using var client=app.CreateClient();
        var provider=app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policies=app.Services.GetServices<EndpointDataSource>().SelectMany(s=>s.Endpoints)
            .SelectMany(e=>e.Metadata.GetOrderedMetadata<IAuthorizeData>()).Select(a=>a.Policy).OfType<string>().Distinct().ToArray();
        Assert.NotEmpty(policies);
        foreach(var policy in policies)Assert.True(await provider.GetPolicyAsync(policy)!=null,$"Unregistered authorization policy: {policy}");
    }

    [Fact]
    public async Task EveryParameterlessReadEndpointAnswersWithoutServerError()
    {
        using var app=new TestApp();using var client=app.CreateClient();
        await Login(client,"admin");
        var routes=app.Services.GetServices<EndpointDataSource>().SelectMany(s=>s.Endpoints).OfType<RouteEndpoint>()
            .Where(e=>e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET")==true)
            .Select(e=>"/"+e.RoutePattern.RawText!.TrimStart('/')).Where(r=>r.StartsWith("/api/v1/")&&!r.Contains('{')).Distinct().ToArray();
        Assert.True(routes.Length>15);
        // The agent package is intentionally 503 until Build.ps1 produced it; everything else must not fail server-side.
        var failures=new List<string>();
        foreach(var route in routes.Where(r=>!r.StartsWith("/api/v1/distribution/")))
        {
            using var response=await client.GetAsync(route);
            if((int)response.StatusCode>=500)failures.Add($"{route} -> {(int)response.StatusCode}");
        }
        Assert.Empty(failures);
    }

    [Theory][InlineData("analyst",HttpStatusCode.OK)][InlineData("admin",HttpStatusCode.OK)][InlineData("viewer",HttpStatusCode.Forbidden)]
    public async Task OperatorFeedbackIsRecordedForAnalystsAndAdmins(string user,HttpStatusCode expected)
    {
        using var app=new TestApp();using var client=app.CreateClient();
        var analysis=new AiAnalysisJob{Domain="example.invalid",Hostname="feedback-pc",Status="completed",Conclusion="Synthetic"};
        using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<WfiDb>();db.AiAnalysisJobs.Add(analysis);await db.SaveChangesAsync();}
        await Login(client,user);
        var response=await client.PostAsJsonAsync($"/api/v1/ai/analysis/{analysis.Id}/feedback",new{outcome="Resolved"});
        Assert.Equal(expected,response.StatusCode);
        Assert.Equal(expected==HttpStatusCode.OK?HttpStatusCode.NotFound:HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync($"/api/v1/ai/analysis/{Guid.NewGuid()}/feedback",new{outcome="Resolved"})).StatusCode);
        using var check=app.Services.CreateScope();
        Assert.Equal(expected==HttpStatusCode.OK?1:0,await check.ServiceProvider.GetRequiredService<WfiDb>().AiFeedbacks.CountAsync(f=>f.AnalysisId==analysis.Id&&f.Outcome=="Resolved"));
    }

    private static async Task Login(HttpClient client,string user)
    {
        async Task Csrf(){var token=await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());}
        await Csrf();
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/auth/login",new{username=user,password="test-password"})).StatusCode);
        await Csrf();
    }
}
