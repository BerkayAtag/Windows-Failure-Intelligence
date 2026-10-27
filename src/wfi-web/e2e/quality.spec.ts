import { test, expect } from '@playwright/test';
import path from 'node:path';
import os from 'node:os';
test('quality laboratory preserves model errors without enabling an unavailable model', async ({ page }) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  await page.goto('/'); await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await expect(page.getByRole('button', { name: 'Sistem Sağlığı', exact: true })).toBeVisible();
  await page.route('**/api/v1/ai/models', route => route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ error: 'Synthetic model outage' }) }));
  await page.getByRole('button', { name: 'AI Kalite Laboratuvarı', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'AI Kalite Laboratuvarı', level: 1 })).toBeVisible();
  await expect(page.getByRole('alert')).toContainText('Model listesi alınamadı');
  await expect(page.getByRole('button', { name: 'Sentetik karşılaştırmayı başlat' })).toBeDisabled();
  await expect(page.getByText('Değerlendirmeler yükleniyor…')).toHaveCount(0);
  await page.getByLabel('Tema', { exact: true }).selectOption('dark');
  await page.screenshot({ path: path.join(os.tmpdir(), 'wfi-quality-dark.png'), fullPage: true, animations: 'disabled' });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: path.join(os.tmpdir(), 'wfi-quality-mobile.png'), fullPage: true, animations: 'disabled' });
  expect(errors).toEqual([]);
});


