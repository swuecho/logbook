<template>
  <el-container class="calendar-page app-page app-page--shell">
    <div class="app-shell">
      <AppTopBar title="Calendar" :show-calendar="false">
        <template #center>
          <div class="year-row">
            <button type="button" class="linkish year-arrow" aria-label="Previous year" @click="prevYear">
              ‹
            </button>
            <h2 class="year-heading">{{ yearTitle }}</h2>
            <button type="button" class="linkish year-arrow" aria-label="Next year" @click="nextYear">
              ›
            </button>
          </div>

          <el-input
            v-model="searchQuery"
            class="calendar-page__search"
            placeholder="Search entries"
            clearable
            size="small"
          />
        </template>
      </AppTopBar>

      <el-main v-loading="loading" class="app-main-padded calendar-page__main">
        <p v-if="loadError" class="load-error" role="alert">{{ loadError }}</p>
        <p v-if="searchError" class="load-error" role="alert">{{ searchError }}</p>
        <p v-if="isSearchActive && searching" class="search-status">Searching…</p>
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
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue';
import moment from 'moment';
import AppTopBar from '@/components/AppTopBar.vue';
import router from '@/router';
import { getDiaryIds, searchDiary } from '@/services/diary';
import { getApiErrorMessage } from '@/services/apiError';

const visibleYear = ref(moment().year());
const diaryIds = ref(new Set());
const searchQuery = ref('');
const searchResults = ref([]);
const searching = ref(false);
const searchError = ref('');
const searchSeq = ref(0);
let searchTimer = null;
const loading = ref(true);
const loadError = ref('');

const weekdayLabels = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];

const yearTitle = computed(() => String(visibleYear.value));
const isSearchActive = computed(() => searchQuery.value.trim().length > 0);
const matchingDiaryIds = computed(() => new Set(searchResults.value.map(item => item.noteId)));

function ariaDayLabel(cell) {
  const d = cell.day.format('MMMM D, YYYY');
  if (cell.isSearchMatch) {
    return `Open ${d}, matches search`;
  }
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
      isSearchMatch: matchingDiaryIds.value.has(id),
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
    'day--search-match': isSearchActive.value && cell.isSearchMatch,
    'day--search-dim': isSearchActive.value && cell.hasNote && !cell.isSearchMatch,
  };
}

function prevYear() {
  visibleYear.value -= 1;
}

function nextYear() {
  visibleYear.value += 1;
}

function openDay(day) {
  router.push({ path: '/view', query: { date: day.format('YYYYMMDD') } });
}

async function fetchSearchResults() {
  const query = searchQuery.value.trim();
  const seq = searchSeq.value + 1;
  searchSeq.value = seq;
  searchError.value = '';

  if (!query) {
    searchResults.value = [];
    searching.value = false;
    return;
  }

  searching.value = true;
  try {
    const results = await searchDiary(query);
    if (seq === searchSeq.value) {
      searchResults.value = results;
    }
  } catch (err) {
    if (seq === searchSeq.value) {
      searchError.value = getApiErrorMessage(err, 'Could not search diary entries.');
      searchResults.value = [];
    }
  } finally {
    if (seq === searchSeq.value) {
      searching.value = false;
    }
  }
}

watch(searchQuery, () => {
  if (searchTimer) {
    clearTimeout(searchTimer);
  }

  searchTimer = setTimeout(() => {
    fetchSearchResults();
  }, 280);
});

onBeforeUnmount(() => {
  if (searchTimer) {
    clearTimeout(searchTimer);
  }
});

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

.year-row {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.calendar-page__search {
  width: min(18rem, 100%);
  min-width: 0;
}

.year-heading {
  margin: 0;
  min-width: 4.5ch;
  text-align: center;
  font-size: 1.2rem;
  font-weight: 650;
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

.search-status {
  margin: 0 0 1rem;
  color: var(--lb-text-muted);
  font-size: 0.9rem;
}

.year-months {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1.25rem;
}

.month-block {
  min-width: 0;
  padding: 0.85rem;
  border: 1px solid var(--lb-border);
  border-radius: var(--lb-radius-lg);
  background: #fff;
}

.month-name {
  margin: 0 0 0.65rem;
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
  font-weight: 600;
}

.day-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 3px;
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
  border-radius: 5px;
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

.day--today {
  box-shadow: inset 0 0 0 1px rgba(45, 134, 89, 0.22);
}

.day--note::after {
  content: '';
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: var(--lb-accent);
  opacity: 0.75;
}

.day--search-match {
  background: #eef8f2;
  box-shadow: inset 0 0 0 1px rgba(45, 134, 89, 0.28);
}

.day--search-match::after {
  width: 5px;
  height: 5px;
  opacity: 1;
}

.day--search-dim {
  color: var(--lb-text-subtle);
  opacity: 0.38;
}

.day-placeholder {
  display: block;
  aspect-ratio: 1;
  min-height: 1.75rem;
}

@media (max-width: 900px) {
  .year-months {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 480px) {
  .calendar-page :deep(.app-top-bar__center) {
    flex-direction: column;
    align-items: stretch;
  }

  .year-row {
    width: 100%;
    justify-content: center;
  }

  .calendar-page__search {
    width: 100%;
  }

  .year-months {
    grid-template-columns: 1fr;
    gap: 0.75rem;
  }

  .month-block {
    padding: 0.65rem;
  }

  .month-name {
    font-size: 0.82rem;
  }

  .weekday {
    font-size: 0.58rem;
  }

  .day-num {
    font-size: 0.78rem;
  }

  .day {
    min-height: 1.75rem;
    max-height: none;
  }

  .day-placeholder {
    min-height: 1.75rem;
  }
}
</style>
