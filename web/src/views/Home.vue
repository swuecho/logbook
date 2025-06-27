<template>
  <div class="content">
    <div class="nav">
      {{ time }}
      <div class="date-nav">
        <div class="date-slider" ref="dateSliderRef">
          <div v-for="d in datesToShow" :key="d.format('YYYYMMDD')" @click="setDate(d.format('YYYYMMDD'))"
            :class="{ 'date-item': true, 'active': d.format('YYYYMMDD') === date, 'today': d.format('YYYYMMDD') === today, 'has-diary': hasDiary(d) }">
            <div class="month-label">
              {{ d.format('MMM') }}
            </div>
            <div class="day-number">{{ d.format('DD') }}</div>
          </div>
        </div>
        <div v-if="date != today" @click="navigateDateToToday" class="icon-container">
          <Icon icon="fluent:arrow-next-16-regular" width="1.2rem" />
        </div>
      </div>
      <el-dialog :visible="dialogVisible" @close="closeModal">
        <Todo></Todo>
      </el-dialog>
      <el-dialog :visible="dialogVisibleMd" @close="closeModalMd">
        <MDView :noteId="date"></MDView>
      </el-dialog>
      <div class="right-corner">
        <OnlineStatusIndicator />
        <div @click="openModalMd">
          <Icon icon="material-symbols:markdown-copy-outline" />
        </div>
        <div @click="openModal">
          <Icon icon="ri:todo-line" />
        </div>
        <div>
          <a href="/content">
            <Icon :icon="tableOfContents" />
          </a>
        </div>
      </div>
    </div>
    <DiaryEditor :date="date"></DiaryEditor>


  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch, nextTick } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue2';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
import DiaryEditor from "@/components/DiaryEditor";
import Todo from '@/components/Todo.vue';
import MDView from '@/components/MDView.vue';
import OnlineStatusIndicator from '@/components/OnlineStatusIndicator.vue';
import { getDiaryIds } from '@/services/diary';

const now = ref(moment());
const date = ref(moment().format('YYYYMMDD'))
const dialogVisible = ref(false)
const dialogVisibleMd = ref(false)
const dateSliderRef = ref(null)
const diaryIds = ref(new Set());

onMounted(async () => {
  // eslint-disable-next-line no-unused-vars
  date.value = now.value.format("YYYYMMDD")
  const interval = setInterval(() => now.value = moment(), 1000);
  const ids = await getDiaryIds();
  diaryIds.value = new Set(ids);
  scrollToActiveDate(false);
});

watch(date, () => {
  nextTick(() => {
    scrollToActiveDate();
  });
});

function scrollToActiveDate(smooth = true) {
  if (!dateSliderRef.value) return;
  const activeElement = dateSliderRef.value.querySelector('.date-item.active');
  if (activeElement) {
    activeElement.scrollIntoView({ behavior: smooth ? 'smooth' : 'auto', inline: 'center', block: 'nearest' });
  }
}

function hasDiary(d) {
  return diaryIds.value.has(d.format('YYYYMMDD'));
}

const datesToShow = computed(() => {
  const dates = [];
  const todayMoment = moment();
  // show last 90 days.
  const start = moment().subtract(90, 'days');
  let current = start.clone();
  while (current.isSameOrBefore(todayMoment, 'day')) {
    dates.push(current.clone());
    current.add(1, 'day');
  }
  return dates; // show most recent first
});

const today = computed(() => {
  return now.value.format('YYYYMMDD');
});

const time = computed(() => {
  const timeFormat = 'h:mm:ss a';
  return now.value.format(timeFormat);
});

function setDate(newDate) {
  date.value = newDate
}

function navigateDateToToday() {
  date.value = today.value
}

function openModal() {
  dialogVisible.value = true
}
function closeModal() {
  dialogVisible.value = false
}

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
  justify-content: space-between;
}

.nav .date-nav {
  display: flex;
  align-items: center;
  white-space: nowrap;
  flex-grow: 1;
  margin: 0 1rem;
  overflow: hidden;
}

.date-slider {
  display: flex;
  overflow-x: scroll;
  scroll-behavior: smooth;
  -ms-overflow-style: none;
  /* IE and Edge */
  scrollbar-width: none;
  /* Firefox */
}

.date-slider::-webkit-scrollbar {
  display: none;
  /* Chrome, Safari, and Opera */
}

.date-item {
  padding: 8px 12px;
  cursor: pointer;
  text-align: center;
  border-radius: 8px;
  border: 1px solid transparent;
  position: relative;
  transition: background-color 0.2s ease-in-out;
}

.day-number {
  font-size: 1.1em;
  font-weight: 500;
}

.month-label {
  font-size: 0.75em;
  font-weight: 500;
  color: #6c757d;
  height: 1.2em;
}

.date-item:hover {
  background-color: #f8f9fa;
}

.date-item.active {
  background-color: #f8f9fa;
  border-color: #28a745;
}

.date-item.active .month-label,
.date-item.active .day-number {
  color: #28a745;
}

.date-item.has-diary::after {
  content: '';
  position: absolute;
  bottom: 4px;
  left: 50%;
  transform: translateX(-50%);
  width: 5px;
  height: 5px;
  background-color: #28a745;
  border-radius: 50%;
}

.date-item.today .day-number {
  font-weight: 700;
}

.date-item.today:not(.active) .day-number {
  color: #28a745;
}

.icon-container {
  display: flex;
  align-items: center;
}

.right-corner {
  display: flex;
  flex-direction: row;
  gap: 3rem;
  justify-content: space-around;
  align-items: center;
  gap: 10px;
}
</style>