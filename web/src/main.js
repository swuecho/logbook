import { createApp } from 'vue'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import ElementTiptapPlugin from 'element-tiptap'
import 'element-tiptap/lib/style.css'

import router from './router'
import App from './App.vue'
import './styles/ui.css'

import { VueQueryPlugin } from '@tanstack/vue-query'
import { initTabLock } from '@/services/tabLock';

initTabLock();

function IsAuthenticatedValid() {
  const isAuthenticated = localStorage.getItem('JWT_TOKEN');
  const expiresAt = localStorage.getItem('JWT_EXPIRES_AT');
  const expiresAtMs = Number(expiresAt);
  const expired = !expiresAtMs || expiresAtMs <= Date.now();
  return Boolean(isAuthenticated) && !expired
}

router.beforeEach((to, from, next) => {
  if (to.path !== '/login' && !IsAuthenticatedValid()) {
    next('/login');
  } else {
    next();
  }
});

const app = createApp(App)
app.use(router)
app.use(ElementPlus)
app.use(ElementTiptapPlugin)
app.use(VueQueryPlugin)
app.mount('#app')
