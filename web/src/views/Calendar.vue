<template>
  <el-container class="calendar-page app-page app-page--shell">
    <div class="app-shell">
      <el-header class="app-header-bar calendar-page__header">
        <button type="button" class="linkish" aria-label="Home" @click="goHome">
          <Icon :icon="homeIcon" height="24" />
        </button>

        <div class="year-row">
          <button type="button" class="linkish year-arrow" aria-label="Previous year" @click="prevYear">
            ‹
          </button>
          <h1 class="year-heading">{{ yearTitle }}</h1>
          <button type="button" class="linkish year-arrow" aria-label="Next year" @click="nextYear">
            ›
          </button>
        </div>

        <button type="button" class="linkish" @click="goThisYear">This year</button>
      </el-header>

      <el-main v-loading="loading" class="app-main-padded calendar-page__main">
        <p v-if="loadError" class="load-error" role="alert">{{ loadError }}</p>
        <div class="year-months">
          <section
            v-for="(block, mi) in yearMonths"
            :key="mi"
            class="month-block"
          >
            <h2 class="month-name">{{ block.title }}</h2>
            <div class="weekdays">
              <span
                v-for="(label, wi) in weekdayLabels"
                :key="'w' + wi"
                class="weekday"
              >{{ label }}</span>
            </div>
            <div class="day-grid">
              <template v-for="(cell, idx) in block.cells">
                <button
                  v-if="cell.day"
                  :key="'d' + idx"
                  type="button"
                  :class="cellClass(cell)"
                  :aria-label="ariaDayLabel(cell)"
                  @click="openDay(cell.day)"
                >
                  <span class="day-num">{{ cell.day.date() }}</span>
                </button>
                <span
                  v-else
                  :key="'e' + idx"
                  class="day-placeholder"
                  aria-hidden="true"
                />
              </template>
            </div>
          </section>
        </div>
      </el-main>
    </div>
  </el-container>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue';
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

function ariaDayLabel(cell) {
  const d = cell.day.format('MMMM D, YYYY');
  if (cell.hasNote && cell.isToday) {
    return `Open ${d}, has entry, today`;
  }
  if (cell.hasNote) {
    return `Open ${d}, has entry`;
  }
  if (cell.isToday) {
    return `Open ${d}, today`;
  }
  return `Open ${d}`;
}

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
    day: true,
    'day--today': cell.isToday,
    'day--note': cell.hasNote,
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
  max-width: 100%;
}

.calendar-page :deep(.el-header) {
  padding: 0;
  background: transparent;
}

.year-row {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.year-heading {
  margin: 0;
  min-width: 4.5ch;
  text-align: center;
  font-size: 1.25rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  color: var(--lb-text);
}

.year-arrow {
  font-size: 1.5rem;
  line-height: 1;
  padding: 0.15rem 0.35rem;
}

.load-error {
  margin: 0 0 1rem;
  color: var(--lb-error);
  font-size: 0.9rem;
}

.year-months {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr));
  gap: 2rem 1.5rem;
}

.month-block {
  min-width: 0;
}

.month-name {
  margin: 0 0 0.5rem;
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--lb-text);
}

.weekdays {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  margin-bottom: 0.25rem;
}

.weekday {
  text-align: center;
  font-size: 0.65rem;
  color: var(--lb-text-subtle);
}

.day-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 2px;
}

.day {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1px;
  aspect-ratio: 1;
  min-height: 1.75rem;
  max-height: 2.25rem;
  margin: 0;
  padding: 0;
  border: none;
  border-radius: 4px;
  background: transparent;
  font: inherit;
  cursor: pointer;
  color: var(--lb-text);
}

.day-num {
  font-size: 0.8rem;
  font-variant-numeric: tabular-nums;
  line-height: 1.1;
}

.day:hover {
  background: var(--lb-hover);
}

.day:focus {
  outline: none;
}

.day:focus-visible {
  background: var(--lb-hover);
  box-shadow: inset 0 0 0 1px var(--lb-accent);
}

.day--today .day-num {
  font-weight: 600;
  color: var(--lb-accent);
}

.day--note::after {
  content: '';
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: var(--lb-accent);
  opacity: 0.75;
}

.day-placeholder {
  display: block;
  aspect-ratio: 1;
  min-height: 1.75rem;
}

@media (max-width: 480px) {
  .year-months {
    grid-template-columns: 1fr;
  }

  .day {
    max-height: none;
  }
}
</style>
