import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests',
  testMatch: process.env.LOGBOOK_REAL_SYNC_TEST ? ['**/real-sync.spec.js', '**/real-vault.spec.js'] : ['**/offline.spec.js', '**/vault.spec.js'],
  fullyParallel: false,
  workers: 1,
  timeout: 30000,
  use: {
    baseURL: 'http://127.0.0.1:9197',
    headless: true,
    launchOptions: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE
      ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE } : {},
  },
  webServer: { command: process.env.LOGBOOK_REAL_SYNC_TEST ? 'node tests/real-server.mjs' : 'node tests/server.mjs', timeout: 180000, url: 'http://127.0.0.1:9197', reuseExistingServer: false },
});
