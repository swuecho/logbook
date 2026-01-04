<!-- OnlineStatusIndicator.vue -->
<template>
  <div class="online-status">
    <span :class="{ 'online': isOnline, 'offline': !isOnline }"></span>
  </div>
</template>

<script setup>
import { useOnlineStatus } from '../hooks/useOnlineStatus';

const { isOnline } = useOnlineStatus();
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
