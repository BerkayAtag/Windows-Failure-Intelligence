import { useState } from 'react';
export function AiFeedback({ id, request }: { id: string; request: <T>(path: string, init?: RequestInit) => Promise<T> }) {
  const [message, setMessage] = useState(''), [busy, setBusy] = useState(false);
  return <section className="finding"><h4>Olayın sonucu</h4><p className="hint">İnsan değerlendirmesi, ajan işlem doğrulamasından ayrıdır. Olumsuz/geçici geri bildirim bu analizin deneyim olarak kullanılmasını engeller; onarım yetkisi vermez.</p>
    {[['Resolved', 'Çözüldü'], ['NotResolved', 'Çözülmedi'], ['Temporary', 'Geçici düzeldi']].map(([outcome, label]) => <button key={outcome} disabled={busy} onClick={async () => { setBusy(true); try { await request(`/ai/analysis/${id}/feedback`, { method: 'POST', body: JSON.stringify({ outcome }) }); setMessage(`Geri bildirim kaydedildi: ${label}`); } catch (e) { setMessage((e as Error).message); } finally { setBusy(false); } }}>{label}</button>)}{message && <p role="status">{message}</p>}
  </section>;
}
