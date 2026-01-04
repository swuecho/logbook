// services/api.ts
import axios from '../axiosConfig';

import { openDB, DBSchema, IDBPDatabase } from 'idb';
import type { DiaryEntry } from '../types.ts';
import { isUnauthorized, getApiErrorMessage } from './apiError';

interface MyDB extends DBSchema {
        notes: {
                key: string;
                value: DiaryEntry;
                indexes: { 'noteId': string }
        }
}

let db: IDBPDatabase<MyDB> | null = null;

const DB_NAME = 'logbook-db';

const openDatabase = async () => {
        if (db) return db;
        db = await openDB<MyDB>(DB_NAME, 1, {
                upgrade(db) {
                        db.createObjectStore('notes', { keyPath: 'noteId' })
                }
        });
        return db;
}

let isSyncing = false;


const normalizePayload = (note: DiaryEntry) => ({
        noteId: note.noteId,
        note: note.note
});

const axiosRequest = async (url: string, method: 'PUT' | 'GET', data: any) => {
        try {
                // add delay to simulate network latency
                // await new Promise(resolve => setTimeout(resolve, 500));
                const response = await axios({ url, method, data });
                return response.data
        }
        catch (error) {
                throw error
        }
}

const syncNote = async (note: DiaryEntry) => {
        const response = await axiosRequest(`/api/diary/${note.noteId}`, 'PUT', normalizePayload(note));
        const db = await openDatabase();
        await db.put('notes', {
                ...note,
                dirty: false,
                syncedAt: Date.now()
        });
        return response;
};

const syncDirtyNotes = async () => {
        if (isSyncing || !navigator.onLine) {
                return;
        }

        isSyncing = true;
        try {
                const db = await openDatabase();
                const notes = await db.getAll('notes');
                const dirtyNotes = notes
                        .filter((note) => note.dirty)
                        .sort((a, b) => (a.updatedAt || 0) - (b.updatedAt || 0));

                for (const note of dirtyNotes) {
                        try {
                                await syncNote(note);
                        } catch (error) {
                                console.error(getApiErrorMessage(error, 'Failed to sync note.'));
                        }
                }
        } finally {
                isSyncing = false;
        }
};


const saveNote = async (note: DiaryEntry) => {
        const db = await openDatabase()
        console.log("saving note", note.noteId);
        if (typeof note.note === 'string') {
                try {
                        console.log(JSON.parse(note.note))
                } catch (error) {
                        console.warn('Note content is not valid JSON.', error);
                }
        }
        const localNote = {
                ...note,
                dirty: true,
                updatedAt: Date.now()
        };
        await db.put('notes', localNote);

        if (navigator.onLine) {
                try {
                        return await syncNote(localNote);
                } catch (error) {
                        console.error(getApiErrorMessage(error, 'Failed to sync note.'));
                        console.info('Saved locally; will sync when online.');
                        return localNote;
                }
        }

        return localNote;
};

const fetchNote = async (noteId: string): Promise<DiaryEntry | undefined> => {
        console.log("fetching note", noteId);
        const db = await openDatabase();
        let cachedNote = await db.get('notes', noteId);
        if (navigator.onLine && !cachedNote?.dirty) {
                try {
                        const response = await axiosRequest(`/api/diary/${noteId}`, 'GET', null);
                        if (response) {
                                cachedNote = response;
                                db.put('notes', {
                                        ...response,
                                        dirty: false,
                                        syncedAt: Date.now()
                                });
                        }
                } catch (error) {
                        console.log("online but server error", error);
                        if (isUnauthorized(error)) {
                                console.error("Note not found on server");
                                throw error;
                        }
                        console.error(getApiErrorMessage(error, "Network error"));
                }
        }
        return cachedNote
};

if (typeof window !== 'undefined') {
        window.addEventListener('online', () => {
                syncDirtyNotes();
        });
        if (navigator.onLine) {
                syncDirtyNotes();
        }
}

export { saveNote, fetchNote };
