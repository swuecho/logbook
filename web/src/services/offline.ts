import { reactive } from 'vue';

export const offlineStatus = reactive({ ready: false, updateReady: false, message: '' });

export async function registerOfflineApp() {
  if (import.meta.env.DEV) { offlineStatus.message = 'Offline reopening is enabled in production builds.'; return; }
  if (!('serviceWorker' in navigator) || !window.isSecureContext) {
    offlineStatus.message = 'Offline reopening requires HTTPS and service worker support.';
    return;
  }
  try {
    const registration = await navigator.serviceWorker.register('/sw.js', { updateViaCache: 'none' });
    offlineStatus.updateReady = Boolean(registration.waiting);
    registration.addEventListener('updatefound', () => {
      registration.installing?.addEventListener('statechange', () => {
        offlineStatus.updateReady = Boolean(registration.waiting);
      });
    });
    await navigator.serviceWorker.ready;
    offlineStatus.ready = true;
  } catch {
    offlineStatus.message = 'App download incomplete. Reopen online to enable offline use.';
  }
}
