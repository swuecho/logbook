<template>
  <div class="date-nav">
    <div class="date-slider" ref="dateSliderRef">
      <div v-for="d in datesToShow" :key="d.format('YYYYMMDD')" @click="setDate(d.format('YYYYMMDD'))" :class="{
        'date-item': true,
        'active': d.format('YYYYMMDD') === modelValue,
        'today': d.format('YYYYMMDD') === today,
        'has-diary': hasDiary(d)
      }">
        <div class="month-label">
          {{ d.format('MMM') }}
        </div>
        <div class="day-number">{{ d.format('DD') }}</div>
      </div>
    </div>
    <div v-if="modelValue != today" @click="navigateDateToToday" class="icon-container">
      <Icon icon="fluent:arrow-next-16-regular" width="1.2rem" height="1.2rem" />
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch, nextTick, onUnmounted } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue';

const props = defineProps({
  modelValue: {
    type: String,
    required: true
  },
  diaryIds: {
    type: Set,
    default: () => new Set()
  }
});

const emit = defineEmits(['update:modelValue']);

const now = ref(moment());
const dateSliderRef = ref(null);
let intervalId = null;

onMounted(() => {
  intervalId = setInterval(() => now.value = moment(), 1000);


  // Ensure scrolling happens after DOM is fully rendered
  nextTick(() => {
    setTimeout(() => {
      scrollToActiveDate(false);
    }, 200); // Increased delay to ensure DOM is ready
  });


});

onUnmounted(() => {
  if (intervalId) {
    clearInterval(intervalId);
    intervalId = null;
  }
});

watch(() => props.modelValue, () => {
  nextTick(() => {
    scrollToActiveDate();
  });
});

function scrollToActiveDate(smooth = true) {
  if (!dateSliderRef.value) return;

  // First try to find the active element
  const activeElement = dateSliderRef.value.querySelector('.date-item.active');

  if (activeElement) {
    activeElement.scrollIntoView({
      behavior: smooth ? 'smooth' : 'auto',
      inline: 'center',
      block: 'nearest'
    });
  } else {
    // If no active element, scroll to the end (most recent dates)
    const slider = dateSliderRef.value;
    slider.scrollLeft = slider.scrollWidth - slider.clientWidth;
  }
}

function hasDiary(d) {
  return props.diaryIds.has(d.format('YYYYMMDD'));
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
  return dates; // chronological order: oldest to newest (today at the end)
});

const today = computed(() => {
  return now.value.format('YYYYMMDD');
});

function setDate(newDate) {
  emit('update:modelValue', newDate);
}

function navigateDateToToday() {
  emit('update:modelValue', today.value);
}
</script>

<style scoped>
.date-nav {
  display: flex;
  align-items: center;
  white-space: nowrap;
  flex-grow: 1;
  overflow: hidden;
  min-height: 58px;
  padding: 0.25rem 0;
}

.date-slider {
  display: flex;
  overflow-x: scroll;
  scroll-behavior: smooth;
  -ms-overflow-style: none;
  /* IE and Edge */
  scrollbar-width: none;
  /* Firefox */
  flex: 1;
  gap: 0.25rem;
  padding: 0.1rem 0;
}

.date-slider::-webkit-scrollbar {
  display: none;
  /* Chrome, Safari, and Opera */
}

.date-item {
  padding: 0.45rem 0.7rem 0.65rem;
  cursor: pointer;
  text-align: center;
  border-radius: var(--lb-radius-md, 8px);
  border: 1px solid transparent;
  position: relative;
  transition:
    background-color 0.18s ease,
    border-color 0.18s ease,
    color 0.18s ease;
  min-width: 3.15rem;
  flex-shrink: 0;
}

.day-number {
  font-size: 1.05em;
  font-weight: 600;
  line-height: 1.25;
  font-variant-numeric: tabular-nums;
}

.month-label {
  font-size: 0.72em;
  font-weight: 500;
  color: var(--lb-text-subtle, #8a9aa8);
  height: 1.25em;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.date-item:hover {
  background-color: var(--lb-hover, #f4f5f7);
}

.date-item.active {
  background-color: #f3faf6;
  border-color: var(--lb-accent, #2d8659);
}

.date-item.active .month-label,
.date-item.active .day-number {
  color: var(--lb-accent, #2d8659);
}

.date-item.has-diary::after {
  content: '';
  position: absolute;
  bottom: 4px;
  left: 50%;
  transform: translateX(-50%);
  width: 4px;
  height: 4px;
  background-color: var(--lb-accent, #2d8659);
  border-radius: 50%;
}

.date-item.today .day-number {
  font-weight: 700;
}

.date-item.today:not(.active) .day-number {
  color: var(--lb-accent, #2d8659);
}

.icon-container {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  margin-left: 0.35rem;
  border-radius: var(--lb-radius-sm, 6px);
  color: var(--lb-text-muted, #5a6d7e);
  cursor: pointer;
  flex-shrink: 0;
}

.icon-container:hover {
  background: var(--lb-hover, #f4f5f7);
  color: var(--lb-text, #2c3e50);
}

/* Mobile optimizations */
@media (max-width: 768px) {
  .date-nav {
    min-height: 64px;
    margin: 0;
  }

  .date-item {
    padding: 0.55rem 0.8rem 0.75rem;
    min-width: 3.6rem;
  }

  .day-number {
    font-size: 1.1em;
  }

  .month-label {
    font-size: 0.8em;
  }
}

@media (max-width: 480px) {
  .date-item {
    padding: 0.5rem 0.65rem 0.7rem;
    min-width: 3.1rem;
  }

  .day-number {
    font-size: 1.1em;
  }

  .month-label {
    font-size: 0.7em;
  }
}
</style>
