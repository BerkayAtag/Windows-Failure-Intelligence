import { defineConfig } from '@playwright/test';
export default defineConfig({ testDir: './e2e', workers: 1, use: { baseURL: 'http://127.0.0.1:5080', browserName: 'chromium', viewport: { width: 1505, height: 1045 } }, reporter: 'list', timeout: 30000 });
