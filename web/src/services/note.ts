import { activeAccount } from './session';
import { getLocalNote, saveLocalNote, resolveLocalConflict, editCombinedConflict, cancelCombinedConflict } from './localStore.js';
import { notifyLocalChange, scheduleSync, refreshRemoteNote } from './sync';
import type { DiaryEntry } from '../types';

export async function saveNote(note: DiaryEntry) {
  const account = note.account || activeAccount.value;
  if (!account) throw new Error('Sign in once before saving entries on this device.');
  const saved = await saveLocalNote(account, note.noteId, note.note, note.previousNote);
  notifyLocalChange();
  scheduleSync();
  return saved;
}

export async function fetchNote(noteId: string): Promise<DiaryEntry> {
  const account = activeAccount.value;
  if (!account) throw new Error('No local account is open.');
  const note = await getLocalNote(account, noteId);
  // The editor initiates remote refresh separately; its query only touches local storage.
  return note || { account, noteId, note: '', dirty: false, unknown: true };
}

export async function resolveConflict(account: string, noteId: string, choice: 'local' | 'remote' | 'combine' | 'cancel', expected: { note: string; revision: string }) {
  if (choice === 'combine') await editCombinedConflict(account, noteId, expected);
  else if (choice === 'cancel') await cancelCombinedConflict(account, noteId, expected);
  else await resolveLocalConflict(account, noteId, choice, expected);
  notifyLocalChange();
  scheduleSync(0);
}

export { refreshRemoteNote };
