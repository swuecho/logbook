<template>
  <el-container class="app-page app-page--shell content-view">
    <div class="app-shell">
      <el-header class="app-header-bar app-header-bar--start">
        <button type="button" class="linkish" aria-label="Home" @click="backHome">
          <Icon :icon="icons.homeIcon" height="24" />
        </button>
      </el-header>
      <el-main
        v-loading="loading"
        class="app-main-padded content-view__main"
        element-loading-text="Loading entries…"
      >
        <div class="grid-container">
          <div v-for="(item, row_idx) in summaries" :key="row_idx" class="grid-item">
            <el-card class="lb-card-flat content-view__card" shadow="never">
              <template #header>
                <a class="entry-date-link" :href="'/view?date=' + item.id">{{ item.id }}</a>
              </template>
              <vue-word-cloud
                style="height: 240px; width: 100%;"
                :words="item.note"
                :color="([, weight]) => weight > 10 ? '#0f766e' : weight > 5 ? '#2563eb' : '#475569'"
                font-family="IBM Plex Sans"
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
import { getDiarySummaries } from '@/services/diary';
import { getApiErrorMessage } from '@/services/apiError';
export default {
  components: {
    VueWordCloud,
    Icon,
  },
  data() {
    return {
      loading: true,
      summaries: [],
      icons: {
        homeIcon,
      },
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

.grid-container {
  display: grid;
  gap: 1.25rem;
  padding: 0;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
}

.grid-item {
  min-width: 0;
}

.content-view__card {
  height: 100%;
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
</style>
