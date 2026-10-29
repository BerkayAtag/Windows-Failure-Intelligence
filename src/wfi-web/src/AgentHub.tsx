import { useEffect, useState } from 'react';
import { Download, ShieldCheck, Network, Terminal, ArrowRight } from 'lucide-react';
type Request = <T>(path: string, init?: RequestInit) => Promise<T>;
const rules = [
  ['İstemci ajanı / setup', 'WFI VIP / HTTPS adresi', 'TCP 443', 'Kayıt, heartbeat, teşhis ve sonuç; istemciden dışarı'],
  ['Operatör tarayıcısı', 'WFI VIP / HTTPS adresi', 'TCP 443', 'Konsol ve setup indirme'],
  ['Load balancer', 'WFI API düğümleri', 'Backend HTTPS portu', 'Yalnız LB kaynakları; /api/v1/ready'],
  ['WFI API', 'Seçilmiş domain controller', 'TCP 636', 'LDAPS giriş ve AD grup sorguları'],
  ['API / worker', 'PostgreSQL', 'TCP 5432 veya özel DB portu', 'TLS doğrulamalı veritabanı'],
  ['API / worker', 'İzinli BigFix REST origin', 'Yapılandırılmış HTTPS portu', 'Koşullu; salt okunur Computer ID / son rapor doğrulaması'],
  ['API / worker', 'Kurum OTLP collector', 'Yapılandırılmış gRPC portu (genellikle 4317)', 'Koşullu; Telemetry:OtlpEndpoint boşsa kapalı'],
  ['API / AI worker', 'Ollama', 'Loopback TCP 11434 / uzak HTTPS', 'Uzak model için yalnız sunuculara açık TLS proxy'],
  ['API / worker', 'Ortak dosya sunucusu', 'TCP 445 — koşullu', 'Yalnız UNC log/key storage kullanılıyorsa'],
  ['İstemci / sunucu', 'Kurum DNS, zaman ve PKI hizmetleri', '53 TCP/UDP · 123 UDP · PKI 80/443', 'Mevcut kurum politikasıyla ad, saat ve sertifika doğrulaması'],
];
export function AgentHub({ request, admin }: { request: Request; admin: boolean }) {
  const [tab, setTab] = useState('download'), [error, setError] = useState(''), [copied, setCopied] = useState(false);
  const [info, setInfo] = useState<{ version: string; size: number; sha256: string; platform: string; signed: boolean }>();
  useEffect(() => { let active = true; if (admin) request<typeof info>('/distribution/agent/info').then(x => { if (active) setInfo(x); }).catch(() => { if (active) setError('Kurulum bilgisi alınamadı. Sunucudaki dağıtım paketini kontrol edin.'); }); return () => { active = false; }; }, [admin, request]);
  // The example points at the console address the operator is using, not a fixed organisation host.
  const server = window.location.protocol === 'https:' ? window.location.origin : 'https://wfi-sunucu.domain';
  const command = `.\\WFI-Agent-Setup.exe --silent --server ${server} --device-id <CIHAZ-GUID> --token-file C:\\Secure\\wfi-token.txt`;
  return <div className="agent-hub">
    <div className="hub-tabs" role="tablist" aria-label="Ajan yönetimi"><button role="tab" id="download-tab" aria-selected={tab === 'download'} aria-controls="download-panel" onClick={() => setTab('download')}><Download size={18}/>Ajan kurulumu</button><button role="tab" id="network-tab" aria-selected={tab === 'network'} aria-controls="network-panel" onClick={() => setTab('network')}><Network size={18}/>Ağ Erişimleri</button></div>
    <section role="tabpanel" id="download-panel" aria-labelledby="download-tab" hidden={tab !== 'download'}>
      <div className="panel download-hero"><div className="download-icon"><ShieldCheck size={40}/></div><div><span className="rule">WINDOWS x64 · KURUM İÇİ AJAN</span><h2>Makinenizi WFI’ye bağlayın</h2><p>Tek kurulum dosyası. Kanıt toplama istemcide, yapay zekâ sunucunuzda.</p><div className="hub-badges"><span className="tag">{info ? `v${info.version}` : 'Paket bilgisi bekleniyor'}</span>{info && <span className="tag">{(info.size / 1048576).toFixed(1)} MB</span>}<span className="tag">Windows servisi</span></div></div><div>{admin && info ? <a className="primary download-action" href="/api/v1/distribution/agent" download><Download size={20}/>Windows ajanını indir</a> : <p>{admin ? (error ? 'Paket bilgisi kullanılamıyor.' : 'Paket yükleniyor…') : 'İndirme için Admin rolü gerekli.'}</p>}</div></div>
      {error && <div className="error" role="alert">{error}</div>}
      <div className="hub-grid"><section className="panel"><h2>Pencereli kurulum</h2><ol><li>EXE’yi yönetici olarak çalıştırın.</li><li>WFI HTTPS adresini ve WFI Admin hesabını girin.</li><li>Makine otomatik kaydolur. İlk bağlantıdan sonra envanterde online görünür.</li></ol><p className="hint">Ek ön koşul yok: Windows 10/11 ve Windows Server ile gelen Windows PowerShell 5.1 ve .NET Framework 4.8 kullanılır; ajanın .NET 10 runtime’ı dosyanın içindedir. EXE şu anda Authenticode imzasızdır.</p>{info && <details><summary>SHA256 bütünlük değeri</summary><code className="hash-value">{info.sha256}</code></details>}</section>
      <section className="panel"><h2><Terminal size={22}/> Sessiz dağıtım</h2><p>Konsolda cihaz kaydı oluşturup tek kullanımlık token’ı yalnız SYSTEM/Administrators erişimli yerel dosyaya yazın. Komutu yükseltilmiş dağıtım hesabıyla çalıştırın.</p><pre className="install-command">{command}</pre><button onClick={async () => { try { await navigator.clipboard.writeText(command); setCopied(true); } catch { setError('Kopyalama kullanılamıyor; komutu seçerek kopyalayın.'); } }}>{copied ? 'Kopyalandı' : 'Komutu kopyala'}</button><p className="hint">Adres ve GUID örnektir. Token’ı komut satırına veya pakete eklemeyin.</p><details><summary>Parametreler ve çıkış kodları</summary><p><code>--upgrade</code>: mevcut kimlikle yükseltme; aynı server/device-id, token dosyası gerekmez. <code>--bigfix-sha256</code>: onaylı reinstall.cmd hash değeri.</p><p>0: kurulum tamamlandı · 2: parametre hatası · 3: token dosyası/izin hatası · 4: yeni kurulum/yükseltme durumu uyuşmuyor · 10: kurulum başarısız. 0, sunucu bağlantısının doğrulandığı anlamına gelmez; envanter heartbeat’ini kontrol edin.</p></details></section></div>
    </section>
    <section role="tabpanel" id="network-panel" aria-labelledby="network-tab" hidden={tab !== 'network'}>
      <section className="panel"><span className="rule">BAĞLANTI GEREKSİNİMLERİ · CANLI TEST DEĞİL</span><h2>Ağ Erişimleri</h2><div className="network-flow"><span>Windows ajanı</span><ArrowRight aria-hidden="true"/><strong>HTTPS 443</strong><ArrowRight aria-hidden="true"/><span>WFI sunucusu</span></div><p>İstemcide WFI için inbound port açılmaz. Sunucudan istemciye WinRM, WMI, RPC veya SMB bağlantısı gerekmez.</p><div className="table-wrap"><table><thead><tr><th>Kaynak</th><th>Hedef</th><th>Port / protokol</th><th>Kuralın amacı</th></tr></thead><tbody>{rules.map(row => <tr key={row[0] + row[1]}>{row.map(cell => <td key={cell}>{cell}</td>)}</tr>)}</tbody></table></div><p className="hint">Kuralları yalnız ilgili kaynak/hedef IP gruplarına sınırlandırın. BigFix reinstall.cmd’nin kendi dağıtım hedefleri ayrıca incelenmelidir. TCP erişimi, TLS sertifikası ve uygulama yetkisinin doğrulandığı anlamına gelmez.</p></section>
    </section>
  </div>;
}
