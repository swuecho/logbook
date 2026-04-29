<template>
  <div class="app-page app-page--shell diary-view">
    <div class="app-shell app-shell--narrow">
      <AppTopBar :title="title" />
      <DiaryEditor :date="date" />
    </div>
  </div>
</template>

<script setup>
import { computed, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import moment from 'moment';
import AppTopBar from '@/components/AppTopBar.vue';
import DiaryEditor from "@/components/DiaryEditor";

const route = useRoute();
const todayId = () => moment().format('YYYYMMDD');
const normalizedDate = (value) => {
  const parsed = moment(String(value || ''), 'YYYYMMDD', true);
  return parsed.isValid() ? parsed.format('YYYYMMDD') : todayId();
};
const date = ref(normalizedDate(route.query.date));
const title = computed(() => {
  const parsed = moment(String(date.value), 'YYYYMMDD', true);
  return parsed.isValid() ? parsed.format('MMM D, YYYY') : 'Diary';
});

watch(
  () => route.query.date,
  (newDate) => {
    date.value = normalizedDate(newDate);
  },
  { immediate: true }
);
</script>
