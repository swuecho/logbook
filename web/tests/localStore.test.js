import 'fake-indexeddb/auto';
import test from 'node:test';
import assert from 'node:assert/strict';
import {
  openLocalDatabase, saveLocalNote, getLocalNote, listLocalNotes,
  applyRemote, applyRemotePage, getSyncMeta, prepareUpload,
  acknowledgeUpload, rejectUpload, resolveLocalConflict, legacyNotes, recordUploadFailure,
} from '../src/services/localStore.js';

const remote = (note, revision = '1', noteId = '20260907') => ({ noteId, note, revision });

test('a delayed acknowledgement never replaces a newer local edit', async () => {
  const account = 'delayed-ack';
  await applyRemote(account, remote('original'));
  await saveLocalNote(account, '20260907', 'first edit');
  const pending = await prepareUpload(account, '20260907');
  await saveLocalNote(account, '20260907', 'newer edit');
  await acknowledgeUpload(account, '20260907', pending, remote('first edit', '2'));
  const saved = await getLocalNote(account, '20260907');
  assert.equal(saved.note, 'newer edit');
  assert.equal(saved.dirty, true);
  assert.equal(saved.serverRevision, '2');
  const next = await prepareUpload(account, '20260907');
  assert.equal(next.baseRevision, '2');
  assert.notEqual(next.mutationId, pending.mutationId);
});

test('an interrupted upload reuses the exact persisted mutation even after editing', async () => {
  const account = 'retry';
  await applyRemote(account, remote('original'));
  await saveLocalNote(account, '20260907', 'first');
  const pending = await prepareUpload(account, '20260907');
  await saveLocalNote(account, '20260907', 'second');
  assert.deepEqual(await prepareUpload(account, '20260907'), pending);
});

test('accounts with the same date never share documents or pending mutations', async () => {
  await saveLocalNote('alice', '20260907', 'private A');
  await saveLocalNote('bob', '20260907', 'private B');
  assert.equal((await getLocalNote('alice', '20260907')).note, 'private A');
  assert.equal((await listLocalNotes('bob')).length, 1);
  assert.equal((await getLocalNote('bob', '20260907')).note, 'private B');
});

test('an undownloaded date becomes a conflict when a remote entry exists', async () => {
  const account = 'unknown-date';
  await saveLocalNote(account, '20260907', 'offline draft');
  assert.equal(await prepareUpload(account, '20260907'), undefined);
  await applyRemote(account, remote('existing remote', '5'));
  const saved = await getLocalNote(account, '20260907');
  assert.equal(saved.note, 'offline draft');
  assert.equal(saved.conflict.note, 'existing remote');
  assert.equal(await prepareUpload(account, '20260907'), undefined);
});

test('an absent remote date permits safe creation with revision zero', async () => {
  const account = 'new-date';
  await saveLocalNote(account, '20260907', 'offline draft');
  await applyRemote(account, remote('', '0'));
  assert.equal((await prepareUpload(account, '20260907')).baseRevision, '0');
});

test('conflict rejection preserves edits made while the upload was in flight', async () => {
  const account = 'conflict';
  await applyRemote(account, remote('original'));
  await saveLocalNote(account, '20260907', 'sent');
  const pending = await prepareUpload(account, '20260907');
  await saveLocalNote(account, '20260907', 'latest');
  await rejectUpload(account, '20260907', pending, remote('other device', '2'));
  assert.equal((await getLocalNote(account, '20260907')).note, 'latest');
  await resolveLocalConflict(account, '20260907', 'local');
  const resolved = await getLocalNote(account, '20260907');
  assert.equal(resolved.dirty, true);
  assert.equal(resolved.recovery[0].remote, 'other device');
  assert.equal((await prepareUpload(account, '20260907')).baseRevision, '2');
});

test('using the server version retains a recoverable copy of local writing', async () => {
  const account = 'remote-choice';
  await saveLocalNote(account, '20260907', 'keep a copy');
  await applyRemote(account, remote('server version'));
  await resolveLocalConflict(account, '20260907', 'remote');
  const saved = await getLocalNote(account, '20260907');
  assert.equal(saved.note, 'server version');
  assert.equal(saved.dirty, false);
  assert.equal(saved.recovery[0].local, 'keep a copy');
});

test('remote changes seen during upload are reconciled after acknowledgement', async () => {
  const account = 'overlapping-pull';
  await applyRemote(account, remote('original'));
  await saveLocalNote(account, '20260907', 'sent');
  const pending = await prepareUpload(account, '20260907');
  await applyRemotePage(account, { entries: [remote('later remote', '3')], cursor: '3', hasMore: false });
  await acknowledgeUpload(account, '20260907', pending, remote('sent', '2'));
  assert.equal((await getLocalNote(account, '20260907')).note, 'later remote');
  assert.equal((await getSyncMeta(account)).cursor, '3');
});

test('clearing content is a durable change and remote empty entries are retained', async () => {
  const account = 'cleared';
  await applyRemote(account, remote('text'));
  await saveLocalNote(account, '20260907', '');
  const pending = await prepareUpload(account, '20260907');
  assert.equal(pending.note, '');
  await acknowledgeUpload(account, '20260907', pending, remote('', '2'));
  assert.equal((await getLocalNote(account, '20260907')).note, '');
  assert.equal((await getLocalNote(account, '20260907')).dirty, false);
});

test('older downloads cannot roll back acknowledged content', async () => {
  await applyRemote('stale-get', remote('new', '9'));
  await applyRemote('stale-get', remote('old', '2'));
  assert.equal((await getLocalNote('stale-get', '20260907')).note, 'new');
});

test('the unscoped legacy cache is retained separately from account data', async () => {
  const db = await openLocalDatabase();
  await db.put('notes', { noteId: '20240101', note: 'legacy unsynced', dirty: true });
  assert.equal((await legacyNotes())[0].note, 'legacy unsynced');
  assert.equal(await getLocalNote('alice', '20240101'), undefined);
});


test('out-of-order downloads during upload preserve the highest observed revision and cursor', async () => {
  const account = 'out-of-order-download';
  await applyRemote(account, remote('original'));
  await saveLocalNote(account, '20260907', 'sent');
  const pending = await prepareUpload(account, '20260907');
  await applyRemotePage(account, { entries: [remote('newest remote', '4')], cursor: '4', hasMore: false });
  await applyRemotePage(account, { entries: [remote('stale remote', '3')], cursor: '3', hasMore: true });
  await acknowledgeUpload(account, '20260907', pending, remote('sent', '2'));
  assert.equal((await getLocalNote(account, '20260907')).note, 'newest remote');
  assert.equal((await getSyncMeta(account)).cursor, '4');
});


test('typing from an older visible document does not adopt an unseen remote revision', async () => {
  const account = 'unseen-remote';
  await applyRemote(account, remote('visible version', '1'));
  await applyRemote(account, remote('not yet displayed', '2'));
  await saveLocalNote(account, '20260907', 'visible version plus typing', 'visible version');
  const saved = await getLocalNote(account, '20260907');
  assert.equal(saved.note, 'visible version plus typing');
  assert.equal(saved.conflict.note, 'not yet displayed');
  assert.equal(await prepareUpload(account, '20260907'), undefined);
});


test('failed uploads preserve their mutation and newer writing until acknowledged', async () => {
  const account = 'upload-failure';
  await applyRemote(account, remote('original'));
  await saveLocalNote(account, '20260907', 'sent draft');
  const pending = await prepareUpload(account, '20260907');
  await saveLocalNote(account, '20260907', 'newer draft');
  await recordUploadFailure(account, '20260907');
  const failed = await getLocalNote(account, '20260907');
  assert.ok(failed.uploadError.failedAt);
  assert.equal(failed.note, 'newer draft');
  assert.deepEqual(await prepareUpload(account, '20260907'), pending);
  await acknowledgeUpload(account, '20260907', pending, remote('sent draft', '2'));
  const saved = await getLocalNote(account, '20260907');
  assert.equal(saved.uploadError, undefined);
  assert.equal(saved.dirty, true);
  assert.equal(saved.note, 'newer draft');
});
