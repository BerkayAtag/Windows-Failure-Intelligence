import { test, expect } from '@playwright/test';
import path from 'node:path';

test('login, upload, evidence, duplicate, search, connections and logout', async ({ page }) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Çalışma alanınıza giriş yapın' })).toBeVisible();
  await page.screenshot({ path: '../../docs/design/login.png', fullPage: true });
  await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await expect(page.getByRole('heading', { name: 'Analiz merkezi', exact: true })).toBeVisible();
  await page.screenshot({ path: '../../docs/design/desktop.png', fullPage: true });
  await page.getByText('İsteğe bağlı: log dosyası incele', { exact: true }).click();
  await page.getByLabel('Hostname').fill('PC-PILOT-001');
  await page.getByLabel('Log dosyası', { exact: true }).setInputFiles(path.resolve('../../tests/Fixtures/upgrade-failure.log'));
  await page.getByRole('button', { name: 'Dosyayı analiz et' }).click();
  await expect(page.getByRole('heading', { name: 'Yükseltme geri alındı' })).toBeVisible({ timeout: 20000 });
  await expect(page.getByText('upgrade-failure.log · Satır 3')).toBeVisible();
  await page.screenshot({ path: '../../docs/design/result.png', fullPage: true });
  await page.getByRole('button', { name: 'Dosyayı analiz et' }).click();
  await expect(page.getByRole('status')).toContainText('daha önce yüklenmiş');
  await page.getByRole('button', { name: 'Analiz geçmişi' }).click();
  await page.getByRole('textbox', { name: 'Cihaz ara' }).fill('NO-SUCH-PC');
  await expect(page.getByText('Eşleşen cihaz bulunamadı.')).toBeVisible();
  await page.getByRole('button', { name: 'Bağlantılar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Active Directory / LDAP' })).toBeVisible();
  await page.getByRole('button', { name: 'Çıkış yap' }).click();
  await expect(page.getByRole('heading', { name: 'Çalışma alanınıza giriş yapın' })).toBeVisible();
  expect(errors).toEqual([]);
});
test('mobile layout and anonymous API boundary', async ({ page, request }) => {
  expect((await request.get('/api/v1/analyses')).status()).toBe(401);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/'); await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await expect(page.getByRole('heading', { name: 'Makinede sorun teşhisi' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: '../../docs/design/mobile.png', fullPage: true });
});
