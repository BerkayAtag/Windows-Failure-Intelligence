# Güvenlik incelemesi ve düzeltmeler — 22 Ekim 2026

## 0.5.0 güvenlik sınırları

Bootstrap yükü EXE kaynağına gömülüdür; açılım Program Files altında yeni ve kısıtlı ACL'li dizindedir. ZIP yolları hedef dışına çıkamaz, mevcut dosya üzerine çıkarılmaz. Sessiz mod parola/token argümanı kabul etmez; token dosyası reparse, boyut, içerik, sahip ve erişim kalıtımı kontrollerinden geçer. Bootstrap ve modern kurucu yönetici yetkisi ister; UAC atlatılmaz. EXE Authenticode imzasızdır.

AI hafızası yalnız aynı domain'de başarılı ve ajan doğrulamalı işlemleri kullanır. Current evidence/rule eşleşmesi olmadan eski örnek otomatik işlem nedeni olamaz. Tarihsel model metni güvenilmeyen bağlamdır, executable değildir. Admin dışlama işlemi audit'e yazılır ve sonraki sorgularda uygulanır; başlamış inference/onarım geri alınmaz. Model ağırlıkları değiştirilmez; dış training API kullanılmaz.


## 0.4.1 kayıt ve setup kontrolleri

Yeni installer Admin kimliğiyle mevcut CSRF korumalı kayıt uç noktasını kullanır. HTTP reddedilir; HTTPS sertifika doğrulaması ve redirect reddi korunur. Kurum parolası config/komut satırına konmaz; yerel kurulum token'ı stdin üzerinden geçirilir. Setup oturum çıkışını başarı/hata yollarında dener. Aynı hostname kaydı mevcut kimliği/token'ı değiştirmez; sunucu domain transaction kilidi eşzamanlı kayıtları serileştirir. Agent token yalnız bir kez kullanılabilir.

Win-x64 runtime paketlendiği için güvenlik güncellemelerinde yeni ajan/setup paketi yayımlamak gerekir. EXE Authenticode imzasızdır. Gerçek UAC/servis kurulumu ve kurumsal LDAP kabulü test makinesinde ayrıca yapılmalıdır. Kaynak regresyonları tüm açıkların kapalı olduğunun garantisi değildir.


## 0.4.0 ek güvenlik sınırları

Sorun açıklaması 4000 karakterle sınırlandırılır, redakte edilir, modelde güvenilmeyen bağlam olarak işaretlenir; kural motoruna kanıt olarak sokulmaz. BF-001 otomatik eşleştirmesi sadece BESClient/BigFix kaynaklı, izinli tam servis durumu metnine uygulanır; olay logundaki marker otomasyonu tetiklemez. Yeniden kurulum dosyası ajan yerel hash ayarı, reparse/ACL kontrolü ve çalışma süresince salt okunur paylaşım kilidi gerektirir. Çağırdığı başka script/binary bağımlılıkları kurum tarafından ayrıca korunmalıdır. Model çıktısı komut olarak yürütülmez.

Bu değişiklikler önceki Codex Security raporunun kapsamına otomatik girmez. Yeni tarama başlangıcı önceki denemede obj/Release/net10.0/compressed altındaki bulunamayan yerel dosya nedeniyle başarısız olmuştu; yeni tam tarama tamamlandı iddiası yoktur. Yerel davranış regresyonları ayrı kanıttır.


23 Ekim geliştirme notu: Bu rapor önceki v0.2 snapshot'ına aittir; sonradan eklenen ajan/Ollama kodunu kapsamaz. Yeni incelemede domain filtreleri, sahte başarı/onay ve ajan kimlik doğrulama eksikleri bulundu. İlk düzeltmeler ve kalan kabul maddeleri agent-ollama-implementation.md içinde; yeni güvenlik kabulü henüz verilmedi.

Codex Security Standard taraması tamamlandı: `08a0b25e-bd9b-4179-84bd-f84f19b1b6a3`. Bağımsız kaynak incelemesi, mimari/güven sınırı incelemesi ve ana inceleyici doğrulaması kullanıldı. Kapsam uygulama kaynaklarıdır; vendor node_modules, derleme çıktıları, runtime verileri ve gerçek kurum ağı hariçtir. Bu bir gerçek ortama sızma testi veya tüm bağımlılık kaynak kodlarının denetimi değildir.

Oluşturulan değiştirilmeyen başlangıç raporu [security-baseline.md](security-baseline.md), tehdit modeli [threat-model.md](threat-model.md). Rapor eski kaynak snapshot'ına aittir; tarama hizmeti içerik değişimi uyarısı verdi. Aşağıdaki düzeltmeler o rapordan SONRA uygulandı. Raporun iki bulgusu düzeltilmiş kaynakta hâlâ açıkmış gibi yorumlanmamalı; plugin bulgu durumunu otomatik kapattığımız iddia edilmiyor.

| Başlangıç bulgusu | Önem | Uygulanan düzeltme | Doğrulama |
| --- | --- | --- | --- |
| İç içe XML Event üzerinde karesel/iptal edilemeyen traversal | Orta | XDocument/Descendants döngüsü kaldırıldı; tek XmlReader geçişi, 64 derinlik sınırı, nested Event reddi ve CancellationToken kontrolleri | NestedEventsAreRejected, ExcessiveXmlDepthIsRejected, CancelledInputStopsBeforeParsing, mevcut NETLOGON ve bozuk XML testleri |
| Authorization Bearer/Basic token'ının kanıtta açık kalması | Orta | Tam header değeri, tırnaklı sırlar, XML sırları maskelenir; kısaltma maskelemeden sonra | StructuredCredentialsAreMasked (5 örnek), MaskingPrecedesEvidenceTruncation |

Ek güvenlik ve hata kontrolleri: CSRF olmadan giriş reddi, anonim analiz erişimi, Viewer yazma/audit reddi, domain ayrımı, LDAP metakarakter kaçışı, yabancı/bozuk kullanıcı adı reddi, boş rol haritasında fail-closed, DTD/bozuk XML/ZIP traversal/bomb/nested ZIP sınırları, güvenlik başlıkları, giriş hız limiti, cookie replay sonrası logout iptali ve iki düğümde ortak oturum davranışı.

Bağımlılık denetiminde pnpm audit ve .NET transitive vulnerability sorguları 22 Ekim 2026 tarihinde bilinen açık bildirmedi. Bu sonuç yeni açık bulunmayacağını veya özel uygulama hatası olmadığını garanti etmez. Kesin komut/sonuç özeti verification.md içindedir.

## Üretim sınırları

- LDAPS sertifika/grup/hesap kilitleme, gerçek PostgreSQL yarışları ve LB/SMB failover kurum ortamında test edilmedi.
- Sırlar için maskeleme en iyi çaba yaklaşımıdır, tam DLP değildir. Ham loglar maskelenmeden korunan storage'da saklanır; erişim/şifreleme/saklama politikası gerekir. Önceden üretilmiş sonuçlar geriye dönük temizlenmez; eski sonuçlarda token bulunabileceğini değerlendirip kurum prosedürüyle temizleyin ve gerektiğinde ilgili token'ı döndürün.
- Login/upload limiter process başınadır; çoklu düğüm için gateway ortak kontrolü gerekir. Toplam disk kotası ve retention uygulama içinde henüz yoktur.
- AD grubundan çıkarılan aktif oturum 30 dakika sonuna kadar geçerli olabilir. Logout iptal edilir; yönetici tüm oturum iptali, MFA/SSO, OU scope, WORM audit ve antimalware servisi bu sürümde yoktur.
- Nginx örneği hazırlanmıştır; bu ortamda Nginx kurulu olmadığından `nginx -t` veya gerçek TLS/LB testi yapılmadı.

Tarama hizmetinin döndürdüğü kullanım: 5.066.712 input, 11.858 output, toplam 5.078.570 token; input içinde 4.864.640 cached token. Ölçüm 6 thread için hizmetin birikimli rollout raporudur, bu tur için faturalama tahmini değildir. Kaynak tarama kapsamı kısmidir; token ölçümünün complete olması güvenlik kapsamının eksiksiz olduğu anlamına gelmez.

Yeni protokol düzeltmeleri: enrollment atomik ve tek kullanımlı; agent heartbeat metin thumbprint kontrolü yerine cihaz ID + rastgele 256-bit bearer sır hash'i ile doğrulanır. Agent endpoint'leri cookie yetkilendirmesi kullanmaz; bu yüzden CSRF muafiyeti yalnızca /api/v1/agent dalındadır. TLS doğrulamasını kapatmak yasaktır. Lease'e bağlı kanıtlar yeniden oynatılamaz; bu önlem henüz düzeltme yürütme receipt hattına bağlanmadı. Yeni testler geçse de tam güvenlik taraması/Windows ajan kabulü henüz tamamlanmadı.

Gerçek yürütücü eklendi: sabit sürümlü katalog, ayrı precheck/action/verify, timeout, bounded output, DPAPI journal ve cihaz/lease bağlı receipt. Başarı yalnızca üç aşamanın exit code doğrulaması sonrasında bildirilir; bu cihazdaki tüm arızaların çözüldüğü anlamına gelmez. DNS cache temizleme geri alınamaz ama otomatik izin verilen düşük etkili işlemdir; DISM ve SCCM restart otomatik kataloğa açık değildir. Süresi geçen çalışmanın sonucu Unknown, tekrar dispatch yoktur. Tam yeni scan ve yönetici haklı Windows servis kabulü hâlâ bekleniyor.
## 0.3.0 güncel durum — 23 Ekim

Yukarıdaki v0.2 raporu ve ara geliştirme notları tarihseldir. Yeni ajan/Ollama uygulaması ve yerel güvenlik regresyonlarının güncel kanıtı [verification-current.md](verification-current.md) içindedir. Yeni Codex Security Standard başlatma çağrısı obj/Release/net10.0/compressed altındaki geçici dosyayı okuyamadı; geçerli yeni tarama kaydı dönmedi ve tamamlanmış scan iddiası yapılmıyor.

Ek düzeltmeler: hash'li tek kullanımlık enrollment; cookie'den ayrı cihaz doğrulama; domain scope; cihaz/lease receipt; istemci journal/disk flush; aynı cihazda seri dispatch; model komutlarının yürütülmemesi; doğru katalog/sürüm/onay; gerçek önkontrol/işlem/doğrulama; bounded log ve çıktı; eksik AI confidence reddi; NAT cihaz bazlı rate limit; kurulum ACL/target kontrolleri. Bu kontroller toplam sızma testi veya 40.000 cihaz güvenlik kabulünün yerine geçmez.

LocalSystem ajanı yetkili kod yürütür. Dağıtım paketinin bütünlüğü, korunan config/state ve işletim sistemi erişimi kurumun kontrolüdür; Authenticode/MSI imzalı kurumsal dağıtım henüz hazırlanmadı. Filo AI admission/retention sınırları pilotta eksiktir; kapasite planına bakın.


Yerel ek regresyon: önceden iptal edilmiş yürütme artık süreç başlatmaz; iptalde stdout/stderr görevleri gözlenmeden stream kapatılmaz. RunnerLifecycleTests gerçek shell çıkışı, bounded output ve geçersiz assignment reddini doğruladı. Güncel toplam 75 testtir; yeni Codex Security taramasının tamamlanmama durumu değişmedi.


### İptal/onay durum geçişleri

- Teşhis iptali ve düzeltme onayı koşullu UPDATE + audit transaction kullanır. Terminal teşhis geç gelen iptalle değişmez; tekrar onay Executing işi Approved'a geri döndüremez. Durumu değişmiş iş için 409, domain kapsamı dışında/bulunamayan iş için 404 döner.
- AI yanıtı beklerken iptal testi: geç sonuç kaydedilmez, otomatik düzeltme oluşmaz, tek iptal audit'i vardır. Eşzamanlı iki onayda tek başarılı yanıt/tek audit doğrulandı. Ölçüm SQLite TestServer kapsamındadır; PostgreSQL yarış kabulü değildir.
- Build.ps1 başarılı, 82/82 .NET testi geçti. Playwright 4/4; yüksek risk manuel isteğin önce yönetici onayı beklemesi ve onaydan sonra yalnız ajan beklemesi de eklendi. Gerçek DISM yürütülmedi.


### Politika audit bütünlüğü

Cihaz otomatik modunun audit yazımı başarısızsa değişiklik artık commit edilmez. Genel politika ve tüm cihazlara uygulama da audit ile birlikte geri alınır. İlk politika/model kaydı veritabanı upsert işlemiyle yapılır; process içi kilide dayanmaz. Bu yerel düzeltme yeni Codex Security taramasının yerine geçmez.

Ajan atama doğrulaması boş iş/lease kimliğini reddeder; yürütme atamanın son süresini aşacak şekilde yalnız katalog timeout'una dayanmaz. Sentetik bekleyen ön kontrol testi action'ın başlatılmadığını sınar. Bu kontrol gerçek işletim sistemi onarımı veya yeni Codex Security taraması değildir.

PostgreSQL 17.11 yerel kabulü: iki bağlantıyla tek cihaz düzeltme dispatch'i ve yinelenen sonuçta tek audit doğrulandı. EF migration/deploy SQL senaryoları 2/2 geçti. Bu veritabanı davranış testi güvenlik taraması veya HA/failover onayı değildir; ayrıntı postgresql-validation.md.

PostgreSQL destekli TestServer API senaryoları: Viewer ve domain sınırları, toplu politika kapsamı, audit trigger hatasında genel/cihaz politikasının atomik rollback'i doğrulandı. LDAP sentetiktir; bu sonuç yeni kaynak güvenlik taraması değildir.

PostgreSQL üzerindeki iki bağımsız API TestServer host'unda ortak cookie ve merkezi logout iptali doğrulandı. İlk host kapatıldıktan sonra ikinci host geçerli oturumla hizmet verdi. Bu yerel sentetik LDAP testi gerçek HA veya yeni güvenlik taraması onayı değildir.

İki API host'u ortak session/logout senaryosu gerçek Kestrel loopback TCP üzerinden geçti; ilk dinleyicinin kapanışı ayrıca bağımsız HTTP istemcisiyle doğrulandı. Bu test sentetik LDAP ve aynı process kullanır; TLS/proxy/production güvenlik kabulü değildir.

Derlenmiş API iki ayrı dotnet sürecinde ortak session/logout ve tek süreç sonlandırılmasıyla doğrulandı. Development/demo/loopback kullanılan yerel kontrol gerçek AD, Production sertifika koruması veya yeni kaynak güvenlik taraması değildir.

Yerel YARP test projesinin transitive dahil NuGet açık sorgusunda mevcut kaynaklar bilinen açık bildirmedi. Bu bir kaynak güvenlik taraması değildir. Proxy senaryosu iki ayrı API sürecinde oturum ve süreç kaybı sonrası yönlendirmeyi kapsar; TLS/kurum LB güvenliği doğrulanmadı.

Manuel teşhis oluşturma cihaz satırı transaction kilidi altında aktif iş kontrolü ve audit ile yapılır. Aktif iş varken 409 döner; revoke edilmiş cihaz kilit aşamasında yeniden kontrol edilir. Bu kontrol filo çapında admission kotası değildir.

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

0.4.0 ek düzeltmeler: uzaktan teşhis makine adı büyük/küçük harf ve baş/son boşluklarından bağımsız eşleşir. PowerShell yürütücüsü uzun script kaynak metni yerine kısa exception mesajı kaydeder; böylece çıktı sınırı asıl hata nedenini gizlemez. BigFix reparse/sahip/yazma izni negatif senaryoları gerçek PowerShell sürecinde sentetik servis/dosya cevaplarıyla doğrulandı.

26 Ekim 2026 Codex Security denemesi: src kapsamındaki standart taramanın `workbench_db.py start-prompt-only-scan` başlangıç komutu başarısız oldu; scanId oluşmadı. Plugin yönergesi nedeniyle alternatif/replacement tarama başlatılmadı. 160 test ve proje bazlı NuGet + frontend audit sonuçları verification-current.md içinde ayrı kaydedildi.

0.5.0 son regresyon: 177 .NET, 9 Playwright, 2 PostgreSQL kontrolü geçti. Eksik deneyim şeması readiness 503 üretir. Gerçek kurulum/AD/HA ve tam güvenlik taraması açık kabul maddeleridir; ayrıntı verification-current.md içindedir.

## 0.6.0-preview.2 — ajan ve yayın hazırlığı (27 Ekim 2026)

Ajan çalışma zamanı ve PowerShell kurucusu yalnız HTTPS origin kabul eder; URL içinde kullanıcı bilgisi, path, query veya fragment reddedilir. HTTP yanıtı 1 MiB, kalıcı ajan durumu 128 KiB ile sınırlıdır. Başka lease'e ait veya işsiz receipt içeren bozuk günlük korunarak reddedilir; otomatik kimlik sıfırlama yapılmaz. 401 ve 503 eski protokole dönüş tetiklemez; 429 Retry-After korunur. Onarım cevabı kaybolduğunda işlem tekrar yürütülmez.

Denetim kayıtlarına Domain sütunu ve indeks eklendi. API artık yalnız oturumun domain'indeki kayıtları döndürür. Eski domain'i belirsiz kayıtlar silinmez, web listesinden gizlenir; DBA tarafından ayrıca incelenebilir. AuditDomainScope EF migration ve deploy/schema.sql birlikte güncellendi. Üretimde migration başlangıçta otomatik çalışmaz.

WFI-Server-Setup.exe yönetici yükseltmesi isteyen tek dosyalı, etkileşimli sunucu kurucusudur. Yükünü Program Files altında admin/SYSTEM korumalı geçici dizine açar; yerel yapılandırma dosyası, hostname, TLS sertifikası ve kurulum yolu ister. IIS + .NET 10 Hosting Bundle ve hazırlanmış PostgreSQL önkoşuldur. Veritabanı ve Ollama otomatik kurulmaz. HA/upgrade manuel bakım adımlarını kullanır. Otomasyon için Install-Server.ps1 kullanılabilir. Setup EXE'leri henüz Authenticode imzalı değildir.

Yeni port gerekmiyor. Ajan → WFI HTTPS 443; sunucu → LDAPS/DB/Ollama ve koşullu BigFix/OTLP mevcut firewall tablosuna göre sınırlandırılır. GitHub Pages statik tanıtımdır, canlı kurum konsolu veya log barındırmaz. Public kaynak örneğinde domain example.invalid olarak temizlenir; yerel kurum checkout varsayılanı korunur.
