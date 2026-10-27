# Mimari — 0.3.0 pilot

WFI.Core modeller, sürümlü kurallar, ZIP/XML/metin parser, Ollama istemci motoru ve sabit düzeltme kataloğunu taşır. WFI.Infrastructure EF Core, LDAPS, kuyruklar ve koordinatörleri içerir. WFI.Api cookie/CSRF/RBAC, HTTP API ve React arayüzünü sunar. WFI.Worker ayrı log/AI worker host'udur. WFI.Agent Windows servisidir.

## Güven sınırları

Tarayıcı AD parolasıyla yapılandırılmış LDAPS DC'sinde doğrulanır; iç içe grup üyeliği roller üretir. Oturum domain claim'i veri kapsamını belirler; kullanıcı gövdesindeki domain yetki kaynağı değildir. Oturum 30 dakika, HttpOnly; logout ortak DB'de iptal edilir. Gerçek AD grupları/CA kurumda test edilmelidir.

Ajan farklı kimlik sınırıdır: tek kullanımlık kayıt token'ı, cihazın ürettiği credential hash'i, her istekte ID+bearer doğrulama. Cookie ajan yetkisi sağlamaz. CSRF muafiyeti sadece /api/v1/agent dalındadır. Ajan endpoint'leri HTTPS kullanır. Başarılı cihaz kimlik doğrulamasından sonra rate limiter cihaz ID'sine göre ayrılır.

Kanıt güvenilmeyen içeriktir. XML DTD/XXE kapalı; streaming traversal/depth/nested Event kontrolü; ZIP traversal/expansion/count sınırları; regex timeout ve satır sınırı vardır. Kanıt maskelenir; React metin olarak gösterir. Ham manuel upload webroot dışındadır. Maskeleme tam DLP değildir.

AI yalnız hipotez üretir. Worker önerileri kural eşleşmesi ve derlenmiş kataloğa indirger. Ajan hiçbir model/DB serbest script'ini yürütmez. LocalSystem yalnız sabit izinli katalog komutlarını çalıştırır; bu yüzden API rolü, cihaz kimliği ve servis dosya ACL'si kritiktir.

## Kalıcı durum ve HA

Jobs/Audit'e Devices, DiagnosticJobs, AiAnalysisJobs, AiConfigs, AutomationPolicies, Playbooks ve RemediationExecutions eklendi. Heartbeats/Incidents şemada eski taslaklardan bulunur; tam incident korelasyonu uygulanmış sayılmaz. Gerçek kimlik AD objectGUID'ine bağlanmaz; hostname yeniden kullanımında yetkili bakım gerekir.

Upload idempotency domain+hostname+SHA256+motor sürümüdür. Diagnostic lease token'ı stale worker sonucunu engeller. Remediation transaction'ı cihaz satırında dispatch'i sıralar; süre aşımı Unknown olur ve tekrar çalıştırılmaz. Ajan DPAPI journal'ı, disk flush ve file lock kullanır.

API düğümleri ortak PostgreSQL/key ring/raw storage kullanır. KnownProxies dışında forwarded başlıklarına güvenilmez. Ayrı worker aynı DB ve raw storage'a erişir; model servisiyle HTTPS veya loopback HTTP kullanır. Model tercihi ortak DB'dedir. Sticky session gerekmez; gerçek failover/40.000 ajan kapasitesi ölçülmedi.

Güncel akış ve hata davranışı agent-ollama-implementation.md; işletim topolojisi load-balancer.md ve server-installation.md içindedir.

