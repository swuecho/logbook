<template>
  <div class="content">
    <div class="nav">
          

      {{ time }}
      <div class="date-nav">
        <div @click="navigateDate(-1)" class="icon-container">
          <Icon icon="grommet-icons:form-previous" width="1.2rem" />
        </div>
        <div>
          {{ displayDate }}
        </div>
        <div v-if="date < today" @click="navigateDate(1)" class="icon-container">
          <Icon icon="grommet-icons:form-next" width="1.2rem" />
        </div>
        <div v-if="date != today" @click="navigateDateToToday" class="icon-container">
          <Icon icon="fluent:arrow-next-16-regular" width="1.2rem" />
        </div>
      </div>
      <el-dialog :visible="dialogVisible" @close="closeModal">
        <Todo></Todo>
      </el-dialog>
      <div class="right-corner">
         <OnlineStatusIndicator />
        <div @click="openModal">
          <Icon icon="streamline:task-list" />
        </div>
        <div>
        <a href="/content">
          <Icon :icon="tableOfContents" />
        </a>
    </div>
      </div>
    </div>
    <DiaryEditor :date="date"></DiaryEditor>
           

  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import moment from 'moment';
import { Icon } from '@iconify/vue2';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
import DiaryEditor from "@/components/DiaryEditor";
import Todo from '@/components/Todo.vue';
import OnlineStatusIndicator from '@/components/OnlineStatusIndicator.vue';



const now = ref(moment());
const date = ref(moment().format('YYYYMMDD'))
const dialogVisible = ref(false)

onMounted(() => {
  // eslint-disable-next-line no-unused-vars
  date.value = now.value.format("YYYYMMDD")
  const interval = setInterval(() => now.value = moment(), 1000);
});

const today = computed(() => {
  return now.value.format('YYYYMMDD');
});

const time = computed(() => {
  const timeFormat = 'h:mm:ss a';
  return now.value.format(timeFormat);
});

const displayDate = computed(() => {
  if (date.value == today.value) {
    return 'Today'
  } else {
    return date.value
  }
})

function navigateDate(offset) {
  const newDate = moment(date.value).add(offset, 'days').format("YYYYMMDD");
  if (newDate > today.value) {
    alert("can not into future")
  }
  date.value = newDate
}

function navigateDateToToday() {
  date.value = today.value
}

function openModal() {
  dialogVisible.value = true
}
function closeModal() {
  dialogVisible.value = false
}

</script>

<style scoped>
.content {
  max-width: 65rem;
  margin: auto;
}

.nav {
  margin: 1em 1em 1rem 1em;
  display: flex;
  justify-content: space-between;
}

.nav .date-nav {
  display: flex;
  align-items: center;
  white-space: nowrap;
}

.icon-container {
  display: flex;
  align-items: center;
}

.right-corner {
  display: flex;
  flex-direction: row;
  gap: 3rem;
  justify-content: space-around;
  align-items: center;
  gap: 10px;
}
</style>