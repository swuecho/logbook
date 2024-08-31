<template>
  <div class="content">
    <div class="nav">
      {{ time }}
      <a href="#">{{ date }}</a>
      <a v-if="date != today" :href="'/view?date=' + today">Today</a>
      <a v-if="date == today" href="/todo">Todo</a>
      <a href="content">
        <Icon :icon="icons.tableOfContents" />
        <Icon v-if="loading" icon="line-md:loading-alt-loop" />
      </a>
    </div>
    <div class="editor">
      <el-tiptap :content="content" :extensions="extensions" @onUpdate="debouncedOnUpdate" @onInit="onInit"></el-tiptap>
    </div>
  </div>
</template>


<script setup>
import { ref, computed, onMounted } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue2';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
import { createExtensions } from '@/editorExt.js';
import codemirror from 'codemirror';
import 'codemirror/lib/codemirror.css'; // import base style
import 'codemirror/mode/xml/xml.js'; // language
import 'codemirror/addon/selection/active-line.js'; // require active-line.js
import 'codemirror/addon/edit/closetag.js'; // autoCloseTags
import { useMutation } from '@tanstack/vue-query';
import router from '@/router';
import { debounce } from 'lodash';

import axios from '@/axiosConfig.js';

const props = defineProps({
  date: String
});


const now = ref(moment());
const loading = ref(true);
const timeFormat = 'h:mm:ss a';
const last_note_json = ref(null);
const content = ref(null);
const icons = {
  tableOfContents,
};
const extensions = createExtensions();
const json = ref(null);

onMounted(() => {
  // eslint-disable-next-line no-unused-vars
  const interval = setInterval(() => now.value = moment(), 1000);
});

const today = computed(() => {
  return now.value.format('YYYYMMDD');
});

const time = computed(() => {
  return now.value.format(timeFormat);
});

const mutation = useMutation({
  mutationFn: async () => {
    const response = await axios.put(`/api/diary/${props.date}`, {
      noteId: props.date,
      note: JSON.stringify(json.value)
    });
    return response.data;
  },
  onSuccess: (data) => {
    console.log(data);
    loading.value = false;
    // Invalidate the todoContent query
    queryClient.invalidateQueries('todoContent');
  },
  onError: (error) => {
    loading.value = false;
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
  json.value = getJSON();
  console.log(json.value);

  mutation.mutate();

  loading.value = mutation.isLoading.value;
  if (mutation.isError.value) {
    console.error('Error updating diary:', mutation.error.value);
  }
};

const debouncedOnUpdate = debounce(function (output, options) {
  onUpdate(output, options);
}, 500)

const onInit = async ({ editor }) => {
  loading.value = true;
  try {
    const response = await axios.get(`/api/diary/${props.date}`);
    const lastNote = response.data;
    if (lastNote) {
      const lastNoteJson = JSON.parse(lastNote.note);
      last_note_json.value = lastNoteJson;
      editor.setContent(lastNoteJson);
    }
  } catch (error) {
    console.error('Error fetching diary content:', error);
  } finally {
    loading.value = false;
  }
};
</script>

<style>
.content {
  max-width: 65rem;
  margin: auto;
}

.nav {
  margin: 1em 1em 1rem 1em;
  display: flex;
  justify-content: space-between;
}

pre code {
  font-family: "Fira Code", Courier, Monaco, monospace;
}

.nav a {
  display: inline-block;
  text-decoration: none;
  border-radius: 5%;
}

/* Change the link color on hover */
.nav a:hover {
  background-color: rgb(223, 214, 214);
  color: white;
}
</style>
