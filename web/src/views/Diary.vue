<template>
  <div class="app-page app-page--shell diary-view">
    <div class="app-shell app-shell--narrow">
      <section class="app-panel app-panel--soft diary-view__editor">
        <DiaryEditor :date="date" />
      </section>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import DiaryEditor from "@/components/DiaryEditor";

const route = useRoute();
const date = ref(route.query.date);

watch(
  () => route.query.date,
  (newDate) => {
    date.value = newDate;
  },
  { immediate: true }
);
</script>

<style scoped>
.diary-view__editor {
  padding: 1rem;
}
</style>
