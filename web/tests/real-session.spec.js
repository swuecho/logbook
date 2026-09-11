import { test, expect } from '@playwright/test';
import { registerSession } from './session-helpers.js';

async function openSession(page, context, request) {
  const account = await registerSession(request);
  await context.addCookies([account.cookie]);
  await page.goto('/vault');
  await expect(page.getByRole('heading', { name: 'Create your vault' })).toBeVisible();
  return account;
}

test('JWT stays HttpOnly, browser login rotates it, and logout invalidates a copied token', async ({ page, context, request }) => {
  const account = await openSession(page, context, request);
  expect((await context.cookies()).find(c => c.name === '__Host-logbook-session')).toMatchObject({ httpOnly: true, secure: true, sameSite: 'Strict' });
  expect(await page.evaluate(() => document.cookie)).not.toContain('__Host-logbook-session');
  expect(await page.evaluate(() => localStorage.getItem('JWT_TOKEN'))).toBeNull();
  await page.goto('/login');
  await page.locator('input[autocomplete=email]').fill(account.username);
  await page.locator('input[autocomplete=current-password]').fill(account.password);
  await page.getByRole('button', { name: '登录', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Logout', exact: true })).toBeVisible();
  const rotated = (await context.cookies()).find(c => c.name === '__Host-logbook-session');
  expect(rotated.value).not.toBe(account.jwt);
  expect((await request.get('/api/diary_ids', { headers: account.headers })).status()).toBe(401);
  const view = await page.evaluate(async () => (await fetch('/api/session')).json());
  const copiedHeaders = { Cookie: `__Host-logbook-session=${rotated.value}`, Origin: 'http://127.0.0.1:9197', 'X-CSRF-Token': view.csrfToken };
  await page.getByRole('button', { name: 'Logout', exact: true }).click();
  await expect(page.getByText('已登出', { exact: true })).toBeVisible();
  expect((await request.get('/api/diary_ids', { headers: copiedHeaders })).status()).toBe(401);
  expect((await context.cookies()).find(c => c.name === '__Host-logbook-session')).toBeUndefined();
  expect(await page.evaluate(() => localStorage.getItem('LOGBOOK_ACCOUNT'))).toBeNull();
});

test('offline logout stays pending across reload and revokes on reconnect', async ({ page, context, request }) => {
  const account = await openSession(page, context, request);
  await page.evaluate(() => navigator.serviceWorker.ready);
  await context.setOffline(true);
  await page.getByRole('button', { name: 'Logout', exact: true }).click();
  await expect(page.getByRole('button', { name: '重试登出', exact: true })).toBeVisible();
  expect(await page.evaluate(() => localStorage.getItem('LOGBOOK_LOGOUT_PENDING'))).toBe('1');
  expect(await page.evaluate(() => localStorage.getItem('LOGBOOK_ACCOUNT'))).toBeNull();
  // Server access really is still valid until it hears the revocation.
  expect((await request.get('/api/diary_ids', { headers: account.headers })).status()).toBe(200);
  await page.reload();
  await expect(page.getByRole('button', { name: '重试登出', exact: true })).toBeVisible();
  await context.setOffline(false);
  await expect.poll(async () => (await request.get('/api/diary_ids', { headers: account.headers })).status()).toBe(401);
  await expect.poll(() => page.evaluate(() => localStorage.getItem('LOGBOOK_LOGOUT_PENDING'))).toBeNull();
  await page.goto('/vault');
  await expect(page).toHaveURL(/\/login$/);
});

test('a shared-cookie account switch rejects stale tab writes and reads', async ({ page, context, request }) => {
  const first = await openSession(page, context, request);
  const second = await registerSession(request);
  await context.addCookies([second.cookie]);
  const result = await page.evaluate(async csrfToken => {
    const get = await fetch('/api/diary_ids', { headers: { 'X-CSRF-Token': csrfToken } });
    const put = await fetch('/api/sync/diary/20260911', {
      method: 'PUT', headers: { 'X-CSRF-Token': csrfToken, 'Content-Type': 'application/json' },
      body: JSON.stringify({ note: 'Must not enter the other account', baseRevision: '0', mutationId: crypto.randomUUID() }),
    });
    return [get.status, put.status];
  }, first.session.csrfToken);
  expect(result).toEqual([403, 403]);
  const untouched = await request.get('/api/sync/diary/20260911', { headers: second.headers });
  expect((await untouched.json()).revision).toBe('0');
});

test('legacy token cleanup preserves existing offline diary account and unsynced writing', async ({ page, context, request }) => {
  const account = await registerSession(request);
  await context.addCookies([account.cookie]);
  await page.goto('/view?date=20260911');
  await expect(page.locator('.ProseMirror')).toHaveAttribute('contenteditable', 'true');
  await page.evaluate(() => navigator.serviceWorker.ready);
  await context.setOffline(true);
  await page.locator('.ProseMirror').fill('Legacy offline writing');
  const namespace = await page.evaluate(() => localStorage.getItem('LOGBOOK_ACCOUNT'));
  await expect(page.locator('.editor-status')).toContainText('Saved on this device');
  await context.addInitScript(jwt => {
    localStorage.setItem('JWT_TOKEN', jwt);
    localStorage.setItem('JWT_EXPIRES_AT', String(Date.now() + 86400000));
    localStorage.removeItem('LOGBOOK_ACCOUNT');
  }, account.jwt);
  await context.clearCookies();
  await page.reload();
  await expect(page.locator('.ProseMirror')).toContainText('Legacy offline writing');
  expect(await page.evaluate(() => localStorage.getItem('LOGBOOK_ACCOUNT'))).toBe(namespace);
  expect(await page.evaluate(() => localStorage.getItem('JWT_TOKEN'))).toBeNull();
  expect(await page.evaluate(() => localStorage.getItem('JWT_EXPIRES_AT'))).toBeNull();
});
