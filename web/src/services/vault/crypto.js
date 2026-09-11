// Version 1: browser-native PBKDF2-SHA256 + AES-256-GCM. No plaintext persistence.
export const ITERATIONS = 600000;
export const MAX_BYTES = 1000000;
const encoder = new TextEncoder();
const decoder = new TextDecoder('utf-8', { fatal: true });
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const fields = (value, keys) => value && typeof value === 'object' && !Array.isArray(value)
  && Object.keys(value).sort().join(',') === [...keys].sort().join(',');
const fail = () => { throw new Error('Invalid vault data.'); };
const random = length => crypto.getRandomValues(new Uint8Array(length));
export function encode(bytes) {
  let binary = '';
  for (let i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
  return btoa(binary);
}
function decode(value, min, max) {
  if (typeof value !== 'string' || value.length > Math.ceil(max / 3) * 4) fail();
  const bytes = Uint8Array.from(atob(value), c => c.charCodeAt(0));
  if (bytes.length < min || bytes.length > max || encode(bytes) !== value) fail();
  return bytes;
}
function checkBox(box, min, max) {
  if (!fields(box, ['iv', 'data'])) fail();
  decode(box.iv, 12, 12);
  decode(box.data, min, max);
}
export function validateEnvelope(e) {
  if (!fields(e, ['format', 'version', 'id', 'kdf', 'master', 'recovery', 'payload'])
    || e.format !== 'logbook-vault' || e.version !== 1 || !uuid.test(e.id)
    || !fields(e.kdf, ['name', 'iterations', 'salt'])
    || e.kdf.name !== 'PBKDF2-SHA256' || e.kdf.iterations !== ITERATIONS) fail();
  decode(e.kdf.salt, 16, 16);
  checkBox(e.master, 48, 48);
  checkBox(e.recovery, 48, 48);
  checkBox(e.payload, 16, MAX_BYTES + 16);
  return e;
}
export function parseBackup(text) {
  if (typeof text !== 'string' || text.length > 1500000) fail();
  return validateEnvelope(JSON.parse(text));
}
export function validateItems(items) {
  if (!Array.isArray(items) || items.length > 2000) fail();
  const ids = new Set();
  for (const item of items) {
    if (!fields(item, ['id', 'kind', 'title', 'username', 'url', 'secret', 'notes', 'tags', 'updatedAt'])
      || !uuid.test(item.id) || ids.has(item.id) || !['login', 'secret', 'note'].includes(item.kind)) fail();
    ids.add(item.id);
    for (const field of ['title', 'username', 'url', 'secret', 'notes', 'tags', 'updatedAt']) {
      if (typeof item[field] !== 'string' || item[field].length > (['notes', 'secret'].includes(field) ? 100000 : 2000)) fail();
    }
  }
  if (encoder.encode(JSON.stringify(items)).length > MAX_BYTES) throw new Error('Vault is full (1 MB).');
  return items;
}
function aad(e, purpose) {
  // Immutable encryption identity stays portable across accounts and backups.
  return encoder.encode(JSON.stringify(['logbook-vault', 1, e.id, purpose,
    purpose === 'master' ? [e.kdf.name, e.kdf.iterations, e.kdf.salt] : null]));
}
async function aesKey(raw) {
  return crypto.subtle.importKey('raw', raw, 'AES-GCM', false, ['encrypt', 'decrypt']);
}
async function masterKey(passphrase, kdf) {
  if (typeof passphrase !== 'string' || passphrase.length > 1024) fail();
  const bytes = encoder.encode(passphrase);
  try {
    const material = await crypto.subtle.importKey('raw', bytes, 'PBKDF2', false, ['deriveKey']);
    return await crypto.subtle.deriveKey({ name: 'PBKDF2', hash: 'SHA-256', salt: decode(kdf.salt, 16, 16), iterations: kdf.iterations },
      material, { name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
  } finally { bytes.fill(0); }
}
function recoveryBytes(value) {
  const compact = value.replace(/[\s-]/g, '');
  if (!/^[0-9a-fA-F]{64}$/.test(compact)) throw new Error('Enter the complete 64-character recovery key.');
  return Uint8Array.from(compact.match(/../g), pair => parseInt(pair, 16));
}
export async function seal(key, bytes, additionalData) {
  const iv = random(12);
  const data = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData, tagLength: 128 }, key, bytes);
  return { iv: encode(iv), data: encode(new Uint8Array(data)) };
}
export async function unseal(key, box, additionalData) {
  return new Uint8Array(await crypto.subtle.decrypt({ name: 'AES-GCM', iv: decode(box.iv, 12, 12), additionalData, tagLength: 128 },
    key, decode(box.data, 16, MAX_BYTES + 16)));
}
export function checkPassphrase(value) {
  if (typeof value !== 'string' || [...value].length < 15 || value.length > 1024) {
    throw new Error('Use a separate master passphrase of 15–1024 characters.');
  }
}
export async function encryptItems(envelope, key, items) {
  validateItems(items);
  const bytes = encoder.encode(JSON.stringify(items));
  try { return { ...envelope, payload: await seal(key, bytes, aad(envelope, 'payload')) }; }
  finally { bytes.fill(0); }
}
export async function createVault(passphrase, items = []) {
  checkPassphrase(passphrase);
  validateItems(items);
  const raw = random(32);
  const recoveryRaw = random(32);
  try {
    const key = await aesKey(raw);
    const envelope = { format: 'logbook-vault', version: 1, id: crypto.randomUUID(),
      kdf: { name: 'PBKDF2-SHA256', iterations: ITERATIONS, salt: encode(random(16)) } };
    envelope.master = await seal(await masterKey(passphrase, envelope.kdf), raw, aad(envelope, 'master'));
    envelope.recovery = await seal(await aesKey(recoveryRaw), raw, aad(envelope, 'recovery'));
    const recoveryKey = [...recoveryRaw].map(b => b.toString(16).padStart(2, '0')).join('').toUpperCase().match(/.{8}/g).join('-');
    return { envelope: await encryptItems(envelope, key, items), key, recoveryKey };
  } finally { raw.fill(0); recoveryRaw.fill(0); }
}
export async function openVault(envelope, credential, recovery = false) {
  validateEnvelope(envelope);
  let raw;
  let plaintext;
  try {
    let wrappingKey;
    if (recovery) {
      const bytes = recoveryBytes(credential);
      try { wrappingKey = await aesKey(bytes); } finally { bytes.fill(0); }
    } else wrappingKey = await masterKey(credential, envelope.kdf);
    raw = await unseal(wrappingKey, envelope[recovery ? 'recovery' : 'master'], aad(envelope, recovery ? 'recovery' : 'master'));
    if (raw.length !== 32) fail();
    const key = await aesKey(raw);
    plaintext = await unseal(key, envelope.payload, aad(envelope, 'payload'));
    return { key, items: validateItems(JSON.parse(decoder.decode(plaintext))) };
  } catch {
    throw new Error('Could not unlock. Check your passphrase or recovery key; the vault may also be damaged.');
  } finally { raw?.fill(0); plaintext?.fill(0); }
}
export function generatePassword(length = 24) {
  if (!Number.isInteger(length) || length < 16 || length > 128) throw new Error('Invalid password length.');
  const alphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+';
  const limit = 256 - (256 % alphabet.length);
  let result = '';
  while (result.length < length) {
    for (const byte of random(128)) {
      if (byte < limit) result += alphabet[byte % alphabet.length];
      if (result.length === length) break;
    }
  }
  return result;
}
