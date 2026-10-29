# 0.6.0-preview.5 — 29 Ekim 2026

Otomatik kurulum ve yerel yönetici özelliklerini, UTF-8/DNS düzeltmelerini içerir. Yayın kontrolü artık sürüm etiketi ile derlenen kaynak commit'inin aynı olmasını zorunlu tutar; başka kaynak sürümünün dosyaları mevcut sürüm üzerine yüklenmez. Güncel indirme preview.5'tir. Windows VM kabulü hâlâ açıktır.

# 0.6.0-preview.4 — 29 Ekim 2026

Kurulum düzeltmesi: Windows PowerShell 5.1 altında SQL UTF-8 olarak okunur ve PostgreSQL istemci kodlaması UTF8 seçilir; Türkçe katalog metinleri korunur. DNS sorgusu başarısızsa sihirbaz makine adıyla açılır. Sunucunun DNS domain bilgisi varsa domain alanına otomatik alınır ve değiştirilebilir.

- .NET: **220/220** geçti. Yerel yönetici doğru/yanlış parola, açık hesap adı, LDAP kapalı davranışı, hatalı/aşırı iterasyonlu hash, CSRF zorunluluğu ve parola değişimi/devre dışı hesapta eski cookie reddi eklendi.
- Playwright Chromium: **14/14** geçti. Yerel yönetici açıklaması, demo düğmesinin gizlenmesi, karanlık/mobil giriş ve önceki ajan/teşhis/onarım/tema akışları.
- Gerçek PostgreSQL 17, ayrılmış geçici loopback cluster: **3/3** geçti. Önceki concurrency/migration/SQL kabullerine ek olarak kurucunun gerçek GRANT ifadeleriyle uygulama hesabının SELECT/DML yapabilmesi, CREATE TABLE ve CREATE DATABASE yapamaması doğrulandı. Cluster test sonrası durduruldu; Windows servisi kurulmadı.
- PowerShell 5.1 otomatik kurulum yardımcı testleri geçti: kısa parola, geçersiz domain/hostname/sertifika, göreli/örtüşen/wildcard dizin, değiştirilmiş paket ve ZIP kök dışı giriş reddi; script parse kontrolü. Mevcut ajan kurucu 6 ve connectivity 12 sentetik senaryosu geçti.
- pnpm audit ve 8 .NET projesinin transitif NuGet taraması bilinen zafiyet bildirmedi. Bu, tam güvenlik garantisi değildir. Önceki Codex Security başlatması uzun yerel PostgreSQL yolu nedeniyle rapor üretmemişti; bu sürüm için başarılı bulut taraması iddia edilmez.
- Windows makinesine gerçek IIS/DB/Ollama kurulmadı. Sihirbazın temiz VM kurulumu, yeniden başlatma/yarım kurulum, servis kimlikleri, appwiz kaldırma ve gerçek AD/HA kabulü açık. 40 bin aktif cihaz kapasitesi ölçülmedi.
- Sunucu kurucu çevrimiçi bootstrap'tir: bağımlılık/model indirir. Mevcut kurulum üstüne otomatik yükseltme veya tam rollback sağlamaz. Yerel HTTPS sertifikasının istemci güveni ayrıca dağıtılmalıdır.

GitHub paketi/yayın doğrulaması yayın tamamlandıktan sonra kaydedilir.

---
# 0.6.0-preview.3 — 29 Ekim 2026

- scripts/Build.ps1: 213/213 .NET testi; frontend, API, worker, istemci EXE ve sunucu EXE başarılı.
- Playwright Chromium: 13/13. Yeni oturumun aydınlık varsayılanı, beyaz sidebar/kırmızı ana işlem, karanlık tercih kalıcılığı, seçeneklerin en az 4.5:1 kontrastı ve Onarım Otomasyonu gezinmesi doğrulandı. Önceki eski RGB beklentisi işlevsel kontrast kontrolüne dönüştürüldü.
- Kurulum regresyonları: ürün/yayıncı kayıtları ve kayıt türleri, tırnaklı simge/kaldırıcı yolu, göreli/UNC/kök yol reddi, junction üst klasör reddi, iç içe veya aynı kanıt/anahtar dizini reddi; korunan ajan klasörünün geri yüklenmesi ve ilgisiz dosya reddi. Makine servisi ve HKLM değiştirilmedi.
- İki EXE dosyasının CompanyName alanı Berkay Atağ, FileVersion 0.6.0.3 olarak okundu. Çok boyutlu kırmızı WFI ICO ve yönetici yükseltmesi isteyen kaldırıcı üretildi. Gerçek appwiz/kurulum/kaldırma Windows VM kabulü yapılmadı.
- pnpm audit bilinen zafiyet bildirmedi. Tam Codex Security başlatma, .data PostgreSQL aracı içindeki uzun yolu okuyamadı; scanId/rapor üretmedi.
- Tanıtım sitesi: 1440px masaüstü, 390/768px mobil; görüntüler, sayfa kimliği, anlamlı içerik, anchor gezinme, taşma ve sayfa hataları kontrol edildi. Aydınlık ve karanlık uygulama ekranları sentetik example.invalid oturumuyla yenilendi. Browser plugin not available; mevcut Playwright altyapısı kullanıldı.
- UI akışı: giriş → varsayılan açık tema → demo → Onarım Otomasyonu → karanlık seçimi → yenileme → tercih korunur. Kullanıcı oturumu/onarım/ajan dağıtım ve diğer mevcut e2e akışları da geçti.
- Tasarım kontrolü: mevcut bilgi mimarisi ve metin hiyerarşisi korundu; palet kırmızı-beyaz/koyu nötr yapıldı; simgeler aynı pulse ailesinde; mobil taşma yok; tanıtım gerçek sentetik ekranları kullanır. Bu bir marka resmiyet/onayı iddiası değildir.

GitHub yayını ve indirilen paket bütünlüğü yayın sonrasında ayrıca kaydedilir. Gerçek AD/BigFix/HA/40k kapasitesi ve tüm olası hataların yokluğu iddia edilmez. Önceki açık yol haritası korunur.

---
# 0.6.0-preview.2 — 27 Ekim 2026

- Build.ps1 geçti: 213/213 .NET testi, API/worker/ajan ve iki setup EXE üretildi.
- Playwright: 12/12, proje dizininden çalıştırıldı. Kök dizinden ilk çalıştırma fixture yolunda başarısız oldu; doğru çalışma dizininde tam tekrar geçti.
- PostgreSQL 17: 2/2, EF ve deploy/schema.sql yollarında domain audit migration ve mevcut concurrency kabulleri geçti.
- pnpm audit ve tüm 7 .NET projesinin transitif NuGet taraması: bilinen zafiyet bildirmedi (bu tarihteki registry verisi).
- Ajan regresyonları: HTTPS origin sınırı, 404 fallback, 401/503 fail-closed, 429 Retry-After, kalıcı DPAPI journal tutarlılığı; önceki kayıp cevap/yeniden başlatma/tekrar yürütmeme ve katalog sınırı testleri dahil.
- Audit güvenlik regresyonu: Admin farklı domain veya eski scope'suz kayıtları okuyamaz; yeni login audit'inin domain'i kaydedilir.
- Statik tanıtım: 1440px masaüstü, 390/768px mobil, görseller, anchor gezinme, taşma ve JS hata kontrolleri geçti. Görseller sentetik example.invalid oturumudur. GitHub Pages yayını tamamlandı ve canlı görünüm tarayıcıda doğrulandı; kurulum linkleri HTTP 200 ve manifest/digest eşleşmesiyle doğrulandı.
- Sunucu EXE gömülü yükü: 197 kayıt, gerekli API/kurulum/şema dosyaları mevcut; runtime sır dosyası veya kök dışı yol bulunmadı. EXE çalıştırılıp işletim sistemine kurulum yapılmadı.
- Codex Security yeni başlatma denemesi PostgreSQL test aracı içindeki uzun dosya yolunu inceleyemedi; scanId veya tamamlanmış rapor oluşmadı. Bu çalışma tam tarama tamamlandı iddiası içermez.

Windows servis kurulumu bu geliştirme bilgisayarında gerçekleştirilmedi. Gerçek hedef Windows VM/AD/BigFix kabulü açık; başarılı yerel regresyon bunların yerine geçmez. Önceki on maddelik yol haritasındaki açık işler implementation-roadmap.md içinde korunur.

---
# 0.6.0-preview.1 doğrulama — 26 Ekim 2026

| Kontrol | Sonuç |
| --- | --- |
| scripts/Build.ps1 | 200/200 .NET testi; frontend/API/worker/ajan/setup derlemesi başarılı |
| Playwright | 12/12; giriş/çıkış, analiz, kampanya pilot onayı/iptal, kurulum/ağ, deneyim, sağlık, kalite hata görünümü, tema/mobil |
| PostgreSQL 17 | 2/2; EF migration ve idempotent SQL yolları, ayrı bağlantılarla ortak model slotu/fencing ve kampanya paralellik sınırı |
| Eski SQLite demo | JSON sürüm kolonu eklenmesi, veri korunması ve tekrarlı yükseltme regresyonu geçti |
| BigFix | Sentetik XML/kimlik, eski/yeni rapor, API kesintisi ve onarım sonrası baseline reddi geçti |
| AI | Katalog dışı öneri filtresi, sentetik değerlendirme, yerel model sınırı, kesintiden dönen iş ve negatif geri bildirim regresyonları geçti |
| Yerel erişim | http://127.0.0.1:5080 ve /api/v1/ready 200; demo yeniden başlatılarak doğrulandı |

UI ortamı: Chromium, 1505×1045 masaüstü ve 390×844 mobil. Browser plugin not available; projenin Playwright akışı kullanıldı. WFI sayfa kimliği, anlamlı içerik ve etkileşimler doğrulandı; pageerror yok. Sistem Sağlığı karanlık ekranı ve kalite mobil hata ekranı gözle incelendi; okunabilir, taşma/overlay yok. Kalite testindeki 503 yanıtı kasıtlı sentetik model kesintisidir. İlk tur testte aynı adlı h1/h2 selector belirsizliği düzeltildi; tam tekrar 12/12 geçti.

Komutlar: scripts/Build.ps1; scripts/Test-Postgres.ps1 -PostgresBin .data/postgresql-test-17/runtime/pgsql/bin; src/wfi-web içinde node node_modules/@playwright/test/cli.js test.

Codex Security standard scan başlatma denemesi `Scan scope must reference an existing directory inside the target` hatası verdi; scanId/rapor oluşmadı. Bu sonuç tam güvenlik taraması başarısı değildir. Elle inceleme ve yukarıdaki davranış regresyonları yapıldı; sıfır açık/hata garantisi yoktur.

Açık kabul: on maddelik planda 5–10, madde 1'in reconnect/model kesintisi yük profilleri ve 40k ölçümü, kampanya grup seçimi, immutable model digest, gerçek model kalite karşılaştırması, Windows VM kurulum/yükseltme, gerçek AD/BigFix/HA. Yapılandırılmış kapasite değeri ölçülmüş kapasite değildir. Güncel ayrıntı implementation-roadmap.md içindedir.

---
# 0.5.0 doğrulama — 26 Ekim 2026

| Kontrol | Sonuç |
| --- | --- |
| scripts/Build.ps1 | Başarılı; 177/177 .NET, frontend/API/worker/ajan ve tek EXE bootstrap yayımlandı |
| PostgreSQL | İzole PostgreSQL 17 cluster: EF migration ve schema.sql yolları 2/2 geçti |
| Playwright | 9/9 Chromium; 1505×1045 masaüstü ve 390×844 mobil; üç tema |
| Yeni UI | Giriş → Ajan ve Ağ Erişimleri → EXE bağlantısı/sessiz parametreler → Ağ Erişimleri; deneyim kaynak kimliği/dışlama akışı geçti |
| Render | Doğru WFI başlığı, anlamlı içerik, hata overlay'i/pageerror yok; masaüstü ve mobil görüntüler gözle incelendi |
| Mobil düzeltme | İlk turda ağ tablosu sayfayı taşırdı; grid min-width düzeltmesinden sonra tüm 9 test geçti |
| Deneyim güvenliği | Domain/kapsam/güncel kanıt/yaş/onay/başarı/dışlama filtreleri; yetki ve audit; modele gönderilen bağlam regresyonları geçti |
| Readiness | Deneyim kolonu eksik veritabanında 503 regresyonu geçti |
| Sessiz seçenekler | HTTPS, bilinmeyen/yinelenen parametre, sır argümanı reddi ve yükseltme parse regresyonları geçti |

Browser plugin not available: mevcut Playwright kullanıldı. Komutlar: scripts/Build.ps1; scripts/Test-Postgres.ps1 -PostgresBin .data/postgresql-test-17/runtime/pgsql/bin; src/wfi-web içinde node node_modules/@playwright/test/cli.js test. Tema geçişinin ortasını yakalamamak için son karanlık ekran görüntüsü animations:disabled ile alındı; etkin olmayan sekme zemin rgb(27,42,64), yazı rgb(227,236,248).

Deneyim UI testi sentetik yanıtla çalışır; yetki/domain/veri filtresi ve model isteği .NET entegrasyon testlerinde doğrulanır. Yeni gerçek model eğitim veya doğruluk artışı ölçümü yapılmadı. Eşzamanlı kayıt testi, SQLite login/audit yarışını kayıt yarışından ayırmak için iki oturumu önce açıp kayıt POST'larını eşzamanlı gönderir.

Kalan kabul: imzalı EXE ile ayrı Windows istemcisinde UAC, servis kurulumu/sessiz yükseltme, gerçek AD/LDAPS, BESClient/reinstall, IIS/HA ve 40.000 aktif istemci yük testi. Kurucu bu bilgisayarda servis kurmak için çalıştırılmadı. Yerel sentetik regresyonlar tam güvenlik taraması değildir; bu sürümde tamamlanmış yeni Codex Security raporu yoktur. Sıfır hata garantisi verilmez.

---
# 0.4.1 doğrulama — 26 Ekim 2026

| Kontrol | Sonuç |
| --- | --- |
| scripts/Build.ps1 | Başarılı; 160/160 .NET testi; frontend, API, worker, self-contained ajan ve Windows Forms setup yayınlandı |
| Kurulum güvenliği | HTTPS zorunluluğu, Admin yetkisi, hatalı parola/Viewer/Analyst reddi, oturum çıkışı, tek kullanımlık token, yinelenen/eşzamanlı hostname kaydı regresyonları geçti |
| PostgreSQL | İzole cluster üzerinde migration/schema.sql yolları 2/2 geçti; bu turda şema değişmedi |
| Playwright | 7/7 Chromium; masaüstü 1505×1045, mobil 390×844 |
| Yeni UI davranışı | Genel etiketi sade; BigFix seçeneği korunuyor; ajan kaydı/heartbeat sonrasında envanter elle yenilenmeden online satırı gösteriyor |
| Görsel kontrol | WFI başlığı/anlamlı içerik, üç tema, mobil taşma kontrolü, pageerror yok; setup penceresi Windows UI ağacı ve ekran görüntüsüyle gözlemlendi |
| NuGet | Core/API/Infrastructure/Agent/Agent.Setup/Worker/Tests projelerinde ayrı --no-restore sorguları bilinen açık bildirmedi. Solution toplu komutu null-reference hatası verdi; proje bazında sorgular tamamlandı |
| Frontend bağımlılıkları | pnpm audit --prod: bilinen açık bulunmadı |
| Ajan ZIP | Setup EXE/DLL/runtimeconfig, ajan EXE, PowerShell kurucu ve runtime dosyaları doğrulandı; *.local.json yok |
| PowerShell | Derlemede 6/6 installer guard ve 12/12 bağlantı kontrolü geçti |

Browser plugin not available: mevcut Playwright kullanıldı. Akış: ana sayfa → Genel/BigFix kapsamı → ajan setup indirme; ayrı sentetik kayıt/enrollment/heartbeat → açık envanterin otomatik yenilenmesi. Setup formu yalnız görüntülendi; gerçek Admin bilgisi girilmedi, bu bilgisayara servis kurulmadı.

Codex Security bu turda src kapsamıyla başlatılmak istendi; `workbench_db.py start-prompt-only-scan` komutu başarısız oldu ve scanId döndürmedi. Başka tarama başlatılmadı, tam rapor tamamlandı denmiyor. Yerel güvenlik regresyonları ve bağımlılık sorguları tam kaynak güvenlik taramasının yerine geçmez.

Kalan kabul: gerçek Windows istemcide UAC/servis kurma, AD/LDAPS girişi, kuruma ait BESClient/reinstall, IIS/HA ve 40.000 istemci yük testi. Setup Authenticode imzasızdır. Test sonuçları sıfır hata veya tüm açıkların kapalı olduğu garantisi değildir.

---

# 0.4.0 doğrulama — 25 Ekim 2026

Bu turda sentetik veri kullanıldı. Gerçek şirket ağına bağlanılmadı; BESClient başlatılmadı ve reinstall.cmd çalıştırılmadı.

| Kontrol | Sonuç |
| --- | --- |
| scripts/Build.ps1 | Başarılı; 153/153 .NET testi, frontend, API/worker/ajan yayın paketleri |
| PowerShell | 6/6 ajan kurulum koruması, 12/12 bağlantı doğrulama regresyonu; tüm script AST kontrolleri geçti |
| PostgreSQL | 2/2: migration ve deploy/schema.sql yolları; yeni ProblemDescription sütunu ve katalog 4.0.0 dahil |
| EF model | has-pending-model-changes: değişiklik yok |
| Playwright | 6/6 Chromium; 1505×1045 masaüstü ve 390×844 mobil |
| Birincil akış | Makine + açıklama → BigFix teşhis kuyruğu → durum gösterimi → iptal |
| Ajan paketi | Admin indirme 200/application/zip; anonim 401 ve Analyst 403 regresyonu |
| Tema | Standart/açık/karanlık; dark option yazı rgb(241,245,249), zemin rgb(24,35,52) |
| Render | WFI başlığı, anlamlı içerik, etkileşimler ve ekran görüntüleri doğrulandı; framework overlay/pageerror yok; yatay sayfa taşması yok |
| Gerçek yerel AI | phi4-mini:latest, sentetik BigFix servis kanıtı + sorun açıklaması → completed; PB-BIGFIX-REPAIR önerisi; onarım yürütülmedi |
| BigFix negatif yollar | Çalışan/başlatılabilen servis reinstall'a gitmez; hash yokluğu, reparse, güvenilmeyen sahip/yazma izni engelleri geçti |
| Erişim | http://127.0.0.1:5080/api/v1/ready HTTP 200 |

Browser plugin not available: mevcut Playwright kullanıldı. Yeni ekran kanıtları sistem TEMP dizininde wfi-problem-dark.png ve wfi-problem-mobile.png. Başlıca komutlar: scripts/Build.ps1; scripts/Test-Postgres.ps1; scripts/Test-OllamaPipeline.ps1 -BigFix; src/wfi-web içinde node node_modules/@playwright/test/cli.js test.

Sınırlar: Setup-Server.cmd/Install-Server.ps1 sözdizimi ve kaynak incelemesi yapıldı; gerçek Windows Server IIS kurulumu, sertifika bağlama, gerçek LDAPS/HA, gerçek BigFix yeniden kurulumunun başarılı yolu ve 40.000 cihaz yük kapasitesi doğrulanmadı. Codex Security tam taraması önceki başlangıç hatası nedeniyle bu sürüm için tamamlanmış değildir. Bağımlılık taraması sonuçları aşağıdaki önceki tur kaydına aittir.

---

# 0.3.0 doğrulama — 23 Ekim 2026

Bu belge eski v0.2 sonuçlarından ayrıdır. Sentetik veriler kullanıldı; gerçek şirket ağına bağlanılmadı.

| Kontrol | Sonuç / kapsam |
| --- | --- |
| scripts/Build.ps1 | Başarılı: frontend build, 141 .NET test, API/worker/win-x64 agent publish |
| Playwright | 5/5 geçti; Chromium, http://127.0.0.1:5080, 1505×1045 ve 390×844 |
| Arayüz | Giriş/çıkış, upload/dedup/kanıt/arama, üç tema, cihaz kayıt/token yenileme, genel ve cihaz politika kaydı, teşhis kuyruğu, manuel düzeltme isteği |
| Render | Sayfa kimliği ve anlamlı içerik; framework hata ekranı yok; pageerror yok; masaüstü/mobil ekran görüntüleri incelendi |
| Model | Gerçek phi4-mini:latest, sentetik API → worker → Ollama → DB sonucu completed; yaklaşık 23,1 saniye model HTTP süresi (tek örnek, kapasite ölçümü değil) |
| Güvenlik regresyonları | CSRF/RBAC/domain, LDAP escape, XML/ZIP sınırları, redaction, cookie logout/iki düğüm, enrollment/revoke/IDOR, lease/replay, politika ve katalog, shell exit kodları ve önkontrol/işlem/doğrulama hataları |
| Yeni düzeltme testleri | Aynı NAT'ta iki cihaz bağımsız limit; eksik model confidence reddi; cihaz başına sırayla dispatch, expired Unknown tekrar verilmemesi, policy off ve receipt sahipliği/idempotency |
| Bağımlılıklar | pnpm audit --prod: bilinen açık yok; dotnet list package --vulnerable --include-transitive: bildirilen açık yok |
| EF | has-pending-model-changes: değişiklik yok; DeviceInventoryPaging dahil migration zinciri ve deploy/schema.sql mevcut |
| PowerShell | scripts/*.ps1 AST syntax kontrolü geçti; servis kurulum/gerçek onarım çalıştırılmadı |
| Yerel eski SQLite | Çalıştırmadan önce DB/WAL yedeği; mevcut demo DB yeni tablolarla açıldı |

Browser plugin/browser skill bu oturumda listelenmedi (Browser plugin not available); Build Web Apps skill gereği mevcut Playwright kullanıldı. Mevcut testlerde eski docs/design görüntü yolları korundu; yeni cihaz ekranı görüntüleri %TEMP%/wfi-remote-{standard,light,dark,mobile}.png konumunda. React performans kontrolünde sayfalama, primitive effect bağımlılıkları, gizli sekmede polling durdurma ve aynı döngüde çakışan istekleri engelleme korundu.

## Bulunan ve giderilen görsel hata

Yeni grid mobilde minimum içerik genişliğine taşarak sayfayı yatay genişletiyordu. minmax(0,1fr), min-width:0 ve sınırlı tablo scroll düzeltildi. Tekrar testte sayfa genişliği viewport'u aşmadı; uzun tablo kendi alanında kaydırılır.

## Codex Security sınırı

Yeni Standard scan başlatma çağrısı şu hata ile döndü:
Could not read local file: src\WFI.Api\obj\Release
et10.0\compressed\gq2uzga5lf-{0}-4nhv88d8nh-4nhv88d8nh.gz

Geçerli yeni scanId/scanDir dönmedi. Eklentinin desktop-scan talimatı gereği uydurma tarama veya terminalden ikame rapor üretilmedi. Yeni tam tarama tamamlanmadı. Yerel testler yeni Codex Security raporu olarak sunulmaz. v0.2 taraması 0.3.0 kaynağını kapsamıyor.

## Kurum kabulünde kalanlar

Gerçek LDAPS/CA/grup/hesap kilitleme, yönetici haklı servis kurulumu/güncelleme/kaldırma, gerçek onarım, PostgreSQL çok düğüm yarışları, TLS/LB/SMB failover, 40.000 aktif ajan ve GPU kuyruk/soak ölçümü. Nginx bu ortamda doğrulanmadı. Sıfır hata/güvenlik açığı veya 40.000 cihaz üretim garantisi verilmez.



## Kesinti/yükseltme ek doğrulaması

Son scripts/Build.ps1 sonucu: **70/70 .NET testi geçti** (önceki 68 test + iki süreç yeniden başlatma/kayıp HTTP yanıtı senaryosu). API/worker/agent publish başarılı. Kurulum betiğinin yeni PollSeconds ve kimlik önkontrolleri için AST sözdizimi kontrolü geçti; SCM/ACL runtime kurulumu henüz yapılmadı. UI kaynakları değişmedi; önceki 4/4 Playwright sonucu geçerlidir.


## Güncel yürütücü ve SDK doğrulaması

Son Build.ps1: **75/75 test geçti**, API/worker/win-x64 ajan publish başarılı. RunnerLifecycleTests gerçek fakat zararsız Windows PowerShell süreci kullanarak iptal/çıkış ve 100.000 stdout + 100.000 stderr karakterinin sınırlı tutulmasını doğrular. Süresi geçmiş ve yanlış sürümlü katalog işleri shell'e ulaşmadı. Önceden iptal edilmiş istekte sentetik marker dosyası oluşmadı.

PATH'teki C:/Program Files/dotnet/dotnet.exe yalnız runtime içeriyordu; yeni Resolve-WfiDotnetSdk kullanıcı SDK'sını seçti. Start-Demo.ps1 ile demo başarıyla açıldı. Aynı kaynakta Playwright **4/4** yeniden geçti: üç tema, mobil taşma, kullanıcı akışı ve cihaz yönetimi. Bu doğrulama gerçek servis kurulumu/AD/PostgreSQL kapasite sonucu değildir.


## Ollama kesinti ek doğrulaması

Güncel Build.ps1 sonucu **80/80 test**. Beş yeni test bozuk JSON/yanlış models tipi, null/boş model adları ve keşif timeout'u ile kullanıcı iptalini ayırır. Ayrı yerel API http://127.0.0.1:5081, Ollama hedefi yalnız erişilemeyen loopback portu olacak şekilde çalıştırıldı. Chromium 1505×1045: WFI başlığı/cihaz envanteri görünür, bağlantı uyarısı var, Modeli kaydet kapalı, pageerror yok. Screenshot: %TEMP%/wfi-ollama-unavailable.png. Bu sentetik kesinti gerçek model servisini değiştirmedi; özel test API'si sonlandırıldı.


### İptal/onay durum geçişleri

- Teşhis iptali ve düzeltme onayı koşullu UPDATE + audit transaction kullanır. Terminal teşhis geç gelen iptalle değişmez; tekrar onay Executing işi Approved'a geri döndüremez. Durumu değişmiş iş için 409, domain kapsamı dışında/bulunamayan iş için 404 döner.
- AI yanıtı beklerken iptal testi: geç sonuç kaydedilmez, otomatik düzeltme oluşmaz, tek iptal audit'i vardır. Eşzamanlı iki onayda tek başarılı yanıt/tek audit doğrulandı. Ölçüm SQLite TestServer kapsamındadır; PostgreSQL yarış kabulü değildir.
- Build.ps1 başarılı, 82/82 .NET testi geçti. Playwright 4/4; yüksek risk manuel isteğin önce yönetici onayı beklemesi ve onaydan sonra yalnız ajan beklemesi de eklendi. Gerçek DISM yürütülmedi.


### Cihaz envanteri regresyonu

Build.ps1 başarılı: 83/83 .NET testi, frontend build ve API/worker/win-x64 ajan publish tamamlandı. FortyThousandDevicesHaveDeterministicBoundedDomainScopedPages testi 40.000 sentetik cihazda eşit heartbeat sıralamasını, 50 kayıt sınırını, domain ayrımını, arama normalleştirmesini ve envanter/kimlik alanlarının listeden çıkarılmasını doğrular. Bu bir SQLite fonksiyonel testidir; PostgreSQL/LB veya gerçek filo kapasitesi kabulü değildir.

EF has-pending-model-changes: bekleyen model değişikliği yok. DeviceInventoryPaging migration'ı ve deploy/schema.sql eşleşiyor; üretim veritabanına uygulanmadı.



Son envanter değişikliğinden sonra Playwright 4/4 yeniden geçti: giriş/çıkış, standart/açık/karanlık tema, mobil görünüm, cihaz/token/politika/teşhis ve manuel düzeltme onayı.


### Eşzamanlı yapılandırma ve audit rollback

Build.ps1: 87/87 .NET testi, frontend build ve API/worker/ajan publish başarılı. ConfigurationConcurrencyTests iki bağımsız OllamaService örneğiyle ilk kayıt/güncelleme, diğer AI ayarlarının korunması ve geçersiz model reddini doğrular. İki yönetici isteğiyle ilk domain politikası kaydında tek satır/iki audit doğrulandı. Sentetik SQLite trigger ile audit yazımı reddedildiğinde genel politika + tüm cihazlara uygulama ve cihaz bazlı ayar geri alındı; API mevcut hata sözleşmesiyle 503 döndü. PostgreSQL/LB eşzamanlılık kabulü değildir.

Bu değişikliklerden sonra Playwright 4/4 geçti: üç tema, mobil görünüm, giriş/çıkış, cihaz politikası ve manuel düzeltme/onay akışları.

### Portal teşhis iptali doğrulaması

Build.ps1 tekrar başarılı: 87/87 .NET, API/worker/ajan publish. Playwright 4/4: cihaz ekranından bekleyen teşhis iptal edildi, hata açıklaması göründü, iptal düğmesi kaldırıldı ve AI sonucu yok açıklaması doğrulandı. Üç tema ve mobil taşma testleri geçti; yeni mobil ekran görüntüsü incelendi. Gerçek ajan komutu veya OS onarımı çalıştırılmadı.


### Ajan atama süresi doğrulaması

Build.ps1: 90/90 .NET testi, frontend build ve üç publish başarılı. Bir saniyelik atama süresi verilen sentetik bekleyen ön kontrol, katalogdaki 60 saniye timeout'una veya 8 saniyelik test korumasına ulaşmadan kesildi; action çağrılmadı. Boş iş/lease kimliklerinde shell hiç çağrılmadı. Gerçek OS onarımı ve ayrı makine saat sapması testi yapılmadı.

Playwright 4/4 tekrar geçti; giriş/çıkış, üç tema, mobil ve uzaktan yönetim akışları korundu.

### Gerçek PostgreSQL ek kanıtı

WFI.PostgresTests 2/2 geçti: PostgreSQL 17.11, EF ve deploy SQL ilk/tekrar kurulum, bağımsız bağlantılarla tek cihaz dispatch ve idempotent audit. Test sonunda PostgreSQL durduruldu. Bu iki test normal 90 testlik SQLite/ajan paketinden ayrıdır; HA/AD/40 bin istemci kabulü hâlâ yapılmadı. Ayrıntı postgresql-validation.md.

Test cluster kapanışı ayrıca doğrulandı: 55439 üzerinde dinleyici yok; postmaster.pid ve geçici bootstrap parola dosyası bulunmuyor.

PostgreSQL 2/2 senaryo model tercihi kontrolleriyle genişletilerek tekrar geçti: bağımsız servis/DI örnekleri, eşzamanlı ilk kayıt, tek satır, değişikliğin diğer örnekten okunması, kurulu olmayan model reddi ve diğer AI alanlarının korunması. Model keşfi sentetik, DB gerçek PostgreSQL 17.11; HTTP/LB/inference testi değildir. Test cluster'ı kapatıldı.

### PostgreSQL API sınırları

EF/deploy SQL iki kurulum senaryosu, PostgreSQL destekli WebApplicationFactory API testleriyle genişletildi. Viewer 403, yabancı domain cihazı 404, domain kapsamlı toplu uygulama ve audit trigger hatasında genel/cihaz politikası rollback'i doğrulandı. Gerçek cookie/CSRF middleware ve sentetik LDAP kullanılır; gerçek ağ HTTP/AD testi değildir.

Genişletilmiş PostgreSQL API senaryoları 2/2; genel Build.ps1 90/90 test ve üç publish ile başarılı. PostgreSQL test dinleyicisi kapalı. Ürün arayüz kaynakları değişmedi.

### İki API host'unda ortak oturum

PostgreSQL 2/2 senaryo ortak key dizini kullanan iki bağımsız TestServer host'u ile tekrar geçti: cookie paylaşımı, logout sonrasında diğer host'un eski cookie'yi reddetmesi ve ilk host kapandıktan sonra ikinci host okumaları. Gerçek TCP/TLS/LB, ayrı OS süreçleri ve AD dahil değildir. Test PostgreSQL durduruldu.


### Gerçek loopback HTTP ile iki host

PostgreSQL 2/2 senaryo gerçek Kestrel TCP dinleyicileriyle tekrar geçti. Host portları farklı/loopback; bağımsız HTTP istemcisi ilk dinleyici health 200 ve kapatıldıktan sonra bağlantı hatasını gördü. Diğer host geçerli cookie ile auth/me ve automation okumalarını sürdürdü; merkezi logout eski cookie'yi reddetti. Host'lar aynı process, LDAP sentetik, TLS/proxy yok.

### İki ayrı dotnet sürecinde yerel doğrulama

Genişletilmiş PostgreSQL senaryoları 2/2 geçti: gerçek API DLL'i, farklı PID/loopback portları, ortak key/PostgreSQL, demo session paylaşımı ve logout; ilk süreç sonlandırıldıktan sonra ilk adreste bağlantı hatası ve ikinci adreste geçerli session ile okumalar doğrulandı. Süreçler ve PostgreSQL kapatıldı. Tek bilgisayar, Development/demo, doğrudan adres erişimi; proxy/TLS/AD yok.

### Yerel YARP üzerinden dağıtım ve geçiş

PostgreSQL iki kurulum senaryosu 2/2 geçti: iki API process'i, tek loopback YARP adresinden login/round-robin, her iki backend'in kullanılması, bir API Kill sonrası kalan backend'de 10 ardışık auth/me 200 ve CSRF logout/401. Sağlık tespiti sırasında 502/503 kabul edilir; kesintisiz SLA ölçümü değildir. TLS/kurum ağı/proxy HA/DB failover kapsam dışı.


### Yinelenen manuel teşhis

Build.ps1 95/101 .NET testi ve üç publish ile başarılı. Dört aktif durumda yeni manuel iş reddedildi; iki eşzamanlı istekte tek Created/tek Conflict ve tek audit doğrulandı. İptal sonrası yeni iş açılabildi. Tarayıcı arama testi debounce yenilenmesini bekleyecek ve hedef hostname satırını seçecek şekilde sağlamlaştırıldı.

Son doğrulama: 95/95 .NET, 4/4 Playwright ve genişletilmiş 2/2 PostgreSQL senaryosu geçti. PostgreSQL yarışında da tek teşhis oluşturuldu; iptal sonrası yeni istek kabul edildi. Test cluster'ı kapatıldı.


2026-10-24: Domain kabul sınırı için 6 yeni davranış regresyonu geçti; toplam 101 .NET testi. Manuel eşzamanlı farklı cihaz isteği, domain ayrımı, iptal sonrası kapasite, dört aktif durum için AI yeniden analiz ve otomatik heartbeat ertelemesi kapsanır.

Son doğrulama: scripts/Build.ps1 başarılı (101/101); Chromium Playwright 4/4; PostgreSQL 17.11 gerçek motor üzerinde EF ve schema.sql yolları 2/2 geçti (1 dakika 18 saniye). İki API host'unda farklı cihazlara eşzamanlı istekte kalan tek slot aşılmadı. Geçici PostgreSQL test cluster'ı kapatıldı. Şema değişikliği yapılmadı.


AI yeniden analiz düzeltmesi: dört aktif durum için 409 ve iptal sonrası yeniden kabul/CompletedAt temizliği doğrulandı. Toplam 105/105 .NET ve 4/4 Playwright geçti.

PostgreSQL son çalışma: 2/2 senaryo, 1 dakika 24 saniye; yeniden analiz/yeni teşhis yarışı iki API host'u üzerinde geçti. Test cluster'ı kapatıldı. Build.ps1 üç uygulama publish adımıyla tamamlandı.


2026-10-25: Ajan kanıt teslimi regresyonları geçti. Süresi dolan/iptal edilmiş/başka cihaza ait lease ile gönderim 409; iş ve audit değişmedi. Yeniden atanan işte eski lease reddedildi, yeni lease kabul edildi. Build.ps1 başarılı: 108/108 .NET ve API/worker/agent publish. Aynı kaynak sürümündeki Playwright 4/4 geçti. Üretim kaynak kodu değişmediği için önceki PostgreSQL 2/2 sonucu yeniden çalıştırılmış gibi sunulmaz.


2026-10-25 düzeltme kuyruğu: 111/111 .NET, 4/4 Playwright ve Build.ps1 publish adımları geçti. Üç yeni regresyon, 12 uygun olmayan işin arkasındaki manuel düzeltmenin seçilmesini doğrular: otomasyon kapalı, eski katalog sürümü ve bilinmeyen kod. İlk eski sürüm testindeki geçersiz yabancı anahtar fixture'ı gerçek katalog kaydıyla düzeltildi.

Düzeltme kuyruğu doğrulaması tamamlandı: 111/111 .NET, 4/4 Playwright, PostgreSQL EF/schema.sql yolları 2/2 (47 saniye). Geçici test cluster'ı kapatıldı. Üretim AD/HA ve gerçek filo kapasitesi kabulü değildir.

2026-10-25 servis yükseltme önkontrolü doğrulaması: 6/6 PowerShell sentetik senaryo, 111/111 .NET, 4/4 Playwright ve Build.ps1 publish başarılı. Gerçek servis/ACL değişikliği yapılmadı; LocalSystem/DPAPI dağıtım kabulü ayrı Windows test cihazında bekliyor. Bu değişiklik DB veya uygulama endpoint'lerini değiştirmediği için PostgreSQL testi yeniden çalıştırılmadı.


2026-10-25 AI sonuç görünümü: Build.ps1 (6 kurulum önkontrolü, 111 .NET testi, publish) ve Playwright 5/5 geçti. Yeni tarayıcı testi sentetik yanıtla güven %70, eksik güven, eksik kanıt, kanıt atfı ve doğrulama adımlarını sınar. HTML benzeri metin düz metindir. Üç tema ve 390x844 mobilde yatay taşma kontrolü geçti; masaüstü/mobil ekran görüntüleri incelendi. Browser plugin not available; mevcut Playwright kullanıldı. Ekran görüntüleri TEMP/wfi-ai-evidence-{desktop,mobile}.png konumundadır. Bu bir model doğruluk veya yeni Codex Security taraması değildir.

2026-10-25 PostgreSQL büyük envanter kabulü: EF ve deploy/schema.sql yollarında 40.000 cihazlı sayfalama/arama/domain ayrımı kontrolleri dahil 2/2 senaryo geçti (toplam paket 47 saniye; bu süre API performans metriği değildir). Geçici cluster kapatıldı. Build.ps1: 111 .NET testi, 6 kurulum önkontrolü ve publish başarılı. Üretim/UI kodu değişmedi; önceki 5/5 Playwright sonucu bu tur yeniden çalıştırılmış gibi sunulmaz.


2026-10-25 bekleyen düzeltme iptali: Build.ps1 (119/119 .NET, 6 kurulum önkontrolü, publish), 5/5 Playwright ve 2/2 PostgreSQL senaryosu geçti. PostgreSQL claim/cancel yarışı tek geçerli sonuç bırakır; başlayan işi iptal olarak etiketlemez. UI yolunda cihaz → manuel DNS isteği → iptal → Başlamadan iptal edildi durumu ve iptal düğmesinin kalkması doğrulandı. Üç tema, mobil taşma, pageerror kontrolleri geçti; mobil görüntü incelendi. Browser plugin not available; mevcut Playwright kullanıldı. Şema değişikliği yoktur. Gerçek OS işlemi/AD/üretim HA ve 40 bin aktif ajan kabulü değildir.

2026-10-25 bağlantı önkontrolü: Build.ps1 başarılı; 119/119 .NET, 6/6 servis önkontrolü ve 12/12 bağlantı doğrulama senaryosu geçti. Yerel HTTP-only demo üzerinde DNS başarılı, iki HTTPS kontrolü beklenen şekilde başarısız oldu. Kurum ağına istek gönderilmedi. UI/DB kodu değişmediği için önceki 5 Playwright/2 PostgreSQL sonucu yeniden çalıştırılmış gibi sunulmaz.


2026-10-25 manuel düzeltme tekrar isteği: Build.ps1 başarılı (123/123 .NET, 6 kurulum ve 12 bağlantı önkontrolü); PostgreSQL iki API host'unda yarış/iptal sonrası yeni istek dahil 2/2 senaryo geçti. Playwright frontend dizininden 5/5 geçti; tekrar tıklamada 409 açıklaması ve tek bekleyen DNS işi doğrulandı. İlk yanlış çalışma dizini fixture dosyası bulunamadı hatası üretti; doğru dizinde tam tekrar başarılı oldu. Şema değişikliği yoktur.


2026-10-25 otomatik tekrar koruması: Build.ps1 başarılı (129 .NET, 6 servis ve 12 bağlantı önkontrolü); PostgreSQL 2/2 geçti. Altı yeni .NET senaryosu eski aktif/belirsiz ve eski terminal iş ayrımını doğrular. İlk test fixture'ındaki EF tracked eski durum okuması AsNoTracking ile düzeltildi. Tarayıcıda ilk senaryo demo henüz başlamadığı için connection-refused aldı; sunucu başladıktan sonra yalnız başarısız senaryo yeniden çalıştırıldı ve geçti. Diğer dört senaryo ilk çalışmada başarılıydı; toplam beş senaryo doğrulandı. Gerçek kurum/OS/kapasite ve yeni güvenlik taraması kabulü açık.

### AD kabul planı — 2026-10-25
Kaynak incelemesiyle docs/ad-acceptance.md eklendi. Kısa adın UPN'e çevrilmesi (ayrı sAMAccountName araması yapılmaması), alternatif suffix/NetBIOS sınırı, nested/primary grup davranışı, grup türü zorlamasının bulunmaması ve mevcut oturumun grup değişikliğinde hemen iptal olmaması açıklandı. Gerçek AD test sonucu üretilmedi; kurum ortamı hazır olduğunda uygulanacak kabul matrisi hazırlandı. Uygulama kodu/port/şema/kurulum değişmedi.


### Kullanıcı yerel kabulü için hazır sürüm — 2026-10-25
Yerel demo http://127.0.0.1:5080 üzerinde health=alive ve ready=ready ile doğrulandı. Son kaynak derlemesi 141/141 .NET, 6 servis ve 12 bağlantı önkontrolüyle başarılı. Son Chromium çalışması 5/5 geçti. Gerçek yerel phi4-mini:latest sentetik teşhisi completed oldu; OS onarımı çalıştırılmadı. LDAP groupType rol kontrolünün 12 yerel regresyonu bu .NET toplamına dahildir; gerçek AD testinin yerine geçmez. Açılış/yeniden başlatma ve test sırası docs/local-testing.md içinde. Mevcut .data/sırlar dağıtım paketinde yoktur.



## GitHub yayını — 27 Ekim 2026

Tanıtım: https://berkayatag.github.io/Windows-Failure-Intelligence/
Kaynak: https://github.com/BerkayAtag/Windows-Failure-Intelligence
Paketler: https://github.com/BerkayAtag/Windows-Failure-Intelligence/releases/tag/v0.6.0-preview.2

GitHub Actions temiz Windows ortamında kurulum paketlerini üretti ve yayımladı. Sunucu EXE, ajan EXE, pilot ZIP ve SHA256SUMS.txt bağlantıları HTTP 200 döndü; üç paket için GitHub asset digest değerleri SHA256SUMS ile eşleşti. Bu kontrol gerçek Windows kurulumu veya Authenticode yayıncı doğrulaması değildir. İlk ayrı CI çalışmasındaki PowerShell cold-start timeout için sentetik testin iki işlemine ayrı 60 saniye bütçe verildi; üretim onarım timeout'u değişmedi.

Son GitHub doğrulaması başarılı: https://github.com/BerkayAtag/Windows-Failure-Intelligence/actions/runs/40496628234 . Temiz Windows ortamında Build, pnpm/NuGet kontrolleri, EF pending-model kontrolü ve tarayıcı testleri geçti. GitHub'dan indirilen iki EXE'nin gerçek dosya SHA256 değerleri de manifestle eşleşti; EXE'ler çalıştırılmadı.


### Yayın ve son doğrulama — 29 Ekim 2026

0.6.0-preview.3 yayımlandı: https://github.com/BerkayAtag/Windows-Failure-Intelligence/releases/tag/v0.6.0-preview.3
Canlı tanıtım: https://berkayatag.github.io/Windows-Failure-Intelligence/
GitHub temiz Windows doğrulaması başarılı: https://github.com/BerkayAtag/Windows-Failure-Intelligence/actions/runs/40805030864

213/213 .NET, 13/13 Playwright ve PostgreSQL 17 üzerinde 2/2 test geçti. İlk PostgreSQL denemesinde 55439 portu Windows tarafından reddedildi; çıktısı dosyaya yönlendirilmiş ikinci koşu beklediği için yalnız o sentetik cluster durduruldu. 35440 portunda normal çıktı ile temiz tekrar 2/2 geçti ve cluster kapatıldı. Bu durum kapasite/HA kabulü değildir.

NuGet ve pnpm taramaları bilinen zafiyet bildirmedi. İndirilen sunucu EXE'sinin SHA256 değeri GitHub digest ile eşleşti; CompanyName Berkay Atağ ve FileVersion 0.6.0.3 okundu. Canlı sitede 1440/390/768px, görseller, menü bağlantısı, güncel sürüm, © 2026 Berkay Atağ · WFI altbilgisi ve ayrı client download bağlantısının kaldırılması doğrulandı. Gerçek Windows VM appwiz/kurulum/kaldırma ve AD/HA kabulü halen açık; imzasız EXE'deki Publisher alanı sertifika değildir.
