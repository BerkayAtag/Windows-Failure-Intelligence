# BigFix teşhis ve onarım

Analiz merkezinde **Genel (BigFix dahil)** veya **BigFix** seçilir. Makine kayıtlı ve ajan bağlı olmalıdır. Ajan BESClient servis durumunu ve standart `Program Files (x86)/BigFix Enterprise/BES Client/__BESData/__Global/Logs/yyyyMMdd.log` (ayrıca 64 bit Program Files) konumunda bugünkü dosyanın son 12 KB bölümünü toplar. Özel kurulum yolları otomatik keşfedilmez. Yok/erişilemeyen log, başarılı sağlık kontrolü sayılmaz. Servis sorgusu yetki hatasında otomatik onarım tetikleyecek marker üretilmez.

Sorun açıklaması sunucu modeline bağlamdır; serbest komut veya dosya yolu değildir. BF-001 yalnız ajan BESClient/BigFix kaynağının tam, izinli servis durumu metninden üretilir. Olay metnine marker yazmak BigFix otomasyonunu başlatmaz.

## Sabit onarım sırası

1. BESClient zaten Running ise yeniden kurulum yapılmaz.
2. Servis başlatılır, en fazla 30 saniye Running beklenir.
3. Başlatma başarısızsa `C:\Windows\Bigfix\Cache\reinstall.cmd` değerlendirilir. Ajanın korunan ayarında `Agent.BigFixReinstallSha256` bulunmalıdır. Dosya ve üst yollar reparse olamaz; sahip/yazma yetkileri SYSTEM, Administrators veya TrustedInstaller dışında ise işlem reddedilir. Muhafazakâr izin kontrolü, kurumda farklı servis hesabına yazma hakkı verilen yolları da reddedebilir; güvenlik ekibi izinleri incelemelidir.
4. Dosya çalışırken değiştirilememesi için paylaşım kilidi tutulur. SHA256 eşleşirse CMD `/d /c` ile sabit konumda çalışır. Kullanıcı script'in etkileşimsiz ve yeniden başlatmasız olduğunu doğrulamıştır. WFI script'in başka dosyalardan çağırdığı bağımlılıkları hash ile doğrulamaz; bunlar da kurum tarafından güvenilir ve yazmaya kapalı tutulmalıdır.
5. Çıkış kodu sıfır olmalı, BESClient 60 saniye içinde Running olmalıdır. Tüm ön kontrol/işlem/doğrulama için süre üst sınırı 900 saniyedir. Süre aşımında süreç ağacı sonlandırılır; belirsiz sonuç otomatik yeniden denenmez. Servisin devre dışı bırakılmış başlangıç tipi zorla değiştirilmez.

## Politika ve kabul

`PB-BIGFIX-REPAIR` yüksek riskli, sabit katalog işlemidir. Manuel talep yönetici onayı ister. Otomatik politika açıkken başarılı AI analizi, servis kanıtı, onaylı katalog ve 24 saatlik cihaz bekleme sınırı birlikte aranır. Hash onayı yoksa servis başlatma yine denenebilir; yeniden kurulum reddedilir. AI veya operatör metni bu sınırları kaldıramaz.

Running yalnız servisin çalıştığını doğrular; BigFix sunucusuna kayıt/iletişim veya bütün uygulama sorunlarının çözüldüğünü kanıtlamaz. BigFix REST entegrasyonu bu özellikten ayrıdır ve mevcut değildir. Gerçek BESClient, kuruma ait reinstall.cmd, bağımlılık izinleri ve BigFix yönetim bağlantısı test makinesinde kabul edilmeden filoya yaymayın. Yerel testler sentetik servis cevapları kullanır, bilgisayarınızda BigFix başlatmaz veya yeniden kurmaz.
