# WFI için skill’ler

Bu oturumda aşağıdaki ilgili skill’ler zaten kuruluydu; ek indirme gerekmedi. Skill, geliştirici asistanının iş akışıdır; WFI uygulamasının runtime bağımlılığı veya şirket ağına kurulan bir agent değildir.

| Skill | Kullanım |
| --- | --- |
| `build-web-apps:frontend-app-builder` | Bu sürümde arayüz tasarım yaklaşımı ve konsept referansı için kullanıldı. |
| `imagegen` | Arayüz konsepti üretiminde kullanıldı; kurum logu/kimlik bilgisi gönderilmedi. |
| `build-web-apps:frontend-testing-debugging` | v0.2 tema, giriş/yükleme/çıkış ve mobil tarayıcı doğrulamasında kullanıldı. |
| `build-web-apps:react-best-practices` | Büyük tablo/render performansı geliştirmeleri için. |
| `codex-security:security-scan` | v0.2 öncesi kaynak taraması tamamlandı; iki orta önem bulgusu raporlandı ve regresyon testleriyle düzeltildi. security-review.md raporuna bakın. |
| `codex-security:security-diff-scan` | Sonraki PR’larda güvenlik etkisi değerlendirmesi için. |
| `codex-security:verify-fix` | Güvenlik bulgularının düzeltmelerini bağımsız doğrulamak için. |
| `skill-creator` | Kurumun onaylı deployment/AD prosedürleri netleşince WFI’ya özel tekrar kullanılabilir skill oluşturmak için. |

On-prem PostgreSQL için Supabase servisi gerekli değildir. LDAP için ek bir cloud plugin gerekmez; uygulama .NET System.DirectoryServices.Protocols kullanır. Gerçek kurum loglarını harici tarama/AI hizmetine göndermeden önce kurumun veri aktarım politikasını uygulayın.

OpenAI Developers eklentisi kullanılabilir durumdadır; uygulamada OpenAI API çağrısı olmadığı için agents/API-key/troubleshooting skill'leri bu değişikliklere uygulanmadı. Genel .NET/React bug fix için API anahtarı veya yeni cloud bağlantısı gerekmez. Geliştirme kurallarının kalıcı kaydı proje kökündeki AGENTS.md dosyasındadır.
## 0.3.0 devam çalışması

Build Web Apps frontend-testing-debugging ve react-best-practices talimatları kullanıldı. Browser plugin/browser skill listelenmediği için mevcut Playwright ile dört akış sınandı. Codex Security security-scan skill'i kullanılarak Standard scan başlatıldı; eklentinin geçici derleme dosyası okuma hatası nedeniyle yeni scan tamamlanmadı. OpenAI Developers genel bug fix veya Ollama için gerekli değildir; OpenAI API/cloud anahtarı eklenmedi. Tüm gerekli skill'ler zaten kurulu; yeni eklenti indirilmedi.


0.4.0: Build Web Apps frontend-testing-debugging ve react-best-practices kullanıldı. Browser plugin bulunmadığından mevcut Playwright tercih edildi. NGS Analysis Workbench genomik iş akışlarına yöneliktir; Windows teşhisine uygulanmadı. Visualize bu uygulama bileşeninin runtime bağımlılığı değildir; yeni görsel gerekmedi. OpenAI Developers kurulu, ancak model sunucusu Ollama olduğundan OpenAI API entegrasyonu eklenmedi.
