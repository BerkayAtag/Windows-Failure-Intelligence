using System.Net.Http.Json;
using System.Text.Json;

namespace WFI.Core;

public sealed record AgentSetupTicket(Guid Id,string EnrollmentToken,string Domain);

// Setup uses an ordinary authenticated Admin session; no anonymous fleet registration secret.
public static class AgentSetupRegistration
{
    public static async Task<AgentSetupTicket> RegisterAsync(HttpClient client,string username,string password,string hostname,CancellationToken ct)
    {
        if(client.BaseAddress?.Scheme!="https")throw new InvalidOperationException("HTTPS sunucu adresi gerekli.");
        var signedIn=false;
        async Task Csrf()
        {
            var token=await client.GetFromJsonAsync<JsonElement>("api/v1/auth/csrf",ct);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());
        }
        try
        {
            await Csrf();
            using var login=await client.PostAsJsonAsync("api/v1/auth/login",new{username,password},ct);
            if(!login.IsSuccessStatusCode)throw new InvalidOperationException($"Giriş başarısız (HTTP {(int)login.StatusCode}). WFI Admin hesabını ve sunucu bağlantısını kontrol edin.");
            signedIn=true;await Csrf();
            using var registration=await client.PostAsJsonAsync("api/v1/devices/register",new{hostname},ct);
            if(!registration.IsSuccessStatusCode)throw new InvalidOperationException(registration.StatusCode==System.Net.HttpStatusCode.Conflict
                ? "Bu makine zaten kayıtlı. Mevcut ajanı yükseltin veya konsolda bekleyen kayıt için token yenileyin."
                : "Cihaz kaydı yapılamadı. WFI Admin yetkisi gereklidir.");
            var result=await registration.Content.ReadFromJsonAsync<AgentSetupTicket>(ct)??throw new InvalidDataException("Kayıt yanıtı geçersiz.");
            if(result.Id==Guid.Empty||result.EnrollmentToken?.Length!=64||!result.EnrollmentToken.All(Uri.IsHexDigit))throw new InvalidDataException("Kayıt yanıtı geçersiz.");
            return result;
        }
        finally
        {
            if(signedIn)
            {
                using var logoutTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try{using var logout=await client.PostAsync("api/v1/auth/logout",null,logoutTimeout.Token);}catch(HttpRequestException){}catch(OperationCanceledException){}
            }
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        }
    }
}
