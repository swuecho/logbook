<template>
  <div class="content">
    <div class="editor">
      <el-tiptap :key="'editor-' + date" :content="content" :extensions="extensions" @onUpdate="debouncedOnUpdate"
        @onInit="onInit" :readonly="!isPrimaryTab"></el-tiptap>
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
import { Icon } from '@iconify/vue2';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
import { createExtensions } from '@/editorExt.js';
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

const content = ref({});
const noteJsonRef = ref(null);
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
        content.value = noteObj || {};
      } catch (parseError) {
        console.error('Failed to parse diary note:', parseError);
        content.value = {};
      }
    } else {
      content.value = {};
    }

    // Update the editor content when new data is loaded
    if (editorRef.value) {
      editorRef.value.setContent(content.value);
    }
  }
});


const editorRef = ref(null);

const onInit = async ({ editor }) => {
  editorRef.value = editor;
  editor.setContent(content.value);
};


const { mutate: updateNote } = useMutation({
  mutationFn: saveNote,
  networkMode: 'always',
  onSuccess: () => {
    // queryClient.invalidateQueries({ queryKey: ['diaryContent', props.date, ] });
    // if invalid the diaryContent, it will cause the editor to refresh content. 
    // will overwrite the current content delta.  (typed in after last put request)
    queryClient.invalidateQueries({ queryKey: ['todoContent'] });
    queryClient.invalidateQueries({ queryKey: ['MdContent', props.date] });
  },
  onError: (error) => {
    if (isUnauthorized(error)) {
      router.push({ name: 'login' });
    }
    console.error(getApiErrorMessage(error, 'Error updating diary.'));
  },
  staleTime: 500,
});


const onUpdate = (output, options) => {
  const { getJSON } = options;
  noteJsonRef.value = getJSON();
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
  font-family: "Fira Code", Courier, Monaco, monospace;
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
