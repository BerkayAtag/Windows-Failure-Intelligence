# Güvenlik

Gerçek parolaları, kurum loglarını ve sertifika özel anahtarlarını depoya/issue’ya eklemeyin. Güvenlik bulgularını şirketinizin iç güvenlik kanalına iletin; bu depo için harici yayın veya iletişim kanalı oluşturulmadı.

Üretim öncesi `docs/deployment.md` ve `docs/verification.md` kontrol edilmelidir. Bu sürüm MFA, OU bazlı erişim, yönetici tarafından tüm oturumları anlık iptal etme, WORM audit veya antivirüs karantina servisi sağlamaz. Yüklemeler biçim/kota kontrollerinden geçer ama antimalware taraması yapılmış kabul edilmez. LDAPS sertifika doğrulamasını kapatmayın.

