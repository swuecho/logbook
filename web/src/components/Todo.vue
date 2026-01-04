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
import { fetchTodoContent } from '@/services/todo';

const loading = ref(true);
const content = ref("");
const extensions = createExtensions();
const editorRef = ref(null);

const { isLoading, isError, data, error, refetch } = useQuery({
  queryKey: ['todoContent'],
  queryFn: fetchTodoContent,
  enabled: false,
});

const onInit = ({ editor }) => {
  editorRef.value = editor;
  refetch();
};

watch(isLoading, (isLoading) => {
  loading.value = isLoading;
});

watch(data, (todoContent) => {
  if (todoContent && editorRef.value) {
    editorRef.value.setContent(todoContent);
  }
});

watch(isError, (hasError) => {
  if (hasError) {
    console.error('Error fetching todo content:', error.value);
  }
});
</script>

<style>
.content {
  max-width: 65rem;
  margin: auto;
}

/* Additional styles... */
</style>
