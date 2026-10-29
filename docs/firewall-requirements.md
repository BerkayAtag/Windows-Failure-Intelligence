# Firewall gereksinimleri

## Otomatik tek sunucu kurulumu — 0.6.0-preview.4

- İstemci/tarayıcı → WFI: HTTPS TCP/443. Kurucu yalnız Domain profiline `WFI-HTTPS` kuralını ekler.
- WFI → kendi PostgreSQL'i: **127.0.0.1 TCP/55432**. Ağdan inbound açılmaz.
- WFI → kendi Ollama'sı: **127.0.0.1 TCP/11435**. Ağdan inbound açılmaz. Manuel kurulumdaki 5432/11434 değerleriyle karıştırmayın.
- Kurulum sırasında sunucu → `download.visualstudio.microsoft.com`, `builds.dotnet.microsoft.com`, `get.enterprisedb.com`, `github.com` ve GitHub release CDN yönlendirmeleri: HTTPS TCP/443. Model için Ollama registry/blob uç noktaları gerekir; kurum proxy ekibi güncel resmi Ollama dağıtım uç noktalarını izinli yapmalıdır. Windows özellik kaynağı yerel değilse Windows Update/kurum WSUS erişimi gerekir.
- DNS kurum DNS sunucularına; LDAP etkinleştirilirse WFI → ilgili DC TCP/636. LDAP ilk yerel giriş için gerekmez.

Önbelleğe alınmış dört doğrulanmış paket ikili indirmelerini azaltır; model için ayrıca erişim gerekir. TLS doğrulaması kapatılmaz. Yeni ajan inbound portu yoktur; otomatik DB/AI portları yalnız loopback'tir.


## 0.5.0 konsoldaki Ağ Erişimleri

Aynı kaynak/hedef/port kuralları **Ajan ve Ağ Erişimleri → Ağ Erişimleri** sekmesinde okunabilir. Bu statik gereksinim görünümüdür; canlı erişim veya güvenlik duvarı değişikliği yapmaz. Tek EXE indirmesi yine istemci/tarayıcı → WFI TCP 443 kullanır. Sessiz kurulum yeni port açmaz. AI deneyim hafızası mevcut PostgreSQL ve kurum içi model bağlantısını kullanır; dış eğitim servisine veri gönderilmez.


## 0.4.1 setup kayıt trafiği

Setup ve ajan istemciden WFI HTTPS 443'e çıkar. Setup giriş/CSRF/kayıt/çıkış işlemleri için aynı HTTPS adresini kullanır; araya ayrı kayıt sunucusu veya inbound istemci portu eklenmedi. LDAPS işlemini WFI sunucusu yapar, setup doğrudan DC'ye LDAP bağlantısı açmaz. Sertifika doğrulamasını veya güvenlik duvarını kapatmayın.


## 0.4.0 ajan teşhisi ve kurulum etkisi

Bu sürümde sunucudan istemciye WinRM/SMB/RPC bağlantısı kurulmaz. Ajan istemciden WFI HTTPS 443'e çıkar; teşhis/onarım işlerini aynı kanal üzerinden alır, kanıt ve sonucu gönderir. Ajan indirme de konsolun HTTPS 443 bağlantısını kullanır. İstemcide WFI için inbound port açılmaz. AI erişimi yalnız sunucu/worker → Ollama iç ağıdır; ajan Ollama'ya bağlanmaz.

BESClient servis kontrolü ve sabit reinstall.cmd çalışması yereldir. Kurum reinstall.cmd'sinin kendi BigFix/dağıtım hedeflerine ihtiyaç duyduğu portlar script sahibi tarafından ayrıca belgelenmelidir; WFI bunları tahmin etmez veya açmaz. Setup-Server.cmd firewall kuralı oluşturmaz. LDAPS 636, PostgreSQL 5432 ve HTTPS/HA gereksinimleri aşağıdaki matriste devam eder.


Kaynak ve hedefleri sunucu IP/gruplarıyla sınırlandırın. Aşağıdaki portlar tüm ağa açılacak bir liste değildir. İzin stateful dönüş trafiğini kapsar; istemcilere WFI adına inbound kural gerekmez.

| Kaynak | Hedef | Port/protokol | Amaç ve koşul |
| --- | --- | --- | --- |
| Yetkili operatör bilgisayarları | WFI VIP/FQDN | TCP 443 | Web arayüzü ve log yükleme |
| Load balancer | WFI API düğümleri | TCP 443 veya seçilen backend HTTPS portu | Trafik ve /api/v1/ready sağlık kontrolü; gerçek backend portunu kullanın |
| WFI API | Seçilmiş DC'ler | TCP 636 | LDAPS giriş ve grup sorguları; düz LDAP 389 gerekmez |
| API ve worker | PostgreSQL VIP/sunucusu | TCP 5432 veya kurumun DB portu | TLS doğrulamalı DB erişimi |
| API ve worker | Dosya sunucusu | TCP 445 | Yalnızca ortak UNC log/key storage kullanılıyorsa; SMB şifreleme ve ACL uygulayın |
| WFI sunucuları | Kurumsal DNS | UDP/TCP 53 | DC/DB/VIP/CA ad çözümleme |
| WFI sunucuları | Kurum zaman kaynağı | UDP 123 | Sertifika ve oturum süreleri için saat eşitleme; işletim sistemi politikasıyla |
| WFI sunucuları | PKI CRL/OCSP adresleri | PKI'nın kullandığı TCP 80/443 | Sertifika zinciri/iptal kontrolleri için; yalnızca kurum PKI hedefleri |
| Yönetim ağı | Sunucular | Kurumun yönetim portları | RDP/WinRM uygulama gereği değildir; ayrı yönetim politikasıyla |

API/worker arasında özel WFI portu yoktur; işler DB üzerinden paylaşılır. API'nin istemcilere SMB, RPC, WinRM veya WMI bağlantısı kurması gerekmez. SCCM/BigFix connector uygulanmadığı için bunlara port açmayın. Domain'e katılım, GPO ve mevcut SCCM altyapısının kendi firewall kuralları bu tabloya ek WFI gereksinimi sayılmaz.

Üretimde internet erişimi uygulamanın çalışması için zorunlu değildir. Paket indirme/bağımlılık denetimi derleme makinesinde yapılır. OpenAI API'ye log gönderilmez.

Doğrulama (API sunucusunda gerçek adları kullanın):

```powershell
Resolve-DnsName DC-FQDN
Test-NetConnection DC-FQDN -Port 636
Test-NetConnection POSTGRES-FQDN -Port 5432
Test-NetConnection FILESERVER-FQDN -Port 445 # yalnızca UNC kullanılıyorsa
```

TCP testinin başarılı olması LDAPS sertifikasını, kullanıcı yetkisini veya DB bağlantısını doğrulamaz. Bunları gerçek pilot oturumu ve readiness ile ayrıca kontrol edin. LB, istemciden gelen forwarded başlıklarını silip kendisi yazmalıdır; backend'e kullanıcıların doğrudan erişimini engelleyin.


## Ajan ve Ollama — 0.3.0

| Kaynak | Hedef | Port | Gerekçe |
| --- | --- | --- | --- |
| WFI Windows ajanları | WFI VIP/FQDN | TCP 443 outbound | Enrollment, heartbeat, iş claim, kanıt ve sonuç |
| API ve AI worker | Aynı makinedeki Ollama | Loopback TCP 11434 | Ağa açılmamalı; uzak inbound kural yok |
| API ve AI worker | Ayrı model sunucusunun TLS proxy'si | TCP 443 veya seçilmiş HTTPS portu | Yalnız bu kaynaklardan izin; sertifika doğrulanır |
| Model sunucusu (kurulum zamanı) | Onaylı model deposu/proxy | TCP 443 | Model indirme; çalışma zamanında zorunlu değil |

Ajanın inbound dinleme portu yoktur. WFI için RPC, WMI, WinRM, SMB veya RDP'yi 40.000 istemciye açmayın. Ajan kurumsal DNS/zaman/PKI hizmetlerine mevcut OS politikasıyla erişir. Web kullanıcısı veya ajan doğrudan Ollama'ya bağlanmaz.

DNS cache temizleme ek bağlantı gerektirmez. Update servisleri ve DISM RestoreHealth kurum WSUS/Update/onarım kaynağına erişim isteyebilir; mevcut onaylı istemci kuralları geçerlidir. WFI API adına genel internet erişimi açmak bunu çözmez.

İstemciden `Test-NetConnection wfi.example.invalid -Port 443`, API'den DC 636/DB portu ve worker'dan model TLS portunu test edin. Açık TCP portu uygulama kimliği/sertifika/rol kabulünün yerine geçmez. LB'de büyük NAT gruplarını tek küçük cihaz kotasına sıkıştırmayın: uygulama ajan limitini doğrulanmış cihaz ID'sine uygular; enrollment IP limitlidir. Gateway'de anonim saldırı ve toplam kaynak sınırları ayrıca gerekir.


Ajan PollSeconds seçimi bağlantı sıklığını değiştirir, port gereksinimini değiştirmez. Kurulum/yükseltme önkontrolleri için ek ağ portu eklenmedi.


SDK seçimi ve PowerShell iptal düzeltmeleri yeni çalışma zamanı bağlantısı veya firewall portu gerektirmez. Sentetik shell testleri ağ kullanmaz.


Model keşfi timeout/hataları için ek port açılmaz. API/worker → onaylı model hedefi bağlantısını ve TLS'i kontrol edin; istemci → Ollama erişimi gerekli değildir.


İptal/onay yarış düzeltmesi yeni bağlantı veya port gerektirmez; mevcut HTTPS ve DB kuralları değişmedi.


### Envanter indeksi güncellemesi

Cihaz sayfalama ve veritabanı indeksi değişikliği yeni ağ bağlantısı/port gerektirmez. Ajan HTTPS, sunucu LDAPS ve PostgreSQL bağlantıları için bu belgedeki mevcut kurallar geçerlidir.

Model/politika kayıt transaction düzeltmesi yeni port gerektirmez; mevcut HTTPS, LDAPS ve PostgreSQL kuralları değişmez.

Portal teşhis iptali mevcut HTTPS API üzerinden çalışır; istemciye gelen bağlantı veya ek port açılmaz.

Ajan atama süresi koruması yeni WFI portu gerektirmez. Sunucu/istemci saatleri mevcut kurum zaman altyapısıyla eşitlenmelidir; bu değişiklik internet NTP erişimi gerektirmez.

Yerel Test-Postgres.ps1 yalnız 127.0.0.1:55439 (veya seçilen port) kullanır; firewall kuralı açmaz. Bu geliştirme testi için üretim PostgreSQL port kurallarını değiştirmeyin.

Yerel PostgreSQL oturum kabul testi iki Kestrel host'unu işletim sisteminin seçtiği dinamik 127.0.0.1 TCP portlarında açar. Dinleyiciler test sonunda kapanır; LAN erişimi veya firewall kuralı gerekmez. Üretim port matrisi değişmez.

İki ayrı API süreci kabul testi yalnız dinamik 127.0.0.1 TCP portları ve yerel PostgreSQL kullanır. Test süreçleri sonunda kapatılır; firewall kuralı veya LAN dinleyicisi açılmaz.

Test reverse proxy'si de dinamik 127.0.0.1 TCP portu kullanır; test dışında dağıtılmaz. Kurum firewall'ında ek port açılması gerekmez.

Yinelenen manuel teşhis kontrolü API/veritabanı transaction'ıdır; mevcut HTTPS/PostgreSQL bağlantıları kullanılır, ek port açılmaz.

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

Yerel deneme 127.0.0.1:5080 loopback kullanır; inbound firewall kuralı açılmaz ve başka bilgisayarlara erişim sağlamaz. Kurum yayını için yukarıdaki HTTPS/VIP izinlerini kullanın. [Yerel test rehberi](local-testing.md).

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

