import { test, expect } from '@playwright/test';
test('campaign preview freezes targets and requires an explicit pilot confirmation', async ({ page }) => {
  await page.goto('/'); await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await expect(page.getByRole('button', { name: 'Onarım Kampanyaları', exact: true })).toBeVisible();
  const hostname = `campaign-ui-${Date.now()}`;
  const deviceId = await page.evaluate(async host => {
    const csrf = await (await fetch('/api/v1/auth/csrf')).json();
    const headers = { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token };
    const response = await fetch('/api/v1/devices/register', { method: 'POST', headers, body: JSON.stringify({ hostname: host }) });
    if (!response.ok) throw new Error('Synthetic device registration failed');
    const device = await response.json();
    const policy = await fetch(`/api/v1/devices/${device.id}/automation`, { method: 'PUT', headers, body: JSON.stringify({ mode: 'Enabled' }) });
    if (!policy.ok) throw new Error('Synthetic policy failed');
    return device.id as string;
  }, hostname);
  try {
    await page.getByRole('button', { name: 'Onarım Kampanyaları', exact: true }).click();
    await page.getByLabel('Kampanya adı', { exact: true }).fill(hostname);
    const books = await (await page.request.get('/api/v1/playbooks')).json();
    await page.getByRole('combobox', { name: 'Onaylı işlem', exact: true }).selectOption(books.find((b: { code: string }) => b.code === 'PB-DNS-FLUSH').id);
    const local = (date: Date) => new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    await page.getByLabel('Bakım başlangıcı', { exact: true }).fill(local(new Date(Date.now() + 300000)));
    await page.getByLabel('Bakım bitişi / son kullanma', { exact: true }).fill(local(new Date(Date.now() + 3600000)));
    await page.getByLabel('Cihaz ara', { exact: true }).fill(hostname);
    await page.getByRole('checkbox', { name: `Seç ${hostname}`, exact: true }).check();
    await page.getByRole('button', { name: 'Kapsamı önizle' }).click();
    await expect(page.getByRole('heading', { name: 'Kampanya önizlemesi' })).toBeVisible();
    await page.getByRole('button', { name: 'Taslak oluştur', exact: true }).click();
    const campaign = page.locator('article').filter({ has: page.getByRole('heading', { name: hostname, exact: true }) });
    await expect(campaign).toContainText('Taslak');
    await campaign.getByRole('button', { name: 'Pilotu başlat' }).click();
    await expect(campaign).toContainText('hedef kapsamını onaylıyor musunuz');
    await campaign.getByRole('button', { name: 'İşlemi onayla', exact: true }).click();
    await expect(campaign).toContainText('Pilot çalışıyor');
    await campaign.getByRole('button', { name: 'Kampanyayı iptal et' }).click();
    await campaign.getByRole('button', { name: 'İşlemi onayla', exact: true }).click();
    await expect(campaign).toContainText('İptal edildi');
  } finally {
    await page.evaluate(async id => { const csrf = await (await fetch('/api/v1/auth/csrf')).json(); await fetch(`/api/v1/devices/${id}/revoke`, { method: 'POST', headers: { 'X-CSRF-TOKEN': csrf.token } }); }, deviceId);
  }
});
