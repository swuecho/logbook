<template>
  <div id="app">
    <SyncPanel v-if="activeAccount" :key="activeAccount" />
    <RouterView :key="activeAccount" />
  </div>
</template>
<script setup>
import SyncPanel from '@/components/SyncPanel.vue';
import { activeAccount } from '@/services/session';
</script>
