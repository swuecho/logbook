<template>
  <div class="content">
    <div class="nav">
      {{ time }}
      <a v-if="date != today" :href="'/view?date=' + today">Diary</a>
      <a href="content">
        <Icon :icon="icons.tableOfContents" />
        <Icon v-if="loading" icon="line-md:loading-alt-loop" />
      </a>
    </div>
    <div class="todo">
      <el-tiptap :content="content" :extensions="extensions" :readonly=true @onInit="onInit" :enableCharCount="false"></el-tiptap>
    </div>
  </div>
</template>
<script>
import moment from 'moment';
import { Icon } from '@iconify/vue2';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';

import {
  Doc,
  Text,
  Paragraph,
  Heading,
  Bold,
  Underline,
  Italic,
  Strike,
  Blockquote,
  Code,
  CodeBlock,
  ListItem,
  BulletList,
  OrderedList,
  TextAlign,
  Indent,
  LineHeight,
  TrailingNode,
  TodoItem,
  TodoList,
  History,
  Fullscreen,
  CodeView,
  Link,
  Iframe,
  Image
} from "element-tiptap";

import codemirror from 'codemirror';
import 'codemirror/lib/codemirror.css'; // import base style
import 'codemirror/mode/xml/xml.js'; // language
import 'codemirror/addon/selection/active-line.js'; // require active-line.js
import 'codemirror/addon/edit/closetag.js'; // autoCloseTags

export default {
  components: {
    Icon,
  },
  props: {
    date: String
  },
  data() {
    return {
      now: moment(),
      loading: true,
      timeFormat: 'h:mm:ss a',
      last_note_json: null,
      content: null,
      icons: {
        tableOfContents,
      },
      extensions: [
        new Doc(),
        new Text(),
        new Paragraph(),
        new Heading({ level: 3 }),
        new Bold(),
        new Underline({ bubble: true }),
        new Italic(),
        new Strike(),
        new Blockquote({ bubble: true }),
        new Code(),
        new CodeBlock({ bubble: true }),
        new TodoItem(),
        new TodoList(),
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
        new CodeView({
          codemirror,
          codemirrorOptions: {
            styleActiveLine: true,
            autoCloseTags: true,
          },
        }),
        new History()
      ]
    };
  },
  computed: {
    today() {
      return this.now.format('YYYYMMDD');
    },
    time() {
      return this.now.format(this.timeFormat);
    }

  },
  methods: {
    onInit({ editor }) {
      let app = this;
      // this.date = this.$route.query.date;
      let date = this.date;
      app.loading = true;
      this.axios
        .get(`/api/todo`)
        .then(function (response) {
          // handle success
          app.loading = false;
          let last_note = response.data;
          if (last_note) {
            app.last_note= last_note;
            editor.setContent(last_note);
          }
        })
        .catch(function (error) {
          // handle error
          console.log(error);
        })
        .then(function () {
          // always executed
        });
    }
  }
};
</script>

<style>
.todo .el-tiptap-editor__content {
  border-bottom: 1px solid #ebeef5 !important;
  border-top: 1px solid #ebeef5 !important;
  border-radius: 5px !important;
}
.todo .el-tiptap-editor__menu-bar {
  display: none;
}

.todo .el-tiptap-editor__footer {
  display: none;
}
.content {
  max-width: 65rem;
  margin: auto;
}

.nav {
  margin: 1em 1em 1rem 1em;
  display: flex;
  justify-content: space-between;
}

pre code {
  font-family: "Fira Code", Courier, Monaco, monospace;
}

.nav a {
  display: inline-block;
  text-decoration: none;
  border-radius: 5%;
}

/* Change the link color on hover */
.nav a:hover {
  background-color: rgb(223, 214, 214);
  color: white;
}
</style>
