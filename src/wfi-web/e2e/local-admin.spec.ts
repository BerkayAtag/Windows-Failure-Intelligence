import { test, expect } from '@playwright/test';

test('local setup login explains the account and keeps dark/mobile controls readable', async ({ page }) => {
  await page.route('**/api/v1/auth/config', route => route.fulfill({ json: { domain: 'synthetic.invalid', demo: false, configured: true, localAdmin: true } }));
  await page.goto('/');
  await expect(page.getByPlaceholder('local:admin')).toBeVisible();
  await expect(page.getByText('Kurulumda belirlediğiniz yerel yönetici hesabıyla devam edin.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Demoyu aç' })).toHaveCount(0);
  await page.getByLabel('Tema', { exact: true }).selectOption('dark');
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('button', { name: 'Güvenli giriş' })).toBeEnabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});
