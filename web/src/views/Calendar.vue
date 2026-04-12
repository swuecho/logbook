<template>
  <el-container class="calendar-page">
    <el-header class="calendar-header">
      <div class="header-inner">
        <button type="button" class="home-btn" aria-label="Home" @click="goHome">
          <Icon :icon="homeIcon" height="22" />
        </button>

        <div class="year-control" role="group" aria-label="Year navigation">
          <button type="button" class="year-step" aria-label="Previous year" @click="prevYear">
            <span class="year-step-chevron" aria-hidden="true">‹</span>
          </button>
          <h1 class="year-title">
            <span class="year-title-num">{{ yearTitle }}</span>
          </h1>
          <button type="button" class="year-step" aria-label="Next year" @click="nextYear">
            <span class="year-step-chevron" aria-hidden="true">›</span>
          </button>
        </div>

        <button type="button" class="today-pill" @click="goThisYear">
          This year
        </button>
      </div>
    </el-header>

    <el-main v-loading="loading" class="calendar-main">
      <div v-if="loadError" class="load-error" role="alert">{{ loadError }}</div>
      <div class="year-months">
        <section
          v-for="(block, mi) in yearMonths"
          :key="mi"
          class="month-card"
        >
          <h2 class="month-card-title">{{ block.title }}</h2>
          <div class="weekdays">
            <div
              v-for="(label, wi) in weekdayLabels"
              :key="'w' + wi"
              class="weekday-cell"
            >
              {{ label }}
            </div>
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
              <div
                v-else
                :key="'e' + idx"
                class="day-cell day-cell--empty"
                aria-hidden="true"
              />
            </template>
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

function ariaDayLabel(cell) {
  const d = cell.day.format('MMMM D, YYYY');
  const parts = [];
  if (cell.hasNote) {
    parts.push('has entry');
  }
  if (cell.isToday) {
    parts.push('today');
  }
  const suffix = parts.length ? `, ${parts.join(', ')}` : '';
  return `Open diary for ${d}${suffix}`;
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
    'day-cell': true,
    'day-cell--empty': !cell.day,
    'day-cell--today': cell.isToday,
    'day-cell--has-note': cell.hasNote,
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
  --cal-bg: #f0f1f3;
  --cal-surface: #ffffff;
  --cal-ink: #2c3e50;
  --cal-ink-soft: #5c6b7a;
  --cal-muted: #8b99a8;
  --cal-line: rgba(44, 62, 80, 0.08);
  --cal-accent: #2d8659;
  --cal-accent-ring: rgba(45, 134, 89, 0.35);
  --cal-accent-fill: rgba(45, 134, 89, 0.09);
  --cal-shadow: 0 1px 1px rgba(44, 62, 80, 0.04), 0 6px 24px rgba(44, 62, 80, 0.07);
  --cal-radius: 14px;
  --cal-font: 'Helvetica Neue', Helvetica, 'PingFang SC', 'Hiragino Sans GB',
    'Microsoft YaHei', Arial, sans-serif;

  min-height: 100vh;
  max-width: 76rem;
  margin: 0 auto;
  background: var(--cal-bg);
  font-family: var(--cal-font);
}

.calendar-page >>> .el-header {
  padding: 0;
  background: transparent;
}

.calendar-header {
  height: auto !important;
  padding: 1.25rem 1rem 1rem;
  border-bottom: 1px solid var(--cal-line);
  background: linear-gradient(180deg, #fafbfc 0%, var(--cal-bg) 100%);
}

.header-inner {
  max-width: 72rem;
  margin: 0 auto;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.home-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.5rem;
  height: 2.5rem;
  padding: 0;
  border: none;
  border-radius: 10px;
  background: var(--cal-surface);
  color: var(--cal-ink-soft);
  box-shadow: 0 1px 2px rgba(44, 62, 80, 0.06);
  cursor: pointer;
  transition: color 0.15s ease, box-shadow 0.15s ease, transform 0.15s ease;
}

.home-btn:hover {
  color: var(--cal-ink);
  box-shadow: 0 2px 8px rgba(44, 62, 80, 0.08);
}

.home-btn:focus {
  outline: none;
  box-shadow: 0 0 0 3px var(--cal-accent-ring);
}

.year-control {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.2rem;
  background: var(--cal-surface);
  border-radius: 12px;
  box-shadow: var(--cal-shadow);
  border: 1px solid var(--cal-line);
}

.year-step {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.25rem;
  height: 2.25rem;
  padding: 0;
  border: none;
  border-radius: 8px;
  background: transparent;
  color: var(--cal-ink-soft);
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease;
}

.year-step:hover {
  background: var(--cal-bg);
  color: var(--cal-ink);
}

.year-step:focus {
  outline: none;
  box-shadow: inset 0 0 0 2px var(--cal-accent-ring);
}

.year-step-chevron {
  font-size: 1.35rem;
  line-height: 1;
  font-weight: 300;
  position: relative;
  top: -1px;
}

.year-title {
  margin: 0;
  padding: 0 0.5rem;
  min-width: 5rem;
  text-align: center;
}

.year-title-num {
  font-size: 1.5rem;
  font-weight: 600;
  letter-spacing: -0.03em;
  color: var(--cal-ink);
  font-variant-numeric: tabular-nums;
}

.today-pill {
  border: 1px solid var(--cal-line);
  background: var(--cal-surface);
  color: var(--cal-ink-soft);
  border-radius: 999px;
  padding: 0.45rem 1rem;
  font-size: 0.8125rem;
  font-weight: 500;
  letter-spacing: 0.01em;
  cursor: pointer;
  box-shadow: 0 1px 2px rgba(44, 62, 80, 0.04);
  transition: border-color 0.15s ease, color 0.15s ease, box-shadow 0.15s ease,
    background 0.15s ease;
}

.today-pill:hover {
  border-color: var(--cal-accent);
  color: var(--cal-accent);
  background: var(--cal-accent-fill);
}

.today-pill:focus {
  outline: none;
  box-shadow: 0 0 0 3px var(--cal-accent-ring);
}

.calendar-main {
  padding: 1.5rem 1.25rem 3rem !important;
  background: transparent;
}

.calendar-page >>> .el-loading-mask {
  background-color: rgba(240, 241, 243, 0.75);
}

.load-error {
  max-width: 40rem;
  margin: 0 auto 1rem;
  padding: 0.75rem 1rem;
  border-radius: 10px;
  background: #fff5f5;
  border: 1px solid rgba(192, 57, 43, 0.2);
  color: #a33025;
  font-size: 0.875rem;
}

.year-months {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
  gap: 1.35rem;
}

.month-card {
  min-width: 0;
  padding: 1rem 1rem 1.1rem;
  background: var(--cal-surface);
  border-radius: var(--cal-radius);
  border: 1px solid var(--cal-line);
  box-shadow: var(--cal-shadow);
  transition: box-shadow 0.2s ease, transform 0.2s ease, border-color 0.2s ease;
}

.month-card:hover {
  box-shadow: 0 2px 4px rgba(44, 62, 80, 0.05), 0 12px 32px rgba(44, 62, 80, 0.09);
  border-color: rgba(44, 62, 80, 0.1);
}

.month-card-title {
  margin: 0 0 0.65rem;
  font-size: 0.8125rem;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cal-muted);
}

.weekdays {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 2px;
  margin-bottom: 0.35rem;
}

.weekday-cell {
  text-align: center;
  font-size: 0.65rem;
  font-weight: 600;
  color: var(--cal-muted);
  padding: 0.15rem 0;
  letter-spacing: 0.02em;
}

.day-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 3px;
}

.day-cell {
  position: relative;
  aspect-ratio: 1;
  min-height: 0;
  max-height: 2.35rem;
  margin: 0;
  padding: 0;
  border: none;
  border-radius: 8px;
  background: rgba(44, 62, 80, 0.03);
  cursor: pointer;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: var(--cal-ink);
  transition: background 0.15s ease, color 0.15s ease, box-shadow 0.15s ease;
  font-family: inherit;
}

.day-cell:disabled {
  cursor: default;
  background: transparent;
  pointer-events: none;
}

.day-cell:not(:disabled):hover {
  background: rgba(44, 62, 80, 0.07);
}

.day-cell:not(:disabled):focus {
  outline: none;
  box-shadow: 0 0 0 2px var(--cal-accent-ring);
  z-index: 1;
}

.day-cell--empty {
  background: transparent;
}

.day-cell--today .day-num {
  font-weight: 700;
  color: var(--cal-accent);
}

.day-cell--today:not(:disabled) {
  background: var(--cal-accent-fill);
  box-shadow: inset 0 0 0 1.5px var(--cal-accent);
}

.day-cell--has-note:not(:disabled)::after {
  content: '';
  position: absolute;
  bottom: 5px;
  left: 50%;
  transform: translateX(-50%);
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: var(--cal-accent);
  opacity: 0.85;
}

.day-cell--today.day-cell--has-note:not(:disabled)::after {
  bottom: 4px;
}

.day-num {
  font-size: 0.78rem;
  font-weight: 500;
  line-height: 1;
  font-variant-numeric: tabular-nums;
}

@media (max-width: 640px) {
  .header-inner {
    justify-content: center;
  }

  .home-btn {
    order: 0;
  }

  .year-control {
    order: 1;
    flex: 1 1 100%;
    justify-content: center;
  }

  .today-pill {
    order: 2;
  }

  .year-title-num {
    font-size: 1.35rem;
  }
}

@media (max-width: 480px) {
  .year-months {
    grid-template-columns: 1fr;
  }

  .day-cell {
    max-height: none;
  }
}
</style>
