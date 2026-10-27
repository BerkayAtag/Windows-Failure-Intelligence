# Ajan ve Ollama uygulama notları — 0.3.0 pilot

## 0.4.0 güncellemesi

ProblemDescription operatör bağlamıdır; EvidenceJson yalnız ajan kanıtıdır. Genel ve BigFixScan BESClient durumunu ve standart günlük kuyruğunu toplar. PB-BIGFIX-REPAIR katalog 4.0.0, otomatik politika ve doğrulanmış servis bulgusuyla seçilebilir. Yeniden kurulum yerel onaylı hash gerektirir. AI sunucuda kalır; istemcide yalnız sabit katalog yürütücüsü vardır. Ayrıntılar: bigfix.md.


23 Ekim 2026. Bu belge önceki geliştirme notlarının yerini alır.

## Çalışan akış

Admin, Cihazlar ve AI ekranında hostname kaydeder. Bir saatlik tek kullanımlık token gösterilir; sunucuda hash tutulur. Windows servisi kendi 256-bit credential'ını DPAPI ile saklar, kayıt token'ıyla eşleştirir. Sonraki istekler cihaz ID + bearer credential kullanır. TLS doğrulaması kapatılmaz; yönlendirme izlenmez. Bekleyen cihazın kayıp/süresi dolmuş token'ı yenilenebilir; aktif kimlik sessizce değiştirilemez.

Operatör teşhis ister. Ajan işi alır, sınırlı System olaylarını/SCCM loglarını/DNS ve disk bilgisini toplar. Kanıt maskelenip lease ile gönderilir. Worker kalıcı kuyruktan kanıtı alır; yerel kurallar ve seçili Ollama modeli değerlendirilir. Sonuç hipotez ve katalog önerisidir. Ollama hatası başarısız analizdir; sağlıklı cihaz veya doğrulanmış kök neden sayılmaz.

Gerçek yerel phi4-mini:latest ile sentetik kanıt → API → worker → Ollama → kayıtlı sonuç akışı 23 Ekim'de tamamlandı. Tekrarı: scripts/Test-OllamaPipeline.ps1 (yalnız loopback demo). Test cihazının otomatik modu kapalıydı ve sonunda kimliği iptal edildi; gerçek onarım yapılmadı. Model güven skoru modelin kendi çıktısıdır, kalibre edilmiş doğruluk ölçümü değildir.

## Manuel ve otomatik düzeltme

Model çıktısı komut olarak yürütülmez. Sunucu ve ajan aynı derlenmiş 3.0.0 kataloğunu kullanır:

| Kod | İşlem | Risk | Otomatik izin |
| --- | --- | --- | --- |
| PB-DNS-FLUSH | DNS istemci önbelleğini temizle | Düşük | Yalnız AD-001 ve açık politika ile |
| PB-WU-START | wuauserv/bits servislerini başlat | Düşük | Hayır; manuel |
| PB-SCCM-RESTART | CcmExec yeniden başlat | Orta | Hayır; manuel |
| PB-DISM-REPAIR | DISM RestoreHealth/CheckHealth | Yüksek | Hayır; Admin isteği/onayı |

Analyst/Admin manuel işlem ister. Yüksek risk Admin gerektirir. Onay işi Approved yapar; başarı yazmaz. Ajan önkontrol, işlem ve doğrulamayı ayrı yürütür. Herhangi biri hata verirse Success yazılmaz. DNS doğrulaması tüm domain arızasının giderildiğini kanıtlamaz. Otomatik geri alma yoktur.

Genel otomatik mod varsayılan kapalıdır. Cihaz Inherit/Enabled/Disabled seçebilir. Tüm cihazlara uygula yalnız oturumun domain'indeki istisnaları Inherit yapar. Açık cihaz heartbeat sırasında en erken 15 dakikada bir teşhis kuyruğuna alınır. Başarılı AI analizi ve kural eşleşmesi sonrası otomatik izinli işlem üretilebilir; cihaz başına 24 saat bekleme vardır. Dispatch sırasında politika tekrar okunur. Modu kapatmak başlamış işlemi durdurmaz.

## Kesinti davranışı

- Teşhis işleri 10 dakikalık lease ile yeniden alınabilir; eski lease kanıtı reddedilir.
- Düzeltme cihaz satırında transaction ile sırayla dispatch edilir; aynı cihazda başka Executing varken yeni iş verilmez.
- Ajan işlemden önce DPAPI journal'ı diske flush eder. Tek state dosyası için tek süreç kilidi vardır.
- Süresi geçmiş yürütme Unknown olur; otomatik tekrar çalıştırılmaz. Crash sonrası belirsiz işlem yeniden yürütülmeden başarısız receipt gönderilir. Kayıp HTTP receipt tekrar gönderilebilir.
- PowerShell süre aşımında sonlandırılır. İşlemin başlattığı dış servislerin durması veya geri alınması garanti edilmez; Unknown sonucu cihazda kontrol edin.

## Düzeltilen sorunlar

Eksik AiConfig primary key/LDAP DI; kayıp status endpoint'i; domain filtresi olmayan API'ler; onayda sahte Success; metin thumbprint ile kimlik taklidi; TLS bypass; boş kanıtla AI; model tercihinin sıfırlanması; erişilemeyen Ollama'da uydurma model/güven/sağlıklı sonuç; büyüyen logun sınırsız okunması; NAT'taki ajanların ortak hız limitine takılması; mobil grid taşması düzeltildi.

## Açık kabul sınırları

Gerçek example.invalid LDAPS/CA/grup kabulü, yönetici haklı Windows servis kurulumu ve SCCM/DISM onarımı, PostgreSQL yarış/failover ve 40.000 cihaz soak ölçümü yapılmadı. Yeni Codex Security taraması derleme çıktısı okuma hatasıyla başlatılamadı; eski v0.2 raporu bu kaynağı kapsamaz. Yerel regresyon ve bağımlılık sorguları harici sızma testi değildir. Geniş filoda otomatik modu açmadan capacity.md kabulünü tamamlayın.


## Yeniden başlatma regresyonu

Yeni AgentService/AgentStateStore örneği yalnız diskteki DPAPI journal'dan açılarak sınandı. Sunucu commit sonrası kayıp HTTP response senaryosunda üç komut aşaması toplam bir kez çalıştı, receipt tekrarında audit çoğalmadı. İşlem sonucu olmayan journal senaryosunda hiçbir komut yürütülmedi. Bu testler collector/komut adapter'ında sentetik davranış kullanır; gerçek OS onarımı değildir.


Model keşfi bozuk JSON/timeout durumunu kullanılabilir model yok olarak ele alır; kaydedilmiş model tercihi korunur. Null/boş/aşırı uzun model adları seçime girmez. Kullanıcı iptali timeout gibi yutulmaz. Erişilemeyen loopback model hedefiyle envanterin kullanılabilir kaldığı tarayıcıda doğrulandı.


Teşhis iptali aktif durumlarda atomiktir. İnference zaten sürüyorsa HTTP çağrısı hemen durdurulmayabilir; geç sonuç ve bundan üretilecek otomatik düzeltme kaydedilmez. İptal, daha önce tamamlanmış teşhisin ürettiği ayrı düzeltme işini iptal etmez. Düzeltme onayı tek geçiştir; yinelenen/geç gelen onay 409 döner ve işi tekrar çalışmaya açmaz.


Model tercihi tek veritabanı upsert işlemiyle saklanır; kurulu model doğrulaması başarısızsa önceki tercih korunur. Genel politika/tüm cihazlara uygulama ve cihaz politikası audit ile transaction içinde kaydedilir. Eşzamanlı geçerli ayar yazmalarında son başarılı yazma geçerlidir.

### Portalda teşhis iptali

Cihazı açın; Son teşhisler altında aktif işin Teşhisi iptal et düğmesini kullanın. Analyst ve Admin görebilir. İptal API'si başarı döndürdükten sonra durum/hata listesi yenilenir; terminal işte düğme kaldırılır. İptal açıklaması gösterilir. Mevcut model/kanıt toplama süreci hemen kesilmeyebilir; geç sonuç işlenmez. Ayrı düzeltme işlerinin iptali veya geri alınması değildir.

### İstemci yürütme son süresi

Ajan, katalog timeout'u ve atamanın kalan süresinden kısa olanını uygular. Ön kontrol/işlem/doğrulama aşamaları iptal token'ını kontrol eder. Boş iş veya lease kimliği reddedilir. Süre dolumu başarısız/belirsiz sonuç üretir; otomatik yeniden yürütme yoktur. Daha önce gerçekleşmiş OS değişiklikleri otomatik geri alınmaz.

### Yinelenen manuel teşhis

Aynı cihazda aktif pending/running/evidence_received/analyzing teşhis varken yeni manuel teşhis 409 ile reddedilir. Önce işin tamamlanmasını bekleyin veya Son teşhisler bölümünden iptal edin. Cihaz satırı kilidi eşzamanlı manuel ve otomatik zamanlama işlemlerini sıralar. Bu kontrol eski mükerrer kayıtları silmez ve filo çapında AI admission/retention kotası yerine geçmez.

### Teşhis kabul sınırı — 2026-10-24
Domain başına aktif teşhis sınırı eklendi (varsayılan 1000). Manuel/yeniden analiz dolu kuyrukta 429, otomatik tarama gecikmeli yeniden deneme kullanır. Tüm API düğümlerinde aynı Diagnostics__MaxActivePerDomain değeri gerekir. Yeni port veya istemci kurulumu değişikliği yoktur; mevcut HTTPS ve API–PostgreSQL bağlantıları kullanılır. Ayrıntılar: [Teşhis kabul sınırı](diagnostic-admission.md). Gerçek AD/üretim HA ve 40.000 aktif istemci kabulü henüz yapılmadı.

### Ajan kanıt teslimi sınır testleri
Sentetik regresyonlar; süresi dolan lease, iptal edilmiş teşhis ve başka cihazın lease'iyle kanıt gönderimini kapsar. Bu talepler 409 ile reddedilmeli, kanıt ve audit kaydı oluşturmamalıdır. Süresi dolan iş yeniden atandığında eski lease geçersiz kalır; yeni lease ile teslim tek audit üretir. Uygulama davranışı, şema ve ağ/kurulum gereksinimleri değiştirilmedi.

### Düzeltme kuyruğunda bekleme hatası — 2026-10-25
İlk 10 bekleyen iş politika veya katalog nedeniyle uygun olmadığında sonraki geçerli manuel iş seçilemiyordu. Uygunluk filtresi artık sorgunun limitinden önce uygulanır; sıralama zaman ve iş kimliğiyle kararlıdır. Kapalı otomatik işler çalıştırılmaz ve silinmez; politika tekrar açıldığında uygun olabilir. Şema, firewall portları ve ajan kurulum adımları değişmedi.

### AI değerlendirmesinde kanıt ve belirsizlik — 2026-10-25
Teşhis sonucu ekranı modelin bildirdiği güveni, eksik kanıtları, hipotez kanıt atıflarını ve önerilen doğrulama adımlarını gösterir. Güven değeri ölçülmüş doğruluk/başarı olasılığı değildir; eksik veya geçersiz değer için yüzde uydurulmaz. Boş eksik-kanıt listesi tamlık garantisi olarak gösterilmez. Model metni düz React metni olarak görüntülenir; doğrulama adımları otomatik komut olarak çalıştırılmaz. Yeni endpoint, port, şema veya ajan ayarı yoktur.

### Başlamamış düzeltmeyi iptal etme — 2026-10-25
Analyst/Admin kendi domain'indeki Approved veya PendingApproval düzeltme isteğini Düzeltme geçmişi → Bekleyen düzeltmeyi iptal et ile Cancelled durumuna alabilir. Viewer ve başka domain erişimi reddedilir. API: POST /api/v1/remediations/{id}/cancel; CSRF gerekir. Koşullu durum güncellemesi ve audit aynı transaction'dadır; iptal edilmiş iş ajana verilmez ve yeniden onaylanamaz. Ajan işi önce almışsa 409 döner; Executing/Unknown/Success/Failed durumları değişmez. İptal işletim sistemi rollback'i değildir. Otomatik işi iptal etmek otomatik politikayı kapatmaz veya 24 saatlik beklemeyi sıfırlamaz; ileride uygun yeni iş oluşabilir. Kalıcı durdurma için cihaz/genel otomatik modu kapatın. Yeni port, şema veya ajan kurulumu değişikliği yoktur.

### Yinelenen manuel düzeltme isteği — 2026-10-25
Aynı cihaz/katalog işlemi PendingApproval, Approved veya Executing durumundayken yeni manuel istek 409 ve mevcut iş ID'siyle reddedilir. Cihaz satırı transaction kilidi eşzamanlı manuel istekleri sıralar; revoked cihaz yeniden kontrol edilir. İş ve audit birlikte kaydedilir. İptal edilmiş işin yerine yeni istek verilebilir; Unknown sonucu olan cihazda yeniden onarım öncesi operatör incelemesi gerekliliği devam eder. Bu kontrol tamamlanmış işlemlere karşı kalıcı idempotency anahtarı değildir. Yeni şema/port/ajan kurulumu gerektirmez.

### Eski/belirsiz düzeltmenin otomatik tekrarını engelleme — 2026-10-25
Otomatik karar, manuel isteklerle ortak cihaz satırı kilidini aldıktan sonra güncel cihaz politikasını ve mevcut işleri kontrol eder. Aynı katalog işlemi PendingApproval/Approved/Executing/Unknown durumundaysa 24 saatten eski olsa da otomatik tekrar oluşturulmaz. Son 24 saat filtresi ve cihaz bekleme süresi korunur. Tamamlanmış veya iptal edilmiş eski iş, diğer koşullar sağlanırsa gelecekte yeni otomatik işe engel değildir. Unknown durumunda cihazın gerçek sonucunu inceleyin; bu durum otomatik başarı veya rollback değildir. Yeni şema, port veya ajan kurulum gereksinimi yoktur.
