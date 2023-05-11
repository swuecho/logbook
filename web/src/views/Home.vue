<template>
  <editor-wrapper :date="date"></editor-wrapper>
</template>

<script>
import EditorWrapper from "@/views/EditorWrapper";
import { get_date_of_today } from "@/util.js";

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
    EditorWrapper
  }
};
</script>

