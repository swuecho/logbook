// services/api.ts
import axios from '../axiosConfig.js';

import { openDB, DBSchema, IDBPDatabase } from 'idb';
import type { DiaryEntry, QueuedRequest } from '../types.ts';

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

const requestQueue: QueuedRequest[] = [];
let isSyncing = false;


// Modified process queue to only process latest state
const processQueue = async () => {
        if (isSyncing || !navigator.onLine) {
                return;
        }

        isSyncing = true;

        try {
                // Group requests by noteId and get only the latest one for each note
                const latestRequests = requestQueue.reduce((acc, curr) => {
                        if (!acc[curr.noteId] || acc[curr.noteId].timestamp < curr.timestamp) {
                                acc[curr.noteId] = curr;
                        }
                        return acc;
                }, {} as Record<string, QueuedRequest>);

                // Convert back to array and sort by timestamp
                const processableRequests = Object.values(latestRequests)
                        .sort((a, b) => a.timestamp - b.timestamp);

                // Clear the queue since we're only processing latest states
                requestQueue.length = 0;

                // Process each latest request
                for (const request of processableRequests) {
                        try {
                                // Get latest state from IndexedDB
                                const db = await openDatabase();
                                const latestNote = await db.get('notes', request.noteId);

                                if (latestNote) {
                                        // Use the latest note content from IndexedDB instead of queued data
                                        const response = await axios({
                                                url: request.url,
                                                method: request.method,
                                                data: latestNote
                                        });

                                        if (response.status >= 200 && response.status < 300) {
                                                request.resolve && request.resolve(response.data);
                                        } else {
                                                console.error('Request failed with status: ' + response.status);
                                                // Re-queue only the latest state
                                                requestQueue.push({
                                                        ...request,
                                                        data: latestNote,
                                                        timestamp: Date.now()
                                                });
                                                request.reject && request.reject('Request failed');
                                        }
                                }
                        } catch (error) {
                                console.error("Request failed", error);
                                // Re-queue the request
                                requestQueue.push({
                                        ...request,
                                        timestamp: Date.now()
                                });
                                request.reject && request.reject('Request failed');
                        }
                }
        } finally {
                isSyncing = false;
                if (requestQueue.length > 0) {
                        // If there are requests left, retry after a delay
                        setTimeout(processQueue, 1000);
                }
        }
};


const enqueueRequest = (url: string, method: 'PUT' | 'GET', data: any): Promise<any> => {
        return new Promise((resolve, reject) => {
                requestQueue.push({
                        url, method, data,
                        timestamp: Date.now(),
                        noteId: data.noteId,
                        resolve, reject
                });
                processQueue();
        });

};

const axiosRequest = async (url: string, method: 'PUT' | 'GET', data: any) => {
        try {
                const response = await axios({ url, method, data });
                return response.data
        }
        catch (error) {
                throw error
        }
}

const wraperApiRequest = async (url: string, method: 'PUT' | 'GET', data: any) => {
        if (navigator.onLine) {
                axiosRequest(url, method, data);
        }
        else {
                return enqueueRequest(url, method, data);
        }
};


const saveNote = async (note: DiaryEntry) => {
        const db = await openDatabase()
        console.log("saving note", note.noteId);
        console.log(JSON.parse(note.note))
        await db.put('notes', note);

        // if user online, immediately save to the server
        return wraperApiRequest(`/api/diary/${note.noteId}`, 'PUT', note);
};

const fetchNote = async (noteId: string): Promise<DiaryEntry | undefined> => {
        console.log("fetching note", noteId);
        const db = await openDatabase();
        let cachedNote = await db.get('notes', noteId);
        if (navigator.onLine) {
                try {
                        const response = await axiosRequest(`/api/diary/${noteId}`, 'GET', null);
                        if (response) {
                                cachedNote = response;
                                db.put('notes', response);
                        }
                } catch (error) {
                        console.log("fetch note failed, using local", error);
                }
        }
        return cachedNote
};

window.addEventListener('online', () => {
        processQueue(); //process the queue on network status change
});

export { saveNote, fetchNote };

