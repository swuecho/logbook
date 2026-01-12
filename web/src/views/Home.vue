<template>
  <div class="content">
    <div class="nav">
      <div class="nav-header">
        <div class="time-display">{{ time }}</div>
        <div class="right-corner">
          <OnlineStatusIndicator />
          <div @click="openModalMd">
            <Icon icon="material-symbols:markdown-copy-outline" />
          </div>
          <div>
            <a href="/content">
              <Icon :icon="tableOfContents" />
            </a>
          </div>
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
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue2';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
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
.content {
  max-width: 65rem;
  margin: auto;
}

.nav {
  margin: 1em 1em 1rem 1em;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.nav-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.time-display {
  font-size: 1.1em;
  font-weight: 500;
  color: #333;
}

.right-corner {
  display: flex;
  flex-direction: row;
  gap: 10px;
  justify-content: space-around;
  align-items: center;
}

/* Mobile optimizations */
@media (max-width: 768px) {
  .nav {
    margin: 0.5em;
    gap: 0.75rem;
  }

  .nav-header {
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
    margin: 0.25em;
  }

  .right-corner {
    gap: 1rem;
  }
}
</style>
