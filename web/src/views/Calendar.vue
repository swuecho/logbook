<template>
  <el-container class="calendar-page">
    <el-header class="calendar-header">
      <div class="calendar-header-left" @click="goHome">
        <Icon :icon="homeIcon" height="28" />
      </div>
      <div class="calendar-header-center">
        <button type="button" class="nav-year" aria-label="Previous year" @click="prevYear">
          ‹
        </button>
        <h1 class="year-title">{{ yearTitle }}</h1>
        <button type="button" class="nav-year" aria-label="Next year" @click="nextYear">
          ›
        </button>
      </div>
      <div class="calendar-header-right">
        <button type="button" class="today-btn" @click="goThisYear">This year</button>
      </div>
    </el-header>

    <el-main v-loading="loading">
      <div v-if="loadError" class="load-error">{{ loadError }}</div>
      <div class="year-months">
        <section
          v-for="(block, mi) in yearMonths"
          :key="mi"
          class="month-block"
        >
          <h2 class="month-block-title">{{ block.title }}</h2>
          <div class="weekdays weekdays--compact">
            <div
              v-for="label in weekdayLabels"
              :key="label"
              class="weekday-cell"
            >
              {{ label }}
            </div>
          </div>
          <div class="grid grid--compact">
            <div
              v-for="(cell, idx) in block.cells"
              :key="idx"
              :class="cellClass(cell)"
              @click="cell.day && openDay(cell.day)"
            >
              <template v-if="cell.day">
                <span class="day-num">{{ cell.day.date() }}</span>
                <span v-if="cell.hasNote" class="note-dot" aria-hidden="true" />
              </template>
            </div>
          </div>
        </section>
      </div>
    </el-main>
  </el-container>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue2';
import homeIcon from '@iconify/icons-material-symbols/home';
import router from '@/router';
import { getDiaryIds } from '@/services/diary';
import { getApiErrorMessage } from '@/services/apiError';

const visibleYear = ref(moment().year());
const diaryIds = ref(new Set());
const loading = ref(true);
const loadError = ref('');

const weekdayLabels = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];

const yearTitle = computed(() => String(visibleYear.value));

function buildCellsForMonth(year, month0Based, todayId) {
  const m = moment({ year, month: month0Based, day: 1 }).startOf('month');
  const startWeekday = m.day();
  const daysInMonth = m.clone().endOf('month').date();
  const cells = [];

  for (let i = 0; i < startWeekday; i++) {
    cells.push({ day: null });
  }
  for (let d = 1; d <= daysInMonth; d++) {
    const day = m.clone().date(d);
    const id = day.format('YYYYMMDD');
    cells.push({
      day,
      hasNote: diaryIds.value.has(id),
      isToday: id === todayId,
    });
  }
  const trailing = (7 - (cells.length % 7)) % 7;
  for (let i = 0; i < trailing; i++) {
    cells.push({ day: null });
  }
  return cells;
}

const yearMonths = computed(() => {
  const y = visibleYear.value;
  const todayId = moment().format('YYYYMMDD');
  const months = [];
  for (let month = 0; month < 12; month++) {
    const start = moment({ year: y, month, day: 1 });
    months.push({
      title: start.format('MMMM'),
      cells: buildCellsForMonth(y, month, todayId),
    });
  }
  return months;
});

function cellClass(cell) {
  return {
    'grid-cell': true,
    'grid-cell--empty': !cell.day,
    'grid-cell--today': cell.isToday,
    'grid-cell--has-note': cell.hasNote,
  };
}

function prevYear() {
  visibleYear.value -= 1;
}

function nextYear() {
  visibleYear.value += 1;
}

function goThisYear() {
  visibleYear.value = moment().year();
}

function openDay(day) {
  router.push({ path: '/view', query: { date: day.format('YYYYMMDD') } });
}

function goHome() {
  router.push('/');
}

onMounted(async () => {
  loading.value = true;
  loadError.value = '';
  try {
    const ids = await getDiaryIds();
    diaryIds.value = new Set(ids);
  } catch (err) {
    loadError.value = getApiErrorMessage(err, 'Could not load diary dates.');
    console.error(loadError.value);
  } finally {
    loading.value = false;
  }
});
</script>

<style scoped>
.calendar-page {
  min-height: 100vh;
  max-width: 72rem;
  margin: 0 auto;
}

.calendar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.75rem;
  height: auto !important;
  padding: 1rem 1rem 0.5rem;
}

.calendar-header-left,
.calendar-header-right {
  flex: 0 0 auto;
  cursor: pointer;
  display: flex;
  align-items: center;
}

.calendar-header-center {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1 1 auto;
  justify-content: center;
  min-width: 8rem;
}

.year-title {
  margin: 0;
  font-size: 1.35rem;
  font-weight: 600;
  color: #333;
  min-width: 4.5rem;
  text-align: center;
}

.nav-year {
  border: 1px solid #dee2e6;
  background: #fff;
  border-radius: 6px;
  width: 2rem;
  height: 2rem;
  font-size: 1.25rem;
  line-height: 1;
  cursor: pointer;
  color: #495057;
}

.nav-year:hover {
  background: #f8f9fa;
}

.today-btn {
  border: 1px solid #28a745;
  background: #fff;
  color: #28a745;
  border-radius: 6px;
  padding: 0.35rem 0.75rem;
  font-size: 0.875rem;
  cursor: pointer;
  font-weight: 500;
}

.today-btn:hover {
  background: #f8fff9;
}

.load-error {
  color: #c0392b;
  font-size: 0.9rem;
  margin-bottom: 0.75rem;
}

.year-months {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(15.5rem, 1fr));
  gap: 1.25rem 1rem;
  padding-bottom: 1.5rem;
}

.month-block {
  min-width: 0;
}

.month-block-title {
  margin: 0 0 0.35rem;
  font-size: 0.95rem;
  font-weight: 600;
  color: #495057;
}

.weekdays {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 1px;
  margin-bottom: 0.2rem;
}

.weekdays--compact .weekday-cell {
  font-size: 0.62rem;
  padding: 0.1rem 0;
}

.weekday-cell {
  text-align: center;
  font-size: 0.75rem;
  font-weight: 600;
  color: #6c757d;
  padding: 0.25rem 0;
}

.grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 1px;
}

.grid--compact .grid-cell {
  min-height: 0;
  padding: 0.12rem 0.05rem 0.1rem;
  border-radius: 4px;
}

.grid--compact .day-num {
  font-size: 0.7rem;
}

.grid--compact .grid-cell--has-note .note-dot {
  width: 4px;
  height: 4px;
}

.grid-cell {
  aspect-ratio: 1;
  min-height: 1.65rem;
  border: 1px solid #e9ecef;
  border-radius: 6px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-start;
  padding: 0.2rem 0.05rem 0.15rem;
  cursor: pointer;
  background: #fff;
  position: relative;
  transition: background-color 0.15s ease;
}

.grid-cell:hover:not(.grid-cell--empty) {
  background: #f8f9fa;
}

.grid-cell--empty {
  cursor: default;
  background: transparent;
  border-color: transparent;
}

.grid-cell--empty:hover {
  background: transparent;
}

.grid-cell--today .day-num {
  font-weight: 700;
  color: #28a745;
}

.grid-cell--has-note .note-dot {
  display: block;
  width: 5px;
  height: 5px;
  background-color: #28a745;
  border-radius: 50%;
  margin-top: auto;
}

.day-num {
  font-size: 0.8rem;
  font-weight: 500;
  color: #333;
  line-height: 1.1;
}

@media (max-width: 480px) {
  .year-title {
    font-size: 1.15rem;
  }

  .year-months {
    grid-template-columns: 1fr;
  }
}
</style>
