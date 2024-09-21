<template>
  <div class="content">
    <todo-editor :content="content" :extensions="extensions" @init="onInit" />
  </div>
</template>

<script setup>
import { ref, watch } from 'vue';
import TodoEditor from '@/components/TodoEditor';
import { createExtensions } from '@/editorExt.js';
import { useQuery } from '@tanstack/vue-query';
import axios from '@/axiosConfig.js';


const loading = ref(true);
const content = ref("");
const extensions = createExtensions();

const onInit = ({ editor }) => {
  fetchTodoContent(editor);
};

const fetchTodoContent = async (editor) => {
  const { isLoading, isError, data, error } = useQuery(
    {
      queryKey: ['todoContent'],
      queryFn: async () => {
        const response = await axios.get('/api/todo');
        return response.data;
      }
    });

  watch(isLoading, (isLoading) => {
    loading.value = isLoading;
  });

  watch(data, (content) => {
    if (content) {
      editor.setContent(content);
    }
  });

  watch(isError, (hasError) => {
    if (hasError) {
      console.error('Error fetching todo content:', error.value);
    }
  });
};
</script>

<style>
.content {
  max-width: 65rem;
  margin: auto;
}

/* Additional styles... */
</style>
