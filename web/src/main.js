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


router.beforeEach((to, from, next) => {
  const isAuthenticated = localStorage.getItem('jwtToken'); // Check if the JWT token is stored

  if (to.path !== '/login' && !isAuthenticated) {
    next('/login'); // Redirect to the login page if not authenticated
  } else {
    next();
  }
});

new Vue({
  router,
  render: h => h(App),
}).$mount('#app')