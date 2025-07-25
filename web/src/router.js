import Vue from 'vue'
import Router from 'vue-router'
import Home from './views/Home.vue'

import Login from './views/Login.vue';
import Logout from './views/Logout.vue';
import Admin from './views/Admin.vue';

Vue.use(Router)

export default new Router({
  mode: 'history',
  routes: [{
      path: '/',
      name: 'home',
      component: Home
    },
    { path: '/admin', component: Admin, name: 'admin' },
    { path: '/login', component: Login, name: 'login' },
    { path: '/logout', component: Logout },
    {
      path: '/content',
      name: 'content',
      // route level code-splitting
      // this generates a separate chunk (about.[hash].js) for this route
      // which is lazy-loaded when the route is visited.
      component: () => import( /* webpackChunkName: "about" */ './views/Content.vue')
    },
    {
      path: '/view',
      name: 'view',
      // route level code-splitting
      // this generates a separate chunk (about.[hash].js) for this route
      // which is lazy-loaded when the route is visited.
      component: () => import( /* webpackChunkName: "about" */ './views/Diary.vue')
    },
  ]
})