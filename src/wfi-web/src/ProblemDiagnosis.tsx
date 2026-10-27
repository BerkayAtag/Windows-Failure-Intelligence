import { useEffect, useState } from 'react';

type Request = <T>(path: string, init?: RequestInit) => Promise<T>;
type Diagnosis = { id: string; status: string; error?: string; analysis?: { conclusion: string; modelUsed: string; confidenceScore: number } };
const states: Record<string, string> = { pending: 'Ajan bağlantısı bekleniyor', running: 'Makineden kanıt toplanıyor', evidence_received: 'Sunucu AI kuyruğunda', analyzing: 'Sunucu AI analiz ediyor', completed: 'Analiz tamamlandı', failed: 'Analiz tamamlanamadı' };

export function ProblemDiagnosis({ request, domain, allowed, openDevices }: { request: Request; domain: string; allowed: boolean; openDevices: () => void }) {
  const [job, setJob] = useState<Diagnosis>(), [error, setError] = useState(''), [busy, setBusy] = useState(false);
  useEffect(() => {
    if (!job || ['completed', 'failed'].includes(job.status)) return;
    let active = true, pending = false;
    const timer = setInterval(async () => {
      if (pending || document.hidden) return;
      pending = true;
      try { const next = await request<Diagnosis>('/diagnostics/' + job.id); if (active) { setJob(next); setError(''); } }
      catch (e) { if (active) setError((e as Error).message); }
      finally { pending = false; }
    }, 5000);
    return () => { active = false; clearInterval(timer); };
  }, [job?.id, job?.status, request]);
  return <section className="panel problem-diagnosis"><h2>Makinede sorun teşhisi</h2>
    <p>Makineyi ve yaşanan sorunu belirtin. WFI ajanı seçilen kapsamdaki kanıtları toplar; kurum içindeki sunucu AI’si sorun açıklamanızla birlikte değerlendirir.</p>
    <form onSubmit={async e => {
      e.preventDefault(); setBusy(true); setError('');
      const form = new FormData(e.currentTarget);
      try { setJob(await request<Diagnosis>('/diagnostics', { method: 'POST', body: JSON.stringify({ hostname: String(form.get('hostname')).trim(), scanType: form.get('scanType'), problemDescription: form.get('problemDescription') }) })); }
      catch (e) { setError((e as Error).message); } finally { setBusy(false); }
    }}>
      <div className="fields"><label>Makine adı<input name="hostname" required maxLength={63} pattern="[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?" placeholder="PC-IST-001"/></label><label>Domain<input value={domain} readOnly/></label></div>
      <label>Teşhis kapsamı<select aria-label="Teşhis kapsamı" name="scanType"><option value="FullSystemScan">Genel</option><option value="BigFixScan">BigFix</option><option value="EventLogScan">Windows olayları</option><option value="UpdateScan">Windows Update</option><option value="SccmScan">SCCM</option><option value="NetworkScan">Ağ / DNS</option></select></label>
      <label>Sorun açıklaması<textarea name="problemDescription" required maxLength={4000} rows={4} placeholder="Sorun ne zaman başladı? Hangi işlem başarısız oluyor? Parola veya gizli bilgi yazmayın."/></label>
      <p className="hint">Kayıtlı ve bağlı WFI ajanı gerekir. Onarım, cihaz otomasyon politikası ve kanıtla eşleşen sabit katalogla sınırlıdır. AI’nin ürettiği komutlar çalıştırılmaz.</p>
      {error && <div className="error" role="alert">{error}</div>}
      <div className="form-footer"><button type="button" onClick={openDevices}>Ajan kurulumu ve cihazlar</button><button className="primary" disabled={!allowed || busy || !!job && !['completed', 'failed'].includes(job.status)}>{busy ? 'Gönderiliyor…' : 'Analizi başlat'}</button></div>
    </form>
    {job && <div className="diagnostic-progress" role="status"><h3>{states[job.status] || job.status}</h3><p>İş: {job.id}</p>{job.error && <p>{job.error}</p>}{job.analysis && <><p>{job.analysis.conclusion}</p><p className="hint">Model: {job.analysis.modelUsed} · Model güveni: %{Math.round(job.analysis.confidenceScore * 100)}</p></>}<button onClick={openDevices}>Kanıtlar ve onarım durumunu aç</button>{!['completed', 'failed'].includes(job.status) && <button disabled={busy} onClick={async () => { setBusy(true); try { await request('/diagnostics/' + job.id + '/cancel', { method: 'POST' }); setJob({ ...job, status: 'failed' }); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }}>Teşhisi iptal et</button>}</div>}
  </section>;
}
