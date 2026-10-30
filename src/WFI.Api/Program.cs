using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WFI.Core;
using WFI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false).AddEnvironmentVariables();
bool dev = builder.Environment.IsDevelopment();
bool demo = dev && builder.Configuration.GetValue<bool>("Demo:Enabled");
_ = DiagnosticAdmission.Limit(builder.Configuration);
var trustedProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownIPNetworks.Clear(); o.KnownProxies.Clear();
    foreach (var address in trustedProxies) o.KnownProxies.Add(System.Net.IPAddress.Parse(address));
});
builder.Services.Configure<LdapOptions>(builder.Configuration.GetSection("Ldap"));
builder.Services.AddWfiDatabase(builder.Configuration, dev);
builder.Services.Configure<LocalAdminOptions>(builder.Configuration.GetSection("LocalAdmin"));
builder.Services.AddSingleton<IAuthenticator, InstallationAuthenticator>();
builder.Services.AddScoped<AgentCoordinator>();
builder.Services.AddScoped<RemediationCoordinator>();
builder.Services.AddSingleton<IOllamaService, OllamaService>();
builder.Services.AddWfiTelemetry(builder.Configuration);
builder.Services.AddSingleton<IBigFixConnector,BigFixConnector>();
builder.Services.AddHttpClient("bigfix",c=>{c.Timeout=TimeSpan.FromSeconds(10);c.MaxResponseContentBufferSize=131072;})
    .ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});

builder.Services.AddHttpClient("ollama", c => { c.Timeout=TimeSpan.FromSeconds(250); c.MaxResponseContentBufferSize=131072; }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect=false });
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.SecurePolicy = dev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Strict; });
var keys = builder.Services.AddDataProtection().SetApplicationName("WFI").PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:Path"] ?? ".data/keys"));
var keyCertificate = builder.Configuration["DataProtection:CertificateThumbprint"];
if (!string.IsNullOrWhiteSpace(keyCertificate)) keys.ProtectKeysWithCertificate(keyCertificate);
if (builder.Configuration.GetValue<bool>("HighAvailability:Enabled"))
{
    if (dev || string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Wfi")) ||
        string.IsNullOrWhiteSpace(keyCertificate) ||
        !Path.IsPathFullyQualified(builder.Configuration["DataProtection:Path"] ?? "") ||
        !Path.IsPathFullyQualified(builder.Configuration["Storage:Path"] ?? ""))
        throw new InvalidOperationException("HA için Production, PostgreSQL, mutlak ortak storage/key yolları ve key koruma sertifikası zorunludur.");
}
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "WFI.Session"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = dev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30); o.SlidingExpiration = false;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = async c =>
    {
        var localVersion = c.Principal?.FindFirstValue("local_credential_version");
        var localOptions = c.HttpContext.RequestServices.GetRequiredService<IOptions<LocalAdminOptions>>().Value;
        if(localVersion != null && (!localOptions.Enabled || localVersion != localOptions.CredentialVersion)) { c.RejectPrincipal(); return; }
        var sid = c.Principal?.FindFirstValue("session_id");
        var db = c.HttpContext.RequestServices.GetRequiredService<WfiDb>();
        if (sid == null || await db.Audit.AnyAsync(a => a.Action == "logout" && a.Target == sid, c.HttpContext.RequestAborted))
            c.RejectPrincipal();
    };
});
builder.Services.AddAuthorization(o => { o.AddPolicy("Analyze", p => p.RequireRole("Analyst", "Admin")); o.AddPolicy("Admin", p => p.RequireRole("Admin")); o.AddPolicy("ConnectorAdmin", p => p.RequireRole("ConnectorAdmin")); });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.OnRejected=(context,ct)=>{context.HttpContext.Response.Headers.RetryAfter="60";return ValueTask.CompletedTask;};
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("upload", c => RateLimitPartition.GetFixedWindowLimiter(c.User.Identity?.Name ?? "unknown", _ => new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("agent", c => RateLimitPartition.GetTokenBucketLimiter(c.Items["agent-device"] is Device device ? "device:"+device.Id : "ip:"+(c.Connection.RemoteIpAddress?.ToString() ?? "unknown"), _ => new TokenBucketRateLimiterOptions { TokenLimit = 100, QueueLimit = 0, ReplenishmentPeriod = TimeSpan.FromMinutes(1), TokensPerPeriod = 100 }));
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = LogAnalyzer.MaxUpload + 64 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = LogAnalyzer.MaxUpload + 64 * 1024);
if (builder.Configuration.GetValue("Worker:Enabled", true) && !args.Contains("--migrate")) { builder.Services.AddHostedService<AnalysisWorker>(); builder.Services.AddHostedService<DiagnosticJobProcessor>(); builder.Services.AddHostedService<CampaignSupervisor>(); builder.Services.AddHostedService<BigFixVerificationWorker>(); builder.Services.AddHostedService<AiQualityWorker>(); }
var app = builder.Build();
demo = dev && app.Configuration.GetValue<bool>("Demo:Enabled");
if (!dev && app.Configuration.GetValue<bool>("Demo:Enabled")) throw new InvalidOperationException("Demo production ortamında kullanılamaz.");
if (!demo && !args.Contains("--migrate")) {
    var ldapOptions=app.Services.GetRequiredService<IOptions<LdapOptions>>().Value;
    var localOptions=app.Services.GetRequiredService<IOptions<LocalAdminOptions>>().Value;
    localOptions.Validate();
    if(ldapOptions.Enabled)ldapOptions.Validate();
    if(!ldapOptions.Enabled&&!localOptions.Enabled)throw new InvalidOperationException("Enable LDAP or a configured local administrator.");
    if(string.IsNullOrWhiteSpace(ldapOptions.Domain))throw new InvalidOperationException("A device domain is required.");
}
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<WfiDb>().Database.MigrateAsync();
    return;
}
if (dev) { using var scope = app.Services.CreateScope(); await DevelopmentSchema.EnsureAsync(scope.ServiceProvider.GetRequiredService<WfiDb>()); }
if (trustedProxies.Length > 0) app.UseForwardedHeaders();
if (!dev) { app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (Exception ex) when (ex is InputRejectedException or InvalidDataException or BadHttpRequestException)
    { ctx.Response.StatusCode = ex is BadHttpRequestException bad ? bad.StatusCode : 400; await ctx.Response.WriteAsJsonAsync(new { error = ex is InputRejectedException ? ex.Message : "Geçersiz istek veya dosya." }); }
    catch (Exception ex) { app.Logger.LogError("Request failed ({Type}); trace {Trace}", ex.GetType().Name, ctx.TraceIdentifier); ctx.Response.StatusCode = 503; await ctx.Response.WriteAsJsonAsync(new { error = "İşlem tamamlanamadı. Servis durumunu kontrol edin.", traceId = ctx.TraceIdentifier }); }
});
app.UseDefaultFiles(); app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization();
app.Use(AgentEndpoints.AuthenticateAgentAsync); app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api") && !ctx.Request.Path.StartsWithSegments("/api/v1/agent") && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method))
    {
        try { await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx); }
        catch (AntiforgeryValidationException) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { error = "Oturum doğrulaması geçersiz. Sayfayı yenileyin." }); return; }
    }
    await next();
});
var api = app.MapGroup("/api/v1");

// Health & readiness
api.MapGet("/health", () => Results.Ok(new { status = "alive" }));
api.MapGet("/ready", async (WfiDb db, HttpContext c) =>
{
    try {
        await db.Jobs.AsNoTracking().Select(j => j.Id).Take(1).ToListAsync(c.RequestAborted);
        await db.AiAnalysisJobs.AsNoTracking().Select(j=>new {j.LearningContextJson,j.VersionsJson}).Take(1).ToListAsync(c.RequestAborted);
        await db.RemediationExecutions.AsNoTracking().Select(r=>new {r.LearningExcluded,r.CampaignId,r.CampaignPilot}).Take(1).ToListAsync(c.RequestAborted);
        await db.Audit.AsNoTracking().Select(x=>x.Domain).Take(1).ToListAsync(c.RequestAborted);
        await db.ModelSlots.AsNoTracking().Select(s=>s.Generation).Take(1).ToListAsync(c.RequestAborted);
        await db.RepairCampaigns.AsNoTracking().Take(1).ToListAsync(c.RequestAborted);
        await db.BigFixMappings.AsNoTracking().Take(1).ToListAsync(c.RequestAborted);
        await db.BigFixVerifications.AsNoTracking().Take(1).ToListAsync(c.RequestAborted);
        await db.AiQualityRuns.AsNoTracking().Take(1).ToListAsync(c.RequestAborted);
        await db.AiFeedbacks.AsNoTracking().Take(1).ToListAsync(c.RequestAborted);
        return Results.Ok(new { status = "ready" }); }
    catch { return Results.StatusCode(503); }
});

// Authentication
api.MapGet("/auth/config", (IOptions<LdapOptions> o, IOptions<LocalAdminOptions> local) => new { domain = o.Value.Domain, demo, localAdmin = local.Value.Enabled, configured = demo || local.Value.Enabled || (o.Value.Enabled && !string.IsNullOrWhiteSpace(o.Value.Server)) });
api.MapGet("/auth/csrf", (HttpContext c, IAntiforgery a) => new { token = a.GetAndStoreTokens(c).RequestToken });
api.MapPost("/auth/login", async (LoginRequest input, HttpContext c, IAuthenticator auth, WfiDb db) =>
{
    if (string.IsNullOrWhiteSpace(input.Username) || string.IsNullOrEmpty(input.Password) || input.Username.Length > 256 || input.Password.Length > 1024) return Results.BadRequest(new { error = "Kullanıcı adı ve parola gereklidir." });
    var principal = auth.Authenticate(input.Username, input.Password);
    db.Audit.Add(new() { Domain=principal?.FindFirstValue("domain") ?? "", Actor = "login", Action = principal == null ? "login.denied" : "login.success", Target = principal?.Identity?.Name ?? "unknown" });
    await db.SaveChangesAsync();
    if (principal == null) return Results.Json(new { error = "Giriş başarısız. Bilgileri ve WFI grup üyeliğinizi kontrol edin." }, statusCode: 401);
    ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("session_id", Guid.NewGuid().ToString("N")));
    await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    return Results.Ok(new { success = true });
}).RequireRateLimiting("login");
if (demo) api.MapPost("/auth/demo", async (HttpContext c, IOptions<LdapOptions> o, WfiDb db) =>
{
    if (c.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip)) return Results.StatusCode(403);
    var p = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "demo@" + o.Value.Domain), new Claim(ClaimTypes.NameIdentifier, "demo"), new Claim(ClaimTypes.Role, "Admin"), new Claim("domain", o.Value.Domain.ToLowerInvariant()) }, "Cookies"));
    db.Audit.Add(new() { Domain=p.FindFirstValue("domain")!, Actor = "demo", Action = "login.demo", Target = "loopback" }); await db.SaveChangesAsync();
    ((ClaimsIdentity)p.Identity!).AddClaim(new Claim("session_id", Guid.NewGuid().ToString("N")));
    await c.SignInAsync("Cookies", p); return Results.Ok(new { success = true });
});
api.MapGet("/auth/me", (HttpContext c) => new { name = c.User.Identity!.Name, domain = Domain(c), roles = c.User.FindAll(ClaimTypes.Role).Select(x => x.Value), demo }).RequireAuthorization();
api.MapPost("/auth/logout", async (HttpContext c, WfiDb db) => { db.Audit.Add(new() { Domain=Domain(c), Actor = c.User.Identity!.Name!, Action = "logout", Target = c.User.FindFirstValue("session_id")! }); await db.SaveChangesAsync(); await c.SignOutAsync(); return Results.Ok(); }).RequireAuthorization();

api.MapAgentEndpoints();
api.MapOperationsEndpoints();
api.MapCampaignEndpoints();
api.MapBigFixEndpoints();
api.MapAiQualityEndpoints();

// WFI 2.0 Device Inventory
api.MapGet("/devices", async (WfiDb db, HttpContext c, string? search = null, string status = "all", int page = 1) =>
{
    var domain = Domain(c); page = Math.Clamp(page, 1, 10000);
    var query = db.Devices.AsNoTracking().Where(d => d.Domain == domain);
    if(search?.Length>63)return Results.BadRequest(new{error="Arama en fazla 63 karakter olabilir."});
    if (!string.IsNullOrWhiteSpace(search)) { var term=search.Trim().ToLowerInvariant();query = query.Where(d => d.Hostname.Contains(term) || d.IpAddress.Contains(term)); }
    if (status != "all") query = query.Where(d => d.Status == status);
    query = query.OrderByDescending(d => d.LastHeartbeat).ThenByDescending(d=>d.Id);
    return Results.Ok(new { total = await query.CountAsync(c.RequestAborted), page, items = await query.Skip((page - 1) * 50).Take(50).Select(d => new { d.Id, d.Hostname, d.Domain, d.IpAddress, d.Status, d.LastHeartbeat, d.AgentVersion, d.AutomationMode }).ToListAsync(c.RequestAborted) });
}).RequireAuthorization();

api.MapGet("/devices/{id:guid}", async (Guid id, WfiDb db, HttpContext c) =>
{
    var domain = Domain(c);
    var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.Domain == domain);
    if (device == null) return Results.NotFound();
    return Results.Ok(new { device.Id, device.Hostname, device.Domain, device.IpAddress, device.Status, device.LastHeartbeat, device.AgentVersion, device.SystemInfoJson });
}).RequireAuthorization();

// WFI 2.0 Diagnostic Jobs (Remote Diagnostics)
api.MapGet("/distribution/agent", (IWebHostEnvironment environment) =>
{
    var path=Path.Combine(environment.ContentRootPath,"distribution","WFI-Agent-Setup.exe");
    return File.Exists(path) ? Results.File(path,"application/octet-stream","WFI-Agent-Setup.exe") : Results.Json(new { error="Ajan paketi henüz oluşturulmadı. Sunucuda scripts/Build.ps1 çalıştırın." },statusCode:503);
}).RequireAuthorization("Admin");
api.MapGet("/distribution/agent/info",(IWebHostEnvironment environment)=>
{
    var path=Path.Combine(environment.ContentRootPath,"distribution","agent-manifest.json");
    return File.Exists(path)?Results.File(path,"application/json"):Results.StatusCode(503);
}).RequireAuthorization("Admin");
api.MapGet("/ai/experience",async(HttpContext c,WfiDb db)=>Results.Ok(new{items=await ExperienceMemory.ReadAsync(db,Domain(c),null,null,c.RequestAborted),periodDays=180})).RequireAuthorization();
api.MapPost("/ai/experience/{id:guid}/exclude",async(Guid id,HttpContext c,WfiDb db)=>
{
    var domain=Domain(c);
    await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
    var changed=await db.RemediationExecutions.Where(r=>r.Id==id&&r.Domain==domain&&!r.LearningExcluded)
        .ExecuteUpdateAsync(s=>s.SetProperty(r=>r.LearningExcluded,true),c.RequestAborted);
    if(changed==0)return Results.NotFound();
    db.Audit.Add(new() { Domain=Domain(c),Actor=c.User.Identity!.Name!,Action="learning.excluded",Target=id.ToString()});
    await db.SaveChangesAsync(c.RequestAborted);await tx.CommitAsync(c.RequestAborted);return Results.NoContent();
}).RequireAuthorization("Admin");

api.MapPost("/diagnostics", async (DiagnosticJobRequest request, WfiDb db, HttpContext c) =>
{
    if (request.ProblemDescription?.Length > 4000) return Results.BadRequest(new { error = "Sorun açıklaması en fazla 4000 karakter olabilir." });
    var domain = Domain(c);
    var hostname=request.Hostname?.Trim().ToLowerInvariant() ?? "";
    if(!Regex.IsMatch(hostname,"\\A[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\\z"))return Results.BadRequest(new { error="Geçerli bir makine adı girin." });
    if (!Enum.TryParse<ScanType>(request.ScanType, out var scan) || !Enum.IsDefined(scan)) return Results.BadRequest();
    var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Domain == domain && d.Hostname == hostname && d.Status != "revoked", c.RequestAborted);
    if (device == null) return Results.NotFound(new { error="Bu domain’de kayıtlı ajan bulunamadı. Cihazlar ve AI ekranından cihazı kaydedip ajanı kurun." });
    await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
    // Serialize manual submissions with other submissions and automatic scheduling on this device.
    await DiagnosticAdmission.LockAsync(db,domain,c.RequestAborted);
    var locked=await db.Devices.Where(d=>d.Id==device.Id&&d.Domain==domain&&d.Status!="revoked")
        .ExecuteUpdateAsync(s=>s.SetProperty(d=>d.Status,d=>d.Status),c.RequestAborted);
    if(locked==0)return Results.NotFound();
    var active=await db.DiagnosticJobs.AsNoTracking().Where(j=>j.DeviceId==device.Id&&
        (j.Status=="pending"||j.Status=="running"||j.Status=="evidence_received"||j.Status=="analyzing"))
        .Select(j=>new{j.Id}).FirstOrDefaultAsync(c.RequestAborted);
    if(active!=null)return Results.Conflict(new{error="Bu cihazda aktif teşhis var. Tamamlanmasını bekleyin veya önce iptal edin.",id=active.Id});
    if(!await DiagnosticAdmission.HasCapacityAsync(db,domain,app.Configuration,c.RequestAborted))
        return Results.Json(new{error="Domain teşhis kuyruğu dolu. Aktif işlerin tamamlanmasını bekleyin."},statusCode:429);
    var job = new DiagnosticJob
    {
        DeviceId = device.Id,
        Domain = domain,
        Hostname = device.Hostname,
        ScanType = request.ScanType,
        ProblemDescription = LogAnalyzer.Redact(request.ProblemDescription?.Trim() ?? ""),
        Status = "pending",
        RequestedBy = c.User.Identity!.Name!,
        CreatedAt = DateTime.UtcNow
    };
    db.DiagnosticJobs.Add(job);
    db.Audit.Add(new() { Domain=Domain(c), Actor = c.User.Identity!.Name!, Action = "diagnostic.requested", Target = job.Id.ToString() });
    await db.SaveChangesAsync(c.RequestAborted);
    await tx.CommitAsync(c.RequestAborted);
    return Results.Created($"/api/v1/diagnostics/{job.Id}", new { job.Id, status = job.Status });
}).RequireAuthorization("Analyze");

api.MapGet("/diagnostics/{id:guid}", async (Guid id, WfiDb db, HttpContext c) =>
{
    var domain = Domain(c);
    var job = await db.DiagnosticJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.Domain == domain);
    if (job == null) return Results.NotFound();
    var analysis=await db.AiAnalysisJobs.AsNoTracking().Where(a=>a.DiagnosticJobId==id&&a.Domain==domain).OrderByDescending(a=>a.CreatedAt).FirstOrDefaultAsync();
    return Results.Ok(new { job.Id, job.Hostname, job.Domain, job.ScanType, job.ProblemDescription, job.Status, job.CreatedAt, job.CompletedAt, job.Error, analysis });
}).RequireAuthorization();

api.MapPost("/diagnostics/{id:guid}/cancel", async (Guid id, WfiDb db, HttpContext c) =>
{
    var domain = Domain(c);
    await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
    var changed=await db.DiagnosticJobs.Where(j=>j.Id==id&&j.Domain==domain&&(j.Status=="pending"||j.Status=="running"||j.Status=="evidence_received"||j.Status=="analyzing"))
        .ExecuteUpdateAsync(s=>s.SetProperty(j=>j.Status,"failed").SetProperty(j=>j.Error,"İstemci tarafından iptal edildi.").SetProperty(j=>j.CompletedAt,DateTime.UtcNow),c.RequestAborted);
    if(changed==0)return await db.DiagnosticJobs.AnyAsync(j=>j.Id==id&&j.Domain==domain,c.RequestAborted)?Results.Conflict(new{error="İş artık iptal edilebilir durumda değil."}):Results.NotFound();
    db.Audit.Add(new() { Domain=Domain(c), Actor = c.User.Identity!.Name!, Action = "diagnostic.cancelled", Target = id.ToString() });
    await db.SaveChangesAsync(c.RequestAborted);
    await tx.CommitAsync(c.RequestAborted);
    return Results.Ok(new { status = "cancelled" });
}).RequireAuthorization("Analyze");

// WFI 2.0 AI Analysis
api.MapPost("/ai/analyze/{diagnosticJobId:guid}", async (Guid diagnosticJobId, WfiDb db, HttpContext c) =>
{
    var domain = Domain(c);
    var job = await db.DiagnosticJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == diagnosticJobId && j.Domain == domain);
    if (job == null) return Results.NotFound();

    if (job.EvidenceJson == null) return Results.BadRequest(new { error="Önce ajan kanıtı bekleniyor." });
    await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
    await DiagnosticAdmission.LockAsync(db,domain,c.RequestAborted);
    if(job.DeviceId!=null&&await db.DiagnosticJobs.AnyAsync(j=>j.DeviceId==job.DeviceId&&j.Domain==domain&&
        (j.Status=="pending"||j.Status=="running"||j.Status=="evidence_received"||j.Status=="analyzing"),c.RequestAborted))
        return Results.Conflict(new{error="Bu cihazda aktif teşhis var. Tamamlanmasını bekleyin veya önce iptal edin."});
    if(!await DiagnosticAdmission.HasCapacityAsync(db,domain,app.Configuration,c.RequestAborted))
        return Results.Json(new{error="Domain teşhis kuyruğu dolu. Aktif işlerin tamamlanmasını bekleyin."},statusCode:429);
    var updated=await db.DiagnosticJobs.Where(j=>j.Id==job.Id&&(j.Status=="completed"||j.Status=="failed")).ExecuteUpdateAsync(s=>s.SetProperty(j=>j.Status,"evidence_received").SetProperty(j=>j.Error,(string?)null).SetProperty(j=>j.CompletedAt,(DateTime?)null),c.RequestAborted);
    if(updated==0)return Results.Conflict();
    await tx.CommitAsync(c.RequestAborted);
    return Results.Accepted($"/api/v1/diagnostics/{job.Id}", new { job.Id, status="evidence_received" });
}).RequireAuthorization("Analyze");

api.MapGet("/ai/analysis/{id:guid}", async (Guid id, WfiDb db, HttpContext c) =>
{
    var domain = Domain(c);
    var aiJob = await db.AiAnalysisJobs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.Domain == domain);
    if (aiJob == null) return Results.NotFound();
    return Results.Ok(new { aiJob.Id, aiJob.ModelUsed, aiJob.VersionsJson, aiJob.LearningContextJson, aiJob.Hostname, aiJob.Domain, aiJob.Status, aiJob.Conclusion, aiJob.HypothesesJson, aiJob.ConfidenceScore, aiJob.MissingEvidenceJson, aiJob.RecommendedPlaybooksJson });
}).RequireAuthorization();

api.MapGet("/ai/models", async (IOllamaService ollama, IConfiguration configuration) =>
{
    var models = await ollama.GetModelsAsync();
    return Results.Ok(new { availableModels = models.Select(m => m.Name), defaultModel = configuration["Ollama:DefaultModel"] ?? "phi4-mini:latest" });
}).RequireAuthorization();

api.MapPost("/ai/model", async (WfiDb db, SetModelRequest req, IOllamaService ollama, HttpContext c) =>
{
    await ollama.SetActiveModelAsync(req.Model,c.RequestAborted);
    db.Audit.Add(new() { Domain=Domain(c),Actor=c.User.Identity!.Name!,Action="ai.model.changed",Target=req.Model});await db.SaveChangesAsync(c.RequestAborted);
    return Results.Ok(new { activeModel = await ollama.GetActiveModelAsync() });
}).RequireAuthorization("Admin");

// WFI 2.0 Incident Management
api.MapGet("/incidents", async (WfiDb db, HttpContext c, string? severity = null, string status = "all", int page = 1) =>
{
    var domain = Domain(c); page = Math.Clamp(page, 1, 10000);
    var query = db.Incidents.AsNoTracking().Where(i => i.Domain == domain);
    if (severity != null) query = query.Where(i => i.Severity == severity);
    if (status != "all") query = query.Where(i => i.Status == status);
    query = query.OrderByDescending(i => i.CreatedAt);
    return Results.Ok(new { total = await query.CountAsync(), page, items = await query.Skip((page - 1) * 25).Take(25).Select(i => new { i.Id, i.Title, i.Severity, i.Status, i.CreatedAt }).ToListAsync() });
}).RequireAuthorization();

api.MapGet("/incidents/{id:guid}", async (Guid id, WfiDb db, HttpContext c) =>
{
    var domain = Domain(c);
    var incident = await db.Incidents.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && i.Domain == domain);
    if (incident == null) return Results.NotFound();
    var aiJob = incident.AiAnalysisId.HasValue ? await db.AiAnalysisJobs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == incident.AiAnalysisId.Value && a.Domain == domain) : null;
    return Results.Ok(new { incident.Id, incident.Title, incident.Severity, incident.Status, incident.Summary, incident.CreatedAt, aiAnalysis = aiJob?.Conclusion });
}).RequireAuthorization();

// WFI 2.0 Remediation Center
api.MapGet("/playbooks", async (WfiDb db) =>
{
    var playbooks = await db.Playbooks.AsNoTracking().OrderBy(p => p.Category).ThenBy(p => p.Code).ToListAsync();
    return Results.Ok(playbooks.Select(p => new { p.Id, p.Code, p.Version, p.Title, p.Category, p.RiskLevel, p.Mode, p.IsApproved }));
}).RequireAuthorization();

api.MapPost("/remediations", async (RemediationRequest req, WfiDb db, HttpContext c) =>
{
    var playbook = await db.Playbooks.AsNoTracking().FirstOrDefaultAsync(p => p.Code == req.PlaybookCode);
    if (playbook == null) return Results.BadRequest(new { error = "Playbook bulunamadı." });
    if (RemediationEngine.Find(playbook.Code) == null || playbook.Version != RemediationEngine.Version) return Results.BadRequest(new { error="Desteklenmeyen katalog sürümü." });

    var domain = Domain(c);
    var hostname=req.Hostname?.Trim().ToLowerInvariant() ?? "";
    if(!Regex.IsMatch(hostname,"\\A[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\\z"))return Results.BadRequest(new { error="Geçerli bir makine adı girin." });
    var device=await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Domain == domain && d.Hostname == hostname && d.Status != "revoked", c.RequestAborted);
    if (device == null) return Results.NotFound();
    if (!playbook.IsApproved || (playbook.RequiredRole == "Admin" && !c.User.IsInRole("Admin"))) return Results.Forbid();

    await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
    var locked=await db.Devices.Where(d=>d.Id==device.Id&&d.Domain==domain&&d.Status!="revoked")
        .ExecuteUpdateAsync(s=>s.SetProperty(d=>d.Status,d=>d.Status),c.RequestAborted);
    if(locked==0)return Results.NotFound();
    var existing=await db.RemediationExecutions.AsNoTracking().Where(r=>r.DeviceId==device.Id&&r.PlaybookCode==playbook.Code&&
        (r.Status=="PendingApproval"||r.Status=="Approved"||r.Status=="Executing")).Select(r=>new{r.Id}).FirstOrDefaultAsync(c.RequestAborted);
    if(existing!=null)return Results.Conflict(new{error="Bu cihazda aynı düzeltme zaten bekliyor veya çalışıyor.",id=existing.Id});

    var execution = new RemediationExecution
    {
        DeviceId = device.Id,
        Status = playbook.RiskLevel == "High" ? "PendingApproval" : "Approved",
        PlaybookId = playbook.Id,
        PlaybookCode = playbook.Code,
        PlaybookTitle = playbook.Title,
        Domain = domain,
        Hostname = device.Hostname,
        RiskLevel = playbook.RiskLevel,
        RequestedBy = c.User.Identity!.Name!,
        ApprovedBy = playbook.RiskLevel == "High" ? null : c.User.Identity!.Name!,
        RequestedAt = DateTime.UtcNow
    };

    db.RemediationExecutions.Add(execution);
    db.Audit.Add(new() { Domain=Domain(c), Actor = c.User.Identity!.Name!, Action = "remediation.requested", Target = execution.Id.ToString() });
    await db.SaveChangesAsync(c.RequestAborted);
    await tx.CommitAsync(c.RequestAborted);

    return Results.Created($"/api/v1/remediations/{execution.Id}", new { execution.Id, status = execution.Status });
}).RequireAuthorization("Analyze");

api.MapGet("/remediations/{id:guid}", async (Guid id, WfiDb db, HttpContext c) =>
{
    // Other domains' executions are indistinguishable from missing ones.
    var domain = Domain(c);
    var execution = await db.RemediationExecutions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.Domain == domain);
    if (execution == null) return Results.NotFound();

    var playbook = execution.PlaybookId != Guid.Empty ? await db.Playbooks.AsNoTracking().FirstOrDefaultAsync(p => p.Id == execution.PlaybookId) : null;

    return Results.Ok(new
    {
        execution.Id, execution.PlaybookCode, execution.PlaybookTitle, execution.Domain,
        execution.Hostname, execution.RiskLevel, execution.Status, execution.ExecutedAt,
        execution.CompletedAt, execution.PrecheckResult, execution.ExecutionOutput,
        execution.ValidationResult, execution.Error
    });
}).RequireAuthorization();

api.MapPost("/remediations/{id:guid}/cancel", async (Guid id, WfiDb db, HttpContext c) =>
{
    var domain=Domain(c);
    await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
    var changed=await db.RemediationExecutions.Where(r=>r.Id==id&&r.Domain==domain&&(r.Status=="Approved"||r.Status=="PendingApproval"))
        .ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,"Cancelled").SetProperty(r=>r.CompletedAt,DateTime.UtcNow)
            .SetProperty(r=>r.Error,"Operatör tarafından başlamadan iptal edildi."),c.RequestAborted);
    if(changed==0)return await db.RemediationExecutions.AnyAsync(r=>r.Id==id&&r.Domain==domain,c.RequestAborted)?Results.Conflict(new{error="Yalnız başlamamış düzeltme iptal edilebilir. Güncel durumu kontrol edin."}):Results.NotFound();
    db.Audit.Add(new() { Domain=Domain(c),Actor=c.User.Identity!.Name!,Action="remediation.cancelled",Target=id.ToString()});
    await db.SaveChangesAsync(c.RequestAborted);await tx.CommitAsync(c.RequestAborted);
    return Results.Ok(new{id,status="Cancelled"});
}).RequireAuthorization("Analyze");

api.MapPost("/remediations/{id:guid}/approve", async (Guid id, WfiDb db, HttpContext c) =>
{
    var domain = Domain(c);
    await using var tx=await db.Database.BeginTransactionAsync(c.RequestAborted);
    var actor=c.User.Identity!.Name!;
    var changed=await db.RemediationExecutions.Where(r=>r.Id==id&&r.Domain==domain&&r.Status=="PendingApproval")
        .ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ApprovedBy,actor).SetProperty(r=>r.Status,"Approved"),c.RequestAborted);
    if(changed==0)return await db.RemediationExecutions.AnyAsync(r=>r.Id==id&&r.Domain==domain,c.RequestAborted)?Results.Conflict(new{error="İş artık onay beklemiyor."}):Results.NotFound();
    db.Audit.Add(new() { Domain=Domain(c), Actor = c.User.Identity!.Name!, Action = "remediation.approved", Target = id.ToString() });
    await db.SaveChangesAsync(c.RequestAborted);
    await tx.CommitAsync(c.RequestAborted);
    return Results.Ok(new { id, status = "Approved" });
}).RequireAuthorization("Admin");

// Original log analysis endpoints (secondary feature)
api.MapGet("/analyses", async (HttpContext c, WfiDb db, int page = 1, string? search = null) =>
{
    page = Math.Clamp(page, 1, 10000); var domain = Domain(c);
    if (search?.Length > 63) return Results.BadRequest(new { error = "Arama en fazla 63 karakter olabilir." });
    var query = db.Jobs.AsNoTracking().Where(j => j.Domain == domain);
    if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLowerInvariant(); query = query.Where(j => j.Hostname.Contains(term)); }
    return Results.Ok(new { total = await query.CountAsync(), page, items = await query.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id).Skip((page - 1) * 25).Take(25).Select(j => new { j.Id, j.Domain, j.Hostname, j.Filename, j.Status, j.CreatedAt, j.CompletedAt, j.Error }).ToListAsync() });
}).RequireAuthorization();

api.MapGet("/analyses/{id:guid}/status", async (Guid id, HttpContext c, WfiDb db) =>
{
    var domain = Domain(c);
    var status = await db.Jobs.AsNoTracking().Where(j => j.Id == id && j.Domain == domain)
        .Select(j => new { j.Id, j.Status }).FirstOrDefaultAsync(c.RequestAborted);
    return status == null ? Results.NotFound() : Results.Ok(status);
}).RequireAuthorization();
api.MapGet("/analyses/{id:guid}", async (Guid id, HttpContext c, WfiDb db) =>
{
    var domain = Domain(c); var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.Domain == domain);
    if (job == null) return Results.NotFound();
    db.Audit.Add(new() { Domain=Domain(c), Actor = c.User.Identity!.Name!, Action = "analysis.read", Target = id.ToString() }); await db.SaveChangesAsync();
    return Results.Ok(new { job.Id, job.Hostname, job.Domain, job.Filename, job.Status, job.CreatedAt, job.Error, result = job.ResultJson == null ? null : JsonSerializer.Deserialize<AnalysisResult>(job.ResultJson, AnalysisWorker.JsonOptions) });
}).RequireAuthorization();

api.MapPost("/analyses/upload", async (HttpContext c, WfiDb db, IConfiguration config) =>
{
    var form = await c.Request.ReadFormAsync(c.RequestAborted);
    var host = form["hostname"].ToString().Trim().ToLowerInvariant(); var domain = Domain(c);
    if (!Regex.IsMatch(host, @"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")) throw new InputRejectedException("Hostname 1–63 karakter olmalı; harf, sayı veya tire kullanın.");
    if (form.Files.Count != 1) throw new InputRejectedException("Tek bir log veya ZIP dosyası seçin.");
    var file = form.Files[0];
    var filename = Path.GetFileName(file.FileName.Replace('\\', '/'));
    if (filename.Length > 180 || filename.Any(char.IsControl) || !LogAnalyzer.Extensions.Contains(Path.GetExtension(filename).ToLowerInvariant())) throw new InputRejectedException("Geçersiz dosya adı veya desteklenmeyen biçim.");
    if (file.Length is <= 0 or > LogAnalyzer.MaxUpload) throw new InputRejectedException("Dosya boyutu 1 bayt ile 20 MB arasında olmalı.");
    var root = Path.GetFullPath(config["Storage:Path"] ?? ".data/logs"); Directory.CreateDirectory(root);
    var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".raw");
    bool saved = false;
    try
    {
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) await file.CopyToAsync(output, c.RequestAborted);
        await using var source = File.OpenRead(path);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(source, c.RequestAborted));
        var dedup = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{domain}|{host}|{hash}|{Rules.Version}")));
        var existing = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.DedupKey == dedup);
        if (existing != null) return Results.Ok(new { id = existing.Id, duplicate = true });
        var job = new AnalysisJob { Domain = domain, Hostname = host, Filename = filename, StorageKey = path, Sha256 = hash, DedupKey = dedup, Size = file.Length, Actor = c.User.Identity!.Name! };
        db.Jobs.Add(job); db.Audit.Add(new() { Domain=Domain(c), Actor = job.Actor, Action = "analysis.upload", Target = job.Id.ToString() });
        try { await db.SaveChangesAsync(c.RequestAborted); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var winner = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.DedupKey == dedup);
            if (winner != null) return Results.Ok(new { id = winner.Id, duplicate = true });
            throw;
        }
        saved = true; return Results.Accepted($"/api/v1/analyses/{job.Id}", new { id = job.Id, duplicate = false });
    }
    finally { if (!saved && File.Exists(path)) File.Delete(path); }
}).RequireAuthorization("Analyze").RequireRateLimiting("upload");

api.MapGet("/rules", () => Rules.All.Select(r => new { r.Id, version = Rules.Version, r.Category, r.Title, r.Explanation, r.Checks })).RequireAuthorization();
api.MapGet("/connectors", (IOptions<LdapOptions> o, IBigFixConnector bigFix) => new { ldap = new { domain = o.Value.Domain, configured = o.Value.Enabled && !string.IsNullOrWhiteSpace(o.Value.Server), mode = "LDAPS", demo }, sccm = "not_configured", bigFix = bigFix.Configured ? "configured" : "not_configured" }).RequireAuthorization();
api.MapGet("/audit", async (HttpContext c, WfiDb db, int page = 1) => { var domain=Domain(c); return await db.Audit.AsNoTracking().Where(x=>x.Domain==domain).OrderByDescending(x => x.Id).Skip((Math.Clamp(page, 1, 10000) - 1) * 50).Take(50).ToListAsync(); }).RequireAuthorization("Admin");

// WFI 2.0 Ollama Config
api.MapGet("/ai/config", async (IOllamaService ollama, WfiDb db) =>
{
    var models=await ollama.GetModelsAsync();
    return Results.Ok(new { model=await ollama.GetActiveModelAsync(), available=models.Count>0, availableModels=models.Select(m=>m.Name) });
}).RequireAuthorization();

app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();
static string Domain(HttpContext c) => c.User.FindFirstValue("domain") ?? throw new InvalidOperationException("Domain claim eksik.");
public sealed record LoginRequest(string Username, string Password);
public sealed record DiagnosticJobRequest(string Hostname, string ScanType, string? ProblemDescription = null);
public sealed record RemediationRequest(string PlaybookCode, string Hostname, string Domain, string? ApprovedBy);
public sealed record SetModelRequest(string Model);
public partial class Program;




