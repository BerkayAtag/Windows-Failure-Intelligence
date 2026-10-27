# Kurulum, LDAP ve işletim

## Paket ve veritabanı

`scripts/Build.ps1` → `artifacts/api` ve `artifacts/worker`. PostgreSQL 17+ üzerinde ayrı veritabanı oluşturun. Şema değişikliklerini DBA/migration hesabıyla uygulayın; çalışma hesabına CREATE/ALTER yetkisi vermeyin.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
# ConnectionStrings__Wfi değerini kurumun secret yönetiminden sağlayın.
dotnet WFI.Api.dll --migrate
```

Migration modu LDAP bağlantısı açmaz ve worker başlatmaz. Uygulama production başlangıcında şemayı otomatik değiştirmez. Aynı dizindeki `appsettings.local.json` okunur; environment değişkenleri üstündür (`Ldap__Domain`, `Ldap__Server`, `ConnectionStrings__Wfi`). PostgreSQL SSL doğrulaması için `SSL Mode=VerifyFull` kullanın. SQLite yalnızca geliştirme içindir.

## HTTPS / IIS

Windows Server üzerinde .NET 10 Hosting Bundle ve IIS ASP.NET Core Module kurun. Publish çıktısı IIS web.config dosyasını oluşturur. HTTPS binding’i kurum sertifikasıyla yapılandırın. Form tabanlı LDAP login kullanılacağı için IIS Anonymous Authentication açık, Windows Authentication kapalı olmalı. Uygulama kimliği için ayrılmış servis hesabı kullanın; Domain Admin gerekmez.

IIS requestFiltering `maxAllowedContentLength` değerini 21 MB veya üzeri ayarlayın; API log başına 20 MB sınırını uygular. `AllowedHosts` gerçek WFI FQDN’i olmalı. DataProtection anahtarlarına ve storage’a yalnızca servis hesabı ile yetkili yedekleme operatörü erişmeli. Diskleri kurumun at-rest şifreleme politikasıyla koruyun. DataProtection dosyaları varsayılan olarak ACL/disk korumasına dayanır; çoklu instance öncesi sertifikayla anahtar koruması ve ortak key ring kurun.

Doğrudan Kestrel kullanılacaksa HTTPS sertifikasını `Kestrel:Endpoints:Https` ile tanımlayın. Bu sürüm rastgele `X-Forwarded-*` başlıklarına güvenmez. IIS dışı reverse proxy kullanılıyorsa güvenilir proxy adresleriyle ReverseProxy.KnownProxies ayarını doldurup HTTPS ve istemci IP hız sınırını doğrulayın.

## LDAPS yapılandırması

Örnek dosyadaki DC adı ve OU'lar yer tutucudur, kurumunuzdaki gerçek değerler değildir. `example.invalid` kullanıcı tarafından sağlanmış varsayılandır. Tam grup DN’lerini AD yönetim araçlarından doğrulayın. Base DN tüm izinli kullanıcıları kapsamalıdır. Grup DN’leri Base DN dışında olabilir ancak aynı DC’den okunabilir olmalıdır.

Uygulama girilen kullanıcı parolasıyla TLS altında bind yapar; ayrı LDAP servis parolası depolamaz. Ardından kullanıcı objectGUID’sini ve yapılandırılmış gruplara transitive üyeliğini sorgular. AD kullanıcısının bu alanları okuma izni olmalı. Primary group üzerinden rol verilmez; özel WFI güvenlik gruplarına normal/nested üyelik kullanın. Disabled kullanıcılar kullanıcı aramasında dışlanır; kilitli/expired kullanıcı bind aşamasında AD tarafından reddedilir. LDAP referral chasing kapalıdır.

Şifre boşsa bind yapılmaz; anonim LDAP bind üzerinden giriş mümkün değildir. Kullanıcıya dönen hata parola yanlış/grup eksik ayrımını açığa çıkarmaz. Bağlantı hataları genel 503 yanıtına dönüşür. Hata günlükleri parola veya bind exception ayrıntısını yazmaz. Parolalar veritabanında, cookie’de veya localStorage’da tutulmaz.

Roller:

| Rol | Okuma | Analiz yükleme | Audit |
| --- | --- | --- | --- |
| Viewer | Kendi domain’i | Hayır | Hayır |
| Analyst | Kendi domain’i | Evet | Hayır |
| Admin | Kendi domain’i | Evet | Evet |
| ConnectorAdmin | Kendi domain’i | Hayır | Hayır |

ConnectorAdmin gelecekte connector yönetimi için ayrılmıştır. Bu sürümde rol ayarları web arayüzünden değiştirilmez; konfigürasyon yönetimiyle yapılır. Kullanıcının AD grubundan çıkarılması mevcut cookie’yi anında sonlandırmaz; **en fazla 30 dakika** geçerli sabit oturum vardır. Çıkış yapılan oturum DB üzerinde iptal edilir ve diğer düğümlerde de reddedilir; AD grubu kaldırılması mevcut oturumu anında iptal etmez. MFA gerekiyorsa kurumun OIDC sağlayıcısına geçiş gerekir; LDAPS form girişi MFA/SSO sağlamaz.

## Worker ve veri yaşam döngüsü

Tek sunucuda `Worker.Enabled=true`. Ayrı worker için API’de false; worker’a aynı PostgreSQL bağlantısını verin. StorageKey mutlak yol içerdiği için API/worker aynı Windows yolu veya erişilebilir ortak UNC yolunu kullanmalıdır. Worker da kendi çalışma dizinindeki appsettings.local.json dosyasını okur; environment değişkenleri üstündür.

İşler atomik koşullu UPDATE ile alınır. Lease 5 dakika, analiz timeout 3 dakika, azami yeniden alma üç denemedir. İşlem kapanırsa lease dolunca başka worker devralır. LeaseToken eski worker’ın yeni sahibin sonucunu ezmesini engeller. Çoklu worker kapasitesi PostgreSQL yük testiyle doğrulanmalıdır.

Yedekleme birlikte yapılmalı: PostgreSQL, raw storage ve DataProtection anahtarları. Restore tatbikatı zorunlu pilot adımıdır. Kaynak dosya saklama hedefi kurum onayına göre örneğin 30 gündür; **otomatik purge henüz yoktur**. Silme prosedürü DB/storage tutarlılığını korumalıdır. Crash sırasında dosya yazılıp DB kaydı oluşmadan kapanma durumunda orphan dosyalar kalabilir; bakımda DB referanslarıyla karşılaştırın.

Audit kayıtları uygulama DB’sindedir; değiştirilemez/WORM değildir. Üretimde ayrı append-only toplama hattı, audit tablo izinleri ve kurum SIEM aktarımı tamamlanmalıdır.

## Pilot doğrulama

İzinli Viewer/Analyst/Admin, yetkisiz grup, yanlış parola, disabled hesap, nested grup, geçersiz CA, DC kesintisi, süresi dolan oturum ve CSRF senaryolarını çalıştırın. Önce sentetik loglarla, ardından 50–200 cihazlık onaylı örneklerle deneyin. Geniş filo yayını için bellek/CPU/kuyruk gecikmesi ve LDAP hesap kilitleme eşiklerini ölçün. IP başına giriş sınırı uygulama instance’ı başınadır; HA için ortak limiter veya gateway gerekir.

