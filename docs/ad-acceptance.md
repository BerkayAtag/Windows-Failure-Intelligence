# AD/LDAPS portal kabul planı

Bu plan kaynak incelemesine dayanır; gerçek AD kabul sonucu değildir. Kurum test ortamı henüz hazır olmadığından aşağıdaki sonuçlar doldurulmamıştır. Sentetik test kullanıcıları kullanın, parola/özel anahtarları rapora veya sohbet ortamına yazmayın.

## Hazırlık

- Test VIP/FQDN, en az bir test DC FQDN'i, kurum CA güveni, Base DN ve rol gruplarının tam DN'leri belirlenmeli.
- `Ldap.Domain` değiştirilebilir; example.invalid yalnız varsayılandır. `Ldap.Server`, `Port`, `BaseDn`, `TimeoutSeconds` ve `GroupRoles` tüm API düğümlerinde eşleşmeli.
- Kullanıcı parolası uygulama konfigürasyonuna konmaz. Girişte kullanıcının sağladığı bilgilerle LDAPS bind yapılır. TCP 636 erişimi tek başına bind/sertifika/grup yetkisini kanıtlamaz.
- Viewer, Analyst ve Admin için ayrı, yetkili kurum yöneticisinin onayladığı AD security grupları kullanın. Kod eşlenen DN üzerinde üyelik sorgular ve dönen groupType değerinin security-enabled bitini kontrol eder. Distribution veya eksik/bozuk groupType rol üretmez. Yükseltmeden önce rol eşlemelerinin AD security grupları olduğunu doğrulayın; dağıtım grubu kullanan eski eşleme artık giriş yetkisi vermez.
- Bir kullanıcının birden fazla eşlenen gruba üyeliği birleşik roller üretir. Beklenmeyen Admin üyeliğini özellikle kontrol edin. Primary group üyeliği tek başına erişim sağlamaz.

## Kimlik biçimi sınırı

Kısa ad `kullanıcı@Ldap.Domain` biçimine çevrilir ve `userPrincipalName` ile aranır. Bu, ayrı bir `sAMAccountName` dizin araması değildir. sAMAccountName ile UPN yerel kısmı farklı kullanıcı için kısa adın çalışması beklenmemelidir. Alternatif UPN suffix ve NetBIOS `DOMAIN\kullanıcı` biçimi desteklenmez. Farklı suffix gerekiyorsa önce tasarım/yetkilendirme kapsamı netleştirilmeli; kurum kabulünde yanlışlıkla çalışıyor kabul edilmemelidir.

## Kabul matrisi

| Senaryo | Beklenen sonuç | Kaydedilecek kanıt |
| --- | --- | --- |
| Doğru FQDN/CA ve yetkili Viewer | Giriş başarılı; yazma/admin işlemleri 403 | HTTP durumu, rol, test zamanı |
| Analyst | Teşhis ve manuel düzeltme istenebilir; admin politikası değiştirilemez | Sentetik cihaz iş kimliği, audit aksiyonu |
| Admin | Model/politika yönetimi ve yüksek risk onay erişimi | Gerçek onarım çalıştırmadan kontrol |
| Yanlış parola / disabled / locked test hesabı | Giriş başarısız; cookie ile yetki verilmez | HTTP durumu; parola veya bind ayrıntısını kaydetmeyin |
| Grup üyeliği olmayan hesap | Giriş başarısız | Başarısız giriş ve oturum yokluğu |
| Nested üyelik | Yalnız eşlenen grup rolü oluşur | Test grubu ilişkisi, elde edilen roller |
| Eşlenen distribution grubu veya okunamayan groupType | Bu eşlemeden rol verilmez; başka uygun rol yoksa giriş reddedilir | Grup türü ve yeni giriş sonucu |
| Yalnız primary group üyeliği | Erişim verilmez | Giriş başarısızlığı |
| Birden fazla rol grubu | Beklenen birleşik roller | Rol listesi |
| Başka domain/suffix veya NetBIOS kullanıcı adı | Reddedilir | Parolasız giriş biçimi örneği ve durum |
| Base DN dışında kullanıcı / yanlış grup DN | Yetki verilmez; dizin operasyon hatası servis hatası olabilir | HTTP durumu, traceId; sır içermeyen kayıt |
| Güvenilmeyen CA / sertifika adı uyuşmazlığı / DC kapalı | Başarılı giriş olmaz; servis hatası oluşabilir | Sertifika doğrulamasını kapatmadan sonuç |
| Grup üyeliği kaldırılması | Yeni girişte erişim yok; mevcut oturum en fazla belgelenmiş 30 dakika süresince kalabilir | Yeni ve mevcut oturumun ayrı sonucu |
| Logout | Aynı cookie başka API düğümünde de reddedilir | VIP üzerinden iki düğüm sonucu |
| Yapılandırılmış domain değiştirilmesi | Yeni domain kimliğiyle giriş; eski domain verileri görünmez | Ayrı test DB/ortamında domain ayrımı |

Hatalı parola ile DC erişim hatasının aynı HTTP kodunu vermesi beklenmez: geçersiz kimlik 401, bağlantı/dizin servis hatası mevcut API genel hata işleyicisinde 503 olabilir. Hata mesajları kurum parolasını veya LDAP bind ayrıntısını içermemelidir.

## Uygulama sırası ve kayıt

Önce tek test API düğümünde kimlik/rol matrisi, sonra VIP üzerinden iki düğüm ve logout kontrolü yapın. Otomatik mod kapalı kalsın. Başarılı bind, OS onarımı veya 40.000 ajan kabulü değildir. Her satıra tarih, paket SHA256, API düğümü, sentetik kullanıcı rolü, beklenen/gerçek sonuç ve varsa traceId ekleyin. Parola, token, session cookie, tam kurum logu veya özel anahtar eklemeyin. Başarısız satır çözülmeden ilgili kabul maddesini tamamlandı olarak işaretlemeyin.

