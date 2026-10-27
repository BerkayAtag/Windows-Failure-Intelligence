# On maddelik uygulama durumu — 26 Ekim 2026

Bu liste teslim edildi iddiası değil, kaynak kodunun güncel durumudur.

| Madde | Uygulanan | Açık işler / kabul |
| --- | --- | --- |
| 1 Kapasite | Birleşik check-in, eski ajan uyumu, geri çekilme, DB model slotu/fencing, öncelik/yaşlandırma, OTLP ve Sistem Sağlığı | k6 idle senaryosu hazır; reconnect/model kesintisi yük profilleri ve 40k ölçümü eksik |
| 2 Kampanyalar | Sabit cihaz listesi, pilot, bakım aralığı, eşzamanlı sınır, ayrı genişletme onayı, duraklatma/iptal | Cihaz grubu seçimi madde 10'a bağlı; kurumsal HA kabulü açık |
| 3 BigFix merkezi | Salt okunur connector, doğrulanmış ID/DNS eşleme, öncesi/sonrası rapor, API kesintisini ayrı gösterme | Gerçek BigFix endpoint/TLS/hesap doğrulaması yapılmadı |
| 4 AI kalite | 40 sentetik varyant/80 çağrı karşılaştırması, sürüm kaydı, geri bildirim ve negatif deneyim filtresi | Gerçek model karşılaştırması koşulmadı; model digest ve daha geniş vaka çeşitliliği eksik |
| 5 İmzalı sürüm | Önceki tek EXE/pilot dağıtımı mevcut | Authenticode yayın kapısı, imzalı metadata/antirollback, SBOM ve sürüm uygunluğu eksik |
| 6 Ağ testi | Statik Ağ Erişimleri tablosu güncel | Ajan kaynaklı DNS/TCP/TLS/HTTP test akışı eksik |
| 7 Snapshot | Mevcut teşhis collector'ları mevcut | Sürümlü paket/snapshot fark zaman çizelgesi eksik |
| 8 Olay yaşam döngüsü | Mevcut incident altyapısı mevcut | İstenen yaşam döngüsü/ilişkilendirme genişletmesi eksik |
| 9 Saklama | Mevcut saklama davranışı korunuyor | Önizlemeli, kilitli, batch retention yönetimi eksik |
| 10 Cihaz kapsamı | Domain ve rol sınırları mevcut | OU/grup kapsam servisi ve tüm endpoint regresyonları eksik |

Filo geneline otomasyon için 1, 2 ve 10 tamamlanmalıdır. Yeni özelliklerin işletim ve geçiş adımları [operations-0.6.md](operations-0.6.md) içindedir. Gerçek AD, Windows kurulum/yükseltme, kurumsal BigFix, imza sertifikası ve HA/yük kabulü ortam hazır olduğunda yapılır. Yerel sentetik testler bu kabulleri kapatmaz. Test sonuçları [verification-current.md](verification-current.md) içinde tutulur.
