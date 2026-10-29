import { test, expect } from '@playwright/test';
import path from 'node:path';
import os from 'node:os';

test('agent hub exposes EXE, silent parameters and network rules in every theme', async ({ page }) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  await page.goto('/'); await expect(page).toHaveTitle(/WFI/); await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await page.getByRole('button', { name: 'Ajan ve Ağ Erişimleri', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Makinenizi WFI’ye bağlayın' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Windows ajanını indir' })).toHaveAttribute('href', '/api/v1/distribution/agent');
  await expect(page.locator('.install-command')).toContainText('--token-file');
  // Over plain HTTP (local demo) the example uses a neutral placeholder, never a fixed organisation host.
  await expect(page.locator('.install-command')).toContainText('--server https://wfi-sunucu.domain');
  for (const theme of ['standard', 'light', 'dark']) {
    await page.getByLabel('Tema', { exact: true }).selectOption(theme);
    await page.screenshot({ path: path.join(os.tmpdir(), `wfi-agent-hub-${theme}.png`), fullPage: true });
  }
  await page.getByRole('tab', { name: 'Ağ Erişimleri', exact: true }).click();
  await expect(page.getByRole('cell', { name: 'TCP 636', exact: true })).toBeVisible();
  await expect(page.getByText('CANLI TEST DEĞİL', { exact: false })).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: path.join(os.tmpdir(), 'wfi-agent-network-mobile.png'), fullPage: true });
  expect(errors).toEqual([]);
});

test('experience memory is traceable and an administrator can exclude a case', async ({ page }) => {
  let excluded = false;
  await page.route('**/api/v1/ai/experience', route => route.fulfill({ json: { items: excluded ? [] : [{ executionId: 'synthetic-case', diagnosticId: 'synthetic-job', playbookCode: 'PB-DNS-FLUSH', conclusion: 'Synthetic previous diagnosis', verification: 'Synthetic verification', completedAt: new Date().toISOString() }] } }));
  await page.route('**/api/v1/ai/experience/synthetic-case/exclude', route => { excluded = true; return route.fulfill({ status: 204 }); });
  await page.goto('/'); await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await page.getByRole('button', { name: 'AI Deneyimleri', exact: true }).click();
  await expect(page.getByText('Kaynak işlem: synthetic-case')).toBeVisible();
  await page.getByRole('button', { name: 'Gelecek analizlerde kullanma' }).click();
  await expect(page.getByText('Henüz uygun doğrulanmış deneyim yok.', { exact: false })).toBeVisible();
  expect(excluded).toBe(true);
});
