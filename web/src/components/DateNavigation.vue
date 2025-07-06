<template>
  <div class="date-nav">
    <div class="date-slider" ref="dateSliderRef">
      <div v-for="d in datesToShow" :key="d.format('YYYYMMDD')" @click="setDate(d.format('YYYYMMDD'))" :class="{
        'date-item': true,
        'active': d.format('YYYYMMDD') === value,
        'today': d.format('YYYYMMDD') === today,
        'has-diary': hasDiary(d)
      }">
        <div class="month-label">
          {{ d.format('MMM') }}
        </div>
        <div class="day-number">{{ d.format('DD') }}</div>
      </div>
    </div>
    <div v-if="value != today" @click="navigateDateToToday" class="icon-container">
      <Icon icon="fluent:arrow-next-16-regular" width="1.2rem" />
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch, nextTick } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue2';

const props = defineProps({
  value: {
    type: String,
    required: true
  },
  diaryIds: {
    type: Set,
    default: () => new Set()
  }
});

const emit = defineEmits(['input']);

const now = ref(moment());
const dateSliderRef = ref(null);

onMounted(() => {
  const interval = setInterval(() => now.value = moment(), 1000);


  // Ensure scrolling happens after DOM is fully rendered
  nextTick(() => {
    setTimeout(() => {
      scrollToActiveDate(false);
    }, 200); // Increased delay to ensure DOM is ready
  });


  // Cleanup interval on component unmount
  return () => clearInterval(interval);
});

watch(() => props.value, () => {
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
  emit('input', newDate);
}

function navigateDateToToday() {
  emit('input', today.value);
}
</script>

<style scoped>
.date-nav {
  display: flex;
  align-items: center;
  white-space: nowrap;
  flex-grow: 1;
  overflow: hidden;
  min-height: 60px;
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
  min-width: 50px;
  flex-shrink: 0;
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
  padding: 0 0.5rem;
  flex-shrink: 0;
}

/* Mobile optimizations */
@media (max-width: 768px) {
  .date-nav {
    min-height: 70px;
    margin: 0;
  }

  .date-item {
    padding: 10px 14px;
    min-width: 60px;
  }

  .day-number {
    font-size: 1.2em;
  }

  .month-label {
    font-size: 0.8em;
  }
}

@media (max-width: 480px) {
  .date-item {
    padding: 8px 10px;
    min-width: 50px;
  }

  .day-number {
    font-size: 1.1em;
  }

  .month-label {
    font-size: 0.7em;
  }
}
</style>