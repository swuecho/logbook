<template>
  <DiaryEditor :date="date"></DiaryEditor>
</template>

<script setup>
import { ref, watch } from 'vue';
import router from '@/router';
import DiaryEditor from "@/components/DiaryEditor";

const date = ref(router.currentRoute.query.date);

watch(
  () => router.currentRoute.query.date,
  (newDate) => {
    date.value = newDate;
  },
  { immediate: true }
);
</script>
