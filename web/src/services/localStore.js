import { openDB } from 'idb';
import { combineNotes } from './conflictNotes.js';

let database;
export function openLocalDatabase() {
  if (!database) {
    database = openDB('logbook-db', 2, {
      upgrade(db) {
        // Keep the unscoped v1 notes untouched for explicit recovery/export.
        if (!db.objectStoreNames.contains('notes')) db.createObjectStore('notes', { keyPath: 'noteId' });
        const entries = db.createObjectStore('entries', { keyPath: ['account', 'noteId'] });
        entries.createIndex('account', 'account');
        db.createObjectStore('syncMeta', { keyPath: 'account' });
      },
      blocked() {
        if (typeof window !== 'undefined') window.dispatchEvent(new Event('logbook-storage-blocked'));
      },
      blocking() { database?.then(db => db.close()); database = undefined; },
    }).catch(error => { database = undefined; throw error; });
  }
  return database;
}

export async function getLocalNote(account, noteId) {
  return (await openLocalDatabase()).get('entries', [account, noteId]);
}

export async function listLocalNotes(account) {
  return (await openLocalDatabase()).getAllFromIndex('entries', 'account', account);
}

export async function getSyncMeta(account) {
  return (await openLocalDatabase()).get('syncMeta', account);
}

export async function saveLocalNote(account, noteId, note, previousNote) {
  const db = await openLocalDatabase();
  const tx = db.transaction('entries', 'readwrite');
  const current = await tx.store.get([account, noteId]);
  // The editor may still display an older document when a download commits.
  // Don't silently adopt that unseen document's revision as the edit's base.
  const unseenRemote = current && !current.dirty && !current.pending && previousNote !== undefined
    && current.note !== previousNote && current.note !== note;
  const entry = {
    ...current, account, noteId, note, dirty: true,
    conflict: unseenRemote ? { noteId, note: current.note, revision: current.serverRevision } : current?.conflict,
    localVersion: (current?.localVersion || 0) + 1, updatedAt: Date.now(),
  };
  await tx.store.put(entry);
  await tx.done;
  return entry;
}

function mergeRemote(current, remote, account) {
  if (current?.pending) {
    const observed = current.observedRemote;
    return { ...current, observedRemote: observed && BigInt(observed.revision) > BigInt(remote.revision) ? observed : remote };
  }
  if (current?.conflict && BigInt(current.conflict.revision) > BigInt(remote.revision)) return current;
  if (current?.serverRevision && BigInt(remote.revision) < BigInt(current.serverRevision)) return current;
  // An explicit merge stays local until the user confirms it, even if its text
  // happens to match a downloaded version.
  if (current?.mergeDraft) return { ...current, conflict: remote };
  if (current?.dirty) {
    if (remote.revision === current.serverRevision || (!current.serverRevision && remote.revision === '0')) {
      return { ...current, serverRevision: remote.revision };
    }
    if (remote.note === current.note) {
      return { ...current, serverRevision: remote.revision, dirty: false, conflict: undefined, uploadError: undefined, syncedAt: Date.now() };
    }
    return { ...current, conflict: remote, uploadError: undefined };
  }
  return {
    ...current, account, noteId: remote.noteId, note: remote.note,
    serverRevision: remote.revision, dirty: false, conflict: undefined, uploadError: undefined, syncedAt: Date.now(),
  };
}

export async function applyRemote(account, remote) {
  const db = await openLocalDatabase();
  const tx = db.transaction('entries', 'readwrite');
  const current = await tx.store.get([account, remote.noteId]);
  await tx.store.put(mergeRemote(current, remote, account));
  await tx.done;
}

export async function applyRemotePage(account, page) {
  const db = await openLocalDatabase();
  const tx = db.transaction(['entries', 'syncMeta'], 'readwrite');
  const entries = tx.objectStore('entries');
  for (const remote of page.entries) {
    const current = await entries.get([account, remote.noteId]);
    await entries.put(mergeRemote(current, remote, account));
  }
  const meta = await tx.objectStore('syncMeta').get(account);
  const cursor = meta?.cursor && BigInt(meta.cursor) > BigInt(page.cursor) ? meta.cursor : page.cursor;
  await tx.objectStore('syncMeta').put({ ...meta, account, cursor, historyReady: !page.hasMore || Boolean(meta?.historyReady) });
  await tx.done;
}

export async function prepareUpload(account, noteId) {
  const db = await openLocalDatabase();
  const tx = db.transaction('entries', 'readwrite');
  const current = await tx.store.get([account, noteId]);
  let pending = current?.pending;
  if (!pending && current?.dirty && !current.conflict && current.serverRevision !== undefined) {
    pending = {
      mutationId: crypto.randomUUID(), baseRevision: current.serverRevision,
      note: current.note, localVersion: current.localVersion,
    };
    await tx.store.put({ ...current, pending });
  }
  await tx.done;
  return pending;
}

export async function acknowledgeUpload(account, noteId, pending, remote) {
  const db = await openLocalDatabase();
  const tx = db.transaction('entries', 'readwrite');
  const current = await tx.store.get([account, noteId]);
  if (current?.pending?.mutationId === pending.mutationId) {
    const unchanged = current.localVersion === pending.localVersion;
    let next = {
      ...current, pending: undefined, observedRemote: undefined,
      serverRevision: remote.revision, syncedAt: Date.now(), uploadError: undefined,
      dirty: !unchanged, note: unchanged ? remote.note : current.note,
    };
    if (current.observedRemote && BigInt(current.observedRemote.revision) > BigInt(remote.revision)) {
      next = mergeRemote(next, current.observedRemote, account);
    }
    await tx.store.put(next);
  }
  await tx.done;
}

// Keep the pending mutation intact: a timed-out request may already be committed.
export async function recordUploadFailure(account, noteId) {
  const db = await openLocalDatabase();
  const tx = db.transaction('entries', 'readwrite');
  const current = await tx.store.get([account, noteId]);
  if (current?.dirty) await tx.store.put({ ...current, uploadError: { failedAt: Date.now() } });
  await tx.done;
}

export async function rejectUpload(account, noteId, pending, remote) {
  const db = await openLocalDatabase();
  const tx = db.transaction('entries', 'readwrite');
  const current = await tx.store.get([account, noteId]);
  if (current?.pending?.mutationId === pending.mutationId) {
    const observed = current.observedRemote;
    const latest = observed && BigInt(observed.revision) > BigInt(remote.revision) ? observed : remote;
    await tx.store.put(mergeRemote({ ...current, pending: undefined, observedRemote: undefined }, latest, account));
  }
  await tx.done;
}

function checkConflict(current, expected) {
  if (expected && (!current?.conflict || current.conflict.revision !== expected.revision || current.note !== expected.note)) {
    throw new Error('This entry changed during review. Review the latest versions and try again.');
  }
}

export async function editCombinedConflict(account, noteId, expected) {
  const db = await openLocalDatabase();
  const tx = db.transaction('entries', 'readwrite');
  const current = await tx.store.get([account, noteId]);
  checkConflict(current, expected);
  if (current?.conflict && !current.mergeDraft) {
    const original = { local: current.note, remote: current.conflict.note, revision: current.conflict.revision, savedAt: Date.now() };
    await tx.store.put({
      ...current, note: combineNotes(original.local, original.remote), mergeDraft: original,
      dirty: true, localVersion: (current.localVersion || 0) + 1,
      recovery: [...(current.recovery || []), original],
    });
  }
  await tx.done;
}

export async function cancelCombinedConflict(account, noteId, expected) {
  const db = await openLocalDatabase();
  const tx = db.transaction('entries', 'readwrite');
  const current = await tx.store.get([account, noteId]);
  checkConflict(current, expected);
  if (current?.mergeDraft && current.conflict) {
    await tx.store.put({
      ...current, note: current.mergeDraft.local, mergeDraft: undefined,
      localVersion: (current.localVersion || 0) + 1,
      recovery: [...(current.recovery || []), { local: current.note, remote: current.conflict.note, savedAt: Date.now() }],
    });
  }
  await tx.done;
}

export async function resolveLocalConflict(account, noteId, choice, expected) {
  const db = await openLocalDatabase();
  const tx = db.transaction('entries', 'readwrite');
  const current = await tx.store.get([account, noteId]);
  checkConflict(current, expected);
  if (current?.conflict) {
    const remote = current.conflict;
    await tx.store.put({
      ...current, note: choice === 'remote' ? remote.note : current.note,
      serverRevision: remote.revision, conflict: undefined, pending: undefined, uploadError: undefined, mergeDraft: undefined,
      localVersion: (current.localVersion || 0) + 1, dirty: choice !== 'remote',
      // Preserve both documents even after the user resolves a conflict.
      recovery: [...(current.recovery || []), { local: current.note, remote: remote.note, savedAt: Date.now() }],
    });
  }
  await tx.done;
}

export async function legacyNotes() {
  return (await openLocalDatabase()).getAll('notes');
}
