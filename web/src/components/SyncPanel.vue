<template>
  <div class="sync-panel">
    <div class="sync-bar">
      <span role="status">{{ label }}</span>
      <span v-if="!online" class="connection-status">Offline</span>
      <button class="linkish" :aria-expanded="expanded" aria-controls="sync-details" @click="expanded = !expanded">{{ expanded ? 'Hide' : 'Details' }}</button>
    </div>
    <el-dialog v-model="expanded" title="Sync details" width="min(60rem, calc(100vw - 2rem))" append-to-body>
      <div id="sync-details" class="sync-details">
        <div class="sync-sections">
          <section aria-labelledby="sync-heading">
            <h3 id="sync-heading">Sync</h3>
            <p>{{ syncStatus.pending ? `${entryCount(syncStatus.pending)} saved on this device, waiting to sync.` : 'No local changes waiting to sync.' }}</p>
            <p class="secondary">{{ lastSync }}</p>
            <p v-if="syncStatus.needsSignIn">Sign in to upload changes. You can keep writing on this device.</p>
            <p v-else-if="!online">Changes will upload when you reconnect and keep the app open.</p>
            <p v-if="syncStatus.conflicts">{{ entryCount(syncStatus.conflicts) }} need review: <router-link v-for="id in conflictIds" :key="id" :to="{ path: '/view', query: { date: id } }">{{ formatDate(id) }} </router-link></p>
            <p v-if="syncStatus.failedDates.length">Upload failed; writing is saved on this device. Will retry: <router-link v-for="id in syncStatus.failedDates" :key="id" :to="{ path: '/view', query: { date: id } }">{{ formatDate(id) }} </router-link></p>
            <div class="sync-actions">
              <button class="linkish" @click="syncNow">Sync now</button>
              <router-link v-if="syncStatus.needsSignIn" to="/login">Sign in to sync</router-link>
            </div>
          </section>
          <section aria-labelledby="offline-heading">
            <h3 id="offline-heading">Offline availability</h3>
            <p>{{ offlineStatus.ready ? 'App ready to reopen offline.' : offlineStatus.message || 'Downloading the app for offline use…' }}</p>
            <p>{{ syncStatus.historyReady ? 'History downloaded on this device.' : 'History download is incomplete.' }}</p>
            <p class="secondary">{{ entryCount(downloadedCount) }} downloaded.{{ syncStatus.historyReady ? '' : ' Only downloaded dates are available offline; other dates may already have writing on the server.' }}</p>
            <p v-if="offlineStatus.updateReady">An app update is ready. Close all Logbook tabs and reopen to update.</p>
          </section>
        </div>
        <details class="recovery-section" :open="Boolean(storageMessage)">
          <summary>Storage and recovery</summary>
          <p>Device backups include unsynced writing and copies kept when resolving conflicts.</p>
          <p class="secondary">Local entries stay on this device after logout. Clearing browser data removes them.</p>
          <p v-if="storageMessage" role="status">{{ storageMessage }}</p>
          <div class="sync-actions">
            <button class="linkish" @click="exportBackup">Export device backup</button>
            <button class="linkish" @click="protectStorage">Keep offline storage</button>
          </div>
        </details>
      </div>
    </el-dialog>
  </div>
</template>
<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { syncStatus, syncNow, onLocalChange } from '@/services/sync';
import { offlineStatus } from '@/services/offline';
import { activeAccount } from '@/services/session';
import { listLocalNotes } from '@/services/localStore.js';

const expanded = ref(false);
const online = ref(navigator.onLine);
const downloadedCount = ref(0);
const entryCount = count => `${count} ${count === 1 ? 'entry' : 'entries'}`;
const formatDate = id => `${id.slice(0, 4)}-${id.slice(4, 6)}-${id.slice(6, 8)}`;
const lastSync = computed(() => syncStatus.lastSyncedAt
  ? `Last completed sync check: ${new Date(syncStatus.lastSyncedAt).toLocaleString()}`
  : 'No completed sync check in this session yet.');
function updateConnection() { online.value = navigator.onLine; }
const conflictIds = ref([]);
const storageMessage = ref('');
const label = computed(() => {
  if (storageMessage.value === 'Could not access local storage.') return storageMessage.value;
  if (syncStatus.message && !syncStatus.message.startsWith('Offline')) return syncStatus.message;
  if (syncStatus.running) return syncStatus.pending ? `Syncing ${entryCount(syncStatus.pending)}…` : syncStatus.historyReady ? 'Checking for changes…' : 'Downloading history…';
  if (syncStatus.failedDates.length) return `${entryCount(syncStatus.failedDates.length)} could not upload · saved on this device`;
  if (syncStatus.conflicts) return 'Saved on this device · conflicts need review';
  if (syncStatus.pending) return `Saved on this device · ${entryCount(syncStatus.pending)} waiting to sync`;
  return syncStatus.lastSyncedAt ? 'All changes synced' : 'Entries save on this device';
});
async function refresh() {
  const account = activeAccount.value;
  try {
    const entries = await listLocalNotes(account);
    if (account !== activeAccount.value) return;
    conflictIds.value = entries.filter(note => note.conflict).map(note => note.noteId);
    downloadedCount.value = entries.filter(note => note.serverRevision && note.serverRevision !== '0').length;
  }
  catch { storageMessage.value = 'Could not access local storage.'; }
}
const unsubscribe = onLocalChange(refresh);
function storageBlocked() { expanded.value = true; storageMessage.value = 'Close older Logbook tabs to finish upgrading local storage.'; }
window.addEventListener('logbook-storage-blocked', storageBlocked);
onMounted(async () => {
  window.addEventListener('online', updateConnection);
  window.addEventListener('offline', updateConnection);
  await refresh();
});
onUnmounted(() => { window.removeEventListener('online', updateConnection); window.removeEventListener('offline', updateConnection); unsubscribe(); window.removeEventListener('logbook-storage-blocked', storageBlocked); });
function download(name, value) {
  const url = URL.createObjectURL(new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
async function exportBackup() {
  try { download('logbook-device-backup.json', { format: 1, account: activeAccount.value, entries: await listLocalNotes(activeAccount.value) }); }
  catch { storageMessage.value = 'Could not export local storage.'; }
}
async function protectStorage() {
  try {
    const persisted = await navigator.storage?.persist?.();
    storageMessage.value = persisted ? 'Persistent storage enabled. Keep backups of unsynced writing.' : 'The browser did not grant persistent storage. Export backups of unsynced writing.';
  } catch { storageMessage.value = 'Persistent storage is unavailable in this browser.'; }
}
</script>
<style scoped>
.sync-panel { min-width: 0; font-size: 0.78rem; color: var(--lb-text-muted); }
.sync-bar { display: flex; flex-wrap: wrap; align-items: center; gap: 0.75rem; }
.connection-status { border-left: 1px solid var(--lb-border); padding-left: 0.75rem; color: var(--lb-text-subtle); }
.sync-details { font-size: 0.78rem; color: var(--lb-text-muted); }
.sync-sections { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1.5rem; }
.sync-sections section { min-width: 0; }
.sync-details h3 { margin: 0 0 0.5rem; font-size: inherit; font-weight: 600; color: var(--lb-text); }
.secondary { color: var(--lb-text-subtle); }
.recovery-section { border-top: 1px solid var(--lb-border); margin-top: 0.8rem; padding-top: 0.6rem; }
.recovery-section summary { cursor: pointer; width: fit-content; }
.sync-details a { color: inherit; }
@media (max-width: 640px) { .sync-sections { grid-template-columns: 1fr; gap: 1rem; } }
.sync-details p { margin: 0.4rem 0; }
.sync-actions { display: flex; flex-wrap: wrap; gap: 0.75rem; margin-top: 0.5rem; }
.sync-actions a { color: inherit; }
</style>
