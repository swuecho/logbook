<template>
  <div class="content">
    <pre>  {{ content }} </pre>
    <button v-if="content" @click="copyToClipboard" class="copy-button">
      <div v-if="copySuccess">
        <Icon icon="akar-icons:check" />
      </div>
      <div v-else>
        <Icon icon="iconamoon:copy"></Icon>
      </div>
    </button>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue';
import { useQuery } from '@tanstack/vue-query';
import { Icon } from '@iconify/vue2';
import { exportMarkdown } from '@/services/markdown';
import { getApiErrorMessage } from '@/services/apiError';

const props = defineProps({
  noteId: String,
});
const copySuccess = ref(false);

const content = ref("");
const loading = ref(true);

const copyToClipboard = async () => {
  if (content.value) {
    try {
      await navigator.clipboard.writeText(content.value);
      copySuccess.value = true;
      setTimeout(() => {
        copySuccess.value = false;
      }, 1500); // Reset the message after 1.5 seconds
    } catch (err) {
      console.error('Failed to copy text: ', err);
      // Optionally provide user feedback about the failure
    }
  }
};

const { isLoading, isError, data, error, refetch } = useQuery(
  {
    queryKey: ['MdContent', props.noteId],
    queryFn: () => exportMarkdown(props.noteId),
    enabled: false
  });

watch(isLoading, (isLoading) => {
  loading.value = isLoading;
});
watch(data, (data) => {
  if (content) {
    content.value = data;
  }
});

watch(isError, (hasError) => {
  if (hasError) {
    console.error(getApiErrorMessage(error.value, 'Error fetching markdown content.'));
  }
});

watch(() => props.noteId, (noteId) => {
  if (noteId) {
    refetch();
  }
}, { immediate: true });
</script>

<style>
.content {
  max-width: 65rem;
  margin: auto;
}

.copy-button {
  background-color: #f1f1f1;
  border: none;
  color: black;
  text-align: center;
  text-decoration: none;
  display: inline-block;
  cursor: pointer;
  border-radius: 4px;
  padding: 10px;
  position: absolute;
  bottom: 10px;
  right: 10px;
}

/* Additional styles... */
</style>
