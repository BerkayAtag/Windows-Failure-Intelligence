# 0.7.0 — 30 Ekim 2026 (güncel kurulum)

**WFI-Server-Setup.exe → Yönetici olarak çalıştır → İleri → ayarlar → Kur → Bitir.** Sihirbaz Türkçe karakterlerle açılır, ilerleme çubuğu gösterir ve bitişte **Konsolu aç** bağlantısı verir. İndirme: https://github.com/BerkayAtag/Windows-Failure-Intelligence/releases/latest

Kurulumun otomatik hazırladıkları: IIS, Visual C++ çalışma zamanı, .NET 10 Hosting Bundle, yalnız 127.0.0.1'de dinleyen PostgreSQL 17, HTTPS sertifikası (alan boşsa), Domain profilinde `WFI-HTTPS` (TCP/443) kuralı, veritabanı şeması ve `local:admin` hesabı. Bileşenler resmi kaynaklardan indirilir ve SHA256/SHA512 ile doğrulanır; Microsoft paketlerinin imzası ayrıca denetlenir.

### Yerel yapay zekâ (Ollama) seçimi

| Seçenek | Ne olur | Model adresi |
|---|---|---|
| **Ollama ve phi4-mini modelini WFI için kur** (makinede Ollama yoksa varsayılan) | Sabit sürümlü Ollama, `WFI.Ollama` Windows servisi olarak kurulur ve phi4-mini indirilir. | `http://127.0.0.1:11435` |
| **Bu makinede çalışan Ollama'yı kullan** (127.0.0.1:11434 yanıt veriyorsa varsayılan) | Ayrı Ollama kurulmaz. phi4-mini mevcut Ollama'ya indirilmeye çalışılır; indirilemezse kurulum tamamlanır ve konsoldan kurulu bir model seçilir. | `http://127.0.0.1:11434` |
| **Şimdi kurma** | AI bileşeni atlanır, gereken boş alan 15 GB yerine 5 GB'tır. AI teşhisi Ollama 127.0.0.1:11434'te hazırlanınca çalışır. | `http://127.0.0.1:11434` |

Mevcut Ollama'yı seçerseniz Windows oturumu kapalıyken de çalışması için Ollama'yı servis olarak çalıştırın ve yalnız loopback'te dinlediğini doğrulayın. Kaldırıcı yalnız WFI'nin kurduğu `WFI.Ollama` servisini kaldırır; mevcut Ollama'ya dokunmaz.

### Yeniden deneme ve yeniden başlatma

Kurulum yarıda kalırsa (ör. "YENİDEN BAŞLATMA GEREKLİ") Windows'u yeniden başlatıp kurucuyu **aynı dizin, DNS adı, domain ve AI seçimiyle** tekrar çalıştırın. Tamamlanan adımlar (indirmeler, veritabanı, şema, kurulumun ürettiği HTTPS sertifikası) tekrarlanmaz; 0.7.0 ile yeniden denemede ikinci bir sertifika oluşturulmaz. Kurucu betikleri `-ExecutionPolicy Bypass` ile yalnız kendi süreci için başlatır; Windows istemci sürümlerindeki varsayılan *Restricted* politika kurulumu engellemez, makine politikası değiştirilmez. Grup İlkesi ile zorlanan *AllSigned* politikası kurulumu engelleyebilir.

Programlar ve Özellikler (appwiz.cpl): **WFI Server**, yayıncı **Berkay Atağ**, sürüm **0.7.0**, kırmızı WFI simgesi, kurulum tarihi, boyut ve destek bağlantısı. Yayıncı adı kayıt bilgisidir, Authenticode imzası değildir.

Gerçek Windows Server VM üzerinde uçtan uca kurulum/kaldırma kabulü bu geliştirme ortamında yapılmadı; ilk kurulumu pilot sunucuda yapın.

# 0.6.0-preview.6 — 29 Ekim 2026

## 30 Ekim 2026 — CI süreç testi düzeltmesi

GitHub'ın ayrı doğrulama işi, sentetik PowerShell'in 10 saniyede başlayacağı varsayımı nedeniyle başarısız oldu; aynı kaynakla paket derlemesi başarılıydı. Süreç testleri paralel CPU yükünden ayrıldı, PID işareti atomik yayımlanıyor ve soğuk başlangıç için ayrı süre tanındı. İptal sonrası sürecin çıkması için mevcut 5 saniyelik kabul korunuyor; üretim ajanı/onarım süreleri değiştirilmedi. Yayınlanan preview.6 uygulama kodu ve paketi değişmedi.


PostgreSQL varsayılan 55432 portu Windows tarafından ayrılmış veya doluysa kurucu 35432–35531 aralığından uygun bir loopback portunu otomatik seçip yapılandırmaya kaydeder; ağdan DB erişimi açılmaz.

Güncel kurulum sürümü **preview.6**. Önceki preview.4/5 kurucularını kullanmayın: Windows PowerShell 5.1'in standart girdi kodlaması SQL aktarımını bozabiliyordu. Kurucu artık SQL'i UTF-8 baytlarıyla gönderir. Gerçek PostgreSQL'de PowerShell 5.1 üzerinden ilk/tekrarlı rol-veritabanı hazırlığı, tam şema ve Türkçe metin roundtrip testi geçti. Kalıcı regresyon: `scripts/Test-ServerSqlTransport.ps1`; `Test-Postgres.ps1` bunu da çalıştırır. İnternet gereksinimi ve açık Windows VM kabul kapısı değişmedi.

# 0.6.0-preview.5 — 29 Ekim 2026

Otomatik kurulum ve yerel yönetici özelliklerini, UTF-8/DNS düzeltmelerini içerir. Yayın kontrolü artık sürüm etiketi ile derlenen kaynak commit'inin aynı olmasını zorunlu tutar; başka kaynak sürümünün dosyaları mevcut sürüm üzerine yüklenmez. Güncel indirme preview.5'tir. Windows VM kabulü hâlâ açıktır.

# Sunucu kurulum rehberi

Kurulum düzeltmesi: Windows PowerShell 5.1 altında SQL UTF-8 olarak okunur ve PostgreSQL istemci kodlaması UTF8 seçilir; Türkçe katalog metinleri korunur. DNS sorgusu başarısızsa sihirbaz makine adıyla açılır. Sunucunun DNS domain bilgisi varsa domain alanına otomatik alınır ve değiştirilebilir.

## Güncel kurulum — 0.6.0-preview.4

Yeni, tek sunuculu pilotta `WFI-Server-Setup.exe` dosyasını yönetici olarak açın. **İleri → ayarları kontrol et → yönetici parolasını iki kez yaz → Kur → Bitir.** Varsayılan dizinler `C:\Program Files\WFI Server` ve `C:\ProgramData\WFI Server`; veritabanı/veri dizinini farklı yerel diske değiştirebilirsiniz. Mevcut veya başka uygulamaya ait dizinleri kullanmayın. İlk giriş kullanıcı adı **local:admin**; parola kurulumda belirlediğiniz en az 14 karakterli paroladır. Hazır veya paylaşılan bir yönetici parolası yoktur.

Kurucu IIS, Visual C++ runtime, .NET 10 Hosting Bundle, PostgreSQL 17 ve Ollama'yı hazırlar; `phi4-mini:latest` modelini indirir. DB sahibi ve uygulama için ayrı rastgele parolalar üretilir. Uygulama hesabına DDL/superuser yetkisi verilmez. Şema yalnız kurulumda uygulanır; uygulama açılışında migration yoktur. Yerel yönetici parolası PBKDF2-SHA256/600000 ile saklanır; parola komut satırına yazılmaz. PostgreSQL `127.0.0.1:55432`, Ollama `127.0.0.1:11435` dinler. Sunucudaki diğer PostgreSQL/Ollama kurulumları yeniden kullanılmaz.

**İnternet gerekir:** birkaç GB bağımlılık ve model indirilir; en az 15 GB boş alan ve model için uygun RAM ayırın. Bu bir çevrimdışı tüm bileşenleri içeren paket değildir. Kurulum paketleri SHA256/SHA512 ile, Microsoft EXE'leri ayrıca Authenticode ile doğrulanır. İsteğe bağlı önbellek: EXE'nin yanında `WFI-Dependencies` klasörüne `scripts/server-dependencies.json` dosyasında adı geçen dört dosyayı koyun. Model indirmesi ayrıca internet ister. Bu dosyalara parola/kurum verisi eklemeyin.

Windows yeniden başlatma isterse kurucu başarı göstermez. Windows'u yeniden başlatın ve aynı dizin/hostname/domain ile yeniden açın. Veritabanı/model aşamasındaki indirilebilir kesintiler aynı korunan kurulum günlüğünden devam eder. Yarım `initdb`, kısmi IIS kurulumu veya kayıtlı mevcut WFI üzerine körlemesine yeniden kurulum yapılmaz: `last-setup-status.txt` ve IIS durumunu inceleyin; verileri silmeyin. Kayıtlı kurulumun son HTTPS kontrolü başarısızsa önce DNS/sertifika/IIS'i düzeltin ve `/api/v1/ready` kontrol edin. Tam rollback veya otomatik yükseltme bu sihirbazın kapsamı değildir.

Sertifika izi boş bırakılırsa belirtilen sunucu DNS adına bir yıllık yerel HTTPS sertifikası oluşturulur ve yalnız sunucunun güven deposuna eklenir. İstemciler için kurum CA sertifikası kullanın veya güveni GPO ile dağıtın; TLS doğrulamasını kapatmayın. Sunucu DNS adı gerçekten bu makineye çözülmelidir. Kurucu Domain profiline HTTPS/443 kuralı ekler; DB ve Ollama için ağdan inbound açmaz. Public profilli pilotta kurumsal firewall politikası ayrıca değerlendirilir.

### LDAP'a sonradan geçiş

Korunan `C:\Program Files\WFI Server\appsettings.local.json` içinde `Ldap.Enabled=true`, Domain, Server, Port=636, BaseDn ve GroupRoles alanlarını kurum AD bilgileriyle ayarlayın. `LocalAdmin.Enabled=true` iken önce ayrı tarayıcı oturumunda gerçek LDAP girişini doğrulayın. Ardından istenirse `LocalAdmin.Enabled=false` yapıp WFI uygulama havuzunu yeniden başlatın. Devre dışı bırakılan veya parolası değişen yerel hesabın eski oturumları kabul edilmez. Yerel hesap AD grup üyeliğine bağlı değildir ve Admin yetkilidir; parolasını güvenli saklayın.

### Kaldırma ve kabul

Programlar ve Özellikler: **WFI Server / Berkay Atağ / kırmızı WFI simgesi**. Kaldırma, sahipliği doğrulanan WFI IIS ve iki özel bağımlılık servisini kaldırır; veritabanı, modeller, kanıtlar, anahtarlar ve yapılandırma saklanır. Paylaşılan IIS/.NET/VC runtime ve sertifikalar otomatik silinmez. Yedekleme ve veri silme ayrı yönetici kararıdır.

Gerçek Windows VM'de temiz kurulum, yeniden başlatma sonrası devam, ilk yerel giriş, DB/AI readiness, LDAP geçişi ve kaldırma henüz kabul kapısıdır. Bu bilgisayara test amacıyla gerçek servis kurulmaz. Aşağıdaki önceki sürüm/manual kurulum bölümleri yükseltme ve özel HA topolojileri içindir; yeni sihirbazın gereksinimleri yukarıdadır.


## 0.5.0 yükseltme

Yedek ve bakım prosedürüyle VerifiedExperienceMemory migration'ını veya aynı paketteki schema.sql dosyasını DBA hesabıyla uygulayın. API ve worker'ı birlikte yükseltin. Yeni kolonlar LearningExcluded / LearningContextJson ve deneyim sorgusu için domain/status/completedAt indeksidir. Üretim başlangıcı şemayı otomatik değiştirmez. Readiness gerekli deneyim kolonlarını da sorgular; migration eksikse HTTP 503 döner ve LB düğümü trafiğe almamalıdır.

`distribution/WFI-Agent-Setup.exe` tek indirme dosyası, `agent-manifest.json` sürüm/boyut/SHA256 bilgisidir. Her API düğümüne aynı ikisini dağıtın. Eski WFI-Agent.zip iç yapı paketidir; konsol indirmesi EXE'yi sunar. Artifact imzalanırsa SHA256 manifestini son imzalı EXE üzerinden yeniden üretin. İmza sonrası düğümler arası hash uyumunu doğrulayın. Hafıza aynı veritabanını kullandığından LB düğümleri paylaşır; her worker kendi inference eşzamanlılık sınırını korur.


## 0.4.1 ajan kurulum dağıtımı

0.7.0'dan itibaren `distribution/WFI-Agent.zip` ara dosyası derlemeden sonra silinir ve sunucu paketine girmez (yalnız EXE'nin gömülü yüküdür). Eski sürümlerde bu ZIP WFI-Agent-Setup.exe ve self-contained ajanı içeriyordu; güncel Admin indirmesi tek EXE sunar. Paket boyutu runtime'lar nedeniyle büyür; reverse proxy indirme zaman aşımı/bant genişliği planlanmalıdır. Dosya wwwroot dışında kalır. Setup mevcut Admin login ve devices/register uç noktalarını kullanır; anonim otomatik kayıt açılmadı. Domain/rol yönetimi LDAPS ayarlarıyla devam eder. Şema değişikliği yoktur.


## 0.4.0 kurulum dosyası

Yeni sunucuya `WFI-0.5.0-pilot.zip` dosyasını açın; **Setup-Server.cmd → Yönetici olarak çalıştır**. Bu, etkileşimli PowerShell kurulumudur; MSI/EXE veya ön koşulları internetten kuran bir bootstrapper değildir. Önce IIS ve .NET 10 Hosting Bundle kurun, PostgreSQL'i hazırlayın ve kurum TLS sertifikasını LocalMachine/My deposuna özel anahtarıyla yükleyin.

`deploy/appsettings.example.json` kopyasını web kökü dışında korunan bir dosyada gerçek yerel değerlerle hazırlayın. Kurulum bu dosyanın yolunu, WFI DNS adını, sertifika thumbprint'ini ve yeni kurulum dizinini sorar. IIS HTTPS sitesi / ayrı application pool oluşturur, uygulamayı kopyalar, ayar dosyası ve yeni data/key dizinleri izinlerini sınırlar. Demo kapalı ve yer tutucular değiştirilmiş olmalıdır. Mevcut site, uygulama veya veri dizinini değiştirmez; yükseltme ve ortak HA depolaması aşağıdaki manuel prosedürü kullanır. Başarısız kurulum kısmi kaynak bırakabilir; kayıtları inceleyip aynı adları yeniden kullanmadan önce düzeltin.

DBA aynı paketteki `deploy/schema.sql` dosyasını ayrı DDL hesabıyla uygular. Setup migration çalıştırmaz, DB/AD/Ollama kurmaz ve firewall açmaz. `/api/v1/ready` 200 ve gerçek LDAPS oturumu doğrulanmadan trafiği açmayın. Pilot için Worker.Enabled=true; ayrı worker/HA yapılandırması aşağıdaki prosedürle yönetilir. Kaynak geliştirme dizininden değil, açılmış dağıtım paketinden Setup-Server.cmd çalıştırılmalıdır.

0.4.0 yükseltmesinde ProblemDiagnosisAndBigFix migration'ını uygulayın; API, worker ve ajan katalog sürümünü birlikte 4.0.0'a yükseltin. Ajan ZIP'i API'nin `distribution` klasöründedir ve wwwroot dışında kalır.


## 1. Topolojiyi hazırlayın

Pilot için bir Windows Server/IIS API, bir PostgreSQL ve korunan veri diski yeterli bir başlangıç topolojisidir; bu bir kapasite taahhüdü değildir. Üretim HA için en az iki API, ayrı worker süreçleri, HA PostgreSQL ve erişilebilir ortak log/key storage planlayın. CPU/RAM/disk boyutunu docs/capacity.md ölçüm planıyla belirleyin.

DNS'te WFI FQDN'ini (ör. wfi.example.invalid) VIP veya pilot sunucuya yönlendirin. HTTPS sertifikasının adı bu FQDN'i kapsamalıdır. LDAPS DC sertifikası ve PostgreSQL sertifikası için CA zincirini servis hesabının kullandığı makine güven deposuna kurun. Firewall ekibine firewall-requirements.md verin.

## 2. Paketi üretin

Derleme bilgisayarına .NET 10 SDK, Node.js 22+ ve pnpm 11 kurun. Proje kökünde `./scripts/Build.ps1` çalıştırın. Çıktılar `artifacts/api` ve `artifacts/worker` dizinleridir. Üretim API sunucusunda .NET 10 Hosting Bundle ve IIS ASP.NET Core Module gerekir; worker sunucusunda .NET 10 Runtime yeterlidir. Node.js üretim sunucusunda gerekmez.

API paketini örneğin `D:\WFI\Api`, worker paketini `D:\WFI\Worker` konumuna kopyalayın. Raw log ve key dizinlerini web kökü dışına koyun. Servis hesabına uygulama dosyalarında okuma, log/key dizinlerinde gerekli okuma/yazma, DB'de sınırlı veri yetkileri verin; Domain Admin kullanmayın.

`scripts/Package.ps1` çalıştırılarak oluşturulan `artifacts/WFI-0.5.0-pilot.zip` içinde api, worker, agent, scripts, deploy ve docs klasörleri bulunur. Yeni pilot kurulumda Setup-Server.cmd kullanılabilir; ön koşullar ve DBA migration adımı ayrıca uygulanmalıdır. Yerel ayar dosyaları ve runtime verileri pakete dahil edilmez.

## 3. AD ve yerel ayarlar

AD'de WFI-Viewers, WFI-Analysts, WFI-Admins güvenlik grupları oluşturun veya mevcut onaylı grupları seçin. Tam DN'lerini AD ekibinden alın. `deploy/appsettings.example.json` dosyasını API paketinde `appsettings.local.json` olarak kopyalayın. Gerçek değerlerle doldurun:

- `Ldap.Domain`: varsayılan example.invalid; değiştirilebilir.
- `Ldap.Server`: LDAPS sertifikasıyla eşleşen DC FQDN; `BaseDn` ve `GroupRoles` gerçek DN'ler.
- `AllowedHosts`: WFI FQDN; `Demo.Enabled=false`.
- `ConnectionStrings.Wfi`: gerçek PostgreSQL bağlantısı, `SSL Mode=VerifyFull` ve gerekiyorsa kurum CA dosyası.
- `Storage.Path`, `DataProtection.Path`: web kökü dışındaki mutlak yollar.
- HA kullanılıyorsa load-balancer.md ayarlarını tamamlayın.

Parolayı depoya eklemeyin. Environment üzerinden `ConnectionStrings__Wfi` tanımlamak yerel dosyayı geçersiz kılar. Bu dosyayı servis hesabı/yetkili yönetici dışında okunamaz yapın. AD giriş parolaları ayrıca yapılandırılmaz; kullanıcı arayüzde kendi hesabıyla giriş yapar.

## 4. Veritabanını hazırlayın

DBA ayrı wfi veritabanı ve çalışma hesabı oluşturur. Migration için ayrı DDL yetkili hesap kullanın. Paket dizininde Production ortamında `dotnet WFI.Api.dll --migrate` çalıştırın veya DBA'ya `deploy/schema.sql` idempotent dosyasını uygulattırın. İkisini farklı sürümlerden karıştırmayın. Ardından DDL yetkisini kaldırıp çalışma hesabına geçin. Şema otomatik production başlangıcında değişmez.

0.1'den yükseltirken DB+log+key yedeği alın; SessionRevocationIndex migration'ını uygulayın. Önceki oturumlar tekrar giriş ister. Şema uygulanmadan yeni sürümü LB'ye almayın. Geri dönüşte tüm paket/config/DB yedeği beraber değerlendirilmelidir; production'da otomatik migration downgrade çalıştırmayın.

## 5. IIS ve worker

IIS sitesi fiziksel yolunu API paketine ayarlayın. App pool için No Managed Code, ayrılmış kimlik, AlwaysRunning ve uygun idle/recycle politikası kullanın. HTTPS 443 binding'i ekleyin. Form tabanlı LDAPS için Anonymous Authentication açık, Windows Authentication kapalıdır. Ortam Production olmalıdır. requestFiltering `maxAllowedContentLength` için 21 MiB (22020096 bayt) kullanın; uygulama log başına 20 MiB sınırı uygular.

Pilot tek API'de `Worker.Enabled=true` kullanılabilir. Ayrı worker kurulumunda tüm API'lerde false yapın; worker dizinine aynı DB bağlantısını içeren korunan appsettings.local.json koyun. Worker'ı Windows Task Scheduler'da sunucu açılışında, kullanıcı oturum açmasa da çalışan ayrılmış servis hesabıyla başlatın: program `dotnet.exe` tam yolu, argüman `D:\WFI\Worker\WFI.Worker.dll`, çalışma dizini `D:\WFI\Worker`. Zaman sınırı koymayın, hata halinde yeniden başlatma ve birden fazla kopyayı başlatmama ayarı yapın. Bu paket kendiliğinden Windows Service kaydı oluşturmaz; `sc create` ile doğrudan kaydetmeyin.

API'nin DB'ye yazdığı StorageKey mutlak yoldur. Ayrı düğümlerde ortak UNC yolu kullanılmalıdır; aynı `D:\...` metni iki ayrı yerel diski ortak yapmaz. Worker key ring'e ihtiyaç duymaz, raw logları okumalıdır. API key ring'e yazabilmelidir.

## 6. Kabul ve işletim

HTTPS üzerinden `/api/v1/health` canlılık, `/api/v1/ready` DB/şema erişimi 200 dönmeli. Readiness storage/LDAP'ı sınamaz. Yetkili/yetkisiz, Viewer/Analyst/Admin, nested grup, yanlış parola, geçersiz CA, DC kesintisi testlerini yapın. Sentetik dosya yükleyip sonucun tamamlandığını ve çıkış sonrası erişimin kesildiğini doğrulayın.

Raw storage'a işletim sistemi/dosya sunucusu kotası, düşük boş alan alarmı ve saklama prosedürü uygulayın. Uygulamada toplam filo disk kotası veya otomatik purge yoktur. DB, log ve key ring yedeğini birlikte alın; geri yüklemeyi test edin. Logout kayıtlarını 30 dakika dolmadan silmek iptali geri alabileceğinden en az bu süre korunmalıdır; normal audit saklama süresi çok daha uzun tutulur. CPU/RAM, disk, DB bağlantıları, HTTP hata oranı, kuyruk yaşı ve worker heartbeat'i izlenmelidir. 40.000 cihaza yayılım kabul planı tamamlanmadan yapılmamalıdır.


## 7. Ollama ve 0.3.0 yükseltmesi

Önce DB, raw storage ve key ring yedeği alın; geri yüklemeyi test edin. Eski API/worker süreçlerini bakımda durdurun. Aynı paketin tüm migration'larını uygulayın: AgentAndOllama, RemediationCatalog ve AutomationScheduling dahil. Sonra API ve worker'ları birlikte yükseltin. Eski DLL'leri yeni paketle karıştırmayın. 0.4.0 ajan ve sunucu katalog sürümü 4.0.0 olmalıdır.

Ollama'yı model sunucusuna kurun, kurumun onaylı ağından `ollama pull phi4-mini` ile modeli hazırlayın. API/worker ile aynı makinede varsayılan adres http://127.0.0.1:11434'tür. Ayrı makinede HTTPS reverse proxy, kurum CA ve yalnız API/worker kaynaklarına firewall izni gerekir; uygulama uzak düz HTTP'yi reddeder. Ajanlara Ollama kurulmaz.

API VE ayrı worker'ın appsettings.local.json dosyasına:
```json
"Ollama": {
  "BaseUrl": "http://127.0.0.1:11434",
  "DefaultModel": "phi4-mini:latest",
  "TimeoutSeconds": 120
}
```

Ayrı worker'da localhost o worker makinesini ifade eder. Aynı model servisini hedefleyin. Timeout 10–240 saniye aralığına sınırlandırılır; process başına tek eşzamanlı inference vardır. Arayüz Admin → Cihazlar ve AI → Ollama modeli → Modeli kaydet. Yalnız gerçek kurulu model seçilebilir; tercih ortak DB'dedir. Sonradan kurulan modeller için ekranı yeniden açın.

Ollama'nın kendisinde kimlik doğrulama varsaymayın; model servisini kullanıcı/istemci ağına açmayın. Model indirmeleri kurulum zamanındadır; inference yerelde kalır. API sözleşmesi: [Ollama resmi API](https://github.com/ollama/ollama/blob/main/docs/api.md).

Geliştirme SQLite'ında yeni tablolar ve zamanlama sütunları korunarak eklenir; bu, her üçüncü taraf şemanın desteklendiği anlamına gelmez. Üretim PostgreSQL migration'ıyla yönetilir. İlk v0.2 demo DB'si yeni sürümle açıldı; çalıştırma öncesi yerel DB/WAL yedeği alındı.

## 8. Ajan pilot kabulü

İstemci rehberindeki tek cihaz kurulumunu tamamlayın. Son bağlantı güncellenmeli; teşhis pending → running → evidence_received → analyzing → completed/failed ilerlemelidir. Model servisinin kapalı olması başarısız analiz üretmeli. Önce düşük riskli manuel işlem, sonra tek cihaz otomatik mod kabulü yapın. Başarılı komut bütün arızanın çözülmesi değildir.

Bu geliştirme bilgisayarına LocalSystem servisi kurulmadı. Gerçek LDAPS, PostgreSQL ve LB/SMB failover kabulü ayrıca yapılmalıdır. Geri dönüşte yeni işler kabul etmeyi durdurun; yürüyen düzeltmeyi tekrarlamayın. Unknown sonuçları cihazda inceleyin. Kapasite kabulünden önce 40.000 cihazın otomatik modunu topluca açmayın.


## Ajan güncelleme uyumluluğu

Ajan kurulum betiği yükseltmede mevcut DeviceId/ServerUrl/state yolunu doğrular; kimliği otomatik değiştirmez. PollSeconds verilmezse eski aralık korunur. Yeni paket dağıtılırken DB/API/ajan katalog uyumluluğunu koruyun. HTTP sonucu kaybı ve süreç yeniden başlama testleri 70 testlik yerel seride geçti; bunlar gerçek Windows SCM kurulum kabulünün yerine geçmez.


## Derleme bilgisayarında SDK/runtime ayrımı

Runtime çalıştırmaya yeterlidir, kaynak derlemeye yetmez. Build.ps1 ve Start-Demo.ps1 artık --list-sdks çıktısında .NET 10 bulunan dotnet kurulumunu seçer. PATH yalnız runtime içeriyorsa %LOCALAPPDATA%/WFI/dotnet adayı kontrol edilir; hiçbir aday uygun değilse açık SDK hatası verilir. Resolve-Dotnet.ps1 dosyasını bu betiklerle beraber tutun. Üretim API/worker çalıştırması için SDK zorunluluğu eklenmedi.


Model keşfi HTTP/JSON/timeout hatasında arayüz bağlantı uyarısı gösterir; kurulu modellerin boş görünmesi seçili modelin DB'den silindiği anlamına gelmez. Ollama bağlantısını düzelttikten sonra Cihazlar ve AI ekranını yeniden açın. Geçersiz model adları listelenmez. Bu durum gerçek teşhis çağrısının başarıyla tamamlandığını göstermez.


İptal/onay durum geçişleri aynı transaction içinde audit edilir. Son yerel testler SQLite üzerinde eşzamanlı onay ve inference sırasında iptali kapsar; üretim PostgreSQL/LB testinde bu yarışları da tekrarlayın. Terminal sonuçları SQL ile elle Pending/Approved durumuna döndürmeyin.


### Cihaz listesi indeks yükseltmesi

Bu pakette 20261023221516_DeviceInventoryPaging migration'ı vardır; Devices üzerinde Domain/LastHeartbeat/Id indeksini ekler. deploy/schema.sql aynı migration'ı içerir. Mevcut üretim veritabanını yedekleyin; önce test PostgreSQL ortamında süre ve kilit etkisini ölçüp bakım penceresinde uygulayın. Standart CREATE INDEX yazmaları bekletebilir; uygulama açılışında otomatik migration çalıştırmayın. Yerelde EF model/migration uyumu kontrol edildi; gerçek PostgreSQL uygulaması henüz doğrulanmadı.

### Politika kayıt düzeltmesi

Bu güncellemede ek migration yoktur. API dosyalarını güncel paketle değiştirip yeniden başlatın; LB arkasındaki tüm düğümlerde aynı sürümü kullanın. Politika değişikliği audit ile birlikte kaydedilir. Audit yazımı başarısızsa ayar değişmez; sunucu/DB loglarını inceleyin ve bağlantı/yazma sorunu çözüldükten sonra yeniden deneyin.

Portal teşhis iptali güncellemesi için API ile birlikte wwwroot dosyalarını da yeni paketten dağıtın. Yeni migration/ayar gerekmez. Analyst/Admin rol eşlemeleri mevcut iptal API'sine erişimi belirler; Viewer iptal yapamaz.

Atama süresi düzeltmesi ajan tarafındadır; istemcilere güncel agent paketini dağıtın. Veritabanı migration'ı veya sunucu yapılandırma değişikliği gerektirmez. Mevcut kurum zaman eşitlemesi korunmalıdır; hatalı istemci saati geçerli işleri erken reddettirebilir.

### Yerel PostgreSQL ön kabulü

PostgreSQL 17.11 üzerinde EF migration ve deploy/schema.sql ilk kurulum/tekrar çalıştırma testleri geçti; iki bağlantıyla tek cihaz dispatch kontrolü doğrulandı. Kaynak checkout içindeki Test-Postgres.ps1 için postgresql-validation.md belgesine bakın. Bu sonuç kurum yedekten dönüş, yükseltme hacmi, LB/failover ve üretim yetkileri testlerinin yerine geçmez.

Yerel PostgreSQL oturum kabul testi Kestrel/loopback TCP üzerinde iki host'u doğrular. Aynı process'teki bu testin geçmesi ayrı üretim API sunucuları, TLS ve proxy failover kabulünü tamamlamaz. Kurulum parametreleri değişmedi.

Yerel Test-Postgres.ps1 artık kaynak checkout'taki Release API DLL'ini iki ayrı dotnet sürecinde test eder. Runtime yolu otomatik seçilir; süreçler yeni geçici content-root ile başlar, mevcut appsettings.local.json dosyanız okunmaz. Test Development/demo/loopback içindir; üretimde demo açmayın. Gerçek kurum LB/TLS/AD kabulü ayrıca gereklidir.

YARP reverse proxy bu sürümde yalnız kaynak checkout kabul testi yardımcısıdır. Üretim API paketi proxy servisi kurmaz; kurumun TLS/LB kurulum adımları geçerlidir. Yerel proxy testinin geçmesi üretim LB/sertifika kabulünü tamamlamaz.

Yinelenen manuel teşhis koruması için API'yi güncel paketle değiştirin; yeni migration/port gerekmez. Aktif işte 409 beklenen yanıttır, servis arızası değildir. Tüm LB düğümlerini aynı sürüme yükseltin; eski düğümün eski kayıt davranışı yeni düğüm tarafından engellenemez.

### Teşhis kabul sınırı — 2026-10-24
Domain başına aktif teşhis sınırı eklendi (varsayılan 1000). Manuel/yeniden analiz dolu kuyrukta 429, otomatik tarama gecikmeli yeniden deneme kullanır. Tüm API düğümlerinde aynı Diagnostics__MaxActivePerDomain değeri gerekir. Yeni port veya istemci kurulumu değişikliği yoktur; mevcut HTTPS ve API–PostgreSQL bağlantıları kullanılır. Ayrıntılar: [Teşhis kabul sınırı](diagnostic-admission.md). Gerçek AD/üretim HA ve 40.000 aktif istemci kabulü henüz yapılmadı.


### AI yeniden analiz çakışması düzeltmesi
Eski teşhisin yeniden analizi, aynı cihazda pending/running/evidence_received/analyzing iş varsa 409 ile reddedilir. Kontrol domain transaction kilidi içinde manuel/otomatik kabul ile sıralanır. Kabul edilen yeniden analiz eski CompletedAt değerini temizler. Şema, port ve ajan kurulum gereksinimleri değişmedi.

### Ajan kanıt teslimi sınır testleri
Sentetik regresyonlar; süresi dolan lease, iptal edilmiş teşhis ve başka cihazın lease'iyle kanıt gönderimini kapsar. Bu talepler 409 ile reddedilmeli, kanıt ve audit kaydı oluşturmamalıdır. Süresi dolan iş yeniden atandığında eski lease geçersiz kalır; yeni lease ile teslim tek audit üretir. Uygulama davranışı, şema ve ağ/kurulum gereksinimleri değiştirilmedi.

### Düzeltme kuyruğunda bekleme hatası — 2026-10-25
İlk 10 bekleyen iş politika veya katalog nedeniyle uygun olmadığında sonraki geçerli manuel iş seçilemiyordu. Uygunluk filtresi artık sorgunun limitinden önce uygulanır; sıralama zaman ve iş kimliğiyle kararlıdır. Kapalı otomatik işler çalıştırılmaz ve silinmez; politika tekrar açıldığında uygun olabilir. Şema, firewall portları ve ajan kurulum adımları değişmedi.

### Ajan servis yükseltme önkontrolü — 2026-10-25
Install-Agent.ps1 mevcut servisin LocalSystem hesabını ve seçilen kurulum dizinindeki WFI.Agent.exe yolunu token isteminden, servis durdurmadan ve dosya değişikliğinden önce denetler. Özel hesap, ek komut argümanı, farklı binary yolu veya okunamayan servis yapılandırması reddedilir. Böyle bir kurulumda hesabı zorla değiştirmeyin: DPAPI state ve mevcut servis yapılandırmasını koruyan ayrı bakım planı hazırlayın. Yeni firewall portu veya sunucu şeması değişikliği yoktur. Test-AgentInstaller.ps1 altı sentetik önkontrol senaryosunu servis değiştirmeden çalıştırır ve Build.ps1'e dahildir; gerçek LocalSystem servis kabulü yerine geçmez.

### AI değerlendirmesinde kanıt ve belirsizlik — 2026-10-25
Teşhis sonucu ekranı modelin bildirdiği güveni, eksik kanıtları, hipotez kanıt atıflarını ve önerilen doğrulama adımlarını gösterir. Güven değeri ölçülmüş doğruluk/başarı olasılığı değildir; eksik veya geçersiz değer için yüzde uydurulmaz. Boş eksik-kanıt listesi tamlık garantisi olarak gösterilmez. Model metni düz React metni olarak görüntülenir; doğrulama adımları otomatik komut olarak çalıştırılmaz. Yeni endpoint, port, şema veya ajan ayarı yoktur.

### PostgreSQL büyük envanter doğrulaması — 2026-10-25
Kabul paketine 40.000 sentetik cihaz içeren PostgreSQL envanter senaryosu eklendi. Aynı heartbeat zamanında kimlikle kararlı sıralama, ilk/orta/son sayfalar, 50 kayıt sınırı, domain ayrımı, arama normalizasyonu ve listeden büyük/özel alanların çıkarılması sınanır. EF migration ve schema.sql yollarının ikisinde de çalışır. Tek kullanıcı ve TestServer API çağrıları kullanır; 40.000 aktif ajan, eşzamanlı trafik, ağ/TLS/LDAP veya üretim kapasitesi ölçümü değildir. Ürün davranışı, şema, kurulum ve firewall gereksinimleri değişmedi.

### Başlamamış düzeltmeyi iptal etme — 2026-10-25
Analyst/Admin kendi domain'indeki Approved veya PendingApproval düzeltme isteğini Düzeltme geçmişi → Bekleyen düzeltmeyi iptal et ile Cancelled durumuna alabilir. Viewer ve başka domain erişimi reddedilir. API: POST /api/v1/remediations/{id}/cancel; CSRF gerekir. Koşullu durum güncellemesi ve audit aynı transaction'dadır; iptal edilmiş iş ajana verilmez ve yeniden onaylanamaz. Ajan işi önce almışsa 409 döner; Executing/Unknown/Success/Failed durumları değişmez. İptal işletim sistemi rollback'i değildir. Otomatik işi iptal etmek otomatik politikayı kapatmaz veya 24 saatlik beklemeyi sıfırlamaz; ileride uygun yeni iş oluşabilir. Kalıcı durdurma için cihaz/genel otomatik modu kapatın. Yeni port, şema veya ajan kurulumu değişikliği yoktur.

### Bağlantı önkontrolü — 2026-10-25
Test-WfiConnectivity.ps1 eklendi: parola kullanmadan DNS, HTTPS health ve readiness yanıtlarını kontrol eder. Sertifika doğrulaması korunur; redirect/HTML başarı sayılmaz; hiçbir sistem ayarı değiştirilmez. Kullanım: [Bağlantı önkontrolü](connectivity-preflight.md). Mevcut DNS ve WFI HTTPS portu kullanılır; ek firewall izni gerekmez. AD/model/ajan/HA/kapasite kabulü yerine geçmez.


### Yinelenen manuel düzeltme isteği — 2026-10-25
Aynı cihaz/katalog işlemi PendingApproval, Approved veya Executing durumundayken yeni manuel istek 409 ve mevcut iş ID'siyle reddedilir. Cihaz satırı transaction kilidi eşzamanlı manuel istekleri sıralar; revoked cihaz yeniden kontrol edilir. İş ve audit birlikte kaydedilir. İptal edilmiş işin yerine yeni istek verilebilir; Unknown sonucu olan cihazda yeniden onarım öncesi operatör incelemesi gerekliliği devam eder. Bu kontrol tamamlanmış işlemlere karşı kalıcı idempotency anahtarı değildir. Yeni şema/port/ajan kurulumu gerektirmez.

### Eski/belirsiz düzeltmenin otomatik tekrarını engelleme — 2026-10-25
Otomatik karar, manuel isteklerle ortak cihaz satırı kilidini aldıktan sonra güncel cihaz politikasını ve mevcut işleri kontrol eder. Aynı katalog işlemi PendingApproval/Approved/Executing/Unknown durumundaysa 24 saatten eski olsa da otomatik tekrar oluşturulmaz. Son 24 saat filtresi ve cihaz bekleme süresi korunur. Tamamlanmış veya iptal edilmiş eski iş, diğer koşullar sağlanırsa gelecekte yeni otomatik işe engel değildir. Unknown durumunda cihazın gerçek sonucunu inceleyin; bu durum otomatik başarı veya rollback değildir. Yeni şema, port veya ajan kurulum gereksinimi yoktur.

### AD kabul planı — 2026-10-25
Kaynak incelemesiyle docs/ad-acceptance.md eklendi. Kısa adın UPN'e çevrilmesi (ayrı sAMAccountName araması yapılmaması), alternatif suffix/NetBIOS sınırı, nested/primary grup davranışı, grup türü zorlamasının bulunmaması ve mevcut oturumun grup değişikliğinde hemen iptal olmaması açıklandı. Gerçek AD test sonucu üretilmedi; kurum ortamı hazır olduğunda uygulanacak kabul matrisi hazırlandı. Uygulama kodu/port/şema/kurulum değişmedi.

### AD security-group tür kontrolü — 2026-10-25
Önceki incelemede belirtilen tür kontrolü eksikliği giderildi. Üye olunan eşlenmiş grubun groupType security-enabled biti yoksa veya değer eksik/bozuksa rol verilmez. İşaretli/işaretsiz 32-bit değerler doğrulanır. Yükseltmeden önce distribution gruplarını rol eşlemesinden çıkarıp uygun security gruplarını belirleyin; mevcut cookie ömrü politikası değişmedi. Gerçek AD bind/nested/izin kabulü ayrıca yapılmalıdır. Şema/port/ajan değişikliği yoktur. Teknik kaynak: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-adts/11972272-09ec-4a42-bf5e-3e99b321cf55

Kaynak checkout üzerinde yerel kullanıcı denemesi: [yerel test rehberi](local-testing.md). Loopback demo üretim HTTPS/LDAPS kabulü değildir; ZIP paketinin kurulumu yukarıdaki üretim adımlarını kullanır.

Kurulum cmdlet başvuruları: [Microsoft New-Website](https://learn.microsoft.com/en-us/powershell/module/webadministration/new-website?view=windowsserver2025-ps), [New-WebBinding ve sertifika bağlama](https://learn.microsoft.com/en-us/powershell/module/webadministration/new-webbinding?view=windowsserver2025-ps). Kurucu Windows PowerShell 5.1 üzerinden başlatılır.

## 0.6 geliştirme — kapasite temeli (devam ediyor)

Yeni ajanlar combined-check-in-v1 yeteneğiyle /api/v1/agent/me/check-in kullanır; eski sunucuda yalnız 404 alındığında eski heartbeat/claim akışına döner. Yeni sunucu eski uç noktaları korur. Boşta aralık kademeli 300 saniyeye çıkar (yerel minimum daha uzunsa korunur), hata beklemesi jitter ile artar; Retry-After dikkate alınır. Sonucu bilinmeyen onarım otomatik tekrar yürütülmez.

SharedModelCapacity migration'ı ve güncel deploy/schema.sql birlikte dağıtılmalıdır. Ollama:MaxConcurrent varsayılan 1 (1..64); aynı model sunucusunu paylaşan tüm düğümlerde aynı değer kullanılır. 300 saniyelik DB saatli slot, token/generation fencing ve en çok 240 saniye model isteği bütçesi vardır. Eski worker'lar slot protokolünü kullanmadığından yükseltme sırasında eski worker'lar durdurulmalıdır; kapasite sınırı karışık worker sürümlerinde garanti edilmez.

Admin Sistem Sağlığı ekranı domain iş kuyruğu özetini gösterir. Telemetry:OtlpEndpoint boşsa dışa metrik gönderilmez. İsteğe bağlı OTLP gRPC collector için yalnız yapılandırılmış sunucu hedefi/portu açılır (yaygın collector portu 4317); bu ayar collector teslimat doğrulaması değildir. Yeni istemci inbound portu yoktur.

On maddelik plan henüz tamamlanmamıştır. Kampanya, BigFix merkezi ve AI kalite uygulamaları eklendi; açık geliştirmeler ve kabul sınırları docs/implementation-roadmap.md içinde, sayısal test sonuçları doğrulama belgesinde tutulur.

## 0.6 geliştirme güncellemesi — 26 Ekim 2026
Kampanya, BigFix merkez doğrulaması ve AI kalite laboratuvarının kurulum/kullanım sınırları docs/operations-0.6.md içinde belgelenmiştir. BigFix ve isteğe bağlı OTLP yalnız sunucudan outbound erişim ister; yeni istemci inbound portu yoktur. Yeni şema uygulanmadan readiness başarılı olmaz. Bu geliştirme henüz on maddelik yol haritasının tamamı değildir; güncel durum docs/implementation-roadmap.md içindedir.



## 0.6.0-preview.2 — ajan ve yayın hazırlığı (27 Ekim 2026)

Ajan çalışma zamanı ve PowerShell kurucusu yalnız HTTPS origin kabul eder; URL içinde kullanıcı bilgisi, path, query veya fragment reddedilir. HTTP yanıtı 1 MiB, kalıcı ajan durumu 128 KiB ile sınırlıdır. Başka lease'e ait veya işsiz receipt içeren bozuk günlük korunarak reddedilir; otomatik kimlik sıfırlama yapılmaz. 401 ve 503 eski protokole dönüş tetiklemez; 429 Retry-After korunur. Onarım cevabı kaybolduğunda işlem tekrar yürütülmez.

Denetim kayıtlarına Domain sütunu ve indeks eklendi. API artık yalnız oturumun domain'indeki kayıtları döndürür. Eski domain'i belirsiz kayıtlar silinmez, web listesinden gizlenir; DBA tarafından ayrıca incelenebilir. AuditDomainScope EF migration ve deploy/schema.sql birlikte güncellendi. Üretimde migration başlangıçta otomatik çalışmaz.

WFI-Server-Setup.exe yönetici yükseltmesi isteyen tek dosyalı, etkileşimli sunucu kurucusudur. Yükünü Program Files altında admin/SYSTEM korumalı geçici dizine açar; yerel yapılandırma dosyası, hostname, TLS sertifikası ve kurulum yolu ister. IIS + .NET 10 Hosting Bundle ve hazırlanmış PostgreSQL önkoşuldur. Veritabanı ve Ollama otomatik kurulmaz. HA/upgrade manuel bakım adımlarını kullanır. Otomasyon için Install-Server.ps1 kullanılabilir. Setup EXE'leri henüz Authenticode imzalı değildir.

Yeni port gerekmiyor. Ajan → WFI HTTPS 443; sunucu → LDAPS/DB/Ollama ve koşullu BigFix/OTLP mevcut firewall tablosuna göre sınırlandırılır. GitHub Pages statik tanıtımdır, canlı kurum konsolu veya log barındırmaz. Public kaynak örneğinde domain example.invalid olarak temizlenir; yerel kurum checkout varsayılanı korunur.
## 0.6.0-preview.3 — 29 Ekim 2026

- Yeni oturumlarda kırmızı-beyaz aydınlık tema varsayılan; sağ üstten karanlık tema seçimi kalıcıdır. Standart seçimi eski tercihler için aydınlık tasarımın uyumlu seçeneği olarak korunur. Onarım Kampanyaları ekranı Onarım Otomasyonu olarak adlandırıldı; API ve politika sınırları değişmedi.
- Sunucu ve istemci setup EXE'leri kırmızı WFI simgesi taşır. Kurulumdan sonra appwiz.cpl içinde WFI Server / WFI Agent, Publisher: Berkay Atağ, sürüm ve simge kaydedilir. Publisher metni Authenticode imzası değildir; paketler imzasız pilot sürümdür.
- Kaldırma yönetici yetkisi ister; servis/IIS sahipliği, kayıtlı kurulum yolu ve reparse kontrollerinden sonra çalışır. Veritabanı, kanıt, anahtarlar, ajan kimliği ve appsettings.local.json korunur. Çalışan kaldırıcı ve küçük kurtarma yardımcıları korumalı kurulum klasöründe kalır. Yeniden kurulum/yükseltme için korunan yapılandırmayı ve kimliği kullanarak bakım rehberini izleyin; fresh-install kurucusu var olan veri/anahtar dizinlerini devralmaz.
- İstemci kurulumunda üst klasör junction/symlink geçişi reddedilir. Sunucuda uygulama, kanıt ve anahtar dizinleri aynı veya iç içe olamaz. Kaldırıcı başka servis yolu veya paylaşılan IIS havuzunu kaldırmaz; alt IIS uygulaması varsa manuel bakım gerekir.
- Yeni port yoktur. Ajan outbound HTTPS 443; LDAPS/PostgreSQL/Ollama ve koşullu BigFix/OTLP erişimleri mevcut firewall tablosuna tabidir.
- GitHub yeni sürümünde sunucu EXE, dağıtım ZIP'i ve SHA256 bulunur. İstemci EXE ayrı release dosyası değildir; sunucu paketinin içindeki konsoldan Ajan ve Ağ Erişimleri üzerinden indirilir. Önceki sürümler tarihsel olarak korunur.
- Gerçek Windows VM appwiz/kurulum/kaldırma, AD/HA ve 40 bin istemci kabulü açık kalır. Codex Security başlatma girişimi test bağımlılığındaki uzun dosya yolu nedeniyle yine rapor oluşturamadı; davranış regresyonları ayrıca çalıştırılır.

