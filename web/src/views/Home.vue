<template>
  <div>
    <div class="nav">
      {{ time }}
      <div>
        <a @click="navigateDate(-1)">
          <Icon icon="grommet-icons:form-previous" width="1.2rem" height="1.2rem" />
        </a>
        <a href="#">{{ date }}</a>
        <a @click="navigateDate(1)">
          <Icon icon="grommet-icons:form-next" width="1.2rem" height="1.2rem" />
        </a>
      </div>
      <a v-if="date != today" :href="'/view?date=' + today">Today</a>
      <a v-if="date == today" href="/todo">Todo</a>
      <a href="content">
        <Icon :icon="icons.tableOfContents" />
      </a>
    </div>
    <div class="content">
      <DiaryEditor :date="date"></DiaryEditor>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue2';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
import DiaryEditor from "@/components/DiaryEditor";

const now = ref(moment());
const date = ref(moment().format('YYYYMMDD'))
const timeFormat = 'h:mm:ss a';
const icons = {
  tableOfContents,
};

onMounted(() => {
  // eslint-disable-next-line no-unused-vars
  date.value = now.value.format("YYYYMMDD")
  const interval = setInterval(() => now.value = moment(), 1000);
});

const today = computed(() => {
  return now.value.format('YYYYMMDD');
});


function navigateDate(offset) {
  const newDate = moment(now.value).add(offset, 'days');
  if (newDate > today) {
    alert("can not be in the future")
    return
  }
  router.push(`/view?date=${newDate.format('YYYYMMDD')}`);
}

const time = computed(() => {
  return now.value.format(timeFormat);
});


</script>

<style>
.content {
  max-width: 65rem;
  margin: auto;
}
</style>