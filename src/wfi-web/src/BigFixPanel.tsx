import { useEffect, useState } from 'react';
type Request = <T>(path: string, init?: RequestInit) => Promise<T>;
type Verification = { executionId: string; status: string; beforeReportAt?: string; latestReportAt?: string; observedAt?: string };
type State = { configured: boolean; mappingCurrent: boolean; mapping?: { computerId: number; verifiedDnsName: string }; verification: Verification[] };
const labels: Record<string, string> = { PendingBaseline: 'Onarım öncesi merkez verisi bekleniyor', BaselineCaptured: 'Onarım öncesi merkez verisi alındı', BaselineUnavailable: 'Önceki merkez verisi alınamadı; karşılaştırma doğrulanamaz', AwaitingReport: 'Yerel düzeldi, merkez doğrulaması bekleniyor', ConnectorUnavailable: 'Merkez API erişilemiyor; istemci arızası kanıtı değildir', Verified: 'Yeni merkez raporu doğrulandı', TimedOut: 'Yerel sonuç ayrı; merkez doğrulaması zamanında gelmedi', LocalNotVerified: 'Yerel onarım doğrulanmadı' };
export function BigFixPanel({ request, deviceId, admin, analyst }: { request: Request; deviceId: string; admin: boolean; analyst: boolean }) {
  const [state, setState] = useState<State>(), [error, setError] = useState(''), [computerId, setComputerId] = useState(''), [confirmed, setConfirmed] = useState(false), [busy, setBusy] = useState(false), [revision, setRevision] = useState(0);
  const [probe, setProbe] = useState<{ computer: { lastReportAt: string; observedAt: string }; reportAgeSeconds: number }>();
  useEffect(() => { const controller = new AbortController(); setError(''); request<State>(`/devices/${deviceId}/bigfix`, { signal: controller.signal }).then(x => { if (!controller.signal.aborted) setState(x); }).catch(e => { if (!controller.signal.aborted) setError(e.message); }); return () => controller.abort(); }, [request, deviceId, revision]);
  async function run(fn: () => Promise<void>) { setBusy(true); setError(''); setProbe(undefined); try { await fn(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  const date = (value?: string) => value ? new Date(value).toLocaleString('tr-TR') : 'Veri yok';
  return <section className="finding"><div className="section-head"><h3>BigFix merkez doğrulaması</h3><button disabled={busy} onClick={() => setRevision(x => x + 1)}>Doğrulamaları yenile</button></div>
    {error && <p className="error" role="alert">{error}</p>}{!state && !error && <p role="status">Merkez yapılandırması yükleniyor…</p>}
    {state && <><p>{state.configured ? 'Sunucu connector ayarı mevcut. Bu bilgi canlı erişim sonucu değildir.' : 'BigFix REST connector sunucuda yapılandırılmamış. Yerel BESClient teşhisi kullanılabilir.'}</p>
      <p>{state.mapping ? `Computer ID: ${state.mapping.computerId} · ${state.mapping.verifiedDnsName} · ${state.mappingCurrent ? 'Eşleme güncel' : 'Eşleme yeniden doğrulanmalı'}` : 'Henüz doğrulanmış Computer ID eşlemesi yok.'}</p>
      {admin && state.configured && <form onSubmit={e => { e.preventDefault(); void run(async () => { await request(`/devices/${deviceId}/bigfix/mapping`, { method: 'POST', body: JSON.stringify({ computerId: Number(computerId), confirmIdentity: confirmed }) }); setRevision(x => x + 1); }); }}><label>BigFix Computer ID<input type="number" min="1" max="9007199254740991" required value={computerId} onChange={e => { setComputerId(e.target.value); setConfirmed(false); }}/></label><label className="check"><input type="checkbox" required checked={confirmed} onChange={e => setConfirmed(e.target.checked)}/>Bu Computer ID'nin seçili cihaza ait olduğunu kontrol ettim.</label><button disabled={busy || !confirmed}>Merkezden doğrula ve eşle</button></form>}
      {analyst && state.mappingCurrent && <button disabled={busy} onClick={() => void run(async () => setProbe(await request(`/devices/${deviceId}/bigfix/probe`, { method: 'POST' })))}>Merkez raporunu sorgula</button>}
      {probe && <p>Son merkez raporu: {date(probe.computer.lastReportAt)} · Alınma: {date(probe.computer.observedAt)} · Rapor yaşı: {Math.round(probe.reportAgeSeconds / 60)} dakika</p>}
      {state.verification.map(v => <article className="evidence" key={v.executionId}><strong>{labels[v.status] || v.status}</strong><p>Önceki rapor: {date(v.beforeReportAt)} · Son rapor: {date(v.latestReportAt)} · Alınma: {date(v.observedAt)}</p><small>Onarım: {v.executionId}</small></article>)}
      <p className="hint">BESClient Running, merkez raporlamasının kanıtı değildir. Merkez doğrulaması yeni rapor bekler; olayın kök nedeninin tamamen çözüldüğünü tek başına kanıtlamaz.</p>
    </>}
  </section>;
}
