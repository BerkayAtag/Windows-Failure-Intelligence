using System.Text.RegularExpressions;

namespace WFI.Core;

public sealed record DetectionRule(string Id, string Category, string Pattern, string Title, string Explanation, string[] Checks)
{
    public Regex Matcher { get; } = new(Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
public static class Rules
{
    public const string Version = "1.0.0";
    public static readonly DetectionRule[] All =
    [
        new("BF-001", "BigFix", @"\bWFI_BESCLIENT_(?:STOPPED|MISSING)\b", "BigFix istemci servisi çalışmıyor",
            "Ajanın BESClient servis kontrolü çalışmayan veya eksik servis bildirdi.", ["BESClient servisini başlatıp doğrulayın.", "Başlatma başarısızsa ajan üzerinde hash ile onaylanmış reinstall.cmd dosyasını kullanın."]),
        new("UPG-001", "Windows Upgrade", @"\b0xC1900101\b", "Yükseltme geri alındı",
            "Genel rollback kodu bulundu. Sürücü uyumluluğu olası nedenlerden biridir; bu kod tek başına sürücü arızasını kanıtlamaz.",
            ["SetupDiag eşleşen kuralını ve setup aşamasını kontrol edin.", "Rollback ve setupapi.dev.log kayıtlarını aynı zaman aralığında karşılaştırın."]),
        new("UPG-002", "Windows Upgrade", @"\b0xC1900208\b", "Uyumluluk engeli olasılığı",
            "Yükseltmeyi engelleyen uygulama veya uyumluluk kontrolü olabilir.", ["CompatData ve SetupDiag içinde engellenen bileşeni doğrulayın."]),
        new("WU-001", "Windows Update", @"\b0x800F081F\b", "Onarım kaynağı bulunamadı",
            "Servicing işlemi gerekli kaynak dosyalarına ulaşamamış olabilir.", ["CBS ve DISM zamanlarını eşleştirin.", "OS build ile onarım kaynağının uyumunu doğrulayın."]),
        new("WU-002", "Windows Update", @"\b0x80070070\b", "Yetersiz disk alanı olasılığı",
            "İşlem yetersiz depolama alanı kodu döndürmüş.", ["Sistem, EFI ve recovery bölümlerinin boş alanını kontrol edin."]),
        new("SCCM-001", "SCCM Client", @"\b0x80072EE2\b", "Yönetim bağlantısında zaman aşımı",
            "İstek zaman aşımına uğramış. Ağ, proxy veya yönetim noktası yanıtı incelenmeli.", ["MP/WSUS hedefini logdan belirleyin.", "DNS, proxy ve TLS erişimini etkilenen istemciden doğrulayın."]),
        new("AD-001", "Domain / DNS", @"\b(?:ERROR_NO_LOGON_SERVERS|0x0000051F)\b|no logon servers available",
            "Domain controller erişimi başarısız",
            "Kimlik doğrulama sunucusuna erişim sorunu işaretlenmiş; DNS, ağ veya DC sağlığı kaynaklı olabilir.", ["DC SRV kayıtlarını ve istemcinin DNS sunucularını kontrol edin.", "Netlogon olayları ve DC erişimini aynı zaman aralığında inceleyin."]),
        new("AD-002", "Domain / DNS", @"\b(?:ERROR_TRUSTED_RELATIONSHIP_FAILURE|0x000006FD)\b|trust relationship.*failed",
            "Domain güven ilişkisi sorunu",
            "Makine hesabı ile domain arasındaki güven ilişkisi hatası bulundu.", ["Makine hesabı, saat senkronizasyonu ve secure channel durumunu doğrulayın.", "Herhangi bir onarım öncesinde yetkili değişiklik prosedürünü izleyin."])
    ];
}
