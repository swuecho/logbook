<template>
  <div class="sync-panel" role="status">
    <span>{{ label }}</span>
    <button class="linkish" @click="expanded = !expanded">{{ expanded ? 'Hide' : 'Details' }}</button>
    <div v-if="expanded" class="sync-details">
      <p>{{ offlineStatus.ready ? 'App ready to reopen offline.' : offlineStatus.message || 'Downloading the app for offline use…' }}</p>
      <p>{{ syncStatus.historyReady ? 'History downloaded on this device.' : 'History download is incomplete. Undownloaded dates may already have an entry on the server.' }}</p>
      <p v-if="offlineStatus.updateReady">An app update is ready. Close all Logbook tabs and reopen to update.</p>
      <p v-if="syncStatus.conflicts">{{ syncStatus.conflicts }} date(s) need review: <router-link v-for="id in conflictIds" :key="id" :to="{ path: '/view', query: { date: id } }">{{ id }} </router-link></p>
      <p v-if="syncStatus.failedDates.length">Upload failed; writing is saved on this device. Will retry: <router-link v-for="id in syncStatus.failedDates" :key="id" :to="{ path: '/view', query: { date: id } }">{{ id }} </router-link></p>
      <p v-if="legacyCount">{{ legacyCount }} entries from the old cache are preserved separately because their account is unknown. Export them for recovery before clearing site data.</p>
      <p>Local entries stay on this device after logout. Clearing browser data removes them.</p>
      <p v-if="storageMessage">{{ storageMessage }}</p>
      <div class="sync-actions">
        <button class="linkish" @click="syncNow">Sync now</button>
        <router-link to="/login">Sign in to sync</router-link>
        <button class="linkish" @click="exportBackup">Export device backup</button>
        <button v-if="legacyCount" class="linkish" @click="exportLegacy">Export old cache</button>
        <button class="linkish" @click="protectStorage">Keep offline storage</button>
      </div>
    </div>
  </div>
</template>
<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { syncStatus, syncNow, onLocalChange } from '@/services/sync';
import { offlineStatus } from '@/services/offline';
import { activeAccount } from '@/services/session';
import { legacyNotes, listLocalNotes } from '@/services/localStore.js';

const expanded = ref(false);
const legacyCount = ref(0);
const conflictIds = ref([]);
const storageMessage = ref('');
const label = computed(() => {
  if (syncStatus.message) return syncStatus.message;
  if (syncStatus.running) return syncStatus.historyReady ? 'Syncing…' : 'Syncing and downloading history…';
  if (syncStatus.failedDates.length) return `${syncStatus.failedDates.length} date(s) could not upload · saved on this device`;
  if (syncStatus.conflicts) return 'Saved on this device · conflicts need review';
  if (syncStatus.pending) return `${syncStatus.pending} entries waiting to sync`;
  return syncStatus.lastSyncedAt ? 'Synced' : 'Entries save on this device';
});
async function refresh() {
  try { conflictIds.value = (await listLocalNotes(activeAccount.value)).filter(note => note.conflict).map(note => note.noteId); }
  catch { storageMessage.value = 'Could not access local storage.'; }
}
const unsubscribe = onLocalChange(refresh);
function storageBlocked() { expanded.value = true; storageMessage.value = 'Close older Logbook tabs to finish upgrading local storage. Their old cache will be preserved.'; }
window.addEventListener('logbook-storage-blocked', storageBlocked);
onMounted(async () => {
  try { legacyCount.value = (await legacyNotes()).length; } catch { storageMessage.value = 'Could not access the old cache.'; }
  await refresh();
});
onUnmounted(() => { unsubscribe(); window.removeEventListener('logbook-storage-blocked', storageBlocked); });
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
async function exportLegacy() {
  try { download('logbook-old-cache.json', { format: 1, account: 'unknown', entries: await legacyNotes() }); }
  catch { storageMessage.value = 'Could not export the old cache.'; }
}
async function protectStorage() {
  try {
    const persisted = await navigator.storage?.persist?.();
    storageMessage.value = persisted ? 'Persistent storage enabled. Keep backups of unsynced writing.' : 'The browser did not grant persistent storage. Export backups of unsynced writing.';
  } catch { storageMessage.value = 'Persistent storage is unavailable in this browser.'; }
}
</script>
<style scoped>
.sync-panel { background: #fff; border-bottom: 1px solid var(--lb-border, #e8eaed); padding: 0.4rem 1rem; font-size: 0.78rem; color: var(--lb-text-muted); }
.sync-panel > button { margin-left: 0.75rem; }
.sync-details { max-width: 52rem; padding: 0.25rem 0; }
.sync-details p { margin: 0.4rem 0; }
.sync-actions { display: flex; flex-wrap: wrap; gap: 0.75rem; margin-top: 0.5rem; }
.sync-actions a { color: inherit; }
</style>
