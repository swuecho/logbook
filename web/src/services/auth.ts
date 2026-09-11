import { logoutSession, sessionRequest } from './session';

async function authenticate(path: string, username: string, password: string) {
  if (localStorage.getItem('LOGBOOK_LOGOUT_PENDING')) await logoutSession();
  const bootstrap = await sessionRequest('/api/session');
  return sessionRequest(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-Token': bootstrap.csrfToken },
    body: JSON.stringify({ username, password }),
  });
}
export const loginUser = (username: string, password: string) => authenticate('/api/login', username, password);
export const registerUser = (username: string, password: string) => authenticate('/api/register', username, password);
export const logoutUser = logoutSession;
