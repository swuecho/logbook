// Keep document nodes intact; the user reviews duplicated passages in the editor.
export function combineNotes(local, remote) {
  const parse = note => {
    const doc = note ? JSON.parse(note) : { type: 'doc', content: [] };
    if (doc.type !== 'doc' || (doc.content !== undefined && !Array.isArray(doc.content))) {
      throw new Error('This version could not be opened. Export the device backup to recover its original data.');
    }
    return doc.content || [];
  };
  return JSON.stringify({ type: 'doc', content: [...parse(local), ...parse(remote)] });
}

// Text-only previews never render untrusted document HTML or load remote media.
export function readableNote(note) {
  if (!note) return '(Empty entry)';
  try {
    const text = node => {
      if (node?.type === 'text') return node.text || '';
      if (node?.type === 'hardBreak' || node?.type === 'hard_break') return '\n';
      if (node?.type === 'image') return `[Image: ${node.attrs?.alt || node.attrs?.src || 'image'}]\n`;
      if (node?.type === 'iframe') return `[Embedded content: ${node.attrs?.src || ''}]\n`;
      const children = (node?.content || []).map(text).join('');
      if (['taskItem', 'todo_item'].includes(node?.type)) return `[${node.attrs?.checked || node.attrs?.done ? 'x' : ' '}] ${children}`;
      if (['paragraph', 'heading', 'codeBlock', 'code_block', 'listItem', 'list_item'].includes(node?.type)) return `${children}\n`;
      return children;
    };
    return text(JSON.parse(note)).trim() || '(Empty entry)';
  } catch { return note; }
}
