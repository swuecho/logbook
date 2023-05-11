<template>
  <editor-wrapper :date="date"></editor-wrapper>
</template>

<script>
import EditorWrapper from "@/views/EditorWrapper";

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
    EditorWrapper
  }
};
</script>

