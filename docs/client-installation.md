# 0.7.1 — 30 Ekim 2026

- Ajan kurulum penceresi Windows temanızı izler; **Karanlık tema / Aydınlık tema** düğmesiyle değiştirilebilir.
- **Kaldırdıktan sonra yeniden kurma / yükseltme:** Kaldırma, `C:\Program Files\WFI Agent\appsettings.local.json` ve `C:\ProgramData\WFI\agent-state.bin` kimliğini korur. WFI-Agent-Setup.exe bu kimliği bulursa **Mevcut kimlikle kur / yükselt** düğmesini gösterir; Admin parolası ve yeni kayıt gerekmez, cihaz konsolda aynı kimlikle görünür. Kimlik dosyası yoksa veya okunamıyorsa pencere yeniden kayıt adımlarına yönlendirir.
- Makineyi başka bir kimlikle kaydetmek için konsolda **Cihazlar ve AI → Cihazı aç → Cihazı iptal et** seçin; ardından setup normal kayıt akışıyla aynı makine adını yeniden kaydeder.

# 0.7.0 — 30 Ekim 2026 (güncel istemci kurulumu)

- **Ek ön koşul yok.** Ajan kurucusu artık Windows 10/11 ve Windows Server ile gelen **Windows PowerShell 5.1** ile çalışır; PowerShell 7 gerekmez. Ajanın .NET 10 çalışma zamanı EXE'nin içindedir. Ajan servisi zaten Windows PowerShell 5.1 kullanıyordu; değişen yalnız kurulum adımıdır.
- **Yeniden kayıt:** Konsolda **iptal edilmiş (revoked)** bir makine veya süresi dolmuş ve hiç kullanılmamış bekleyen kayıt, aynı makine adıyla setup çalıştırıldığında **aynı cihaz kimliğiyle** yeni tek kullanımlık token alır. Online/kayıtlı bir ajan ve son bir saat içinde açılmış bekleyen kayıt sessizce değiştirilmez (409). Eski ajan kimlik bilgisi iptalden sonra geçersiz kalır.
- **İptal temizliği:** Cihaz iptal edildiğinde bekleyen/çalışan teşhisleri "Cihaz iptal edildi" ile kapanır ve başlamamış onarımlar iptal edilir; böylece domain teşhis kuyruğu kapasitesi boşa tutulmaz.
- Setup penceresi sunucu adresini makinenin DNS domain'ine göre önerir (`https://wfi.<domain>`); konsoldaki sessiz kurulum örneği konsolun gerçek HTTPS adresini gösterir.
- Programlar ve Özellikler: **WFI Agent**, yayıncı **Berkay Atağ**, sürüm 0.7.0 ve kırmızı WFI simgesi. İstemci EXE'si GitHub release dosyası değildir; kurum konsolunda **Ajan ve Ağ Erişimleri → Windows ajanını indir** ile alınır.

Aşağıdaki eski bölümlerde geçen "PowerShell 7 gerekir" ifadeleri 0.7.0 öncesi sürümler içindir.

# 0.6.0-preview.6 — 29 Ekim 2026

Güncel kurulum sürümü **preview.6**. Önceki preview.4/5 kurucularını kullanmayın: Windows PowerShell 5.1'in standart girdi kodlaması SQL aktarımını bozabiliyordu. Kurucu artık SQL'i UTF-8 baytlarıyla gönderir. Gerçek PostgreSQL'de PowerShell 5.1 üzerinden ilk/tekrarlı rol-veritabanı hazırlığı, tam şema ve Türkçe metin roundtrip testi geçti. Kalıcı regresyon: `scripts/Test-ServerSqlTransport.ps1`; `Test-Postgres.ps1` bunu da çalıştırır. İnternet gereksinimi ve açık Windows VM kabul kapısı değişmedi.

# 0.6.0-preview.5 — 29 Ekim 2026

Otomatik kurulum ve yerel yönetici özelliklerini, UTF-8/DNS düzeltmelerini içerir. Yayın kontrolü artık sürüm etiketi ile derlenen kaynak commit'inin aynı olmasını zorunlu tutar; başka kaynak sürümünün dosyaları mevcut sürüm üzerine yüklenmez. Güncel indirme preview.5'tir. Windows VM kabulü hâlâ açıktır.

# İstemci ve operatör kurulumu — 0.5.0 pilot

## Sunucu kurulumuyla uyum — 0.6.0-preview.4

Yeni sunucu sihirbazı ilk yönetici hesabını `local:admin` olarak oluşturur; LDAP sonradan yapılandırılabilir. Ajan EXE'sini oturum açılan **Ajan ve Ağ Erişimleri** menüsünden indirin. Sunucunun otomatik yerel sertifikası istemcide kendiliğinden güvenilir olmaz: kurum CA/GPO güven dağıtımını tamamlayın, HTTPS denetimini kapatmayın. İstemci için yeni inbound port gerekmez; sunucuya TCP/443 çıkışı yeterlidir. Sunucu bağımlılık kurulumu istemcinin PowerShell gereksinimini değiştirmez. İstemci sessiz kurulum parametreleri aşağıdaki gibidir.


## 0.5.0 — tek EXE ile kurulum

Konsolda **Ajan ve Ağ Erişimleri → Ajan kurulumu → Windows ajanını indir** seçin. Artık ZIP açmanız gerekmez; indirilen WFI-Agent-Setup.exe tek dağıtım dosyasıdır. Windows ile gelen PowerShell 5.1 ve .NET Framework 4.8 yeterlidir (0.7.0 öncesi sürümler PowerShell 7 x64 istiyordu). Kurucu yönetici yetkisiyle açılır; kendi yükünü Program Files altında rastgele adlı, SYSTEM/Administrators dışında erişilemeyen dizine çıkarır ve işlem sonrasında temizler. Ajan .NET 10 runtime'ı dahildir. EXE imzasızdır; kurumsal dağıtımda Authenticode imzalama gerekir.

Pencereli kurulumda HTTPS adresi ve WFI Admin hesabı girilir; makine kendiliğinden kaydolur. Kurulan servis Program Files/WFI Agent altındaki dosyaları kullanır; kurulumdan sonra bu dosyaları tek başına taşımayın.

### Sessiz kurulum (yükseltilmiş PowerShell / SYSTEM dağıtım hesabı)

Önce Cihazlar ve AI menüsünde hedef hostname kaydı oluşturun; DeviceId ve tek kullanımlık token alın. Token'ı yalnız SYSTEM/Administrators ve gerekiyorsa çalıştıran yetkili hesap okuyabilecek, kalıtımı kapalı **yerel** dosyaya güvenli dağıtım aracınızla yazın. Token içeriğini komut satırına, loga veya pakete koymayın.

```powershell
$p = Start-Process -FilePath '.\WFI-Agent-Setup.exe' -ArgumentList '--silent --server https://wfi.example.invalid --device-id GERCEK-CIHAZ-GUID --token-file C:\Secure\wfi-token.txt' -Wait -PassThru
$p.ExitCode
```

Adres/GUID örneklerini değiştirin. Boşluk içeren argüman yollarını çift tırnakla çevreleyin. Sessiz mod UAC'yi atlamaz: SCCM/BigFix/GPO görevini yetkili hesapla çalıştırın. İlk enrollment sonrası token sunucuda tüketilir; dağıtım sisteminiz artık gerekmeyen token dosyasını kurum prosedürüyle kaldırmalıdır.

Yükseltme aynı kimlikle yapılır:

```powershell
.\WFI-Agent-Setup.exe --silent --upgrade --server https://wfi.example.invalid --device-id GERCEK-CIHAZ-GUID
```

--upgrade mevcut ajan ister; yeni kurulumda verilmez. Aynı DeviceId/server ve DPAPI kimliği korunur. `--bigfix-sha256 ONAYLI-64-HEX` isteğe bağlıdır; verilmezse yükseltmede mevcut BigFix onayı korunur. Yanlış/yinelenen parametre reddedilir. --password ve --token parametreleri yoktur.

| Kod | Anlam |
| --- | --- |
| 0 | Yerel kurulum tamamlandı; konsolda heartbeat ayrıca kontrol edilir |
| 2 | Geçersiz/eksik parametre |
| 3 | Token içeriği, reparse veya erişim izni reddi |
| 4 | Yeni kurulum/yükseltme durumu uyuşmuyor |
| 10 | Paket açma, dosya okuma, ön koşul veya kurulum hatası |

Başarısız işlemde pending kayıt kalabilir; tekrar kayıtla kimlik devralınmaz. Token yenileme ve mevcut kayıtla kurtarma prosedürü aşağıdadır. Gerçek Windows servis kurulum kabulü test istemcisinde yapılmalıdır.


## Pencereli kurulum adımları

1. Ek ön koşul yoktur (0.7.0+: Windows PowerShell 5.1 yeterli). WFI HTTPS adresinin sertifika zinciri güvenilir olmalı. Ajan ve setup gerekli .NET runtime'ını içerir.
2. WFI konsoluna Admin ile giriş yapın. **Ajan ve Ağ Erişimleri → Ajan kurulumu** sekmesinden tek EXE dosyasını indirin.
3. `WFI-Agent-Setup.exe` çalıştırın. Windows yönetici yetkisi ister. HTTPS sunucu kök adresini ve **WFI Admin** hesabını yazıp **Kur ve konsola kaydet** seçin. Windows yerel yönetici olmak tek başına WFI kayıt yetkisi vermez.
4. Makine adı otomatik alınır; domain sunucuda doğrulanan oturumdan atanır. Kurulum öncesi konsolda elle cihaz oluşturmayın. Ajan `C:\Program Files\WFI Agent` konumuna, Windows servisi olarak kurulur. AI sunucuda kalır.
5. Konsolun açık Cihazlar ve AI ekranı yaklaşık 10 saniyede yenilenir. Kayıt önce pending, ilk güvenli ajan bağlantısı sonrası online görünür. İlk bağlantı için yaklaşık 30–60 saniye bekleyin; filtre/sayfalama yeni satırı gizliyorsa hostname ile arayın.

Parola dosyaya/argümana yazılmaz; setup çıkışta parola alanını temizler. Kayıt token'ı yalnız korunan ajan ayarında tutulur ve sunucu ilk kullanımda tüketir. Setup TLS doğrulamasını atlamaz ve HTTP yönlendirmelerini takip etmez. ZIP sır veya kuruma ait parola içermez. EXE şu anda Authenticode imzasızdır; kurum dağıtımında imzalayıp doğrulayın.

### Başarısız kurulum ve yükseltme

Setup sunucuda kaydı açıp yerel kurulumda başarısız olursa pending satırı kalabilir; başarı gibi online gösterilmez. Aynı hostname tekrar kayıtla devralınmaz. Admin konsolundan bekleyen kaydın token'ını yenileyin, DeviceId alın ve aşağıdaki Install-Agent.ps1 prosedürüyle devam edin. Kurulu ajan için mevcut DeviceId, sunucu, DPAPI durumu ve servis hesabını koruyan Install-Agent.ps1 yükseltmesini kullanın. Pencereli setup mevcut kurulum üzerinde kimlik değiştirmez. Gerçek servis kurulum/yeniden başlatma kabulü test Windows istemcisinde yapılmalıdır.


## 0.4.0 konsoldan indirme ve BigFix

Admin ile **Cihazlar ve AI → Ajan paketini indir** seçin. ZIP'i açın; istemcide .NET 10 Runtime ve PowerShell 7 kurulu olmalıdır. Konsolda cihazı kaydedip DeviceId ve tek kullanımlık token alın. Yönetici PowerShell 7 terminalinde açılmış paketten:

```powershell
./Install-Agent.ps1 -PackagePath . -ServerUrl https://wfi.example.invalid -DeviceId '<KONSOLDAKI-GUID>'
```

Token gizli istemde girilir; komut satırına yazılmaz. Sunucudaki domain/HTTPS adresi değiştirilebilir. Ajan kimliği DPAPI ile saklanır; mevcut ajan yükseltmesinde kimlik ve durum korunur. İstemciye AI modeli kurulmaz.

BigFix son çare kurulumunu etkinleştirmek için kurumun güvenilir reinstall.cmd dosyasını ve çağırdığı bağımlılıkları inceleyin. Dosyanın SHA256 değerini güvenilir kopyadan alın; `-BigFixReinstallSha256 '<64-HEX-SHA256>'` parametresini kurulum/yükseltmeye ekleyin. Hash yoksa BESClient başlatma çalışabilir ama reinstall engellenir. Bu değer parola değildir. Script etkileşimsiz ve yeniden başlatmasız olmalıdır; kullanıcı bu koşulları doğrulamıştır. Dosya güncellenirse onaylı hash de yönetici tarafından güncellenir. [BigFix akışı](bigfix.md).


## Operatör bilgisayarı

Kurum destekli Edge/Chrome, WFI DNS çözümlemesi, HTTPS 443 ve kurum CA güveni gerekir. Operatöre .NET/Node/PostgreSQL kurulmaz. Kullanıcı uygun WFI AD grubunda olmalıdır. https://wfi.example.invalid üzerinde kısa kullanıcı adı veya kullanici@example.invalid ile giriş yapar; EXAMPLE\\kullanici biçimi desteklenmez. Domain sunucu ayarından gelir. Standart/Açık/Karanlık tema tercihi tarayıcıda saklanır.

## Windows ajanını tek pilot cihaza kurun

1. Hedef Windows x64 cihazında kurumun desteklediği .NET 10 Runtime ve kurulum için PowerShell 7 hazırlayın. Runtime uyumluluğunu işletim sistemi sürümünüzle doğrulayın. Derleme makinesinde scripts/Build.ps1 çalıştırılmış olmalı; artifacts/agent paketini hedefe güvenli dağıtın.
2. Cihazın WFI FQDN'ini çözebildiğini, TCP 443'e eriştiğini ve sertifika CA'sına güvendiğini doğrulayın. Sertifika doğrulamasını kapatmayın. Ajan için inbound port gerekmez.
3. WFI'ye Admin olarak girin. Cihazlar ve AI → Yeni cihaz hostname → Cihaz kaydet. Cihaz ID ve tek kullanımlık token'ı güvenli alın. Token bir saatte sona erer. Kaybolursa henüz kayıt olmamış cihazı açıp Kayıt token'ını yenile seçin; eski token iptal olur.
4. Hedefte Windows PowerShell'i (0.7.0+; eski sürümlerde PowerShell 7) yönetici olarak açın:

```powershell
.\scripts\Install-Agent.ps1 -PackagePath 'C:\Staging\WFI\agent' -ServerUrl 'https://wfi.example.invalid' -DeviceId 'KAYIT-EKRANINDAKI-GUID'
```

Token gizli istemde alınır; komut satırına/parola dağıtım listesine yazmayın. Sunucu URL'si HTTPS olmalıdır. LDAP kullanıcı parolası ajan ayarına yazılmaz.

5. Betik **WFI Agent** Windows servisini LocalSystem olarak kurar ve başlatır. Varsayılan uygulama klasörü C:\Program Files\WFI Agent; durum dosyası C:\ProgramData\WFI\agent-state.bin. Kurulum klasörüne sadece SYSTEM/Administrators izin verilir. Başka ürünün klasörünü InstallPath olarak kullanmayın.
6. Get-Service -Name 'WFI Agent' ile Running durumunu; WFI envanterinde son bağlantıyı doğrulayın. Tek kullanımlık token tüketildikten sonra appsettings.local.json içindeki EnrollmentToken değerini boşaltabilirsiniz; kalıcı credential DPAPI state içindedir. Dosyaları diğer cihazlara klonlamayın.
7. Cihazı açın → Uzaktan teşhis başlat. Sonuç hazır olunca Teşhis sonucu seçin. İlk testte otomatik mod Disabled kalmalıdır. Gerçek modelin yalnız hipotez ürettiğini unutmayın.

Bu geliştirme oturumunda yönetici haklı servis kurulumu ve gerçek cihaz onarımı yapılmadı. Önce ayrılmış test cihazında aşağıdaki kabul tamamlanmalıdır.

## Manuel düzeltme ve otomatik mod

Manuel düzeltme alanında işlemi ve riskini okuyup isteyin. Yüksek risk DISM Admin onayı ister. Ajan bağlı değilse Ajan bekleniyor görünür; bu başarı değildir. İşlem doğrulandı yalnız ajan önkontrol/işlem/doğrulama aşamaları başarılıysa görünür. Unknown durumunda cihazı kontrol etmeden tekrar istemeyin. Otomatik geri alma yoktur.

Admin cihaz bazında Inherit/Enabled/Disabled seçebilir. Genel otomatik mod varsayılan kapalıdır. Tüm cihazlara uygula mevcut domain'deki istisnaları kaldırır. Mevcut otomatik katalog yalnız kanıt eşleşmeli DNS cache temizlemeye izin verir; tüm AI önerilerini çalıştırmaz. Başlamış işlem ayar kapatılınca geriye alınmaz. 40.000 cihazda toplu açmadan capacity.md kabulünü tamamlayın.

## Güncelleme, kimlik ve kaldırma

Aynı DeviceId, ServerUrl, servis hesabı ve state yolu korunmalıdır; DPAPI CurrentUser nedeniyle hesap değişikliği state'i okunamaz yapabilir. Güncellemeden önce korunan uygulama/config/state yedeği alın; servis durduktan sonra aynı betikle yeni paketi uygulayın. Önceki enrollment tüketilmişse token istemini boş geçin. Sunucu/ajan katalog sürümleri uyumlu olmalıdır.

Kimlik iptalinde Admin /api/v1/devices/{id}/revoke endpoint'i kullanılır; oturum ve CSRF gerekir. Ajanın sonraki istekleri reddedilir. İptal, başlamış işletim sistemi işlemini durdurmaz. İptal edilmiş kaydın aynı hostname ile yeniden enrollment'ı otomatik desteklenmez; yetkili bakım prosedürü gerekir.

Kaldırırken önce sunucuda kimliği iptal edin, sonra yönetici PowerShell'de:
```powershell
Stop-Service -Name 'WFI Agent'
sc.exe delete 'WFI Agent'
```
Durum/config dosyalarını kurumun sır imha ve saklama politikasına göre yetkili kişi temizlemelidir. Betik GPO/SCCM toplu dağıtım otomasyonu veya imzalı MSI değildir; her cihazın farklı ID/token'ı olmalıdır. Program dosyası ve durum/state klasörü başka kullanıcıların yazmasına açılmamalıdır.

## Agent olmadan log yükleme

Analiz merkezi LOG/TXT/XML ve bunları içeren ZIP alır. En fazla 20 MiB yükleme; ZIP 100 girdi, toplam 64 MiB açılmış içerik. EVTX doğrudan yüklenmez, Get-WinEvent/ToXml ile XML'e aktarın. Büyük logları olay zamanına göre daraltın; sistem logunu değiştirmeyin. Manuel upload otomatik uzaktan onarım başlatmaz.

## Pilot kabul

Servis yeniden başlatma, ağ kesintisi, kayıt yanıtı kaybı, token süresi dolması, yanlış CA, cihaz iptali, model kesintisi ve belirsiz işlem sonucu kontrol edilmelidir. Önce sentetik/düşük risk test; sonra yetkili değişiklik penceresinde gerçek onarım. DNS/WSUS/PKI mevcut kurum izinlerini kullanır; WFI adına inbound WinRM/RPC açılmaz.


## Yükseltme önkontrolleri

Install-Agent.ps1 -PollSeconds 300 ile 5 dakikalık sorgulama seçebilirsiniz (10–600 saniye). Yeni kurulum varsayılanı 30 saniye; yükseltmede parametre verilmezse mevcut değer korunur. Daha uzun aralık komutun alınma gecikmesini artırır; kapasite hesabı için capacity.md kullanın.

Mevcut config'teki DeviceId, ServerUrl veya özel StatePath farklıysa betik servisi durdurmadan reddeder. Yeni kimlik/sunucuya geçiş ayrı yeniden kayıt bakım işlemidir. Token istemi de servis durmadan tamamlanır. Dosya kopyalama/servis başlatma hatasında otomatik rollback yoktur: servisin durumunu kontrol edin ve önceden aldığınız paket/config yedeğiyle yetkili bakım yapın; journal'ı sıfırlamayın.


## Komut iptali sınırı

Ajanın yürütücü isteği önceden iptal edilmişse PowerShell başlatılmaz. Çalışan shell iptalde sonlandırılır ve çıktı okuyucuları kapatılmadan tamamlanır. Bu davranış gerçek sentetik shell testiyle doğrulandı. Komutun işletim sisteminde daha önce başlattığı dış servis veya yaptığı değişiklik otomatik geri alınmaz; belirsiz sonuçta cihazı inceleyin.


Ollama erişilemiyor uyarısı görüldüğünde cihaz envanteri kullanılabilir; Modeli kaydet düğmesi kapalıdır. Ajanlara model yükleyerek çözmeye çalışmayın; API/worker ile model sunucusu bağlantısını sunucu yöneticisi kontrol etmelidir.


Düzeltme onayından sonra Ajan bekleniyor görünmesi normaldir; onay başarı sonucu değildir. Tekrar onay veya artık bitmiş işi iptal etme 409 durum çatışması döndürebilir. Durumu yenileyin; isteği art arda yeniden göndermeyin. Teşhis iptali daha önce başlatılmış ayrı onarımı geri almaz.


### Envanter sayfalama güncellemesi

Bu güncelleme sunucu sorgusu ve veritabanı indeksidir; istemciye yeni port veya ek kurulum parametresi getirmez. Portal 50 cihazlık sayfalar gösterir. Canlı heartbeat güncellemeleri cihazın sayfasını değiştirebilir; belirli cihazı bulmak için hostname/IP araması kullanın (en fazla 63 karakter).

Politika kayıt düzeltmesi sunucu tarafındadır; ajan yeniden kaydı veya yeni istemci parametresi gerektirmez. Portal ayar kaydında hata gösterirse değişikliğin uygulandığını varsaymayın; ayarı yeniden okuyun.

Portalda teşhis iptali ajanı kaldırmaz veya çalışmakta olan ayrı düzeltmeyi durdurmaz. Kanıt toplama devam ediyor olsa bile iptal edilmiş teşhisin geç sonucu sunucuda işlenmez. Bu arayüz güncellemesi için ajan yeniden kaydı gerekmez.

### Atama süresi düzeltmesini dağıtma

Güncel agent klasörünü mevcut kurulum betiğiyle yükseltin; cihaz kimliğini ve korunan state dosyasını koruyun. Yeni ayar veya yeniden kayıt gerekmez. İstemci ve sunucu saatlerini kurum zaman kaynağıyla eşitleyin: atama süresi UTC ile değerlendirilir. Süre dolmuş işte ajan başarısız sonuç gönderir; yapılan OS değişiklikleri geri alınmış sayılmaz.

Yerel PostgreSQL kabul testi ajan kurulumu gerektirmez ve istemcide onarım çalıştırmaz. Test paketi sunucu geliştirme doğrulaması içindir; istemcilere PostgreSQL dağıtmayın.

İki API süreci kabul testi sunucu geliştirme bilgisayarında çalışır; Windows istemci servisi kurmaz veya onarım yürütmez. İstemci kurulumu ve ajan kimliği bu test genişletmesinde değişmedi.

Aktif teşhis varken portal yeni manuel teşhisi reddeder. Mevcut işi bekleyin veya iptal edin; ajanı yeniden kaydetmek gerekmez. Bu değişiklik için istemci parametresi/port değişmez.

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

### PostgreSQL büyük envanter doğrulaması — 2026-10-25
Kabul paketine 40.000 sentetik cihaz içeren PostgreSQL envanter senaryosu eklendi. Aynı heartbeat zamanında kimlikle kararlı sıralama, ilk/orta/son sayfalar, 50 kayıt sınırı, domain ayrımı, arama normalizasyonu ve listeden büyük/özel alanların çıkarılması sınanır. EF migration ve schema.sql yollarının ikisinde de çalışır. Tek kullanıcı ve TestServer API çağrıları kullanır; 40.000 aktif ajan, eşzamanlı trafik, ağ/TLS/LDAP veya üretim kapasitesi ölçümü değildir. Ürün davranışı, şema, kurulum ve firewall gereksinimleri değişmedi.

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

Uygulamayı ajan kurmadan arayüzden denemek için [yerel test rehberi](local-testing.md). Gerçek ajanı olmayan kayıt üzerinde bekleyen teşhis/düzeltme gerçek yürütme değildir.

## 0.6 geliştirme — kapasite temeli (devam ediyor)

Yeni ajanlar combined-check-in-v1 yeteneğiyle /api/v1/agent/me/check-in kullanır; eski sunucuda yalnız 404 alındığında eski heartbeat/claim akışına döner. Yeni sunucu eski uç noktaları korur. Boşta aralık kademeli 300 saniyeye çıkar (yerel minimum daha uzunsa korunur), hata beklemesi jitter ile artar; Retry-After dikkate alınır. Sonucu bilinmeyen onarım otomatik tekrar yürütülmez.

SharedModelCapacity migration'ı ve güncel deploy/schema.sql birlikte dağıtılmalıdır. Ollama:MaxConcurrent varsayılan 1 (1..64); aynı model sunucusunu paylaşan tüm düğümlerde aynı değer kullanılır. 300 saniyelik DB saatli slot, token/generation fencing ve en çok 240 saniye model isteği bütçesi vardır. Eski worker'lar slot protokolünü kullanmadığından yükseltme sırasında eski worker'lar durdurulmalıdır; kapasite sınırı karışık worker sürümlerinde garanti edilmez.

Admin Sistem Sağlığı ekranı domain iş kuyruğu özetini gösterir. Telemetry:OtlpEndpoint boşsa dışa metrik gönderilmez. İsteğe bağlı OTLP gRPC collector için yalnız yapılandırılmış sunucu hedefi/portu açılır (yaygın collector portu 4317); bu ayar collector teslimat doğrulaması değildir. Yeni istemci inbound portu yoktur.

On maddelik plan henüz tamamlanmamıştır. Kampanya, BigFix merkezi ve AI kalite uygulamaları eklendi; açık geliştirmeler ve kabul sınırları docs/implementation-roadmap.md içinde, sayısal test sonuçları doğrulama belgesinde tutulur.

## 0.6 geliştirme güncellemesi — 26 Ekim 2026
Kampanya, BigFix merkez doğrulaması ve AI kalite laboratuvarının kurulum/kullanım sınırları docs/operations-0.6.md içinde belgelenmiştir. BigFix ve isteğe bağlı OTLP yalnız sunucudan outbound erişim ister; yeni istemci inbound portu yoktur. Yeni şema uygulanmadan readiness başarılı olmaz. Bu geliştirme henüz on maddelik yol haritasının tamamı değildir; güncel durum docs/implementation-roadmap.md içindedir.



## 0.6.0-preview.2 — ajan ve yayın hazırlığı (27 Ekim 2026)

Ajan çalışma zamanı ve PowerShell kurucusu yalnız HTTPS origin kabul eder; URL içinde kullanıcı bilgisi, path, query veya fragment reddedilir. HTTP yanıtı 1 MiB, kalıcı ajan durumu 128 KiB ile sınırlıdır. Başka lease'e ait veya işsiz receipt içeren bozuk günlük korunarak reddedilir; otomatik kimlik sıfırlama yapılmaz. 401 ve 503 eski protokole dönüş tetiklemez; 429 Retry-After korunur. Onarım cevabı kaybolduğunda işlem tekrar yürütülmez.

Denetim kayıtlarına Domain sütunu ve indeks eklendi. API artık yalnız oturumun domain'indeki kayıtları döndürür. Eski domain'i belirsiz kayıtlar silinmez, web listesinden gizlenir; DBA tarafından ayrıca incelenebilir. AuditDomainScope EF migration ve deploy/schema.sql birlikte güncellendi. Üretimde migration başlangıçta otomatik çalışmaz.

WFI-Server-Setup.exe yönetici yükseltmesi isteyen tek dosyalı, etkileşimli sunucu kurucusudur. Yükünü Program Files altında admin/SYSTEM korumalı geçici dizine açar; yerel yapılandırma dosyası, hostname, TLS sertifikası ve kurulum yolu ister. IIS + .NET 10 Hosting Bundle ve hazırlanmış PostgreSQL önkoşuldur. Veritabanı ve Ollama otomatik kurulmaz. HA/upgrade manuel bakım adımlarını kullanır. Otomasyon için Install-Server.ps1 kullanılabilir. Setup EXE'leri henüz Authenticode imzalı değildir.

Yeni port gerekmiyor. Ajan → WFI HTTPS 443; sunucu → LDAPS/DB/Ollama ve koşullu BigFix/OTLP mevcut firewall tablosuna göre sınırlandırılır. GitHub Pages statik tanıtımdır, canlı kurum konsolu veya log barındırmaz. Public kaynak örneğinde domain example.invalid olarak temizlenir; yerel kurum checkout varsayılanı korunur.
## 0.6.0-preview.3 — 29 Ekim 2026

- Yeni oturumlarda kırmızı-beyaz aydınlık tema varsayılan; sağ üstten karanlık tema seçimi kalıcıdır. Standart seçimi eski tercihler için aydınlık tasarımın uyumlu seçeneği olarak korunur. Onarım Kampanyaları ekranı Onarım Otomasyonu olarak adlandırıldı; API ve politika sınırları değişmedi.
- Sunucu ve istemci setup EXE'leri kırmızı WFI simgesi taşır. Kurulumdan sonra appwiz.cpl içinde WFI Server / WFI Agent, Publisher: Berkay Atağ, sürüm ve simge kaydedilir. Publisher metni Authenticode imzası değildir; paketler imzasız pilot sürümdür.
- Kaldırma yönetici yetkisi ister; servis/IIS sahipliği, kayıtlı kurulum yolu ve reparse kontrollerinden sonra çalışır. Veritabanı, kanıt, anahtarlar, ajan kimliği ve appsettings.local.json korunur. Çalışan kaldırıcı ve küçük kurtarma yardımcıları korumalı kurulum klasöründe kalır. Yeniden kurulum/yükseltme için korunan yapılandırmayı ve kimliği kullanarak bakım rehberini izleyin; fresh-install kurucusu var olan veri/anahtar dizinlerini devralmaz.
- İstemci kurulumunda üst klasör junction/symlink geçişi reddedilir. Sunucuda uygulama, kanıt ve anahtar dizinleri aynı veya iç içe olamaz. Kaldırıcı başka servis yolu veya paylaşılan IIS havuzunu kaldırmaz; alt IIS uygulaması varsa manuel bakım gerekir.
- Yeni port yoktur. Ajan outbound HTTPS 443; LDAPS/PostgreSQL/Ollama ve koşullu BigFix/OTLP erişimleri mevcut firewall tablosuna tabidir.
- GitHub yeni sürümünde sunucu EXE, dağıtım ZIP'i ve SHA256 bulunur. İstemci EXE ayrı release dosyası değildir; sunucu paketinin içindeki konsoldan Ajan ve Ağ Erişimleri üzerinden indirilir. Önceki sürümler tarihsel olarak korunur.
- Gerçek Windows VM appwiz/kurulum/kaldırma, AD/HA ve 40 bin istemci kabulü açık kalır. Codex Security başlatma girişimi test bağımlılığındaki uzun dosya yolu nedeniyle yine rapor oluşturamadı; davranış regresyonları ayrıca çalıştırılır.

