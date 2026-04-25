<template>
  <div class="content">
    <div class="editor-status" :class="editorStatusClass" aria-live="polite">
      {{ editorStatusText }}
    </div>
    <div class="editor">
      <el-tiptap
        :key="'editor-' + date"
        output="json"
        :content="content"
        :extensions="extensions"
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
    <div v-if="isLoading" class="loading">
      <Icon icon="eos-icons:bubble-loading" />
    </div>
  </div>
</template>


<script setup>
import { ref, computed, onMounted, watch } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
import { createExtensions, emptyDoc, normalizeTiptapDoc } from '@/editorExt.js';
import codemirror from 'codemirror';
import 'codemirror/lib/codemirror.css'; // import base style
import 'codemirror/mode/xml/xml.js'; // language
import 'codemirror/addon/selection/active-line.js'; // require active-line.js
import 'codemirror/addon/edit/closetag.js'; // autoCloseTags
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

const extensions = createExtensions();

const content = ref(emptyDoc());
const noteJsonRef = ref(null);
const lastSavedAt = ref(null);
const saveError = ref('');
const queryKey = computed(() => ['diaryContent', props.date]);
const { data: noteData, isLoading, error: getNoteError } = useQuery({
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
        const noteObj = typeof newData.note === 'string' ? JSON.parse(newData.note) : newData.note;
        content.value = normalizeTiptapDoc(noteObj);
      } catch (parseError) {
        console.error('Failed to parse diary note:', parseError);
        content.value = emptyDoc();
      }
    } else {
      content.value = emptyDoc();
    }

    // Update the editor content when new data is loaded
    if (editorRef.value) {
      editorRef.value.commands.setContent(content.value);
    }
  }
});


const editorRef = ref(null);

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
  onMutate: () => {
    saveError.value = '';
  },
  onSuccess: () => {
    // queryClient.invalidateQueries({ queryKey: ['diaryContent', props.date, ] });
    // if invalid the diaryContent, it will cause the editor to refresh content. 
    // will overwrite the current content delta.  (typed in after last put request)
    lastSavedAt.value = new Date();
    queryClient.invalidateQueries({ queryKey: ['todoContent'] });
    queryClient.invalidateQueries({ queryKey: ['MdContent', props.date] });
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


const onUpdate = (output, editor) => {
  noteJsonRef.value = normalizeTiptapDoc(editor?.getJSON ? editor.getJSON() : output);
  updateNote(
    {
      noteId: props.date,
      note: JSON.stringify(noteJsonRef.value),
    });
};

const debouncedOnUpdate = debounce(function (output, options) {
  onUpdate(output, options);
}, 500)



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
</style>
