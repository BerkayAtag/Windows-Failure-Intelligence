# Doğrulama raporu — 22 Ekim 2026

v0.2 ekleri: [güvenlik bulguları ve düzeltmeleri](security-review.md), [40.000 kayıt ölçümü](capacity-results.json), [HA kabul sınırları](load-balancer.md). İki ayrı TestServer host'u arasında ortak key ring ile oturum kabulü ve diğer düğümde logout iptali otomatik testte doğrulandı. Bu test SQLite ve yerel dosya sistemi kullanır.

Tema akışı: giriş → Standart/Açık/Karanlık seçimi → yenilemede tercihin korunması → demo dashboard → üç temada masaüstü ve mobil taşma kontrolü. Ekran kimliği, anlamlı içerik, JS pageerror ve görünür etkileşimler kontrol edildi. Görsel kayıtlar docs/design/theme-*.png dosyalarındadır. Çıktıdaki NO_COLOR/FORCE_COLOR uyarısı test çalıştırıcısına aittir.

## Tamamlanan kontroller

| Kontrol | Sonuç |
| --- | --- |
| .NET Release testleri | 49 geçti, 0 başarısız |
| React TypeScript + Vite production build | Geçti |
| Playwright Chromium | 3 uçtan uca senaryo geçti |
| pnpm audit | 0 bilinen açık |
| .NET doğrudan/transitive bağımlılık denetimi | Bilinen açık raporlanmadı |
| EF migration model farkı | Yok |
| PostgreSQL idempotent SQL üretimi | `deploy/schema.sql` oluşturuldu |
| API/Worker publish | `artifacts/api`, `artifacts/worker` oluşturuldu |

API testleri gerçek HTTP pipeline, cookie ve CSRF kullanır; LDAP yerine test double, PostgreSQL yerine izole SQLite kullanır. Bu yüzden gerçek LDAPS bind, AD grup sorguları ve PostgreSQL üzerinde eşzamanlı queue davranışı bu sonuçlarla doğrulanmış sayılmaz.

## Hata düzeltmeleri

- SQLite native bağımlılığındaki bilinen yüksek önem dereceli açık giderildi.
- Vite ve esbuild için denetimde bulunan açıklar yamalı sürümlerle giderildi; esbuild override gerekçesi GHSA-g7r4-m6w7-qqqr.
- Başlangıç LDAP konfigürasyon doğrulaması tamamlanmış host yapılandırmasını okuyacak şekilde düzeltildi.
- ZIP toplam kotası yalnızca metadata yerine gerçekten açılan baytlarla da hesaplanıyor.
- Aynı dosyanın eşzamanlı/mükerrer yüklenmesinde unique idempotency anahtarı ve kaybeden istek için mevcut işi döndürme yolu var. Ardışık tekrar yükleme otomatik testte doğrulandı; PostgreSQL yarış testi henüz yapılmadı.
- Süresi dolan iş lease’inin geri alınması ve aktif lease’in korunması test edildi.
- Bozuk XML işinin sonraki geçerli işi durdurmaması test edildi.
- Farklı domain’lerin aynı hostname’i birbirine karışmıyor; yabancı domain job erişimi 404 dönüyor.
- Dosya seçicideki İngilizce tarayıcı metni kaldırıldı; seçilen dosya adı ve klavye odağı korunuyor.
- Yerel ayar dosyaları ve `.data` publish içeriğinden açıkça dışlandı.

## Tarayıcı ve tasarım incelemesi

Browser plugin ve browser skill listede olmadığı için skill yönlendirmesiyle Playwright Chromium kullanıldı (Browser plugin not available). Masaüstü viewport **1505×1045**, mobil **390×844**. Referans `docs/design/concept.png`; gerçek ekranlar `desktop.png`, `mobile.png`, `login.png`, `result.png`. Konsept ve son tarayıcı çıktıları `view_image` ile incelendi.

| Nokta | İnceleme / karar |
| --- | --- |
| Yerleşim | Lacivert sol gezinme, açık zemin, beyaz analiz formu, sağ kapsam alanı ve altta tablo korundu. |
| Tipografi | Segoe UI ailesi, büyük ana başlık, belirgin form/tablolar ve ikincil metin hiyerarşisi uygulandı. |
| Renkler | Lacivert #102744, açık #eef3f8, mavi eylem düğmeleri ve ince sınırlar korundu. |
| Form | Hostname/domain alanları, kesik çizgili dosya seçici ve sağa hizalı analiz eylemi korundu; native İngilizce kontrol düzeltildi. |
| İkonlar | Dosya, arama, geçmiş, bağlantı, ağ ve bilgi simgeleri tutarlı çizgi ikonlarıyla uygulandı. Konseptteki Windows logosu yerine nötr monitör ikonu kullanıldı. |
| Mobil | Üstte gezinme, tek sütun form, yatay taşma olmadan tablo kaydırma. Yardım paneli dar ekranda gizleniyor. |
| Etkileşim | Giriş/çıkış, upload, sonuç/kanıt, tekrar yükleme, filtre ve bağlantılar çalıştırıldı; JS pageerror yok. |

Konsepte görsel bağlılık, yukarıdaki işlevsel farklılıklar dahilinde kontrol edildi; piksel düzeyinde aynı olduğu iddia edilmiyor. İlk ekran metin karşılaştırmasında başlık, alt başlık, navigasyon, form ve ana CTA korundu. Bilinçli farklar: demo uyarısı, gerçek kullanıcı/rol, çıkış eylemi, arama/sayfalama, “Seçili analizde bulgu” ifadesi, henüz seçili sonuç yoksa 0 yerine “—”. Konseptteki masaüstü pencere kontrolü simgeleri web uygulamasına taşınmadı. Demo uyarısı nedeniyle form aşağı kayar; sahte güvenli bağlantı veya sahte bulgu sayısı gösterilmez.

## Henüz yapılmayan kurumsal kabul

- Gerçek DC ile başarılı/başarısız LDAPS giriş, nested grup, kilitli/disabled kullanıcı, sertifika süresi/hostname/CA sorunları.
- PostgreSQL migration uygulama ve rollback/backup restore denemesi; birden çok API/worker ile eşzamanlı iş sahiplenme.
- IIS HTTPS, servis kimliği, NTFS ACL, anahtar şifreleme ve audit merkezi aktarımı.
- SCCM/BigFix connector, doğrudan EVTX, tam SetupDiag şema yorumu, kaynaklar arası zaman korelasyonu, incident yaşam döngüsü.
- 50–200 cihaz pilotu, ardından ölçüme bağlı ölçek; 40.000 kayıtla SQLite listeleme testi geçti; 40.000 eşzamanlı cihaz desteği kanıtlanmadı.

Bu maddeler çözülmeden “eksiksiz kurumsal üretim platformu” kabulü verilmemelidir.

## Güncel 0.3.0 sonuçları

Bu dosyanın eski v0.2 ölçümleri tarihsel kapsamdır. Yeni ajan/Ollama/UI çalışması için [verification-current.md](verification-current.md) esas alınmalıdır: 95 .NET test, 4 Playwright, gerçek sentetik phi4-mini hattı; yeni Codex Security scan başlatma hatası ve henüz yapılmayan kurum kabulü açıkça kayıtlıdır.





