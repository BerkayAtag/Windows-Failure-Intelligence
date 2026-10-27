# Kurulum öncesi bağlantı kontrolü
PowerShell 7 ile hedef istemcide veya sunucuda:

```powershell
.\scripts\Test-WfiConnectivity.ps1 -ServerUrl 'https://wfi.example.invalid' -TimeoutSeconds 10
```

Gerçek WFI FQDN'inizi kullanın. Betik DNS çözümlemesini, HTTPS üzerinden `/api/v1/health` için `alive`, `/api/v1/ready` için `ready` yanıtını doğrular. Her HTTP adımı için 1–60 saniye timeout seçilebilir; varsayılan 10 saniyedir. TLS sertifika doğrulaması işletim sistemi güven zincirini kullanır; sertifika doğrulaması kapatılmaz, redirect izlenmez. Başarılı HTML proxy sayfası API kabulü sayılmaz. Path-prefix varsa URL'ye eklenebilir; kurum proxy yapılandırması ayrıca doğrulanmalıdır.

Parola, token veya LDAP kullanıcı bilgisi gerekmez. URL'de kullanıcı bilgisi, query veya fragment reddedilir. Çıktı yanıt gövdesini içermez. Betik servis başlatmaz, firewall/sertifika/konfigürasyon değiştirmez; yalnız verilen sunucuya DNS ve iki GET kontrolü yapar. Başarısızlıkta hata üretir ve PowerShell dosya çalıştırması başarısız biter.

DNS başarısızsa isim/kurumsal DNS erişimini kontrol edin. HTTPS başarısızsa CA güveni, FQDN/sertifika adı, proxy, TCP portu, servis ve timeout'u kontrol edin. HTTP 503 readiness için DB erişimini/şemayı inceleyin. Yönlendirme veya beklenmeyen içerikte WFI base URL ve reverse proxy yolunu kontrol edin. Betik ayrıntılı TLS hata nedeni veya sertifika iptal kontrolü sertifikasyonu sağlamaz.

Başarılı sonuç AD kullanıcı/grup yetkisi, ajan enrollment/DPAPI, Ollama, raw storage, OS onarımı, load balancer failover veya 40.000 aktif ajan kapasitesini doğrulamaz. Readiness mevcut uygulamada DB sorgusunu sınar; storage/LDAP/model kontrolü değildir. Her API düğümü ve VIP için kurumun izin verdiği erişim yolundan ayrıca çalıştırın.

Geliştirme doğrulaması: `Test-ConnectivityValidation.ps1` ağ isteği göndermeden 12 URL/yanıt senaryosunu sınar; Build.ps1'e dahildir. Yerel HTTP-only demo HTTPS başarı kontrolünden geçmemelidir. Gerçek kurum CA/HTTPS kabulü ayrı ortam gerektirir.
