<template>
  <div class="content">
    <div class="editor-status" :class="editorStatusClass" aria-live="polite">
      {{ editorStatusText }}
    </div>
    <div v-if="writeFailed" class="conflict-actions">
      <button class="linkish" @click="onEditorUpdate(content)">Retry local save</button>
      <button class="linkish" @click="downloadWriting">Download this writing</button>
    </div>
    <div v-if="entry?.conflict" class="conflict-panel">
      <p v-if="entry.mergeDraft">Edit the combined draft below, remove repeated passages, then choose Use combined version. It stays on this device until you confirm.</p>
      <p v-else>This date changed on another device. Compare both versions, then choose one or edit a combined draft.</p>
      <div class="conflict-comparison">
        <section aria-label="Your writing" class="conflict-version">
          <h3>{{ entry.mergeDraft ? 'Your original writing' : 'Your writing' }}</h3>
          <pre>{{ readableNote(entry.mergeDraft ? entry.mergeDraft.local : entry.note) }}</pre>
        </section>
        <section aria-label="Server version" class="conflict-version">
          <h3>Server version</h3>
          <pre>{{ readableNote(entry.conflict.note) }}</pre>
        </section>
      </div>
      <p v-if="entry.mergeDraft && entry.mergeDraft.revision !== entry.conflict.revision" class="conflict-update">The server version changed again. Review it above before confirming your draft.</p>
      <div class="conflict-actions">
        <button v-if="!entry.mergeDraft" class="linkish" :disabled="conflictDisabled" @click="chooseVersion('combine')">Edit combined version</button>
        <button class="linkish" :disabled="conflictDisabled" @click="chooseVersion('local')">{{ entry.mergeDraft ? 'Use combined version' : 'Keep my writing' }}</button>
        <button class="linkish" :disabled="conflictDisabled" @click="chooseVersion('remote')">Use server version</button>
        <button v-if="entry.mergeDraft" class="linkish" :disabled="conflictDisabled" @click="chooseVersion('cancel')">Back to my original</button>
      </div>
      <small>Both versions remain in the device backup.</small>
    </div>
    <div class="editor">
      <el-tiptap
        :key="editorKey"
        output="json"
        :content="content"
        :extensions="extensions"
        :tooltip="false"
        @onUpdate="onEditorUpdate"
        @onCreate="onCreate"
        :readonly="!isPrimaryTab || loading || readError || conflictBusy"
      />
    </div>
    <div v-if="!isPrimaryTab" class="lock-warning">
      <div class="lock-warning__banner">
        Another tab is active. Close other tabs to edit.
      </div>
    </div>
    <div v-if="loading" class="loading" aria-live="polite" aria-label="Loading note">
      <Icon icon="eos-icons:bubble-loading" class="loading__icon" />
    </div>
  </div>
</template>


<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import moment from 'moment';
import { onBeforeRouteLeave, onBeforeRouteUpdate } from 'vue-router';
import { Icon } from '@iconify/vue';
import { createExtensions, emptyDoc, normalizeTiptapDoc } from '@/editorExt.js';
import { saveNote, fetchNote, resolveConflict, refreshRemoteNote } from '@/services/note';
import { activeAccount } from '@/services/session';
import { onLocalChange } from '@/services/sync';
import { isPrimaryTab } from '@/services/tabLock';
import { readableNote } from '@/services/conflictNotes.js';
import { getApiErrorMessage } from '@/services/apiError';

const props = defineProps({ date: String });
const content = ref(emptyDoc());
const entry = ref(null);
const editorRef = ref(null);
const loading = ref(true);
const saveError = ref('');
const writeFailed = ref(false);
const readError = ref(false);
const conflictBusy = ref(false);
const conflictDisabled = computed(() => conflictBusy.value || loading.value || readError.value || writeFailed.value || !isPrimaryTab.value);
const pendingWrites = ref(0);
const isMobileToolbar = ref(false);
const toolbarMode = computed(() => isMobileToolbar.value ? 'writing' : 'full');
const editorKey = computed(() => `editor-${props.date}-${toolbarMode.value}`);
const extensions = computed(() => createExtensions({ toolbar: toolbarMode.value }));
let mobileToolbarMediaQuery;
let applyingContent = false;
let lastDocument = '';
let viewedNote = '';
let writeQueue = Promise.resolve();
let loadSequence = 0;
let disposed = false;

function hasMeaningfulContent(node) {
  if (!node) return false;
  if (Array.isArray(node)) return node.some(hasMeaningfulContent);
  if (node.type === 'text') return Boolean((node.text || '').trim());
  if (['image', 'iframe', 'taskList', 'taskItem'].includes(node.type)) return true;
  return Array.isArray(node.content) && node.content.some(hasMeaningfulContent);
}

function payload(doc) {
  const normalized = normalizeTiptapDoc(doc);
  return hasMeaningfulContent(normalized) ? JSON.stringify(normalized) : '';
}

async function loadLocal() {
  const seq = ++loadSequence;
  const date = props.date;
  try {
    const next = await fetchNote(date);
    if (disposed || seq !== loadSequence || date !== props.date) return;
    entry.value = next;
    // Never replace edits that have not reached IndexedDB, or a failed local save.
    if (pendingWrites.value || writeFailed.value) return;
    let doc;
    try { doc = next.note ? normalizeTiptapDoc(JSON.parse(next.note)) : emptyDoc(); }
    catch { readError.value = true; saveError.value = 'This entry could not be opened. Its original data is preserved in the device backup.'; return; }
    readError.value = false;
    saveError.value = '';
    content.value = doc;
    viewedNote = next.note;
    lastDocument = payload(doc);
    if (editorRef.value && payload(editorRef.value.getJSON()) !== lastDocument) {
      applyingContent = true;
      try { editorRef.value.commands.setContent(doc); }
      finally { applyingContent = false; }
    }
  } catch (error) {
    readError.value = true;
    saveError.value = getApiErrorMessage(error, 'Could not read local storage.');
  } finally {
    if (seq === loadSequence) loading.value = false;
  }
}

function onCreate({ editor }) {
  editorRef.value = editor;
  editor.setEditable(!loading.value && !readError.value && !conflictBusy.value && isPrimaryTab.value, false);
  applyingContent = true;
  try { editor.commands.setContent(content.value); }
  finally { applyingContent = false; }
}

function onEditorUpdate(output, editor) {
  if (applyingContent || loading.value || readError.value || conflictBusy.value || !isPrimaryTab.value) return;
  const doc = normalizeTiptapDoc(editor?.getJSON ? editor.getJSON() : editorRef.value?.getJSON() || output);
  const note = payload(doc);
  if (note === lastDocument && !saveError.value) return;
  const previousNote = viewedNote;
  viewedNote = note;
  lastDocument = note;
  content.value = doc;
  const date = props.date;
  const account = activeAccount.value;
  pendingWrites.value++;
  // Capture the document and date now, before navigation or another edit.
  writeQueue = writeQueue.then(async () => {
    try {
      const saved = await saveNote({ account, noteId: date, note, previousNote });
      if (date === props.date && account === activeAccount.value) {
        entry.value = saved;
        saveError.value = '';
        writeFailed.value = false;
      }
    } catch (error) {
      if (date === props.date) {
        writeFailed.value = true;
        saveError.value = getApiErrorMessage(error, 'Could not save on this device. Keep this page open.');
      }
    } finally {
      pendingWrites.value--;
      if (!pendingWrites.value && !disposed) void loadLocal();
    }
  });
}

async function chooseVersion(choice) {
  if (conflictDisabled.value || !entry.value?.conflict) return;
  const account = activeAccount.value;
  const date = props.date;
  const expected = { note: viewedNote, revision: entry.value.conflict.revision };
  conflictBusy.value = true;
  try {
    await writeQueue;
    if (writeFailed.value || date !== props.date || account !== activeAccount.value || !isPrimaryTab.value) return;
    await resolveConflict(account, date, choice, expected);
    if (date === props.date && account === activeAccount.value) await loadLocal();
  } catch (error) {
    if (date === props.date && account === activeAccount.value) {
      await loadLocal();
      saveError.value = getApiErrorMessage(error, 'Could not resolve this entry.');
    }
  } finally { conflictBusy.value = false; }
}

const editorStatusText = computed(() => {
  if (!isPrimaryTab.value) return 'Read-only in this tab';
  if (saveError.value) return saveError.value;
  if (loading.value) return 'Opening entry…';
  if (pendingWrites.value) return 'Saving on this device…';
  if (entry.value?.mergeDraft) return 'Combined draft saved on this device · awaiting confirmation';
  if (entry.value?.conflict) return 'Saved on this device · review conflict';
  if (entry.value?.dirty) return 'Saved on this device · waiting to sync';
  if (entry.value?.unknown) return 'Not downloaded yet · you can write a draft';
  if (entry.value?.syncedAt) return `Synced ${moment(entry.value.syncedAt).format('h:mm a')}`;
  return 'Autosaves on this device';
});
const editorStatusClass = computed(() => ({ 'editor-status--error': Boolean(saveError.value), 'editor-status--muted': !saveError.value }));

function updateMobileToolbar(event) {
  if (editorRef.value) content.value = normalizeTiptapDoc(editorRef.value.getJSON());
  isMobileToolbar.value = event.matches;
}
function beforeUnload(event) {
  if (pendingWrites.value || saveError.value) { event.preventDefault(); event.returnValue = ''; }
}
watch([loading, readError, isPrimaryTab, conflictBusy], () => {
  // element-tiptap reads readonly only when constructing its editor.
  editorRef.value?.setEditable(!loading.value && !readError.value && !conflictBusy.value && isPrimaryTab.value, false);
});
async function canLeave() {
  await writeQueue;
  return !writeFailed.value && !conflictBusy.value;
}
defineExpose({ canLeave });
onBeforeRouteLeave(canLeave);
onBeforeRouteUpdate(canLeave);
function downloadWriting() {
  const url = URL.createObjectURL(new Blob([JSON.stringify({ noteId: props.date, note: payload(content.value) }, null, 2)], { type: 'application/json' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = `logbook-${props.date}.json`;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
const unsubscribe = onLocalChange(() => { void loadLocal(); });
watch(() => props.date, async () => {
  loading.value = true;
  content.value = emptyDoc();
  entry.value = null;
  saveError.value = '';
  await writeQueue;
  await loadLocal();
  void refreshRemoteNote(props.date);
}, { immediate: true, flush: 'sync' });
onMounted(() => {
  window.addEventListener('beforeunload', beforeUnload);
  mobileToolbarMediaQuery = window.matchMedia('(max-width: 768px)');
  updateMobileToolbar(mobileToolbarMediaQuery);
  mobileToolbarMediaQuery.addEventListener('change', updateMobileToolbar);
});
onUnmounted(() => {
  disposed = true;
  unsubscribe();
  window.removeEventListener('beforeunload', beforeUnload);
  mobileToolbarMediaQuery?.removeEventListener('change', updateMobileToolbar);
});
</script>

<style scoped>
.conflict-panel { border: 1px solid var(--lb-border); padding: 0.75rem; margin-bottom: 0.75rem; font-size: 0.85rem; }
.conflict-panel p { margin: 0 0 0.5rem; }
.conflict-comparison { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.75rem; }
.conflict-version { min-width: 0; border: 1px solid var(--lb-border); border-radius: var(--lb-radius-sm); padding: 0.6rem; background: #fff; }
.conflict-version h3 { margin: 0 0 0.4rem; font-size: 0.8rem; font-weight: 600; color: var(--lb-text-muted); }
.conflict-panel pre { margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; max-height: 12rem; overflow: auto; font: inherit; line-height: 1.5; }
.conflict-update { margin-top: 0.5rem !important; color: var(--lb-text-muted); }
@media (max-width: 768px) { .conflict-comparison { grid-template-columns: 1fr; } }
.conflict-actions { display: flex; flex-wrap: wrap; gap: 1rem; margin: 0.5rem 0; }

pre code {
  font-family: var(--lb-font-mono, "Fira Code", Courier, Monaco, monospace);
}

.content {
  position: relative;
}

.editor-status {
  display: flex;
  justify-content: flex-end;
  min-height: 1.45rem;
  margin: -0.2rem 0 0.35rem;
  font-size: 0.78rem;
}

.editor-status--muted {
  color: var(--lb-text-subtle, #8a9aa8);
}

.editor-status--error {
  color: var(--lb-error, #b03a2e);
}

.editor {
  border-radius: var(--lb-radius-lg, 10px);
}

.editor :deep(.el-tiptap-editor) {
  border: 1px solid var(--lb-border, #e8eaed);
  border-radius: var(--lb-radius-lg, 10px);
  overflow: hidden;
  background: #fff;
}

.editor :deep(.el-tiptap-editor__menu-bar) {
  border-bottom: 1px solid var(--lb-border, #e8eaed);
  background: var(--lb-bg-soft, #fafbfc);
}

.editor :deep(.el-tiptap-editor__menu-bubble),
.editor :deep(.el-tiptap-editor__menu-bar) {
  color: var(--lb-text-muted, #5a6d7e);
}

.editor :deep(.el-tiptap-editor__content) {
  min-height: 60vh;
  background: #fff;
  padding: 0.15rem 0.2rem;
}

.editor :deep(.ProseMirror) {
  color: var(--lb-text, #2c3e50);
  line-height: 1.75;
  padding: 1rem 1.15rem;
}

.editor :deep(.ProseMirror:focus) {
  outline: none;
}

@media (max-width: 768px) {
  .editor-status {
    min-height: 1.1rem;
    margin: -0.25rem 0 0.2rem;
    font-size: 0.72rem;
  }

  .editor :deep(.el-tiptap-editor__menu-bar) {
    display: flex;
    align-items: center;
    flex-wrap: nowrap;
    gap: 0.1rem;
    min-height: 2.35rem;
    overflow-x: auto;
    overflow-y: hidden;
    padding: 0.25rem 0.35rem;
    white-space: nowrap;
    -ms-overflow-style: none;
    scrollbar-width: none;
  }

  .editor :deep(.el-tiptap-editor__menu-bar::-webkit-scrollbar) {
    display: none;
  }

  .editor :deep(.el-tiptap-editor__menu-bar > *) {
    flex: 0 0 auto;
  }

  .editor :deep(.el-tiptap-editor__command-button) {
    width: 1.9rem;
    height: 1.9rem;
    margin: 0;
    border-radius: var(--lb-radius-sm, 6px);
  }

  .editor :deep(.el-tiptap-editor__command-button svg) {
    width: 1rem;
    height: 1rem;
  }

  .editor :deep(.ProseMirror) {
    padding: 0.85rem 0.95rem;
  }
}

.lock-warning {
  margin-top: 0.75rem;
}

.lock-warning__banner {
  background: #fff2f0;
  border: 1px solid #ffccc7;
  color: #a8071a;
  padding: 0.6rem 0.9rem;
  border-radius: 6px;
  font-size: 0.95rem;
}

.loading {
  position: absolute;
  inset: 1.8rem 0 0;
  display: grid;
  place-items: center;
  background: rgb(255 255 255 / 72%);
  color: var(--lb-text-muted, #5a6d7e);
  pointer-events: none;
}

.loading__icon {
  width: 2rem;
  height: 2rem;
}
</style>
