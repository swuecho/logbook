<template>
  <div id="app">
    <RouterView :key="activeAccount" />
  </div>
</template>
<script setup>
import { activeAccount } from '@/services/session';
</script>
