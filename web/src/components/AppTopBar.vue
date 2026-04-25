<template>
  <el-header class="app-header-bar app-header-bar--dense app-top-bar">
    <div class="app-top-bar__start">
      <slot name="start">
        <button v-if="showHome" type="button" class="linkish" aria-label="Home" @click="goHome">
          <Icon :icon="homeIcon" height="24" />
        </button>
        <h1 v-if="title" class="app-top-bar__title">{{ title }}</h1>
      </slot>
    </div>

    <nav class="app-top-bar__actions" aria-label="Main navigation">
      <slot name="actions-before" />
      <OnlineStatusIndicator v-if="showStatus" />
      <button
        v-if="showMarkdown"
        type="button"
        class="linkish"
        aria-label="Markdown preview"
        title="Markdown preview"
        @click="$emit('open-markdown')"
      >
        <Icon icon="material-symbols:markdown-copy-outline" />
      </button>
      <router-link v-if="showCalendar" to="/calendar" class="linkish" title="Calendar" aria-label="Calendar">
        <Icon :icon="calendarMonth" />
      </router-link>
      <router-link v-if="showSearch" to="/search" class="linkish" title="Search entries" aria-label="Search entries">
        <Icon :icon="magnifyIcon" />
      </router-link>
      <router-link v-if="showContent" to="/content" class="linkish" title="Browse entries" aria-label="Browse entries">
        <Icon :icon="tableOfContents" />
      </router-link>
      <router-link v-if="isAdmin" to="/admin" class="linkish" title="Admin" aria-label="Admin">
        <Icon :icon="adminIcon" />
      </router-link>
      <button
        v-if="isAuthenticated"
        type="button"
        class="linkish"
        title="Logout"
        aria-label="Logout"
        @click="goLogout"
      >
        <Icon :icon="logoutIcon" />
      </button>
      <slot name="actions-after" />
    </nav>
  </el-header>
</template>

<script setup>
import { computed } from 'vue';
import { Icon } from '@iconify/vue';
import homeIcon from '@iconify/icons-mdi/home-outline';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
import calendarMonth from '@iconify/icons-mdi/calendar-month';
import magnifyIcon from '@iconify/icons-mdi/magnify';
import logoutIcon from '@iconify/icons-mdi/logout';
import adminIcon from '@iconify/icons-mdi/shield-account-outline';
import router from '@/router';
import { parseJwt } from '@/util';
import OnlineStatusIndicator from '@/components/OnlineStatusIndicator.vue';

defineProps({
  title: {
    type: String,
    default: '',
  },
  showHome: {
    type: Boolean,
    default: true,
  },
  showStatus: {
    type: Boolean,
    default: false,
  },
  showMarkdown: {
    type: Boolean,
    default: false,
  },
  showCalendar: {
    type: Boolean,
    default: true,
  },
  showSearch: {
    type: Boolean,
    default: true,
  },
  showContent: {
    type: Boolean,
    default: true,
  },
});

defineEmits(['open-markdown']);

const isAuthenticated = computed(() => {
  const token = localStorage.getItem('JWT_TOKEN');
  const expiresAt = Number(localStorage.getItem('JWT_EXPIRES_AT'));
  return Boolean(token) && Boolean(expiresAt) && expiresAt > Date.now();
});

const isAdmin = computed(() => {
  const token = localStorage.getItem('JWT_TOKEN');
  if (!token || !isAuthenticated.value) return false;

  try {
    const claims = parseJwt(token);
    return claims.role === 'admin';
  } catch (error) {
    console.error('Failed to parse auth token:', error);
    return false;
  }
});

function goHome() {
  router.push('/');
}

function goLogout() {
  router.push({ path: '/logout' });
}
</script>

<style scoped>
.app-top-bar {
  min-height: 3rem;
}

.app-top-bar__start,
.app-top-bar__actions {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  min-width: 0;
}

.app-top-bar__actions {
  flex-wrap: wrap;
  justify-content: flex-end;
}

.app-top-bar__title {
  margin: 0;
  color: var(--lb-text);
  font-size: 1rem;
  font-weight: 600;
}

@media (max-width: 768px) {
  .app-top-bar {
    align-items: stretch;
  }

  .app-top-bar__actions {
    justify-content: center;
    gap: 0.5rem;
  }
}
</style>
