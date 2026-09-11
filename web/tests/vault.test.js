import test from 'node:test';
import assert from 'node:assert/strict';
import { pbkdf2Sync, createDecipheriv } from 'node:crypto';
import { createVault, openVault, encryptItems, parseBackup, validateItems, generatePassword, encode, unseal } from '../src/services/vault/crypto.js';
const pass = 'synthetic master passphrase';
const item = { id: '11111111-2222-4333-8444-555555555555', kind: 'secret', title: 'Private title', username: '', url: 'https://example.test', secret: 'SYNTHETIC-SECRET', notes: 'Private notes', tags: 'work', updatedAt: '2026-09-11T00:00:00Z' };

test('master and recovery independently unlock; backup contains no plaintext and keys are nonextractable', async () => {
  const created = await createVault(pass, [item]);
  assert.equal(created.key.extractable, false);
  const serialized = JSON.stringify(created.envelope);
  for (const value of [pass, created.recoveryKey, item.title, item.secret, item.notes, item.url]) assert.ok(!serialized.includes(value));
  const backup = parseBackup(serialized);
  assert.deepEqual((await openVault(backup, pass)).items, [item]);
  assert.deepEqual((await openVault(backup, created.recoveryKey, true)).items, [item]);
  await assert.rejects(openVault(backup, 'wrong'));
  await assert.rejects(openVault(backup, '00'.repeat(32), true));
});

test('PBKDF2 envelope interoperates with independent Node crypto', async () => {
  const { envelope } = await createVault(pass);
  const wrapping = pbkdf2Sync(pass, Buffer.from(envelope.kdf.salt, 'base64'), 600000, 32, 'sha256');
  const decipher = createDecipheriv('aes-256-gcm', wrapping, Buffer.from(envelope.master.iv, 'base64'));
  decipher.setAAD(Buffer.from(JSON.stringify(['logbook-vault', 1, envelope.id, 'master', ['PBKDF2-SHA256', 600000, envelope.kdf.salt]])));
  const data = Buffer.from(envelope.master.data, 'base64');
  decipher.setAuthTag(data.subarray(-16));
  assert.equal(Buffer.concat([decipher.update(data.subarray(0, -16)), decipher.final()]).length, 32);
});

test('AES-256-GCM decrypts the NIST empty-message vector', async () => {
  const key = await crypto.subtle.importKey('raw', new Uint8Array(32), 'AES-GCM', false, ['decrypt']);
  const box = { iv: encode(new Uint8Array(12)), data: Buffer.from('530f8afbc74536b9a963b4f1c4cb738b', 'hex').toString('base64') };
  assert.equal((await unseal(key, box, new Uint8Array())).length, 0);
});

test('rejects tampering with ciphertext, identity, wrapping role and KDF salt', async () => {
  const created = await createVault(pass, [item]);
  for (const mutate of [
    e => { const bytes = Buffer.from(e.payload.data, 'base64'); bytes[0] ^= 1; e.payload.data = bytes.toString('base64'); },
    e => { e.id = crypto.randomUUID(); },
    e => { [e.master, e.recovery] = [e.recovery, e.master]; },
    e => { e.kdf.salt = encode(new Uint8Array(16)); },
  ]) {
    const altered = structuredClone(created.envelope); mutate(altered);
    await assert.rejects(openVault(altered, pass));
  }
});

test('each write uses a fresh nonce; full rotation revokes old credentials on current vault', async () => {
  const first = await createVault(pass, [item]);
  const next = await encryptItems(first.envelope, first.key, [item]);
  assert.notEqual(first.envelope.payload.iv, next.payload.iv);
  const rotated = await createVault('another synthetic master passphrase', (await openVault(next, pass)).items);
  assert.notEqual(first.envelope.id, rotated.envelope.id);
  assert.deepEqual((await openVault(rotated.envelope, rotated.recoveryKey, true)).items, [item]);
  await assert.rejects(openVault(rotated.envelope, pass));
  await assert.rejects(openVault(rotated.envelope, first.recoveryKey, true));
  assert.deepEqual((await openVault(first.envelope, pass)).items, [item]);
});

test('restore decrypts source backup and re-encrypts with destination keys', async () => {
  const source = await createVault(pass, [item]);
  const destination = await createVault('destination master passphrase');
  const restored = await encryptItems(destination.envelope, destination.key, (await openVault(source.envelope, source.recoveryKey, true)).items);
  assert.equal(restored.id, destination.envelope.id);
  assert.deepEqual((await openVault(restored, 'destination master passphrase')).items, [item]);
  await assert.rejects(openVault(restored, pass));
});

test('rejects oversized, unsupported, extra-field and dangerous-cost backup formats', async () => {
  const { envelope } = await createVault(pass);
  for (const alter of [e => e.version = 2, e => e.kdf.iterations = 999999999, e => e.kdf.salt = 'AA==', e => e.plaintext = 'oops', e => e.payload.iv = 'AA==']) {
    const e = structuredClone(envelope); alter(e);
    assert.throws(() => parseBackup(JSON.stringify(e)));
  }
  assert.throws(() => parseBackup('x'.repeat(1500001)));
  assert.throws(() => validateItems([item, item]));
  assert.throws(() => validateItems([{ ...item, notes: 'x'.repeat(100001) }]));
  await assert.rejects(createVault('short'));
});

test('password generator provides bounded long random values', () => {
  const values = new Set(Array.from({ length: 100 }, () => generatePassword()));
  assert.equal(values.size, 100);
  assert.ok([...values].every(value => value.length === 24));
  assert.throws(() => generatePassword(0));
});
