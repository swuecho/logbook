<template>
  <DiaryEditor :date="date"></DiaryEditor>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import DiaryEditor from "@/components/DiaryEditor";
import { get_date_of_today } from "../util.js";

const date = ref(null);

onMounted(() => {
  date.value = get_date_of_today();
});
</script>
