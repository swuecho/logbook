import { ref } from 'vue';

const LOCK_KEY = 'LOGBOOK_TAB_LOCK';
const HEARTBEAT_MS = 2000;
const STALE_MS = 5000;

type LockPayload = {
  id: string;
  ts: number;
};

const isPrimaryTab = ref(true);
const tabId =
  typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`;

let heartbeatId = null;

const readLock = () => {
  const raw = localStorage.getItem(LOCK_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as LockPayload;
  } catch (error) {
    console.warn('Invalid tab lock payload:', error);
    return null;
  }
};

const writeLock = (payload: LockPayload) => {
  localStorage.setItem(LOCK_KEY, JSON.stringify(payload));
};

const isStale = (payload: LockPayload | null) =>
  !payload || Date.now() - payload.ts > STALE_MS;

const tryAcquireLock = () => {
  const current = readLock();
  if (isStale(current) || current?.id === tabId) {
    writeLock({ id: tabId, ts: Date.now() });
  }

  const updated = readLock();
  isPrimaryTab.value = updated?.id === tabId;
};

const releaseLock = () => {
  const current = readLock();
  if (current?.id === tabId) {
    localStorage.removeItem(LOCK_KEY);
  }
};

const startHeartbeat = () => {
  if (heartbeatId) return;
  heartbeatId = setInterval(() => {
    if (isPrimaryTab.value) {
      writeLock({ id: tabId, ts: Date.now() });
    } else {
      tryAcquireLock();
    }
  }, HEARTBEAT_MS);
};

const initTabLock = () => {
  if (typeof window === 'undefined') return;

  tryAcquireLock();
  startHeartbeat();

  window.addEventListener('storage', (event) => {
    if (event.key === LOCK_KEY) {
      tryAcquireLock();
    }
  });

  window.addEventListener('beforeunload', () => {
    releaseLock();
  });
};

export { initTabLock, isPrimaryTab };
