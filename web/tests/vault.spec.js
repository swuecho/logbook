import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';
const pass = 'synthetic master passphrase';
const token = `${Buffer.from('{}').toString('base64url')}.${Buffer.from(JSON.stringify({ user_id: 1, iss: 'test', aud: 'logbook', role: 'user' })).toString('base64url')}.test`;
async function setup(page, context) {
  const state = { snapshot: null, writes: [], fail: false, conflict: false, hold: null };
  await context.addInitScript(token => {
    localStorage.setItem('JWT_TOKEN', token);
    localStorage.setItem('JWT_EXPIRES_AT', String(Date.now() + 86400000));
  }, token);
  await context.route('**/api/**', async route => {
    if (new URL(route.request().url()).pathname !== '/api/vault') { await route.fulfill({ json: { entries: [], cursor: '0', hasMore: false } }); return; }
    if (state.fail) { await route.abort(); return; }
    if (state.hold) await state.hold;
    if (route.request().method() === 'GET') {
      await route.fulfill({ status: state.snapshot ? 200 : 404, json: state.snapshot || {} }); return;
    }
    const body = route.request().postDataJSON();
    state.writes.push(body);
    if (state.conflict || body.baseRevision !== (state.snapshot?.revision || '0')) {
      await route.fulfill({ status: 409, json: {} }); return;
    }
    state.snapshot = { envelope: body.envelope, revision: String(Number(body.baseRevision) + 1) };
    await route.fulfill({ json: state.snapshot });
  });
  await page.goto('/vault');
  await expect(page.getByRole('heading', { name: 'Create your vault' })).toBeVisible();
  return state;
}
async function create(page) {
  await page.getByLabel('Master passphrase', { exact: true }).fill(pass);
  await page.getByLabel('Confirm master passphrase', { exact: true }).fill(pass);
  await page.getByRole('button', { name: 'Create vault', exact: true }).click();
  const recovery = await page.getByTestId('recovery-key').innerText();
  await page.getByLabel('I saved this recovery key outside Logbook.').check();
  await page.getByRole('button', { name: 'Finish and save' }).click();
  await expect(page.getByRole('button', { name: 'New item', exact: true })).toBeVisible();
  return recovery;
}
async function add(page) {
  await page.getByRole('button', { name: 'New item', exact: true }).click();
  await page.getByLabel('Title', { exact: true }).fill('Private example');
  await page.getByLabel('Password', { exact: true }).fill('SYNTHETIC-SECRET');
  await page.getByRole('button', { name: 'Save item', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Item saved');
}
async function unlock(page, value = pass, recovery = false) {
  if (recovery) await page.getByLabel('Use recovery key', { exact: true }).check();
  await page.getByLabel(recovery ? 'Recovery key' : 'Master passphrase', { exact: true }).fill(value);
  await page.getByRole('button', { name: 'Unlock', exact: true }).click();
  await expect(page.getByRole('button', { name: 'New item', exact: true })).toBeVisible();
}

test('create, edit, encrypted backup, recovery, restore and rotation work without plaintext persistence', async ({ page, context }) => {
  const state = await setup(page, context);
  const recovery = await create(page);
  await add(page);
  expect(JSON.stringify(state.writes)).not.toContain('SYNTHETIC-SECRET');
  expect(JSON.stringify(state.writes)).not.toContain('Private example');
  expect(JSON.stringify(state.writes)).not.toContain(pass);
  expect(JSON.stringify(state.writes)).not.toContain(recovery);
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Encrypted backup', exact: true }).click();
  const download = await downloadPromise;
  const backupPath = await download.path();
  expect(await readFile(backupPath, 'utf8')).not.toContain('SYNTHETIC-SECRET');
  const storage = await page.evaluate(async () => {
    const databases = [];
    for (const info of await indexedDB.databases()) {
      const db = await new Promise(resolve => { const request = indexedDB.open(info.name); request.onsuccess = () => resolve(request.result); });
      for (const name of db.objectStoreNames) {
        databases.push(await new Promise(resolve => { const request = db.transaction(name).objectStore(name).getAll(); request.onsuccess = () => resolve(request.result); }));
      }
      db.close();
    }
    return JSON.stringify([localStorage, sessionStorage, databases]);
  });
  expect(storage).not.toContain('SYNTHETIC-SECRET');
  expect(storage).not.toContain(recovery);
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Unlock vault' })).toBeVisible();
  await unlock(page, recovery, true);
  await page.getByRole('button', { name: 'Change keys', exact: true }).click();
  await page.getByRole('button', { name: /Private example/ }).click();
  await page.getByRole('button', { name: 'Delete item', exact: true }).click();
  await page.getByRole('button', { name: 'Confirm delete', exact: true }).click();
  await expect(page.getByText('No items yet.', { exact: false })).toBeVisible();
  await page.getByRole('button', { name: 'Restore backup', exact: true }).click();
  await page.getByLabel('Backup file').setInputFiles(backupPath);
  await page.getByLabel('Backup master passphrase', { exact: true }).fill(pass);
  await page.getByLabel('Replace my current vault items with this backup.').check();
  await page.getByRole('button', { name: 'Restore and save' }).click();
  await expect(page.getByRole('button', { name: /Private example/ })).toBeVisible();
  await page.getByRole('button', { name: 'Change keys', exact: true }).click();
  await page.getByLabel('New master passphrase', { exact: true }).fill('new synthetic master passphrase');
  await page.getByLabel('Confirm master passphrase', { exact: true }).fill('new synthetic master passphrase');
  await page.getByRole('button', { name: 'Generate new keys' }).click();
  const nextRecovery = await page.getByTestId('recovery-key').innerText();
  expect(nextRecovery).not.toBe(recovery);
  await page.getByLabel('I saved this recovery key outside Logbook.').check();
  await page.getByRole('button', { name: 'Finish and save' }).click();
  await page.getByRole('button', { name: 'Lock', exact: true }).click();
  await page.getByLabel('Master passphrase', { exact: true }).fill(pass);
  await page.getByRole('button', { name: 'Unlock', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Could not unlock');
  await unlock(page, 'new synthetic master passphrase');
  await expect(page.getByRole('button', { name: /Private example/ })).toBeVisible();
  await page.getByRole('button', { name: /Private example/ }).click();
  await page.screenshot({ path: '/tmp/logbook-vault-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: '/tmp/logbook-vault-mobile.png', fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('conflicts and failed writes preserve editor, and unsafe links remain text', async ({ page, context }) => {
  const state = await setup(page, context); await create(page); await add(page);
  await page.getByRole('button', { name: /Private example/ }).click();
  await page.getByLabel('URL', { exact: true }).fill('javascript:alert(1)');
  await expect(page.getByRole('link', { name: 'Open URL' })).toHaveCount(0);
  await page.getByLabel('Title', { exact: true }).fill('<img src=x onerror=alert(1)>');
  state.conflict = true;
  await page.getByRole('button', { name: 'Save item', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Another session changed');
  await expect(page.getByLabel('Title', { exact: true })).toHaveValue('<img src=x onerror=alert(1)>');
  state.conflict = false; state.fail = true;
  await page.getByRole('button', { name: 'Save item', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Connection failed');
  await expect(page.getByLabel('Password', { exact: true })).toHaveValue('SYNTHETIC-SECRET');
});

test('idle, hidden, navigation, session changes and cross-tab signals lock the vault', async ({ page, context }) => {
  await setup(page, context); await create(page);
  await page.clock.install();
  await page.clock.fastForward(301000);
  await expect(page.getByRole('heading', { name: 'Unlock vault' })).toBeVisible();
  await unlock(page);
  await page.evaluate(() => { const channel = new BroadcastChannel('logbook-vault-lock'); channel.postMessage('lock'); channel.close(); });
  await expect(page.getByRole('heading', { name: 'Unlock vault' })).toBeVisible();
  await unlock(page);
  await page.evaluate(() => window.dispatchEvent(new Event('logbook-session')));
  await expect(page.getByRole('heading', { name: 'Unlock vault' })).toBeVisible();
  await unlock(page);
  await page.evaluate(() => {
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await expect(page.getByRole('heading', { name: 'Unlock vault' })).toBeVisible();
  await page.evaluate(() => Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' }));
  await unlock(page);
  await page.getByRole('button', { name: 'Home', exact: true }).click();
  await page.getByRole('link', { name: 'Vault', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Unlock vault' })).toBeVisible();
});

test('locking during pending unlock cannot resurrect decrypted state', async ({ page, context }) => {
  const state = await setup(page, context); await create(page); await add(page);
  await page.getByRole('button', { name: 'Lock', exact: true }).click();
  let release;
  state.hold = new Promise(resolve => { release = resolve; });
  await page.getByLabel('Master passphrase', { exact: true }).fill(pass);
  await page.getByRole('button', { name: 'Unlock', exact: true }).click();
  await page.evaluate(() => window.dispatchEvent(new Event('logbook-session')));
  release(); state.hold = null;
  await expect(page.getByRole('heading', { name: 'Unlock vault' })).toBeVisible();
  await expect(page.getByRole('button', { name: /Private example/ })).toHaveCount(0);
  await unlock(page);
});

test('multiline secrets survive masking, saving and reload', async ({ page, context }) => {
  await setup(page, context); await create(page);
  const secret = '-----BEGIN SYNTHETIC KEY-----\nline one\nline two\n-----END SYNTHETIC KEY-----';
  await page.getByRole('button', { name: 'New item', exact: true }).click();
  await page.getByLabel('Type', { exact: true }).selectOption('secret');
  await page.getByLabel('Title', { exact: true }).fill('Multiline key');
  await expect(page.getByLabel('Secret value', { exact: true })).toHaveAttribute('readonly');
  await page.getByRole('button', { name: 'Reveal', exact: true }).click();
  await page.getByLabel('Secret value', { exact: true }).fill(secret);
  await page.getByRole('button', { name: 'Hide', exact: true }).click();
  await page.getByRole('button', { name: 'Save item', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Item saved');
  await page.reload(); await unlock(page);
  await page.getByRole('button', { name: /Multiline key/ }).click();
  await page.getByRole('button', { name: 'Reveal', exact: true }).click();
  await expect(page.getByLabel('Secret value', { exact: true })).toHaveValue(secret);
});
