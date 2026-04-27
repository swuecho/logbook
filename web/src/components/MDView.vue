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
import { computed, ref, watch } from 'vue';
import { useQuery } from '@tanstack/vue-query';
import { Icon } from '@iconify/vue';
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

const markdownQueryKey = computed(() => ['MdContent', props.noteId]);
const isQueryEnabled = computed(() => Boolean(props.noteId));

const { isLoading, isError, data, error } = useQuery(
  {
    queryKey: markdownQueryKey,
    queryFn: () => exportMarkdown(props.noteId),
    enabled: isQueryEnabled
  });

watch(isLoading, (isLoading) => {
  loading.value = isLoading;
});
watch(data, (data) => {
  if (data !== undefined && data !== null) {
    content.value = data;
  }
});

watch(isError, (hasError) => {
  if (hasError) {
    console.error(getApiErrorMessage(error.value, 'Error fetching markdown content.'));
  }
});

watch(() => props.noteId, () => {
  content.value = '';
});
</script>

<style scoped>
.content {
  position: relative;
  max-width: 65rem;
  margin: auto;
}

.content pre {
  white-space: pre-wrap;
}

.copy-button {
  background-color: var(--lb-bg, #fff);
  border: 1px solid var(--lb-border, #e8eaed);
  color: var(--lb-text-muted, #5a6d7e);
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

.copy-button:hover {
  background-color: var(--lb-hover, #f4f5f7);
  color: var(--lb-text, #2c3e50);
}

/* Additional styles... */
</style>
