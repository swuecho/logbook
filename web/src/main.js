import { createApp } from 'vue'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import ElementTiptapPlugin from 'element-tiptap'
import 'element-tiptap/lib/style.css'

import router from './router'
import App from './App.vue'
import './styles/ui.css'

import { VueQueryPlugin, QueryClient } from '@tanstack/vue-query'
import { initTabLock } from '@/services/tabLock';
import { initTheme } from '@/services/theme';
import { activeAccount, restoreSession } from '@/services/session';
import { initSync, onLocalChange } from '@/services/sync';
import { registerOfflineApp } from '@/services/offline';

initTheme();
initTabLock();

restoreSession();
router.beforeEach((to) => {
  if (to.path !== '/login' && !activeAccount.value) return '/login';
});

const app = createApp(App)
app.use(router)
app.use(ElementPlus)
app.use(ElementTiptapPlugin)
const queryClient = new QueryClient();
app.use(VueQueryPlugin, { queryClient });
onLocalChange(() => { queryClient.invalidateQueries({ queryKey: ['diaryIds'] }); });
window.addEventListener('logbook-session', () => { queryClient.clear(); });
app.mount('#app')

initSync();
registerOfflineApp();
