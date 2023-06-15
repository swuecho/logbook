<template>
  <el-container>
    <el-card shadow="never">
      <div slot="header" class="clearfix">Logbook</div>
      <el-main>
        <el-row>
          <el-form ref="ruleForm" label-position="left" :model="form" status-icon :rules="rules" label-width="60px">
            <el-form-item label="邮箱" prop="name">
              <el-input v-model="form.name" type="text" autocomplete="off" />
            </el-form-item>

            <el-form-item label="密码" prop="pwd">
              <el-input v-model="form.pwd" type="password" autocomplete="off" />
            </el-form-item>
          </el-form>
          <el-button type="primary" size="medium" round @click="submit_form('ruleForm')">注册/登录</el-button>
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

<script>
import axios from "axios";

export default {
  name: "Login",
  data() {
    return {
      success: "",
      form: {
        name: "",
        pwd: "",
      },
      errors: [],
      rules: {
        name: [
          { required: true, message: "请输入邮箱", trigger: "blur" },
          { type: "email", message: "不是正确的邮箱格式", trigger: "blur" },
        ],
        pwd: [
          { required: true, message: "请输入密码", trigger: "blur" },
          { min: 5, max: 100, message: "密码最少5个字符", trigger: "blur" },
        ],
      },
    };
  },
  methods: {
    submit_form(formName) {
      this.$refs[formName].validate((valid) => {
        if (valid) {
          this.login();
        } else {
          console.log("error submit!!");
          return false;
        }
      });
    },
    async login() {
      // TODO: verify $router is avaliable
      // clear error
      this.errors = [];

      let { name, pwd } = this.form;
      // bestqa_fs
      const options = {
        method: "POST",
        url: "/api/login",
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          //ClientId: "bc442bb2b1d848fba5be2aa24312e711",
          Username: name,
          Password: pwd,
        },
      };
      let app = this;
      axios
        .request(options)
        .then(function (response) {
          let accessToken = response.data;
          let jwt = accessToken["AccessToken"];
          if (jwt) {
            let jwtMap = parseJwt(jwt);
            localStorage.setItem("jwtToken", jwt);
            localStorage.setItem("username", name);
            localStorage.setItem("user_id", jwtMap["user_id"]);
            localStorage.setItem("exp", jwtMap["exp"]);
            app.$router.push({ path: "/" });
          }
        })
        .catch(function (error) {
          console.error(error);
        });

    },
  },
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


