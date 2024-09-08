import {
        Doc, Text, Paragraph, Heading, Bold, Underline, Italic, Strike, TextColor, TextHighlight,
        Blockquote, Code, CodeBlock, ListItem, BulletList, OrderedList, FormatClear,
        TextAlign, Indent, LineHeight, TrailingNode, TodoItem, TodoList,
        History, Fullscreen, CodeView, Link, Iframe, Image, 
} from "element-tiptap";

import codemirror from 'codemirror';
import 'codemirror/lib/codemirror.css';
import 'codemirror/mode/xml/xml.js';
import 'codemirror/addon/selection/active-line.js';
import 'codemirror/addon/edit/closetag.js';

export function createExtensions() {
        return [
                new Doc(),
                new Text(),
                new Paragraph(),
                new Heading({ level: 3 }),
                new Bold(),
                new Underline(),
                new Italic(),
                new Strike(),
                new TextColor(),
                new TextHighlight(),
                new Blockquote(),
                new Code(),
                new CodeBlock({ bubble: true }),
                new TodoItem(),
                new TodoList({ bubble: true }),
                new LineHeight(),
                new ListItem(),
                new BulletList(),
                new OrderedList(),
                new Indent(),
                new TrailingNode(),
                new TextAlign(),
                new Link(),
                new Image(),
                new Iframe(),
                new Fullscreen(),
                new FormatClear({ bubble: true }),
                new CodeView({
                        codemirror,
                        codemirrorOptions: {
                                styleActiveLine: true,
                                autoCloseTags: true,
                        },
                }),
                new History()
        ];
}