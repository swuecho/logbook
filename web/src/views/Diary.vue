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
const date = ref(route.query.date);
const title = computed(() => {
  if (!date.value) return 'Diary';
  const parsed = moment(String(date.value), 'YYYYMMDD', true);
  return parsed.isValid() ? parsed.format('MMM D, YYYY') : 'Diary';
});

watch(
  () => route.query.date,
  (newDate) => {
    date.value = newDate;
  },
  { immediate: true }
);
</script>
