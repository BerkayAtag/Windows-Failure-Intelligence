# Yerel PostgreSQL doğrulaması

Bu kontrol kurum bağlantısı kullanmaz. Test betiği yalnız 127.0.0.1 üzerinde geçici PostgreSQL cluster açar; SCRAM parolası rastgele üretilir ve çıktıya yazılmaz. Windows servisi kurmaz, firewall kuralı eklemez. Test projesi yalnız loopback hedef kabul eder ve kendi rastgele wfi_test_ veritabanını oluşturup sonunda kaldırır. Betik cluster sürecini finally içinde durdurur. Cluster/log dizini inceleme için TEMP altında kalır; bootstrap parola dosyası silinir.

## Çalıştırma

Kaynak checkout, .NET 10 SDK ve PostgreSQL Windows binaries gerekir. Bu test projesi normal WFI.slnx/Build.ps1 testlerine dahil değildir; PostgreSQL olmadan sessizce başarılı sayılmaz. Dağıtım ZIP'i kaynak test projesini içermez; aşağıdaki komutu kaynak checkout içinde çalıştırın.

```powershell
./scripts/Test-Postgres.ps1 -PostgresBin 'C:\tools\pgsql\bin' -Port 55439
```

Port doluysa ayrı boş bir port seçin. Mevcut PostgreSQL servisini veya kurum veritabanını kullanmayın. Betik yalnız kendi oluşturduğu cluster'ı durdurur. Ani makine kapanması/betik zorla öldürülmesi halinde finally çalışmayabilir; raporlanan test dizini için pg_ctl status/stop ile süreç kontrol edilmelidir.

## Doğrulanan kapsam

PostgreSQL 17.11 Windows x64 üzerinde **2/2 test** geçti:

- Bir boş veritabanına EF migration zinciri uygulandı ve tekrar çalıştırıldı.
- İkinci boş veritabanına deploy/schema.sql iki kez uygulandı; EF migration durumu bekleyen değişiklik göstermedi.
- Her iki kurulumda katalog kayıt sayısı doğrulandı.
- YARP test reverse proxy'si tek loopback adresinde round-robin dağıtım yaptı. Test yanıt işaretçisi iki backend portunun da kullanıldığını doğruladı. İlk API süreci kapatıldıktan sonra aynı proxy adresinden kalan backend'e art arda 10 auth/me isteği 200 döndü; proxy üzerinden CSRF korumalı logout ve ardından 401 doğrulandı.
- Derlenmiş API iki ayrı dotnet sürecinde başlatıldı (PID'ler farklı); loopback TCP üzerinden ortak session/logout doğrulandı. İlk süreç zorla sonlandırıldıktan sonra ilk port bağlantıyı reddetti; ikinci süreç geçerli session ile auth/me ve automation okumalarını sürdürdü.
- İki bağımsız Kestrel API host'u farklı loopback TCP portlarında ortak key dizini/PostgreSQL ile aynı cookie oturumunu kabul etti. İlk örnekte logout sonrası ikinci örnek eski cookie'yi 401 ile reddetti. Yeni oturum açıldıktan sonra ilk örnek kapatıldığında ikinci örnek auth/me ve automation okumalarını sürdürdü.
- PostgreSQL destekli API: Viewer genel/cihaz politikasını değiştiremedi; yabancı domain yöneticisinin cihaz değişikliği 404 döndürdü. Tüm cihazlara uygulama yabancı domain istisnasını korudu.
- PostgreSQL trigger ile audit yazımı reddedildiğinde genel politika + cihaz istisnası ve cihaz bazlı değişiklik geri alındı; API 503 döndürdü, başarılı audit sayısı değişmedi.
- İki bağımsız OllamaService/DI örneği aynı ilk model kaydına eşzamanlı ulaştırıldı: tek ayar satırı kaldı, sonraki geçerli seçim diğer örnekten okundu, kurulu olmayan seçim reddedildi ve diğer AI alanları korundu. Model keşfi sentetiktir; gerçek Ollama inference yapılmaz.
- İki bağımsız DbContext/bağlantı aynı cihazın iki bekleyen düzeltmesini eşzamanlı sahiplendiğinde tek iş Executing oldu; diğer iş Approved kaldı.
- İlk işin sonucu iki kez gönderildiğinde tek audit kaydı kaldı; sonra ikinci iş sahiplenilebildi. Gerçek OS komutu çalıştırılmadı.

Bu test PostgreSQL SQL/migration ve dar kapsamlı transaction davranışı kanıtıdır. Aynı process içi iki Kestrel host testi yanında iki ayrı dotnet süreci de aynı bilgisayarda gerçek loopback TCP kullanır. Ayrı fiziksel sunucular, kurum proxy/LB cihazı, TLS ve DB failover, AD, büyük filo veya kapasite ölçümü değildir. Süreç testi Development/demo modunda çalışır; Production HA sertifika koşulları doğrulanmaz. Model seçimi ve politika API davranışları kapsanır. Politika API testleri WebApplicationFactory/TestServer, oturum testleri WebApplicationFactory/Kestrel üzerinde çalışır; cookie/CSRF/rol middleware gerçektir, LDAP doğrulayıcısı sentetiktir. Loopback HTTP sınanır; kurum ağı/TLS ve AD kabulü değildir. Politika API eşzamanlılık yarışlarının tamamı bu projede sınanmaz; SQLite regresyonları ayrı kapsamdır.

## Runtime kaynağı

PostgreSQL'in [Windows indirme sayfası](https://www.postgresql.org/download/windows/) üzerinden yönlendirilen [EDB binaries](https://www.enterprisedb.com/download-postgresql-binaries) kullanıldı. Yerel test için indirilen postgresql-17.11-5-windows-x64-binaries.zip SHA-256:

80379B2C04D51C30225532E0AE04509899141E9957ED096FE749D7FD9DF8F82F

Değer [EDB checksum dosyası](https://get.enterprisedb.com/postgresql/postgresql-17.11-5-windows-x64-binaries.zip.sha256) ile eşleşti. postgres.exe Authenticode durumu NotSigned idi; imzalı paket iddiası yapılmaz. Runtime .data altında tutulur ve WFI dağıtım ZIP'ine dahil edilmez.






## Yerel reverse proxy sınırı

YARP 2.3.0 yalnız WFI.PostgresTests bağımlılığıdır; WFI API/ajan üretim paketine eklenmez. Test proxy loopback üzerinde dinamik port kullanır. RoundRobin, /api/v1/ready aktif sağlık kontrolü, 1 saniye aralık/timeout ve 1 başarısızlık eşiği yalnız kısa test için seçilmiştir. [Microsoft sağlık kontrolü dokümanı](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/dests-health-checks?view=aspnetcore-10.0).

Sağlık kontrolü arızayı fark edene kadar 502/503 oluşabilir; test bu geçiş yanıtlarını kabul edip sonrasında kalan backend'den 10 ardışık başarılı yanıt arar. Sıfır kesinti/SLA iddiası yoktur. Login/logout dahil kullanıcı HTTP akışı tek proxy adresinden geçer; mutasyonlar otomatik tekrar gönderilmez. Test backend işaretçi header'ı yalnız test proxy'sine aittir. Production TLS, kurum proxy ürünü, proxy'nin kendisinin yedekliliği, ayrı fiziksel sunucular ve DB failover bu kapsamda değildir.

Manuel teşhis API'sine eşzamanlı iki istek testi eklendi: tek 201/tek 409, tek aktif iş ve tek oluşturma audit'i doğrulandı. İptalden sonra yeni iş kabul edildi. Bu senaryo gerçek PostgreSQL ile iki kurulum yolunda da geçti.

Domain kabul testi: iki ayrı API host'u ortak PostgreSQL üzerinde farklı cihazlara eşzamanlı istek gönderir; yalnız bir boş slot varken bir 201 ve bir 429 beklenir. Her iki şema kurulum yolunda çalışır. Bu düşük hacimli doğruluk testi throughput ölçümü değildir.

Yeniden analiz yarışı: farklı API host'larında aynı cihaz için eski tamamlanmış işi yeniden analiz etme ve yeni teşhis oluşturma eşzamanlı denenir. Bir başarılı yanıt, bir 409 ve veritabanında tek aktif iş beklenir.

Düzeltme kuyruğu filtresi: 12 kapalı otomatik işin arkasındaki geçerli manuel işin seçilmesi her iki şema yolunda sınanır. Test betiğini doğrudan çalıştırın; tüm betiğe PowerShell *> çıktı yönlendirmesi pg_ctl arka plan sürecinin akışını açık tutarak başlangıçta beklemeye yol açabilir. PostgreSQL kendi postgres.log dosyasını zaten oluşturur.

### PostgreSQL büyük envanter doğrulaması — 2026-10-25
Kabul paketine 40.000 sentetik cihaz içeren PostgreSQL envanter senaryosu eklendi. Aynı heartbeat zamanında kimlikle kararlı sıralama, ilk/orta/son sayfalar, 50 kayıt sınırı, domain ayrımı, arama normalizasyonu ve listeden büyük/özel alanların çıkarılması sınanır. EF migration ve schema.sql yollarının ikisinde de çalışır. Tek kullanıcı ve TestServer API çağrıları kullanır; 40.000 aktif ajan, eşzamanlı trafik, ağ/TLS/LDAP veya üretim kapasitesi ölçümü değildir. Ürün davranışı, şema, kurulum ve firewall gereksinimleri değişmedi.

İptal/dispatch yarışı: aynı bekleyen düzeltmeye API iptali ve ayrı context ile ajan claim eşzamanlı gönderilir. Ya Cancelled + boş claim + tek audit, ya Executing + iptal 409 + iptal auditi yok beklenir. Her iki şema yolu geçti.

Manuel düzeltme tekrar isteği testi iki API host'undan aynı cihaz/katalog için eşzamanlı POST gönderir: bir 201, bir 409, tek iş ve tek istek audit'i; iptal sonrası yeni POST 201. Her iki şema yolunda geçti.

Otomatik dışlama: sentetik başarılı model/kural ile gerçek DiagnosticJobProcessor çalıştırılır. İki gün önce istenmiş Approved ve Unknown işler varken yeni otomatik düzeltme oluşmadığı, teşhisin yine completed olduğu her iki şema yolunda doğrulandı. Model sentetiktir; OS onarımı yapılmaz.

25 Ekim 2026: ProblemDiagnosisAndBigFix migration ve güncel idempotent schema.sql ile izole PostgreSQL 17 testinde iki kurulum yolu da geçti (2/2). Bu sonuç fonksiyonel doğrulamadır, 40.000 cihaz eşzamanlı yük kapasite ölçümü değildir.
