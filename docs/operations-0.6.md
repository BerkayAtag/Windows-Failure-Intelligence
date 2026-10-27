# 0.6 geliştirme: işletim ve kabul notları

Bu geliştirme henüz on maddelik yol haritasının tamamı değildir. Üretim dağıtımı için kalan güvenlik ve kapasite kabul kapıları `implementation-roadmap.md` içinde izlenir.

## Sunucu geçişi

Veritabanını yedekleyin. Eski API ve worker süreçlerini durdurun; eski worker model slotu ve kampanya kısıtlarını tanımadığı için sürümler karışık çalıştırılmamalı. `deploy/schema.sql` idempotent PostgreSQL betiğini yetkili dağıtım hesabıyla uygulayın. Uygulama başlangıcı üretimde migration çalıştırmaz. Readiness yeni tabloları ve analiz sürüm/kampanya sütunlarını da kontrol eder. API ve worker aynı veritabanını ve aynı `Ollama:MaxConcurrent` değerini kullanmalıdır (varsayılan 1).

## BigFix merkezi doğrulama

API ve worker sunucularının korunan yerel yapılandırmasına `BigFix:BaseUrl`, `AllowedOrigins`, `Username`, `Password` ekleyin. Örnek hedef biçimi `https://bigfix.example.invalid:52311/`; gerçek portu BigFix yöneticinizden alın. AllowedOrigins aynı origin'i içermelidir. Boş ayarlar connector'ı kapatır. Hesap salt okunur olmalı; parola istemciye veya depoya konmaz. Kurum CA zinciri Windows tarafından güvenilir olmalıdır; sertifika doğrulaması devre dışı bırakılamaz. Redirect kabul edilmez.

Admin Uzak Konsol'da cihazı seçer, BigFix Computer ID değerini girip kimliği onaylar. Sunucu DNS adını cihazın FQDN'iyle karşılaştırır; yalnız hostname benzerliği eşleme yapmaz. Merkez raporunu Analyst veya Admin sorgulayabilir. Bağlantılar ekranındaki “Yapılandırıldı” bir bağlantı testi değildir.

Eşlenmiş cihazın BigFix onarımı öncesinde merkez raporu alınır. API kullanılamazsa yerel onarım devam edebilir fakat merkez doğrulaması yapılmış sayılmaz. Onarım başladıktan sonra alınan veri öncesi kanıtı olarak kullanılamaz. Yerel başarıdan sonra yeni merkez raporu 15 dakika sınırıyla beklenir. API kesintisi cihaz arızası değildir. Bu connector BigFix action başlatmaz.

## Kampanyalar

Admin açık cihaz listesini, onaylı katalog işlemini ve saat dilimli zaman aralığını seçer. Önizlemede çevrimdışı ve politika engelli cihazlar gösterilir; oluşturulan hedef listesi sabittir. Pilot en fazla 5, eşzamanlı çalışma en fazla 2 cihazdır. Başlatma ve pilot sonrası genişletme ayrı onaydır. Başarısız/belirsiz sonuç kampanyayı duraklatır. İptal ve süre sonu yalnız başlamamış işleri engeller; çalışan işletim sistemi işlemini öldürmez. Genel rollback yoktur. Grup seçimi henüz yoktur.

## AI Kalite Laboratuvarı

Admin yerel kurulu modeli seçerek sentetik karşılaştırma başlatır: 8 senaryo ailesinin 40 varyantı geçmiş açık/kapalı toplam 80 çağrıda değerlendirilir. Üretim teşhisi önceliklidir ve aynı model slot bütçesi paylaşılır. Laboratuvar yalnız loopback Ollama adresini kabul eder. Model veya prompt otomatik etkinleştirilmez. Aktif iş iptal edilirse geç gelen sonuç kayıt edilmez.

Sonuçlar pay/payda ve veri seti/prompt sürümüyle gösterilir. Atıf kontrolü kaynak kimliğini doğrular; kök neden doğruluğunu kanıtlamaz. Model etiketi kaydedilir ancak immutable model digest henüz kaydedilmez. İnsan geri bildirimi yerel servis doğrulamasından ayrıdır; “çözülmedi/geçici düzeldi” geçmiş örneğin yeni analizlerde kullanılmasını engeller, otomasyon yetkisi vermez.

## Ağ ve istemci

Yeni istemci inbound portu yoktur; ajan sunucuya mevcut HTTPS bağlantısını kullanır. Yeni ajan birleşik check-in kullanır, eski sunucunun 404 yanıtında eski protokole döner; eski ajan uç noktaları korunur. Boşta sorgulama 300 saniyeye kadar seyrekleşebilir. Kesinti geri çekilmesi jitter öncesi 3600 saniye üst sınırına sahiptir.

BigFix REST trafiği yalnız API/worker → izinli BigFix HTTPS origin yönündedir; port gerçek connector ayarındaki porttur. İsteğe bağlı OpenTelemetry için API/worker → kurum OTLP collector gRPC portu (genellikle 4317) açılır. `Telemetry:OtlpEndpoint` boşsa dışa aktarım yoktur. Model portu 11434 yalnız yerel/izinli sunucu sınırında tutulur; laboratuvar loopback zorunludur.

Gerçek AD, kurumsal BigFix, Windows kurulum/yükseltme, fiziksel HA ve 40 bin cihaz yükü bu yerel kontrollerle doğrulanmış değildir.
