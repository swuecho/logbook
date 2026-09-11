import axios from 'axios';
import { reactive } from 'vue';
import { activeAccount, syncCredentials, restoreSession } from './session';
import {
  listLocalNotes, getSyncMeta, applyRemote, applyRemotePage,
  prepareUpload, acknowledgeUpload, rejectUpload, recordUploadFailure,
} from './localStore.js';

export const syncStatus = reactive({
  running: false, pending: 0, conflicts: 0, historyReady: false,
  needsSignIn: !syncCredentials(),
  message: '', lastSyncedAt: 0, failedDates: [] as string[],
});
let timer: ReturnType<typeof setTimeout>;
let running = false;
let failures = 0;
let rerun = false;
let initialized = false;
const listeners = new Set<() => void>();
const channel = typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('logbook-data') : null;
export const onLocalChange = (listener: () => void) => { listeners.add(listener); return () => { listeners.delete(listener); }; };

export function notifyLocalChange(broadcast = true) {
  listeners.forEach(listener => listener());
  if (broadcast) channel?.postMessage('changed');
}
channel?.addEventListener('message', () => { notifyLocalChange(false); scheduleSync(); });

async function refreshStatus(account: string) {
  const [entries, meta] = await Promise.all([listLocalNotes(account), getSyncMeta(account)]);
  if (account !== activeAccount.value) return;
  syncStatus.failedDates = entries.filter(entry => entry.dirty && !entry.conflict && entry.uploadError).map(entry => entry.noteId);
  syncStatus.pending = entries.filter(entry => entry.dirty).length;
  syncStatus.conflicts = entries.filter(entry => entry.conflict).length;
  syncStatus.historyReady = Boolean(meta?.historyReady);
}

export function scheduleSync(delay = 500) {
  if (running) { rerun = true; return; }
  clearTimeout(timer);
  timer = setTimeout(() => { void syncNow(); }, delay);
}

export async function syncNow() {
  if (running) { rerun = true; return; }
  running = true;
  clearTimeout(timer);
  if (navigator.onLine && !syncCredentials()) await restoreSession();
  const account = activeAccount.value;
  const credentials = syncCredentials();
  syncStatus.needsSignIn = !credentials;
  try {
    if (!account) return;
    await refreshStatus(account);
    if (!credentials) { syncStatus.message = 'Sign in to sync. You can keep writing.'; return; }
    if (!navigator.onLine) { syncStatus.message = 'Offline · saved on this device'; return; }
    syncStatus.running = true;
    syncStatus.message = '';
    // Capture credentials for this run. An account switch must never retarget uploads.
    const client = axios.create({ timeout: 12000, headers: { 'X-CSRF-Token': credentials.csrfToken } });
    const stillCurrent = () => activeAccount.value === account && syncCredentials()?.csrfToken === credentials.csrfToken;
    const run = async () => {
      let uploadFailed = false;
      for (const entry of await listLocalNotes(account)) {
        if (!stillCurrent()) return;
        if (!entry.dirty || entry.conflict) continue;
        try {
          if (entry.serverRevision === undefined && !entry.pending) {
            const { data } = await client.get(`/api/sync/diary/${entry.noteId}`);
            await applyRemote(account, data);
          }
          if (!stillCurrent()) return;
          const pending = await prepareUpload(account, entry.noteId);
          if (!pending) continue;
          try {
            const { data } = await client.put(`/api/sync/diary/${entry.noteId}`, {
              note: pending.note, baseRevision: pending.baseRevision, mutationId: pending.mutationId,
            });
            await acknowledgeUpload(account, entry.noteId, pending, data);
          } catch (error) {
            if (axios.isAxiosError(error) && error.response?.status === 409 && error.response.data?.current) {
              await rejectUpload(account, entry.noteId, pending, error.response.data.current);
            } else throw error;
          }
        } catch (error) {
          // Authentication and local storage errors affect the whole run. A failed
          // request for one date must not prevent other uploads or history downloads.
          if (!axios.isAxiosError(error) || [401, 403].includes(error.response?.status || 0)) throw error;
          uploadFailed = true;
          await recordUploadFailure(account, entry.noteId);
        }
        notifyLocalChange();
      }
      // Fetch the currently open date first; the complete history follows in resumable pages.
      const date = new URL(location.href).searchParams.get('date') || new Date().toLocaleDateString('sv-SE').replace(/-/g, '');
      if (stillCurrent() && /^\d{8}$/.test(date)) {
        const { data } = await client.get(`/api/sync/diary/${date}`);
        await applyRemote(account, data);
        notifyLocalChange();
      }
      let more = true;
      // Yield after ten pages, so new edits don't wait behind a large download.
      for (let pageIndex = 0; more && pageIndex < 10 && stillCurrent(); pageIndex++) {
        const meta = await getSyncMeta(account);
        const { data } = await client.get('/api/sync/changes', { params: { cursor: meta?.cursor || '0' } });
        await applyRemotePage(account, data);
        more = data.hasMore;
        await refreshStatus(account);
        notifyLocalChange();
      }
      if (more) rerun = true;
      if (stillCurrent()) {
        failures = uploadFailed ? failures + 1 : 0;
        syncStatus.lastSyncedAt = Date.now();
        const entries = await listLocalNotes(account);
        if (entries.some(entry => entry.dirty && !entry.conflict)) rerun = true;
      }
    };
    // IndexedDB transactions protect edits; a browser lock avoids duplicate sync workers across tabs.
    if (navigator.locks) {
      await navigator.locks.request(`logbook-sync:${account}`, { ifAvailable: true }, async lock => { if (lock) await run(); });
    } else {
      await run(); // Persisted mutation IDs make concurrent retries safe on older browsers.
    }
  } catch (error) {
    failures++;
    if (account === activeAccount.value && credentials?.csrfToken === syncCredentials()?.csrfToken) {
      const authenticationFailed = axios.isAxiosError(error) && [401, 403].includes(error.response?.status || 0);
      syncStatus.needsSignIn = authenticationFailed || !syncCredentials();
      syncStatus.message = authenticationFailed
        ? 'Sign in to sync. You can keep writing.'
        : 'Sync unavailable · changes stay on this device';
    }
  } finally {
    if (account) {
      try { await refreshStatus(account); } catch { syncStatus.message = 'Local storage is unavailable.'; }
    }
    syncStatus.running = false;
    running = false;
    const delay = failures ? Math.min(60000, 2000 * 2 ** Math.min(failures, 5)) : rerun ? 500 : 30000;
    rerun = false;
    scheduleSync(delay);
  }
}

export async function refreshRemoteNote(noteId: string) {
  const credentials = syncCredentials();
  if (!credentials || !navigator.onLine) return;
  try {
    const { data } = await axios.get(`/api/sync/diary/${noteId}`, {
      timeout: 12000, headers: { 'X-CSRF-Token': credentials.csrfToken },
    });
    await applyRemote(credentials.account, data);
    notifyLocalChange();
  } catch { /* The durable queue retries; reads never block local editing. */ }
}

export function initSync() {
  if (initialized) return;
  initialized = true;
  window.addEventListener('online', () => scheduleSync(0));
  window.addEventListener('focus', () => scheduleSync(0));
  window.addEventListener('logbook-session', () => {
    syncStatus.message = '';
    syncStatus.needsSignIn = !syncCredentials();
    syncStatus.pending = 0;
    syncStatus.conflicts = 0;
    syncStatus.historyReady = false;
    syncStatus.failedDates = [];
    syncStatus.lastSyncedAt = 0;
    notifyLocalChange(false);
    scheduleSync(0);
  });
  document.addEventListener('visibilitychange', () => { if (!document.hidden) scheduleSync(0); });
  scheduleSync(0);
}
