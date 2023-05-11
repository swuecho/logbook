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

new Vue({
  router,
  render: h => h(App),
}).$mount('#app')