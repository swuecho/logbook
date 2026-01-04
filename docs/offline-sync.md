# Offline Sync Summary

This frontend uses an offline-first model where IndexedDB is the source of truth and the server is eventually consistent.

## Flow
- Every edit writes to IndexedDB immediately.
- Notes are marked `dirty` with `updatedAt` timestamps on save.
- When online, dirty notes are synced to the API and then marked `dirty=false` with `syncedAt`.
- Sync runs on startup (if online) and whenever the browser fires the `online` event.
- If a note is dirty, `fetchNote` does not overwrite it with server data to avoid clobbering unsynced edits.

## Key Files
- `web/src/services/note.ts`: IndexedDB storage, dirty tracking, and sync logic.
- `web/src/types.ts`: `DiaryEntry` shape including `dirty`, `updatedAt`, and `syncedAt`.

## Sync Logic (Mermaid)
```mermaid
flowchart TD
  A[User edits note] --> B[saveNote]
  B --> C[Write to IndexedDB]
  C --> D[Mark dirty + updatedAt]
  D --> E{navigator.onLine?}

  E -- No --> F[Return local note\n(dirty stays true)]
  E -- Yes --> G[Try syncNote]

  G --> H{Sync success?}
  H -- Yes --> I[Mark dirty=false\nsyncedAt=now]
  H -- No --> J[Log error\nLeave dirty=true]

  K[App start or online event] --> L[syncDirtyNotes]
  L --> M[Load notes from IndexedDB]
  M --> N[Filter dirty notes]
  N --> O[Sort by updatedAt]
  O --> P[For each note -> syncNote]
  P --> H
```

## Notes
- Failed syncs keep notes dirty for later retry.
- Conflict resolution is not implemented; newest local edits win until synced.
- There is no retry backoff yet; repeated failures will retry on next online event or app start.

## Future Improvements
- Add retry backoff with jitter to avoid hammering the server during outages.
- Introduce conflict detection (e.g., server-side versioning) and surface merge options to the user.
