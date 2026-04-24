<template>
  <el-container class="lb-auth">
    <el-card shadow="never">
      <template #header>
        <div class="lb-auth__title-wrap">
          <div class="lb-auth__eyebrow">Session</div>
          <div class="lb-auth__title">Logbook</div>
        </div>
      </template>
      <el-main>
        <el-result
          v-if="done && !errors.length"
          icon="success"
          title="已登出"
          sub-title="你的登录状态已清除。"
        >
          <template #extra>
            <div class="lb-auth__actions">
              <el-button class="lb-auth__primary" type="primary" round @click="goLogin">
                返回登录<span v-if="redirectIn > 0">（{{ redirectIn }}）</span>
              </el-button>
              <el-button round @click="goHome">回到首页</el-button>
            </div>
          </template>
        </el-result>

        <div v-else class="lb-logout__body">
          <div class="lb-logout__status">
            <el-text type="info">{{ loading ? '正在登出…' : '登出未完成' }}</el-text>
          </div>

          <div v-if="errors.length" class="lb-logout__alerts">
            <div v-for="(error, key) in errors" :key="key">
              <el-alert :closable="false" :title="error" type="error" />
            </div>
            <div class="lb-auth__actions">
              <el-button type="primary" round :loading="loading" @click="logout">重试登出</el-button>
              <el-button round @click="goLogin">返回登录</el-button>
            </div>
          </div>
        </div>
      </el-main>
    </el-card>
  </el-container>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';
import router from '@/router';
import { logoutUser } from '@/services/auth';
import { getApiErrorMessage } from '@/services/apiError';

const done = ref(false);
const loading = ref(false);
const errors = ref([]);
const redirectIn = ref(3);

let redirectTimer = null;
let countdownTimer = null;

const logout = async () => {
  if (loading.value) return;
  errors.value = [];
  done.value = false;
  loading.value = true;

  try {
    await logoutUser();

    localStorage.removeItem('JWT_TOKEN');
    localStorage.removeItem('JWT_EXPIRES_AT');

    done.value = true;
    startRedirectCountdown();
  } catch (error) {
    console.error('Logout failed:', error);
    errors.value.push(getApiErrorMessage(error, '登出失败，请重试。'));
  } finally {
    loading.value = false;
  }
};

const goLogin = () => router.push({ path: '/login' });
const goHome = () => router.push({ path: '/' });

const clearTimers = () => {
  if (redirectTimer) clearTimeout(redirectTimer);
  if (countdownTimer) clearInterval(countdownTimer);
  redirectTimer = null;
  countdownTimer = null;
};

const startRedirectCountdown = () => {
  clearTimers();
  redirectIn.value = 3;

  countdownTimer = setInterval(() => {
    redirectIn.value = Math.max(0, redirectIn.value - 1);
  }, 1000);

  redirectTimer = setTimeout(() => {
    goLogin();
  }, 3000);
};

onMounted(() => {
  logout();
});

onBeforeUnmount(() => {
  clearTimers();
});
</script>

<style scoped>
.lb-auth__title-wrap {
  text-align: center;
}

.lb-auth__eyebrow {
  margin-bottom: 0.35rem;
  font-size: 0.76rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--lb-text-subtle);
}

.lb-auth__title {
  text-align: center;
}

.lb-auth__actions {
  display: flex;
  gap: 0.75rem;
  justify-content: center;
  flex-wrap: wrap;
}

.lb-auth__primary {
  min-width: 12rem;
}

.lb-logout__body {
  display: grid;
  gap: 1rem;
}

.lb-logout__status {
  text-align: center;
}

.lb-logout__alerts {
  display: grid;
  gap: 0.75rem;
}
</style>
