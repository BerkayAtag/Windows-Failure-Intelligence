import { test, expect } from '@playwright/test';
import path from 'node:path';
import os from 'node:os';
test('admin operations health is domain scoped and renders in all themes', async ({ page }) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  await page.goto('/'); await expect(page).toHaveTitle(/WFI/);
  await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await page.getByRole('button', { name: 'Sistem Sağlığı', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'İş kuyruğu ve model kapasitesi' })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Aktif teşhis', exact: true })).toBeVisible();
  for (const theme of ['standard', 'light', 'dark']) {
    await page.getByLabel('Tema', { exact: true }).selectOption(theme);
    await page.screenshot({ path: path.join(os.tmpdir(), `wfi-operations-${theme}.png`), fullPage: true, animations: 'disabled' });
  }
  await page.getByRole('button', { name: 'Yenile', exact: true }).click();
  await expect(page.getByRole('cell', { name: 'Aktif teşhis', exact: true })).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: path.join(os.tmpdir(), 'wfi-operations-mobile.png'), fullPage: true });
  expect(errors).toEqual([]);
});
