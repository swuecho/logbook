<template>
  <el-container class="lb-auth">
    <el-card shadow="never">
      <template #header><div class="lb-auth__title">Logbook</div></template>
      <el-main>
        <el-tabs v-model="activeTab" class="lb-auth__tabs">
          <el-tab-pane label="登录" name="login" />
          <el-tab-pane label="注册" name="register" />
        </el-tabs>

        <el-form
          v-if="activeTab === 'login'"
          ref="loginFormRef"
          label-position="left"
          :model="form"
          status-icon
          :rules="loginRules"
          label-width="60px"
          @submit.prevent
        >
          <el-form-item label="邮箱" prop="name">
            <el-input v-model="form.name" type="text" autocomplete="email" />
          </el-form-item>

          <el-form-item label="密码" prop="pwd">
            <el-input v-model="form.pwd" type="password" autocomplete="current-password" show-password />
          </el-form-item>

          <el-form-item label-width="0">
            <el-button class="lb-auth__submit" type="primary" :loading="submitting" @click="handleLogin">
              登录
            </el-button>
          </el-form-item>
        </el-form>

        <el-form
          v-else
          ref="registerFormRef"
          label-position="left"
          :model="form"
          status-icon
          :rules="registerRules"
          label-width="60px"
          @submit.prevent
        >
          <el-form-item label="邮箱" prop="name">
            <el-input v-model="form.name" type="text" autocomplete="email" />
          </el-form-item>

          <el-form-item label="密码" prop="pwd">
            <el-input v-model="form.pwd" type="password" autocomplete="new-password" show-password />
          </el-form-item>

          <el-form-item label="确认" prop="confirmPwd">
            <el-input v-model="form.confirmPwd" type="password" autocomplete="new-password" show-password />
          </el-form-item>

          <el-form-item label-width="0">
            <el-button class="lb-auth__submit" type="primary" :loading="submitting" @click="handleRegister">
              注册
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
import { loginUser, registerUser } from '@/services/auth';
import { getApiErrorMessage } from '@/services/apiError';

const activeTab = ref('login');
const loginFormRef = ref(null);
const registerFormRef = ref(null);
const submitting = ref(false);
const success = ref('');
const form = reactive({
  name: '',
  pwd: '',
  confirmPwd: '',
});
const errors = ref([]);

const emailRule = [
  { required: true, message: '请输入邮箱', trigger: 'blur' },
  { type: 'email', message: '不是正确的邮箱格式', trigger: 'blur' },
];

const passwordRule = [
  { required: true, message: '请输入密码', trigger: 'blur' },
  { min: 5, max: 100, message: '密码最少5个字符', trigger: 'blur' },
];

const confirmPwdRule = [
  { required: true, message: '请再次输入密码', trigger: 'blur' },
  {
    validator: (_rule, value, callback) => {
      if (value !== form.pwd) {
        callback(new Error('两次输入的密码不一致'));
      } else {
        callback();
      }
    },
    trigger: 'blur',
  },
];

const loginRules = {
  name: emailRule,
  pwd: passwordRule,
};

const registerRules = {
  name: emailRule,
  pwd: passwordRule,
  confirmPwd: confirmPwdRule,
};

const saveToken = (data) => {
  const { accessToken, expiresIn } = data;
  if (accessToken) {
    const expiresAt = new Date().getTime() + expiresIn * 1000;
    localStorage.setItem('JWT_TOKEN', accessToken);
    localStorage.setItem('JWT_EXPIRES_AT', expiresAt.toString());
    router.push({ path: '/' });
  }
};

const handleLogin = async () => {
  if (!loginFormRef.value) return;
  errors.value = [];
  success.value = '';

  try {
    const valid = await loginFormRef.value.validate();
    if (!valid) return;
  } catch {
    return;
  }

  submitting.value = true;
  try {
    const data = await loginUser(form.name, form.pwd);
    saveToken(data);
  } catch (error) {
    console.error('Login failed:', error);
    errors.value.push(getApiErrorMessage(error, '登录失败，请检查邮箱和密码。'));
  } finally {
    submitting.value = false;
  }
};

const handleRegister = async () => {
  if (!registerFormRef.value) return;
  errors.value = [];
  success.value = '';

  try {
    const valid = await registerFormRef.value.validate();
    if (!valid) return;
  } catch {
    return;
  }

  submitting.value = true;
  try {
    const data = await registerUser(form.name, form.pwd);
    saveToken(data);
  } catch (error) {
    console.error('Registration failed:', error);
    errors.value.push(getApiErrorMessage(error, '注册失败，请稍后重试。'));
  } finally {
    submitting.value = false;
  }
};
</script>

<style scoped>
.lb-auth__title {
  text-align: center;
}

.lb-auth__tabs {
  margin-bottom: 8px;
}

.lb-auth__submit {
  width: 100%;
}
</style>
