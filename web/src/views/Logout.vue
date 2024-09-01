<template>
        <el-container>
                <el-card shadow="never">
                        <div slot="header" class="clearfix">Logbook</div>
                        <el-main>
                                <el-row>
                                        <el-button type="primary" size="medium" round @click="logout">登出</el-button>
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
import { ref } from 'vue';
import axios from "@/axiosConfig.js";
import router from '@/router';

const success = ref('');
const errors = ref([]);

const logout = async () => {
        errors.value = [];

        try {
                // Send POST request to /api/logout
                await axios.post('/api/logout');

                // Remove JWT token and expiration from localStorage
                localStorage.removeItem("JWT_TOKEN");
                localStorage.removeItem("JWT_EXPIRES_AT");

                success.value = "登出成功";

                // Redirect to login page after a short delay
                setTimeout(() => {
                        router.push({ path: "/login" });
                }, 1500);
        } catch (error) {
                console.error(error);
                errors.value.push("登出失败，请重试。");
        }
};
</script>

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

.alert-row {
        margin-top: 20px;
}
</style>