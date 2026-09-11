<template>
  <div class="app-page vault-page">
    <AppTopBar title="Vault" :show-calendar="false" :show-search="false" :show-content="false">
      <template #actions-before>
        <button v-if="unlocked || pending" class="linkish vault-lock" type="button" @click="locks.lock()">
          <Icon :icon="lockIcon" aria-hidden="true" /> Lock
        </button>
      </template>
    </AppTopBar>
    <main class="app-shell vault-main">
      <section v-if="!activeAccount" class="app-panel vault-panel vault-narrow">
        <div class="vault-state-icon"><Icon :icon="lockIcon" aria-hidden="true" /></div>
        <h2>Your private vault</h2>
        <p>Sign in to access your passwords and secrets.</p>
        <router-link to="/login" class="linkish">Sign in</router-link>
      </section>
      <section v-else-if="!supported" class="app-panel vault-panel vault-narrow">
        <h2>A secure connection is needed</h2>
        <p>The vault requires HTTPS and a browser with Web Crypto.</p>
      </section>
      <template v-else>
        <div class="vault-status-strip">
          <span><Icon :icon="shieldIcon" aria-hidden="true" /> Encrypted in your browser</span>
          <span>Auto-lock · 5 min</span>
        </div>
        <p v-if="error" role="alert" class="vault-feedback vault-error">{{ error }}</p>
        <p v-if="message" role="status" class="vault-feedback">{{ message }}</p>
        <p v-if="loading" class="vault-loading">Loading vault…</p>
        <section v-else-if="pending" class="app-panel vault-panel vault-narrow">
          <div class="vault-state-icon"><Icon :icon="keyIcon" aria-hidden="true" /></div>
          <h2>Save your recovery key</h2>
          <p>Keep this key outside Logbook. It unlocks your vault if you forget your master passphrase.</p>
          <pre class="vault-recovery" data-testid="recovery-key">{{ recoveryKey }}</pre>
          <el-button text @click="copy(recoveryKey)">Copy recovery key</el-button>
          <label class="vault-check"><input v-model="recoverySaved" type="checkbox"> I saved this recovery key outside Logbook.</label>
          <p class="vault-caption">Without your master passphrase or recovery key, your secrets cannot be recovered.</p>
          <p v-if="unlocked" class="vault-caption">Old backups still need their original passphrase or recovery key.</p>
          <div class="vault-form-actions">
            <el-button type="primary" plain :disabled="!recoverySaved" :loading="busy" @click="commitPending">Finish and save</el-button>
            <el-button text :disabled="busy" @click="discardPending">Cancel</el-button>
          </div>
        </section>
        <section v-else-if="!unlocked" class="app-panel vault-panel vault-narrow">
          <div class="vault-state-icon"><Icon :icon="lockIcon" aria-hidden="true" /></div>
          <h2>{{ exists ? 'Unlock vault' : 'Create your vault' }}</h2>
          <p>{{ exists ? 'Enter your master passphrase to open your vault.' : 'Choose a master passphrase different from your Logbook password. It stays in your browser.' }}</p>
          <form @submit.prevent="exists ? unlock() : prepareKeys()">
            <label v-if="exists" class="vault-check"><input v-model="useRecovery" type="checkbox"> Use recovery key</label>
            <label class="vault-field">{{ useRecovery && exists ? 'Recovery key' : 'Master passphrase' }}
              <input v-model="credential" type="password" :autocomplete="exists ? 'off' : 'new-password'" maxlength="1024" required>
            </label>
            <label v-if="!exists" class="vault-field">Confirm master passphrase
              <input v-model="confirmation" type="password" autocomplete="new-password" maxlength="1024" required>
            </label>
            <p v-if="!exists" class="vault-caption">Use at least 15 characters. A few unrelated words work well.</p>
            <div class="vault-form-actions">
              <el-button native-type="submit" type="primary" plain :loading="busy">{{ exists ? 'Unlock' : 'Create vault' }}</el-button>
              <el-button text :disabled="busy" @click="refresh">Reload</el-button>
            </div>
          </form>
          <p v-if="!exists" class="vault-card-footnote">Have a backup? Create a vault, then choose Restore backup.</p>
        </section>
        <template v-else>
          <div class="vault-toolbar">
            <div class="vault-search">
              <Icon :icon="searchIcon" aria-hidden="true" />
              <input v-model="search" aria-label="Search vault" placeholder="Search your vault…" type="search" autocomplete="off">
            </div>
            <el-button type="primary" plain :disabled="busy" @click="newItem"><Icon :icon="plusIcon" class="vault-button-icon" aria-hidden="true" /> New item</el-button>
            <div class="vault-tools" aria-label="Vault tools">
              <el-button text :disabled="busy" @click="exportBackup">Encrypted backup</el-button>
              <el-button text :disabled="busy" :aria-pressed="panel === 'restore'" @click="panel = panel === 'restore' ? '' : 'restore'">Restore backup</el-button>
              <el-button text :disabled="busy" :aria-pressed="panel === 'keys'" @click="panel = panel === 'keys' ? '' : 'keys'">Change keys</el-button>
            </div>
          </div>
          <section v-if="panel === 'keys'" class="app-panel vault-panel vault-tools-panel">
            <div class="vault-panel-heading">
              <h2>Change master passphrase and recovery key</h2>
              <button class="linkish" :disabled="busy" @click="panel = ''">Done</button>
            </div>
            <p>Update the keys for all saved items. Save any editor changes first.</p>
            <form @submit.prevent="prepareKeys">
              <div class="vault-field-grid">
                <label class="vault-field">New master passphrase<input v-model="credential" type="password" autocomplete="new-password" maxlength="1024" required></label>
                <label class="vault-field">Confirm master passphrase<input v-model="confirmation" type="password" autocomplete="new-password" maxlength="1024" required></label>
              </div>
              <el-button native-type="submit" type="primary" plain :loading="busy">Generate new keys</el-button>
            </form>
          </section>
          <section v-if="panel === 'restore'" class="app-panel vault-panel vault-tools-panel">
            <div class="vault-panel-heading">
              <h2>Restore encrypted backup</h2>
              <button class="linkish" :disabled="busy" @click="panel = ''">Done</button>
            </div>
            <p>Restore replaces your current items. Export a backup first to keep a copy. Your current vault keys stay in use.</p>
            <form @submit.prevent="restoreBackup">
              <div class="vault-field-grid">
                <label class="vault-field">Backup file<input ref="fileInput" type="file" accept=".json,application/json" required></label>
                <label class="vault-field">Backup {{ backupRecovery ? 'recovery key' : 'master passphrase' }}<input v-model="backupCredential" type="password" autocomplete="off" maxlength="1024" required></label>
              </div>
              <label class="vault-check"><input v-model="backupRecovery" type="checkbox"> Unlock backup with recovery key</label>
              <label class="vault-check"><input v-model="replaceConfirmed" type="checkbox"> Replace my current vault items with this backup.</label>
              <el-button native-type="submit" type="primary" plain :disabled="!replaceConfirmed" :loading="busy">Restore and save</el-button>
            </form>
          </section>
          <div class="app-panel vault-layout">
            <aside class="vault-list" aria-label="Vault items">
              <div class="vault-list-heading"><h2>All items</h2><span>{{ items.length }}</span></div>
              <div class="vault-list-content">
                <p v-if="!filtered.length" class="vault-list-empty">{{ items.length ? 'No matching items.' : 'No items yet. Add a login, secret, or secure note.' }}</p>
                <button v-for="item in filtered" :key="item.id" type="button" :disabled="busy" :aria-pressed="draft?.id === item.id" :class="{ selected: draft?.id === item.id }" @click="selectItem(item)">
                  <Icon :icon="kindIcon(item.kind)" class="vault-item-icon" aria-hidden="true" />
                  <span class="vault-item-text"><strong>{{ item.title || 'Untitled' }}</strong><span>{{ item.username || kindLabel(item.kind) }}</span></span>
                </button>
              </div>
            </aside>
            <section class="vault-editor">
              <form v-if="draft" @submit.prevent="saveItem" autocomplete="off">
                <div class="vault-editor-heading">
                  <h2>{{ items.some(i => i.id === draft.id) ? 'Item details' : 'New item' }}</h2>
                  <span>{{ kindLabel(draft.kind) }}</span>
                </div>
                <div class="vault-editor-body">
                  <div class="vault-field-grid vault-field-grid--title">
                    <label class="vault-field">Title<input v-model="draft.title" maxlength="2000" placeholder="Give this item a name" required></label>
                    <label class="vault-field">Type<select v-model="draft.kind" aria-label="Type"><option value="login">Login</option><option value="secret">Secret</option><option value="note">Secure note</option></select></label>
                  </div>
                  <div class="vault-field-grid" :class="{ 'vault-field-grid--single': draft.kind !== 'login' }">
                    <label v-if="draft.kind === 'login'" class="vault-field">Username<input v-model="draft.username" maxlength="2000" autocomplete="off" placeholder="Email or username"></label>
                    <div class="vault-url-field">
                      <label class="vault-field">URL<input v-model="draft.url" maxlength="2000" type="text" placeholder="https://"></label>
                      <a v-if="safeUrl" class="vault-open-url linkish" :href="safeUrl" target="_blank" rel="noopener noreferrer" referrerpolicy="no-referrer">Open URL</a>
                    </div>
                  </div>
                  <div v-if="draft.kind !== 'note'" class="vault-secret-field">
                    <label class="vault-field">{{ draft.kind === 'login' ? 'Password' : 'Secret value' }}
                      <textarea v-if="revealed" v-model="draft.secret" rows="4" maxlength="100000" spellcheck="false" autocapitalize="off"></textarea>
                      <input v-else v-model="draft.secret" type="password" autocomplete="new-password" maxlength="100000" :readonly="draft.kind === 'secret'" :placeholder="draft.kind === 'secret' ? 'Reveal to edit this secret' : ''">
                    </label>
                    <div class="vault-secret-actions">
                      <el-button text @click="revealed = !revealed">{{ revealed ? 'Hide' : 'Reveal' }}</el-button>
                      <el-button text @click="copy(draft.secret)">Copy secret</el-button>
                      <el-button text @click="draft.secret = generatePassword()">Generate password</el-button>
                    </div>
                  </div>
                  <label class="vault-field">{{ draft.kind === 'note' ? 'Secure note' : 'Notes' }}<textarea v-model="draft.notes" :rows="draft.kind === 'note' ? 8 : 4" maxlength="100000" spellcheck="false" placeholder="Anything else to keep with this item"></textarea></label>
                  <label class="vault-field">Tags<input v-model="draft.tags" maxlength="2000" placeholder="work, personal"></label>
                </div>
                <div class="vault-editor-actions">
                  <el-button native-type="submit" type="primary" plain :loading="busy">Save item</el-button>
                  <el-button text :disabled="busy" @click="cancelEdit">Close</el-button>
                  <el-button v-if="items.some(i => i.id === draft.id)" class="vault-delete" text :disabled="busy" @click="deleteConfirmed = !deleteConfirmed">Delete item</el-button>
                </div>
                <div v-if="deleteConfirmed" class="vault-delete-confirm"><span>Delete this item from the current vault?</span><el-button type="danger" plain :loading="busy" @click="deleteItem">Confirm delete</el-button></div>
              </form>
              <div v-else class="vault-empty">
                <Icon :icon="items.length ? keyIcon : shieldIcon" aria-hidden="true" />
                <h2>{{ items.length ? 'Choose an item' : 'A place for your private details' }}</h2>
                <p>{{ items.length ? 'View or edit its details here.' : 'Add a login, secret, or secure note to get started.' }}</p>
                <span>Only decrypted in this browser.</span>
              </div>
            </section>
          </div>
          <div class="vault-footer">
            <span>Online only · Backups include your current editor</span>
            <span>Copied values may remain in clipboard history.</span>
          </div>
        </template>
        <p v-if="!loading" class="vault-lock-note">Locks when you leave or hide this page. Unsaved edits are cleared on lock.</p>
      </template>
    </main>
  </div>
</template>

<script setup>
import { computed, onMounted, onBeforeUnmount, ref, watch } from 'vue';
import { Icon } from '@iconify/vue';
import lockIcon from '@iconify/icons-mdi/lock-outline';
import keyIcon from '@iconify/icons-mdi/key-outline';
import noteIcon from '@iconify/icons-mdi/note-text-outline';
import searchIcon from '@iconify/icons-mdi/magnify';
import plusIcon from '@iconify/icons-mdi/plus';
import shieldIcon from '@iconify/icons-mdi/shield-lock-outline';
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
const kindIcon = kind => ({ login: keyIcon, secret: lockIcon, note: noteIcon })[kind];
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
    const api = vaultApi(credentials, controller.signal);
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
.vault-page { background: var(--lb-bg-elevated); }
.vault-main { padding-top: 16px; padding-bottom: 32px; }
.vault-main :deep(.el-button) { font-family: inherit; font-size: 12px; }
.vault-main :deep(.el-button + .el-button) { margin-left: 0; }
.vault-main :deep(.el-button.is-text) { color: var(--lb-text-muted); padding-left: 9px; padding-right: 9px; }
.vault-main :deep(.el-button.is-text[aria-pressed='true']) { color: var(--lb-text); background: var(--lb-hover); }
.vault-main :deep(.el-button--primary.is-plain) { background: var(--lb-bg-elevated); border-color: var(--lb-border-strong); color: var(--lb-accent-strong); }
.vault-main :deep(.el-button--primary.is-plain:not(.is-disabled):hover),
.vault-main :deep(.el-button--primary.is-plain:not(.is-disabled):focus-visible) { background: var(--lb-hover); border-color: var(--lb-accent); color: var(--lb-accent-strong); }
.vault-main :deep(.el-button--primary.is-plain.is-disabled) { color: var(--lb-text-subtle); border-color: var(--lb-border); }
.vault-lock { font-size: 13px; }
.vault-lock svg, .vault-button-icon { width: 16px; height: 16px; }
.vault-button-icon { margin-right: 5px; }
.vault-status-strip, .vault-status-strip > span { display: flex; align-items: center; gap: 7px; }
.vault-status-strip { justify-content: space-between; gap: 12px; color: var(--lb-text-muted); font-size: 11px; margin-bottom: 18px; }
.vault-status-strip svg { width: 15px; height: 15px; }
.vault-status-strip > span:last-child { color: var(--lb-text-subtle); white-space: nowrap; }
.vault-caption, .vault-card-footnote, .vault-loading { color: var(--lb-text-muted); font-size: 12px; line-height: 1.7; }
.vault-feedback { margin: 0 0 14px; padding: 10px 12px; border: 1px solid var(--lb-border); border-radius: var(--lb-radius-sm); color: var(--lb-text-muted); font-size: 12px; overflow-wrap: anywhere; }
.vault-error { color: var(--lb-error); }
.vault-panel { padding: 24px; }
.vault-narrow { max-width: 440px; margin: clamp(28px, 7vh, 72px) auto 28px; }
.vault-panel h2 { margin: 0; font-size: 15px; font-weight: 600; }
.vault-panel > p { margin: 10px 0 20px; color: var(--lb-text-muted); font-size: 12px; line-height: 1.8; }
.vault-state-icon { display: flex; align-items: center; justify-content: center; width: 36px; height: 36px; border: 1px solid var(--lb-border); border-radius: var(--lb-radius-sm); color: var(--lb-text-muted); margin-bottom: 18px; }
.vault-state-icon svg { width: 20px; height: 20px; }
.vault-form-actions { display: flex; align-items: center; gap: 8px; margin-top: 20px; }
.vault-panel > .vault-card-footnote { border-top: 1px solid var(--lb-border); margin: 24px -24px -24px; padding: 14px 24px; }
.vault-field { display: flex; flex-direction: column; gap: 7px; min-width: 0; margin: 0 0 16px; color: var(--lb-text-muted); font-size: 12px; line-height: 1.4; }
input:not([type=checkbox]), textarea, select { width: 100%; min-width: 0; padding: 9px 10px; border: 1px solid var(--lb-border-strong); border-radius: var(--lb-radius-sm); background: var(--lb-bg-elevated); color: var(--lb-text); font: inherit; font-size: 13px; line-height: 1.5; transition: border-color 0.15s; }
input::placeholder, textarea::placeholder { color: var(--lb-text-subtle); opacity: 1; }
input:not([type=checkbox]):hover, textarea:hover, select:hover { border-color: var(--lb-text-subtle); }
textarea { resize: vertical; }
input:focus-visible, textarea:focus-visible, select:focus-visible { outline: 2px solid var(--lb-focus-ring); outline-offset: 1px; border-color: var(--lb-accent); }
input[type=checkbox] { flex: none; margin: 3px 0 0; accent-color: var(--lb-accent); }
input[type=file] { padding: 6px; font-size: 12px; }
input[type=file]::file-selector-button { padding: 3px 8px; border: 0; border-radius: 3px; background: var(--lb-hover); color: var(--lb-text-muted); font: inherit; margin-right: 8px; }
.vault-check { display: flex; align-items: flex-start; gap: 9px; margin: 16px 0; font-size: 12px; color: var(--lb-text-muted); line-height: 1.7; }
.vault-recovery { white-space: pre-wrap; overflow-wrap: anywhere; padding: 16px; border: 1px solid var(--lb-border); border-radius: var(--lb-radius-sm); font-family: var(--lb-font-mono); font-size: 13px; line-height: 1.9; letter-spacing: 0.03em; margin: 16px 0 4px; }
.vault-toolbar { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; margin-bottom: 14px; }
.vault-search { position: relative; flex: 1 1 210px; min-width: 0; }
.vault-search svg { position: absolute; left: 11px; top: 50%; transform: translateY(-50%); width: 17px; height: 17px; color: var(--lb-text-subtle); pointer-events: none; }
.vault-search input { padding-left: 35px; height: 36px; border-color: var(--lb-border); font-size: 12px; }
.vault-tools { display: flex; align-items: center; flex-wrap: wrap; gap: 0; padding-left: 8px; border-left: 1px solid var(--lb-border); }
.vault-tools-panel { margin-bottom: 14px; padding: 20px; }
.vault-panel-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
.vault-panel-heading h2 { font-size: 13px; }
.vault-panel-heading button { font-size: 12px; }
.vault-field-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
.vault-field-grid--title { grid-template-columns: minmax(0, 2fr) minmax(0, 1fr); }
.vault-field-grid--single { grid-template-columns: minmax(0, 1fr); }
.vault-layout { display: grid; grid-template-columns: 260px minmax(0, 1fr); overflow: hidden; min-height: 490px; }
.vault-list { border-right: 1px solid var(--lb-border); min-width: 0; }
.vault-list-heading, .vault-editor-heading { display: flex; align-items: center; justify-content: space-between; height: 48px; padding: 0 20px; border-bottom: 1px solid var(--lb-border); gap: 12px; }
.vault-list-heading h2, .vault-editor-heading h2 { font-size: 12px; font-weight: 500; margin: 0; }
.vault-list-heading > span, .vault-editor-heading > span { font-size: 11px; color: var(--lb-text-subtle); }
.vault-list-content { padding: 6px; max-height: 65vh; overflow-y: auto; }
.vault-list-content button { display: flex; align-items: center; gap: 10px; text-align: left; width: 100%; padding: 12px 10px; border: 1px solid transparent; border-radius: var(--lb-radius-sm); background: transparent; color: var(--lb-text); cursor: pointer; font: inherit; }
.vault-list-content button:hover { background: var(--lb-hover); }
.vault-list-content button.selected { background: var(--lb-hover); border-color: var(--lb-border); }
.vault-list-content button:focus-visible { outline: 2px solid var(--lb-accent); outline-offset: -2px; }
.vault-item-icon { flex: none; width: 18px; height: 18px; color: var(--lb-text-muted); }
.vault-item-text { display: flex; flex-direction: column; gap: 4px; min-width: 0; }
.vault-item-text strong { font-size: 12px; font-weight: 500; overflow-wrap: anywhere; }
.vault-item-text > span { font-size: 11px; color: var(--lb-text-subtle); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.vault-list-empty { padding: 12px 14px; color: var(--lb-text-subtle); font-size: 12px; line-height: 1.8; }
.vault-editor { min-width: 0; }
.vault-editor-body { padding: 20px 24px 4px; }
.vault-url-field { position: relative; min-width: 0; }
.vault-open-url { position: absolute; right: 0; top: -4px; font-size: 10px; min-height: 22px; }
.vault-secret-field { margin-bottom: 16px; }
.vault-secret-field .vault-field { margin-bottom: 3px; }
.vault-secret-actions { display: flex; flex-wrap: wrap; gap: 0; margin-left: -9px; }
.vault-secret-actions :deep(.el-button) { height: 28px; font-size: 11px; }
.vault-editor-actions { display: flex; align-items: center; gap: 8px; padding: 14px 24px; border-top: 1px solid var(--lb-border); }
.vault-editor-actions :deep(.el-button.vault-delete) { margin-left: auto; color: var(--lb-text-subtle); }
.vault-editor-actions :deep(.vault-delete:hover) { color: var(--lb-error); }
.vault-delete-confirm { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; padding: 0 24px 16px; font-size: 12px; color: var(--lb-error); }
.vault-empty { min-height: 490px; display: flex; align-items: center; justify-content: center; flex-direction: column; text-align: center; padding: 32px; }
.vault-empty > svg { width: 28px; height: 28px; color: var(--lb-text-subtle); margin-bottom: 16px; }
.vault-empty h2 { font-size: 14px; font-weight: 500; margin: 0 0 8px; }
.vault-empty p { color: var(--lb-text-muted); font-size: 12px; margin: 0; line-height: 1.8; }
.vault-empty > span { color: var(--lb-text-subtle); font-size: 11px; margin-top: 16px; }
.vault-footer { display: flex; justify-content: space-between; flex-wrap: wrap; gap: 4px 16px; margin-top: 12px; color: var(--lb-text-muted); font-size: 11px; line-height: 1.8; }
.vault-lock-note { text-align: center; color: var(--lb-text-muted); font-size: 11px; margin-top: 24px; line-height: 1.8; }
@media (max-width: 850px) {
  .vault-tools { flex-basis: 100%; padding-left: 0; border-left: 0; margin-left: -9px; }
  .vault-toolbar { gap: 8px 10px; }
  .vault-layout { grid-template-columns: 215px minmax(0, 1fr); }
  .vault-editor-body { padding: 18px 18px 2px; }
  .vault-editor-actions { padding: 14px 18px; }
}
@media (max-width: 600px) {
  .vault-main { padding-top: 12px; }
  .vault-status-strip { font-size: 10px; gap: 8px; margin-bottom: 14px; }
  .vault-status-strip svg { display: none; }
  .vault-search { flex-basis: 160px; }
  .vault-layout { grid-template-columns: minmax(0, 1fr); min-height: 0; }
  .vault-list { border-right: 0; border-bottom: 1px solid var(--lb-border); }
  .vault-list-heading { height: 40px; padding: 0 16px; }
  .vault-list-content { max-height: 190px; }
  .vault-list-content button { padding: 10px; }
  .vault-editor-heading { padding: 0 16px; }
  .vault-editor-body { padding: 18px 16px 2px; }
  .vault-field-grid { grid-template-columns: minmax(0, 1fr); gap: 0; }
  .vault-field-grid--title { grid-template-columns: minmax(0, 1fr) 105px; gap: 12px; }
  .vault-editor-actions { padding: 14px 16px; }
  .vault-empty { min-height: 270px; padding: 24px; }
  .vault-narrow { margin-top: 28px; padding: 20px; }
  .vault-panel > .vault-card-footnote { margin: 20px -20px -20px; padding: 14px 20px; }
  .vault-tools-panel { padding: 16px; }
  .vault-panel-heading { align-items: flex-start; }
  .vault-lock-note { text-align: left; }
}
@media (prefers-reduced-motion: reduce) { input, textarea, select { transition: none; } }
</style>
