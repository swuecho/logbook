import axios from '../axiosConfig';
import { listLocalNotes } from './localStore.js';
import { activeAccount } from './session';


export const getDiaryIds = async (): Promise<string[]> => {
        const notes = await listLocalNotes(activeAccount.value);
        return notes.filter(note => Boolean(note.note)).map(note => note.noteId).sort().reverse();
};

export const getDiarySummaries = async () => {
        const response = await axios.get('/api/diary');
        return response.data;
};

export const searchDiary = async (query: string) => {
        const response = await axios.get('/api/diary/search', {
                params: { q: query }
        });
        return response.data;
};
