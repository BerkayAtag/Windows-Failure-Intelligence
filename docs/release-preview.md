## WFI 0.7.0 — Windows Failure Intelligence

Windows uç noktalarında kanıta dayalı teşhis, kurum içi yapay zekâ (Ollama) ve onaylı katalogla kontrollü onarım.

![WFI analiz merkezi](https://raw.githubusercontent.com/BerkayAtag/Windows-Failure-Intelligence/main/site/assets/console.png)

### İndirme
- **WFI-Server-Setup.exe**: Windows Server için tıkla-kur sihirbazı. Yönetici olarak çalıştırın → **İleri → Kur → Bitir**.
- **SHA256SUMS.txt**: dosya bütünlüğü (yayıncı doğrulaması değildir).
- İstemci ajanı ayrı release dosyası değildir: kurulumdan sonra konsolda **Ajan ve Ağ Erişimleri → Windows ajanını indir**.

### Bu sürümde
- Kurulum sihirbazı yenilendi: Türkçe karakterler, adım göstergesi, ilerleme çubuğu, **Konsolu aç** bağlantısı.
- **Ollama seçeneği:** makinede yoksa WFI kurar (varsayılan); 127.0.0.1:11434'te çalışan bir Ollama varsa onu kullanabilir; ya da AI'yı sonraya bırakabilirsiniz.
- IIS, .NET 10 Hosting Bundle, VC++ çalışma zamanı, PostgreSQL, HTTPS sertifikası, güvenlik duvarı kuralı ve şema otomatik hazırlanır. Yeniden denemede ikinci sertifika oluşturulmaz.
- Kurulum ve kaldırma betikleri kendi süreçleri için `-ExecutionPolicy Bypass` ile başlar; Windows'un varsayılan *Restricted* politikası artık kurulumu durdurmaz.
- Programlar ve Özellikler: **WFI Server / WFI Agent**, yayıncı **Berkay Atağ**, sürüm 0.7.0, yeni kırmızı WFI simgesi (tırnaksız `DisplayIcon` yolu), kurulum tarihi, boyut ve destek bağlantısı.
- İstemci ajanı kurulumu için **PowerShell 7 gerekmez**; Windows PowerShell 5.1 yeterli.
- İptal edilmiş makine veya süresi dolmuş kullanılmamış kayıt aynı cihaz kimliğiyle yeniden kaydedilebilir; cihaz iptalinde bekleyen teşhisler kapatılır ve başlamamış onarımlar iptal edilir.
- Güvenlik: başka domain'e ait onarım kaydı artık 403 yerine 404 döndürür (varlık sızdırmaz); olay detayındaki AI özeti domain ile sınırlandı; manuel onarım isteğinde makine adı doğrulanır ve normalize edilir.
- Sunucu konsolu, kurulum pencereleri ve tanıtım sayfasında altbilgi: **© 2026 Berkay Atağ · Windows Failure Intelligence (WFI)**.
- Tanıtım sayfası yenilendi: açık/karanlık tema düğmesi, temaya göre değişen güncel ekran görüntüleri.

### Bilinen sınırlar
EXE'ler Authenticode imzalı değildir; SmartScreen uyarısı görülebilir. İlk kurulum internetten birkaç GB indirir. Gerçek Windows Server VM'de uçtan uca kurulum/kaldırma, gerçek AD/BigFix, HA ve 40 bin cihaz kapasite kabulü bu sürümde ölçülmedi; pilot ortamda doğrulayın. Ayrıntı: [doğrulama](https://github.com/BerkayAtag/Windows-Failure-Intelligence/blob/main/docs/verification-current.md).

Tanıtım: https://berkayatag.github.io/Windows-Failure-Intelligence/

© 2026 Berkay Atağ · Windows Failure Intelligence (WFI)
