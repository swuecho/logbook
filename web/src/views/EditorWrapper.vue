<template>
  <div class="content">
    <div class="nav">
      {{ time }}
      <a href="#">{{ date }}</a>
      <a v-if="date != today" :href="'/view?date=' + today">Today</a>
      <a href="content">
        <Icon :icon="icons.tableOfContents" />
      </a>
    </div>
    <div class="editor">
      <el-tiptap :content="content" :extensions="extensions" @onUpdate="onUpdate" @onInit="onInit"></el-tiptap>
    </div>
  </div>
</template>
<script>
import { debounce } from "debounce";
import moment from 'moment';
import { Icon } from '@iconify/vue2';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';

import { DB_URL } from '@/config.js'

var base_url = DB_URL;

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
  created() {

    // set interval.
    // this update date `now` and trigger `today` , `time`
    // eslint-disable-next-line no-unused-vars
    var interval = setInterval(() => this.now = moment(), 1000);
    // this.date = this.$route.query.date;
    /*
    let app = this;
    let date = this.date;
    this.axios
      .get(`${base_url}/diary/${date}`)
      .then(function(response) {
        // handle success
        console.log(response.data)
        let last_note = response.data;
        console.log(last_note)
        if (last_note) {
          console.log(last_note.note)
          let last_note_json = JSON.parse(last_note.note);
          // set content should trigger OnUpdate?
          app.last_note_json = last_note_json;
          //editor.setContent(last_note_json);
        } else {
          app.$message({message: `welcome to start your new diary of ${date}` })
        }
      })
      .catch(function(error) {
        // handle error
        console.log(error)
        app.$message({message: "error send request to db" + error })
      })
      .finally(function() {
        // always executed
      });
      */
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
    update_doc(output, options) {
      const { getJSON, getHTML } = options;
      console.log(this.date);
      this.json = getJSON();
      this.axios
        .put(
          `${base_url}/diary/${this.date}`,
          {
            id: this.date,
            note: JSON.stringify(this.json)
          },
        )
        .then(function (response) {
          console.log(response);
        })
        .catch(function (error) {
          console.log(error);
        });
    },
    onUpdate(output, options) {
      debounce(() => {
        this.update_doc(output, options);
      }, 500);
    },
    onInit({ editor }) {
      let app = this;
      // this.date = this.$route.query.date;
      let date = this.date;
      this.axios
        .get(`${base_url}/diary/${date}`)
        .then(function (response) {
          // handle success
          let last_note = response.data;
          if (last_note) {
            let last_note_json = JSON.parse(last_note.note);
            // set content should trigger OnUpdate?
            console.log(last_note_json);
            app.last_note_json = last_note_json;
            editor.setContent(last_note_json);
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

<style lang="scss">
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
