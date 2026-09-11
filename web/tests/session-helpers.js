import { expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';
export const origin = 'http://127.0.0.1:9197';
export function cookieValue(response, name) {
  const header = response.headersArray().find(h => h.name.toLowerCase() === 'set-cookie' && h.value.startsWith(name + '='));
  return header?.value.split(';')[0].slice(name.length + 1);
}
export async function registerSession(request, username = `${randomUUID()}@example.test`, password = randomUUID()) {
  // Explicit empty session cookie isolates each enrollment from the request jar.
  const bootstrap = await request.get('/api/session', { headers: { Cookie: '__Host-logbook-session=' } });
  expect(bootstrap.ok()).toBe(true);
  const preauth = cookieValue(bootstrap, '__Host-logbook-csrf');
  const csrf = (await bootstrap.json()).csrfToken;
  const response = await request.post('/api/register', {
    headers: { Cookie: `__Host-logbook-csrf=${preauth}`, Origin: origin, 'X-CSRF-Token': csrf },
    data: { username, password },
  });
  expect(response.status()).toBe(201);
  const session = await response.json();
  const jwt = cookieValue(response, '__Host-logbook-session');
  expect(jwt).toBeTruthy();
  expect(session.accessToken).toBeUndefined();
  return {
    session, jwt, username, password,
    cookie: { name: '__Host-logbook-session', value: jwt, domain: '127.0.0.1', path: '/', secure: true, httpOnly: true, sameSite: 'Strict' },
    headers: { Cookie: `__Host-logbook-session=${jwt}`, Origin: origin, 'X-CSRF-Token': session.csrfToken },
  };
}
