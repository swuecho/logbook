import { ref } from 'vue';

const ACCOUNT_KEY = 'LOGBOOK_ACCOUNT';
export const activeAccount = ref(localStorage.getItem(ACCOUNT_KEY) || '');

function tokenAccount(token: string) {
  try {
    const part = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const claims = JSON.parse(atob(part.padEnd(Math.ceil(part.length / 4) * 4, '=')));
    if (!claims.user_id) return '';
    return JSON.stringify([location.origin, claims.iss, claims.aud, String(claims.user_id)]);
  } catch { return ''; }
}

export function restoreSession() {
  // Tokens here were obtained through a previous successful server login.
  const account = tokenAccount(localStorage.getItem('JWT_TOKEN') || '');
  if (account) {
    activeAccount.value = account;
    localStorage.setItem(ACCOUNT_KEY, account);
  }
}

export function acceptSession(data: { accessToken: string; expiresIn: number }) {
  const account = tokenAccount(data.accessToken);
  if (!account) throw new Error('The server returned an invalid account.');
  localStorage.setItem('JWT_TOKEN', data.accessToken);
  localStorage.setItem('JWT_EXPIRES_AT', String(Date.now() + data.expiresIn * 1000));
  localStorage.setItem(ACCOUNT_KEY, account);
  activeAccount.value = account;
  window.dispatchEvent(new Event('logbook-session'));
}

export function clearSession() {
  localStorage.removeItem('JWT_TOKEN');
  localStorage.removeItem('JWT_EXPIRES_AT');
  localStorage.removeItem(ACCOUNT_KEY);
  activeAccount.value = '';
  window.dispatchEvent(new Event('logbook-session'));
}

export function syncCredentials() {
  const token = localStorage.getItem('JWT_TOKEN') || '';
  const account = activeAccount.value;
  if (!account || tokenAccount(token) !== account || Number(localStorage.getItem('JWT_EXPIRES_AT')) <= Date.now()) return null;
  return { account, token };
}

window.addEventListener('storage', event => {
  if ([ACCOUNT_KEY, 'JWT_TOKEN'].includes(event.key || '')) {
    activeAccount.value = localStorage.getItem(ACCOUNT_KEY) || '';
    window.dispatchEvent(new Event('logbook-session'));
  }
});
