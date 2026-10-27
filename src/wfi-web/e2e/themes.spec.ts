import { test, expect } from '@playwright/test';

test('themes persist on login and dashboard without mobile overflow', async ({ page }) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  await page.goto('/');
  await expect(page).toHaveTitle(/WFI/);
  for (const theme of ['dark', 'light', 'standard']) {
    await page.getByLabel('Tema', { exact: true }).selectOption(theme);
    await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
    await page.reload();
    await expect(page.getByLabel('Tema', { exact: true })).toHaveValue(theme);
  }
  await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await expect(page.getByRole('heading', { name: 'Analiz merkezi', exact: true })).toBeVisible();
  for (const theme of ['dark', 'light', 'standard']) {
    await page.getByLabel('Tema', { exact: true }).selectOption(theme);
    await page.screenshot({ path: `../../docs/design/theme-${theme}.png`, fullPage: true, animations: 'disabled' });
    await page.setViewportSize({ width: 390, height: 844 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.setViewportSize({ width: 1505, height: 1045 });
  }
  expect(errors).toEqual([]);
});
