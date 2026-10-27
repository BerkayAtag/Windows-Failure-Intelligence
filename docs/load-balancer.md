# Load balancer / çoklu düğüm

API düğümleri aynı uygulama sürümü, PostgreSQL veritabanı, DataProtection key ring ve raw storage kullanmalıdır. Sticky session zorunlu değildir. Yerel testte iki ayrı ASP.NET host arasında cookie kabulü ve diğer düğümde logout iptali doğrulandı; bu, gerçek LB/PKI/SMB kesinti testinin yerine geçmez.

`HighAvailability.Enabled=true` yapın. `DataProtection.Path` ve `Storage.Path` tüm API/worker'ların aynı veriye eriştiği mutlak UNC yolları olmalıdır. Aynı yol adı altında farklı yerel disk kullanmayın. `DataProtection.CertificateThumbprint` için tüm API düğümlerinde aynı özel anahtarlı sertifikayı kurun; servis kimliğine özel anahtar okuma izni verin. Anahtar koruması yeni key dosyalarını şifreler; mevcut eski düz metin key dosyalarını geriye dönük şifrelemez. Mevcut key'leri güvenli bakım/rotasyon planıyla taşıyın, mevcut oturumların etkisini hesaba katın.

`ReverseProxy.KnownProxies` içine yalnızca uygulamanın gerçekten gördüğü son hop LB IP'lerini yazın. ForwardLimit=1; çok hop'lu topolojide son hop başlıkları normalize etmelidir. IPv4-mapped IPv6 adresi görünüyorsa gerçek remote adres biçimini kullanın. Genel `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` ayarını kullanmayın. Boş liste halinde özel forwarding middleware'i devreye girmez.

LB HTTPS sonlandırır, backend HTTPS'i de doğrular. İstemciden gelen X-Forwarded-For/Proto'yu silip tek doğru değerle yazar. `AllowedHosts` gerçek WFI FQDN'idir; Host korunur. API düğümüne yalnızca LB/yönetim ağı erişir. Sağlık kontrolü HTTPS `/api/v1/ready` için 200 bekler; yönlendirme 30x başarı sayılmaz. Endpoint DB sorgular; disk, LDAPS veya worker hazır olduğunu kanıtlamaz.

`deploy/nginx.example.conf` tek gateway örneğidir; F5/HAProxy/IIS ARR karşılığını ağ ekibi uygulayabilir. Uygulama limiter'ları process başınadır. Gateway'de ortak IP/hesap bazlı login kötüye kullanım kontrolü, bağlantı ve toplam upload sınırı gerekir. Birden fazla LB kendi bağımsız limitini uygularsa global limit oluşmaz; cluster destekli WAF/gateway kullanın. AD hesap kilitleme politikasını değiştirmeden kurumla uyumlu eşikleri belirleyin.

API worker'ını HA'da kapatıp ayrı worker süreçleri kullanın. Mevcut kuyruk atomik koşullu UPDATE ve lease token ile sahiplenilir; birden fazla worker güvenlik kontrolüne sahiptir, ancak yoğun PostgreSQL lock/throughput performansı henüz ölçülmedi. DB connection pool üst sınırı tüm API+worker süreçlerinin toplamıyla PostgreSQL max_connections altında tutulmalıdır. Rastgele worker sayısı artırmak kapasite kanıtı değildir.

Kabul sırası:

1. Aynı cookie/CSRF oturumuyla A ve B API düğümlerine sırayla gidin; login/upload/detail çalışmalı.
2. A'dan logout yapın; B eski cookie'yi 401 ile reddetmeli.
3. İşi alan worker'ı durdurun; 5 dakikalık lease sonrasında başka worker işi tamamlamalı, tek terminal audit oluşmalı.
4. LB backend'ini devreden çıkarın; yeni istek sağlıklı düğüme geçmeli. Upload sırasında kopma için retry/dedup davranışını doğrulayın.
5. DB/storage/CA kesintisinde hata ve alarm oluşmalı; veri kaybı, salt okunur dosya ve yetki hataları gözlenmeli.
6. Temsili loglarla yükü kademeli artırıp p95/p99, hata oranı, kuyruk yaşı, disk ve DB bağlantı sayısını kaydedin.

Kaynak: [Microsoft proxy yapılandırması](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
## 0.3.0 ajan ve AI iş paylaşımı

Ajan kimlik hash'leri, politikalar, model tercihi, teşhis ve düzeltme işleri ortak DB'dedir. Ajan istekleri cookie/sticky session gerektirmez. Her API aynı katalog ve sürümde olmalıdır. Agent kimlik doğrulaması rate limiter'dan önce yapılır; cihaz başına process-local 100 istek/dakika limit vardır. Enrollment IP başına aynı limiti kullanır; büyük dağıtımı kademelendirin.

Teşhis lease'i 10 dakikadır. Düzeltme lease'i komut timeout'u + 300 saniyedir; süresi biten yürütme Unknown olur, başka düğüm otomatik tekrar çalıştırmaz. Cihaz satırı transaction kilidi aynı cihazdaki dispatch'leri sıralar. Gerçek PostgreSQL eşzamanlılık/failover ölçümü henüz yoktur.

Ayrı worker hem log analizini hem AI teşhis kuyruğunu işler. Her worker Ollama'ya bir inference açar; worker sayısını GPU/VRAM kapasitesine göre sınırlayın. Çok worker aynı tek GPU'yu hızlandırmaz. DB, paylaşılan storage ve model servisi kendi HA tasarımına ihtiyaç duyar; API LB bunları otomatik yedekli yapmaz.


Model ve domain politikası ilk kaydı parametreli veritabanı upsert kullanır; birden fazla API düğümünün aynı anahtarı oluşturması için process içi kilit gerekmez. Eşzamanlı yöneticilerde son başarılı yazma geçerlidir; arayüzde eski ayarı düzenlemeye karşı sürüm/ETag kontrolü yoktur. PostgreSQL çok düğümlü kabul testi henüz yapılmadı.

Yerel PostgreSQL 17.11 kabulünde iki bağımsız DB bağlantısıyla aynı cihazda tek dispatch doğrulandı (2/2 kurulum senaryosu). Bu, iki API düğümü/proxy veya failover testi değildir. Ayrıntı: postgresql-validation.md.

PostgreSQL kabulünde iki bağımsız OllamaService örneğinin model tercihi aynı DB üzerinde doğrulandı; ilk eşzamanlı kayıt ve sonraki tercihin diğer örnekten görünürlüğü geçti. Bunun kapsamı servis/DB sınırıdır; iki HTTP API düğümü veya proxy failover testi değildir.

### İki yerel API host'unda oturum kanıtı

PostgreSQL kabul senaryolarında ortak DataProtection dizinini kullanan iki bağımsız TestServer host'u aynı session cookie'sini kabul etti. İlk host logout'u sonrası diğer host cookie'yi reddetti; yeni oturum oluşturulup ilk host kapatılınca ikinci host okumaları sürdürdü. İki ayrı process/sunucu, proxy failover, TLS veya Production sertifika koruması denenmedi.

Oturum testi artık iki farklı loopback TCP portunda gerçek Kestrel kullanır. Bağımsız HttpClient ilk host health erişimini ve host kapandıktan sonra bağlantı reddini doğrular; ikinci host geçerli session ile okumaları sürdürür. Host'lar aynı test process'indedir; proxy yönlendirmesi veya process çökmesi test edilmez.

### Ayrı süreç sınırı

Derlenmiş API iki ayrı dotnet sürecinde aynı yerel PostgreSQL/key diziniyle doğrulandı. Ortak cookie ve merkezi logout çalıştı; ilk süreç sonlandırılınca ikinci süreç auth/me/automation okumalarını sürdürdü. İlk süreç testinde istemci ikinci adresi doğrudan kullandı; sonraki YARP testi tek proxy adresinden geçişi de kapsar. Development/demo ve tek bilgisayar kullanıldığı için Production sertifika/AD, proxy otomatik geçişi veya DB failover kabulü sayılmaz.

Yerel YARP test proxy'si üzerinden iki gerçek API sürecine dağıtım ve süreç kaybı sonrası kalan API'ye geçiş doğrulandı. Sağlık tespiti sırasında 502/503 olabilir; test kalan backend'de 10 ardışık başarı ve logout'u doğrular. Üretim proxy ürünü, TLS, proxy HA ve DB failover ayrı kabul kapılarıdır. Ayrıntı postgresql-validation.md.


### Teşhis kabul sınırı — 2026-10-24
Domain başına aktif teşhis sınırı eklendi (varsayılan 1000). Manuel/yeniden analiz dolu kuyrukta 429, otomatik tarama gecikmeli yeniden deneme kullanır. Tüm API düğümlerinde aynı Diagnostics__MaxActivePerDomain değeri gerekir. Yeni port veya istemci kurulumu değişikliği yoktur; mevcut HTTPS ve API–PostgreSQL bağlantıları kullanılır. Ayrıntılar: [Teşhis kabul sınırı](diagnostic-admission.md). Gerçek AD/üretim HA ve 40.000 aktif istemci kabulü henüz yapılmadı.
