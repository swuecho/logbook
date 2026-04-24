<!-- OnlineStatusIndicator.vue -->
<template>
  <div class="online-status">
    <span class="online-status__label">{{ isOnline ? 'Online' : 'Offline' }}</span>
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
  gap: 0.45rem;
  font-size: 0.9rem;
  color: var(--lb-text-muted, #5a6d7e);
}

.online-status__label {
  font-size: 0.82rem;
}

.online-status > span:last-child {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.online {
  background-color: var(--lb-accent, #2d8659);
}

.offline {
  background-color: var(--lb-error, #b03a2e);
}
</style>
