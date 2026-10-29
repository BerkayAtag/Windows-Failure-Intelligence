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

test('new sessions default to red and white light theme; automation label and dark contrast remain usable', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.getByRole('button', { name: 'Demoyu aç' }).click();
  await expect(page.getByRole('button', { name: 'Onarım Otomasyonu', exact: true })).toBeVisible();
  await expect(page.locator('.sidebar')).toHaveCSS('background-color', 'rgb(255, 255, 255)');
  await expect(page.locator('.primary').first()).toHaveCSS('background-color', 'rgb(201, 0, 22)');
  await page.getByLabel('Tema', { exact: true }).selectOption('dark');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(page.getByLabel('Tema', { exact: true })).toHaveCSS('color', 'rgb(243, 243, 246)');
  await page.getByRole('button', { name: 'Onarım Otomasyonu', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Onarım Otomasyonu', exact: true })).toBeVisible();
});

test('copyright footer is shown on login and workspace in every theme', async ({ page }) => {
  const footer = '© 2026 Berkay Atağ · Windows Failure Intelligence (WFI)';
  await page.goto('/');
  await expect(page.getByText(footer, { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Demoyu aç' }).click();
  for (const theme of ['light', 'dark', 'standard']) {
    await page.getByLabel('Tema', { exact: true }).selectOption(theme);
    await expect(page.locator('.workspace .app-footer')).toHaveText(footer);
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator('.workspace .app-footer').scrollIntoViewIfNeeded();
  await expect(page.locator('.workspace .app-footer')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});
