import {
  Document,
  Text,
  Paragraph,
  Heading,
  Bold,
  Underline,
  Italic,
  Strike,
  Color,
  Highlight,
  Blockquote,
  Code,
  CodeBlock,
  BulletList,
  OrderedList,
  TaskList,
  Indent,
  LineHeight,
  FormatClear,
  History,
  Fullscreen,
  CodeView,
  Link,
  Iframe,
  Image,
  TextAlign,
} from 'element-tiptap';
import codemirror from 'codemirror';
import 'codemirror/lib/codemirror.css';
import 'codemirror/mode/xml/xml.js';
import 'codemirror/addon/selection/active-line.js';
import 'codemirror/addon/edit/closetag.js';

export const emptyDoc = () => ({
  type: 'doc',
  content: [],
});

const legacyNodeTypes = {
  todo_list: 'taskList',
  todo_item: 'taskItem',
  bullet_list: 'bulletList',
  ordered_list: 'orderedList',
  list_item: 'listItem',
  code_block: 'codeBlock',
  hard_break: 'hardBreak',
  horizontal_rule: 'horizontalRule',
};

export function normalizeTiptapDoc(value) {
  if (!value || typeof value !== 'object') return emptyDoc();
  if (!value.type) return emptyDoc();

  const normalizeNode = (node) => {
    if (!node || typeof node !== 'object') return node;

    const type = legacyNodeTypes[node.type] || node.type;
    const normalized = {
      ...node,
      type,
    };

    if (type === 'taskItem') {
      normalized.attrs = {
        ...(node.attrs || {}),
        checked: Boolean(node.attrs && (node.attrs.checked || node.attrs.done)),
        done: Boolean(node.attrs && (node.attrs.done || node.attrs.checked)),
      };
    }

    if (Array.isArray(node.content)) {
      normalized.content = node.content.map(normalizeNode);
    }

    return normalized;
  };

  return normalizeNode(value);
}

export function createExtensions() {
  return [
    Document,
    Text,
    Paragraph,
    Heading.configure({ levels: [1, 2, 3] }),
    Bold,
    Underline,
    Italic,
    Strike,
    Color,
    Highlight,
    Blockquote,
    Code,
    CodeBlock.configure({ bubble: true }),
    TaskList.configure({ bubble: true }),
    LineHeight,
    BulletList,
    OrderedList,
    Indent,
    TextAlign,
    Link,
    Image,
    Iframe,
    Fullscreen,
    FormatClear.configure({ bubble: true }),
    CodeView.configure({
      codemirror,
      codemirrorOptions: {
        styleActiveLine: true,
        autoCloseTags: true,
      },
    }),
    History,
  ];
}
