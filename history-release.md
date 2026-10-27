# WFI sürüm geçmişi

## 0.6.0-preview.1 — 26 Ekim 2026 (geliştirme önizlemesi)

- Birleşik ajan check-in, uyarlanabilir bekleme ve PostgreSQL ortak model slotu/fencing; Sistem Sağlığı ve isteğe bağlı OTLP.
- Sabit hedefli onarım kampanyaları; pilot, bakım aralığı, paralellik, ayrı genişletme onayı ve başarısız/belirsiz sonuçta duraklatma.
- Salt okunur BigFix merkez eşleme/rapor doğrulaması. API kesintisi yerel arızadan ayrı; onarım sonrası veri öncesi baseline olarak kullanılmaz.
- AI Kalite Laboratuvarı, 40 sentetik vaka varyantı, 80 geçmiş açık/kapalı değerlendirme; operatör geri bildirimi ve negatif deneyim veto filtresi.
- Eski demo veritabanında JSON sürüm alanı yükseltme hatası düzeltildi; analiz verisinin korunması ve tekrar yükseltme test edildi. Readiness yeni şemayı denetler.
- AI model-listesi hatasının başarılı liste yenilemesiyle gizlenmesi düzeltildi; geçersiz model adresinde kesintiden dönen iş bloke edilir.
- Bu önizleme imzasızdır; 5–10 numaralı yol haritası geliştirmeleri ve gerçek kurum kabulü tamamlanmadı. Detay: docs/implementation-roadmap.md ve docs/operations-0.6.md.


## 0.5.0 — 26 Ekim 2026

- Tek dosyalı EXE bootstrap; gömülü self-contained ajan/setup, korunan geçici açılım dizini ve çıkış kodu aktarımı.
- Sessiz kurulum/yükseltme: --silent, --server, --device-id, --token-file, --upgrade, --bigfix-sha256. Parola/token doğrudan argüman olarak kabul edilmez.
- Yeni sol menü Ajan ve Ağ Erişimleri; belirgin indirme kartı, paket hash/boyut/sürümü, komut kopyalama, ağ kural tablosu ve mobil görünüm.
- Doğrulanmış deneyimlerin AI bağlamına katılması; domain/kapsam/katalog/güncel kanıt filtresi, 180 gün sınırı, 5 örnek, kaynak işlem kimlikleri ve Admin dışlama/audit.
- Mobil Ağ Erişimleri tablosunun sayfa dışına taşması düzeltildi; tablo kendi alanında kaydırılır.
- Readiness eksik deneyim şemasında 503 döndürür; paket bilgi hatası yükleniyor durumunda takılı kalmaz.
- VerifiedExperienceMemory migration: LearningExcluded, LearningContextJson ve domain/status/completedAt indeksi; schema.sql birlikte güncellendi.


## 0.4.1 — 26 Ekim 2026

- “Genel (BigFix dahil)” etiketi “Genel” oldu; teşhis kapsamı korunuyor.
- WFI-Agent-Setup.exe: pencereli, yönetici yetkisi isteyen istemci kurulumu. HTTPS Admin oturumu ile makineyi otomatik kaydeder; tek kullanımlık kayıt bilgisi kurucuya stdin üzerinden aktarılır. Parola saklanmaz, oturum çıkışı denenir.
- Ajan ve setup self-contained win-x64 yayınlanır. PowerShell 7 x64 gereklidir; istemciye ayrı .NET runtime kurulmaz.
- Aynı domain'de eşzamanlı cihaz kaydı transaction/advisory lock ile seri hale getirilir; kimlik üzerine yazılmaz.
- Kurulum kaydı, yetki reddi, HTTP reddi, token tekrar kullanımı ve otomatik envanter yenilemesi için regresyonlar eklendi.
- Şema değişikliği yok; 0.4.0 migration zinciri geçerlidir.


## 0.4.0 — 2026-10-25

- Birincil akış: makine + sorun açıklaması → ajan kanıtı → sunucu AI. Açıklama 4000 karakterle sınırlı, redakte edilir ve komut/onarım kanıtı olarak kullanılmaz.
- Genel ve BigFix kapsamları: BESClient servis durumu ve standart BigFix günlükleri. Sabit PB-BIGFIX-REPAIR kataloğu önce başlatmayı, başarısızsa yerel hash onaylı yeniden kurulumu dener; timeout, dosya kilidi, reparse ve yazma izni kontrolleri vardır. Kullanıcı reinstall.cmd'nin etkileşimsiz ve yeniden başlatmasız olduğunu doğruladı.
- Ajan indirme yalnız Admin oturumuna açık; gizli yapılandırma dağıtım paketine alınmaz.
- Yeni sunucu kurulum başlatıcısı Setup-Server.cmd ve PowerShell IIS kurucusu. Üretim migration'ı kendiliğinden uygulanmaz.
- Karanlık tema yerel select/option renkleri düzeltildi. Üç tema ve mobil görünüm korunur.
- ProblemDiagnosisAndBigFix migration'ı ve schema.sql birlikte güncellendi. Sunucu/ajan katalog sürümü 4.0.0; birlikte yükseltin.
- Gerçek AD, BESClient/reinstall ve Windows Server IIS kurulumu için kurum kabulü gerekir; 40 bin istemci kapasite garantisi verilmez.


## Geliştirme aşaması — 23 Ekim 2026 — Ajan / Ollama

Henüz yayımlanmamış çalışmadır. Mevcut diğer-model ekleri incelendi; ayrıntı ve tam kabul kapsamı docs/agent-ollama-implementation.md içinde tutuluyor.

- AiConfig primary key hatası, eksik LDAP authenticator DI ve kaybolan status endpoint'i düzeltildi.
- Envanter/AI/incident okuma ve remediation onayında domain sınırları eklendi; kullanıcı tarafından gönderilen onaylayan kimliği güvenilir kabul edilmiyor.
- Remediation onayı artık ajan işlemi olmadan Success/verified sonucu oluşturmuyor.
- Ollama hatasında uydurma model doğrulaması/sağlıklı cihaz/yüksek güven sonucu kaldırıldı; model listesi gerçek endpoint yanıtını kullanır.
- Model tercihinin teşhis sırasında varsayılana dönmesi giderildi; AI çağrısına kaydedilen kanıtlardan bulgular aktarılıyor.
- Üç Ollama regresyon testi eklendi. Tam ajan/Ollama kabulü ve yeni kurulum paketi henüz tamamlanmadı.

## 0.2.0 — 22 Ekim 2026 — Güvenlik ve HA hazırlığı

Pilot sürümdür; 40.000 eşzamanlı istemci için üretim sertifikasyonu değildir.

- İç içe Event ve aşırı XML derinliği reddediliyor; doğrusal XML okuma ve iptal kontrolleri eklendi.
- Bearer/Basic, tırnaklı anahtar/değer ve XML sır maskelemesi düzeltildi. Maskeleme kanıt kısaltılmadan önce yapılıyor.
- Çıkış oturum kimliğini ortak DB'de iptal eder; eski çerez diğer düğümde de reddedilir. Önceki sürümün oturumlarında session_id olmadığından yükseltmede yeniden giriş gerekir.
- Yalnızca yapılandırılmış proxy IP'lerinden X-Forwarded-For/Proto işlenir. HA yapılandırmasında PostgreSQL, mutlak ortak veri/anahtar yolları ve anahtar koruma sertifikası zorunludur.
- DB hazırlık endpoint'i, audit yazmayan durum endpoint'i, stabil sayfalama, arama uzunluğu ve API 404 düzeltmeleri.
- Gizli sekmede sorgu durdurma, çakışan periyodik sorguları engelleme; liste 15 saniye, aktif iş 5 saniye aralıkla güncellenir.
- Standart/açık/karanlık temalar ve kalıcı kullanıcı tercihi; mobil düzen ve form doğrulaması düzeltmeleri.
- Ayrı worker artık appsettings.local.json okur; environment değişkenleri üstündür.
- SessionRevocationIndex migration ve güncel idempotent SQL. Logout kayıtları oturumun 30 dakikalık süresi dolmadan silinmemelidir.
- Sunucu, istemci, firewall, yük dengeleyici, kapasite ve güvenlik dokümanları eklendi.
- API/worker yayınlarında yerel ayar ve runtime dosyaları dışlanır; scripts/Package.ps1 dokümanlı ZIP paketini üretir.

Doğrulama sonuçları docs/verification.md ve docs/capacity-results.json içindedir. Gerçek AD, PostgreSQL ve LB kabulü kurum ortamında tamamlanmalıdır.

## 0.1.0 — 22 Ekim 2026 — İlk pilot

LDAPS/RBAC giriş, log yükleme, yedi analiz kuralı, kalıcı kuyruk, audit, React arayüzü, PostgreSQL migration ve yerel demo. Başlangıç bağımlılık açıkları giderildi. SCCM/BigFix otomatik toplama henüz uygulanmadı.

### Ajan protokolü ve politika — 23 Ekim, geliştirme devamı

- Cihaz kayıt/revoke API'leri, bir saatlik tek kullanımlık enrollment anahtarı ve cihaz kimliğine bağlı 256-bit credential doğrulaması eklendi. Credential yalnızca hash olarak DB'de tutulur; HTTPS zorunlu üretim sınırıdır.
- Eski thumbprint metni karşılaştıran heartbeat endpoint'i kaldırıldı; /agent/me endpoint'leri ayrı cihaz kimliğiyle doğrulanır, kullanıcı cookie'si cihaz yetkisi sağlamaz.
- Teşhis işine DeviceId bağlandı; atomik lease alma, süre/cihaz/token kontrolü, tekrar sonuç reddi ve kanıt boyut/maskeleme kontrolleri eklendi.
- Varsayılan otomatik mod, cihaz Enabled/Disabled/Inherit ayarı ve tüm cihazlara uygulama Admin API'leri eklendi. Otomatik iş üretimi ve gerçek ajan yürütücüsü sonraki bağlı adımlardır; bu API'lerin varlığı otomatik düzeltmenin tamamlandığı anlamına gelmez.
- AgentAndOllama migration ve deploy/schema.sql eklendi. Üretim DB'sine uygulanmadı; mevcut SQLite verisinin yükseltmesi henüz tamamlanmadı.
- Yeni testler tek kullanımlık kayıt, revoke, başka cihazı taklit etme, lease/replay, kanıt maskeleme ve politika önceliğini kapsar.

### Windows ajanı ve gerçek yürütücü — 23 Ekim, geliştirme devamı

- WFI.Agent solution'a eklendi, yanlış/eksik paket referansları ve olmayan ikon ayarı düzeltildi; agent win-x64 publish adımı eklendi.
- Windows servis host'u, HTTPS zorunluluğu, redirect engeli, jitter'lı polling ve yeni cihaz protokolü eklendi. TLS doğrulamasını kapatan taslak kaldırıldı.
- Kimlik ve iş durumu DPAPI CurrentUser ile korunur. Kayıttan önce credential diske kaydedilir; kayıp enrollment yanıtı heartbeat ile toparlanabilir. İptal edilen kayıt otomatik yeniden kayıt yapmaz.
- Windows olayları, sınırlı SCCM logları ve yerel sistem/ağ kanıtı toplanır; Google/public DNS'e IP bulma bağlantısı kaldırıldı.
- Sahte ExecutePlaybookLocally başarı metodu kaldırıldı. Derlenmiş katalogdan önkontrol/işlem/doğrulama çalıştıran PowerShell yürütücüsü eklendi. Model veya DB içindeki serbest script yürütülmez.
- Düzeltme claim/result hattı cihaz+lease ile bağlanır. İşlem kaydı önce diske yazılır; crash sonrası belirsiz işlem tekrar çalıştırılmaz. HTTP sonucu kaybolduğunda receipt tekrar gönderilebilir. Otomatik işlerde politika dispatch sırasında yeniden kontrol edilir.
- DNS cache temizleme, Update servis başlatma, SCCM restart ve DISM onarım kataloğu eklendi. Disk doluluğu gibi ilgisiz bulgular servis başlatma/DISM işlemlerine bağlanmaz.
- RemediationCatalog migration ve SQL güncellendi. Install-Agent.ps1 ve agent.example.json eklendi. Installer sözdizimi kontrol edildi; bu bilgisayara servis kurulmadı.
- 62 test geçti: ajan kayıt/kanıt/düzeltme receipt döngüsü (sentetik komut adapter'i), önkontrol/işlem/doğrulama hatası ve bilinmeyen komut reddi dahil. Windows PowerShell yürütücüsü gerçek fakat yalnızca sentetik çıktı/hata komutlarıyla test edildi; gerçek sistem onarımı çalıştırılmadı.
- Otomatik iş üretimi, AI kuyruğu, UI ve tam güvenlik kabulü hâlâ devam ediyor.
## 0.3.0 pilot — 23 Ekim 2026 — Son doğrulama

Önceki geliştirme maddelerindeki açık durumlar bu bölüm ve docs/verification-current.md ile güncellenir; eski sonuçlar tarihsel kayıttır.

- AI/background teşhis kuyruğu ve otomatik iş üretimi tamamlandı. Gerçek phi4-mini:latest ile sentetik uçtan uca analiz geçti; model tercihi DB'de kalır.
- Cihazlar ve AI ekranı: model seçimi, enrollment/token yenileme, cihaz/genel otomasyon, teşhis/AI sonuçları, manuel katalog düzeltmesi ve onay/geçmiş.
- Otomatik teşhis 15 dakika, cihaz başına otomatik düzeltme 24 saat bekleme; yalnız izinli katalog/kural ve açık politika. Çalışmaya başlamış işi geri alma yoktur.
- AutomationScheduling migration, SQL ve mevcut demo SQLite yükseltmesi. Üretim başlangıcında otomatik migration yok.
- Bounded log okuma, NETLOGON 5719 dil bağımsız kanıt işareti, DPAPI disk flush/tek süreç kilidi, servis klasörü izinleri ve kurulum hedefi/reparse kontrolleri.
- Ajan rate limit doğrulanmış cihaz bazında; NAT paylaşımı engeli düzeltildi. Remediation cihaz transaction kilidi, katalog sürüm/onay tekrar kontrolü ve lease/receipt testleri.
- Eksik model confidence artık uydurma başarı üretmez; mobil grid taşması düzeltildi.
- 68 .NET test ve 4 Playwright testi geçti; API, worker, win-x64 ajan yayımlandı. NuGet transitive/pnpm production sorguları bilinen açık bildirmedi.
- Sunucu, istemci, firewall, HA, kapasite, mimari ve uygulama notları güncellendi. Package.ps1 agent ve scripts içeren WFI-0.3.0-pilot.zip üretir.
- Yeni Codex Security scan başlatma derleme çıktısı okuma hatası aldı; tam yeni güvenlik onayı YOK. Gerçek LDAPS, Windows servis/onarım, PostgreSQL/LB failover ve 40.000 ajan kabulü yapılmadı.


### 0.3.0 kesinti ve yükseltme doğrulaması

- Ajan süreç yeniden başlatma testleri eklendi: sunucuda commit edilmiş fakat HTTP yanıtı kaybolmuş receipt tekrar gönderilir, komut tekrar çalışmaz; journal'da sonucu olmayan işlem yeniden başlatılmaz ve belirsiz/başarısız sonuçlanır. Her iki durumda tek terminal audit doğrulandı.
- Install-Agent.ps1 artık -PollSeconds (10–600) alır; yükseltmede belirtilmezse mevcut değer korunur. Cihaz ID/sunucu/state yolu uyuşmazlığı servisi durdurmadan reddedilir. Token istemi/doğrulaması da servis durdurulmadan yapılır.
- scripts/Build.ps1: 70 test geçti; frontend/API/worker/agent paketleri yeniden üretildi. Installer AST kontrolü geçti. Yeni değişiklik arayüz davranışını değiştirmedi; önceki 4 Playwright testi bu kaynak UI'sını kapsar.
- Gerçek Windows servis kurulum/yükseltme, AD ve PostgreSQL/LB kabulü hâlâ test ortamı gerektirir.


### Yerel devam — yürütücü ve SDK seçimi

- PowerShellRunner önceden iptal edilmiş istekte süreç başlatmaz. Çalışan komut iptalinde süreç sonlandırıldıktan sonra stdout/stderr okuma görevleri tamamlanır; stream'ler görevler bitmeden kapatılmaz.
- Beş yeni regresyon: önceden iptal edilen komutun yan etki oluşturmaması, çalışan shell'in iptalde çıkması, toplam 200.000 karakter stdout/stderr'nin kilitlenmeden 3.601 karakterle sınırlandırılması, süresi geçmiş ve katalog sürümü uyumsuz işin reddi. Gerçek shell komutları yalnız sentetik dosya/çıktı/bekleme kullanır; OS onarımı yoktur.
- Build.ps1 ve Start-Demo.ps1 PATH'teki runtime-only dotnet'i SDK sanıyordu. Resolve-Dotnet.ps1 tüm adaylarda .NET 10 SDK varlığını kontrol eder; kullanıcı dizinindeki SDK'ya geçer. Mevcut runtime-only PATH ortamında hata yeniden üretildi ve düzeltme doğrulandı.
- Build/publish başarılı; 75/75 .NET ve 4/4 Playwright geçti. Demo gerçek Start-Demo.ps1 yolundan başlatıldı. Kurum test ortamının henüz hazır olmadığı kullanıcı tarafından belirtildi; yerel geliştirme sürüyor, gerçek AD/HA/servis kabulü yapılmış sayılmıyor.


### Ollama keşif hataları

- Bozuk model listesi JSON'u ve model keşfi timeout'u artık bağlantı yok durumu verir; geçersiz/boş/null model girdileri seçime sunulmaz. Kullanıcının iptal ettiği istek iptal olarak korunur.
- Beş yeni regresyonla 80/80 .NET testi geçti; Build.ps1 frontend/API/worker/ajan yayınlarını tamamladı.
- Erişilemeyen loopback Ollama adresiyle ayrı yerel API çalıştırıldı. Chromium'da bağlantı uyarısı, devre dışı model kaydı ve kullanılabilir cihaz envanteri doğrulandı; pageerror yok. Gerçek Ollama servisi durdurulmadı.


### İptal/onay durum geçişleri

- Teşhis iptali ve düzeltme onayı koşullu UPDATE + audit transaction kullanır. Terminal teşhis geç gelen iptalle değişmez; tekrar onay Executing işi Approved'a geri döndüremez. Durumu değişmiş iş için 409, domain kapsamı dışında/bulunamayan iş için 404 döner.
- AI yanıtı beklerken iptal testi: geç sonuç kaydedilmez, otomatik düzeltme oluşmaz, tek iptal audit'i vardır. Eşzamanlı iki onayda tek başarılı yanıt/tek audit doğrulandı. Ölçüm SQLite TestServer kapsamındadır; PostgreSQL yarış kabulü değildir.
- Build.ps1 başarılı, 82/82 .NET testi geçti. Playwright 4/4; yüksek risk manuel isteğin önce yönetici onayı beklemesi ve onaydan sonra yalnız ajan beklemesi de eklendi. Gerçek DISM yürütülmedi.


### Cihaz envanteri sayfalama düzeltmesi

- Aynı heartbeat zamanına sahip cihazlar Id ile ikincil sıralanır. Arama boşlukları temizlenir, küçük harfe dönüştürülür ve 63 karakter sınırı uygulanır; sorgular istemci iptalini izler.
- Domain/LastHeartbeat/Id indeksi için 20261023221516_DeviceInventoryPaging migration'ı ve idempotent deploy/schema.sql eklendi.
- 40.000 sentetik cihazla ilk, ikinci, orta ve son sayfa; tekrar okuma, domain ayrımı, arama ve küçük yanıt gövdesi doğrulandı. SQLite sonucu PostgreSQL kapasite sonucu değildir.

Doğrulama: Build.ps1 83/83 .NET testi ve üç publish çıktısı başarılı; Playwright 4/4 geçti; EF bekleyen model değişikliği yok. Firewall, sunucu/istemci kurulumu ve kapasite belgeleri güncellendi.

### Eşzamanlı model/politika kaydı ve audit bütünlüğü

İlk model/politika kaydındaki okuma-sonra-ekleme yarışı kaldırıldı; parametreli ON CONFLICT kullanılıyor. Model değişiminde diğer AI ayarları korunur. Domain politikası, tüm cihazlara uygulama ve audit aynı transaction'dadır; cihaz bazlı politika da audit ile atomik hale getirildi. Şema değişikliği yoktur. Eşzamanlı ilk kayıt, geçersiz model ve sentetik audit hatasında rollback regresyonları eklendi.

Son doğrulama: Build.ps1 başarılı, 87/87 .NET ve 4/4 Playwright testi geçti. Sunucu/istemci, firewall, LB ve güvenlik notları güncellendi; ek port veya şema değişikliği yok.

### Portal üzerinden teşhis iptali

Analyst/Admin için aktif teşhislerde Teşhisi iptal et düğmesi eklendi. Terminal işlerde düğme gösterilmez; hata/iptal açıklaması görünür. AI sonucu olmayan terminal iş artık hâlâ sonuç bekliyormuş gibi açıklanmaz. Var olan atomik iptal API'si kullanılır; devam eden model çağrısı/kanıt toplama hemen kesilmeyebilir ve önceden oluşturulmuş düzeltmeler iptal edilmez. Browser regresyonuna iptal, terminal düğme yokluğu ve sonuç yok açıklaması eklendi.

Doğrulama: 87/87 .NET, 4/4 Playwright; mobil ekran görüntüsü incelendi. İptal akışı mevcut backend yetkilendirmesini ve terminal durum korumasını kullanır.

### Ajan atama süresi koruması

Düzeltme yürütücüsü artık atamanın kalan süresi ile katalog timeout'undan kısa olanını kullanır. Süre dolumu/iptal her aşama öncesinde kontrol edilir; boş iş veya lease kimliği komuta ulaşmaz. Ön kontrol sırasında atama süresi dolduğunda action aşamasına geçilmemesini doğrulayan sentetik test ve iki geçersiz kimlik testi eklendi. Şema ve katalog komutları değişmedi.

Doğrulama: 90/90 .NET ve 4/4 Playwright testi geçti. README, güvenlik/uygulama notları, firewall ve sunucu/istemci kurulum belgeleri güncellendi.

### Gerçek PostgreSQL yerel kabul testi

Ayrı WFI.PostgresTests projesi ve Test-Postgres.ps1 eklendi. EF migration ve deploy/schema.sql boş PostgreSQL 17.11 veritabanlarında tekrar uygulanarak sınandı; bağımsız bağlantılarla cihaz başına tek düzeltme sahiplenme ve yinelenen sonuçta tek audit doğrulandı. 2/2 test geçti. Loopback/SCRAM test cluster'ı durduruldu; kurum ağına bağlanılmadı. PostgreSQL runtime WFI paketine eklenmez. Ayrıntı docs/postgresql-validation.md.

Genel Build.ps1 yeniden başarılı: 90/90 .NET testi ve API/worker/ajan publish. Yeni PostgreSQL testleri ayrı 2/2 sonuçtur; ürün arayüz kaynakları bu tur değişmedi.

### PostgreSQL model tercihi regresyonu

İki bağımsız servis/DI örneğinin ilk model kaydı eşzamanlı başlatılarak gerçek PostgreSQL üzerinde tek ayar satırı, diğer örnekten görünürlük, geçersiz model reddi ve diğer AI alanlarının korunması sınandı. Keşif yanıtı sentetiktir; test dış Ollama servisine bağlanmaz.

Genişletilmiş PostgreSQL senaryoları 2/2 geçti; test cluster'ı durduruldu. Ürün davranışı, kurulum parametreleri, şema veya firewall gereksinimi bu test genişletmesinde değişmedi.

Kullanıcı gereksinimleri için docs/acceptance-matrix.md eklendi: mevcut kod/test kanıtları, otomatik düzeltmenin sınırlı katalog kapsamı, gerçek AD/Windows/HA/filo ve güvenlik taraması açık işleri ayrı kaydedildi.

Genel Build.ps1 tekrar geçti: 90/90 test ve üç publish çıktısı başarılı.

### PostgreSQL API politika/yetki/audit doğrulaması

PostgresApi TestServer altyapısı eklendi. Viewer değişiklikleri 403, yabancı domain cihaz değişikliği 404; toplu politika yalnız kendi domain'ini etkiler. Gerçek PostgreSQL trigger ile audit hatasında genel politika ve Disabled cihaz istisnasının korunması, cihaz bazlı değişikliğin geri alınması ve audit sayısının değişmemesi sınandı. Sentetik LDAP, gerçek cookie/CSRF middleware kullanılır; network/TLS/AD veya iki API düğümü kabulü değildir.

Doğrulama: 2/2 PostgreSQL senaryosu ve 90/90 genel test başarılı. Ürün davranışı/şema/port veya kurulum parametresi değişmedi; kabul matrisi ve doğrulama belgeleri güncellendi.

### PostgreSQL üzerinde iki API host'unda oturum doğrulaması

PostgresApi ortak key diziniyle iki bağımsız WebApplicationFactory host'u açacak şekilde genişletildi. Cookie ikinci host'ta kabul edildi; birinci host logout'u sonrası eski cookie ikinci host'ta reddedildi. Yeni oturumla birinci host kapatıldıktan sonra ikinci host auth/me ve otomatik politika okumalarını sürdürdü. 2/2 PostgreSQL senaryosu geçti. LDAP sentetik, TCP/proxy/sertifika korumalı Production HA kapsam dışıdır.

Genel Build.ps1: 90/90 test, frontend ve üç publish başarılı. Yeni test yalnız doğrulama altyapısını genişletir; ürün portları/kurulum/şema değişmedi.

### İki Kestrel host'unda gerçek loopback TCP doğrulaması

Ortak oturum testi TestServer yerine iki farklı loopback TCP portundaki Kestrel host'una taşındı. Bağımsız HTTP istemcisiyle health erişimi ve ilk host kapandıktan sonra bağlantının reddi doğrulandı; ikinci host cookie/otomatik politika okumalarını sürdürdü. Merkezi logout reddi TCP üzerinde geçti. Özel cookie seçeneğinin istemciyi localhost:80'e yönlendirmesi test altyapısında düzeltildi. 2/2 PostgreSQL senaryosu geçti; production/proxy/TLS ve ayrı process kapsam dışı.

Genel Build.ps1 90/90 test ve üç publish ile başarılı. Firewall, sunucu kurulumu, güvenlik ve kabul/verification belgeleri yeni test kapsamıyla güncellendi.

### İki ayrı API sürecinde yerel kabul

ProcessApi test yardımcısı derlenmiş API DLL'ini ayrı PID'lerle, loopback TCP ve ortak PostgreSQL/key deposuyla başlatır. Cookie paylaşımı, merkezi logout ve ilk süreç Kill sonrası ikinci süreçte auth/me/automation erişimi doğrulandı. Geçici key/config dizinleri temizlenir, test süreçleri sonlandırılır. Test-Postgres.ps1 runtime yolunu ortam üzerinden geçirip önceki ortam değerini geri yükler; parola komut argümanına/çıktıya yazılmaz. 2/2 PostgreSQL senaryosu geçti. Development/demo kullanılır; gerçek LB/TLS/AD veya ayrı makineler test edilmez.

Genel Build.ps1: 90/90 test, frontend ve API/worker/ajan publish başarılı. Kurulum, firewall, güvenlik ve kabul belgeleri güncellendi.

### Yerel reverse proxy üzerinden düğüm kaybı testi

YARP 2.3.0 yalnız PostgreSQL test projesine eklendi. İki ayrı API sürecine RoundRobin dağıtım, iki backend portunun gözlenmesi, ilk süreç sonlandırıldıktan sonra tek proxy adresinden kalan API'ye 10 ardışık auth/me başarısı ve CSRF korumalı logout doğrulandı. 2/2 senaryo geçti. Geçişte 502/503 kabul edilir; sıfır kesinti iddiası yoktur. Proxy yalnız loopback/dinamik port ve Development/demo kullanır; production proxy/TLS/DB failover kabulü değildir.

Genel Build.ps1 90/90 test ve üç publish ile başarılı. Yeni test projesinin transitive dahil paket açık sorgusu bildirilen açık bulmadı. YARP yalnız test bağımlılığıdır; üretim API/worker/ajan paketinde yoktur.

### Yinelenen manuel teşhis kuyruğu düzeltmesi

Manuel teşhis endpoint'ine cihaz satırı kilidi ve aktif iş kontrolü eklendi. Art arda/eşzamanlı istekler aynı cihaz için gereksiz aktif teşhisler oluşturmaz; 409 açıklaması ve mevcut iş ID'si döner. Kontrol otomatik heartbeat zamanlamasının kullandığı cihaz satırıyla sıralanır; işlem sırasında revoke edilmiş cihaz reddedilir. Job ve audit aynı transaction'da kaydedilir. SQLite dört aktif durum/eşzamanlılık ve PostgreSQL API yarış testleri, tarayıcıda çift istek uyarısı eklendi. Şema değişikliği yoktur.

Doğrulama: 95/95 .NET, 4/4 Playwright, 2/2 PostgreSQL senaryosu başarılı. PostgreSQL yarış testi tek oluşturma ve iptal sonrası yeniden isteği doğruladı. Arama debounce beklemesi eksik olan tarayıcı testi düzeltildi. Kurulum/firewall/kapasite ve güvenlik belgeleri güncellendi.

### Teşhis kabul sınırı — 2026-10-24
Domain başına aktif teşhis sınırı eklendi (varsayılan 1000). Manuel/yeniden analiz dolu kuyrukta 429, otomatik tarama gecikmeli yeniden deneme kullanır. Tüm API düğümlerinde aynı Diagnostics__MaxActivePerDomain değeri gerekir. Yeni port veya istemci kurulumu değişikliği yoktur; mevcut HTTPS ve API–PostgreSQL bağlantıları kullanılır. Ayrıntılar: docs/diagnostic-admission.md (docs içinden diagnostic-admission.md). Gerçek AD/üretim HA ve 40.000 aktif istemci kabulü henüz yapılmadı.

Doğrulama: 101/101 .NET, 4/4 Playwright, 2/2 PostgreSQL kabul senaryosu başarılı. Domain kotası testi iki API host'u ile yapıldı; kapasite/üretim HA onayı değildir.

### AI yeniden analiz çakışması düzeltmesi
Eski teşhisin yeniden analizi, aynı cihazda pending/running/evidence_received/analyzing iş varsa 409 ile reddedilir. Kontrol domain transaction kilidi içinde manuel/otomatik kabul ile sıralanır. Kabul edilen yeniden analiz eski CompletedAt değerini temizler. Şema, port ve ajan kurulum gereksinimleri değişmedi.

Doğrulama: Build.ps1 başarılı; 105/105 .NET, 4/4 Playwright, 2/2 PostgreSQL senaryosu geçti. İki API host'unda yeniden analiz/yeni teşhis yarışı tek aktif iş bıraktı. Gerçek AD/üretim HA/40.000 aktif ajan ve yeni Codex Security taraması kabulü bekleniyor.

### Ajan kanıt teslimi sınır testleri
Sentetik regresyonlar; süresi dolan lease, iptal edilmiş teşhis ve başka cihazın lease'iyle kanıt gönderimini kapsar. Bu talepler 409 ile reddedilmeli, kanıt ve audit kaydı oluşturmamalıdır. Süresi dolan iş yeniden atandığında eski lease geçersiz kalır; yeni lease ile teslim tek audit üretir. Uygulama davranışı, şema ve ağ/kurulum gereksinimleri değiştirilmedi.

2026-10-25: Ajan kanıt teslimi regresyonları geçti. Süresi dolan/iptal edilmiş/başka cihaza ait lease ile gönderim 409; iş ve audit değişmedi. Yeniden atanan işte eski lease reddedildi, yeni lease kabul edildi. Build.ps1 başarılı: 108/108 .NET ve API/worker/agent publish. Aynı kaynak sürümündeki Playwright 4/4 geçti. Üretim kaynak kodu değişmediği için önceki PostgreSQL 2/2 sonucu yeniden çalıştırılmış gibi sunulmaz.

### Düzeltme kuyruğunda bekleme hatası — 2026-10-25
İlk 10 bekleyen iş politika veya katalog nedeniyle uygun olmadığında sonraki geçerli manuel iş seçilemiyordu. Uygunluk filtresi artık sorgunun limitinden önce uygulanır; sıralama zaman ve iş kimliğiyle kararlıdır. Kapalı otomatik işler çalıştırılmaz ve silinmez; politika tekrar açıldığında uygun olabilir. Şema, firewall portları ve ajan kurulum adımları değişmedi.

Düzeltme kuyruğu doğrulaması tamamlandı: 111/111 .NET, 4/4 Playwright, PostgreSQL EF/schema.sql yolları 2/2 (47 saniye). Geçici test cluster'ı kapatıldı. Üretim AD/HA ve gerçek filo kapasitesi kabulü değildir.

### Ajan servis yükseltme önkontrolü — 2026-10-25
Install-Agent.ps1 mevcut servisin LocalSystem hesabını ve seçilen kurulum dizinindeki WFI.Agent.exe yolunu token isteminden, servis durdurmadan ve dosya değişikliğinden önce denetler. Özel hesap, ek komut argümanı, farklı binary yolu veya okunamayan servis yapılandırması reddedilir. Böyle bir kurulumda hesabı zorla değiştirmeyin: DPAPI state ve mevcut servis yapılandırmasını koruyan ayrı bakım planı hazırlayın. Yeni firewall portu veya sunucu şeması değişikliği yoktur. Test-AgentInstaller.ps1 altı sentetik önkontrol senaryosunu servis değiştirmeden çalıştırır ve Build.ps1'e dahildir; gerçek LocalSystem servis kabulü yerine geçmez.

2026-10-25 servis yükseltme önkontrolü doğrulaması: 6/6 PowerShell sentetik senaryo, 111/111 .NET, 4/4 Playwright ve Build.ps1 publish başarılı. Gerçek servis/ACL değişikliği yapılmadı; LocalSystem/DPAPI dağıtım kabulü ayrı Windows test cihazında bekliyor. Bu değişiklik DB veya uygulama endpoint'lerini değiştirmediği için PostgreSQL testi yeniden çalıştırılmadı.

### AI değerlendirmesinde kanıt ve belirsizlik — 2026-10-25
Teşhis sonucu ekranı modelin bildirdiği güveni, eksik kanıtları, hipotez kanıt atıflarını ve önerilen doğrulama adımlarını gösterir. Güven değeri ölçülmüş doğruluk/başarı olasılığı değildir; eksik veya geçersiz değer için yüzde uydurulmaz. Boş eksik-kanıt listesi tamlık garantisi olarak gösterilmez. Model metni düz React metni olarak görüntülenir; doğrulama adımları otomatik komut olarak çalıştırılmaz. Yeni endpoint, port, şema veya ajan ayarı yoktur.

2026-10-25 AI sonuç görünümü: Build.ps1 (6 kurulum önkontrolü, 111 .NET testi, publish) ve Playwright 5/5 geçti. Yeni tarayıcı testi sentetik yanıtla güven %70, eksik güven, eksik kanıt, kanıt atfı ve doğrulama adımlarını sınar. HTML benzeri metin düz metindir. Üç tema ve 390x844 mobilde yatay taşma kontrolü geçti; masaüstü/mobil ekran görüntüleri incelendi. Browser plugin not available; mevcut Playwright kullanıldı. Ekran görüntüleri TEMP/wfi-ai-evidence-{desktop,mobile}.png konumundadır. Bu bir model doğruluk veya yeni Codex Security taraması değildir.

### PostgreSQL büyük envanter doğrulaması — 2026-10-25
Kabul paketine 40.000 sentetik cihaz içeren PostgreSQL envanter senaryosu eklendi. Aynı heartbeat zamanında kimlikle kararlı sıralama, ilk/orta/son sayfalar, 50 kayıt sınırı, domain ayrımı, arama normalizasyonu ve listeden büyük/özel alanların çıkarılması sınanır. EF migration ve schema.sql yollarının ikisinde de çalışır. Tek kullanıcı ve TestServer API çağrıları kullanır; 40.000 aktif ajan, eşzamanlı trafik, ağ/TLS/LDAP veya üretim kapasitesi ölçümü değildir. Ürün davranışı, şema, kurulum ve firewall gereksinimleri değişmedi.

2026-10-25 PostgreSQL büyük envanter kabulü: EF ve deploy/schema.sql yollarında 40.000 cihazlı sayfalama/arama/domain ayrımı kontrolleri dahil 2/2 senaryo geçti (toplam paket 47 saniye; bu süre API performans metriği değildir). Geçici cluster kapatıldı. Build.ps1: 111 .NET testi, 6 kurulum önkontrolü ve publish başarılı. Üretim/UI kodu değişmedi; önceki 5/5 Playwright sonucu bu tur yeniden çalıştırılmış gibi sunulmaz.

### Başlamamış düzeltmeyi iptal etme — 2026-10-25
Analyst/Admin kendi domain'indeki Approved veya PendingApproval düzeltme isteğini Düzeltme geçmişi → Bekleyen düzeltmeyi iptal et ile Cancelled durumuna alabilir. Viewer ve başka domain erişimi reddedilir. API: POST /api/v1/remediations/{id}/cancel; CSRF gerekir. Koşullu durum güncellemesi ve audit aynı transaction'dadır; iptal edilmiş iş ajana verilmez ve yeniden onaylanamaz. Ajan işi önce almışsa 409 döner; Executing/Unknown/Success/Failed durumları değişmez. İptal işletim sistemi rollback'i değildir. Otomatik işi iptal etmek otomatik politikayı kapatmaz veya 24 saatlik beklemeyi sıfırlamaz; ileride uygun yeni iş oluşabilir. Kalıcı durdurma için cihaz/genel otomatik modu kapatın. Yeni port, şema veya ajan kurulumu değişikliği yoktur.

2026-10-25 bekleyen düzeltme iptali: Build.ps1 (119/119 .NET, 6 kurulum önkontrolü, publish), 5/5 Playwright ve 2/2 PostgreSQL senaryosu geçti. PostgreSQL claim/cancel yarışı tek geçerli sonuç bırakır; başlayan işi iptal olarak etiketlemez. UI yolunda cihaz → manuel DNS isteği → iptal → Başlamadan iptal edildi durumu ve iptal düğmesinin kalkması doğrulandı. Üç tema, mobil taşma, pageerror kontrolleri geçti; mobil görüntü incelendi. Browser plugin not available; mevcut Playwright kullanıldı. Şema değişikliği yoktur. Gerçek OS işlemi/AD/üretim HA ve 40 bin aktif ajan kabulü değildir.

### Bağlantı önkontrolü — 2026-10-25
Test-WfiConnectivity.ps1 eklendi: parola kullanmadan DNS, HTTPS health ve readiness yanıtlarını kontrol eder. Sertifika doğrulaması korunur; redirect/HTML başarı sayılmaz; hiçbir sistem ayarı değiştirilmez. Kullanım: [Bağlantı önkontrolü](docs/connectivity-preflight.md). Mevcut DNS ve WFI HTTPS portu kullanılır; ek firewall izni gerekmez. AD/model/ajan/HA/kapasite kabulü yerine geçmez.


2026-10-25 bağlantı önkontrolü: Build.ps1 başarılı; 119/119 .NET, 6/6 servis önkontrolü ve 12/12 bağlantı doğrulama senaryosu geçti. Yerel HTTP-only demo üzerinde DNS başarılı, iki HTTPS kontrolü beklenen şekilde başarısız oldu. Kurum ağına istek gönderilmedi. UI/DB kodu değişmediği için önceki 5 Playwright/2 PostgreSQL sonucu yeniden çalıştırılmış gibi sunulmaz.

### Yinelenen manuel düzeltme isteği — 2026-10-25
Aynı cihaz/katalog işlemi PendingApproval, Approved veya Executing durumundayken yeni manuel istek 409 ve mevcut iş ID'siyle reddedilir. Cihaz satırı transaction kilidi eşzamanlı manuel istekleri sıralar; revoked cihaz yeniden kontrol edilir. İş ve audit birlikte kaydedilir. İptal edilmiş işin yerine yeni istek verilebilir; Unknown sonucu olan cihazda yeniden onarım öncesi operatör incelemesi gerekliliği devam eder. Bu kontrol tamamlanmış işlemlere karşı kalıcı idempotency anahtarı değildir. Yeni şema/port/ajan kurulumu gerektirmez.

2026-10-25 manuel düzeltme tekrar isteği: Build.ps1 başarılı (123/123 .NET, 6 kurulum ve 12 bağlantı önkontrolü); PostgreSQL iki API host'unda yarış/iptal sonrası yeni istek dahil 2/2 senaryo geçti. Playwright frontend dizininden 5/5 geçti; tekrar tıklamada 409 açıklaması ve tek bekleyen DNS işi doğrulandı. İlk yanlış çalışma dizini fixture dosyası bulunamadı hatası üretti; doğru dizinde tam tekrar başarılı oldu. Şema değişikliği yoktur.

### Eski/belirsiz düzeltmenin otomatik tekrarını engelleme — 2026-10-25
Otomatik karar, manuel isteklerle ortak cihaz satırı kilidini aldıktan sonra güncel cihaz politikasını ve mevcut işleri kontrol eder. Aynı katalog işlemi PendingApproval/Approved/Executing/Unknown durumundaysa 24 saatten eski olsa da otomatik tekrar oluşturulmaz. Son 24 saat filtresi ve cihaz bekleme süresi korunur. Tamamlanmış veya iptal edilmiş eski iş, diğer koşullar sağlanırsa gelecekte yeni otomatik işe engel değildir. Unknown durumunda cihazın gerçek sonucunu inceleyin; bu durum otomatik başarı veya rollback değildir. Yeni şema, port veya ajan kurulum gereksinimi yoktur.

2026-10-25 otomatik tekrar koruması: Build.ps1 başarılı (129 .NET, 6 servis ve 12 bağlantı önkontrolü); PostgreSQL 2/2 geçti. Altı yeni .NET senaryosu eski aktif/belirsiz ve eski terminal iş ayrımını doğrular. İlk test fixture'ındaki EF tracked eski durum okuması AsNoTracking ile düzeltildi. Tarayıcıda ilk senaryo demo henüz başlamadığı için connection-refused aldı; sunucu başladıktan sonra yalnız başarısız senaryo yeniden çalıştırıldı ve geçti. Diğer dört senaryo ilk çalışmada başarılıydı; toplam beş senaryo doğrulandı. Gerçek kurum/OS/kapasite ve yeni güvenlik taraması kabulü açık.

### AD kabul planı — 2026-10-25
Kaynak incelemesiyle docs/ad-acceptance.md eklendi. Kısa adın UPN'e çevrilmesi (ayrı sAMAccountName araması yapılmaması), alternatif suffix/NetBIOS sınırı, nested/primary grup davranışı, grup türü zorlamasının bulunmaması ve mevcut oturumun grup değişikliğinde hemen iptal olmaması açıklandı. Gerçek AD test sonucu üretilmedi; kurum ortamı hazır olduğunda uygulanacak kabul matrisi hazırlandı. Uygulama kodu/port/şema/kurulum değişmedi.

### AD security-group tür kontrolü — 2026-10-25
Önceki incelemede belirtilen tür kontrolü eksikliği giderildi. Üye olunan eşlenmiş grubun groupType security-enabled biti yoksa veya değer eksik/bozuksa rol verilmez. İşaretli/işaretsiz 32-bit değerler doğrulanır. Yükseltmeden önce distribution gruplarını rol eşlemesinden çıkarıp uygun security gruplarını belirleyin; mevcut cookie ömrü politikası değişmedi. Gerçek AD bind/nested/izin kabulü ayrıca yapılmalıdır. Şema/port/ajan değişikliği yoktur. Teknik kaynak: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-adts/11972272-09ec-4a42-bf5e-3e99b321cf55

### Kullanıcı yerel kabulü için hazır sürüm — 2026-10-25
Yerel demo http://127.0.0.1:5080 üzerinde health=alive ve ready=ready ile doğrulandı. Son kaynak derlemesi 141/141 .NET, 6 servis ve 12 bağlantı önkontrolüyle başarılı. Son Chromium çalışması 5/5 geçti. Gerçek yerel phi4-mini:latest sentetik teşhisi completed oldu; OS onarımı çalıştırılmadı. LDAP groupType rol kontrolünün 12 yerel regresyonu bu .NET toplamına dahildir; gerçek AD testinin yerine geçmez. Açılış/yeniden başlatma ve test sırası docs/local-testing.md içinde. Mevcut .data/sırlar dağıtım paketinde yoktur.

0.4.0 ek düzeltmeler: uzaktan teşhis makine adı büyük/küçük harf ve baş/son boşluklarından bağımsız eşleşir. PowerShell yürütücüsü uzun script kaynak metni yerine kısa exception mesajı kaydeder; böylece çıktı sınırı asıl hata nedenini gizlemez. BigFix reparse/sahip/yazma izni negatif senaryoları gerçek PowerShell sürecinde sentetik servis/dosya cevaplarıyla doğrulandı.

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
