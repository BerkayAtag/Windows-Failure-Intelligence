# 40.000 cihaz kapasite hedefi

40.000 yönetilen cihaz, 40.000 eşzamanlı web kullanıcısı ve 40.000 aynı anda yükleme farklı iş yükleridir. v0.2 ölçümü yalnız manuel log yükleme kapsamındadır; 0.3.0 ajan hesabı aşağıdadır. Bu nedenle filo genelinde otomatik veri toplama veya 40.000 eşzamanlı istemci desteği doğrulanmış değildir.

Yerel ölçüm: 40.000 sentetik analiz satırı, satır başına 2 KiB sonuç, 60 ardışık HTTP TestServer liste isteği, tek kullanıcı, SQLite. İlk ve son sayfa dönüşü 25 satırla sınırlı, domain filtresi korunuyor, büyük ResultJson listede taşınmıyor. Ölçüm `capacity-results.json` dosyasındadır. Testin p95 değeri ağ, TLS, PostgreSQL veya gerçek LDAP gecikmesini içermez ve üretim SLO'su olarak kullanılamaz.

Tekrar çalıştırma: `WFI_CAPACITY_REPORT` environment değişkenine raporun mutlak yolunu verip `dotnet test WFI.slnx -c Release --filter FortyThousandRowsRemainPagedAndDomainScoped` çalıştırın. Test kendi geçici SQLite verisini oluşturur, üretim DB'sine bağlanmaz.

Boyutlandırmada günlük log sayısı × ortalama ham boyut × saklama günü hesabını kullanın. Örneğin cihaz başına günde bir 2 MiB dosya, 40.000 cihaz ve 30 gün yaklaşık 2,29 TiB ham alan gerektirir; DB sonuçları, indeksler, yedekler ve replikasyon ayrıca eklenir. Sıkıştırma oranına göre kapasite garantisi vermeyin. 100 operatörün 15 saniyelik liste yenilemesi yaklaşık 6,7 istek/saniye üretir; durum sorguları ayrıca hesaplanır.

## Üretim öncesi ölçüm

1. Önce gerçek LDAPS, PostgreSQL ve ortak storage üzerinde 50–200 cihazlık pilot.
2. Gerçekçi dosya boyutu/format dağılımını sentetik örneklerle temsil edin; küçük loglardan çıkan sonucu 20 MiB XML/ZIP'e genellemeyin.
3. Kuyruğa geliş hızı, ortalama işlem süresi ve worker sayısını ölçün. Kuyruk sürekli büyüyorsa kapasite yetersizdir.
4. Test ortamında 1.000 → 5.000 → 10.000 → 40.000 cihazlık kayıt/iş dalgaları; jitter, kesinti sonrası retry ve tekrar dosya senaryoları.
5. En az 1 saat kararlı yük ve 24 saat soak; API p95/p99, 429/5xx, CPU/RAM, connection pool, WAL/IOPS, boş disk ve en yaşlı pending iş kaydedilsin.
6. Önerilen kabul hedeflerini kurum SLA'sıyla onaylayın: liste p95 < 1 saniye, beklenmeyen 5xx < %0,1, kayıp/çift terminal sonuç 0; analiz kuyruk hedefi gerçek log boyutuna göre belirlensin. Bunlar sağlandığı iddia edilen ölçümler değildir.

`scripts/load/read-workload.js` k6 ile HTTPS test ortamında liste/sağlık yükü üretir. Geçerli test oturumu gerekir; gerçek kullanıcı parolasını script'e yazmaz. Testi üretim yerine ayrılmış kabul ortamında çalıştırın. Bu script upload throughput, AD login ve otomatik collector testi değildir. Oturum 30 dakika olduğundan kısa koşu kullanır; sonuçta 401 alırsa süreyi/giriş durumunu düzeltin, hatayı başarılı saymayın.

Örnek: `k6 run -e WFI_BASE_URL=https://wfi-test.example -e WFI_SESSION=<test-cookie-value> scripts/load/read-workload.js`. Cookie bir sırdır; güvenli environment/secret injection kullanın, shell geçmişine gerçek değer yazmayın.

Toplam upload kotası, otomatik retention, gerçek filo collector kapasitesi, gerçek PostgreSQL çok worker yarış testi ve kurumsal failover kabulü açık işlerdir. Ölçümler tamamlanmadan 40.000 cihaz için üretim onayı verilmemelidir.
## 0.3.0 ajan için ek kapasite hesabı

Ajan artık vardır; yukarıdaki v0.2 satır testi hâlâ gerçek filo yükü değildir. Varsayılan 30 saniye + jitter döngüsünde boşta heartbeat, teşhis claim ve düzeltme claim olmak üzere yaklaşık 3 HTTP isteği oluşur. 40.000 cihaz için jitter hariç yaklaşık 4.000 istek/saniye; ayrıca her istek kimlik DB sorgusu yapar. PollSeconds 10–600 arasında ayarlanabilir. Örneğin 300 saniye yaklaşık 400 istek/saniyeye indirir, işin alınma gecikmesini artırır. Bunlar hesaplamadır, ölçülmüş throughput değildir.

Tüm cihazlarda otomatik 15 dakika teşhis yaklaşık 44,4 yeni AI işi/saniye üretir. Modelin ortalama işlem süresi T saniyeyse gerekli eşzamanlı inference yaklaşık 44,4 × T'dir; T=30 saniye varsayımında yaklaşık 1.334 slot gerekir. Tek phi4-mini/Ollama süreci bu hedef için kapasite garantisi değildir. Domain başına aktif teşhis kotası vardır (diagnostic-admission.md); global model scheduler ve otomatik retention yoktur. Toplu otomasyon yalnız kabul edilmiş küçük grupta kullanılmalıdır.

Kabul ortamında kayıt/enrollment, heartbeat/claim, model kesintisi, reconnect dalgası, timeout/replay ve 24 saat soak ölçün. GPU RAM, token/s, kuyruk yaşı ve DB büyümesini raporlayın. 40.000 satır testi ile 40.000 canlı ajan testi birbirinin yerine geçmez.



### Cihaz listesi fonksiyonel regresyonu

40.000 sentetik cihaz, aynı heartbeat zamanı ve cihaz başına 1 KB envanter yüküyle SQLite TestServer'da sınandı. 1, 2, 400 ve 800. sayfalar 50 kayıt döndürdü; tekrar okuma aynı sıralamayı korudu. Yabancı domain kaydı görünmedi; her yanıt 32 KB altında kaldı ve ayrıntılı envanter/kimlik alanlarını içermedi. Bu test eşzamanlı ajan, ağ veya PostgreSQL performans ölçümü değildir.

Domain/LastHeartbeat/Id indeksi liste sıralamasını destekler; gerçek PostgreSQL sorgu planı ve gecikmesi ayrıca ölçülmelidir. OFFSET sayfalama canlı heartbeat güncellemeleri arasında snapshot garantisi vermez: cihazlar sayfa değiştirebilir. İkincil Id sıralaması yalnız eşit zaman damgalarındaki belirsizliği giderir.

Manuel teşhis istekleri cihazda aktif iş varken reddedilir; aynı cihazda tekrarlanan tıklamaların kuyruğu büyütmesi engellenir. Heartbeat mevcut cihaz satırını günceller, her pulse için Heartbeats geçmiş satırı eklemez. Domain teşhis kabul sınırı ayrıca uygulanır; bu kontroller retention veya 40.000 aktif ajan kapasite kanıtı değildir.



### PostgreSQL büyük envanter doğrulaması — 2026-10-25
Kabul paketine 40.000 sentetik cihaz içeren PostgreSQL envanter senaryosu eklendi. Aynı heartbeat zamanında kimlikle kararlı sıralama, ilk/orta/son sayfalar, 50 kayıt sınırı, domain ayrımı, arama normalizasyonu ve listeden büyük/özel alanların çıkarılması sınanır. EF migration ve schema.sql yollarının ikisinde de çalışır. Tek kullanıcı ve TestServer API çağrıları kullanır; 40.000 aktif ajan, eşzamanlı trafik, ağ/TLS/LDAP veya üretim kapasitesi ölçümü değildir. Ürün davranışı, şema, kurulum ve firewall gereksinimleri değişmedi.
