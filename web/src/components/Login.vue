<template>
  <el-container>
    <el-header>
      <h3>用户登陆</h3>
    </el-header>
    <el-main>
      <el-row>
        <el-col :xs="{span: 18, offset: 3}" :sm="{span: 10, offset: 7}" :span="4" :offset="10">
          <div v-if="errors.length">
            <div v-for="(error,key) in errors" :key="key">
              <el-alert v-bind:title="error" type="error"></el-alert>
            </div>
          </div>
        </el-col>
      </el-row>
      <el-row>
        <el-col :xs="{span: 18, offset: 3}" :sm="{span: 8, offset: 8}" :span="4" :offset="10">
          <el-input type="text" v-model="name" placeholder="请输入用户名"/>
        </el-col>
      </el-row>
      <el-row>
        <el-col :xs="{span: 18, offset: 3}" :sm="{span: 8, offset: 8}" :span="4" :offset="10">
          <el-input type="password" v-model="pwd" placeholder="请输入密码"/>
        </el-col>
      </el-row>
    </el-main>
    <el-footer>
      <el-button type="basic" size="medium" @click="login">提交</el-button>
    </el-footer>
  </el-container>
</template>

<script>
export default {
  name: "login",
  data() {
    return {
      name: "",
      pwd: "",
      errors: []
    };
  },
  methods: {
    // use yup to handle the validation
    login() {
      // check user and pass, if they are in table
      // the return the token
    }
  }
};
</script>

<!-- Add "scoped" attribute to limit CSS to this component only -->
<style scoped>
.el-container {
  margin: 10% auto;
}
</style>
