import Vue from 'vue'
import './plugins/axios'
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

function IsAuthenticatedValid() {
  const isAuthenticated = localStorage.getItem('JWT_Token'); // Check if the JWT token is stored
  const expiresAt = localStorage.getItem('JWT_EXPIRES_AT'); // Check if the JWT token is stored
  let seconds = new Date() / 1000;
  let expired = expiresAt - seconds < 0 
  return isAuthenticated && !expired
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