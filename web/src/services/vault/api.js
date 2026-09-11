// Dedicated online-only transport. No query cache, diary sync or Axios error logging.
export function vaultApi(token, signal) {
  async function request(method, body) {
    const response = await fetch('/api/vault', {
      method, signal, cache: 'no-store', credentials: 'omit', redirect: 'error',
      headers: { Authorization: `Bearer ${token}`, ...(body ? { 'Content-Type': 'application/json' } : {}) },
      body: body ? JSON.stringify(body) : undefined,
    });
    if (method === 'GET' && response.status === 404) return null;
    if (response.status === 401) throw new Error('Your login expired. Sign in again.');
    if (response.status === 409) throw new Error('Another session changed this vault. Your edit was not saved. Export an encrypted backup if needed, then lock and unlock before retrying.');
    if (!response.ok) throw new Error('Could not save or load the vault. Check your connection and try again.');
    return response.json();
  }
  return { get: () => request('GET'), save: (envelope, baseRevision) => request('PUT', { envelope: JSON.stringify(envelope), baseRevision }) };
}
