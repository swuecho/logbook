<template>
  <div class="content">
    <div class="editor">
      <el-tiptap :key="'editor-' + date" :content="content" :extensions="extensions" @onUpdate="debouncedOnUpdate"
        @onInit="onInit"></el-tiptap>
    </div>
    <Icon v-if="loadingRef" icon="line-md:loading-alt-loop" />
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
import { useIsFetching, useIsMutating, useMutation, useQuery } from '@tanstack/vue-query';
import router from '@/router';
import { debounce } from 'lodash';

import axios from '@/axiosConfig.js';

import { useQueryClient } from '@tanstack/vue-query';
const queryClient = useQueryClient();

const props = defineProps({
  date: String
});


const extensions = createExtensions();

const content = ref(null);
const noteJsonRef = ref(null);
//const loadingRef = computed(() => useIsFetching() + useIsMutating());
const loadingRef = ref(false);

const mutation = useMutation({
  mutationFn: async () => {
    const response = await axios.put(`/api/diary/${props.date}`, {
      noteId: props.date,
      note: JSON.stringify(noteJsonRef.value)
    });
    return response.data;
  },
  onSuccess: (data) => {
    console.log(data);
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
  mutation.mutate();
};

const debouncedOnUpdate = debounce(function (output, options) {
  onUpdate(output, options);
}, 500)


const { data: noteData, error, isLoading } = useQuery({
  queryKey: ['diaryContent', props.date],
  queryFn: async () => {
    const response = await axios.get(`/api/diary/${props.date}`);
    return response.data;
  },
  onError: (error) => {
    console.log(error);
    if (error.response && error.response.status === 401) {
      router.push({ name: 'login' });
    }
    console.error('Error fetching diary content:', error);
  }
});

watch(noteData, (newData) => {
  if (newData) {
    const noteObj = JSON.parse(newData.note);
    content.value =  noteObj;
  }
});

const onInit = async ({ editor }) => {
  editor.setContent(content.value);
};
</script>

<style scoped>
pre code {
  font-family: "Fira Code", Courier, Monaco, monospace;
}
</style>
