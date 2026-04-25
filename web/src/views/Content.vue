<template>
  <el-container class="app-page app-page--shell content-view">
    <div class="app-shell">
      <AppTopBar title="Browse" :show-content="false" />
      <el-main
        v-loading="loading"
        class="app-main-padded content-view__main"
        element-loading-text="Loading entries…"
      >
        <div class="grid-container">
          <div v-for="(item, row_idx) in summaries" :key="row_idx" class="grid-item">
            <el-card class="lb-card-flat content-view__card" shadow="never">
              <template #header>
                <a class="entry-date-link" :href="'/view?date=' + item.id">{{ formatEntryDate(item.id) }}</a>
              </template>
              <vue-word-cloud
                style="height: 240px; width: 100%;"
                :words="item.note"
                :color="wordColor"
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
import moment from 'moment';
import AppTopBar from '@/components/AppTopBar.vue';
import { getDiarySummaries } from '@/services/diary';
import { getApiErrorMessage } from '@/services/apiError';
export default {
  components: {
    VueWordCloud,
    AppTopBar,
  },
  data() {
    return {
      loading: true,
      summaries: [],
    };
  },
  async created() {
    await this.fetchDiaryNotes();
  },
  methods: {
    dict_to_lol(dict) {
      var lol = [];
      for (const [key, value] of Object.entries(JSON.parse(dict))) {
        lol.push([key, value])
      }
      return lol;
    },
    wordColor([, weight]) {
      if (weight > 14) return '#236747';
      if (weight > 8) return '#2d8659';
      if (weight > 4) return '#5a6d7e';
      return '#8a9aa8';
    },
    formatEntryDate(noteId) {
      const parsed = moment(String(noteId), 'YYYYMMDD', true);
      return parsed.isValid() ? parsed.format('MMM D, YYYY') : noteId;
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
.content-view .app-shell {
  max-width: 88rem;
}

.content-view__main {
  min-height: 14rem;
}

.grid-container {
  display: grid;
  gap: 1.25rem;
  padding: 0;
  grid-template-columns: repeat(3, minmax(18rem, 1fr));
}

.grid-item {
  min-width: 0;
}

.content-view__card {
  height: 100%;
}

.content-view__card :deep(.el-card__body) {
  padding: 0.75rem 1rem 1rem;
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

@media (max-width: 900px) {
  .grid-container {
    grid-template-columns: repeat(2, minmax(16rem, 1fr));
  }
}

@media (max-width: 680px) {
  .grid-container {
    gap: 0.75rem;
    grid-template-columns: 1fr;
  }

  .content-view__card :deep(.el-card__header) {
    padding: 0.5rem;
  }

  .content-view__card :deep(.el-card__body) {
    padding: 0.5rem;
  }

}
</style>
