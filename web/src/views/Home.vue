<template>
  <DiaryEditor :date="date"></DiaryEditor>
</template>

<script>
import DiaryEditor from "@/components/DiaryEditor";
import { get_date_of_today } from "../util.js";

export default {
  date() {
    return {
      date: null
    };
  },
  created() {
    this.date = get_date_of_today();
  },
  components: {
    DiaryEditor
  }
};
</script>
