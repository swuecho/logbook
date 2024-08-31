<template>
  <div class="content">
    <nav-bar :date="date" :today="today" :loading="loading" />
    <todo-editor :content="content" :extensions="extensions" @init="onInit" />
  </div>
</template>

<script>
import moment from 'moment';
import NavBar from '@/components/NavBar';
import TodoEditor from '@/components/TodoEditor';
import { createExtensions } from '@/editorExt.js';

export default {
  components: {
    NavBar,
    TodoEditor,
  },
  props: {
    date: {
      type: String,
      required: true,
    },
  },
  data() {
    return {
      now: moment(),
      loading: true,
      content: null,
      extensions: createExtensions(),
    };
  },
  computed: {
    today() {
      return this.now.format('YYYYMMDD');
    },
  },
  methods: {
    onInit({ editor }) {
      this.fetchTodoContent(editor);
    },
    async fetchTodoContent(editor) {
      const { isLoading, isError, data, error } = useQuery('todoContent', async () => {
        const response = await this.axios.get('/api/todo');
        return response.data;
      });

      watch(isLoading, (loading) => {
        this.loading = loading;
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
    },
  },
};
</script>

<style>
.content {
  max-width: 65rem;
  margin: auto;
}

/* Additional styles... */
</style>
