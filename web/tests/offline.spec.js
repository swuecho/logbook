import { test, expect } from '@playwright/test';

const date = '20260907';
const doc = text => JSON.stringify({ type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text }] }] });
const token = user => `${Buffer.from('{}').toString('base64url')}.${Buffer.from(JSON.stringify({ user_id: user, iss: 'test', aud: 'logbook', role: 'user' })).toString('base64url')}.test`;

async function setup(page, context) {
  const state = { note: doc('From server'), revision: 1, writes: [], hold: null, fail: false };
  await context.addInitScript(({ accessToken }) => {
    if (!localStorage.getItem('JWT_TOKEN')) {
      localStorage.setItem('JWT_TOKEN', accessToken);
      localStorage.setItem('JWT_EXPIRES_AT', String(Date.now() + 86400000));
    }
  }, { accessToken: token('1') });
  await context.route('**/api/**', async route => {
    if (state.fail) { await route.abort('failed'); return; }
    const url = new URL(route.request().url());
    let data;
    let status = 200;
    if (url.pathname === '/api/sync/changes') {
      const entries = Number(url.searchParams.get('cursor')) < state.revision ? [{ noteId: date, note: state.note, revision: String(state.revision) }] : [];
      data = { entries, cursor: String(state.revision), hasMore: false };
    } else if (url.pathname.startsWith('/api/sync/diary/')) {
      const noteId = url.pathname.split('/').pop();
      if (route.request().method() === 'PUT') {
        const body = route.request().postDataJSON();
        state.writes.push(body);
        if (state.hold) await state.hold;
        if (body.baseRevision !== String(state.revision)) {
          status = 409;
          data = { current: { noteId, note: state.note, revision: String(state.revision) } };
        } else {
          state.note = body.note;
          state.revision++;
          data = { noteId, note: state.note, revision: String(state.revision) };
        }
      } else {
        data = noteId === date ? { noteId, note: state.note, revision: String(state.revision) } : { noteId, note: '', revision: '0' };
      }
    } else data = [];
    await route.fulfill({ status, json: data });
  });
  await page.goto(`/view?date=${date}`);
  await expect(page.locator('.ProseMirror')).toContainText('From server');
  return state;
}

async function localEntry(page, noteId = date) {
  return page.evaluate(async noteId => {
    const db = await new Promise((resolve, reject) => {
      const req = indexedDB.open('logbook-db', 2);
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    const entry = await new Promise((resolve, reject) => {
      const req = db.transaction('entries').objectStore('entries').get([localStorage.getItem('LOGBOOK_ACCOUNT'), noteId]);
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    db.close();
    return entry;
  }, noteId);
}

async function typeText(page, text) {
  const editor = page.locator('.ProseMirror');
  await editor.click();
  await page.keyboard.press('ControlOrMeta+End');
  await page.keyboard.insertText(text);
}

test('typing remains durable while a previous upload is delayed', async ({ page, context }) => {
  const state = await setup(page, context);
  let release;
  state.hold = new Promise(resolve => { release = resolve; });
  await typeText(page, ' first edit');
  await expect.poll(() => state.writes.length).toBe(1);
  await typeText(page, ' newer edit');
  await expect.poll(async () => (await localEntry(page)).note).toContain('newer edit');
  expect((await localEntry(page)).dirty).toBe(true);
  release();
  state.hold = null;
  await expect.poll(() => state.note).toContain('newer edit');
  await expect.poll(async () => (await localEntry(page)).dirty).toBe(false);
  await expect(page.locator('.ProseMirror')).toContainText('newer edit');
});

test('cached app reopens and saves offline with an expired token', async ({ page, context }) => {
  await setup(page, context);
  await page.evaluate(() => navigator.serviceWorker.ready);
  await page.evaluate(() => localStorage.setItem('JWT_EXPIRES_AT', '1'));
  await context.setOffline(true);
  await page.reload();
  await expect(page.locator('.ProseMirror')).toContainText('From server');
  await typeText(page, ' airplane writing');
  await expect.poll(async () => (await localEntry(page)).note).toContain('airplane writing');
  await page.reload();
  await expect(page.locator('.ProseMirror')).toContainText('airplane writing');
  await expect(page.locator('.editor-status')).toContainText('Saved on this device');
  await page.screenshot({ path: 'test-results/offline-editor.png', fullPage: true });
});

test('same-date conflict keeps local writing and lets the user choose', async ({ page, context }) => {
  const state = await setup(page, context);
  await context.setOffline(true);
  await typeText(page, ' local draft');
  await expect.poll(async () => (await localEntry(page)).dirty).toBe(true);
  state.note = doc('Changed on laptop');
  state.revision = 2;
  await context.setOffline(false);
  await expect(page.locator('.conflict-panel')).toBeVisible();
  await expect(page.locator('.ProseMirror')).toContainText('local draft');
  await page.getByRole('button', { name: 'Keep my writing' }).click();
  await expect.poll(() => state.note).toContain('local draft');
  await expect(page.locator('.conflict-panel')).toHaveCount(0);
  expect((await localEntry(page)).recovery[0].remote).toContain('Changed on laptop');
});

test('server failure while the browser is online leaves cached editing usable', async ({ page, context }) => {
  const state = await setup(page, context);
  state.fail = true;
  await page.reload();
  await expect(page.locator('.ProseMirror')).toContainText('From server');
  await typeText(page, ' unavailable server');
  await expect.poll(async () => (await localEntry(page)).note).toContain('unavailable server');
  await expect(page.locator('.editor-status')).toContainText('Saved on this device');
  state.fail = false;
  await page.getByRole('button', { name: /^Sync details:/ }).click();
  await page.getByRole('button', { name: 'Sync now', exact: true }).click();
  await expect.poll(() => state.note).toContain('unavailable server');
});


test('offline calendar navigation opens a new date and retains its draft after reload', async ({ page, context }) => {
  await setup(page, context);
  await page.evaluate(() => navigator.serviceWorker.ready);
  await context.setOffline(true);
  await page.getByRole('link', { name: 'Calendar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Calendar', exact: true })).toBeVisible();
  await page.goto('/view?date=20260908');
  await expect(page.locator('.ProseMirror')).toBeEditable();
  await typeText(page, 'A new offline date');
  await expect.poll(async () => (await localEntry(page, '20260908'))?.note).toContain('A new offline date');
  await page.reload();
  await expect(page.locator('.ProseMirror')).toContainText('A new offline date');
});

test('switching accounts offline never displays or uploads the previous account draft', async ({ page, context }) => {
  const state = await setup(page, context);
  await page.evaluate(() => navigator.serviceWorker.ready);
  await context.setOffline(true);
  await typeText(page, ' private account one');
  await expect.poll(async () => (await localEntry(page)).dirty).toBe(true);
  await page.evaluate(accessToken => {
    localStorage.setItem('JWT_TOKEN', accessToken);
    localStorage.setItem('JWT_EXPIRES_AT', '1');
  }, token('2'));
  await page.reload();
  await expect(page.locator('.ProseMirror')).toBeEditable();
  await expect(page.locator('.ProseMirror')).not.toContainText('private account one');
  expect(await localEntry(page)).toBeUndefined();
  await context.setOffline(false);
  await page.getByRole('button', { name: /^Sync details:/ }).click();
  await page.getByRole('button', { name: 'Sync now', exact: true }).click();
  expect(state.writes.length).toBe(0);
});

test('date navigation preserves edits immediately without waiting for upload', async ({ page, context }) => {
  await setup(page, context);
  await context.setOffline(true);
  await typeText(page, ' before navigation');
  await page.getByRole('link', { name: 'Calendar', exact: true }).click();
  await expect.poll(async () => (await localEntry(page)).note).toContain('before navigation');
  await page.goBack();
  await expect(page.locator('.ProseMirror')).toContainText('before navigation');
});


test('a local storage failure keeps writing visible and blocks navigation until saved', async ({ page, context }) => {
  await setup(page, context);
  await context.setOffline(true);
  await page.evaluate(() => {
    window.originalPut = IDBObjectStore.prototype.put;
    IDBObjectStore.prototype.put = function (...args) {
      if (this.name === 'entries') throw new DOMException('Storage full', 'QuotaExceededError');
      return window.originalPut.apply(this, args);
    };
  });
  await typeText(page, ' recover this writing');
  await expect(page.getByRole('button', { name: 'Retry local save' })).toBeVisible();
  await page.getByRole('link', { name: 'Calendar', exact: true }).click();
  await expect(page.locator('.ProseMirror')).toContainText('recover this writing');
  expect(page.url()).toContain('/view');
  await page.evaluate(() => { IDBObjectStore.prototype.put = window.originalPut; });
  await page.getByRole('button', { name: 'Retry local save' }).click();
  await expect.poll(async () => (await localEntry(page)).note).toContain('recover this writing');
  await expect(page.getByRole('button', { name: 'Retry local save' })).toHaveCount(0);
});


test('sync details shows sign-in only when the session needs authentication', async ({ page, context }) => {
  await setup(page, context);
  await page.getByRole('button', { name: /^Sync details:/ }).click();
  const signIn = page.locator('.sync-details').getByRole('link', { name: 'Sign in to sync', exact: true });
  await expect(signIn).toHaveCount(0);
  await page.evaluate(() => {
    localStorage.setItem('JWT_EXPIRES_AT', '1');
    window.dispatchEvent(new Event('logbook-session'));
  });
  await expect(signIn).toBeVisible();
  await page.evaluate(() => {
    localStorage.setItem('JWT_EXPIRES_AT', String(Date.now() + 86400000));
    window.dispatchEvent(new Event('logbook-session'));
  });
  await expect(signIn).toHaveCount(0);
});

test('details separates sync, offline availability, and optional recovery tools', async ({ page, context }) => {
  await setup(page, context);
  await expect(page.locator('.app-top-bar__actions .sync-button')).toBeVisible();
  await expect(page.locator('#app > .sync-panel')).toHaveCount(0);
  await page.getByRole('button', { name: /^Sync details:/ }).click();
  await expect(page.getByRole('region', { name: 'Sync', exact: true })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Offline availability', exact: true })).toContainText('1 entry downloaded');
  await expect(page.getByRole('button', { name: 'Export device backup', exact: true })).not.toBeVisible();
  await page.getByText('Storage and recovery', { exact: true }).click();
  await expect(page.getByRole('button', { name: 'Export device backup', exact: true })).toBeVisible();
  await page.getByText('Storage and recovery', { exact: true }).click();
  await page.getByRole('dialog', { name: 'Sync details' }).getByRole('button', { name: 'Close this dialog' }).click();
  await context.setOffline(true);
  await typeText(page, ' saved offline');
  await expect(page.locator('.sync-button')).toHaveAttribute('title', /Offline · Saved on this device/);
  await page.screenshot({ path: 'test-results/sync-details-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: 'test-results/sync-details-mobile.png', fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
