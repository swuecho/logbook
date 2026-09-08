import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';

const date = '20260907';
const nextDate = '20260908';
const doc = text => JSON.stringify({ type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text }] }] });
async function localEntry(page, noteId = date) {
  return page.evaluate(async noteId => {
    const db = await new Promise((resolve, reject) => {
      const request = indexedDB.open('logbook-db', 2);
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
    const entry = await new Promise((resolve, reject) => {
      const request = db.transaction('entries').objectStore('entries').get([localStorage.getItem('LOGBOOK_ACCOUNT'), noteId]);
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
    db.close();
    return entry;
  }, noteId);
}
async function edit(page, text) {
  await page.locator('.ProseMirror').click();
  await page.keyboard.press('ControlOrMeta+End');
  await page.keyboard.insertText(text);
  await expect.poll(async () => (await localEntry(page, new URL(page.url()).searchParams.get('date')))?.note).toContain(text);
}
async function sync(page) {
  if (await page.getByRole('button', { name: 'Details', exact: true }).isVisible()) {
    await page.getByRole('button', { name: 'Details', exact: true }).click();
  }
  await page.getByRole('button', { name: 'Sync now', exact: true }).click();
}
async function devices(browser, request) {
  const response = await request.post('/api/register', { data: { username: `${randomUUID()}@example.test`, password: randomUUID() } });
  expect(response.status()).toBe(201);
  const session = await response.json();
  const contexts = [];
  const pages = [];
  for (let i = 0; i < 2; i++) {
    const context = await browser.newContext({ baseURL: test.info().project.use.baseURL });
    contexts.push(context);
    await context.addInitScript(session => {
      localStorage.setItem('JWT_TOKEN', session.accessToken);
      localStorage.setItem('JWT_EXPIRES_AT', String(Date.now() + session.expiresIn * 1000));
    }, session);
    pages.push(await context.newPage());
  }
  const headers = { Authorization: `Bearer ${session.accessToken}` };
  const seed = await request.put(`/api/sync/diary/${date}`, { headers, data: { note: doc('Original'), baseRevision: '0', mutationId: randomUUID() } });
  expect(seed.ok()).toBe(true);
  for (const page of pages) {
    await page.goto(`/view?date=${date}`);
    await expect(page.locator('.ProseMirror')).toContainText('Original');
    await page.evaluate(() => navigator.serviceWorker.ready);
  }
  return { contexts, pages, headers };
}

test('real migrated API preserves offline edits, resolves two-device conflicts, and replays a lost acknowledgement', async ({ browser, request }) => {
  const { contexts, pages: [first, second], headers } = await devices(browser, request);
  try {
    await contexts[0].setOffline(true);
    await edit(first, ' offline draft');
    await first.reload();
    await expect(first.locator('.ProseMirror')).toContainText('offline draft');
    await edit(second, ' laptop edit');
    await expect.poll(async () => (await localEntry(second))?.dirty).toBe(false);
    await contexts[0].setOffline(false);
    await sync(first);
    await expect(first.locator('.conflict-panel')).toBeVisible();
    await expect(first.locator('.ProseMirror')).toContainText('offline draft');
    await first.getByRole('button', { name: 'Keep my writing' }).click();
    await expect.poll(async () => (await localEntry(first))?.dirty).toBe(false);
    expect((await localEntry(first)).recovery[0].remote).toContain('laptop edit');
    await sync(second);
    await expect(second.locator('.ProseMirror')).toContainText('offline draft');

    // Let PostgreSQL commit, then lose the HTTP response. Retry must use the same
    // mutation and return its receipt without creating a second revision.
    let committed;
    const mutations = [];
    let loseResponse = true;
    await contexts[0].route(`**/api/sync/diary/${date}`, async route => {
      if (route.request().method() !== 'PUT') return route.continue();
      mutations.push(route.request().postDataJSON().mutationId);
      if (loseResponse) {
        const response = await route.fetch();
        expect(response.ok()).toBe(true);
        committed = await response.json();
        await route.abort('failed');
      } else await route.continue();
    });
    await edit(first, ' acknowledged later');
    await expect.poll(() => committed?.note).toContain('acknowledged later');
    await expect.poll(async () => (await localEntry(first))?.uploadError).toBeTruthy();
    expect((await localEntry(first)).dirty).toBe(true);
    await first.reload();
    loseResponse = false;
    await sync(first);
    await expect.poll(async () => (await localEntry(first))?.dirty).toBe(false);
    expect(mutations.length).toBeGreaterThanOrEqual(2);
    expect(new Set(mutations).size).toBe(1);
    const saved = await (await request.get(`/api/sync/diary/${date}`, { headers })).json();
    expect(saved.revision).toBe(committed.revision);
    await sync(second);
    await expect(second.locator('.ProseMirror')).toContainText('acknowledged later');
  } finally { await Promise.all(contexts.map(context => context.close())); }
});

test('one rejected upload leaves other dates and history syncing, and remains retryable', async ({ browser, request }) => {
  const { contexts, pages: [first, second], headers } = await devices(browser, request);
  try {
    await contexts[0].setOffline(true);
    await edit(first, ' blocked draft');
    await first.goto(`/view?date=${nextDate}`);
    await edit(first, ' healthy draft');
    const remoteDate = '20260909';
    const seeded = await request.put(`/api/sync/diary/${remoteDate}`, { headers, data: { note: doc('Downloaded despite failure'), baseRevision: '0', mutationId: randomUUID() } });
    expect(seeded.ok()).toBe(true);
    let blocked = true;
    await contexts[0].route(`**/api/sync/diary/${date}`, route =>
      blocked && route.request().method() === 'PUT'
        ? route.fulfill({ status: 422, json: { message: 'Injected entry rejection' } })
        : route.continue());
    await contexts[0].setOffline(false);
    await sync(first);
    await expect.poll(async () => (await localEntry(first, nextDate))?.dirty).toBe(false);
    await expect.poll(async () => (await localEntry(first, remoteDate))?.note).toContain('Downloaded despite failure');
    const failed = await localEntry(first);
    expect(failed.dirty).toBe(true);
    expect(failed.pending.note).toContain('blocked draft');
    await expect(first.locator('.sync-details')).toContainText('Upload failed');
    await expect(first.locator('.sync-details')).toContainText(date);
    await first.screenshot({ path: 'test-results/sync-upload-failure.png', fullPage: true });
    await sync(second);
    await second.goto(`/view?date=${nextDate}`);
    await expect(second.locator('.ProseMirror')).toContainText('healthy draft');
    blocked = false;
    await sync(first);
    await expect.poll(async () => (await localEntry(first))?.dirty).toBe(false);
    expect((await localEntry(first)).uploadError).toBeUndefined();
  } finally { await Promise.all(contexts.map(context => context.close())); }
});


for (const status of [401, 403]) {
  test(`authentication failure ${status} pauses all uploads until credentials work again`, async ({ browser, request }) => {
    const { contexts, pages: [first] } = await devices(browser, request);
    try {
      await contexts[0].setOffline(true);
      await edit(first, ' waiting for authentication');
      await first.goto(`/view?date=${nextDate}`);
      await edit(first, ' also waiting');
      let rejected = true;
      const uploadedDates = [];
      await contexts[0].route('**/api/sync/diary/*', route => {
        if (route.request().method() !== 'PUT') return route.continue();
        uploadedDates.push(new URL(route.request().url()).pathname.split('/').pop());
        return rejected ? route.fulfill({ status, json: { message: 'Sign in required' } }) : route.continue();
      });
      const previousFailure = (await localEntry(first)).uploadError;
      await contexts[0].setOffline(false);
      await sync(first);
      await expect(first.locator('.sync-panel')).toContainText('Sign in to sync. You can keep writing.');
      expect(uploadedDates.length).toBeGreaterThan(0);
      expect(uploadedDates.every(id => id === date)).toBe(true);
      expect((await localEntry(first, nextDate)).dirty).toBe(true);
      expect((await localEntry(first)).uploadError).toEqual(previousFailure);
      rejected = false;
      await sync(first);
      await expect.poll(async () => (await localEntry(first, nextDate))?.dirty).toBe(false);
    } finally { await Promise.all(contexts.map(context => context.close())); }
  });
}

test('combined conflict draft survives offline reload and syncs only after explicit confirmation', async ({ browser, request }) => {
  const { contexts, pages: [first, second], headers } = await devices(browser, request);
  try {
    await contexts[0].setOffline(true);
    await edit(first, ' phone writing');
    await edit(second, ' laptop writing');
    await expect.poll(async () => (await localEntry(second))?.dirty).toBe(false);
    await contexts[0].setOffline(false);
    await sync(first);
    await expect(first.locator('.conflict-panel')).toBeVisible();
    await expect(first.getByRole('region', { name: 'Your writing', exact: true })).toContainText('phone writing');
    await expect(first.getByRole('region', { name: 'Server version', exact: true })).toContainText('laptop writing');
    await first.getByRole('button', { name: 'Edit combined version', exact: true }).click();
    await expect(first.locator('.ProseMirror')).toContainText('phone writing');
    await expect(first.locator('.ProseMirror')).toContainText('laptop writing');
    await contexts[0].setOffline(true);
    await edit(first, ' reviewed together');
    await first.reload();
    await expect(first.locator('.ProseMirror')).toContainText('reviewed together');
    await expect(first.getByRole('button', { name: 'Use combined version', exact: true })).toBeVisible();
    const draft = await localEntry(first);
    expect(draft.recovery[0].local).toContain('phone writing');
    expect(draft.recovery[0].remote).toContain('laptop writing');

    // A newer server version must remain visible without replacing the draft.
    await edit(second, ' another laptop edit');
    await expect.poll(async () => (await localEntry(second))?.dirty).toBe(false);
    await contexts[0].setOffline(false);
    await sync(first);
    await expect(first.locator('.conflict-update')).toContainText('changed again');
    await expect(first.getByRole('region', { name: 'Server version', exact: true })).toContainText('another laptop edit');
    await expect(first.locator('.ProseMirror')).toContainText('reviewed together');
    const before = await (await request.get(`/api/sync/diary/${date}`, { headers })).json();
    expect(before.note).not.toContain('reviewed together');
    await first.screenshot({ path: 'test-results/conflict-comparison-desktop.png', fullPage: true });
    await first.setViewportSize({ width: 390, height: 844 });
    await expect(first.locator('.ProseMirror')).toContainText('reviewed together');
    await first.screenshot({ path: 'test-results/conflict-comparison-mobile.png', fullPage: true });
    expect(await first.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await first.getByRole('button', { name: 'Use combined version', exact: true }).click();
    await expect.poll(async () => (await localEntry(first))?.dirty).toBe(false);
    await expect(first.locator('.conflict-panel')).toHaveCount(0);
    await sync(second);
    await expect(second.locator('.ProseMirror')).toContainText('reviewed together');
    const saved = await localEntry(first);
    expect(saved.recovery[0].local).toContain('phone writing');
    expect(saved.recovery[0].remote).toContain('laptop writing');
    expect(saved.recovery.at(-1).remote).toContain('another laptop edit');
  } finally { await Promise.all(contexts.map(context => context.close())); }
});
