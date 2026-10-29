import { test, expect } from '@playwright/test';
import os from 'node:os';
import path from 'node:path';

test('problem-first diagnosis, BigFix, protected agent download and dark options', async ({ page }) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  await page.goto('/'); await expect(page).toHaveTitle(/WFI/);
  await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await expect(page.getByRole('heading', { name: 'Makinede sorun teşhisi' })).toBeVisible();
  await expect(page.getByLabel('Log dosyası', { exact: true })).not.toBeVisible();
  await expect(page.getByRole('option', { name: 'Genel', exact: true })).toHaveCount(1);
  await expect(page.getByText('Genel (BigFix dahil)', { exact: true })).toHaveCount(0);
  await page.getByLabel('Tema', { exact: true }).selectOption('dark');
  const colors = await page.getByLabel('Tema', { exact: true }).locator('option').evaluateAll(options => options.map(o => ({ color: getComputedStyle(o).color, bg: getComputedStyle(o).backgroundColor })));
  const luminance = (rgb: string) => {
    const values = rgb.match(/\d+/g)!.slice(0, 3).map(Number).map(v => { const s = v / 255; return s <= .04045 ? s / 12.92 : ((s + .055) / 1.055) ** 2.4; });
    return values[0] * .2126 + values[1] * .7152 + values[2] * .0722;
  };
  for (const c of colors) {
    const values = [luminance(c.color), luminance(c.bg)].sort((a,b) => a-b);
    expect((values[1] + .05) / (values[0] + .05)).toBeGreaterThanOrEqual(4.5);
  }
  await page.getByRole('button', { name: 'Ajan kurulumu ve cihazlar' }).click();
  const download = await page.request.get('/api/v1/distribution/agent');
  expect(download.status()).toBe(200); expect(download.headers()['content-type']).toContain('application/octet-stream');
  const hostname = `problem-${Date.now()}`;
  await page.getByLabel('Yeni cihaz hostname').fill(hostname);
  await page.getByRole('button', { name: 'Cihaz kaydet', exact: true }).click();
  await expect(page.getByLabel('Cihaz kimliği', { exact: true })).not.toBeEmpty();
  await page.getByRole('button', { name: 'Analiz merkezi', exact: true }).click();
  await page.getByLabel('Makine adı').fill(hostname);
  await page.getByLabel('Teşhis kapsamı', { exact: true }).selectOption('BigFixScan');
  await page.getByLabel('Sorun açıklaması').fill('Sentetik test: BigFix servisi çalışmıyor.');
  await page.getByRole('button', { name: 'Analizi başlat', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Ajan bağlantısı bekleniyor' })).toBeVisible();
  await page.screenshot({ path: path.join(os.tmpdir(), 'wfi-problem-dark.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: path.join(os.tmpdir(), 'wfi-problem-mobile.png'), fullPage: true });
  await page.getByRole('button', { name: 'Teşhisi iptal et', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Analiz tamamlanamadı' })).toBeVisible();
  expect(errors).toEqual([]);
});


test('installed agent appears through automatic inventory refresh', async ({ page }) => {
  await page.goto('/'); await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await page.getByRole('button', { name: 'Cihazlar ve AI', exact: true }).click();
  const host = `installed-${Date.now()}`;
  const csrf = await (await page.request.get('/api/v1/auth/csrf')).json();
  const registered = await page.request.post('/api/v1/devices/register', { headers: { 'X-CSRF-TOKEN': csrf.token }, data: { hostname: host } });
  expect(registered.status()).toBe(200);
  const device = await registered.json();
  const credential = 'ab'.repeat(32);
  expect((await page.request.post('/api/v1/agent/enroll', { data: { deviceId: device.id, token: device.enrollmentToken, credential } })).status()).toBe(200);
  expect((await page.request.post('/api/v1/agent/me/heartbeat', { headers: { 'X-WFI-Device': device.id, Authorization: `Bearer ${credential}` }, data: { agentVersion: 'synthetic-setup-test', osVersion: 'Synthetic Windows' } })).status()).toBe(200);
  await expect(page.getByRole('row').filter({ hasText: host })).toContainText('online', { timeout: 20000 });
  expect((await page.request.post(`/api/v1/devices/${device.id}/revoke`, { headers: { 'X-CSRF-TOKEN': csrf.token } })).status()).toBe(200);
});
