<template>
  <el-container>
    <el-card shadow="never">
      <div slot="header" class="clearfix">Logbook</div>
      <el-main>
        <el-row>
          <el-form ref="ruleFormRef" label-position="left" :model="form" status-icon :rules="rules" label-width="60px">
            <el-form-item label="邮箱" prop="name">
              <el-input v-model="form.name" type="text" autocomplete="off" />
            </el-form-item>

            <el-form-item label="密码" prop="pwd">
              <el-input v-model="form.pwd" type="password" autocomplete="off" />
            </el-form-item>
          </el-form>
          <el-button type="primary" size="medium" round @click="submitForm">注册/登录</el-button>
        </el-row>
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

<!-- Add "scoped" attribute to limit CSS to this component only -->
<style scoped>
.el-container {
  margin: 10% auto;
  text-align: center;
  display: flex;
  align-items: center;
  justify-content: center;
}

.el-main {
  min-width: 400px;
  max-width: 600px;
  margin: auto;
}

.el-card {
  border: none;
}

.el-card__header div {
  font-size: 1.5rem;
}
</style>
