<template>
  <div class="content">
    <nav-bar :date="date" :today="today" :loading="loading" />
    <todo-editor :content="content" :extensions="extensions" @init="onInit" />
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import moment from 'moment';
import NavBar from '@/components/NavBar';
import TodoEditor from '@/components/TodoEditor';
import { createExtensions } from '@/editorExt.js';
import { useQuery } from '@tanstack/vue-query';
import axios from '@/axiosConfig.js';

const props = defineProps({
  date: {
    type: String,
    required: true,
  },
});

const now = ref(moment());
const loading = ref(true);
const content = ref(null);
const extensions = createExtensions();

const today = computed(() => {
  return now.value.format('YYYYMMDD');
});

const onInit = ({ editor }) => {
  fetchTodoContent(editor);
};

const fetchTodoContent = async (editor) => {
  const { isLoading, isError, data, error } = useQuery(
    { queryKey: ['todoContent'],
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
