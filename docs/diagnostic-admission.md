
## Domain teşhis kuyruğu sınırı
`Diagnostics:MaxActivePerDomain` (ortam değişkeni `Diagnostics__MaxActivePerDomain`) varsayılan 1000, izin verilen aralık 1–10000'dır. Tüm API düğümlerinde aynı değeri kullanın. Pending, running, evidence_received ve analyzing işleri birlikte sayılır. Manuel başlatma ve AI yeniden analiz dolu kuyrukta HTTP 429 döndürür; otomatik tarama heartbeat'i kesmeden 60–119 saniye sonrasına ertelenir (gerçek yeniden deneme ajan polling zamanına bağlıdır). PostgreSQL transaction advisory lock aynı domain'deki kabul kararını düğümler arasında sıralar. İptal/tamamlanma yer açar; limit düşürmek mevcut işleri silmez.

Bu sınır kuyruk büyümesini kontrol eder; model işlem kapasitesi, domainler arası adalet, log yükleme kotası veya otomatik retention sağlamaz. 40.000 aktif istemci kapasitesi ölçülmüş değildir. Önce küçük pilotta kuyruk yaşı, 429 oranı ve model gecikmesini izleyin.

### AI yeniden analiz çakışması düzeltmesi
Eski teşhisin yeniden analizi, aynı cihazda pending/running/evidence_received/analyzing iş varsa 409 ile reddedilir. Kontrol domain transaction kilidi içinde manuel/otomatik kabul ile sıralanır. Kabul edilen yeniden analiz eski CompletedAt değerini temizler. Şema, port ve ajan kurulum gereksinimleri değişmedi.
