import { AiFeedback } from './AiFeedback';
import { BigFixPanel } from './BigFixPanel';
import { useEffect, useState } from 'react';

type Request = <T>(path: string, init?: RequestInit) => Promise<T>;
type Device = { id: string; hostname: string; domain: string; status: string; lastHeartbeat: string; agentVersion: string; automationMode: string };
type Diagnostic = { id: string; status: string; scanType: string; error?: string };
type Remediation = { id: string; playbookCode: string; playbookTitle: string; status: string; automatic: boolean; executionOutput?: string; validationResult?: string };
type Activity = { diagnostics: Diagnostic[]; remediations: Remediation[] };
type Analysis = { id?: string; versionsJson?: string; conclusion: string; modelUsed: string; status: string; hypothesesJson: string; recommendedPlaybooksJson: string; confidenceScore?: number; missingEvidenceJson?: string; learningContextJson?: string; error?: string };
type Playbook = { code: string; title: string; riskLevel: string; version: string };
function array(value?: string): unknown[] { try { const parsed = JSON.parse(value || '[]'); return Array.isArray(parsed) ? parsed : []; } catch { return []; } }
const labels: Record<string, string> = { pending: 'Bekliyor', running: 'Kanıt toplanıyor', evidence_received: 'AI sırasında', analyzing: 'AI analiz ediyor', completed: 'Tamamlandı', failed: 'Başarısız', Approved: 'Ajan bekleniyor', PendingApproval: 'Yönetici onayı bekliyor', Executing: 'Çalışıyor', Success: 'İşlem doğrulandı', Failed: 'Başarısız', Unknown: 'Sonuç belirsiz', Cancelled: 'Başlamadan iptal edildi' };

export function RemoteConsole({ request, roles }: { request: Request; roles: string[] }) {
  const admin = roles.includes('Admin'), analyst = admin || roles.includes('Analyst');
  const [devices, setDevices] = useState<Device[]>([]), [total, setTotal] = useState(0), [page, setPage] = useState(1), [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Device>(), [activity, setActivity] = useState<Activity>({ diagnostics: [], remediations: [] });
  const [diagnosticId, setDiagnosticId] = useState(''), [analysis, setAnalysis] = useState<Analysis>();
  const [models, setModels] = useState<string[]>([]), [model, setModel] = useState(''), [activeModel, setActiveModel] = useState('');
  const [enabled, setEnabled] = useState(false), [applyAll, setApplyAll] = useState(false), [mode, setMode] = useState('Inherit');
  const [playbooks, setPlaybooks] = useState<Playbook[]>([]), [playbook, setPlaybook] = useState(''), [scan, setScan] = useState('FullSystemScan');
  const [hostname, setHostname] = useState(''), [enrollment, setEnrollment] = useState<{ id: string; enrollmentToken: string }>();
  const [error, setError] = useState(''), [notice, setNotice] = useState(''), [busy, setBusy] = useState(false), [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    request<{ model: string; availableModels: string[] }>('/ai/config').then(x => { if (active) { setModels(x.availableModels); setModel(x.model); setActiveModel(x.model); } }).catch(e => { if (active) setError(e.message); });
    request<Playbook[]>('/playbooks').then(x => { if (active) setPlaybooks(x); }).catch(e => { if (active) setError(e.message); });
    if (admin) request<{ enabled: boolean }>('/automation').then(x => { if (active) setEnabled(x.enabled); }).catch(e => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [request, admin]);
  useEffect(() => {
    let active = true, pending = false;
    async function refresh() {
      if (pending || document.hidden) return; pending = true;
      try {
        const list = await request<{ items: Device[]; total: number }>(`/devices?page=${page}&search=${encodeURIComponent(search)}`);
        if (active) { setDevices(list.items); setTotal(list.total); }
        if (selected) {
          const detail = await request<Activity>(`/devices/${selected.id}/activity`); if (active) setActivity(detail);
        }
        if (diagnosticId) { const detail = await request<{ analysis?: Analysis }>(`/diagnostics/${diagnosticId}`); if (active) setAnalysis(detail.analysis); }
      } catch (e) { if (active) setError((e as Error).message); } finally { pending = false; }
    }
    const start = setTimeout(refresh, 250), timer = setInterval(refresh, 10000);
    return () => { active = false; clearTimeout(start); clearInterval(timer); };
  }, [request, selected?.id, diagnosticId, page, search, revision]);
  async function act(work: () => Promise<unknown>, message: string) {
    if (busy) return; setBusy(true); setError(''); setNotice('');
    try { await work(); setNotice(message); setRevision(x => x + 1); } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  const post = (path: string, body?: unknown, method = 'POST') => request(path, { method, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
  const chosen = playbooks.find(p => p.code === playbook);
  return <div className="remote-console">
    {error && <div className="error" role="alert">{error}</div>}{notice && <div className="notice" role="status">{notice}</div>}
    <div className="remote-settings">
      <section className="panel"><h2>Sunucu yapay zekâ modeli</h2><p>Etkin model: <strong>{activeModel || 'Yükleniyor…'}</strong></p>
        <label>Ollama modeli<select aria-label="Ollama modeli" value={model} onChange={e => setModel(e.target.value)} disabled={!admin || busy}>
          {!models.includes(model) && <option value={model}>{model || 'Model bulunamadı'}</option>}{models.map(x => <option key={x}>{x}</option>)}</select></label>
        {!models.length && <p className="hint">Ollama erişilemiyor veya kurulu model yok. Sunucu bağlantısını kontrol edin.</p>}
        {admin && <button disabled={busy || !models.includes(model)} onClick={() => act(async () => { const result = await request<{ activeModel: string }>('/ai/model', { method: 'POST', body: JSON.stringify({ model }) }); setActiveModel(result.activeModel); }, 'Model tercihi kaydedildi.')}>Modeli kaydet</button>}
      </section>
      {admin && <section className="panel"><h2>Genel otomatik mod</h2><p>Bu domain’deki cihazlar için varsayılan. Ajan periyodik teşhis yapar; yalnızca kanıtla eşleşen otomatik izinli katalog işlemleri çalışabilir.</p>
        <label className="check"><input type="checkbox" checked={enabled} onChange={e => setEnabled(e.target.checked)}/>Otomatik modu etkinleştir</label>
        <label className="check"><input type="checkbox" checked={applyAll} onChange={e => setApplyAll(e.target.checked)}/>Tüm cihazlara uygula; cihaz istisnalarını kaldır</label>
        <button disabled={busy} onClick={() => act(() => post('/automation', { enabled, applyToAll: applyAll }, 'PUT'), 'Genel otomatik mod kaydedildi.')}>Genel ayarı kaydet</button>
        <p className="hint">Teşhis aralığı 15 dakika; cihaz başına otomatik işlem bekleme süresi 24 saat. Çalışmaya başlamış işlemler ayar kapatılınca geriye alınmaz.</p>
      </section>}
    </div>
    <section className="panel"><div className="section-head"><h2>Cihaz envanteri</h2><input aria-label="Ajan cihazı ara" placeholder="Hostname veya IP ara" maxLength={63} value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}/></div>
      {admin && <form className="register-device" onSubmit={e => { e.preventDefault(); act(async () => { const result = await request<{ id: string; enrollmentToken: string }>('/devices/register', { method: 'POST', body: JSON.stringify({ hostname }) }); setEnrollment(result); setHostname(''); }, 'Cihaz kaydı hazır. Token ile ajanı kurun.'); }}>
        <label>Yeni cihaz hostname<input required maxLength={63} value={hostname} onChange={e => setHostname(e.target.value)}/></label><button disabled={busy}>Cihaz kaydet</button></form>}
      {enrollment && <div className="enrollment"><h3>Ajan kayıt bilgileri</h3><p>Token bir saat geçerlidir ve bir kez kullanılır. Bu alan kapatılınca tekrar gösterilmez.</p><label>Cihaz kimliği<input readOnly value={enrollment.id}/></label><label>Kayıt token’ı<input type="password" autoComplete="off" readOnly value={enrollment.enrollmentToken} onFocus={e => e.target.select()}/></label><button onClick={() => setEnrollment(undefined)}>Bilgileri kapat</button></div>}
      <div className="table-wrap"><table><thead><tr><th>Cihaz</th><th>Durum</th><th>Son bağlantı</th><th>Otomasyon</th><th>İşlem</th></tr></thead><tbody>{devices.map(d => <tr key={d.id}><td>{d.hostname}<small>{d.domain}</small></td><td>{d.status}</td><td>{d.agentVersion ? new Date(d.lastHeartbeat).toLocaleString('tr-TR') : 'Henüz bağlanmadı'}</td><td>{d.automationMode}</td><td><button onClick={() => { setSelected(d); setMode(d.automationMode); setAnalysis(undefined); setDiagnosticId(''); setActivity({ diagnostics: [], remediations: [] }); }}>Cihazı aç</button></td></tr>)}</tbody></table></div>
      {!devices.length && <p className="empty">Kayıtlı cihaz bulunamadı.</p>}
      <div className="pagination"><span>{total} cihaz</span><button disabled={page <= 1} onClick={() => setPage(x => x - 1)}>Önceki</button><span>{page}</span><button disabled={page * 50 >= total} onClick={() => setPage(x => x + 1)}>Sonraki</button></div>
    </section>
    {selected && <BigFixPanel key={selected.id} request={request} deviceId={selected.id} admin={admin} analyst={analyst}/>}
    {selected && <section className="panel"><div className="section-head"><h2>{selected.hostname} · Uzaktan yönetim</h2><button onClick={() => { setSelected(undefined); setAnalysis(undefined); setDiagnosticId(''); }}>Cihazı kapat</button></div>
      {admin && selected.status === 'pending' && <button disabled={busy} onClick={() => act(async () => { setEnrollment(await request<{ id: string; enrollmentToken: string }>(`/devices/${selected.id}/enrollment`, { method: 'POST' })); }, 'Yeni kayıt bilgileri envanter bölümünde. Eski token geçersizdir.')}>Kayıt token’ını yenile</button>}
      {admin && <div className="remote-actions"><label>Cihaz otomatik modu<select aria-label="Cihaz otomatik modu" value={mode} onChange={e => setMode(e.target.value)}><option value="Inherit">Genel ayarı kullan</option><option value="Enabled">Bu cihazda açık</option><option value="Disabled">Bu cihazda kapalı</option></select></label><button disabled={busy} onClick={() => act(() => post(`/devices/${selected.id}/automation`, { mode }, 'PUT'), 'Cihaz otomatik modu kaydedildi.')}>Cihaz ayarını kaydet</button></div>}
      {analyst && <div className="remote-actions"><label>Teşhis kapsamı<select value={scan} onChange={e => setScan(e.target.value)}><option value="FullSystemScan">Genel teşhis</option><option value="EventLogScan">Windows olayları</option><option value="UpdateScan">Windows Update</option><option value="SccmScan">SCCM</option><option value="BigFixScan">BigFix</option><option value="NetworkScan">Ağ bilgileri</option></select></label><button className="primary" disabled={busy} onClick={() => act(() => post('/diagnostics', { hostname: selected.hostname, scanType: scan }), 'Teşhis sıraya alındı; ajan bağlantısı bekleniyor.')}>Uzaktan teşhis başlat</button></div>}
      <h3>Son teşhisler</h3>{activity.diagnostics.map(d => <div className="finding" key={d.id}><div className="remote-row"><span>{d.scanType} · {labels[d.status] || d.status}</span><button onClick={() => { setDiagnosticId(d.id); setAnalysis(undefined); }}>Teşhis sonucu</button>
        {analyst && ['pending', 'running', 'evidence_received', 'analyzing'].includes(d.status) && <button disabled={busy} onClick={() => act(() => post(`/diagnostics/${d.id}/cancel`), 'Teşhis iptal edildi. Bu teşhisten gelecek sonuç işlenmeyecek.')}>Teşhisi iptal et</button>}</div>
        {d.error && <p>{d.error}</p>}</div>)}
      {analyst && activity.diagnostics.some(d => ['pending', 'running', 'evidence_received', 'analyzing'].includes(d.status)) && <p className="hint">İptal, devam eden kanıt toplama veya model çağrısını hemen durdurmayabilir; sonucu işlemez. Daha önce oluşturulmuş düzeltme işlerini durdurmaz.</p>}
      {diagnosticId && !analysis && <p>Bu teşhis için AI sonucu bulunmuyor. Yukarıdaki iş durumunu ve varsa hata açıklamasını kontrol edin.</p>}
      {analysis && <article className="finding"><h3>AI değerlendirmesi · {analysis.modelUsed}</h3><p>{analysis.conclusion}</p>{analysis.error && <div className="error">{analysis.error}</div>}
        <p>Modelin bildirdiği güven: {analysis.status === 'completed' && typeof analysis.confidenceScore === 'number' && Number.isFinite(analysis.confidenceScore) && analysis.confidenceScore >= 0 && analysis.confidenceScore <= 1 ? `%${Math.round(analysis.confidenceScore * 100)}` : 'Kullanılabilir değil'}. Bu değer ölçülmüş doğruluk veya başarı olasılığı değildir.</p>
        {array(analysis.hypothesesJson).map((h, i) => { const item = h as Record<string, unknown>; return item && typeof item === 'object' ? <div key={i}><h4>{String(item.title || '')}</h4><p>{String(item.explanation || '')}</p>
          {Array.isArray(item.evidenceCitations) && item.evidenceCitations.some(x => typeof x === 'string') && <><h5>Modelin atıf yaptığı kanıtlar</h5><ul>{item.evidenceCitations.filter(x => typeof x === 'string').map((x, n) => <li key={n}>{x}</li>)}</ul></>}
          {Array.isArray(item.verificationSteps) && item.verificationSteps.some(x => typeof x === 'string') && <><h5>Önerilen doğrulama adımları</h5><ul>{item.verificationSteps.filter(x => typeof x === 'string').map((x, n) => <li key={n}>{x}</li>)}</ul></>}
        </div> : null; })}
        {analyst && analysis.id && <AiFeedback key={analysis.id} id={analysis.id} request={request}/>}<h4>Geçmişten sunulan örnekler</h4><p className="hint">{array(analysis.learningContextJson).length} doğrulanmış geçmiş işlem modele bağlam olarak sunuldu. Bunlar yeni olay için onarım yetkisi oluşturmaz.</p>{array(analysis.learningContextJson).map((x, i) => { const item = x as { executionId?: string; playbookCode?: string }; return <p key={i}>{item.playbookCode} · {item.executionId}</p>; })}<h4>Eksik kanıtlar</h4>{array(analysis.missingEvidenceJson).some(x => typeof x === 'string') ? <ul>{array(analysis.missingEvidenceJson).filter(x => typeof x === 'string').map((x, i) => <li key={i}>{x}</li>)}</ul> : <p>Model ek kanıt belirtmedi; bu, kanıtların eksiksiz olduğu anlamına gelmez.</p>}
        <p className="hint">Model çıktısı hipotezdir. Öneri, düzeltmenin çalıştığı veya arızanın giderildiği anlamına gelmez.</p>
        {array(analysis.recommendedPlaybooksJson).filter(x => typeof x === 'string').map(code => <button key={String(code)} onClick={() => setPlaybook(String(code))}>{playbooks.find(p => p.code === code)?.title || String(code)}</button>)}
        {analyst && <button disabled={busy} onClick={() => act(() => post(`/ai/analyze/${diagnosticId}`), 'AI analizi yeniden sıraya alındı.')}>Yeniden analiz et</button>}
      </article>}
      {analyst && <div className="finding"><h3>Manuel düzeltme</h3><label>Düzeltme işlemi<select aria-label="Düzeltme işlemi" value={playbook} onChange={e => setPlaybook(e.target.value)}><option value="">İşlem seçin</option>{playbooks.map(p => <option key={p.code} value={p.code}>{p.title} · {p.riskLevel}</option>)}</select></label>
        {chosen && <p>{chosen.title} — Risk: {chosen.riskLevel}. İşlem cihazın durumunu değiştirebilir. Sonuç ajan tarafından doğrulanır; otomatik geri alma yoktur.</p>}
        <button disabled={busy || !chosen || (chosen.riskLevel === 'High' && !admin)} onClick={() => act(() => post('/remediations', { playbookCode: playbook, hostname: selected.hostname, domain: selected.domain }), 'Düzeltme isteği kaydedildi. Ajan/onay durumunu izleyin.')}>Seçili düzeltmeyi iste</button>
      </div>}
      <h3>Düzeltme geçmişi</h3>{activity.remediations.map(r => <div className="finding" key={r.id}><strong>{r.playbookTitle} · {labels[r.status] || r.status}</strong><p>{r.automatic ? 'Otomatik politika' : 'Manuel istek'}</p>
        {analyst && ['Approved', 'PendingApproval'].includes(r.status) && <button disabled={busy} onClick={() => act(() => post(`/remediations/${r.id}/cancel`), 'Düzeltme başlamadan iptal edildi.')}>Bekleyen düzeltmeyi iptal et</button>}
        {admin && r.status === 'PendingApproval' && <button disabled={busy} onClick={() => act(() => post(`/remediations/${r.id}/approve`), 'Düzeltme onaylandı; ajan bekleniyor.')}>Düzeltmeyi onayla</button>}
        {(r.executionOutput || (r.validationResult && r.validationResult !== 'Pending')) && <pre className="evidence">{r.executionOutput}{'\n'}{r.validationResult}</pre>}</div>)}
    </section>}
  </div>;
}

