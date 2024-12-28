// types.ts
export interface DiaryEntry {
        noteId: string;
        note: string;
}
export interface QueuedRequest {
        url: string;
        method: 'PUT' | 'GET';
        data: any;
        resolve?: (value: any) => void;
        reject?: (reason?: any) => void;
}
