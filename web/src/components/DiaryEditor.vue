<template>
  <div class="content">
    <div class="editor">
      <el-tiptap :key="'editor-' + date" :content="content" :extensions="extensions" @onUpdate="debouncedOnUpdate"
        @onInit="onInit"></el-tiptap>
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
  if (error.response && error.response.status === 401) {
    // Use the correct router method in the Vue 3 setup
    router.push({ name: 'login' });
  }
  console.error('Error fetching diary:', error);
})

watch(noteData, (newData) => {
  if (newData) {
    if (newData.note) {
      const noteObj = JSON.parse(newData.note);
      content.value = noteObj;
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
    // Invalidate the todoContent query
    // Invalidate the todoContent query
    // Note: queryClient is not defined in this scope. 
    // You might need to import and configure it, or use a different method to invalidate queries.
    // For example, if using Vue Query, you could use the useQueryClient composable:
    // 
    // import { useQueryClient } from '@tanstack/vue-query';
    // const queryClient = useQueryClient();
    // 
    // Then use it here:
    queryClient.invalidateQueries({ queryKey: ['todoContent'] });
    queryClient.invalidateQueries({ queryKey: ['MdContent', props.date] });
  },
  onError: (error) => {
    if (error.response && error.response.status === 401) {
      // Use the correct router method in the Vue 3 setup
      router.push({ name: 'login' });
    }
    console.error('Error updating diary:', error);
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
</style>
