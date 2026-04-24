import { createRouter, createWebHistory } from 'vue-router'
import Home from './views/Home.vue'

import Login from './views/Login.vue';
import Logout from './views/Logout.vue';
import Admin from './views/Admin.vue';

export default createRouter({
  history: createWebHistory(),
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
      component: () => import('./views/Content.vue')
    },
    {
      path: '/view',
      name: 'view',
      component: () => import('./views/Diary.vue')
    },
    {
      path: '/calendar',
      name: 'calendar',
      component: () => import('./views/Calendar.vue')
    },
  ]
})
