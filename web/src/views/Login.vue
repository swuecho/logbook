<template>
  <el-container class="lb-auth">
    <el-card shadow="never">
      <template #header>
        <div class="lb-auth__title-wrap">
          <div class="lb-auth__eyebrow">Welcome back</div>
          <div class="lb-auth__title">Logbook</div>
        </div>
      </template>
      <el-main>
        <p class="lb-auth__intro">Sign in to keep your notes, timelines, and todos in one steady workspace.</p>
        <el-form
          ref="ruleFormRef"
          label-position="left"
          :model="form"
          status-icon
          :rules="rules"
          label-width="60px"
          @submit.prevent
        >
          <el-form-item label="邮箱" prop="name">
            <el-input v-model="form.name" type="text" autocomplete="off" />
          </el-form-item>

          <el-form-item label="密码" prop="pwd">
            <el-input v-model="form.pwd" type="password" autocomplete="off" show-password />
          </el-form-item>

          <el-form-item label-width="0">
            <el-button class="lb-auth__submit" type="primary" size="default" round @click="submitForm">
              注册/登录
            </el-button>
          </el-form-item>
        </el-form>
        <el-row class="alert-row">
          <div v-if="errors.length">
            <div v-for="(error, key) in errors" :key="key">
              <el-alert :closable="false" :title="error" type="error" />
            </div>
          </div>
          <el-alert v-if="success" :closable="false" :title="success" type="success" />
        </el-row>
      </el-main>
    </el-card>
  </el-container>
</template>

<script setup>
import { ref, reactive } from 'vue';
import router from '@/router';
import { loginUser } from '@/services/auth';
import { getApiErrorMessage } from '@/services/apiError';

const ruleFormRef = ref(null);
const success = ref('');
const form = reactive({
  name: '',
  pwd: '',
});
const errors = ref([]);
const rules = {
  name: [
    { required: true, message: "请输入邮箱", trigger: "blur" },
    { type: "email", message: "不是正确的邮箱格式", trigger: "blur" },
  ],
  pwd: [
    { required: true, message: "请输入密码", trigger: "blur" },
    { min: 5, max: 100, message: "密码最少5个字符", trigger: "blur" },
  ],
};

const submitForm = async () => {
  if (!ruleFormRef.value) return;

  const valid = await ruleFormRef.value.validate().catch(() => false);
  if (valid) {
    await login();
  } else {
    console.log("error submit!!");
  }
};

const login = async () => {
  errors.value = [];

  const { name, pwd } = form;

  try {
    const data = await loginUser(name, pwd);
    console.log(data);
    const { accessToken, expiresIn } = data;
    if (accessToken) {
      const expiresAt = new Date().getTime() + expiresIn * 1000;
      localStorage.setItem("JWT_TOKEN", accessToken);
      localStorage.setItem("JWT_EXPIRES_AT", expiresAt.toString());
      router.push({ path: "/" });
    }
  } catch (error) {
    console.error('Login failed:', error);
    errors.value.push(getApiErrorMessage(error, "Login failed. Please check your credentials and try again."));
  }
};
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

.lb-auth__intro {
  margin: 0 0 1.1rem;
  text-align: center;
  color: var(--lb-text-muted);
  line-height: 1.5;
}

.lb-auth__submit {
  width: 100%;
}
</style>
