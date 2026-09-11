// Each mounted vault has its own keys. Only lock notifications cross tabs.
export function installVaultLock(onLock, target = window, timeout = 5 * 60 * 1000) {
  let lastActivity = Date.now();
  let channel;
  const clear = () => { lastActivity = Date.now(); onLock(); };
  const lock = () => { clear(); channel?.postMessage('lock'); };
  const check = () => { if (Date.now() - lastActivity >= timeout) lock(); };
  const activity = () => { check(); lastActivity = Date.now(); };
  const hidden = () => { if (document.visibilityState === 'hidden') lock(); else check(); };
  const storage = event => { if (['JWT_TOKEN', 'LOGBOOK_ACCOUNT'].includes(event.key)) lock(); };
  if (typeof BroadcastChannel !== 'undefined') {
    channel = new BroadcastChannel('logbook-vault-lock');
    channel.onmessage = event => { if (event.data === 'lock') clear(); };
  }
  for (const type of ['pointerdown', 'keydown', 'touchstart']) target.addEventListener(type, activity);
  target.addEventListener('logbook-session', lock);
  target.addEventListener('storage', storage);
  target.addEventListener('pagehide', lock);
  document.addEventListener('visibilitychange', hidden);
  const timer = setInterval(check, 1000);
  return {
    lock,
    dispose() {
      clearInterval(timer);
      channel?.close();
      for (const type of ['pointerdown', 'keydown', 'touchstart']) target.removeEventListener(type, activity);
      target.removeEventListener('logbook-session', lock);
      target.removeEventListener('storage', storage);
      target.removeEventListener('pagehide', lock);
      document.removeEventListener('visibilitychange', hidden);
    },
  };
}
