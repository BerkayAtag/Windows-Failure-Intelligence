# WFI kabul kapsamı ve açık işler

Bu belge tamamlanma onayı değildir. Kullanıcı hedefleri mevcut kod/test kanıtlarıyla eşleştirilir; yerel kanıt kurum kabulü yerine kullanılmaz. Güncel sayısal sonuçlar verification-current.md içindedir.

| Gereksinim | Mevcut uygulama ve kanıt | Eksik kabul / sonraki doğrulama |
| --- | --- | --- |
| Sunucuda Ollama, phi4-mini ve değiştirilebilir model | OllamaService, OllamaEngine, RemoteConsole; gerçek yerel phi4-mini sentetik uçtan uca denemesi; SQLite ve PostgreSQL model seçimi regresyonları | Model/GPU kesintisi ve kurum yükünde gecikme/kapasite |
| İstemcide ajan, uzaktan kanıt toplama | AgentService, WindowsEvidenceCollector, kayıt/kimlik/lease API'leri; sentetik AgentCycle ve DPAPI yeniden başlatma testleri | Ayrı Windows istemcisinde LocalSystem servis kurulumu, gerçek olay/SCCM logları ve proxy/CA kabulü |
| Teşhis ve çözüm önerisi | Kanıt → kural → Ollama → sınırlı katalog önerisi hattı; DiagnosticPipelineTests | Kurumun gerçek arıza örnekleriyle doğruluk, yanlış öneri ve eksik kanıt değerlendirmesi |
| Kullanıcının seçtiği manuel düzeltme | Katalog önkontrol/işlem/doğrulama, yüksek risk yönetici onayı; API/ajan ve Playwright senaryoları | İzole Windows test makinesinde gerçek düzeltme ve başarısızlık sonrası operasyonel müdahale |
| Cihaz ve tüm domain için otomatik mod | Varsayılan kapalı, cihaz istisnası ve tümüne uygulama; politika/audit rollback ve teşhis kuyruğu regresyonları | Kanıtla otomatik eşleşen düzeltmeler DNS flush ve onaylı BigFix onarımıyla sınırlı. Diğer sorunlara otomatik çözüm varmış gibi kabul edilmez. Gerçek pilot ve filo yük sınırları gerekir |
| LDAP ile portal girişi, değiştirilebilir domain | LdapAuthenticator ve LdapOptions; varsayılan example.invalid, rol/domain sınır testleri | Gerçek LDAPS CA/FQDN/Base DN/grup eşlemesi ve kilitli/disabled kullanıcı kabulü; kullanıcı ortamın hazır olmadığını bildirdi |
| Modern standart/açık/karanlık ve mobil arayüz | Tema, giriş/çıkış, cihaz, politika, teşhis iptali ve manuel onay Playwright senaryoları | Kurum tarayıcı politikası ve erişilebilirlik kullanıcı kabulü |
| Load balancer | Ortak DB/key/storage tasarımı; PostgreSQL bağımsız bağlantı dispatch, bağımsız servis model kaydı ve iki Kestrel host ve iki ayrı dotnet süreci; yerel YARP dağıtım/geçiş, oturum/logout testleri | Production API düğümleri, ortak DP key sertifikası/storage, gerçek proxy otomatik geçişi ve DB failover kabulü |
| 40.000 istemci | SQLite ve PostgreSQL 40.000 cihaz fonksiyonel sayfalama/domain ayrımı; kapasite dokümanı | Gerçek ajan/heartbeat/kanıt/inference yükü, p95/p99, kuyruk büyümesi; domain teşhis kabul sınırı mevcut; filo geneli model scheduler ve retention tamamlanmadı |
| Güvenlik ve bug fix | Davranış regresyonları, önceki kaynak incelemesi, sınırlı gerçek PostgreSQL transaction testleri | Yeni Codex Security taraması geçici obj dosyası okuma hatası nedeniyle tamamlanmadı. Eski v0.2 raporu yeni kodun onayı değildir |
| Kurulum, firewall, uygulama ve release belgeleri | README, history-release, server-installation, client-installation, firewall-requirements, agent-ollama-implementation | Kurumun gerçek DNS, sertifika, servis hesabı ve kaynak boyutlarıyla son dağıtım planı |

PostgreSQL destekli TestServer API isteklerinde politika/yetki/audit sınırları doğrulandı (sentetik LDAP). İki Kestrel host ve iki ayrı dotnet sürecinde loopback TCP oturum/logout testi de geçti; yerel YARP üzerinden yönlendirme/geçiş de doğrulandı. Sıradaki doğrulama üretim TLS/kurum proxy koşulları ve yük kabul altyapısıdır. Gerçek kurum AD/Windows kabulü ortam hazır olduğunda yapılır; yerel küçük testler bu kapıları otomatik kapatmaz.






### Teşhis kabul sınırı — 2026-10-24
Domain başına aktif teşhis sınırı eklendi (varsayılan 1000). Manuel/yeniden analiz dolu kuyrukta 429, otomatik tarama gecikmeli yeniden deneme kullanır. Tüm API düğümlerinde aynı Diagnostics__MaxActivePerDomain değeri gerekir. Yeni port veya istemci kurulumu değişikliği yoktur; mevcut HTTPS ve API–PostgreSQL bağlantıları kullanılır. Ayrıntılar: [Teşhis kabul sınırı](diagnostic-admission.md). Gerçek AD/üretim HA ve 40.000 aktif istemci kabulü henüz yapılmadı.



## 0.4.0 ek kabul kapıları

Sorun açıklamasıyla ajan teşhisi, BigFix kapsamı, Admin ajan indirmesi ve tema seçenekleri yerel regresyona eklendi. Gerçek Windows Server üzerinde Setup-Server.cmd/IIS/sertifika ve gerçek BESClient/reinstall.cmd kabulü ortam hazır olunca yapılmalıdır. Bu aşamada kurumsal AD/HA/40.000 cihaz yük testi tamamlandı sayılmaz.

0.4.1: pencereli setup derlendi/gözlemlendi; otomatik hostname kaydı, Admin/CSRF sınırları, token tüketimi, eşzamanlı kayıt ve envanter yenilemesi regresyonları geçti. Gerçek UAC/Windows Service ve LDAPS kurulum kabulü bekliyor.

## 0.5.0 kabul ekleri

- Tek dağıtım EXE'si ve Admin konsol indirmesi mevcut; gerçek Windows istemcide UAC, sessiz yeni kurulum/yükseltme ve kaldırma kabulü açık. EXE Authenticode imzasızdır.
- Ajan ve Ağ Erişimleri ekranındaki tablo statik gereksinimdir; kurum firewall erişimi ölçülmüş değildir.
- Deneyim hafızası aynı domain/kapsam/güncel kanıt filtreleri, dışlama/audit ve modele bağlam iletimiyle regresyon kapsamındadır. Model yeniden eğitilmez; kurum olaylarında iyileşme oranı ölçülmemiştir.
- VerifiedExperienceMemory migration/schema.sql birlikte uygulanır; eksik kolon readiness 503 üretir.