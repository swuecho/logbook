import { ref } from 'vue';

const ACCOUNT_KEY = 'LOGBOOK_ACCOUNT';
const EVENT_KEY = 'LOGBOOK_SESSION_EVENT';
const LOGOUT_KEY = 'LOGBOOK_LOGOUT_PENDING';
export const activeAccount = ref(localStorage.getItem(ACCOUNT_KEY) || '');
export const sessionRole = ref('');
type SessionData = { authenticated: boolean; userId?: number; issuer?: string; audience?: string; role?: string; csrfToken: string; expiresAt?: string };
type Credentials = { account: string; csrfToken: string; expiresAt: number };
let credentials: Credentials | null = null;
let generation = 0;
let logoutFlight: Promise<void> | null = null;
let logoutRetry: ReturnType<typeof setTimeout>;
function retryPendingLogout() {
  clearTimeout(logoutRetry);
  logoutRetry = setTimeout(() => {
    if (!localStorage.getItem(LOGOUT_KEY)) return;
    if (navigator.onLine) void logoutSession().catch(() => {});
    else retryPendingLogout();
  }, 2000);
}

function notify(broadcast = false) {
  window.dispatchEvent(new Event('logbook-session'));
  if (broadcast) localStorage.setItem(EVENT_KEY, crypto.randomUUID());
}

// Keep the exact legacy account namespace so existing offline writing stays put.
// Legacy JWTs are read once only to recover that namespace, never for authentication.
function migrateLegacyAccount() {
  const token = localStorage.getItem('JWT_TOKEN');
  if (!activeAccount.value && token && !localStorage.getItem(LOGOUT_KEY)) {
    try {
      const part = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const claims = JSON.parse(atob(part.padEnd(Math.ceil(part.length / 4) * 4, '=')));
      if (claims.user_id && claims.iss && claims.aud) {
        activeAccount.value = JSON.stringify([location.origin, claims.iss, claims.aud, String(claims.user_id)]);
        localStorage.setItem(ACCOUNT_KEY, activeAccount.value);
      }
    } catch { /* Malformed old credentials cannot authenticate or select an account. */ }
  }
  localStorage.removeItem('JWT_TOKEN');
  localStorage.removeItem('JWT_EXPIRES_AT');
}

export async function sessionRequest(path: string, options: RequestInit = {}, timeout = 8000) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeout);
  try {
    const response = await fetch(path, { ...options, credentials: 'same-origin', cache: 'no-store', redirect: 'error', signal: controller.signal });
    const data = await response.json();
    if (!response.ok) throw new Error(data.message || 'The request could not be completed.');
    return data;
  } catch (error) {
    if (error instanceof TypeError || (error instanceof DOMException && error.name === 'AbortError')) {
      throw new Error('Could not reach Logbook. Check your connection and try again.');
    }
    throw error;
  } finally { clearTimeout(timer); }
}

export function acceptSession(data: SessionData, broadcast = true) {
  if (!data.authenticated || !Number.isInteger(data.userId) || !data.issuer || !data.audience || !data.csrfToken || !data.expiresAt) {
    throw new Error('The server returned an invalid session.');
  }
  const expiresAt = Date.parse(data.expiresAt);
  if (!Number.isFinite(expiresAt) || expiresAt <= Date.now()) throw new Error('Your session has expired. Sign in again.');
  const account = JSON.stringify([location.origin, data.issuer, data.audience, String(data.userId)]);
  const changed = credentials?.csrfToken !== data.csrfToken || activeAccount.value !== account || sessionRole.value !== data.role;
  generation++;
  credentials = { account, csrfToken: data.csrfToken, expiresAt };
  sessionRole.value = data.role || '';
  localStorage.setItem(ACCOUNT_KEY, account);
  activeAccount.value = account;
  if (changed || broadcast) notify(broadcast);
}

export function clearSession(broadcast = true) {
  generation++;
  credentials = null;
  sessionRole.value = '';
  localStorage.removeItem('JWT_TOKEN');
  localStorage.removeItem('JWT_EXPIRES_AT');
  localStorage.removeItem(ACCOUNT_KEY);
  activeAccount.value = '';
  notify(broadcast);
}

export function syncCredentials() {
  if (localStorage.getItem(LOGOUT_KEY) || !credentials || credentials.account !== activeAccount.value || credentials.expiresAt <= Date.now()) return null;
  return credentials;
}

export async function logoutSession() {
  if (logoutFlight) return logoutFlight;
  clearTimeout(logoutRetry);
  // The HttpOnly cookie can only be removed by the server. Keep a persistent
  // local intent until revocation succeeds, including after an offline reload.
  localStorage.setItem(LOGOUT_KEY, '1');
  clearSession();
  logoutFlight = (async () => {
    const bootstrap: SessionData = await sessionRequest('/api/session');
    await sessionRequest('/api/logout', { method: 'POST', headers: { 'X-CSRF-Token': bootstrap.csrfToken } });
    localStorage.removeItem(LOGOUT_KEY);
    notify(true);
  })().catch(error => {
    retryPendingLogout();
    throw error;
  }).finally(() => { logoutFlight = null; });
  return logoutFlight;
}

export async function restoreSession() {
  migrateLegacyAccount();
  if (localStorage.getItem(LOGOUT_KEY)) {
    try { await logoutSession(); } catch { /* Retry on reconnect; never restore a pending logout. */ }
    return;
  }
  const epoch = generation;
  try {
    const data: SessionData = await sessionRequest('/api/session', {}, 3000);
    if (epoch !== generation || localStorage.getItem(LOGOUT_KEY)) return;
    if (data.authenticated) acceptSession(data, false);
    else {
      const changed = credentials !== null;
      credentials = null; sessionRole.value = '';
      // Session expiry does not discard the account's offline diary workspace.
      if (changed) { generation++; notify(); }
    }
  } catch { /* Cached writing remains usable offline. No credentials are persisted. */ }
}

window.addEventListener('storage', event => {
  if ([ACCOUNT_KEY, EVENT_KEY, LOGOUT_KEY].includes(event.key || '')) {
    generation++; credentials = null; sessionRole.value = '';
    activeAccount.value = localStorage.getItem(LOGOUT_KEY) ? '' : localStorage.getItem(ACCOUNT_KEY) || '';
    notify();
    if (event.key === EVENT_KEY) void restoreSession();
  }
});
window.addEventListener('online', () => { void restoreSession(); });
window.addEventListener('focus', () => { void restoreSession(); });
