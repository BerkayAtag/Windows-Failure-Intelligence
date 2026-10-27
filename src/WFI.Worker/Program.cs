using WFI.Infrastructure;
var builder=Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json",optional:true,reloadOnChange:false).AddEnvironmentVariables();
builder.Services.AddWfiDatabase(builder.Configuration,builder.Environment.IsDevelopment());
builder.Services.AddHttpClient("ollama",c=>{c.Timeout=TimeSpan.FromSeconds(250);c.MaxResponseContentBufferSize=131072;}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
builder.Services.AddSingleton<IOllamaService,OllamaService>();
builder.Services.AddWfiTelemetry(builder.Configuration);
builder.Services.AddSingleton<IBigFixConnector,BigFixConnector>();
builder.Services.AddHttpClient("bigfix",c=>{c.Timeout=TimeSpan.FromSeconds(10);c.MaxResponseContentBufferSize=131072;})
    .ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
builder.Services.AddHostedService<AnalysisWorker>();
builder.Services.AddHostedService<DiagnosticJobProcessor>();
builder.Services.AddHostedService<CampaignSupervisor>(); builder.Services.AddHostedService<BigFixVerificationWorker>(); builder.Services.AddHostedService<AiQualityWorker>();
await builder.Build().RunAsync();
