import Vue from 'vue'
import router from './router'
import App from './App.vue'

import Element from 'element-ui'

import { ElementTiptapPlugin } from 'element-tiptap';

// import ElementUI's styles
import 'element-ui/lib/theme-chalk/index.css';
// import this package's styles
import 'element-tiptap/lib/index.css';

Vue.use(Element)
Vue.use(ElementTiptapPlugin);

Vue.config.productionTip = false

import { VueQueryPlugin } from '@tanstack/vue-query'
import { initTabLock } from '@/services/tabLock';

Vue.use(VueQueryPlugin)

initTabLock();


function IsAuthenticatedValid() {
  const isAuthenticated = localStorage.getItem('JWT_TOKEN'); // Check if the JWT token is stored
  const expiresAt = localStorage.getItem('JWT_EXPIRES_AT'); // Check if the JWT token is stored
  const expiresAtMs = Number(expiresAt);
  const expired = !expiresAtMs || expiresAtMs <= Date.now();
  return Boolean(isAuthenticated) && !expired
}

router.beforeEach((to, from, next) => {
  if (to.path !== '/login' && !IsAuthenticatedValid()) {
    next('/login'); // Redirect to the login page if not authenticated
  } else {
    next();
  }
});

new Vue({
  router,
  render: h => h(App),
}).$mount('#app')
