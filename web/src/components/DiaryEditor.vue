<template>
  <div class="content">
    <div class="editor-status" :class="editorStatusClass" aria-live="polite">
      {{ editorStatusText }}
    </div>
    <div class="editor">
      <el-tiptap
        :key="editorKey"
        output="json"
        :content="content"
        :extensions="extensions"
        :tooltip="false"
        @onUpdate="debouncedOnUpdate"
        @onCreate="onCreate"
        :readonly="!isPrimaryTab"
      />
    </div>
    <div v-if="!isPrimaryTab" class="lock-warning">
      <div class="lock-warning__banner">
        Another tab is active. Close other tabs to edit.
      </div>
    </div>
    <div v-if="isFetching" class="loading" aria-live="polite" aria-label="Loading note">
      <Icon icon="eos-icons:bubble-loading" class="loading__icon" />
    </div>
  </div>
</template>


<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue';
import { createExtensions, emptyDoc, normalizeTiptapDoc } from '@/editorExt.js';
import { useMutation, useQuery } from '@tanstack/vue-query';
import router from '@/router';
import { debounce } from 'lodash';

import { saveNote, fetchNote } from '@/services/note.ts';
import { isPrimaryTab } from '@/services/tabLock';
import { getApiErrorMessage, isUnauthorized } from '@/services/apiError';

import { useQueryClient } from '@tanstack/vue-query';
const queryClient = useQueryClient();

const props = defineProps({
  date: String
});

const content = ref(emptyDoc());
const noteJsonRef = ref(null);
const lastSubmittedNote = ref(null);
const lastSavedAt = ref(null);
const saveError = ref('');
const isMobileToolbar = ref(false);
let mobileToolbarMediaQuery = null;

function areDocsEqual(left, right) {
  return JSON.stringify(normalizeTiptapDoc(left)) === JSON.stringify(normalizeTiptapDoc(right));
}

function hasMeaningfulContent(node) {
  if (!node) return false;
  if (Array.isArray(node)) return node.some(hasMeaningfulContent);
  if (node.type === 'text') return Boolean((node.text || '').trim());
  if (node.type === 'image' || node.type === 'iframe') return true;
  if (Array.isArray(node.content)) return node.content.some(hasMeaningfulContent);
  return false;
}

function notePayloadFromDoc(doc) {
  const normalized = normalizeTiptapDoc(doc);
  return hasMeaningfulContent(normalized) ? JSON.stringify(normalized) : '';
}

const toolbarMode = computed(() => (isMobileToolbar.value ? 'writing' : 'full'));
const editorKey = computed(() => `editor-${props.date}-${toolbarMode.value}`);
const extensions = computed(() => createExtensions({ toolbar: toolbarMode.value }));

function updateMobileToolbar(event) {
  if (editorRef.value && event.matches !== isMobileToolbar.value) {
    content.value = normalizeTiptapDoc(editorRef.value.getJSON());
  }
  isMobileToolbar.value = event.matches;
}

onMounted(() => {
  if (typeof window === 'undefined' || !window.matchMedia) return;

  mobileToolbarMediaQuery = window.matchMedia('(max-width: 768px)');
  updateMobileToolbar(mobileToolbarMediaQuery);

  if (mobileToolbarMediaQuery.addEventListener) {
    mobileToolbarMediaQuery.addEventListener('change', updateMobileToolbar);
  } else {
    mobileToolbarMediaQuery.addListener(updateMobileToolbar);
  }
});

onUnmounted(() => {
  debouncedSave.flush();
  invalidateAfterSave.flush();
  debouncedOnUpdate.cancel();

  if (!mobileToolbarMediaQuery) return;

  if (mobileToolbarMediaQuery.removeEventListener) {
    mobileToolbarMediaQuery.removeEventListener('change', updateMobileToolbar);
  } else {
    mobileToolbarMediaQuery.removeListener(updateMobileToolbar);
  }
});

const queryKey = computed(() => ['diaryContent', props.date]);
const { data: noteData, isLoading, isFetching, error: getNoteError } = useQuery({
  queryKey: queryKey,
  queryFn: () => fetchNote(props.date),
  retry: false,
  staleTime: 1000 * 60 * 5,
});

watch(getNoteError, (error) => {
  if (isUnauthorized(error)) {
    router.push({ name: 'login' });
  }
  console.error(getApiErrorMessage(error, 'Error fetching diary.'));
})

watch(noteData, (newData) => {
  if (newData) {
    if (newData.note) {
      try {
        const nextContent = normalizeTiptapDoc(
          typeof newData.note === 'string' ? JSON.parse(newData.note) : newData.note
        );
        content.value = nextContent;
        lastSubmittedNote.value = notePayloadFromDoc(nextContent);
      } catch (parseError) {
        console.error('Failed to parse diary note:', parseError);
        content.value = emptyDoc();
        lastSubmittedNote.value = '';
      }
    } else {
      content.value = emptyDoc();
      lastSubmittedNote.value = '';
    }

    // Update the editor content when new data is loaded
    if (editorRef.value) {
      const editorContent = editorRef.value.getJSON();
      if (!areDocsEqual(editorContent, content.value)) {
        editorRef.value.commands.setContent(content.value);
      }
    }
  }
});


const editorRef = ref(null);

const invalidateAfterSave = debounce((noteId) => {
  queryClient.invalidateQueries({ queryKey: ['diaryIds'] });
  queryClient.invalidateQueries({ queryKey: ['todoContent'] });
  queryClient.invalidateQueries({ queryKey: ['MdContent', noteId] });
}, 2000, { maxWait: 5000 });

const onCreate = ({ editor }) => {
  editorRef.value = editor;
  editor.commands.setContent(content.value);
};


const editorStatusText = computed(() => {
  if (!isPrimaryTab.value) return 'Read-only in this tab';
  if (isLoading.value) return 'Loading entry...';
  if (saveError.value) return saveError.value;
  if (isSaving.value) return 'Saving...';
  if (lastSavedAt.value) return `Saved ${moment(lastSavedAt.value).format('h:mm a')}`;
  return 'Autosaves changes';
});

const editorStatusClass = computed(() => ({
  'editor-status--error': Boolean(saveError.value),
  'editor-status--muted': !saveError.value,
}));

const { mutate: updateNote, isPending: isSaving } = useMutation({
  mutationFn: saveNote,
  networkMode: 'always',
  onMutate: (note) => {
    saveError.value = '';
    queryClient.setQueryData(['diaryContent', note.noteId], (current) => ({
      ...(current || {}),
      ...note,
    }));
  },
  onSuccess: (_data, note) => {
    // queryClient.invalidateQueries({ queryKey: ['diaryContent', props.date, ] });
    // if invalid the diaryContent, it will cause the editor to refresh content. 
    // will overwrite the current content delta.  (typed in after last put request)
    if (note.noteId === props.date) {
      lastSavedAt.value = new Date();
    }
    invalidateAfterSave(note.noteId);
  },
  onError: (error) => {
    if (isUnauthorized(error)) {
      router.push({ name: 'login' });
    }
    saveError.value = getApiErrorMessage(error, 'Could not save changes.');
    console.error(getApiErrorMessage(error, 'Error updating diary.'));
  },
  staleTime: 500,
});


const onUpdate = (noteId, output, editor) => {
  noteJsonRef.value = normalizeTiptapDoc(editor?.getJSON ? editor.getJSON() : output);
  const note = notePayloadFromDoc(noteJsonRef.value);

  if (note === lastSubmittedNote.value) {
    return;
  }

  lastSubmittedNote.value = note;
  updateNote(
    {
      noteId,
      note,
    });
};

const debouncedSave = debounce(function (noteId, output, options) {
  onUpdate(noteId, output, options);
}, 500);

const debouncedOnUpdate = (output, options) => {
  debouncedSave(props.date, output, options);
};

debouncedOnUpdate.cancel = () => debouncedSave.cancel();

watch(() => props.date, (newDate, oldDate) => {
  if (oldDate && newDate !== oldDate) {
    debouncedSave.flush();
  }
}, { flush: 'sync' });



</script>

<style scoped>
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
