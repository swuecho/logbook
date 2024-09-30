<!-- OnlineStatusIndicator.vue -->
<template>
  <div class="online-status">
    <span :class="{ 'online': isOnline, 'offline': !isOnline }"></span>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue';

const isOnline = ref(navigator.onLine);

const updateOnlineStatus = () => {
  isOnline.value = navigator.onLine;
};

onMounted(() => {
  window.addEventListener('online', updateOnlineStatus);
  window.addEventListener('offline', updateOnlineStatus);
});

onUnmounted(() => {
  window.removeEventListener('online', updateOnlineStatus);
  window.removeEventListener('offline', updateOnlineStatus);
});
</script>

<style scoped>
.online-status {
  display: flex;
  align-items: center;
  font-size: 0.9rem;
}

.online-status span {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  margin-right: 5px;
}

.online {
  background-color: #4CAF50;
}

.offline {
  background-color: #F44336;
}
</style>
