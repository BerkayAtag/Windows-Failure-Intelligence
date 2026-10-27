import { useEffect, useState } from 'react';
type Request = <T>(path: string, init?: RequestInit) => Promise<T>;
type Device = { id: string; hostname: string; status: string };
type Book = { id: string; title: string; riskLevel: string; isApproved: boolean };
type Campaign = { id: string; name: string; status: string; playbookCode: string; startsAt: string; endsAt: string };
type Preview = { targetCount: number; risk: string; offline: string[]; blocked: string[]; pilotCount: number; maxParallel: number };
const labels: Record<string, string> = { Draft: 'Taslak', Pilot: 'Pilot çalışıyor', AwaitingExpansion: 'Genişleme onayı bekliyor', Expanded: 'Genişletildi', Paused: 'Duraklatıldı', Cancelled: 'İptal edildi', Completed: 'Tamamlandı', Expired: 'Süresi doldu' };
export function Campaigns({ request }: { request: Request }) {
  const [devices, setDevices] = useState<Device[]>([]), [books, setBooks] = useState<Book[]>([]), [campaigns, setCampaigns] = useState<Campaign[]>([]);
  const [ids, setIds] = useState<string[]>([]), [name, setName] = useState(''), [book, setBook] = useState(''), [start, setStart] = useState(''), [end, setEnd] = useState('');
  const [search, setSearch] = useState(''), [page, setPage] = useState(1), [total, setTotal] = useState(0), [revision, setRevision] = useState(0);
  const [preview, setPreview] = useState<Preview>(), [error, setError] = useState(''), [notice, setNotice] = useState(''), [busy, setBusy] = useState(false), [loaded, setLoaded] = useState(false);
  const [confirmation, setConfirmation] = useState<{ id: string; action: string }>();
  useEffect(() => { let active = true; Promise.all([request<Book[]>('/playbooks'), request<Campaign[]>('/campaigns')]).then(([b, c]) => { if (active) { setBooks(b.filter(x => x.isApproved)); setCampaigns(c); setLoaded(true); } }).catch(e => { if (active) setError(e.message); }); return () => { active = false; }; }, [request, revision]);
  useEffect(() => { const controller = new AbortController(); request<{ items: Device[]; total: number }>(`/devices?page=${page}&search=${encodeURIComponent(search)}`, { signal: controller.signal }).then(x => { if (!controller.signal.aborted) { setDevices(x.items); setTotal(x.total); } }).catch(e => { if (!controller.signal.aborted) setError(e.message); }); return () => controller.abort(); }, [request, search, page]);
  function draft() { return { name, playbookId: book, deviceIds: ids, startsAt: new Date(start).toISOString(), endsAt: new Date(end).toISOString(), expiresAt: new Date(end).toISOString(), timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone, pilotLimit: 5, maxParallel: 2 }; }
  async function run(action: () => Promise<void>) { setBusy(true); setError(''); setNotice(''); try { await action(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  function invalidate() { setPreview(undefined); }
  return <div className="remote-console"><section className="panel"><h2>Kontrollü onarım kampanyası</h2><p>Önce en fazla 5 cihazlık pilot, ardından ayrı genişleme onayı. Cihaz politikası kapalıysa işlem başlamaz. İlk başarısız veya belirsiz sonuç kampanyayı duraklatır.</p>
    {error && <p className="error" role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    <form onSubmit={e => { e.preventDefault(); void run(async () => setPreview(await request<Preview>('/campaigns/preview', { method: 'POST', body: JSON.stringify(draft()) }))); }}>
      <div className="fields"><label>Kampanya adı<input required maxLength={120} value={name} onChange={e => { setName(e.target.value); invalidate(); }}/></label><label>Onaylı işlem<select required value={book} onChange={e => { setBook(e.target.value); invalidate(); }}><option value="">İşlem seçin</option>{books.map(b => <option key={b.id} value={b.id}>{b.title} · {b.riskLevel}</option>)}</select></label></div>
      <div className="fields"><label>Bakım başlangıcı<input required type="datetime-local" value={start} onChange={e => { setStart(e.target.value); invalidate(); }}/></label><label>Bakım bitişi / son kullanma<input required type="datetime-local" value={end} onChange={e => { setEnd(e.target.value); invalidate(); }}/></label></div>
      <p className="hint">Saat dilimi: {Intl.DateTimeFormat().resolvedOptions().timeZone}. Tek bakım aralığı; en fazla 30 gün ileriye planlanabilir.</p>
      <label>Cihaz ara<input value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}/></label><p>{ids.length} cihaz seçildi · Hedefler taslak oluşturulurken dondurulur.</p>
      <div className="table-wrap"><table><thead><tr><th>Seç</th><th>Cihaz</th><th>Durum</th></tr></thead><tbody>{devices.map(d => <tr key={d.id}><td><input type="checkbox" aria-label={`Seç ${d.hostname}`} checked={ids.includes(d.id)} onChange={e => { setIds(current => e.target.checked ? [...current, d.id] : current.filter(x => x !== d.id)); invalidate(); }}/></td><td>{d.hostname}</td><td>{d.status}</td></tr>)}</tbody></table></div>
      <div className="pagination"><button type="button" disabled={page === 1} onClick={() => setPage(x => x - 1)}>Önceki</button><span>{page}</span><button type="button" disabled={page * 50 >= total} onClick={() => setPage(x => x + 1)}>Sonraki</button></div>
      <button className="primary" disabled={busy || ids.length === 0 || ids.length > 1000}>Kapsamı önizle</button>
    </form>
    {preview && <section className="finding"><h3>Kampanya önizlemesi</h3><p>{preview.targetCount} hedef · Pilot {preview.pilotCount} · Aynı anda {preview.maxParallel} · Risk {preview.risk}</p><p>{preview.offline.length} çevrimdışı/bekleyen cihaz · {preview.blocked.length} politika engeli</p><button disabled={busy || preview.blocked.length > 0} onClick={() => void run(async () => { await request('/campaigns', { method: 'POST', body: JSON.stringify(draft()) }); setPreview(undefined); setRevision(x => x + 1); setNotice('Taslak oluşturuldu. Pilot henüz başlamadı.'); })}>Taslak oluştur</button></section>}
  </section><section className="panel"><div className="section-head"><h2>Kampanyalar</h2><button disabled={busy} onClick={() => setRevision(x => x + 1)}>Listeyi yenile</button></div>
    {!loaded && !error && <p role="status">Kampanyalar yükleniyor…</p>}{loaded && !campaigns.length && <p>Henüz kampanya yok.</p>}
    {campaigns.map(c => <article className="finding" key={c.id}><h3>{c.name}</h3><p>{c.playbookCode} · {labels[c.status] || c.status}</p><p>{new Date(c.startsAt).toLocaleString('tr-TR')} – {new Date(c.endsAt).toLocaleString('tr-TR')}</p>
      {c.status === 'Draft' && <button disabled={busy} onClick={() => setConfirmation({ id: c.id, action: 'start' })}>Pilotu başlat</button>}
      {c.status === 'AwaitingExpansion' && <button disabled={busy} onClick={() => setConfirmation({ id: c.id, action: 'expand' })}>Genişlemeyi onayla</button>}
      {['Pilot', 'Expanded'].includes(c.status) && <button disabled={busy} onClick={() => setConfirmation({ id: c.id, action: 'pause' })}>Duraklat</button>}
      {!['Completed', 'Cancelled', 'Expired'].includes(c.status) && <button disabled={busy} onClick={() => setConfirmation({ id: c.id, action: 'cancel' })}>Kampanyayı iptal et</button>}
      {confirmation?.id === c.id && <div className="enrollment"><p>{confirmation.action === 'start' || confirmation.action === 'expand' ? 'Seçili katalog işleminin riskini ve hedef kapsamını onaylıyor musunuz? Bu onay yüksek riskli işlem onayını da içerir.' : 'Çalışan işlem zorla sonlandırılmaz; yeni işler durdurulur.'}</p><button disabled={busy} onClick={() => void run(async () => { await request(`/campaigns/${c.id}/command`, { method: 'POST', body: JSON.stringify({ action: confirmation.action, highRiskConfirmed: true }) }); setConfirmation(undefined); setRevision(x => x + 1); })}>İşlemi onayla</button><button disabled={busy} onClick={() => setConfirmation(undefined)}>Vazgeç</button></div>}
    </article>)}<p className="hint">En son 50 kampanya gösterilir. Duraklatılan kampanya otomatik devam etmez; incelemeden sonra kalan hedeflerle yeni kampanya oluşturun.</p>
  </section></div>;
}
