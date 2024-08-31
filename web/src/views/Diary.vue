<template>
  <DiaryEditor :date="date"></DiaryEditor>
</template>

<script>
import DiaryEditor from "@/components/DiaryEditor";

export default {
  date() {
    return {
      date: null
    };
  },
  created() {
    this.date = this.$route.query.date;
  },
  components: {
    DiaryEditor
  }
};
</script>
