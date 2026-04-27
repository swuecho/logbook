<template>
  <div class="app-page app-page--shell home-page">
    <div class="app-shell app-shell--narrow home-page__inner">
      <div class="nav">
        <AppTopBar
          :show-home="false"
          show-status
          show-markdown
          @open-markdown="openModalMd"
        >
          <template #start>
            <div class="time-display">{{ time }}</div>
          </template>
        </AppTopBar>
        <DateNavigation v-if="date && diaryIds.size > 0" v-model="date" :diary-ids="diaryIds" />
        <TodoStrip />
        <el-dialog v-model="dialogVisibleMd" title="Markdown export" class="home-page__dialog" @close="closeModalMd">
          <MDView :note-id="date" />
        </el-dialog>
      </div>
      <DiaryEditor :date="date"></DiaryEditor>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useQuery } from '@tanstack/vue-query';
import moment from 'moment';
import DiaryEditor from "@/components/DiaryEditor";
import TodoStrip from '@/components/TodoStrip.vue';
import MDView from '@/components/MDView.vue';
import DateNavigation from '@/components/DateNavigation.vue';
import AppTopBar from '@/components/AppTopBar.vue';
import { getDiaryIds } from '@/services/diary';

const now = ref(moment());
const date = ref(moment().format('YYYYMMDD'))
const dialogVisibleMd = ref(false)
const diaryIds = ref(new Set());

let intervalId = null;

const { data: diaryIdsData } = useQuery({
  queryKey: ['diaryIds'],
  queryFn: getDiaryIds,
});

watch(diaryIdsData, (ids) => {
  diaryIds.value = new Set(ids || []);
}, { immediate: true });

onMounted(async () => {
  date.value = now.value.format("YYYYMMDD")
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
.nav {
  margin: 0 0 1.1rem;
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.time-display {
  font-size: 1rem;
  font-weight: 600;
  letter-spacing: 0.01em;
  color: var(--lb-text);
  font-variant-numeric: tabular-nums;
}

.home-page__dialog :deep(.el-dialog) {
  width: min(56rem, calc(100vw - 2rem));
}

/* Mobile optimizations */
@media (max-width: 768px) {
  .nav {
    margin: 0 0 0.75rem;
    gap: 0.75rem;
  }

  .time-display {
    text-align: center;
    font-size: 1.2em;
  }
}

@media (max-width: 480px) {
  .nav {
    margin: 0 0 0.75rem;
  }
}
</style>
