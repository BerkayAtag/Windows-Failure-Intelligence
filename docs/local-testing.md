# Son sürümü yerel olarak deneme

## 0.6 geliştirme ekranını deneme

Proje kökünde `./scripts/Build.ps1`, ardından `./scripts/Start-Demo.ps1` çalıştırın. http://127.0.0.1:5080 adresinde **Demoyu aç** seçin. Analiz merkezi makine + sorun açıklaması alır. Demo gerçek kurumsal makinelere erişmez: cihazın kaydı ve çalışan HTTPS ajanı olmadan iş “Ajan bağlantısı bekleniyor” durumunda kalır; bu durum hata gizleme veya başarılı teşhis değildir. Ajan ve Ağ Erişimleri menüsünden EXE indirme, sessiz komut ve ağ kuralları; AI Deneyimleri menüsünden geçmiş örnekler incelenebilir. Cihaz kaydı ve politika ayarları Cihazlar ve AI menüsündedir. Gerçek makine kabulü için sunucu/istemci kurulum belgelerini izleyin.


## Şimdi açın

Bu bilgisayardaki adres: http://127.0.0.1:5080
Giriş ekranında **Demoyu aç** düğmesini kullanın. Demo oturumu gerçek LDAP/AD girişi değildir. Bu adres yalnız aynı bilgisayardan erişilebilir; şirket ağına yayın için server-installation.md ve firewall-requirements.md içindeki HTTPS/VIP kurulumu gerekir.

## Kapatıldıktan sonra yeniden başlatma

PowerShell 7'de:

```powershell
Set-Location 'C:\WFI-Source'
.\scripts\Start-Demo.ps1
```

Pencereyi açık bırakın; durdurmak için Ctrl+C kullanın. 5080 zaten çalışıyorsa yeniden başlatmak yerine mevcut sayfayı açın. Alternatif port için `-Port 5081` kullanabilirsiniz. Kaynak değişikliği yaptıysanız önce `scripts/Build.ps1` çalıştırın; bu frontend, .NET testleri ve API/worker/ajan paketlerini oluşturur. Başlatma betiği .NET SDK ister ve mevcut WFI SDK kurulumunu bulur.

## Deneme sırası

1. Analiz merkezi: makine adı, kapsam ve sorun açıklaması girin. Gerçek kanıt için kayıtlı/bağlı ajan gerekir; dosya yükleme isteğe bağlı eski akıştır.
2. Cihazlar ve AI: sunucudaki Ollama model listesini ve etkin modeli kontrol edin. Model kurulu değilse Ollama sunucusuna kurun; ajan bilgisayarlarına model kurmayın.
3. Gerçek ajan bağlı olmayan bir cihaz kaydı üzerinde teşhis/düzeltme kuyruğu, iptal ve politika arayüzünü deneyebilirsiniz. Ajan olmadan gerçek kanıt toplama veya OS düzeltmesi gerçekleşmez; bekleyen iş başarı değildir.
4. Standart/açık/karanlık temaları ve dar ekran görünümünü deneyin.
5. Ayrı Windows pilot cihazında gerçek ajan kurulumuna geçmeden client-installation.md ve ad-acceptance.md kabul adımlarını uygulayın. Otomatik mod varsayılan kapalı kalmalıdır.

## Gerçek yerel Ollama için sentetik hat kontrolü

Demo çalışırken ayrı PowerShell'de `scripts/Test-OllamaPipeline.ps1` çalıştırılabilir. Bu betik yalnız loopback hedefe bağlanır, sentetik cihaz/kanıt oluşturur, otomatik modu kapatır ve model sonucunu bekler. OS onarımı istemez; sonunda test cihazının kimliğini iptal eder. Model çalışmasının tamamlanması üretim doğruluk/kapasite onayı değildir.

## Dosyalar ve paket sınırı

`artifacts/WFI-0.6.0-preview.3-pilot.zip` (geliştirme önizlemesi): API, worker, Windows ajanı, deploy, scripts ve docs içerir. Kaynak checkout, test projeleri, yerel .data verileri ve sır içeren local.json dosyaları dağıtım paketine eklenmez. Start-Demo/Build/Test-Postgres gibi kaynak proje isteyen geliştirme betikleri checkout üzerinden çalıştırılır; yalnız ZIP açılmış klasörde kaynak varmış gibi çalıştırılmaz. Üretim paketi kurulumunda server-installation.md kullanılır.

Güncel test kapsamı ve açık kabul maddeleri verification-current.md; sürüm geçmişi kökte history-release.md içindedir. Gerçek AD/HA, OS onarımı, 40.000 aktif ajan ve yeni tam güvenlik taraması kabulü henüz tamamlanmamıştır.

## Tarihsel 0.4.1 test akışı (güncel kurulum için yukarıdaki EXE akışını kullanın)

Ana kapsam etiketi Genel olmalıdır. Cihazlar ve AI → Ajan setup indir bağlantısı Admin rolüyle ZIP döndürür. Pencereli kurulum gerçek HTTPS + WFI Admin oturumu ister; loopback HTTP demo bunu atlamaz. Yerel sentetik test, SetupRegistration → kayıt → enrollment → online akışını TestServer üzerinde doğrular; gerçek Windows servis kurulumu yapmaz.

Yeni menüler: Sistem Sağlığı, Onarım Kampanyaları ve AI Kalite Laboratuvarı. BigFix eşleme/merkez raporu Cihazlar ve AI içinde seçili cihazdadır. Yeni geliştirmelerin ayarları operations-0.6.md içinde açıklanır. Tam on maddelik plan ve üretim kabulü henüz bitmedi; önizleme üretim yayını değildir.

