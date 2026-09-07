export interface DiaryEntry {
  account?: string;
  noteId: string;
  note: string;
  dirty?: boolean;
  updatedAt?: number;
  syncedAt?: number;
  localVersion?: number;
  serverRevision?: string;
  unknown?: boolean;
  conflict?: { noteId: string; note: string; revision: string };
}
