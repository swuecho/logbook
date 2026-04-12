<template>
  <div class="app-page home-page">
    <div class="app-shell app-shell--narrow home-page__inner">
      <div class="nav">
        <div class="app-header-bar app-header-bar--dense home-page__top">
          <div class="time-display">{{ time }}</div>
          <div class="right-corner">
            <OnlineStatusIndicator />
            <button type="button" class="linkish" aria-label="Markdown preview" @click="openModalMd">
              <Icon icon="material-symbols:markdown-copy-outline" />
            </button>
            <a href="/calendar" class="linkish" title="Calendar">
              <Icon :icon="calendarMonth" />
            </a>
            <a href="/content" class="linkish" title="Browse entries">
              <Icon :icon="tableOfContents" />
            </a>
          </div>
        </div>
        <DateNavigation v-if="date && diaryIds.size > 0" v-model="date" :diary-ids="diaryIds" />
        <TodoStrip />
        <el-dialog :visible="dialogVisibleMd" @close="closeModalMd">
          <MDView :noteId="date"></MDView>
        </el-dialog>
      </div>
      <DiaryEditor :date="date"></DiaryEditor>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue2';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
import calendarMonth from '@iconify/icons-mdi/calendar-month';
import DiaryEditor from "@/components/DiaryEditor";
import TodoStrip from '@/components/TodoStrip.vue';
import MDView from '@/components/MDView.vue';
import OnlineStatusIndicator from '@/components/OnlineStatusIndicator.vue';
import DateNavigation from '@/components/DateNavigation.vue';
import { getDiaryIds } from '@/services/diary';

const now = ref(moment());
const date = ref(moment().format('YYYYMMDD'))
const dialogVisibleMd = ref(false)
const diaryIds = ref(new Set());

let intervalId = null;

onMounted(async () => {
  date.value = now.value.format("YYYYMMDD")
  const ids = await getDiaryIds();
  diaryIds.value = new Set(ids);
  intervalId = setInterval(() => now.value = moment(), 1000);
});

onUnmounted(() => {
  if (intervalId) {
    clearInterval(intervalId);
    intervalId = null;
  }
});
const time = computed(() => {
  const timeFormat = 'h:mm:ss a';
  return now.value.format(timeFormat);
});

function openModalMd() {
  dialogVisibleMd.value = true
}
function closeModalMd() {
  dialogVisibleMd.value = false
}

</script>

<style scoped>
.home-page__inner {
  padding-top: 0;
  padding-bottom: 2rem;
}

.nav {
  margin: 0 0 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.home-page__top {
  margin-bottom: 0.25rem;
}

.time-display {
  font-size: 1.05em;
  font-weight: 500;
  color: var(--lb-text);
}

.right-corner {
  display: flex;
  flex-direction: row;
  gap: 0.65rem;
  align-items: center;
}

/* Mobile optimizations */
@media (max-width: 768px) {
  .nav {
    margin: 0.5em 0 0.75rem 0;
    gap: 0.75rem;
  }

  .home-page__top {
    flex-direction: column;
    align-items: stretch;
    gap: 0.75rem;
  }

  .time-display {
    text-align: center;
    font-size: 1.2em;
  }

  .right-corner {
    justify-content: center;
    gap: 1.5rem;
  }
}

@media (max-width: 480px) {
  .nav {
    margin: 0.25em 0;
  }

  .right-corner {
    gap: 1rem;
  }
}
</style>
