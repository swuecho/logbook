<template>
  <el-container class="app-page app-page--shell search-view">
    <div class="app-shell">
      <el-header class="app-header-bar app-header-bar--start">
        <button type="button" class="linkish" aria-label="Home" @click="backHome">
          <Icon :icon="homeIcon" height="24" />
        </button>
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
      </el-header>

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
              <a class="search-view__date" :href="'/view?date=' + item.noteId">{{ item.noteId }}</a>
            </template>
            <p class="search-view__snippet">{{ item.snippet }}</p>
          </el-card>
        </div>
      </el-main>
    </div>
  </el-container>
</template>

<script setup>
import { computed, onBeforeUnmount, ref } from 'vue';
import { Icon } from '@iconify/vue';
import homeIcon from '@iconify/icons-material-symbols/home';
import router from '@/router';
import { searchDiary } from '@/services/diary';
import { getApiErrorMessage } from '@/services/apiError';

const searchQuery = ref('');
const searchResults = ref([]);
const searching = ref(false);
const searchError = ref('');
const searchSeq = ref(0);
let searchTimer = null;

const isSearchActive = computed(() => searchQuery.value.trim().length > 0);

function backHome() {
  router.push('/');
}

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

onBeforeUnmount(() => {
  if (searchTimer) {
    clearTimeout(searchTimer);
  }
});
</script>

<style scoped>
.search-view :deep(.el-header) {
  padding: 0;
  height: auto !important;
  background: transparent;
}

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
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 1rem;
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
  line-height: 1.7;
}

@media (max-width: 480px) {
  .search-view__input {
    width: 100%;
  }

  .search-view__results {
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 0.75rem;
  }

  .search-view__result :deep(.el-card__header),
  .search-view__result :deep(.el-card__body) {
    padding: 0.5rem;
  }
}
</style>
