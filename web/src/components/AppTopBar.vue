<template>
  <el-header class="app-header-bar app-header-bar--dense app-top-bar">
    <div class="app-top-bar__start">
      <slot name="start">
        <button v-if="showHome" type="button" class="linkish" aria-label="Home" title="Home" @click="goHome">
          <Icon :icon="homeIcon" class="app-top-bar__icon" />
        </button>
        <h1 v-if="title" class="app-top-bar__title">{{ title }}</h1>
      </slot>
    </div>

    <div class="app-top-bar__center">
      <slot name="center" />
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
        <Icon icon="material-symbols:markdown-copy-outline" class="app-top-bar__icon" />
      </button>
      <router-link v-if="showCalendar" to="/calendar" class="linkish" title="Calendar" aria-label="Calendar">
        <Icon :icon="calendarMonth" class="app-top-bar__icon" />
      </router-link>
      <router-link v-if="showSearch" to="/search" class="linkish" title="Search entries" aria-label="Search entries">
        <Icon :icon="magnifyIcon" class="app-top-bar__icon" />
      </router-link>
      <router-link v-if="showContent" to="/content" class="linkish" title="Browse entries" aria-label="Browse entries">
        <Icon :icon="tableOfContents" class="app-top-bar__icon" />
      </router-link>
      <router-link v-if="isAdmin" to="/admin" class="linkish" title="Admin" aria-label="Admin">
        <Icon :icon="adminIcon" class="app-top-bar__icon" />
      </router-link>
      <button
        type="button"
        class="linkish"
        title="Settings"
        aria-label="Settings"
        @click="settingsVisible = true"
      >
        <Icon :icon="settingsIcon" class="app-top-bar__icon" />
      </button>
      <button
        v-if="isAuthenticated"
        type="button"
        class="linkish"
        title="Logout"
        aria-label="Logout"
        @click="goLogout"
      >
        <Icon :icon="logoutIcon" class="app-top-bar__icon" />
      </button>
      <slot name="actions-after" />
    </nav>

    <el-dialog v-model="settingsVisible" title="Settings" class="app-settings-dialog" width="360px">
      <section class="app-settings-section">
        <div class="app-settings-section__title">Theme</div>
        <div class="app-theme-options" role="radiogroup" aria-label="Theme">
          <button
            v-for="theme in themeOptions"
            :key="theme.value"
            type="button"
            class="app-theme-option"
            :class="{ 'is-active': currentTheme === theme.value }"
            role="radio"
            :aria-checked="currentTheme === theme.value"
            @click="chooseTheme(theme.value)"
          >
            <span>{{ theme.label }}</span>
            <span v-if="currentTheme === theme.value" class="app-theme-option__current">Current</span>
          </button>
        </div>
      </section>
    </el-dialog>
  </el-header>
</template>

<script setup>
import { computed, ref } from 'vue';
import { Icon } from '@iconify/vue';
import homeIcon from '@iconify/icons-mdi/home-outline';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
import calendarMonth from '@iconify/icons-mdi/calendar-month';
import magnifyIcon from '@iconify/icons-mdi/magnify';
import logoutIcon from '@iconify/icons-mdi/logout';
import adminIcon from '@iconify/icons-mdi/shield-account-outline';
import settingsIcon from '@iconify/icons-mdi/cog-outline';
import router from '@/router';
import { parseJwt } from '@/util';
import OnlineStatusIndicator from '@/components/OnlineStatusIndicator.vue';
import { currentTheme, setTheme, themeOptions } from '@/services/theme';

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

const settingsVisible = ref(false);

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

function chooseTheme(theme) {
  setTheme(theme);
}
</script>

<style scoped>
.app-top-bar {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 1rem;
  width: 100%;
  height: auto !important;
  min-height: 3rem;
}

.app-top-bar__start,
.app-top-bar__center,
.app-top-bar__actions {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  min-width: 0;
}

.app-top-bar__center {
  justify-content: center;
}

.app-top-bar__actions {
  justify-content: flex-end;
  flex-wrap: nowrap;
}

.app-top-bar__start {
  justify-content: flex-start;
}

.app-top-bar__title {
  margin: 0;
  color: var(--lb-text);
  font-size: 1rem;
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.app-top-bar__icon {
  width: 1.1rem;
  height: 1.1rem;
}

.app-settings-section {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.app-settings-section__title {
  color: var(--lb-text-muted);
  font-size: 0.82rem;
  font-weight: 600;
}

.app-theme-options {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
}

.app-theme-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  width: 100%;
  min-height: 2.35rem;
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--lb-border);
  border-radius: var(--lb-radius-sm);
  background: var(--lb-bg-elevated);
  color: var(--lb-text);
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.app-theme-option:hover {
  border-color: var(--lb-border-strong);
  background: var(--lb-hover);
}

.app-theme-option.is-active {
  border-color: var(--lb-accent);
  background: var(--lb-bg-soft);
}

.app-theme-option__current {
  color: var(--lb-text-subtle);
  font-size: 0.75rem;
}

@media (max-width: 768px) {
  .app-top-bar {
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: center;
    gap: 0.5rem 0.75rem;
  }

  .app-top-bar__start {
    grid-column: 1;
    grid-row: 1;
    justify-content: flex-start;
  }

  .app-top-bar__center {
    grid-column: 1 / -1;
    grid-row: 2;
    justify-content: flex-start;
    flex-wrap: wrap;
  }

  .app-top-bar__center:empty {
    display: none;
  }

  .app-top-bar__actions {
    grid-column: 2;
    grid-row: 1;
    justify-content: flex-end;
    gap: 0.2rem;
    overflow-x: auto;
    scrollbar-width: none;
  }

  .app-top-bar__actions::-webkit-scrollbar {
    display: none;
  }
}
</style>
