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
import ListItem from '@tiptap/extension-list-item';
import TaskItem from '@tiptap/extension-task-item';
import codemirror from 'codemirror';
import 'codemirror/lib/codemirror.css';
import 'codemirror/mode/xml/xml.js';
import 'codemirror/addon/selection/active-line.js';
import 'codemirror/addon/edit/closetag.js';

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
    TaskItem.configure({ nested: true }),
    TaskList.configure({ bubble: true }),
    LineHeight,
    ListItem,
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
