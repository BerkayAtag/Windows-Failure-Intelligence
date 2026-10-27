# AI deneyim hafızası

WFI model ağırlıklarını kendi kendine değiştirmez. Geçmiş doğrulanmış sonuçlardan bağlam getirerek yeni teşhisi destekler (retrieval). Geçmiş başarı yeni sorunun kesin nedeni veya onarım izni değildir.

## Uygun deneyim

- Aynı domain; teşhis, AI analizi ve onarımın domain/device ilişkileri tutarlı.
- Teşhis ve AI completed; onarım Success; son 180 gün.
- Katalog işlemi halen onaylı ve geçerli katalog sürümünde.
- Yeni analizde aynı teşhis kapsamı ve güncel deterministic kanıtta eşleşen playbook.
- Admin tarafından LearningExcluded olarak dışlanmamış.

Son 50 adaydan yinelenen işlem kimlikleri elenir, en fazla 5 örnek seçilir. Metinler redakte edilir ve sınırlandırılır. AI'ye verilen örnekler LearningContextJson içinde kaydedilir; Cihazlar ve AI sonuç alanında işlem kimlikleri görülebilir. AI Deneyimleri ekranı en son 25 uygun örneği gösterir. Bu sayı toplam kurum başarısı/başarı oranı değildir.

İşlem doğrulaması, örneğin BESClient Running, bütün olayın kök nedeninin kesin çözüldüğünü kanıtlamaz. İnsan tarafından yanlış olduğu görülen örnek Admin tarafından gelecekteki kullanımdan çıkarılabilir; audit learning.excluded kaydı oluşturulur. Dışlama geçmiş analiz/audit'i silmez ve başlamış işleri iptal etmez.

Otomatik onarım kararı geçmiş model metninden üretilmez. Güncel ajan kanıtı, sabit katalog, modelin başarılı yanıtı, cihaz politikası, onay ve bekleme sınırları korunur. Model keyfi komut oluşturup yürütemez. Yeni bulgu/tür için yeni katalog ve regresyon gerekir.
