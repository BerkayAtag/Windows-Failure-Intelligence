import { useEffect, useState } from 'react';
type Health = { sampledAt: string; domain: string; activeDiagnostics: number; oldestPendingSeconds: number; completedLast24Hours: number; failedLast24Hours: number; modelCapacity: number; telemetryConfigured: boolean };
export function OperationsHealth({ request }: { request: <T>(path: string, init?: RequestInit) => Promise<T> }) {
  const [data, setData] = useState<Health>(); const [error, setError] = useState(''); const [revision, setRevision] = useState(0);
  useEffect(() => { const controller = new AbortController(); setError(''); setData(undefined);
    request<Health>('/operations/health', { signal: controller.signal }).then(x => { if (!controller.signal.aborted) setData(x); }).catch(e => { if (!controller.signal.aborted) setError(e.message); });
    return () => controller.abort();
  }, [request, revision]);
  return <section className="panel" aria-label="Sistem sağlığı"><div className="section-head"><h2>İş kuyruğu ve model kapasitesi</h2><button onClick={() => setRevision(x => x + 1)}>Yenile</button></div>
    {error ? <p className="error" role="alert">{error}</p> : !data ? <p role="status">Sağlık bilgisi yükleniyor…</p> : <>
      <p>{data.domain} · Ölçüm: {new Date(data.sampledAt).toLocaleString('tr-TR')}</p><div className="table-wrap"><table><thead><tr><th>Ölçüt</th><th>Değer</th></tr></thead><tbody>
      <tr><td>Aktif teşhis</td><td>{data.activeDiagnostics}</td></tr><tr><td>En eski aktif işin yaşı</td><td>{Math.round(data.oldestPendingSeconds)} saniye</td></tr>
      <tr><td>Son 24 saatte tamamlanan teşhis</td><td>{data.completedLast24Hours}</td></tr><tr><td>Son 24 saatte başarısız teşhis</td><td>{data.failedLast24Hours}</td></tr>
      <tr><td>Yapılandırılmış ortak model slotu</td><td>{data.modelCapacity}</td></tr><tr><td>OTLP aktarım yapılandırması</td><td>{data.telemetryConfigured ? 'Tanımlı; teslimat doğrulaması değildir' : 'Kapalı'}</td></tr>
      </tbody></table></div><p className="hint">Veriler oturum domain'ine aittir. Model kapasitesi yapılandırma değeridir; yük testi veya anlık model erişilebilirliği sonucu değildir.</p>
    </>}
  </section>;
}
