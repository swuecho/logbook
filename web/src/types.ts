// types.ts
export interface DiaryEntry {
        noteId: string;
        note: string;
        dirty?: boolean;
        updatedAt?: number;
        syncedAt?: number;
}
