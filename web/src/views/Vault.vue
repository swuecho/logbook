<template>
  <div class="vault-page">
    <AppTopBar title="Vault" :show-calendar="false" :show-search="false" :show-content="false">
      <template #actions-before>
        <button v-if="unlocked || pending" class="linkish" type="button" @click="locks.lock()">Lock</button>
      </template>
    </AppTopBar>
    <main class="vault-main">
      <p v-if="!activeAccount">Sign in to use your vault. <router-link to="/login">Sign in</router-link></p>
      <p v-else-if="!supported">The vault requires HTTPS and a browser with Web Crypto.</p>
      <template v-else>
        <p class="vault-caption">Encrypted in your browser. Locks after 5 minutes of inactivity, when hidden, or when you leave this page.</p>
        <p v-if="error" role="alert" class="vault-error">{{ error }}</p>
        <p v-if="message" role="status" class="vault-caption">{{ message }}</p>
        <p v-if="loading">Loading vault…</p>
        <section v-else-if="pending" class="vault-panel vault-narrow">
          <h2>Save your recovery key</h2>
          <p>Keep this key somewhere safe outside Logbook. It unlocks this vault if you forget your master passphrase. Without either, your secrets cannot be recovered.</p>
          <pre class="vault-recovery" data-testid="recovery-key">{{ recoveryKey }}</pre>
          <el-button @click="copy(recoveryKey)">Copy recovery key</el-button>
          <label class="vault-check"><input v-model="recoverySaved" type="checkbox"> I saved this recovery key outside Logbook.</label>
          <p v-if="unlocked" class="vault-caption">This rotates the data key and recovery key. Old backups still need their original passphrase or recovery key.</p>
          <el-button :disabled="!recoverySaved" :loading="busy" @click="commitPending">Finish and save</el-button>
          <el-button :disabled="busy" @click="discardPending">Cancel</el-button>
        </section>
        <section v-else-if="!unlocked" class="vault-panel vault-narrow">
          <h2>{{ exists ? 'Unlock vault' : 'Create your vault' }}</h2>
          <p v-if="!exists">Choose a master passphrase different from your Logbook password. It is never sent to the server.</p>
          <form @submit.prevent="exists ? unlock() : prepareKeys()">
            <label v-if="exists" class="vault-check"><input v-model="useRecovery" type="checkbox"> Use recovery key</label>
            <label class="vault-field">{{ useRecovery && exists ? 'Recovery key' : 'Master passphrase' }}
              <input v-model="credential" type="password" :autocomplete="exists ? 'off' : 'new-password'" maxlength="1024" required>
            </label>
            <label v-if="!exists" class="vault-field">Confirm master passphrase
              <input v-model="confirmation" type="password" autocomplete="new-password" maxlength="1024" required>
            </label>
            <el-button native-type="submit" :loading="busy">{{ exists ? 'Unlock' : 'Create vault' }}</el-button>
            <el-button :disabled="busy" @click="refresh">Reload</el-button>
          </form>
          <p v-if="!exists" class="vault-caption">Have a backup? Create a vault, then use Restore backup. Your backup is decrypted locally and re-encrypted with your new keys.</p>
        </section>
        <template v-else>
          <div class="vault-toolbar">
            <input v-model="search" aria-label="Search vault" placeholder="Search titles, usernames, URLs or tags" type="search" autocomplete="off">
            <el-button :disabled="busy" @click="newItem">New item</el-button>
            <el-button :disabled="busy" @click="exportBackup">Encrypted backup</el-button>
            <el-button :disabled="busy" @click="panel = panel === 'restore' ? '' : 'restore'">Restore backup</el-button>
            <el-button :disabled="busy" @click="panel = panel === 'keys' ? '' : 'keys'">Change keys</el-button>
          </div>
          <section v-if="panel === 'keys'" class="vault-panel vault-narrow">
            <h2>Change master passphrase and recovery key</h2>
            <p>This re-encrypts all saved items with a new data key. Save any editor changes first.</p>
            <form @submit.prevent="prepareKeys">
              <label class="vault-field">New master passphrase<input v-model="credential" type="password" autocomplete="new-password" maxlength="1024" required></label>
              <label class="vault-field">Confirm master passphrase<input v-model="confirmation" type="password" autocomplete="new-password" maxlength="1024" required></label>
              <el-button native-type="submit" :loading="busy">Generate new keys</el-button>
            </form>
          </section>
          <section v-if="panel === 'restore'" class="vault-panel vault-narrow">
            <h2>Restore encrypted backup</h2>
            <p>Restore replaces the current items. Export a backup first if you want to keep them. Your current master passphrase and recovery key stay in use.</p>
            <form @submit.prevent="restoreBackup">
              <label class="vault-field">Backup file<input ref="fileInput" type="file" accept=".json,application/json" required></label>
              <label class="vault-check"><input v-model="backupRecovery" type="checkbox"> Unlock backup with recovery key</label>
              <label class="vault-field">Backup {{ backupRecovery ? 'recovery key' : 'master passphrase' }}<input v-model="backupCredential" type="password" autocomplete="off" maxlength="1024" required></label>
              <label class="vault-check"><input v-model="replaceConfirmed" type="checkbox"> Replace my current vault items with this backup.</label>
              <el-button native-type="submit" :disabled="!replaceConfirmed" :loading="busy">Restore and save</el-button>
            </form>
          </section>
          <div class="vault-layout">
            <aside class="vault-panel vault-list" aria-label="Vault items">
              <p v-if="!filtered.length" class="vault-caption">{{ items.length ? 'No matching items.' : 'No items yet. Add a login, secret, or secure note.' }}</p>
              <button v-for="item in filtered" :key="item.id" type="button" :disabled="busy" :class="{ selected: draft?.id === item.id }" @click="selectItem(item)">
                <strong>{{ item.title || 'Untitled' }}</strong><span>{{ kindLabel(item.kind) }}{{ item.username ? ' · ' + item.username : '' }}</span>
              </button>
            </aside>
            <section class="vault-panel vault-editor">
              <form v-if="draft" @submit.prevent="saveItem" autocomplete="off">
                <label class="vault-field">Type<select v-model="draft.kind" aria-label="Type"><option value="login">Login</option><option value="secret">Secret</option><option value="note">Secure note</option></select></label>
                <label class="vault-field">Title<input v-model="draft.title" maxlength="2000" required></label>
                <label v-if="draft.kind === 'login'" class="vault-field">Username<input v-model="draft.username" maxlength="2000" autocomplete="off"></label>
                <label class="vault-field">URL<input v-model="draft.url" maxlength="2000" type="text"></label>
                <a v-if="safeUrl" :href="safeUrl" target="_blank" rel="noopener noreferrer" referrerpolicy="no-referrer">Open URL</a>
                <template v-if="draft.kind !== 'note'">
                  <label class="vault-field">{{ draft.kind === 'login' ? 'Password' : 'Secret value' }}
                    <textarea v-if="revealed" v-model="draft.secret" rows="4" maxlength="100000" spellcheck="false" autocapitalize="off"></textarea>
                    <input v-else v-model="draft.secret" type="password" autocomplete="new-password" maxlength="100000" :readonly="draft.kind === 'secret'" :placeholder="draft.kind === 'secret' ? 'Reveal to edit this secret' : ''">
                  </label>
                  <div class="vault-row"><el-button @click="revealed = !revealed">{{ revealed ? 'Hide' : 'Reveal' }}</el-button><el-button @click="copy(draft.secret)">Copy secret</el-button><el-button @click="draft.secret = generatePassword()">Generate password</el-button></div>
                </template>
                <label class="vault-field">{{ draft.kind === 'note' ? 'Secure note' : 'Notes' }}<textarea v-model="draft.notes" rows="7" maxlength="100000" spellcheck="false"></textarea></label>
                <label class="vault-field">Tags<input v-model="draft.tags" maxlength="2000" placeholder="work, personal"></label>
                <div class="vault-row"><el-button native-type="submit" :loading="busy">Save item</el-button><el-button :disabled="busy" @click="cancelEdit">Close</el-button><el-button v-if="items.some(i => i.id === draft.id)" :disabled="busy" @click="deleteConfirmed = !deleteConfirmed">Delete item</el-button></div>
                <div v-if="deleteConfirmed" class="vault-row"><span>Delete this item permanently from the current vault?</span><el-button :loading="busy" @click="deleteItem">Confirm delete</el-button></div>
              </form>
              <p v-else class="vault-caption">Select an item or create a new one. Search and editing happen only in this browser.</p>
            </section>
          </div>
          <p class="vault-caption">{{ items.length }} items · Online only · Encrypted backups include the current editor. Clipboard history is outside Logbook’s control.</p>
        </template>
      </template>
    </main>
  </div>
</template>

<script setup>
import { computed, onMounted, onBeforeUnmount, ref, watch } from 'vue';
import AppTopBar from '@/components/AppTopBar.vue';
import { activeAccount, syncCredentials } from '@/services/session';
import { createVault, openVault, parseBackup, encryptItems, generatePassword } from '@/services/vault/crypto';
import { vaultApi } from '@/services/vault/api';
import { installVaultLock } from '@/services/vault/lock';

const supported = Boolean(globalThis.crypto?.subtle);
const loading = ref(false), busy = ref(false), error = ref(''), message = ref('');
const exists = ref(true), unlocked = ref(false), pending = ref(false), recoveryKey = ref(''), recoverySaved = ref(false);
const credential = ref(''), confirmation = ref(''), useRecovery = ref(false), search = ref(''), panel = ref('');
const items = ref([]), draft = ref(null), revealed = ref(false), deleteConfirmed = ref(false);
const backupCredential = ref(''), backupRecovery = ref(false), replaceConfirmed = ref(false), fileInput = ref(null);
let envelope = null, dataKey = null, pendingKeys = null, revision = '0', generation = 0, controller = new AbortController();
const locks = installVaultLock(clear);
const filtered = computed(() => {
  const q = search.value.toLocaleLowerCase();
  return items.value.filter(item => [item.title, item.username, item.url, item.tags].some(s => s.toLocaleLowerCase().includes(q)));
});
const safeUrl = computed(() => {
  try { const url = new URL(draft.value?.url); return ['https:', 'http:'].includes(url.protocol) ? url.href : ''; }
  catch { return ''; }
});
const kindLabel = kind => ({ login: 'Login', secret: 'Secret', note: 'Secure note' })[kind];
function clear() {
  generation++;
  controller.abort(); controller = new AbortController();
  envelope = null; dataKey = null; pendingKeys = null; revision = '0';
  unlocked.value = false; pending.value = false; items.value = []; draft.value = null;
  credential.value = ''; confirmation.value = ''; recoveryKey.value = ''; recoverySaved.value = false;
  backupCredential.value = ''; backupRecovery.value = false; replaceConfirmed.value = false;
  if (fileInput.value) fileInput.value.value = '';
  search.value = ''; panel.value = ''; revealed.value = false; deleteConfirmed.value = false;
  busy.value = false; loading.value = false; message.value = ''; error.value = ''; useRecovery.value = false;
}
function session() {
  const credentials = syncCredentials();
  if (!credentials) throw new Error('Sign in again before opening your vault.');
  return credentials;
}
async function run(action) {
  if (busy.value) return;
  const epoch = generation;
  busy.value = true; error.value = ''; message.value = '';
  try {
    const credentials = session();
    const valid = () => epoch === generation && credentials.account === activeAccount.value;
    const api = vaultApi(credentials.token, controller.signal);
    await action(api, valid);
  } catch (e) {
    if (epoch === generation && e.name !== 'AbortError') error.value = e instanceof TypeError ? 'Connection failed. Check your connection and try again.' : e.message;
  } finally { if (epoch === generation) { busy.value = false; loading.value = false; } }
}
function refresh() {
  clear(); loading.value = true;
  return run(async (api, valid) => {
    const snapshot = await api.get();
    if (valid()) { exists.value = Boolean(snapshot); revision = snapshot?.revision || '0'; }
  });
}
function unlock() {
  return run(async (api, valid) => {
    const value = credential.value; credential.value = '';
    const snapshot = await api.get();
    if (!valid()) return;
    if (!snapshot) { exists.value = false; throw new Error('No vault exists yet. Create one first.'); }
    const loaded = parseBackup(snapshot.envelope);
    const result = await openVault(loaded, value, useRecovery.value);
    if (!valid()) return;
    envelope = loaded; revision = snapshot.revision; dataKey = result.key;
    items.value = result.items; unlocked.value = true;
    if (useRecovery.value) { panel.value = 'keys'; message.value = 'Recovered. Set a new master passphrase and recovery key below.'; }
  });
}
function prepareKeys() {
  return run(async (_api, valid) => {
    if (credential.value !== confirmation.value) throw new Error('The passphrases do not match.');
    const passphrase = credential.value; credential.value = ''; confirmation.value = '';
    const result = await createVault(passphrase, items.value);
    if (!valid()) return;
    pendingKeys = result; recoveryKey.value = result.recoveryKey; pending.value = true; recoverySaved.value = false;
  });
}
function discardPending() { pendingKeys = null; pending.value = false; recoveryKey.value = ''; recoverySaved.value = false; }
function commitPending() {
  return run(async (api, valid) => {
    if (!pendingKeys || !recoverySaved.value) return;
    const next = pendingKeys;
    const saved = await api.save(next.envelope, revision);
    if (!valid()) return;
    envelope = next.envelope; dataKey = next.key; revision = saved.revision;
    discardPending(); exists.value = true; unlocked.value = true; panel.value = ''; draft.value = null;
    message.value = 'Vault saved. Keep the recovery key safe.';
  });
}
function selectItem(item) { draft.value = { ...item }; revealed.value = false; deleteConfirmed.value = false; }
function newItem() { selectItem({ id: crypto.randomUUID(), kind: 'login', title: '', username: '', url: '', secret: '', notes: '', tags: '', updatedAt: new Date().toISOString() }); }
function cancelEdit() { draft.value = null; revealed.value = false; deleteConfirmed.value = false; }
function editedItems() {
  const next = items.value.map(i => ({ ...i }));
  if (draft.value) {
    if (!draft.value.title.trim()) throw new Error('Give this item a title first.');
    const item = { ...draft.value, updatedAt: new Date().toISOString() };
    const index = next.findIndex(i => i.id === item.id);
    if (index < 0) next.push(item); else next[index] = item;
  }
  return next;
}
async function persist(next, api, valid) {
  const encrypted = await encryptItems(envelope, dataKey, next);
  if (!valid()) return false;
  const result = await api.save(encrypted, revision);
  if (!valid()) return false;
  envelope = encrypted; revision = result.revision; items.value = next;
  return true;
}
function saveItem() {
  return run(async (api, valid) => {
    if (await persist(editedItems(), api, valid)) { cancelEdit(); message.value = 'Item saved.'; }
  });
}
function deleteItem() {
  return run(async (api, valid) => {
    if (await persist(items.value.filter(i => i.id !== draft.value.id), api, valid)) { cancelEdit(); message.value = 'Item deleted.'; }
  });
}
function exportBackup() {
  return run(async (_api, valid) => {
    const backup = await encryptItems(envelope, dataKey, editedItems());
    if (!valid()) return;
    const url = URL.createObjectURL(new Blob([JSON.stringify(backup)], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = 'logbook-vault-encrypted.json'; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    message.value = 'Encrypted backup downloaded. It needs the passphrase or recovery key in use when exported.';
  });
}
function restoreBackup() {
  return run(async (api, valid) => {
    const file = fileInput.value?.files?.[0];
    if (!file || file.size > 1500000 || !replaceConfirmed.value) throw new Error('Choose a vault backup under 1.5 MB and confirm replacement.');
    const value = backupCredential.value; backupCredential.value = '';
    const backup = parseBackup(await file.text());
    const restored = await openVault(backup, value, backupRecovery.value);
    if (!valid()) return;
    if (await persist(restored.items, api, valid)) {
      cancelEdit(); panel.value = ''; replaceConfirmed.value = false;
      message.value = 'Backup restored with your current vault keys.';
    }
  });
}
async function copy(value) {
  try { await navigator.clipboard.writeText(value); message.value = 'Copied. Clipboard history may retain this value.'; }
  catch { error.value = 'Clipboard access is unavailable. Reveal and copy the value manually.'; }
}
watch(panel, () => { credential.value = ''; confirmation.value = ''; backupCredential.value = ''; replaceConfirmed.value = false; });
onMounted(() => { if (supported && activeAccount.value) refresh(); });
onBeforeUnmount(() => { locks.lock(); locks.dispose(); });
</script>

<style scoped>
.vault-page { min-height: 100vh; background: var(--lb-bg-elevated); color: var(--lb-text); }
.vault-main { max-width: 1120px; margin: 0 auto; padding: 16px; }
.vault-caption { color: var(--lb-text-muted); font-size: 0.85rem; line-height: 1.6; }
.vault-error { color: #b42318; font-size: 0.9rem; }
.vault-panel { border: 1px solid var(--lb-border); border-radius: var(--lb-radius-sm); padding: 16px; background: var(--lb-bg-elevated); }
.vault-narrow { max-width: 560px; margin: 20px auto; }
h2 { margin: 0 0 12px; font-size: 1rem; font-weight: 600; }
p { line-height: 1.6; }
.vault-field { display: flex; flex-direction: column; gap: 6px; margin: 12px 0; font-size: 0.85rem; }
input:not([type=checkbox]), textarea, select { box-sizing: border-box; width: 100%; padding: 8px; border: 1px solid var(--lb-border); border-radius: var(--lb-radius-sm); background: var(--lb-bg-elevated); color: var(--lb-text); font: inherit; }
textarea { resize: vertical; }
input:focus-visible, textarea:focus-visible, select:focus-visible { outline: 2px solid var(--lb-accent); outline-offset: 1px; }
.vault-check { display: flex; align-items: flex-start; gap: 8px; margin: 16px 0; font-size: 0.85rem; }
.vault-recovery { white-space: pre-wrap; overflow-wrap: anywhere; padding: 12px; border: 1px solid var(--lb-border); font-size: 0.9rem; line-height: 1.8; }
.vault-toolbar, .vault-row { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; margin: 12px 0; }
.vault-toolbar > input { flex: 1 1 240px; width: auto; }
.vault-row :deep(.el-button + .el-button), .vault-toolbar :deep(.el-button + .el-button) { margin-left: 0; }
.vault-layout { display: grid; grid-template-columns: 280px minmax(0, 1fr); gap: 12px; }
.vault-list { padding: 8px; align-self: start; max-height: 65vh; overflow: auto; }
.vault-list button { display: flex; flex-direction: column; gap: 6px; text-align: left; width: 100%; padding: 12px; border: 0; border-radius: var(--lb-radius-sm); background: transparent; color: var(--lb-text); cursor: pointer; overflow-wrap: anywhere; }
.vault-list button:hover, .vault-list button.selected { background: var(--lb-hover); }
.vault-list strong { font-size: 0.9rem; font-weight: 500; }
.vault-list span { font-size: 0.78rem; color: var(--lb-text-muted); }
@media (max-width: 700px) { .vault-layout { grid-template-columns: 1fr; } .vault-list { max-height: 220px; } .vault-main { padding: 12px; } }
</style>
