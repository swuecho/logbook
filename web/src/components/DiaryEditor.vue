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
      <p>This date changed on another device. Your writing is saved here; choose which version to sync.</p>
      <details><summary>View the server version</summary><pre>{{ readableNote(entry.conflict.note) }}</pre></details>
      <div class="conflict-actions">
        <button class="linkish" @click="chooseVersion('local')">Keep my writing</button>
        <button class="linkish" @click="chooseVersion('remote')">Use server version</button>
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
        :readonly="!isPrimaryTab || loading || readError"
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
import { getApiErrorMessage } from '@/services/apiError';

const props = defineProps({ date: String });
const content = ref(emptyDoc());
const entry = ref(null);
const editorRef = ref(null);
const loading = ref(true);
const saveError = ref('');
const writeFailed = ref(false);
const readError = ref(false);
const pendingWrites = ref(0);
const isMobileToolbar = ref(false);
const toolbarMode = computed(() => isMobileToolbar.value ? 'writing' : 'full');
const editorKey = computed(() => `editor-${props.date}-${toolbarMode.value}`);
const extensions = computed(() => createExtensions({ toolbar: toolbarMode.value }));
let mobileToolbarMediaQuery;
let applyingContent = false;
let lastDocument = '';
let writeQueue = Promise.resolve();
let loadSequence = 0;
let disposed = false;

function hasMeaningfulContent(node) {
  if (!node) return false;
  if (Array.isArray(node)) return node.some(hasMeaningfulContent);
  if (node.type === 'text') return Boolean((node.text || '').trim());
  if (node.type === 'image' || node.type === 'iframe') return true;
  return Array.isArray(node.content) && node.content.some(hasMeaningfulContent);
}

function payload(doc) {
  const normalized = normalizeTiptapDoc(doc);
  return hasMeaningfulContent(normalized) ? JSON.stringify(normalized) : '';
}

function readableNote(note) {
  try {
    const text = node => node?.type === 'text' ? node.text : (node?.content || []).map(text).join(node?.type === 'doc' ? '\n' : '');
    return text(JSON.parse(note || '{}'));
  } catch { return note; }
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
  editor.setEditable(!loading.value && !readError.value && isPrimaryTab.value, false);
  applyingContent = true;
  try { editor.commands.setContent(content.value); }
  finally { applyingContent = false; }
}

function onEditorUpdate(output, editor) {
  if (applyingContent || loading.value || readError.value || !isPrimaryTab.value) return;
  const doc = normalizeTiptapDoc(editor?.getJSON ? editor.getJSON() : editorRef.value?.getJSON() || output);
  const note = payload(doc);
  if (note === lastDocument && !saveError.value) return;
  lastDocument = note;
  content.value = doc;
  const date = props.date;
  const account = activeAccount.value;
  pendingWrites.value++;
  // Capture the document and date now, before navigation or another edit.
  writeQueue = writeQueue.then(async () => {
    try {
      const saved = await saveNote({ account, noteId: date, note });
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
  await writeQueue;
  try { await resolveConflict(props.date, choice); await loadLocal(); }
  catch (error) { saveError.value = getApiErrorMessage(error, 'Could not resolve this entry.'); }
}

const editorStatusText = computed(() => {
  if (!isPrimaryTab.value) return 'Read-only in this tab';
  if (saveError.value) return saveError.value;
  if (loading.value) return 'Opening entry…';
  if (pendingWrites.value) return 'Saving on this device…';
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
watch([loading, readError, isPrimaryTab], () => {
  // element-tiptap reads readonly only when constructing its editor.
  editorRef.value?.setEditable(!loading.value && !readError.value && isPrimaryTab.value, false);
});
async function canLeave() {
  await writeQueue;
  return !writeFailed.value;
}
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
.conflict-panel pre { white-space: pre-wrap; max-height: 14rem; overflow: auto; }
.conflict-actions { display: flex; gap: 1rem; margin: 0.5rem 0; }

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
