<template>
  <el-container class="app-page app-page--shell content-view">
    <div class="app-shell">
      <el-header class="app-header-bar app-header-bar--start">
        <button type="button" class="linkish" aria-label="Home" @click="backHome">
          <Icon :icon="icons.homeIcon" height="24" />
        </button>
        <el-input
          v-model="searchQuery"
          class="content-view__search"
          placeholder="Search entries"
          clearable
          size="small"
          @input="scheduleSearch"
          @clear="scheduleSearch"
        />
      </el-header>
      <el-main
        v-loading="loading"
        class="app-main-padded content-view__main"
        element-loading-text="Loading entries…"
      >
        <p v-if="searchError" class="search-error" role="alert">{{ searchError }}</p>
        <p v-if="isSearchActive && searching" class="search-empty">Searching…</p>
        <div v-if="isSearchActive" class="grid-container">
          <div v-for="item in searchResults" :key="item.noteId" class="grid-item">
            <el-card class="lb-card-flat content-view__card" shadow="never">
              <template #header>
                <a class="entry-date-link" :href="'/view?date=' + item.noteId">{{ item.noteId }}</a>
              </template>
              <p class="search-snippet">
                <template v-for="(part, idx) in snippetParts(item.snippet)" :key="idx">
                  <mark v-if="part.match">{{ part.text }}</mark>
                  <span v-else>{{ part.text }}</span>
                </template>
              </p>
            </el-card>
          </div>
        </div>
        <p v-if="isSearchActive && !searching && searchResults.length === 0 && !searchError" class="search-empty">
          No entries found.
        </p>
        <div v-if="!isSearchActive" class="grid-container">
          <div v-for="(item, row_idx) in summaries" :key="row_idx" class="grid-item">
            <el-card class="lb-card-flat content-view__card" shadow="never">
              <template #header>
                <a class="entry-date-link" :href="'/view?date=' + item.id">{{ item.id }}</a>
              </template>
              <vue-word-cloud
                class="content-view__cloud"
                :words="item.note"
                :color="([, weight]) => weight > 10 ? '#0f766e' : weight > 5 ? '#2563eb' : '#475569'"
                font-family="Fira Code"
              />
            </el-card>
          </div>
        </div>
      </el-main>
    </div>
  </el-container>
</template>

<script>
import VueWordCloud from 'vuewordcloud';
import { Icon } from '@iconify/vue';
import homeIcon from '@iconify/icons-material-symbols/home';
import { getDiarySummaries, searchDiary } from '@/services/diary';
import { getApiErrorMessage } from '@/services/apiError';
export default {
  components: {
    VueWordCloud,
    Icon,
  },
  data() {
    return {
      loading: true,
      searching: false,
      searchQuery: '',
      searchResults: [],
      searchError: '',
      searchTimer: null,
      searchSeq: 0,
      summaries: [],
      icons: {
        homeIcon,
      },
    };
  },
  async created() {
    await this.fetchDiaryNotes();
  },
  computed: {
    isSearchActive() {
      return this.searchQuery.trim().length > 0;
    }
  },
  beforeUnmount() {
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
  },
  methods: {
    dict_to_lol(dict) {
      var lol = [];
      for (const [key, value] of Object.entries(JSON.parse(dict))) {
        lol.push([key, value])
      }
      return lol;
    },
    backHome() {
      this.$router.push('/')
    },
    async fetchDiaryNotes() {
      this.loading = true;
      try {
        const notes = await getDiarySummaries();
        this.processNotes(notes);
      } catch (error) {
        console.error(getApiErrorMessage(error, 'Error fetching diary notes.'));
      } finally {
        this.loading = false;
      }
    },
    scheduleSearch() {
      if (this.searchTimer) {
        clearTimeout(this.searchTimer);
      }

      this.searchTimer = setTimeout(() => {
        this.fetchSearchResults();
      }, 280);
    },
    async fetchSearchResults() {
      const query = this.searchQuery.trim();
      const seq = ++this.searchSeq;
      this.searchError = '';

      if (!query) {
        this.searchResults = [];
        this.searching = false;
        return;
      }

      this.searching = true;
      try {
        const results = await searchDiary(query);
        if (seq === this.searchSeq) {
          this.searchResults = results;
        }
      } catch (error) {
        if (seq === this.searchSeq) {
          this.searchError = getApiErrorMessage(error, 'Error searching diary notes.');
          this.searchResults = [];
        }
      } finally {
        if (seq === this.searchSeq) {
          this.searching = false;
        }
      }
    },
    snippetParts(snippet) {
      const terms = this.searchQuery
        .trim()
        .toLowerCase()
        .split(/\s+/)
        .filter(Boolean);

      if (!snippet || terms.length === 0) {
        return [{ text: snippet || '', match: false }];
      }

      const lowerSnippet = snippet.toLowerCase();
      const parts = [];
      let index = 0;

      while (index < snippet.length) {
        let match = null;
        for (const term of terms) {
          if (term && lowerSnippet.startsWith(term, index)) {
            match = term;
            break;
          }
        }

        if (match) {
          parts.push({ text: snippet.slice(index, index + match.length), match: true });
          index += match.length;
        } else {
          const start = index;
          index += 1;
          while (
            index < snippet.length &&
            !terms.some(term => term && lowerSnippet.startsWith(term, index))
          ) {
            index += 1;
          }
          parts.push({ text: snippet.slice(start, index), match: false });
        }
      }

      return parts;
    },
    processNotes(notes) {
      const processedNotes = notes
        .map(note =>
        ({
          id: note.noteId,
          note: this.dict_to_lol(note.note)
        })
        );
      this.summaries = processedNotes;
    }
  },

};
</script>

<style scoped>
.content-view :deep(.el-header) {
  padding: 0;
  height: auto !important;
  background: transparent;
}

.content-view__main {
  min-height: 14rem;
}

.content-view__search {
  width: min(22rem, 100%);
}

.grid-container {
  display: grid;
  gap: 1.25rem;
  padding: 0;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.grid-item {
  min-width: 0;
}

.content-view__card {
  height: 100%;
}

.content-view__cloud {
  height: 240px;
  width: 100%;
}

.search-snippet {
  margin: 0;
  color: var(--lb-text);
  font-size: 0.85rem;
  line-height: 1.7;
}

.search-snippet mark {
  border-radius: 3px;
  background: #dff3ea;
  color: var(--lb-text);
  padding: 0 0.1rem;
}

.search-empty,
.search-error {
  margin: 0 0 1rem;
  font-size: 0.9rem;
  color: var(--lb-text-muted);
}

.search-error {
  color: var(--lb-error);
}

.entry-date-link {
  color: var(--lb-accent);
  text-decoration: none;
  font-weight: 600;
}

.entry-date-link:hover {
  text-decoration: underline;
}

code {
  line-height: 1;
}

@media (max-width: 480px) {
  .content-view__search {
    width: 100%;
  }

  .grid-container {
    gap: 0.75rem;
  }

  .content-view__card :deep(.el-card__header) {
    padding: 0.5rem;
  }

  .content-view__card :deep(.el-card__body) {
    padding: 0.5rem;
  }

  .content-view__cloud {
    height: 150px;
  }
}
</style>
