<template>
  <el-container class="app-page app-page--shell search-view">
    <div class="app-shell">
      <AppTopBar title="Search" :show-search="false">
        <template #center>
          <el-input
            v-model="searchQuery"
            class="search-view__input"
            placeholder="Search entries"
            clearable
            size="small"
            autofocus
            @input="scheduleSearch"
            @clear="scheduleSearch"
          />
        </template>
      </AppTopBar>

      <el-main class="app-main-padded search-view__main">
        <p v-if="searchError" class="search-view__error" role="alert">{{ searchError }}</p>
        <p v-if="searching" class="search-view__status">Searching...</p>
        <p
          v-else-if="isSearchActive && searchResults.length === 0 && !searchError"
          class="search-view__status"
        >
          No entries found.
        </p>

        <div v-if="searchResults.length > 0" class="search-view__results">
          <el-card
            v-for="item in searchResults"
            :key="item.noteId"
            class="lb-card-flat search-view__result"
            shadow="never"
          >
            <template #header>
              <a class="search-view__date" :href="'/view?date=' + item.noteId">
                {{ formatEntryDate(item.noteId) }}
              </a>
            </template>
            <p class="search-view__snippet" v-html="highlightSnippet(item.snippet)"></p>
          </el-card>
        </div>
      </el-main>
    </div>
  </el-container>
</template>

<script setup>
import { computed, onBeforeUnmount, ref } from 'vue';
import moment from 'moment';
import AppTopBar from '@/components/AppTopBar.vue';
import { searchDiary } from '@/services/diary';
import { getApiErrorMessage } from '@/services/apiError';

const searchQuery = ref('');
const searchResults = ref([]);
const searching = ref(false);
const searchError = ref('');
const searchSeq = ref(0);
let searchTimer = null;

const isSearchActive = computed(() => searchQuery.value.trim().length > 0);

function scheduleSearch() {
  if (searchTimer) {
    clearTimeout(searchTimer);
  }

  searchTimer = setTimeout(() => {
    fetchSearchResults();
  }, 280);
}

async function fetchSearchResults() {
  const query = searchQuery.value.trim();
  const seq = searchSeq.value + 1;
  searchSeq.value = seq;
  searchError.value = '';

  if (!query) {
    searchResults.value = [];
    searching.value = false;
    return;
  }

  searching.value = true;
  try {
    const results = await searchDiary(query);
    if (seq === searchSeq.value) {
      searchResults.value = results;
    }
  } catch (error) {
    if (seq === searchSeq.value) {
      searchError.value = getApiErrorMessage(error, 'Error searching diary notes.');
      searchResults.value = [];
    }
  } finally {
    if (seq === searchSeq.value) {
      searching.value = false;
    }
  }
}

function formatEntryDate(noteId) {
  const parsed = moment(String(noteId), 'YYYYMMDD', true);
  return parsed.isValid() ? parsed.format('MMM D, YYYY') : noteId;
}

function highlightSnippet(snippet) {
  const escapedSnippet = escapeHtml(snippet || '');
  const query = searchQuery.value.trim();
  if (!query) return escapedSnippet;

  const escapedQuery = escapeRegExp(escapeHtml(query));
  return escapedSnippet.replace(new RegExp(`(${escapedQuery})`, 'ig'), '<mark>$1</mark>');
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

onBeforeUnmount(() => {
  if (searchTimer) {
    clearTimeout(searchTimer);
  }
});
</script>

<style scoped>
.search-view__input {
  width: min(30rem, 100%);
}

.search-view__main {
  min-height: 14rem;
}

.search-view__status,
.search-view__error {
  margin: 0 0 1rem;
  font-size: 0.9rem;
  color: var(--lb-text-muted);
}

.search-view__error {
  color: var(--lb-error);
}

.search-view__results {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  max-width: 52rem;
}

.search-view__result {
  min-width: 0;
}

.search-view__date {
  color: var(--lb-accent);
  text-decoration: none;
  font-weight: 600;
}

.search-view__date:hover {
  text-decoration: underline;
}

.search-view__snippet {
  margin: 0;
  color: var(--lb-text);
  font-size: 0.85rem;
  line-height: 1.75;
}

.search-view__snippet :deep(mark) {
  padding: 0 0.12rem;
  border-radius: 3px;
  background: #eef8f2;
  color: var(--lb-accent-strong);
}

@media (max-width: 480px) {
  .search-view__input {
    width: 100%;
  }

  .search-view__result :deep(.el-card__header),
  .search-view__result :deep(.el-card__body) {
    padding: 0.5rem;
  }
}
</style>
